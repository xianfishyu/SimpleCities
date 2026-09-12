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
- `road-graph:BUG-8` 自动化回归（2026-07-22）：`RoadGraphCoverageTests` 覆盖完全重复路径、带内部锚点的完全覆盖路径和拒绝后 ID 分配状态；临时移除前置覆盖检查时 2 个关键场景失败且命令退出码为 1，恢复后聚焦测试 3/3 通过。
- `save-system:BUG-1` 的存档往返验证仍见 `docs/bugfix/save-system.md`；本条自动化证据只声明 `road-graph:BUG-8` 的重复铺路无副作用行为。

---

<a id="road-graph-bug-9"></a>
## BUG-9：跨 RoadGroup 或 RoadType 的共线边被自动合并

### 症状

两次独立铺设的共线道路会合并为一条边，后创建的 `RoadGroup` 被移除，且其 `RoadType` 可能被第一条边覆盖。

### 根因分析

`TryMergeAtNode` 只检查两条边是否共线，随后选择较小的 `GroupID` 和 `edgeA.Type` 创建替代边，没有验证两条边属于同一玩家操作和道路类型。

### 修复方案

合并前要求 `GroupID` 与 `RoadType` 都一致，并保留原 Group ID。不同 Group 或 Type 的边保留共享节点和各自边。

### 影响范围

影响 `AddRoad`、`RemoveEdge` 和 `RemoveRoadGroup` 触发的共线合并；同 Group、同 Type 的内部压缩行为保持不变。

---

<a id="road-graph-bug-10"></a>
## BUG-10：长边的中点命中和交叉候选检索遗漏

### 症状

没有 waypoint 的长边在中点无法被拆除工具命中；短边穿过该长边中段时，也不会创建连接交点。

### 根因分析

空间索引仅存储端点和 waypoint。`FindClosestEdge` 比较采样点距离，`QueryCandidateEdgeIDs` 也只能发现查询圆内的采样点，因此线段虽经过查询区域仍可能完全漏检。

### 修复方案

新增 `EdgeSegmentRef`，将每个边子线段登记到其 AABB 覆盖的 bucket，并按点到线段距离过滤半径查询。最近边查询在候选边集合内按完整折线的最小距离排序。

### 影响范围

影响道路拆除悬停、半格吸附、最近边查询和新增道路的交叉解析。bucket 覆盖可产生候选假阳性，但最终几何距离和交叉计算会过滤它们。

---

<a id="road-graph-bug-11"></a>
## BUG-11：`GraphEdge.Points` 允许绕过 RoadGraph 修改几何

### 症状

调用方修改从 `GraphEdge.Points` 获得的数组，会直接改变存档几何，却不会同步长度或空间索引。

### 根因分析

`GraphEdge` 将构造参数数组直接公开，公共属性返回同一数组引用。

### 修复方案

构造时复制输入数组，公共 `Points` 属性返回防御性副本；图内部通过 `InternalPoints` 读取权威数组，渲染器一次取得副本后构建绘制点。

### 影响范围

外部调用方不再能原地修改道路几何；现有数组形状的公共 API 保持兼容。

## BUG-9 至 BUG-11 验证状态

- `dotnet test tests/SimpleCities.RoadGraph.Tests/SimpleCities.RoadGraph.Tests.csproj --no-restore`：39 通过、0 失败、0 跳过。
- `dotnet build SimpleCities.sln --no-restore`：0 警告、0 错误。

- 回归用例：`RoadGraphRegressionTests` 覆盖跨 Group/Type 合并、长边中点命中、长边中段交叉和 `Points` 防御性副本。
- Godot `MapTest` 场景可启动且编辑器、运行时控制台无新增错误；运行时状态桥接在时间推进和状态读取时超时，未取得输入驱动的端到端断言。

---

<a id="road-graph-bug-12"></a>
## BUG-12：删除道路后自动合并未删除的边

### 症状

删除穿过交点的整组道路后，原先被劈分的另一组道路会自动合并为替代边，交点节点随之消失。

### 根因分析

`RemoveRoadGroup` 在批量移除边后收集受影响节点并调用 `TryMergeAtNode`；公共 `RemoveEdge` 也允许删除后合并。该修复操作改变了未删除边的数量和节点拓扑。

### 修复方案

单边和整组删除均使用抑制合并的内部删除路径；删除仅清理目标边、孤立节点和空 Group，不再创建替代边。

### 影响范围

`AddRoad` 仍可在同一玩家操作内压缩共线边；`RemoveEdge` 与 `RemoveRoadGroup` 不再压缩其余道路拓扑。

## BUG-12 验证状态

- `RoadGraphRegressionTests.RemoveRoadGroup_CrossingRoad_DoesNotMergeRemainingSegments`：先创建十字路口，再删除交叉组；断言交点仍存在、具有两条剩余边，且道路组保留。
- `dotnet test tests/SimpleCities.RoadGraph.Tests/SimpleCities.RoadGraph.Tests.csproj --no-restore`：40 通过、0 失败、0 跳过。
- `dotnet build SimpleCities.sln --no-restore`：0 警告、0 错误。

---

<a id="road-graph-bug-13"></a>
## BUG-13：任意角度共线边无法合并

### 症状

直接调用 `RoadGraph.AddRoad` 添加任意角度的共线多段路径时，同一操作内的边不会压缩为一条带 waypoint 的边。

### 根因分析

`TryMergeAtNode` 通过 `DirectionUtil` 的 8 方向匹配判断反向，非 8 方向向量无法得到方向枚举值。

### 修复方案

改为基于交点两侧局部向量的叉积和点积，要求向量共线且方向相反；现有 Group 和 Type 约束继续生效。

### 影响范围

影响数据层任意角度路径的共线压缩，不改变 `RoadBuilder` 的 8 方向输入限制。

---

<a id="road-graph-bug-14"></a>
## BUG-14：最近边查询排除恰好位于半径边界的道路

### 症状

道路与查询圆相切时，空间索引已返回候选边，但 `FindClosestEdge` 返回 `null`。

### 根因分析

候选边的距离使用严格小于 `maxRadius` 的平方进行比较，和空间索引的包含边界语义不一致。

### 修复方案

距离比较改为小于或等于半径平方，使最终筛选和空间索引的圆形查询都包含边界。

### 影响范围

影响拆除、悬停和吸附在最大命中半径边界上的行为。

## BUG-13 至 BUG-14 验证状态

- `RoadGraphRegressionTests.AddRoad_ArbitraryAngleCollinearSegments_MergeWithinTheSameGroup` 与 `FindClosestEdge_EdgeAtRadiusBoundary_IsIncluded` 覆盖任意角度合并和半径边界命中。
- `dotnet test tests/SimpleCities.RoadGraph.Tests/SimpleCities.RoadGraph.Tests.csproj --no-restore`：42 通过、0 失败、0 跳过。
- `dotnet build SimpleCities.sln --no-restore`：0 警告、0 错误。

## BUG-9 至 BUG-14 提交前复核

- `dotnet test tests/SimpleCities.RoadGraph.Tests/SimpleCities.RoadGraph.Tests.csproj --no-restore`（2026-08-02）：52 通过、0 失败、0 跳过。
- `dotnet build SimpleCities.sln --no-restore`（2026-08-02）：0 警告、0 错误。
- `godot --headless --path . --log-file .godot/qa-roadgraph-commit.log --script tests/godot/pause_menu_runtime_contract.gd`（2026-08-02，沙箱外运行）：输出 `PASS pause menu runtime contract`，验证 `MapTest` 可装载、RoadGraph 可随场景注册并参与保存/加载；两条 `ConstructionDock` 缺少 `ToolManager.Instance` 的警告来自测试在节点进入树前读取 authored 状态，不属于本组道路修复。
- Godot MCP 可启动 `MapTest`，停止后编辑器没有新增错误，DAP `stderr` 为空；`godot_game_time step` 与 `godot_exec` 状态桥接仍超时，因此没有声明输入驱动的铺路/拆路端到端断言通过。
- 当前会话未提供 `csharp-ls` MCP，逐文件 LSP 诊断不可用；编译器与测试项目构建已覆盖全部改动 C# 文件。

---

<a id="road-graph-bug-15"></a>
## BUG-15：节点吸附在半径边界和多候选场景下选择不一致

### 症状

`FindClosestNode` 无法命中恰好位于查询半径边界上的节点。`AddRoad` 在新端点同时落入多个节点的 `0.5f` 吸附半径时，`GetOrCreateNode` 会复用空间索引首先枚举的节点，即使另一个候选更近；当存档中的节点恢复顺序变化时，等距候选的选择也会随插入顺序变化。

### 根因分析

节点查询和节点复用维护了两套不同逻辑：`FindClosestNode` 使用严格小于半径平方的比较，因此排除边界；`GetOrCreateNode` 则在 `UniformGrid.QueryRadius` 中遇到第一个 Node ref 后立即返回，没有比较全部候选的距离，也没有稳定的等距决胜规则。空间桶和字典的枚举顺序不属于节点身份契约，不能决定拓扑焊接结果。

### 修复方案

新增统一的 `FindClosestIndexedNode` 选择路径，由 `FindClosestNode` 和 `GetOrCreateNode` 共同使用。它检查半径内全部有效 Node ref，选择几何距离最近的节点；距离由 `Mathf.IsEqualApprox` 判定为相同时选择较小 Node ID，并允许首个候选位于半径边界。`SnapRadius` 保持 `0.5f`，不引用 `CellSize` 或 UI 网格设置。

### 影响范围

影响 `FindClosestNode` 查询，以及新增道路、交叉拆分和其他通过 `GetOrCreateNode` 复用节点的 RoadGraph 操作。存档 schema、节点坐标、空间索引结构和当前输入网格策略均未改变。

## BUG-15 验证状态

- `RoadGraphNodeIdentityTests` 修复前 7 项中 3 项失败，分别证明边界遗漏、错误复用较远节点和恢复顺序影响等距选择；修复后 7/7 通过。
- `RoadGraphRegressionTests`：17/17 通过，既有交叉、拆分、删除和空间命中行为未回归。
- `dotnet test SimpleCities.sln --configuration Debug --no-build --no-restore`：59 通过、0 失败、0 跳过。
- `dotnet build SimpleCities.sln --configuration Debug --no-restore`：0 警告、0 错误。
- 当前会话未提供 `csharp-ls` MCP，逐文件 LSP 诊断不可用；本修复属于无场景树依赖的 RoadGraph 数据层行为，未执行 Godot 场景运行验证。

---

<a id="road-graph-bug-16"></a>
## BUG-16：近似共享端点被误判为内部交叉并重建既有边

### 症状

已有道路端点位于 `(0,0)` 时，从几何 epsilon 内的 `(0,0.005)` 铺设一条斜穿道路，会把两端视为独立点并计算出极靠近端点的内部交叉。新增道路能够完成，但既有 Edge 被拆除并以新 ID 重建，产生不必要的拓扑和事件变更。

### 根因分析

`TryComputeInteriorCross` 仅使用 `Vector2 ==` 排除共享端点。两个端点只要存在任何浮点偏差，就会继续执行直线交点计算；当交点参数仍落在 `(GeometryEpsilon, 1 - GeometryEpsilon)` 内时，`ResolveIntersections` 会收集该交点并拆分既有边。该严格相等规则与 RoadGraph 其他几何位置使用距离平方 epsilon 的语义不一致。

### 修复方案

新增 `ArePositionsApproximatelyEqual`，统一使用 `DistanceSquaredTo < GeometryEpsilon` 判断端点近似相等。`TryComputeInteriorCross` 在计算交点前检查四种端点组合；近似共享端点直接退出，非端点内部交叉继续沿原参数和叉积逻辑处理。

### 影响范围

影响 `AddRoad` 的直线子段交叉解析，尤其是浮点计算或未来曲线离散入口产生的近端点坐标。平行判断、交点参数范围、节点 `SnapRadius`、存档和输入网格均未改变。

## BUG-16 验证状态

- 修复前 `RoadGraphRegressionTests` 21 项中 1 项失败：`0.005f` 近似共享端点导致原 Edge ID 消失；完全相同端点和真实内部交叉通过。
- 修复后 `RoadGraphRegressionTests`：21/21 通过。
- `dotnet test SimpleCities.sln --configuration Debug --no-build --no-restore`：63 通过、0 失败、0 跳过。
- `dotnet build SimpleCities.sln --configuration Debug --no-restore`：0 警告、0 错误。
- 当前会话未提供 `csharp-ls` MCP，逐文件 LSP 诊断不可用；本修复为无场景树依赖的 RoadGraph 几何逻辑，未执行 Godot 场景运行验证。

---

<a id="road-graph-bug-17"></a>
## BUG-17：曲线与直线内部相切被重复报告为大量交点

### 症状

一条 cubic Bézier 在内部参数处与直线相切时，递归交点查询把相切点附近落入空间容差带的连续叶节点分别返回，聚焦测试得到 75 个 `Crossing`，而不是一个 `Tangent`。

### 根因分析

`RoadGeometryIntersectionQuery.FindLineIntersections` 对每个收敛子曲线独立生成候选，只按极小的位置阈值即时去重。相切曲线在切点附近与直线的距离呈二次增长，多个相邻参数叶节点都满足空间容差；这些候选之间超过即时位置阈值，因此没有被识别为同一个几何根，且偏离精确切点的候选切线叉积仍被分类为交叉。

### 修复方案

候选按曲线参数排序，并依据空间容差与曲线长度推导参数归并阈值，将连续叶节点组成同一参数簇。每个簇只保留两条原生几何之间残差最小的候选，再用该候选的权威切线重新分类。独立交点之间存在不命中的参数间隔，仍形成不同簇。

### 影响范围

影响直线与任意原生曲线的相切结果数量和 `Tangent` 分类。直线解析交叉、直线重叠标记、曲线权威参数、显示采样和 RoadGraph 写入链均未改变。

## BUG-17 验证状态

- 修复前 `RoadGeometryLineIntersectionTests.LineBezier_InteriorTangencyIsClassified` 返回 75 个候选；修复后只返回一个参数接近 `0.5` 的 `Tangent`。
- `RoadGeometryLineIntersectionTests`：15/15 通过，覆盖解析直线交叉、端点接触、重叠、平行分离、多交点、内部相切、圆弧双交点、四类一般曲线的已知参数交点及非法容差拒绝。
- `dotnet test SimpleCities.sln --configuration Debug --no-build --no-restore`：271 通过、0 失败、0 跳过。
- `dotnet build SimpleCities.sln --configuration Debug --no-restore`：0 警告、0 错误。
- 当前会话未提供 `csharp-ls` MCP，逐文件 LSP 诊断不可用；本修复为纯几何查询行为，未执行 Godot 场景运行验证。

---

<a id="road-graph-bug-18"></a>
## BUG-18：图不变式严格比较原生拆分端点

### 症状

在 `RoadGraph.AssertInvariants` 已接入原生 Edge 拆分提交后，圆弧和回旋线的既有拆分用例抛出 `InvalidOperationException: Edge 6 geometry does not end at node 2.`。直线等能够精确复现端点的几何不受影响。

### 根因分析

`RoadGraph.AssertInvariants` 使用严格 `Vector2 ==` 比较 Edge 首尾几何端点与 Node 位置。`CircularArcRoadGeometrySegment` 和 `ClothoidRoadGeometrySegment` 的解析拆分会产生约 `1e-6` 的合法浮点残差；该比较把满足项目 `GeometryEpsilon` 空间契约的端点误判为图损坏。

### 修复方案

端点不变式改用 RoadGraph 已有的 `ArePositionsApproximatelyEqual`，与交点、覆盖和拆分共用 `GeometryEpsilon` 位置语义。其余 Node/Edge ID、邻接、Group 和空间引用断言仍保持严格检查。

### 影响范围

影响 Debug 构建中圆弧、回旋线及其他可能产生合法浮点残差的原生几何拆分诊断。Release 行为、序列化格式、拓扑身份、节点吸附半径和生产几何数据均未改变。

## BUG-18 验证状态

- 修复前原生拆分聚焦测试中 `CircularArcRoadGeometrySegment` 与 `ClothoidRoadGeometrySegment` 两项失败；修复后相关聚焦测试 19/19 通过。
- `dotnet test SimpleCities.sln --configuration Debug --no-restore`：372 通过、0 失败、0 跳过。
- `dotnet build SimpleCities.sln --configuration Debug --no-restore`：0 警告、0 错误。
- `dotnet run --project tests/SimpleCities.RoadGraph.Performance/SimpleCities.RoadGraph.Performance.csproj --configuration Release --no-restore -- --enforce-budget`：10k 全部场景通过 16.67 ms P95 硬门槛，单边删除 P95 为 0.038 ms。
- 当前会话未提供 `csharp-ls` MCP，无法执行逐文件 C# LSP 诊断；Godot 运行时契约授权重跑输出 `PASS pause menu runtime contract`，两轮 autosave 保存/加载通过。

---

<a id="road-graph-bug-19"></a>
## BUG-19：旧折线提交与已有原生曲线使用不同的几何语义

> 修复日期：2026-08-10
> 来源：`docs/bugfix/session-2026-08-05.md#session-bug-01旧折线提交路径把原生曲线当作端点弦线处理`

### 症状

图中已有通过 `SubmitPath()` 保存的 Bezier、圆弧或其他原生曲线时，再调用 `SubmitPolyline()` / `AddRoad()` 添加折线，交叉拆分和覆盖判断会把既有曲线退化为端点弦线。真实曲线交点可能遗漏，弦线上的假交点也可能被错误收集。

### 根因分析

旧折线入口继续执行依赖 `GraphEdge.GetFullPath()` 的折线规划，而单段原生曲线没有旧式 waypoint，`GetFullPath()` 只返回两个端点。空间索引虽然能选中曲线候选，候选后的交叉、覆盖和拆分计算却没有使用权威 `GeometrySegments`。

### 修复方案

`RoadGraph.SubmitPolyline()` 在检测到图中存在非直线原生段时，把输入折线转换为 `LineRoadGeometrySegment` 组成的 `RoadPath`，再进入与 `SubmitPath()` 相同的 `SubmitPathCore()`。纯折线图仍保留原入口行为；混合图则统一使用原生几何交叉、覆盖和拆分语义。

### 影响范围

影响原生曲线已经存在时的旧折线提交。纯折线图的旧路径、原生路径格式、RoadGroup 身份和显示采样均未改变。

## BUG-19 验证状态

- 回归测试 `SubmitPolyline_CrossingBezierUsesNativeCurveGeometry` 验证折线在真实 Bezier 内部交点拆分两组道路并形成四度节点。
- 回归测试 `SubmitPolyline_CrossingOnlyBezierEndpointChordDoesNotCreateFalseIntersection` 验证只穿过端点弦线的折线不会生成假交点。
- `dotnet test SimpleCities.sln --no-restore`：492/492 通过；`dotnet build SimpleCities.sln --no-restore`：0 警告、0 错误。
- Roslyn CodeLens 解决方案诊断为 0 error、0 warning；Godot 4.7 editor 的 `MapTest.tscn` 错误日志为 0。

---

<a id="road-graph-bug-20"></a>
## BUG-20：最近节点和最近边查询没有统一拒绝非法参数

> 修复日期：2026-08-10
> 来源：`docs/bugfix/session-2026-08-05.md#session-bug-02最近节点边查询未在入口校验非法位置和半径`

### 症状

`FindClosestEdge()` 和 `FindClosestNode()` 对负数或非有限半径静默返回 `null`，对非有限坐标则可能在候选几何内部抛出异常。同类入口 `FindEdgeIDsNear()` 已经在公开 API 边界明确拒绝这些参数，导致空间查询契约不一致。

### 根因分析

两个最近查询直接把参数传给 `UniformGrid.QueryRadius()`，没有复用 `FindEdgeIDsNear()` 的有限坐标、有限半径和非负半径检查。错误表现因此依赖桶遍历是否执行以及是否命中候选几何。

### 修复方案

新增共享的 `ValidateSpatialQuery()`，由 `FindClosestEdge()`、`FindClosestNode()` 和 `FindEdgeIDsNear()` 在进入空间索引前调用。非法位置统一抛出 `ArgumentException`，非法半径统一抛出 `ArgumentOutOfRangeException`；正常查询的边界包含语义不变。

`DebugPanel` 同时避免在 headless 环境中把非有限鼠标世界坐标或网格坐标传入严格查询；该兼容处理只影响调试文本显示，非法查询契约本身仍保持严格。

### 影响范围

影响公开最近节点/边查询收到调用方非法参数时的失败方式，以及 headless 调试面板的无鼠标状态。正常有限查询、空间索引内容和道路拓扑不变。

## BUG-20 验证状态

- `ClosestQueries_RejectNonFinitePosition` 覆盖 `NaN` 和正负无穷坐标；`ClosestQueries_RejectInvalidRadius` 覆盖负数、`NaN` 和正无穷半径。
- `dotnet test SimpleCities.sln --no-restore`：492/492 通过；`dotnet build SimpleCities.sln --no-restore`：0 警告、0 错误。
- `road_system_v2_final_runtime_contract.gd` 输出 `PASS`；严格查询接入后真实道路系统运行路径无新增失败。

---

<a id="road-graph-bug-21"></a>
## BUG-21：空间索引覆盖不变式使 100k V3 Load 二次退化并无响应

> 修复日期：2026-08-14
> 影响文件：`Scripts/Road/RoadGraph.Diagnostics.cs`、`Scripts/Road/SpatialIndex.cs`、`tests/SimpleCities.RoadGraph.Tests/UniformGridInvariantTests.cs`

### 症状

Godot 道路渲染性能契约写入大型 V3 fixture 后停在 `STAGE load-start`。旧 10k Load/renderer rebuild 约需 18 秒，20k 已长时间无进展，100k 进程被 Windows 判定为 AppHang；payload 写入和 manifest hash 阶段均已完成，因此卡顿发生在 Load 构图/不变式阶段，而不是 fixture I/O。

### 根因分析

Debug `RoadGraph.AssertInvariants()` 对每个 Node ref 和每个 Edge query fragment 分别调用 `UniformGrid.HasExactCoverage(reference, bounds)`。旧方法每次都遍历 `_buckets` 的全部 bucket 与条目来寻找单个引用；随着图规模增长，总成本约为 `空间引用数 × bucket 数`，而 V3 Load 在完成 prepared graph 和 full-reset commit 时都会执行这条严格不变式。100k 独立 Edge 同时带来大量引用和 bucket，因此正确的 payload 被二次诊断扫描拖入近似二次复杂度。

### 修复方案

`RoadGraph.AssertInvariants()` 先用引用 identity 字典收集所有已注册引用及其预期 bounds，再一次调用批量 `UniformGrid.HasExactCoverage(expectedBounds)`。批量实现为每个引用计算预期 `BucketCoverage` 与剩余条目数，然后只扫描一次全部实际 bucket entry；扫描中同时验证引用是否已注册、是否落在预期 bucket、同一 bucket 是否重复、引用是否出现过多，最后比较 `_referenceEntryCount` 并要求每个预期条目计数归零。

该修复没有削弱诊断：缺失引用、额外引用、错桶、同桶重复、重复 expected identity 和内部条目计数不一致仍返回失败。复杂度降为预期引用与实际索引条目的线性总和；图拓扑、存档格式、空间查询结果和 Release mutation 行为不变。

### 影响范围

影响 Debug invariant、V3 Load/full reset 以及任何提交后运行 `AssertInvariants()` 的大型 RoadGraph 操作。`UniformGridInvariantTests` 直接覆盖单桶/跨桶合法情况和缺失、额外、错桶、重复、计数不符；正常查询、query fragment 半开所有权和容量上限未改变。

## BUG-21 验证状态

- 修复前证据：10k Load/重建约 18 秒，20k 停在 `STAGE load-start`，100k 被 Windows 判为 AppHang；新增 `STAGE fixture-write-*`、`manifest-hash-*`、`load-*`、`renderer-count-*` 将停顿定位到 Load。
- 逐级恢复：12k、20k、40k、60k、80k、100k Load/renderer rebuild 分别为 906.549、1244.115、2563.902、3429.621、4327.669、5069.431 ms；100k 单规模契约完整通过并删除测试槽。
- `UniformGridInvariantTests` 覆盖合法单桶/跨桶及缺失、额外、错桶、同桶重复和 `_referenceEntryCount` 不符；聚焦 invariant + V3 persistence 为 31/31，`dotnet test SimpleCities.sln --no-restore` 为 637/637。
- `dotnet build SimpleCities.sln --no-restore` 与 Release performance build 均为 0 警告、0 错误；Roslyn compiler/analyzer 为 0 diagnostics；Godot 4.7 相关 8 个 GDScript parser 检查为 8/8，editor error 与 DAP console 增量通道为空。
- `dotnet run --project tests/SimpleCities.RoadGraph.Performance/SimpleCities.RoadGraph.Performance.csproj --configuration Release --no-restore -- --enforce-budget` 通过：10k 最坏多交叉 P95 为 7.556 ms，100k 多交叉为 10.797 ms。
- Godot 默认 `--enforce-budget` 第一次冷启动虽完成 10k/100k，但 10k camera/preview/highlight P95 为 20.041/17.278/20.505 ms，超过 16.67 ms，门禁失败；原样复跑为 16.471/14.702/13.750 ms 并通过，100k 为 18.641/19.901/15.825 ms、重建 4393.478 ms。该冷启动抖动保留为 Phase 7/8 风险，不归因于本次二次复杂度修复。
- Windows Debug QA 导出包的 V3 根、manifest family/schema/长度/hash 与删除契约通过；Release 导出因 ImGui GDExtension 导出错误且 MCP 6550 端口占用未正常返回，未记为通过。两个中断遗留性能槽已删除，测试结束后项目停止且错误通道为空。
- 后续最终复跑（2026-08-14）中，Release C# 10k 最坏多交叉 P95 为 7.783 ms、100k 为 10.120 ms；Vulkan 10k camera/preview/highlight P95 为 2.183/2.633/2.297 ms，100k 为 2.088/1.959/1.853 ms，100k Load 与 renderer rebuild 为 4111.449 ms，契约输出 PASS。Windows Desktop QA 导出也已在显示驱动下通过；这些后续证据补齐导出与抖动复核，不改变 BUG-21 的线性覆盖修复范围。
- 本轮收口复跑（2026-08-14）继续通过：Vulkan 10k camera/preview/highlight P95 为 0.410/0.477/0.395 ms，100k 为 0.635/0.697/0.612 ms，100k Load 与 renderer rebuild 为 3824.467 ms，`.godot/qa-road-rendering-performance-v3-current.log` 输出 PASS；renderer lifecycle 和当前 V3 综合运行时契约也分别输出 PASS。完整自动化更新为 720/720，双配置构建及 Roslyn diagnostics 继续为 0。该热复跑只增加当前证据，不覆盖前述首次冷启动失败，也不把尚未完成的 Phase 7 分级 surface 门禁记为通过。
- 再次复验（2026-08-17）：invariant + V3 persistence 聚焦组合为 33/33，完整自动化为 833/833，Debug/`ExportRelease` build 均为 0 警告、0 错误。Vulkan 12k/20k/40k/60k/80k/100k Load 与 renderer rebuild 为 779.503/1193.439/2428.374/3431.815/4463.697/5129.679 ms，100k camera/preview/highlight P95 为 0.660/0.658/0.675 ms；独立 10k 硬门为 0.461/0.482/0.491 ms、重建 648.221 ms，各级均 PASS、静态 renderer 节点为 2。Release C# 10k/100k 多交叉 P95 为 7.848/8.363 ms；renderer lifecycle、V3 综合、render token、RoadType style/mesh、输入、闭环、六类几何和 Windows QA 导出包 writable 存档契约均 PASS。当前会话未暴露 Roslyn/Godot MCP 调用工具，未刷新历史 MCP/DAP 证据；两个 2026-08-16 既有损坏 QA 槽、headless editor 的 main-scene UID 提前解析和 ImGui 禁用导出分支的预期 GDExtension error 均保留为无关环境/工具输出。
- 当前复验（2026-08-20）：`dotnet test SimpleCities.sln --no-restore` 为 849/849；Debug 与 `ExportRelease` build 均为 0 警告、0 错误。Release 性能入口 10k/100k 多交叉 P95 为 7.575/6.339 ms。隔离 `APPDATA` 的 Godot Vulkan `road_rendering_performance_contract.gd --enforce-budget` 输出 `PASS`：10k camera/preview/highlight P95 为 0.549/0.541/0.572 ms、Load/renderer rebuild 为 718.363 ms；100k 为 0.710/0.682/0.689 ms、Load/renderer rebuild 为 4617.638 ms，静态 renderer 节点为 2。该 CLI 运行只留下直接实例化场景时既有 `ConstructionDock: ToolManager.Instance is missing` warning；Roslyn/Godot MCP/DAP 未暴露，headless editor 的 main-scene UID/6550 端口错误仍为环境/工具阻塞，不计作 BUG-21 回归。
- 当前复验（2026-08-21）：`dotnet test SimpleCities.sln --no-restore` 为 863/863；Debug 与 `ExportRelease` build 均为 0 警告、0 错误。Release RoadGraph 性能入口 10k/100k 多交叉 P95 为 7.375/8.421 ms。真实 Vulkan owner-dense 10k/100k Load/renderer rebuild 为 736.276/6135.119 ms，camera/preview/highlight P95 分别为 0.482/0.467/0.468 与 0.573/0.625/0.561 ms，四类 owner 查询均 20 批 × 1,000 次完整命中；Roslyn compiler/analyzer 为 0，Godot editor error channel 为 0。GDScript workspace scan 仍见一条与磁盘源码行号不一致的疑似缓存 `REDUNDANT_AWAIT`，editor log 另有既存 `LSP: Client is opening already opened file`，两者均未归因于本轮 C# 测试，也未作为通过门禁。该复验确认 BUG-21 修复后的 100k Load 路径继续保持线性恢复；owner-dense 夹具宽度修正另记录为 `grid-rendering` BUG-7，不改变 RoadGraph 修复范围。
- 追加收口复验（2026-08-21）：`dotnet test SimpleCities.sln --no-restore` 为 864/864；Release 性能入口 `--enforce-budget` 的 10k 多交叉 P95 为 7.247 ms、100k 为 7.431 ms，10k 全场景均低于 16.67 ms 硬门槛。该次 C# 性能结果与上述真实 Vulkan owner-dense 证据共同确认线性 coverage 修复未回归；Phase 7/8 的完整表现与故障矩阵仍不属于 BUG-21 完成范围。
- 当前 100k grid Vulkan 复验（2026-08-21）：隔离 APPDATA 的 `godot --path . --script tests/godot/road_rendering_performance_contract.gd -- --dataset-kind=grid --dataset-size=100000 --enforce-budget` 退出码为 0，输出 `PASS`。camera/preview/highlight P95 为 `0.635/0.664/0.669 ms`，Load/renderer rebuild 为 `4980.748 ms`，draw calls 为 `4/5/4`，objects 为 `4/56/4`，静态 renderer 节点为 `2`；stderr 仅有既有 `ConstructionDock: ToolManager.Instance is missing` warning。该复验只刷新独立 grid 压力证据，不覆盖 10k 冷启动风险或 Phase 7/8 完整矩阵；当前会话未暴露 Roslyn CodeLens 与 Godot MCP/DAP，未将其记为通过。
- Phase 8 重新开始后的必需门复验（2026-08-24，`b95e295`）：`UniformGridInvariantTests` 与 `RoadGraphPersistenceV3Tests` 为 33/33，完整 `dotnet test SimpleCities.sln --no-restore` 为 959/959；Debug 与 `ExportRelease` build 均为 0 警告、0 错误。目标性能脚本 `--check-only` 退出码为 0；隔离 APPDATA 的真实 Vulkan junction-dense 10k camera/preview/highlight P95 为 `0.580/0.655/0.532 ms`、Load/renderer rebuild 为 `785.455 ms`，geometry-dense 10k 为 `0.569/0.709/0.546 ms`、`2786.748 ms`，两项均输出 `PASS` 且静态 renderer 节点为 `2`。本次没有运行非必需的 100k；历史 12k～100k 逐级恢复证据继续保留。CLI 只出现直接实例化场景时既有的 `ConstructionDock: ToolManager.Instance is missing` warning；本轮工具集未暴露 Roslyn CodeLens、Godot MCP 或 minimal DAP，因此这些门没有刷新，也未记为通过。该复验确认 BUG-21 在线性修复后的必需 10k 路径没有回归，不代替仍开放的 Phase 8 代表性组合与 MCP 门。
- 当前 100K 压力矩阵（2026-08-24，`0b39871`）：grid、junction-dense、geometry-dense、owner-dense 四个独立 APPDATA/Vulkan 进程均退出 0 并输出 PASS，观测 Load/renderer rebuild 分别为 `5292.983/5271.321/24429.000/6091.620 ms`，camera/preview/highlight P95 分别为 `0.736/0.770/0.764`、`0.662/0.676/0.668`、`0.780/0.741/0.679`、`0.656/0.648/0.655 ms`，静态 renderer 节点全部为 `2`。以互不重叠的 worker Prepare、Preflight 与 aggregate commit 三段合计为分母，worker 占 `96.83%～98.91%`；aggregate 内部的 reference commit 只有 `6.082～8.342 ms`。owner-dense 四类 surface 各完成 20 批 × 1,000 次查询，记录的是 20 个批量摊销单次均值样本。测试根均已送入回收站，CLI 仅保留既有 ConstructionDock 缺依赖 warning。完整方法、结果、统计口径与技术原理见 `docs/performance/road-system-v3-100k-technical-analysis.md`；100K 仍是额外压力记录，不改变 10K 硬门定义。

---

<a id="road-graph-bug-22"></a>
## BUG-22：删除两路口环的一侧支路后 seam 无法迁移到剩余 junction

> 修复日期：2026-08-14
> 影响文件：`Scripts/Road/RoadGraph.Canonicalization.cs`、`tests/SimpleCities.RoadGraph.Tests/RoadGraphClosedPathV3Tests.cs`、`tests/SimpleCities.RoadGraph.Tests/RoadRendererLoadPrepareTests.cs`
> 关联事项：`v3-grid-rendering:2.0`、`v3-road-graph:8.6`

### 症状

一个闭环在两个不同节点各接一条支路时，环由两条 junction 间弧和两条支路组成。删除原 rooted seam 一侧的支路后，该节点只剩同类型的两条环弧，本应被消除并把两条弧合并为以另一处真实 junction 为 seam 的 self-loop；旧实现却保留原二 incidence 节点和两条平行环弧，导致最大连续 Edge 规范形没有恢复，renderer 也会保留不应存在的 seam 边界。

### 根因分析

`RoadGraph.Canonicalization.TryMergeAtNode()` 在两条待合并 Edge 的远端相同时包含额外守卫：

```csharp
if (firstFarID == secondFarID && nodeID < firstFarID)
    return false;
```

该判断只比较 Node ID，无法区分“全分量都是二 incidence、必须保留一个确定 seam 的纯环”和“远端仍有支路、当前节点已经不是结构边界”的两路口环。当前规范化流程已经由 `FindProtectedCycleSeams()` 只为同类型纯二度闭合分量保护最小 Node ID；旧守卫与这条结构化规则重复，并在真实 junction 仍存在时错误阻止合法合并。

### 修复方案

移除 `TryMergeAtNode()` 中按 `firstFarID == secondFarID` 与 Node ID 拒绝合并的旧守卫。未被 `FindProtectedCycleSeams()` 保护的二 incidence 节点现在继续走既有 typed geometry 拼接、canonicalize、容量校验和最小 Edge ID 保留流程；当两条弧的远端相同时，结果自然成为以剩余 junction 为 A/B 端的 canonical self-loop。纯二度环仍由 protected seam 集合保留确定 seam，不会被本修复消除全部节点。

### 影响范围

影响删除或其他规范化操作使二 incidence 节点的两条同类型 Edge 指向同一远端节点的场景，主要是两路口环移除一侧支路后的 seam 重定位。异类型 semantic boundary、纯二度闭合分量的 protected seam、非环合并、geometry 校验和 ID 保留规则不变。

## BUG-22 验证状态

- `RoadGraphClosedPathV3Tests.RemoveEdge_TwoJunctionLoopRelocatesSeamToRemainingJunction` 验证原 seam 与已删支路端点消失，剩余 junction 具有 3 条 incidence，图收敛为 1 条 rooted self-loop 与 1 条支路，共 2 Node / 2 Edge。
- `RoadRendererLoadPrepareTests.PurePreparer_RemovingOneBranchRelocatesSeamAndClosesRemainingLoop` 验证删除前后 marker、closed 点列、顶点和索引均与重定位后的拓扑一致；`PurePreparer_FigureEightClosesBothLoopsAndKeepsSharedJunction` 继续保护共享 junction 上的两个 closed ribbon。本轮相关聚焦组合为 33/33。
- `dotnet test SimpleCities.sln --no-restore`：727/727 通过；Debug 与 `ExportRelease` build 均为 0 警告、0 错误；Roslyn compiler/analyzer 为 0 diagnostics。
- `road_closed_ribbon_runtime_contract.gd` 输出 `PASS`：aggregate Load 后的两路口环删除 seam 侧支路前为 `4 Edge / 20 mesh vertices / 4 node markers`，删除后为 `2 Edge / 12 mesh vertices / 2 node markers`。Godot MCP 冻结场景显示原 seam 无伪标记，剩余 junction/endpoint 正确，editor error 与 DAP `stderr` 均为空。

---

<a id="road-graph-bug-23"></a>
## BUG-23：单格编辑误接受同 token 的另一候选目标格段

> 修复日期：2026-09-12
> 影响文件：`SimpleCities.RoadCore/RoadSpanEditPlanner.cs`、`tests/SimpleCities.RoadCore.Tests/RoadSpanEditTests.cs`
> 关联事项：GitHub #13、#14

### 症状

从同一个来源快照规划两条不同长度的道路，两个未提交候选的目标token可以相同。提交其中一个候选后，把另一个候选上的格段传给单格删除或类型改造，会被错误接受，尽管该格段的区间和链点并不对应当前路网。回归测试 `SpanFromDifferentUncommittedTargetWithSameToken_IsRejectedBeforeAnyEdit` 在修复前报告预期 `Rejected`、实际 `Ready`。

### 根因分析

`RoadSpanEditPlanner.Plan()` 原先只验证来源token和EdgeId存在，随后直接使用传入格段的参数区间和链点。候选目标尚未提交时，这两个字段不足以证明格段内容属于当前快照；同token不保证两个独立候选的几何相同。

### 修复方案

在创建编辑草稿及判断同类型无需改变之前，使用传入格段首个参数区间的中点，在当前来源快照重新查询规范格段，并逐项比较 `Key`、`Ranges` 和 `Points`。内容不一致立即拒绝，不分配实体或修改活动路网。

### 影响范围

收紧V4单格删除与类型改造入口的格段来源验证，包含同目标类型的调用。该修复只保护此编辑入口，没有更改候选目标token的生成规则，也不声明所有使用token的其他入口已解决同类问题。

## BUG-23 验证状态

- 上述公开回归先红后绿，验证另一候选来源的格段在删除、改为不同类型、改为原类型三种调用中均被拒绝，活动snapshot引用不变。
- 修复后核心测试275/275通过；Debug构建为0警告、0错误。
- 本轮未提供Roslyn CodeLens、Godot editor MCP及DAP工具，对应检查未完成；上述核心回归与构建结果不替代这些检查，也不表示完整QA已通过。
