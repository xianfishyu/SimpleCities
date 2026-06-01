using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public class RoadNetwork : ISaveable
{
    private readonly Dictionary<int, Junction> _junctions = new();
    private readonly Dictionary<int, Segment> _segments = new();
    private readonly Dictionary<int, Road> _roads = new();

    /// <summary>仅存 Junction 位置（不含 waypoint）</summary>
    private readonly Dictionary<Vector2, int> _posToJunctionID = new();

    /// <summary>Segment 占用的所有格点（端点 + waypoints）→ Segment.ID，用于按位置反查 Segment（拆除工具）</summary>
    private readonly Dictionary<Vector2, int> _posToSegmentID = new();

    private int _nextID;
    private int NextID() => _nextID++;

    // cellSize 已由 GridSystem 全局管理，不再本地持有字段。
    // 防止 TryMergeAtJunction 内部 RemoveSegment 时递归触发末尾的合并降级，造成级联误并
    private bool _inMergeOperation = false;

    public string SaveFileName => "road_network";

    public event Action<Segment>? SegmentAdded;
    public event Action<Segment>? SegmentRemoved;

    /// <summary>整网从存档加载完成后触发，通知渲染器等重建显示</summary>
    public event Action? NetworkReloaded;

    /// <summary>
    /// 铺设一条道路。from/to 为两端端点，waypoints 为中间途经格点（共 8 方向相邻）。
    /// 若路径与已有 Segment 相交（端点撞 Junction、或穿过别的 Segment 的中段 waypoint），
    /// 自动在交点创建 Junction，并将旧 Segment + 新 Segment 按交点切分成多段。
    ///
    /// extendRoadID（占位接口，本期未实现）：未来传入已有 RoadID 可扩展那条 Road；
    /// 当前阶段不论传什么都新建一个 Road。
    ///
    /// 返回值：RoadID（成功）；-1（失败，路径不合法 / 完全重叠 / 自环 / 或本次未产生任何 Segment）。
    /// </summary>
    public int AddRoad(Vector2 from, Vector2 to, Vector2[] waypoints, float cellSize, int? extendRoadID = null)
    {
        // extendRoadID 现阶段忽略（占位接口）。未来此处取已有 Road 复用。
        _ = extendRoadID;

        // 已在路网上的点不 snap。若 from 是半格点，to/waypoints 也不 snap
        // （否则 snap 后位移不是合法 8 方向：如(150,150)→(200,200)位移(2,2)）。
        bool fromOnRoad = IsOnRoadPoint(from);
        bool toOnRoad   = IsOnRoadPoint(to);
        bool skipSnap = fromOnRoad; // 半格起点→整条路都不 snap
        GD.Print($"[ADDROAD] raw from=({from.X:F0},{from.Y:F0}) onRoad={fromOnRoad} to=({to.X:F0},{to.Y:F0}) onRoad={toOnRoad} skipSnap={skipSnap}");
        if (!skipSnap) from = SnapToGrid(from, cellSize);
        if (!skipSnap) to   = SnapToGrid(to, cellSize);
        GD.Print($"[ADDROAD] after snap: from=({from.X:F0},{from.Y:F0}) to=({to.X:F0},{to.Y:F0})");
        if (!skipSnap)
        {
            for (int i = 0; i < waypoints.Length; i++)
                waypoints[i] = SnapToGrid(waypoints[i], cellSize);
        }

        // 自环显式拒绝（Junction/邻接结构尚不支持）
        if (from == to) return -1;

        // 完整路径（含两端）。AddRoad 调用方保证已 8 方向相邻。
        var path = new List<Vector2>(waypoints.Length + 2) { from };
        path.AddRange(waypoints);
        path.Add(to);

        // 校验 8 方向（用 AnyLength 兼容半格步长；FromDisplacement 的 Banker's rounding 会误判）
        for (int i = 0; i < path.Count - 1; i++)
        {
            if (DirectionUtil.FromDisplacementAnyLength(path[i], path[i + 1]) == null)
            {
                GD.Print($"[ADDROAD] REJECT: non-8dir step[{i}] ({path[i].X:F0},{path[i].Y:F0})->({path[i+1].X:F0},{path[i+1].Y:F0})");
                return -1;
            }
        }

        // 路径自身不能含重复格点
        if (path.Distinct().Count() != path.Count) { GD.Print("[ADDROAD] REJECT: duplicate points in path"); return -1; }

        // 完全重叠预检
        if (IsPathFullyCovered(path)) { GD.Print("[ADDROAD] REJECT: path fully covered"); return -1; }

        // X 形几何交叉处理：找出新路径每相邻线段与现有 Segment 任一相邻线段的内部交点，
        // 在每个交点处对现有 Segment 做"位置劈分"（位置可以是非格点的"半格 Junction"），
        // 同时把交点作为新路径上的额外锚点（也会在那里生成 Junction）。
        // 网格只是绘制简化，Junction 位置不必落在 snap 格点上。
        path = ResolveInteriorCrossings(path, cellSize);

        // 本次操作的新 Road
        var newRoad = new Road(NextID());
        _roads[newRoad.ID] = newRoad;

        // 第一步：劈开所有被新路中段穿过的旧 Segment。
        // 共线重叠跳过：若新路接近该格点的方向与已有 Segment 在该格点处的延伸方向一致，
        // 说明新路与已有 Segment 共线，无需劈分（避免产生冗余 Junction）。
        for (int i = 1; i < path.Count - 1; i++)
        {
            var p = path[i];
            if (HasJunctionAt(p)) continue;
            int oldSegmentID = FindSegmentAt(p);
            if (oldSegmentID < 0) continue;

            if (IsApproachColinearWithSegment(p, path[i - 1], oldSegmentID))
                continue; // 共线重叠，跳过劈分（十字路口不共线 → 仍会劈分）

            SplitSegmentAtWaypoint(oldSegmentID, p, cellSize);
        }

        // 新路两端也可能撞到旧 Segment 中段 waypoint。
        // 共线端点跳过劈分；非共线则劈分。半格 waypoint 不在 FindSegmentAt 字典中，
        // 通过几何扫 waypoint 补查。
        if (!HasJunctionAt(from))
        {
            int sid = FindSegmentAtIncludingHalfGrid(from);
            // from 端点检测 departure 方向（from→path[1]），非 approach 方向
            if (sid >= 0 && !IsApproachColinearWithSegment(path[1], from, sid))
                SplitSegmentAtWaypoint(sid, from, cellSize);
        }
        if (!HasJunctionAt(to))
        {
            int sid = FindSegmentAtIncludingHalfGrid(to);
            if (sid >= 0 && !IsApproachColinearWithSegment(to, path[path.Count - 2], sid))
                SplitSegmentAtWaypoint(sid, to, cellSize);
        }

        // 第二步：按 path 上所有 Junction 位置把新路切成多段。
        var splitIdx = new List<int> { 0 };
        for (int i = 1; i < path.Count - 1; i++)
        {
            if (IsAnyJunctionAt(path[i]))
                splitIdx.Add(i);
        }
        splitIdx.Add(path.Count - 1);

        // 拓扑去重：若新路从已有 Junction 起沿已有 Segment 共线延伸，
        // 截断到该 Junction——重叠部分由已有路的 Segment 覆盖。
        // 避免同一条物理路径上铺两条重叠 Segment（拓扑错误）。
        if (splitIdx.Count >= 3)
        {
            for (int s = 0; s < splitIdx.Count - 1; s++)
            {
                int a = splitIdx[s];
                if (!HasJunctionAt(path[a])) continue;
                bool allColinear = true;
                for (int t = s; t < splitIdx.Count - 1; t++)
                {
                    int ta = splitIdx[t];
                    int tb = splitIdx[t + 1];
                    int sid = FindSegmentAt(path[ta]);
                    if (sid < 0 || !IsApproachColinearWithSegment(path[tb], path[ta], sid))
                    { allColinear = false; break; }
                }
                if (allColinear && s > 0)
                {
                    // 保留 splitIdx[0..s]，去掉后面的共线段
                    while (splitIdx.Count > s + 1) splitIdx.RemoveAt(splitIdx.Count - 1);
                    break;
                }
            }
        }

        // 第三步：按 splitIdx 相邻对生成各段 Segment（全部归属 newRoad）。
        bool anyAdded = false;
        // 快照"接入前 ConnectionCount"用于合并触发（仅 1→2 触发）
        var preConnectionSnapshot = new Dictionary<Vector2, int>();
        for (int s = 0; s < splitIdx.Count - 1; s++)
        {
            int a = splitIdx[s];
            int b = splitIdx[s + 1];
            var segFrom = path[a];
            var segTo = path[b];
            if (!preConnectionSnapshot.ContainsKey(segFrom))
                preConnectionSnapshot[segFrom] = GetJunctionAt(segFrom)?.ConnectionCount ?? 0;
            if (!preConnectionSnapshot.ContainsKey(segTo))
                preConnectionSnapshot[segTo] = GetJunctionAt(segTo)?.ConnectionCount ?? 0;

            var segWps = new Vector2[b - a - 1];
            for (int i = 0; i < segWps.Length; i++) segWps[i] = path[a + 1 + i];
            if (AddSegment(segFrom, segTo, segWps, newRoad.ID, cellSize))
                anyAdded = true;
        }

        // 第四步：合并降级（仅 1→2）。
        // 经本次操作触及的 Junction：接入前 == 1 且 接入后 == 2 → 合并两侧 Segment。
        // 分裂场景产生的中间 Junction 接入前 ConnectionCount >= 2，不会被合并 → 不级联。
        foreach (var kv in preConnectionSnapshot.ToList())
        {
            if (kv.Value != 1) continue;
            var j = GetJunctionAt(kv.Key);
            if (j == null) continue;
            if (j.ConnectionCount != 2) continue;
            TryMergeAtJunction(j.ID, cellSize);
        }

        if (!anyAdded)
        {
            // newRoad 没有 Segment（被全部跳过）→ 清掉 Road 占位，返回 -1
            _roads.Remove(newRoad.ID);
            return -1;
        }
        // newRoad 可能在合并后变空（被并入另一 Road）→ 清掉
        if (_roads.TryGetValue(newRoad.ID, out var maybeEmpty) && maybeEmpty.IsEmpty)
            _roads.Remove(newRoad.ID);

        return newRoad.ID;
    }

    /// <summary>
    /// 判断路径上每对相邻格点是否都已被某条现有 Segment 作为相邻格点覆盖。
    /// </summary>
    private bool IsPathFullyCovered(List<Vector2> path)
    {
        if (path.Count < 2) return false;
        for (int i = 0; i < path.Count - 1; i++)
        {
            if (!IsSubSegmentCoveredByAnySegment(path[i], path[i + 1]))
                return false;
        }
        return true;
    }

    /// <summary>
    /// 判断 (a, b) 这一对相邻格点是否被现有所有 Segment 的几何并集"完全覆盖"。
    /// 不要求是某条 Segment 的相邻格点对——X 交叉切开后的两段拼起来仍构成完全覆盖。
    /// 算法：收集与 (a,b) 共线的所有现有子线段，把它们投影到 (a,b) 的参数轴 [0,1]，
    /// 区间并起来若覆盖 [0,1] 则视为完全覆盖。
    /// </summary>
    private bool IsSubSegmentCoveredByAnySegment(Vector2 a, Vector2 b)
    {
        Vector2 d = b - a;
        float dLenSq = d.LengthSquared();
        if (dLenSq < 1e-8f) return false;

        // 收集所有与 (a,b) 共线的现有 Segment 子线段，投影到 [0,1] 参数轴
        var intervals = new List<(float lo, float hi)>();
        foreach (var (q1, q2) in CollectExistingSubSegments())
        {
            // 共线判定：q1, q2 都在 (a,b) 的无限延长线上
            if (!IsPointOnInfiniteLine(a, b, q1)) continue;
            if (!IsPointOnInfiniteLine(a, b, q2)) continue;
            // 投影参数
            float t1 = ProjectParam(a, b, q1);
            float t2 = ProjectParam(a, b, q2);
            float lo = Mathf.Min(t1, t2);
            float hi = Mathf.Max(t1, t2);
            // 与 [0,1] 取交
            lo = Mathf.Max(lo, 0f);
            hi = Mathf.Min(hi, 1f);
            if (hi - lo > 1e-4f) intervals.Add((lo, hi));
        }

        if (intervals.Count == 0) return false;

        // 区间合并 + 检查是否覆盖 [0, 1]
        intervals.Sort((x, y) => x.lo.CompareTo(y.lo));
        float curHi = 0f;
        foreach (var (lo, hi) in intervals)
        {
            if (lo > curHi + 1e-4f) return false; // 出现空隙
            if (hi > curHi) curHi = hi;
        }
        return curHi >= 1f - 1e-4f;
    }

    private static bool IsPointOnInfiniteLine(Vector2 a, Vector2 b, Vector2 p)
    {
        Vector2 ab = b - a;
        Vector2 ap = p - a;
        float cross = ab.X * ap.Y - ab.Y * ap.X;
        // 与线段长度成比例的容差
        float scale = Mathf.Max(ab.LengthSquared(), 1f);
        return cross * cross < 1e-4f * scale;
    }

    private static float ProjectParam(Vector2 a, Vector2 b, Vector2 p)
    {
        Vector2 ab = b - a;
        Vector2 ap = p - a;
        return (ap.X * ab.X + ap.Y * ab.Y) / ab.LengthSquared();
    }


    /// <summary>
    /// 内部：添加单段 Segment（两端必须是同一段直线、之间无 Junction）。
    /// 不在此处对"两端 Junction 之间已有 Segment"做查重——查重已由 AddRoad 顶层 IsPathFullyCovered 完成。
    /// 这里允许同一对 Junction 之间存在多条不同路径的 Segment（多重边）。
    /// </summary>
    private bool AddSegment(Vector2 from, Vector2 to, Vector2[] waypoints, int roadID, float cellSize)
    {
        if (from == to) return false;

        var pts = new Vector2[waypoints.Length + 2];
        pts[0] = from;
        for (int i = 0; i < waypoints.Length; i++) pts[i + 1] = waypoints[i];
        pts[^1] = to;

        // 8 方向校验：
        //   首段 (from → pts[1]) 与末段 (pts[^2] → to) 允许任意距离（端点可为半格 Junction）
        //   waypoints 之间的相邻段必须严格单位 cellSize 8 方向
        // 长度统一用几何欧氏距离累加，保证半格段长度正确。
        float totalLength = 0f;
        for (int i = 0; i < pts.Length - 1; i++)
        {
            bool firstOrLast = (i == 0) || (i == pts.Length - 2);
            Direction? dir = firstOrLast
                ? DirectionUtil.FromDisplacementAnyLength(pts[i], pts[i + 1])
                : DirectionUtil.FromDisplacement(pts[i], pts[i + 1], cellSize);
            if (dir == null) return false;
            totalLength += pts[i].DistanceTo(pts[i + 1]);
        }

        var fromJunction = GetOrCreateJunction(from, cellSize);
        var toJunction = GetOrCreateJunction(to, cellSize);

        var segment = new Segment(NextID(), fromJunction.ID, toJunction.ID, roadID, waypoints, totalLength);
        _segments[segment.ID] = segment;

        // 把 Segment 挂到 Road
        if (!_roads.TryGetValue(roadID, out var road))
        {
            road = new Road(roadID);
            _roads[roadID] = road;
        }
        road.AddSegment(segment.ID);

        // 反向索引：仅 snap 格点位置进 _posToSegmentID。半格 Junction 不进字典；
        // 玩家点击拆除时只能命中走过格点的 Segment，半格 Junction 通过被切的另一侧 Segment 间接拆除。
        if (IsSnapGrid(from, cellSize)) _posToSegmentID[from] = segment.ID;
        if (IsSnapGrid(to, cellSize)) _posToSegmentID[to] = segment.ID;
        foreach (var wp in waypoints)
            if (IsSnapGrid(wp, cellSize)) _posToSegmentID[wp] = segment.ID;

        var firstDir = DirectionUtil.FromDisplacementAnyLength(from, pts[1])!.Value;
        fromJunction.AddSegmentConnection(segment.ID, toJunction.ID, firstDir);

        var lastPoint = pts[^2];
        var lastDir = DirectionUtil.FromDisplacementAnyLength(lastPoint, to)!.Value;
        toJunction.AddSegmentConnection(segment.ID, fromJunction.ID, lastDir);

        SegmentAdded?.Invoke(segment);
        return true;
    }

    /// <summary>委托给 GridSystem；保留签名供内外部兼容调用。</summary>
    private static bool IsSnapGrid(Vector2 pos, float cellSize) => GridSystem.IsSnapGrid(pos);

    /// <summary>
    /// 在指定位置劈开一条已有 Segment：原 Segment 删除，新建两段 Segment（继承原 RoadID）。
    /// <summary>
    /// 在指定 waypoint 位置劈开一条已有 Segment：原 Segment 删除，新建两段 Segment（继承原 RoadID）。
    /// splitPos 必须是该 Segment 的某个中段 waypoint 位置（共格点劈分场景）。
    /// </summary>
    private void SplitSegmentAtWaypoint(int segmentID, Vector2 splitPos, float cellSize)
    {
        if (!_segments.TryGetValue(segmentID, out var seg)) return;

        int wpIdx = Array.IndexOf(seg.Waypoints, splitPos);
        if (wpIdx < 0) return;

        var leftWps = new Vector2[wpIdx];
        Array.Copy(seg.Waypoints, 0, leftWps, 0, wpIdx);
        var rightWps = new Vector2[seg.Waypoints.Length - wpIdx - 1];
        Array.Copy(seg.Waypoints, wpIdx + 1, rightWps, 0, rightWps.Length);

        var fromPos = GetJunction(seg.FromJunctionID)!.Position;
        var toPos = GetJunction(seg.ToJunctionID)!.Position;
        int origRoadID = seg.RoadID;

        // 拆除原 Segment（会清反向索引、断开 from/to Junction 连接、清理孤立 Junction、从 Road 摘除）
        RemoveSegment(segmentID);

        // 重建两段 Segment，归属同一 RoadID（劈分不改 Road 归属）
        AddSegment(fromPos, splitPos, leftWps, origRoadID, cellSize);
        AddSegment(splitPos, toPos, rightWps, origRoadID, cellSize);
    }

    /// <summary>
    /// 在 Segment 几何线段任意位置（不必是 waypoint）劈开。splitPos 必须几何上落在 Segment 的某条
    /// 子线段上（即 fromJunction → wp[0] → wp[1] → … → toJunction 中某一段的内部）。
    /// 用于 X 形交叉处理：交点位置可能是非 snap 格点的"半格"位置。
    /// </summary>
    private void SplitSegmentAtPosition(int segmentID, Vector2 splitPos, float cellSize)
    {
        if (!_segments.TryGetValue(segmentID, out var seg)) return;

        var fromPos = GetJunction(seg.FromJunctionID)!.Position;
        var toPos = GetJunction(seg.ToJunctionID)!.Position;
        int origRoadID = seg.RoadID;

        // 构建 Segment 完整格点序列 (含两端 Junction 与所有 waypoints)
        var seq = new List<Vector2>(seg.Waypoints.Length + 2) { fromPos };
        seq.AddRange(seg.Waypoints);
        seq.Add(toPos);

        // 找到 splitPos 落在哪一子线段内部 [seq[i], seq[i+1]]
        int hitSubSegIdx = -1;
        for (int i = 0; i < seq.Count - 1; i++)
        {
            if (PointOnSegmentInterior(seq[i], seq[i + 1], splitPos))
            {
                hitSubSegIdx = i;
                break;
            }
        }
        if (hitSubSegIdx < 0)
        {
            // 兜底：splitPos 与某 waypoint 几乎重合 → 退化为 SplitSegmentAtWaypoint
            for (int i = 1; i < seq.Count - 1; i++)
            {
                if (seq[i].DistanceSquaredTo(splitPos) < 1e-4f)
                {
                    SplitSegmentAtWaypoint(segmentID, seq[i], cellSize);
                    return;
                }
            }
            return; // 无法定位 → 忽略
        }

        // 左半 waypoints = waypoints[0 .. hitSubSegIdx-1]（如果 hitSubSegIdx==0 则左半为空）
        // 右半 waypoints = waypoints[hitSubSegIdx .. ^1]（hitSubSegIdx 在 waypoint 数组里的索引 = hitSubSegIdx，
        //   因为 seq[0]=from, seq[1..N]=waypoints, seq[N+1]=to。waypoint 索引 = seq 索引 - 1）
        // 即子线段 [seq[i], seq[i+1]] 内部劈分 → 左半 waypoints = waypoints[0 .. i-1]，右半 waypoints = waypoints[i .. ^1]。
        int wpCount = seg.Waypoints.Length;
        int leftWpCount = hitSubSegIdx;                       // 含 0
        int rightWpCount = wpCount - hitSubSegIdx;            // 含 wpCount
        var leftWps = new Vector2[leftWpCount];
        var rightWps = new Vector2[rightWpCount];
        Array.Copy(seg.Waypoints, 0, leftWps, 0, leftWpCount);
        Array.Copy(seg.Waypoints, hitSubSegIdx, rightWps, 0, rightWpCount);

        // 拆除原 Segment
        RemoveSegment(segmentID);

        // 重建两段。两段的连接处 = splitPos（可能是非格点）
        AddSegment(fromPos, splitPos, leftWps, origRoadID, cellSize);
        AddSegment(splitPos, toPos, rightWps, origRoadID, cellSize);
    }

    /// <summary>判断 q 是否严格在线段 [a, b] 的内部（不含端点，容差比较）。</summary>
    private static bool PointOnSegmentInterior(Vector2 a, Vector2 b, Vector2 q)
    {
        Vector2 ab = b - a;
        float lenSq = ab.LengthSquared();
        if (lenSq < 1e-8f) return false;
        Vector2 aq = q - a;
        float t = (aq.X * ab.X + aq.Y * ab.Y) / lenSq; // 投影参数
        if (t <= 1e-4f || t >= 1f - 1e-4f) return false;
        // 投影点
        Vector2 proj = a + ab * t;
        return proj.DistanceSquaredTo(q) < 1e-4f;
    }

    /// <summary>
    /// 扫描新路径与现有 Segment 之间的内部几何交叉点。对每个交点：
    ///   1. 在受影响的现有 Segment 上调 SplitSegmentAtPosition 切开（在交点处生成新 Junction）
    ///   2. 把交点作为额外锚点插入新路径（按沿 path 的距离顺序）
    /// 返回插入了交点的新路径。新路径中每相邻元素之间仍是 8 方向（首尾段可非单位距离）。
    /// </summary>
    private List<Vector2> ResolveInteriorCrossings(List<Vector2> newPath, float cellSize)
    {
        // 收集新路径每段的内部交点：(段在 newPath 中的索引, 沿该段的参数 t, 交点位置, 受影响 segmentID)
        // 注意：处理一个交点会修改现有 _segments，所以分两步——先收集快照所有交点位置，再依次切。
        // 切的过程中老 segment 被删、新 segment 替换，但交点位置不变，下次 PointOnSegmentInterior
        // 会落到"两个新 segment 之一"，因此按位置切仍然正确。
        var collected = new List<(int pathSegIdx, float t, Vector2 pos)>();

        // 当前已有 Segment 的"几何子线段"列表快照（不依赖 segmentID，只看几何）
        var existingSubSegs = CollectExistingSubSegments();

        for (int i = 0; i < newPath.Count - 1; i++)
        {
            var p1 = newPath[i];
            var p2 = newPath[i + 1];
            foreach (var (q1, q2) in existingSubSegs)
            {
                if (TryComputeInteriorCross(p1, p2, q1, q2, out var cross, out var tNew))
                {
                    collected.Add((i, tNew, cross));
                }
            }
        }

        if (collected.Count == 0) return newPath;

        // 同位置的交点去重（两条不同子线段在同一点撞 newPath）
        var uniquePositions = new List<Vector2>();
        foreach (var c in collected)
        {
            bool dup = false;
            foreach (var u in uniquePositions)
            {
                if (u.DistanceSquaredTo(c.pos) < 1e-4f) { dup = true; break; }
            }
            if (!dup) uniquePositions.Add(c.pos);
        }

        // 对每个唯一交点：找出当前所有命中该位置的现有 Segment，调位置劈分
        foreach (var pos in uniquePositions)
        {
            // 重新收集（因为前一次切已改变 _segments）
            var hits = new List<int>();
            foreach (var seg in _segments.Values)
            {
                var fp = GetJunction(seg.FromJunctionID)!.Position;
                var tp = GetJunction(seg.ToJunctionID)!.Position;
                var seq = new List<Vector2>(seg.Waypoints.Length + 2) { fp };
                seq.AddRange(seg.Waypoints);
                seq.Add(tp);
                for (int i = 0; i < seq.Count - 1; i++)
                {
                    if (PointOnSegmentInterior(seq[i], seq[i + 1], pos))
                    {
                        hits.Add(seg.ID);
                        break;
                    }
                }
            }
            foreach (var sid in hits)
                SplitSegmentAtPosition(sid, pos, cellSize);
        }

        // 把交点按"在 newPath 中的位置"插入新路径。同一新路径段可能有多个交点，按 t 排序。
        var byPathSeg = new Dictionary<int, List<(float t, Vector2 pos)>>();
        foreach (var c in collected)
        {
            if (!byPathSeg.TryGetValue(c.pathSegIdx, out var list))
                byPathSeg[c.pathSegIdx] = list = new();
            // 同一 pathSeg 上去重
            bool dup = false;
            foreach (var existing in list)
            {
                if (existing.pos.DistanceSquaredTo(c.pos) < 1e-4f) { dup = true; break; }
            }
            if (!dup) list.Add((c.t, c.pos));
        }
        var rebuilt = new List<Vector2>();
        for (int i = 0; i < newPath.Count - 1; i++)
        {
            rebuilt.Add(newPath[i]);
            if (byPathSeg.TryGetValue(i, out var inserts))
            {
                inserts.Sort((a, b) => a.t.CompareTo(b.t));
                foreach (var ins in inserts) rebuilt.Add(ins.pos);
            }
        }
        rebuilt.Add(newPath[^1]);
        return rebuilt;
    }

    /// <summary>收集所有现有 Segment 的相邻格点对，作为几何子线段快照。</summary>
    private List<(Vector2 a, Vector2 b)> CollectExistingSubSegments()
    {
        var result = new List<(Vector2, Vector2)>();
        foreach (var seg in _segments.Values)
        {
            var fp = GetJunction(seg.FromJunctionID)!.Position;
            var tp = GetJunction(seg.ToJunctionID)!.Position;
            var seq = new Vector2[seg.Waypoints.Length + 2];
            seq[0] = fp;
            for (int i = 0; i < seg.Waypoints.Length; i++) seq[i + 1] = seg.Waypoints[i];
            seq[^1] = tp;
            for (int i = 0; i < seq.Length - 1; i++)
                result.Add((seq[i], seq[i + 1]));
        }
        return result;
    }

    /// <summary>
    /// 计算两条线段 [p1,p2] 与 [q1,q2] 的内部交点（不共享端点、不共线）。
    /// 输出参数 t 是交点在 [p1,p2] 上的归一化位置 (0,1)。返回 false 表示无内部交点。
    /// </summary>
    private static bool TryComputeInteriorCross(
        Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2,
        out Vector2 cross, out float t)
    {
        cross = default;
        t = 0f;
        if (p1 == q1 || p1 == q2 || p2 == q1 || p2 == q2) return false;
        Vector2 r = p2 - p1;
        Vector2 s = q2 - q1;
        float rxs = r.X * s.Y - r.Y * s.X;
        if (Mathf.Abs(rxs) < 1e-6f) return false;
        Vector2 qp = q1 - p1;
        float tt = (qp.X * s.Y - qp.Y * s.X) / rxs;
        float uu = (qp.X * r.Y - qp.Y * r.X) / rxs;
        const float eps = 1e-4f;
        if (tt <= eps || tt >= 1f - eps || uu <= eps || uu >= 1f - eps) return false;
        cross = p1 + r * tt;
        t = tt;
        return true;
    }

    /// <summary>
    /// 若指定 Junction 当前连接数恰为 2 且两段对向直通 → 把它降级为 waypoint，合并两侧 Segment 成一条。
    /// 合并时较早 RoadID（数值较小）吸收较晚 RoadID 的所有 Segment。
    /// 安全护栏：
    ///   - 自环 Segment（segA == segB）→ 拒绝
    ///   - 多重边环路（segA、segB 远端是同一 Junction）→ 拒绝（farA == farB 检查）
    ///   - 合并后整路非 8 方向连续 → 拒绝（自然过滤掉非对向 Curve 节点）
    ///   - 含自环 Segment 的 Junction：自环只占 1 个 SegmentID 槽，加上另一段非自环 Segment 才达 ConnectionCount==2。
    ///     但 segA.ID == segB.ID 检查不命中（segA 是自环、segB 是另一段）；合并几何不连续 → 被 8 方向校验拒绝。
    ///     稳妥起见此处再显式判一次"任一段是否自环"。
    /// </summary>
    private void TryMergeAtJunction(int junctionID, float cellSize)
    {
        if (!_junctions.TryGetValue(junctionID, out var junction)) return;
        if (junction.ConnectionCount != 2) return;
        Vector2 pos = junction.Position;
        GD.Print($"[MERGE] try J@{pos.X:F0},{pos.Y:F0} cc=2");

        var segIDs = junction.ConnectedSegmentIDs.ToList();
        if (!_segments.TryGetValue(segIDs[0], out var segA)) return;
        if (!_segments.TryGetValue(segIDs[1], out var segB)) return;

        if (segA.ID == segB.ID) return;
        // 自环 Segment 拒绝合并（任一段的两端是同一 Junction）
        if (segA.FromJunctionID == segA.ToJunctionID) return;
        if (segB.FromJunctionID == segB.ToJunctionID) return;

        // 把两段统一成"远端 → junction"方向
        var (farA, seqAToJunction) = OrientTowardsJunction(segA, junctionID);
        var (farB, seqBToJunction) = OrientTowardsJunction(segB, junctionID);

        // 关键护栏：仅"对向直通"才合并降级——即从 junction 出发指向 segA 第一相邻点的方向
        // 与指向 segB 第一相邻点的方向必须互为反向（dispA + dispB == 0）。
        // L 形 / 直角弯（East+South 等）虽然每段单独是合法 8 方向（下面 8 方向连续校验会通过），
        // 但合并后是 Curve，违反"junction 降级回 waypoint"语义——waypoint 只用于直线序列上的过渡点。
        // 见文档 §6.4：合并仅降级"对向直通"节点；剩下的 ConnectionCount==2 是 Curve（非对向转弯点），
        // 仍当真路口保留。
        // OrientTowardsJunction 返回的序列是"远端 → ... → junction"，所以"junction 出发指向 segA 邻点"
        // 等于序列倒数第二个点 - junction（用 FromDisplacementAnyLength 兼容半格 Junction 端点）。
        Vector2 aNeighbor = seqAToJunction[seqAToJunction.Count - 2];
        Vector2 bNeighbor = seqBToJunction[seqBToJunction.Count - 2];
        var dirAFromJ = DirectionUtil.FromDisplacementAnyLength(pos, aNeighbor);
        var dirBFromJ = DirectionUtil.FromDisplacementAnyLength(pos, bNeighbor);
        if (dirAFromJ == null || dirBFromJ == null) return;
        var dispA = DirectionUtil.GetDisplacement(dirAFromJ.Value);
        var dispB = DirectionUtil.GetDisplacement(dirBFromJ.Value);
        if (dispA.X + dispB.X != 0 || dispA.Y + dispB.Y != 0) { GD.Print($"[MERGE] skip J@{pos.X:F0},{pos.Y:F0}: not opposite dirA={dirAFromJ} dirB={dirBFromJ}"); return; }
        GD.Print($"[MERGE] ok J@{pos.X:F0},{pos.Y:F0}: opposite segA#{segA.ID} segB#{segB.ID} farA={farA.X:F0},{farA.Y:F0} farB={farB.X:F0},{farB.Y:F0}");

        // 合并方向：farA → junction → farB
        var mergedWps = new List<Vector2>();
        for (int i = 1; i < seqAToJunction.Count - 1; i++)
            mergedWps.Add(seqAToJunction[i]);
        mergedWps.Add(pos);
        for (int i = seqBToJunction.Count - 2; i >= 1; i--)
            mergedWps.Add(seqBToJunction[i]);

        // 校验合并后整路 8 方向合法（含半格首/尾段：FromDisplacementAnyLength 是 AddSegment 内部校验的事，
        // 这里仍用 FromDisplacement 单位距离校验——半格 Junction 处合并的两段中至少一段是首/尾段半格距离，
        // 单位距离会拒绝。需放宽：首尾段允许半格，中间 waypoint 段单位距离）。
        var fullPath = new List<Vector2> { farA };
        fullPath.AddRange(mergedWps);
        fullPath.Add(farB);
        for (int i = 0; i < fullPath.Count - 1; i++)
        {
            // 首段（i==0）和尾段（i==fullPath.Count-2）允许任意 8 方向距离（半格 Junction 端点）；
            // 中间段必须单位距离。
            bool allowAnyLength = (i == 0) || (i == fullPath.Count - 2);
            var dirCheck = allowAnyLength
                ? DirectionUtil.FromDisplacementAnyLength(fullPath[i], fullPath[i + 1])
                : DirectionUtil.FromDisplacement(fullPath[i], fullPath[i + 1], cellSize);
            if (dirCheck == null) return;
        }
        if (farA == farB) return;

        // Road 归并：较小 RoadID 吸收较大 RoadID
        int keepRoadID = Math.Min(segA.RoadID, segB.RoadID);
        int loseRoadID = Math.Max(segA.RoadID, segB.RoadID);
        if (keepRoadID != loseRoadID && _roads.TryGetValue(loseRoadID, out var loseRoad)
                                     && _roads.TryGetValue(keepRoadID, out var keepRoad))
        {
            // 把 lose Road 名下所有 Segment 重新挂到 keep Road，并改各 Segment 的 RoadID
            foreach (var sid in loseRoad.SegmentIDs.ToList())
            {
                if (_segments.TryGetValue(sid, out var s))
                    s.RoadID = keepRoadID;
                keepRoad.AddSegment(sid);
            }
            _roads.Remove(loseRoadID);
        }

        // 进入合并操作守卫：内部两次 RemoveSegment + 一次 AddSegment 期间，
        // 抑制 RemoveSegment 末尾的级联合并降级。否则 (3,2) 触发的合并会因
        // 删段使 (5,0)/(0,5) 暂时变 cc=2 而被误并。
        _inMergeOperation = true;
        try
        {
            RemoveSegment(segA.ID);
            RemoveSegment(segB.ID);
            AddSegment(farA, farB, mergedWps.ToArray(), keepRoadID, cellSize);
        }
        finally
        {
            _inMergeOperation = false;
        }
    }

    /// <summary>
    /// 把一段 Segment 的格点序列按"远端 → 指定 Junction"方向重新表示，返回 (远端位置, 序列)。
    /// 序列首元素 = 远端位置，末元素 = junction 位置，中间 = 内部 waypoints。
    /// </summary>
    private (Vector2 farPos, List<Vector2> seq) OrientTowardsJunction(Segment seg, int junctionID)
    {
        var fp = GetJunction(seg.FromJunctionID)!.Position;
        var tp = GetJunction(seg.ToJunctionID)!.Position;
        var seq = new List<Vector2>();
        if (seg.ToJunctionID == junctionID)
        {
            seq.Add(fp);
            seq.AddRange(seg.Waypoints);
            seq.Add(tp);
            return (fp, seq);
        }
        else
        {
            seq.Add(tp);
            for (int i = seg.Waypoints.Length - 1; i >= 0; i--)
                seq.Add(seg.Waypoints[i]);
            seq.Add(fp);
            return (tp, seq);
        }
    }

    /// <summary>拆除单段 Segment（按 ID）。同时把它从所属 Road 中摘除；Road 变空时清掉。</summary>
    public bool RemoveSegment(int segmentID)
    {
        if (!_segments.TryGetValue(segmentID, out var seg)) return false;

        _segments.Remove(segmentID);

        // 清反向索引
        var fjPos = GetJunction(seg.FromJunctionID)?.Position;
        var tjPos = GetJunction(seg.ToJunctionID)?.Position;
        if (fjPos.HasValue && _posToSegmentID.TryGetValue(fjPos.Value, out int fid) && fid == segmentID)
            _posToSegmentID.Remove(fjPos.Value);
        if (tjPos.HasValue && _posToSegmentID.TryGetValue(tjPos.Value, out int tid) && tid == segmentID)
            _posToSegmentID.Remove(tjPos.Value);
        foreach (var wp in seg.Waypoints)
            if (_posToSegmentID.TryGetValue(wp, out int wid) && wid == segmentID)
                _posToSegmentID.Remove(wp);

        var fj = GetJunction(seg.FromJunctionID);
        var tj = GetJunction(seg.ToJunctionID);

        fj?.RemoveSegmentConnection(seg.ID);
        tj?.RemoveSegmentConnection(seg.ID);

        // 清孤立 Junction
        if (fj != null && fj.ConnectionCount == 0)
        {
            _junctions.Remove(fj.ID);
            _posToJunctionID.Remove(fj.Position);
        }
        if (tj != null && tj != fj && tj.ConnectionCount == 0)
        {
            _junctions.Remove(tj.ID);
            _posToJunctionID.Remove(tj.Position);
        }

        // 补回共享 Junction 在 _posToSegmentID 中的索引。
        // Bug: 当 Junction 被多个 Segment 共享时，_posToSegmentID 只存最后写入的那个 Segment ID。
        // RemoveSegment 清除条目后若 Junction 仍存活（剩余连接 >0），但字典位置已被清空，
        // 玩家将无法通过点击该 Junction 格点拆除剩余 Segment（Lookup 返回 -1 然后被忽略）。
        // 此修复从该 Junction 的 ConnectedSegmentIDs 取一条存活的按格点补回。
        MaybeReindexJunctionInPosDict(fj, GridSystem.CellSize);
        if (tj != fj)
            MaybeReindexJunctionInPosDict(tj, GridSystem.CellSize);

        // 从 Road 摘除；Road 变空时清掉（不发 Road 级事件，本期未引入）
        if (_roads.TryGetValue(seg.RoadID, out var road))
        {
            road.RemoveSegment(seg.ID);
            if (road.IsEmpty)
            {
                _roads.Remove(seg.RoadID);
            }
            else
            {
                // 删除中间一段后，剩下的 Segments 可能不再是连续路径——做连通分量切分。
                // "Road = 连续路径的总和" 语义：同一 Road 的所有 Segment 必须几何上通过共享 Junction 连通。
                SplitRoadIntoConnectedComponents(road);
            }
        }

        SegmentRemoved?.Invoke(seg);

        // 合并降级：被删段两端的 Junction 若 ConnectionCount 从 ≥3 降到 2 且两段对向直通同 Road，
        // 应合并回单段 Segment（如 T 路口拆除竖直分支后，水平左右两半合并回一条）。
        // TryMergeAtJunction 内部已含全部安全护栏（自环 / 多重边环路 / 非对向 / 含自环 Segment）。
        // 注意：合并触发时机必须在 SegmentRemoved 事件之后——合并会再触发一次 SegmentRemoved+SegmentAdded，
        // 渲染层据此先看到"删一段"再看到"两段合并成一段"两个事件批次。
        // 守卫：若当前 RemoveSegment 是 TryMergeAtJunction 内部调用产生的，跳过末尾合并触发以避免递归级联。
        if (!_inMergeOperation)
        {
            if (fj != null && _junctions.ContainsKey(fj.ID))
                TryMergeAtJunction(fj.ID, GridSystem.CellSize);
            if (tj != null && tj != fj && _junctions.ContainsKey(tj.ID))
                TryMergeAtJunction(tj.ID, GridSystem.CellSize);
        }

        return true;
    }

    /// <summary>
    /// 把一条 Road 按"共享 Junction 连通性"切成多条 Road。第一个连通分量保留原 RoadID，
    /// 其余分量各分配一个新 RoadID 并把对应 Segment 的 RoadID 改写、新 Road 加入 _roads。
    /// 用于 RemoveSegment 后修复"中间被掏空导致同 Road 含多个不相连 Segment 子集"。
    /// </summary>
    private void SplitRoadIntoConnectedComponents(Road road)
    {
        var segIDs = road.SegmentIDs.ToList();
        if (segIDs.Count <= 1) return;

        // 邻接：两个 Segment 共享至少一个 Junction → 邻接
        var visited = new HashSet<int>();
        var components = new List<List<int>>();

        foreach (var startID in segIDs)
        {
            if (visited.Contains(startID)) continue;
            var queue = new Queue<int>();
            var comp = new List<int>();
            queue.Enqueue(startID);
            visited.Add(startID);
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                comp.Add(cur);
                if (!_segments.TryGetValue(cur, out var curSeg)) continue;
                foreach (var otherID in segIDs)
                {
                    if (visited.Contains(otherID)) continue;
                    if (!_segments.TryGetValue(otherID, out var otherSeg)) continue;
                    bool shareJunction =
                        curSeg.FromJunctionID == otherSeg.FromJunctionID ||
                        curSeg.FromJunctionID == otherSeg.ToJunctionID ||
                        curSeg.ToJunctionID == otherSeg.FromJunctionID ||
                        curSeg.ToJunctionID == otherSeg.ToJunctionID;
                    if (shareJunction)
                    {
                        visited.Add(otherID);
                        queue.Enqueue(otherID);
                    }
                }
            }
            components.Add(comp);
        }

        if (components.Count <= 1) return; // 仍是连通的，不动

        // 保留第一个分量挂原 Road；其余分量各建新 Road
        for (int i = 1; i < components.Count; i++)
        {
            var newRoad = new Road(NextID());
            _roads[newRoad.ID] = newRoad;
            foreach (var sid in components[i])
            {
                if (_segments.TryGetValue(sid, out var s))
                {
                    s.RoadID = newRoad.ID;
                    road.RemoveSegment(sid);
                    newRoad.AddSegment(sid);
                }
            }
        }
    }

    /// <summary>
    /// 拆除整条 Road（其下所有 Segment）。返回是否实际删除。
    /// </summary>
    public bool RemoveRoad(int roadID)
    {
        if (!_roads.TryGetValue(roadID, out var road)) return false;
        // ToList 拷贝，因为 RemoveSegment 会改动 road._segmentIDs
        foreach (var sid in road.SegmentIDs.ToList())
            RemoveSegment(sid);
        // 若 RemoveSegment 没把 Road 清掉（理论上 IsEmpty 时会清），保险再删一次
        _roads.Remove(roadID);
        return true;
    }

    /// <summary>按位置反查 Segment.ID；找不到返回 -1</summary>
    public int FindSegmentAt(Vector2 pos) =>
        _posToSegmentID.TryGetValue(pos, out int id) ? id : -1;

    /// <summary>FindSegmentAt + 半格 waypoint 几何回退（半格点不在 _posToSegmentID 中）。</summary>
    private int FindSegmentAtIncludingHalfGrid(Vector2 pos)
    {
        int sid = FindSegmentAt(pos);
        if (sid >= 0) return sid;
        // 扫描所有 Segment 的 waypoint 找几何重合
        foreach (var seg in _segments.Values)
            foreach (var wp in seg.Waypoints)
                if (wp.DistanceSquaredTo(pos) < 1e-4f) return seg.ID;
        return -1;
    }

    /// <summary>
    /// RemoveSegment 后调用：若某 Junction 位置在字典中被清空，但 Junction 仍存活（仍有连接 Segment），
    /// 则从 ConnectedSegmentIDs 取一条存活的按格点补回 _posToSegmentID。否则点击该格点拆除会失效。
    /// 半格 Junction 不补（IsSnapGrid == false，无法命中字典 key）。
    /// </summary>
    private void MaybeReindexJunctionInPosDict(Junction? junction, float cellSize)
    {
        if (junction == null) return;
        if (junction.ConnectionCount == 0) return;
        var pos = junction.Position;
        if (_posToSegmentID.ContainsKey(pos)) return;
        if (!IsSnapGrid(pos, cellSize)) return;
        foreach (var sid in junction.ConnectedSegmentIDs)
        {
            if (_segments.ContainsKey(sid))
            {
                _posToSegmentID[pos] = sid;
                break;
            }
        }
    }

    /// <summary>
    /// 判断新路从 approachPos 接近 targetPos 的方向是否与已有 Segment 在 targetPos
    /// 处的延伸方向共线（同方向）。用于跳过共线重叠场景中的冗余劈分。
    /// </summary>
    private bool IsApproachColinearWithSegment(Vector2 targetPos, Vector2 approachPos, int segmentID)
    {
        if (!_segments.TryGetValue(segmentID, out var seg)) return false;
        var approachDir = DirectionUtil.FromDisplacementAnyLength(approachPos, targetPos);
        if (approachDir == null) return false;

        var fj = GetJunction(seg.FromJunctionID);
        var tj = GetJunction(seg.ToJunctionID);
        if (fj == null || tj == null) return false;

        var pts = new List<Vector2> { fj.Position };
        pts.AddRange(seg.Waypoints);
        pts.Add(tj.Position);

        int idx = pts.IndexOf(targetPos);
        if (idx < 0) return false;

        // 检查 approachDir 是否与 Segment 在 targetPos 处的延伸方向一致
        if (idx < pts.Count - 1)
        {
            var fwd = DirectionUtil.FromDisplacementAnyLength(targetPos, pts[idx + 1]);
            if (fwd != null && fwd == approachDir) return true;
        }
        if (idx > 0)
        {
            var bwd = DirectionUtil.FromDisplacementAnyLength(targetPos, pts[idx - 1]);
            if (bwd != null && bwd == approachDir) return true;
        }
        return false;
    }

    public Junction? GetJunctionAt(Vector2 pos) =>
        _posToJunctionID.TryGetValue(pos, out int id) ? _junctions.GetValueOrDefault(id) : null;

    public bool HasJunctionAt(Vector2 pos) => _posToJunctionID.ContainsKey(pos);

    /// <summary>判断位置是否落在已有路网点上（含 Junction + waypoint，含半格点）。</summary>
    private bool IsOnRoadPoint(Vector2 pos)
    {
        if (_posToJunctionID.ContainsKey(pos)) return true;
        if (_posToSegmentID.ContainsKey(pos)) return true;
        // 扫描所有 Segment 的 waypoint（含半格点——不在 _posToSegmentID 中）
        foreach (var seg in _segments.Values)
            foreach (var wp in seg.Waypoints)
                if (wp.DistanceSquaredTo(pos) < 1e-4f) return true;
        // 也扫描 Junction 位置（半格 Junction 不在 _posToJunctionID 中）
        foreach (var j in _junctions.Values)
            if (j.Position.DistanceSquaredTo(pos) < 1e-4f) return true;
        return false;
    }

    /// <summary>
    /// 几何匹配查 Junction：含字典命中（格点）+ 几何近似匹配（半格 Junction）。
    /// 内部 splitIdx 切段使用，确保半格交点也成为切分锚点。
    /// </summary>
    private bool IsAnyJunctionAt(Vector2 pos)
    {
        if (_posToJunctionID.ContainsKey(pos)) return true;
        foreach (var j in _junctions.Values)
        {
            if (j.Position.DistanceSquaredTo(pos) < 1e-4f) return true;
        }
        return false;
    }

    public Junction? GetJunction(int id) => _junctions.GetValueOrDefault(id);

    public Segment? GetSegment(int id) => _segments.GetValueOrDefault(id);

    public Road? GetRoad(int id) => _roads.GetValueOrDefault(id);

    public IEnumerable<Segment> GetAllSegments() => _segments.Values;
    public IEnumerable<Road> GetAllRoads() => _roads.Values;
    public IEnumerable<Junction> GetAllJunctions() => _junctions.Values;

    /// <summary>委托给 GridSystem；保留签名供内外部兼容调用。</summary>
    public static Vector2 SnapToGrid(Vector2 pos, float cellSize) => GridSystem.SnapToGrid(pos);

    /// <summary>
    /// 获取或创建一个 Junction。仅当位置落在标准 snap 格点上时才进 _posToJunctionID 字典。
    /// 半格 Junction（位于斜线交点等非格点位置）只通过 ID 访问，不索引位置。
    /// </summary>
    private Junction GetOrCreateJunction(Vector2 pos, float cellSize)
    {
        // 优先按格点位置查（半格 Junction 不会命中字典）
        if (_posToJunctionID.TryGetValue(pos, out int id))
            return _junctions[id];

        // 半格场景：线性扫描已有 Junctions 找几何重合（容差，避免浮点漂移产生重复 Junction）
        if (!IsSnapGrid(pos, cellSize))
        {
            foreach (var j in _junctions.Values)
            {
                if (j.Position.DistanceSquaredTo(pos) < 1e-4f) return j;
            }
        }

        var junction = new Junction(NextID(), pos);
        _junctions[junction.ID] = junction;
        if (IsSnapGrid(pos, cellSize))
            _posToJunctionID[pos] = junction.ID;
        return junction;
    }

    // ═══════════════════════════════════════════════
    // ISaveable 实现
    // ═══════════════════════════════════════════════

    public object CaptureState()
    {
        var data = new RoadNetworkData
        {
            NextID = _nextID,
            CellSize = GridSystem.CellSize
        };

        // Junctions
        foreach (var j in _junctions.Values)
        {
            data.Junctions.Add(new JunctionData
            {
                ID = j.ID,
                X = j.Position.X,
                Y = j.Position.Y
            });
        }

        // Segments
        foreach (var s in _segments.Values)
        {
            var sd = new SegmentData
            {
                ID = s.ID,
                FromJunctionID = s.FromJunctionID,
                ToJunctionID = s.ToJunctionID,
                RoadID = s.RoadID,
                TotalLength = s.TotalLength
            };
            foreach (var wp in s.Waypoints)
                sd.Waypoints.Add(new Vector2Data(wp));
            data.Segments.Add(sd);
        }

        // Roads
        foreach (var r in _roads.Values)
        {
            data.Roads.Add(new RoadData
            {
                ID = r.ID,
                SegmentIDs = new List<int>(r.SegmentIDs)
            });
        }

        return data;
    }

    public void RestoreState(string json)
    {
        var data = SaveJson.Deserialize<RoadNetworkData>(json);

        // 清空所有现有状态
        _junctions.Clear();
        _segments.Clear();
        _roads.Clear();
        _posToJunctionID.Clear();
        _posToSegmentID.Clear();

        // 恢复基础字段
        _nextID = data.NextID;
        GridSystem.Config.CellSize = data.CellSize;
        _inMergeOperation = false;

        // 重建 Junctions（先只建 ID + Position，连接关系由 RebuildIndexes 补）
        foreach (var jd in data.Junctions)
        {
            var junction = new Junction(jd.ID, new Vector2(jd.X, jd.Y));
            _junctions[junction.ID] = junction;
        }

        // 重建 Roads
        foreach (var rd in data.Roads)
        {
            var road = new Road(rd.ID);
            foreach (var sid in rd.SegmentIDs)
                road.AddSegment(sid);
            _roads[road.ID] = road;
        }

        // 重建 Segments
        foreach (var sd in data.Segments)
        {
            var waypoints = new Vector2[sd.Waypoints.Count];
            for (int i = 0; i < waypoints.Length; i++)
                waypoints[i] = sd.Waypoints[i].ToVector2();

            var segment = new Segment(
                sd.ID,
                sd.FromJunctionID,
                sd.ToJunctionID,
                sd.RoadID,
                waypoints,
                sd.TotalLength
            );
            _segments[segment.ID] = segment;
        }

        // 重建所有反向索引
        RebuildIndexes();

        // 通知渲染器等重建显示
        NetworkReloaded?.Invoke();
    }

    /// <summary>
    /// 从已加载的 Junctions / Segments 重建反向索引词典和 Junction 内部连接关系。
    /// 在 RestoreState 结束时调用。
    /// </summary>
    internal void RebuildIndexes()
    {
        float cellSize = GridSystem.CellSize;

        // 1. 重建 _posToJunctionID（仅 snap 格点 Junction）
        foreach (var j in _junctions.Values)
        {
            if (IsSnapGrid(j.Position, cellSize))
                _posToJunctionID[j.Position] = j.ID;
        }

        // 2. 重建 _posToSegmentID（从 Segment 的 waypoints + 端点）
        foreach (var s in _segments.Values)
        {
            var fromJ = _junctions.GetValueOrDefault(s.FromJunctionID);
            var toJ = _junctions.GetValueOrDefault(s.ToJunctionID);

            if (fromJ != null && IsSnapGrid(fromJ.Position, cellSize))
                _posToSegmentID[fromJ.Position] = s.ID;
            if (toJ != null && IsSnapGrid(toJ.Position, cellSize))
                _posToSegmentID[toJ.Position] = s.ID;
            foreach (var wp in s.Waypoints)
            {
                if (IsSnapGrid(wp, cellSize))
                    _posToSegmentID[wp] = s.ID;
            }
        }

        // 3. 重建 Junction 内部的 _connections（从 Segment 反向构建）
        foreach (var s in _segments.Values)
        {
            var fromJ = _junctions.GetValueOrDefault(s.FromJunctionID);
            var toJ = _junctions.GetValueOrDefault(s.ToJunctionID);
            if (fromJ == null || toJ == null) continue;

            // 确定 fromJunction 侧的入方向
            Vector2 firstPt = s.Waypoints.Length > 0
                ? s.Waypoints[0]
                : toJ.Position;
            var dirFrom = DirectionUtil.FromDisplacementAnyLength(fromJ.Position, firstPt);
            if (dirFrom.HasValue)
                fromJ.AddSegmentConnection(s.ID, toJ.ID, dirFrom.Value);

            // 确定 toJunction 侧的入方向
            Vector2 lastPt = s.Waypoints.Length > 0
                ? s.Waypoints[^1]
                : fromJ.Position;
            var dirTo = DirectionUtil.FromDisplacementAnyLength(toJ.Position, lastPt);
            if (dirTo.HasValue)
                toJ.AddSegmentConnection(s.ID, fromJ.ID, dirTo.Value);
        }
    }
}
