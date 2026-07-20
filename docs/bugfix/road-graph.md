# RoadGraph 重构后 Bug 修复记录

> 日期：2026-06-06
> 影响文件：`Scripts/Road/RoadGraph.cs`
> 关联重构：road-system-v2-gen（阶段 A+B）

---

<a id="road-graph-bug-1"></a>
## BUG-1：TryMergeAtNode 远端节点被误删，导致道路不渲染

### 症状

铺路后仅生成节点圆点（junction dots），不出现连线（Line2D）。表现为"道路建设失败，仅生成 node 或不完整"。

### 根因分析

`TryMergeAtNode` 合并两条共线边时执行顺序：

1. `RemoveEdge(edgeA, suppressMerge: true)` — 内部调用 `RemoveNodeIfIsolated(farA)`
2. 此时 `farA.EdgeCount == 0`（edgeA 已被移除，而合并边尚未创建）→ farA 被从 `_nodes` 字典和空间索引中删除
3. `RemoveEdge(edgeB, suppressMerge: true)` — 同理可能删除 farB
4. `AddEdge(farA, farB, ...)` 创建合并边，触发 `EdgeAdded` 事件
5. 渲染器响应事件：`_network.GetNode(edge.NodeA)` → 返回 `null`（farA 已被删除）→ 静默跳过 Line2D 创建

### 修复方案

在两次 `RemoveEdge` 之后、`AddEdge` 之前，检查并重新插入被误删的远端节点：

```csharp
RemoveEdge(edgeA.ID, suppressMerge: true);
RemoveEdge(edgeB.ID, suppressMerge: true);

// 远端节点可能因 RemoveNodeIfIsolated 被误删，重新插入
if (!_nodes.ContainsKey(farA.ID))
{
    _nodes[farA.ID] = farA;
    InsertNodeSpatialRef(farA);
}
if (!_nodes.ContainsKey(farB.ID))
{
    _nodes[farB.ID] = farB;
    InsertNodeSpatialRef(farB);
}

AddEdge(farA, farB, mergedPoints.ToArray(), keepGroupID, type);
```

### 影响范围

所有调用 `AddRoad` 后触发的 `TryMergeAtNode`；首次铺路（空图上）即可复现。

---

<a id="road-graph-bug-2"></a>
## BUG-2：ResolveIntersections 未检测到边 waypoint 处的交叉点

### 症状

新路穿过已有边的 waypoint 位置时，交叉口不产生——已有边未被拆分，新路被当作"不覆盖"区段直接添加，导致路网逻辑不一致。

### 根因分析

`ResolveIntersections` 用 `TryComputeInteriorCross(a, b, existing[j], existing[j+1])` 逐对检测交叉。当交叉点恰好落在已有边的 waypoint（即相邻子段的共享端点）时：

- 对子段 `(q[j-1], q[j])`：交叉参数 `uu = 1.0`，被 `uu >= 1 - ε` 排除
- 对子段 `(q[j], q[j+1])`：交叉参数 `uu = 0.0`，被 `uu <= ε` 排除

两侧都排除 → 交叉点丢失。

### 修复方案

在 `ResolveIntersections` 内循环中，追加对已有边 waypoints 的逐点检测：

```csharp
// 检测已有边 waypoint 是否落在新路段的内部
for (int j = 1; j < existing.Length - 1; j++)
{
    var wp = existing[j];
    if (!PointOnSegmentInterior(a, b, wp)) continue;
    float tWp = ProjectParam(a, b, wp);
    collected.Add((i, tWp, wp));
}
```

### 影响范围

任何新路线经过已有边内部 waypoint 的场景（常见于交叉铺设）。

---

<a id="road-graph-bug-3"></a>
## BUG-3：QueryCandidateEdgeIDs 搜索半径不足，对角线交叉概率性失败

### 症状

两条路交叉时**有概率**不产生交叉口节点，尤其涉及对角线方向的已有道路。

### 根因分析

`QueryCandidateEdgeIDs` 用空间索引搜索候选边，搜索半径为：

```
radius = halfSegmentLength + IndexBucketSize (64)
```

已有边在空间索引中的 ref 点间距：
- 正交边：`CellSize = 64px`
- 对角线边：`CellSize × √2 ≈ 90.5px`

当新路段较短（如单格 64px，halfSeg=32）时，搜索半径 = 32 + 64 = 96px。对角线边 ref 间距 90.5px，查询中心若偏离最近 ref 超过 96px，该边不会出现在候选集中 → 交叉检测被跳过。

### 修复方案

将搜索半径增加到 `IndexBucketSize * 1.5f`，确保覆盖对角线间距：

```csharp
float radius = a.DistanceTo(b) * 0.5f + IndexBucketSize * 1.5f;
```

新的最小搜索半径 = 32 + 96 = 128px，远大于对角线间距 90.5px。

### 影响范围

所有涉及对角线方向的交叉检测。正交方向此前基本不受影响（间距 64 < 旧半径 96）。

---

<a id="road-graph-bug-4"></a>
## BUG-4：RemoveRoadGroup 批量删除后未触发 merge repair

### 症状

使用 `RemoveRoadGroup` 删除整组道路后，暴露的 2-degree 共线节点未被合并，路网中残留无意义的中间节点。

### 根因分析

`RemoveRoadGroup` 对每条边调用 `RemoveEdge(edgeID, suppressMerge: true)`，意图避免逐条删除时的重复 merge。但删除全部边后从未执行 merge repair 就直接返回。

对比：单条 `RemoveEdge(edgeID)` 调用 `RemoveEdge(edgeID, suppressMerge: false)`，会在删除后对两端节点尝试 merge。

### 修复方案

删除前收集所有端点节点 ID，删除全部边后对仍存在的节点执行 merge：

```csharp
// 收集端点
var touchedNodeIDs = new HashSet<int>();
foreach (int edgeID in group.EdgeIDs)
{
    if (_edges.TryGetValue(edgeID, out var edge))
    {
        touchedNodeIDs.Add(edge.NodeA);
        touchedNodeIDs.Add(edge.NodeB);
    }
}

// 删除所有边
foreach (int edgeID in group.EdgeIDs.ToList())
    RemoveEdge(edgeID, suppressMerge: true);
_groups.Remove(groupID);

// Merge repair
foreach (int nodeID in touchedNodeIDs)
    if (_nodes.ContainsKey(nodeID))
        TryMergeAtNode(nodeID, suppressMerge: true);
```

### 影响范围

仅在调用 `RemoveRoadGroup` 时触发（当前 UI 未暴露此功能，但数据层 API 完整性需要保证）。

---

<a id="road-graph-bug-5"></a>
## BUG-5：QueryCandidateEdgeIDs 搜索半径与 CellSize 不匹配

### 症状

CellSize > 64 时（如 CellSize=100），交叉路口有概率不生成。第一条路合并后 spatial ref 间距为 CellSize（100px），但搜索半径不足以从新路段中点覆盖到最近的 edge ref。

### 根因分析

原实现：`radius = halfSegLen + IndexBucketSize(64)`。当 CellSize=100、新路段为单格时：
- halfSegLen = 50
- radius = 50 + 64 = 114（旧值）或 50 + 96 = 146（BUG-3 修复后）
- 最近 edge ref 到查询中心距离可达 ~150px → 遗漏

更根本的问题：从单一中点查询无法保证覆盖任意长度的 edge ref 间距。

### 修复方案

改为从路段**两个端点**分别查询，搜索半径为 `segLen + IndexBucketSize * 2`：

```csharp
private void QueryCandidateEdgeIDs(Vector2 a, Vector2 b, HashSet<int> result)
{
    float segLen = a.DistanceTo(b);
    float radius = segLen + IndexBucketSize * 2f;

    foreach (var hit in _spatialIndex.QueryRadius(a, radius))
        if (hit.Kind == SpatialRefKind.EdgePoint)
            result.Add(((EdgePointRef)hit).EdgeID);
    foreach (var hit in _spatialIndex.QueryRadius(b, radius))
        if (hit.Kind == SpatialRefKind.EdgePoint)
            result.Add(((EdgePointRef)hit).EdgeID);
}
```

### 影响范围

所有 CellSize > IndexBucketSize 的场景；对角线路同理。

---

<a id="road-graph-bug-6"></a>
## BUG-6：SplitEdgesAtPathAnchors 未检测到路径点与已有边 waypoint 重合

### 症状

新路径点恰好落在已有合并边的 waypoint 上时，该边未被拆分，交叉口不产生。这是用户报告的"概率不生成交叉路口"的**主要原因**。

### 根因分析

`SplitEdgesAtPathAnchors` 对每个路径点调用 `FindEdgesContainingInteriorPoint(point)`，后者依赖 `PointOnSegmentInterior` 判断点是否在子段内部。但 waypoint 位于相邻子段的边界（t=0 或 t=1），被严格排除。

示例：已有边 full path = [..., (100,0), (200,0), (300,0), ...]，新路径经过 (200,0)。
- 子段 (100,0)→(200,0)：t=1.0 → 排除
- 子段 (200,0)→(300,0)：t=0.0 → 排除
- 结果：`FindEdgesContainingInteriorPoint` 返回空 → 不拆分 → 不产生交叉口

### 修复方案

添加 `FindEdgesWithWaypointAt` 作为备选查找：当 `FindEdgesContainingInteriorPoint` 无结果时，检查是否有边的 `Points` 数组包含该位置：

```csharp
private void SplitEdgesAtPathAnchors(IEnumerable<Vector2> path)
{
    foreach (var point in path)
    {
        var edgeIDs = FindEdgesContainingInteriorPoint(point).ToList();
        if (edgeIDs.Count == 0)
            edgeIDs = FindEdgesWithWaypointAt(point).ToList();
        if (edgeIDs.Count == 0) continue;

        GetOrCreateNode(point);
        foreach (int edgeID in edgeIDs)
            SplitEdgeAtPosition(edgeID, point);
    }
}

private IEnumerable<int> FindEdgesWithWaypointAt(Vector2 pos)
{
    foreach (var edge in _edges.Values)
    {
        foreach (var wp in edge.Points)
        {
            if (wp.DistanceSquaredTo(pos) < GeometryEpsilon)
            {
                yield return edge.ID;
                break;
            }
        }
    }
}
```

### 影响范围

所有新路径经过已有合并边内部 waypoint 的场景。这是最常见的交叉建设模式——新路的格点与已有路的格点重合。

---

<a id="road-graph-bug-7"></a>
## BUG-7：SplitEdgeAtPosition 无法在 waypoint 位置执行拆分

### 症状

即使 `SplitEdgesAtPathAnchors` 通过 `FindEdgesWithWaypointAt` 正确找到了需要拆分的边，`SplitEdgeAtPosition` 仍然静默返回（不拆分），交叉口不产生。

### 根因分析

`SplitEdgeAtPosition` 内部调用 `FindSubSegmentContaining(fullPath, splitPos)` 来确定在哪个子段位置执行拆分。该方法使用 `PointOnSegmentInterior`，排除子段端点（t=0 或 t=1）。

当 `splitPos` 恰好是 fullPath 的一个内部 waypoint 时（如 fullPath[4]），它是相邻子段的共享端点：
- 子段 [3]→[4]：t=1.0 → 排除
- 子段 [4]→[5]：t=0.0 → 排除

`hitIndex = -1` → 方法直接 return，不执行拆分。

这是 BUG-6 修复的"最后一环"：`FindEdgesWithWaypointAt` 能找到边，但拆分操作本身无法执行。

### 修复方案

在 `SplitEdgeAtPosition` 中，当 `hitIndex < 0` 时，增加对 fullPath 内部点的直接匹配。找到匹配 waypoint 后，按其索引将边拆为两段：

```csharp
if (hitIndex < 0)
{
    for (int i = 1; i < fullPath.Length - 1; i++)
    {
        if (fullPath[i].DistanceSquaredTo(splitPos) < GeometryEpsilon)
        {
            // 在 waypoint 索引 i 处拆分：left=[0..i], right=[i..end]
            var leftPts = new List<Vector2>();
            var rightPts = new List<Vector2>();
            for (int k = 1; k < i; k++) leftPts.Add(fullPath[k]);
            for (int k = i + 1; k < fullPath.Length - 1; k++) rightPts.Add(fullPath[k]);

            RemoveEdge(edge.ID, suppressMerge: true);
            var splitNode = GetOrCreateNode(splitPos);
            var nodeA = GetOrCreateNode(fullPath[0]);
            var nodeB = GetOrCreateNode(fullPath[^1]);
            AddEdge(nodeA, splitNode, leftPts.ToArray(), groupID, type);
            AddEdge(splitNode, nodeB, rightPts.ToArray(), groupID, type);
            return;
        }
    }
    return; // splitPos 不在此边上
}
```

### 影响范围

所有经过 `FindEdgesWithWaypointAt` 路径触发的拆分操作。这是交叉口不生成的**最终根因**——前面的检测修复（BUG-2/5/6）能正确找到需要拆分的边和位置，但拆分动作本身在 waypoint 处失败。

---

<a id="road-graph-bug-8"></a>
## BUG-8：完整重复铺路在被拒绝前仍会拆分现有路网

关联文档：`save-system:BUG-1`

### 症状

沿已有道路完整重复铺设同一路径时，`AddRoad` 最终返回 `-1` 表示没有添加新道路，但已有道路仍可能被传入路径的锚点拆分。调用方看到操作被拒绝，路网内部却发生边和节点变更，并可能产生对应的增删事件。

### 根因分析

原流程在检查 `IsPathFullyCovered(path)` 之前，先调用了两个会修改路网的方法：

1. `ResolveIntersections(path)` 可能在交点处拆分已有边；
2. `SplitEdgesAtPathAnchors(path)` 可能在新路径锚点处拆分已有边；
3. 随后覆盖检查发现整条路径已存在并返回 `-1`。

因此，“拒绝重复道路”并不是无副作用操作。虽然没有留下新的道路组，但已有边可能经历不必要的拆分、重建和事件通知。

### 修复方案

在任何路网变更之前先对原始折线路径执行完整覆盖检查：

```csharp
var path = new List<Vector2>(waypoints.Length + 2) { start };
path.AddRange(waypoints);
path.Add(end);

if (IsPathFullyCovered(path)) return -1;

path = ResolveIntersections(path);
SplitEdgesAtPathAnchors(path);
path = InsertExistingNodeAnchors(path);
```

保留后续的第二次覆盖检查，用于处理交叉点和既有节点被插入路径后的最终状态；前置检查则保证明显的完整重复路径在进入任何可变流程前直接退出。

### 影响范围

影响所有完整覆盖既有路网的 `AddRoad` 调用。部分重叠但仍包含新区段的路径不会被前置检查拒绝，仍按原流程完成交叉处理并添加未覆盖区段。

---

## 验证状态

- `dotnet build`：0 错误，4 个无关警告（MapBackground.cs nullable）
- 所有修复均在 `RoadGraph.cs` 内完成，无外部接口变更
- 已用用户提供的精确日志数据验证 BUG-5 + BUG-6 + BUG-7 场景：
  - 第一条路：(-300,0)→(500,0)，CellSize=100，8 格东向
  - 第二条路：(100,-200)→(100,200)，4 格南向
  - 交叉点 (100,0) 现在能正确检测并拆分已有边

## BUG-8 / BUG-9 验证状态

- 关联提交：`6ec0a66`（`修复：保持道路类型存档并避免重复铺路副作用`）
- `dotnet build SimpleCities.sln`：构建成功，0 个错误，4 个既有的 `Scripts/Grid/MapBackground.cs` nullable 警告
- 已核对当前代码路径：存档捕获与恢复均处理 `RoadType`，且完整覆盖检查位于 `ResolveIntersections`、`SplitEdgesAtPathAnchors` 等变更操作之前
- 当前仓库未发现覆盖上述两个场景的自动化测试；本次未执行 Godot 运行时存档往返或重复铺路手工测试，因此不声明运行时回归验证已完成
