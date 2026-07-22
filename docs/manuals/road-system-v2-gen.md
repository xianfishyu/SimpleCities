# 第二代道路系统迭代设计指南

> 状态：历史设计与迁移记录 | 创建日期：2026-06-04 | 当前状态更新：RoadGraph 迁移已完成
>
> 本文档基于迁移前 Phase 1 RoadNetwork 系统的完整复盘，记录 RoadGraph 重构的诊断、设计和迁移路径。文中“当前系统”在诊断章节指迁移前的 legacy RoadNetwork。当前运行时已经完成 RoadGraph / GraphNode / GraphEdge / RoadGroup / SpatialIndex 迁移。TrafficGraph、A*、道路升级工具和按 RoadType 差异化渲染仍是未来工作。

---

## 目录

1. [问题诊断：迁移前 Phase 1 系统的核心矛盾](#1-问题诊断迁移前-phase-1-系统的核心矛盾)
2. [设计原则](#2-设计原则)
3. [核心架构：纯连续图](#3-核心架构纯连续图)
4. [数据结构](#4-数据结构)
5. [空间索引层](#5-空间索引层)
6. [关键算法](#6-关键算法)
7. [API 设计](#7-api-设计)
8. [渲染集成](#8-渲染集成)
9. [与模拟系统集成](#9-与模拟系统集成)
10. [迁移策略](#10-迁移策略)
11. [路线图与优先级](#11-路线图与优先级)
12. [附录 A：命名对照表](#附录-a命名对照表)
13. [附录 B：不变式对比](#附录-b不变式对比)
14. [附录 C：当前完成程度评估](#附录-c当前完成程度评估)

---

## 1. 问题诊断：迁移前 Phase 1 系统的核心矛盾

> 本章保留历史诊断。“当前 RoadNetwork”指迁移前系统，不是当前 SimpleCities 运行时。

### 1.1 双重身份危机

迁移前 RoadNetwork 的核心数据结构同时存在于两个世界：

```
世界 A — 网格索引（Grid-Indexed）
  _posToJunctionID  : Dictionary<Vector2, int>   // 格点位置 → JunctionID
  _posToSegmentID   : Dictionary<Vector2, int>   // 格点位置 → SegmentID
  逻辑：位置是 key，实体是 value。查询是 O(1) 字典查表。

世界 B — 图结构（Graph-Structured）
  _junctions        : Dictionary<int, Junction>   // JunctionID → 拓扑节点
  _segments         : Dictionary<int, Segment>    // SegmentID → 几何边
  _roads            : Dictionary<int, Road>       // RoadID → 逻辑聚合
  逻辑：ID 是 key，实体是 value。操作是图遍历。
```

这两个世界通过 **位置↔ID 的双向映射** 耦合成一体。问题在于：当一个实体在两个世界中的表现不一致时，系统开始破裂。

### 1.2 半格点：裂缝的具象化

半格点（Half-Grid Point）是双重身份冲突的直接产物：

| 场景 | 网格世界（世界 A） | 图世界（世界 B） |
|------|-------------------|-------------------|
| 两条对角路的 X 形交叉 | **无法表示** — 交叉点不在 `CellSize` 整数倍上，不能作为 `Dictionary<Vector2,int>` 的 key | **可以表示** — 创建一个新的 `Junction`，ID 存入 `_junctions` |
| 空间查询此交叉点 | `_posToJunctionID.ContainsKey(pos)` → `false`（不在字典中） | 必须用 `IsAnyJunctionAt()` 做 O(n) 几何扫描 |
| 从交叉点开始铺路 | `FindSegmentAt(pos)` → `-1`（字典未命中） | 必须用 `FindSegmentAtIncludingHalfGrid()` 三级回退 |

半格点系统添加了大量"补丁代码"来弥合这个裂缝：

```
补丁 1: GetOrCreateJunction() — 半格点不进 _posToJunctionID，仅存 _junctions
补丁 2: IsAnyJunctionAt() — 字典未命中时 O(n) 几何扫描
补丁 3: FindSegmentAtIncludingHalfGrid() — 三级回退（字典 → waypoint 扫描 → Junction 取 ConnectedSegment）
补丁 4: MaybeReindexJunctionInPosDict() — 删段后补回字典条目（否则点击该格点拆不了路）
补丁 5: IsOnRoadPoint() — 判断位置是否是路网点（字典 + waypoint 扫描 + Junction 扫描）
补丁 6: 半格起点仅允许对角拖拽 — 因为正交方向会产生"脱离网格"的线段
```

**根本原因**：`Dictionary<Vector2, int>` 假设了所有有意义的坐标都落在离散格点上。一旦实体可以出现在格点之外（X 形交叉不可避免），这个假设就崩塌了。

### 1.3 CellSize 的病毒式传播

`CellSize` 参数在迁移前系统中几乎无处不在：

```
RoadBuilder          — 所有拖拽/吸附计算
RoadNetwork          — AddRoad, AddSegment, SplitSegment*, TryMerge*, GetOrCreateJunction, 多个查询方法
RoadRenderer         — 传入但不直接使用
GridSystem           — 全局静态属性
GameHUD              — 鼠标格点坐标显示
```

`CellSize` 本质上是 **UI 输入层的概念**（用户在一个离散网格上放置道路），却被传染到了 **数据层**（RoadNetwork）。这种耦合使得：

- 修改 CellSize 需要穿透整个 RoadNetwork 的算法逻辑
- RoadNetwork 无法独立于"网格"概念存在
- 单元测试需要 mock CellSize 参数

### 1.4 具体症状清单

| 症状 | 表现 | 根源 |
|------|------|------|
| 双索引同步 | 增删 Segment 时需要同时维护 `_segments` 字典和 `_posToSegmentID` 字典 | 世界 A 和世界 B 各自维护索引 |
| 浮点精度焦虑 | `_posToSegmentID` 用 `Vector2` 做 key，需要 `SnapToGrid` 保证精度一致性 | `Vector2` 作为字典 key 的天然脆弱性 |
| 拆段后补索引 | `MaybeReindexJunctionInPosDict` 的存在本身就是 bug 证据 | 字典的最后写入者覆盖了共享 Junction 的其他 Segment |
| 半格点特殊分支 | 至少 6 处代码有"如果是半格点则走另一条路径"的分支 | 网格模型无法表达非格点位置 |
| O(n) 回退查询 | `IsAnyJunctionAt`, `FindSegmentAtIncludingHalfGrid`, `IsOnRoadPoint` | 字典无法覆盖非格点，只能全量扫描 |

---

## 2. 设计原则

下一代系统遵循以下原则，每条都针对迁移前系统的具体问题：

### P1：图即真相（Graph as Single Source of Truth）

**所有实体（节点、边）只有一种表示方式：图中的顶点和边。** 不存在第二套索引系统试图从另一个维度描述同一实体。

> 消除：`_posToJunctionID`、`_posToSegmentID`。空间查询由独立的空间索引层完成（见 §5）。

### P2：连续空间，离散化是 UI 的事（Continuous Space, Discrete Input）

**路网图存在于连续的 R² 空间中。** 节点可以落在任意 Vector2 坐标。CellSize 是 `RoadBuilder`（输入层）的概念，不是 `RoadNetwork`（数据层）的概念。

> 消除：RoadNetwork 中所有 `cellSize` 参数。GridSystem 不再被数据层引用。

### P3：空间索引是服务，不是存储（Spatial Index as Service, Not Storage）

**"某位置附近有什么"是一个查询问题，不应和数据存储模型耦合。** 使用独立的空间索引（如空间哈希网格），它维护的是实体引用而非实体本身。索引可以按需重建或选择性更新，从不作为主数据源。

> 消除：`_posToJunctionID`/`_posToSegmentID` 作为"既是索引又是主存储"的模糊角色。

### P4：添加比修改简单，修改比删除简单（CRUD Asymmetry）

迁移前系统的高复杂度集中在 **删除操作**（`RemoveSegment`）：需要同步清理 `_posToSegmentID`、断连 Junction、清理孤立 Junction、补回共享 Junction 的索引、拆分 Road 连通分量、触发合并降级。下一代的设计应使删除操作的复杂度和添加操作相当。

> 手段：将"Road = 连通分量"的语义从"删除时修复"变为"查询时计算"。

### P5：不变式最小化（Minimize Invariants）

迁移前系统有多个容易破裂的不变式：
- `_posToSegmentID` 的 key 必须被 `SnapToGrid` 标准化（否则字典查找失败）
- 半格 Junction 不能出现在 `_posToJunctionID` 中（否则格点 Junction 覆盖它）
- `_posToSegmentID` 中共享 Junction 的条目必须在删段后补回（否则点击拆除失效）

下一代系统追求零不变式 — 或者至少，不变式由类型系统在编译时保证而非由运行时逻辑维护。

---

## 3. 核心架构：纯连续图

### 3.1 架构分层

```
┌─────────────────────────────────────────────────────────────┐
│ Layer 3: UI / Input                                         │
│   RoadBuilder     — 鼠标拖拽 → 8 方向投影 → 生成拓扑操作请求   │
│   GridSystem      — SnapToGrid, IsSnapGrid（纯 UI 工具）     │
│   ToolManager     — 工具切换，输入路由                        │
├─────────────────────────────────────────────────────────────┤
│ Layer 2: Topology (Pure Graph)                              │
│   RoadGraph       — 节点 + 边的增删改查，图遍历，拓扑操作      │
│   SpatialIndex    — 空间哈希网格，O(1) 邻近查询               │
│   RoadGroup       — 逻辑分组（替代当前 Road 概念）             │
├─────────────────────────────────────────────────────────────┤
│ Layer 1: Rendering                                          │
│   RoadRenderer    — 监听图事件，同步 Line2D / 交叉口渲染      │
│   RoadStyle       — 样式映射（RoadType → Color/Width/etc）    │
└─────────────────────────────────────────────────────────────┘

关键变化：
  - RoadNetwork 重命名为 RoadGraph，不再包含任何网格/位置索引逻辑
  - SpatialIndex 是独立的辅助层
  - CellSize 不出现在 Layer 2
```

**当前运行时状态**：RoadGraph、GraphNode、GraphEdge、RoadGroup 和 SpatialIndex 已落地。GridSystem 与 DirectionUtil 仍承担 8 方向输入约束，RoadGraph 不回到 legacy RoadNetwork 命名。

### 3.2 与迁移前架构的对照

| 迁移前 | 当前 RoadGraph | 变化 |
|------|--------|------|
| `RoadNetwork` | `RoadGraph` | 去掉所有位置索引字典；去掉 CellSize 参数 |
| `_posToJunctionID` | `SpatialIndex` | 从数据存储变为查询服务 |
| `_posToSegmentID` | `SpatialIndex` | 同上 |
| `Junction` | `GraphNode` | 概念相同，去掉关于"是否在整格点"的认知 |
| `Segment` | `GraphEdge` | 概念相同，去掉 `cellSize` 依赖 |
| `Road` | `RoadGroup` | 语义从"连通分量（需主动维护）"变为"用户意图标签（查询时推断）" |
| `RoadConfig` | `RoadConfig` | **保留**，但 RoadGraph 不持有引用 |

### 3.3 实体关系

```
GraphNode (顶点)
  ├── ID: int
  ├── Position: Vector2        ← 可以是任意坐标，无"半格"概念
  └── Edges: List<EdgeRef>     ← 从此节点出发的边引用

GraphEdge (边)
  ├── ID: int
  ├── NodeA: int               ← 端点 A 的 NodeID
  ├── NodeB: int               ← 端点 B 的 NodeID
  ├── Points: Vector2[]        ← 中间途经点（可为空）
  ├── GroupID: int             ← 所属 RoadGroup
  └── Type: RoadType           ← 道路等级（土路/普通/主干/高速）

RoadGroup (逻辑分组)
  ├── ID: int
  ├── EdgeIDs: HashSet<int>    ← 玩家一次操作产生的边集合
  └── Type: RoadType           ← 该组道路的等级

SpatialIndex (空间索引)
  ├── BucketSize: float        ← 哈希桶边长（≈ CellSize，但仅用于索引粒度）
  ├── buckets: Dictionary<(int,int), List<ISpatialRef>>
  └── 提供：QueryRadius(pos, radius) → List<ISpatialRef>
```

---

## 4. 数据结构

### 4.1 GraphNode

```csharp
public class GraphNode
{
    public int ID { get; }
    public Vector2 Position { get; }
    public IReadOnlyList<EdgeRef> Edges { get; }

    // 内部结构
    private readonly List<EdgeRef> _edges = new();

    public int EdgeCount => _edges.Count;

    // 添加连接（从本节点视角：本节点 → 邻居节点，沿指定边）
    internal void AddEdge(int edgeID, int neighborNodeID)
    {
        _edges.Add(new EdgeRef(edgeID, neighborNodeID));
    }

    internal bool RemoveEdge(int edgeID)
    {
        return _edges.RemoveAll(e => e.EdgeID == edgeID) > 0;
    }

    /// <summary>
    /// 获取所有邻居节点 ID（去重）。
    /// 如果两条边连接到同一邻居，仅出现一次。
    /// </summary>
    public IEnumerable<int> GetNeighborIDs()
    {
        var seen = new HashSet<int>();
        foreach (var e in _edges)
            if (seen.Add(e.NeighborNodeID))
                yield return e.NeighborNodeID;
    }
}

public readonly struct EdgeRef
{
    public int EdgeID { get; }
    public int NeighborNodeID { get; }
    public EdgeRef(int edgeID, int neighborNodeID)
    {
        EdgeID = edgeID;
        NeighborNodeID = neighborNodeID;
    }
}
```

**与当前 Junction 的关键差异**：
- 不再存储 `Direction`（方向由 `Position` 和邻居节点 `Position` 计算得出）
- 不再有 `JunctionType` 枚举（类型由边的数量和几何关系**按需计算**，而非存储为状态）
- 不再有 `RecalculateType()` — 不再需要"类型"作为可变状态

### 4.2 GraphEdge

```csharp
public class GraphEdge
{
    public int ID { get; }
    public int NodeA { get; internal set; }
    public int NodeB { get; internal set; }
    public Vector2[] Points { get; }       // NodeA → pts[0] → pts[1] → ... → NodeB
    public int GroupID { get; internal set; }
    public RoadType Type { get; internal set; }

    /// <summary>总几何长度（含首尾段）。</summary>
    public float Length { get; }

    /// <summary>完整点序列（含两端节点位置）。调用方需传入节点查找函数。</summary>
    public Vector2[] GetFullPath(Func<int, GraphNode?> getNode)
    {
        var nodeA = getNode(NodeA);
        var nodeB = getNode(NodeB);
        if (nodeA == null || nodeB == null) return Array.Empty<Vector2>();

        var result = new Vector2[2 + Points.Length];
        result[0] = nodeA.Position;
        Array.Copy(Points, 0, result, 1, Points.Length);
        result[^1] = nodeB.Position;
        return result;
    }

    // ⚠️ 注意：不再需要用 Direction 参数判断方向。
    // 方向总是从两个节点的 Position 差计算得出。
}
```

**关键简化**：
- 不再存储 `FromJunctionID` / `ToJunctionID` 的"方向性" — 边在拓扑上是**无向的**（如果需要方向，在查询时按需计算）
- 两端节点用 `NodeA` / `NodeB`（无向），而非 `From` / `To`（有向）
- 交通模拟层需要方向时，在模拟层按需建模（如"从 A 到 B 的单向通行"）

### 4.3 RoadGroup

```csharp
public class RoadGroup
{
    public int ID { get; }
    public RoadType Type { get; internal set; }

    private readonly HashSet<int> _edgeIDs = new();

    public IReadOnlyCollection<int> EdgeIDs => _edgeIDs;
    public int EdgeCount => _edgeIDs.Count;
    public bool IsEmpty => _edgeIDs.Count == 0;

    internal void AddEdge(int edgeID) => _edgeIDs.Add(edgeID);
    internal void RemoveEdge(int edgeID) => _edgeIDs.Remove(edgeID);
}
```

**与当前 Road 的关键差异**：
- 语义从"连通分量"变为"**用户操作标签**"
- 不再强制 `RoadGroup` 内部的边必须连通（`SplitRoadIntoConnectedComponents` 消失）
- 如果某时刻需要知道"这些边是否还连通"，在查询时计算，不在写操作时修复
- 拆除中间段导致 Group 分裂为两个连通分量 → **是合法状态**。两个分量共用一个 GroupID，仅仅表示"它们曾是同一次拖拽铺的"

### 4.4 RoadType（新增）

```csharp
public enum RoadType
{
    Dirt = 0,       // 土路 — 最低速、最低维护费
    Street = 1,     // 普通街道 — 默认
    Arterial = 2,   // 主干道 — 高速、中等维护费
    Highway = 3,    // 高速公路 — 最高速、高维护费、不能直接连接建筑
}
```

> RoadType 已进入当前数据和存档模型。当前 UI 仍固定创建 `Street`，按 RoadType 差异化渲染和道路升级工具仍是未来工作。

---

## 5. 空间索引层

### 5.1 设计动机

消除 `Dictionary<Vector2, int>` 后需要一个替代方案来回答："玩家点击的位置 (x, y) 附近有哪些节点或边？"

### 5.2 UniformGrid（空间哈希网格）

```csharp
/// <summary>
/// 基于均匀网格的空间哈希索引。
/// 世界空间被划分为 BucketSize × BucketSize 的方形桶。
/// 每个桶存储落入该范围的所有 ISpatialRef 引用。
///
/// 性能：
///   - 插入/移除：O(1)（哈希表 + List 操作）
///   - 半径查询：O(1 + k) 其中 k = 命中桶内的实体数（小常数）
///   - 适用于城市规模（数千节点 + 数万边）的场景
/// </summary>
public class UniformGrid
{
    private readonly float _bucketSize;
    private readonly Dictionary<(int bx, int by), List<ISpatialRef>> _buckets = new();

    public UniformGrid(float bucketSize)
    {
        _bucketSize = Mathf.Max(bucketSize, 1f);
    }

    /// <summary>插入一个空间引用。同一实体可多次插入（如一个边插入其所有途经点）。</summary>
    public void Insert(ISpatialRef entity)
    {
        var (bx, by) = WorldToBucket(entity.Position);
        if (!_buckets.TryGetValue((bx, by), out var list))
            _buckets[(bx, by)] = list = new List<ISpatialRef>();
        list.Add(entity);
    }

    /// <summary>移除指定实体的所有条目。</summary>
    public void Remove(ISpatialRef entity)
    {
        var (bx, by) = WorldToBucket(entity.Position);
        if (_buckets.TryGetValue((bx, by), out var list))
            list.RemoveAll(r => r == entity);
    }

    /// <summary>
    /// 查询以 center 为圆心、radius 为半径范围内的所有实体。
    /// 返回结果可能包含少量超出半径的实体（桶级预过滤），调用方应做精确距离检查。
    /// </summary>
    public IEnumerable<ISpatialRef> QueryRadius(Vector2 center, float radius)
    {
        int minBX = WorldToBucketCoord(center.X - radius);
        int maxBX = WorldToBucketCoord(center.X + radius);
        int minBY = WorldToBucketCoord(center.Y - radius);
        int maxBY = WorldToBucketCoord(center.Y + radius);

        float radiusSq = radius * radius;

        for (int bx = minBX; bx <= maxBX; bx++)
        for (int by = minBY; by <= maxBY; by++)
        {
            if (!_buckets.TryGetValue((bx, by), out var list)) continue;
            foreach (var entity in list)
            {
                if (entity.Position.DistanceSquaredTo(center) <= radiusSq)
                    yield return entity;
            }
        }
    }

    /// <summary>清空所有索引。</summary>
    public void Clear() => _buckets.Clear();

    // — 内部 —

    private (int bx, int by) WorldToBucket(Vector2 pos)
    {
        return (WorldToBucketCoord(pos.X), WorldToBucketCoord(pos.Y));
    }

    private int WorldToBucketCoord(float val)
    {
        return Mathf.FloorToInt(val / _bucketSize);
    }
}

/// <summary>
/// 可被空间索引引用的实体必须实现此接口。
/// </summary>
public interface ISpatialRef
{
    Vector2 Position { get; }
    SpatialRefKind Kind { get; }  // Node / Edge / 未来可扩展
}

public enum SpatialRefKind
{
    Node,
    EdgePoint   // 代表边上的一个途经点
}
```

### 5.3 使用模式

```csharp
// 插入：节点和边的所有途经点都注册到空间索引
foreach (var node in _nodes.Values)
    _spatialIndex.Insert(new NodeSpatialRef(node));

foreach (var edge in _edges.Values)
{
    var nodeA = _nodes[edge.NodeA];
    var nodeB = _nodes[edge.NodeB];
    // 也插入端点位置（归属那条边），使得点击端点附近也能命中这条边
    _spatialIndex.Insert(new EdgePointRef(edge.ID, nodeA.Position));
    _spatialIndex.Insert(new EdgePointRef(edge.ID, nodeB.Position));
    foreach (var pt in edge.Points)
        _spatialIndex.Insert(new EdgePointRef(edge.ID, pt));
}

// 查询：玩家点击位置附近有什么
var hits = _spatialIndex.QueryRadius(clickPos, snapRadius);
// hits 包含 NodeSpatialRef 和 EdgePointRef
// 调用方按 Kind 筛选 + 按距离排序 + 取最近者
```

**关键优势**：
- 不再区分"整格点"和"半格点" — 所有位置一律平等
- 不再需要 `IsSnapGrid` 判断来决定"是否入字典"
- 不再需要 `MaybeReindexJunctionInPosDict` 补丁
- 不再需要 `FindSegmentAtIncludingHalfGrid` 三级回退

---

## 6. 关键算法

### 6.1 铺设道路（AddRoad）

**迁移前流程的复杂度来源**：
1. 半格点特殊处理（skipSnap、锚定计算）
2. X 形交叉解析（ResolveInteriorCrossings + SplitSegmentAtPosition）
3. 共线重叠跳过（IsApproachColinearWithSegment）
4. 端点劈分（SplitSegmentAtWaypoint）
5. 按 Junction 切段（splitIdx）
6. 拓扑去重
7. 合并降级（TryMergeAtJunction）

**RoadGraph 目标流程**：

```
AddRoad(fromPos, toPos, waypoints, groupID, roadType):

  1. 构建完整路径 P = [fromPos, ...waypoints, toPos]
     — 无需 8 方向校验（RoadGraph 不关心方向，只关心几何位置）
     — 方向合法性由 RoadBuilder（UI 层）保证

  2. 碰撞检测与切分：
     FOR EACH 新路径段 [P[i], P[i+1]]:
       a. 查询 SpatialIndex 中与该段相交的所有已有边的途经点
       b. 在交点处创建新的 GraphNode（位置 = 交点坐标）
       c. 将已有边在交点处劈分为两条边
       d. 将交点加入 P 作为切分锚点
     — 不再区分"共格点劈分"和"位置劈分" → 统一为"几何交点→切边"
     — 不再区分"整格点"和"非整格点" → 所有交点创建的节点一律平等

  3. 创建边：
     FOR EACH 相邻切分锚点对 (P[a], P[a+1]) 之间的子段:
       IF 完全重叠（已有边覆盖了这段） → SKIP
       ELSE:
         a. GetOrCreateNode(P[a])  → nodeA
         b. GetOrCreateNode(P[a+1]) → nodeB
         c. 创建 GraphEdge(nodeA, nodeB, 中间点, groupID, roadType)
         d. 在 SpatialIndex 中注册边的途经点

  4. 合并降级（可选，性能优化）：
     IF 某节点连接数 == 2 且两侧边共线:
       合并为一条边
     — 这是一个"压缩"操作，不影响正确性，仅减少节点/边数量

  5. 返回 groupID
```

**简化了什么**：
- ✅ 消除了 CellSize 参数
- ✅ 消除了半格点特殊分支
- ✅ 统一了"共格点劈分"和"位置劈分"
- ✅ 消除了 direction 校验（从数据层移至 UI 层）
- ✅ 消除了 `_posToJunctionID` / `_posToSegmentID` 维护
- ✅ 消除了 `IsApproachColinearWithSegment`（简化为"相交→切；重叠→跳过"）

### 6.2 拆除边（RemoveEdge）

**迁移前流程的复杂度来源**：
1. 清 `_posToSegmentID` 索引（含共享 Junction 的条目标记）
2. 断连 Junction → 清理孤立 Junction → 清 `_posToJunctionID`
3. `MaybeReindexJunctionInPosDict` 补丁
4. 从 Road 摘除 Segment → Road 空则清 → 否则 `SplitRoadIntoConnectedComponents`
5. 触发 `TryMergeAtJunction`（含 `_inMergeOperation` 守卫）

**RoadGraph 目标流程**：

```
RemoveEdge(edgeID):

  1. 从 _edges 字典移除边
  2. 从 SpatialIndex 移除边的所有途经点引用
  3. 从两端节点的 EdgeRef 列表中移除此边
  4. 若某端节点 EdgeCount == 0 → 移除该节点 + 从 SpatialIndex 移除
  5. 从 RoadGroup 中摘除此边（Group 为空则移除）
  6. 触发 EdgeRemoved 事件（渲染层响应）

  — END —
```

**简化了什么**：
- ✅ 不再有 `SplitRoadIntoConnectedComponents`（Group 允许不连通）
- ✅ 不再有删段后自动合并降级（合并降级是可选的压缩操作，非必须的拓扑修复）
- ✅ 不再有 `MaybeReindexJunctionInPosDict`
- ✅ 不再有 `_inMergeOperation` 递归守卫
- ✅ 不再有接口索引字典的双重维护

**权衡**：拆除中间段后 Group 可能包含两个不连通的子图。这不影响渲染（每个边独立渲染），也不影响交通模拟（模拟层自己维护连通性）。唯一的语义差异是"玩家一次拖拽铺的路"不再强保证连通—但实际游戏中，拆除中间段后剩下的本来就不是"一条路"了。

### 6.3 空间查询（用户点击找路）

```
FindClosestEdge(position, radius):

  1. hits = _spatialIndex.QueryRadius(position, radius)
  2. 筛选 EdgePointRef → 取距离最近的 → 返回 edgeID
  3. 若未命中，筛选 NodeRef → 取距离最近的 → 返回该节点上任一条边的 edgeID
```

对比迁移前的三级回退（字典 → waypoint 扫描 → Junction ConnectedSegment），新方案是单次空间索引查询 + 按需过滤。

---

## 7. API 设计

### 7.1 RoadGraph 公共接口

```csharp
public class RoadGraph
{
    // ── 创建 ──
    public int AddEdge(Vector2 posA, Vector2 posB, Vector2[] points,
                       int groupID, RoadType type);
    // 返回 edgeID；失败返回 -1。
    // 自动 GetOrCreate 两端节点。若 posA/B 附近已有节点，复用。

    public int AddRoad(Vector2 start, Vector2 end, Vector2[] waypoints,
                       RoadType type);
    // 封装 AddEdge 的便利方法：自动创建 RoadGroup，切分交叉边，合并降级。

    // ── 删除 ──
    public bool RemoveEdge(int edgeID);
    // 移除单条边。自动清理孤立节点和空 Group。

    public bool RemoveRoadGroup(int groupID);
    // 移除整个 Group 的所有边。

    // ── 查询 ──
    public GraphEdge? GetEdge(int edgeID);
    public GraphNode? GetNode(int nodeID);
    public RoadGroup? GetGroup(int groupID);

    public GraphEdge? FindClosestEdge(Vector2 position, float maxRadius);
    public GraphNode? FindClosestNode(Vector2 position, float maxRadius);

    // ── 遍历 ──
    public IEnumerable<GraphEdge> GetAllEdges();
    public IEnumerable<GraphNode> GetAllNodes();
    public IEnumerable<RoadGroup> GetAllGroups();

    // ── 事件 ──
    public event Action<GraphEdge>? EdgeAdded;
    public event Action<GraphEdge>? EdgeRemoved;
    public event Action? GraphCleared;  // 存档加载后整图重建

    // ── 持久化 ──
    // 实现 ISaveable（接口不变）
}
```

### 7.2 与 legacy API 的对照

| legacy 方法 | 当前 RoadGraph 方法 | 变化 |
|----------|-----------|------|
| `AddRoad(from, to, wps, cellSize, extendRoadID)` | `AddRoad(start, end, wps, type)` | 去掉 `cellSize`，去掉 `extendRoadID`（占位），加 `type` |
| `RemoveSegment(segmentID)` | `RemoveEdge(edgeID)` | 行为简化（不再触发拓扑修复链） |
| `RemoveRoad(roadID)` | `RemoveRoadGroup(groupID)` | 行为等价 |
| `FindSegmentAt(pos)` | `FindClosestEdge(pos, radius)` | 语义从"精确位置匹配"变为"最近邻查询" |
| `GetJunctionAt(pos)` | `FindClosestNode(pos, radius)` | 同上 |
| `HasJunctionAt(pos)` | 废弃 | 用 `FindClosestNode(pos, tinyRadius) != null` 替代 |
| `SnapToGrid(pos, cellSize)` | 移入 `GridSystem` | 不再属于 RoadGraph 的公共 API |

---

## 8. 渲染集成

### 8.1 渲染与数据的解耦

渲染层（RoadRenderer）通过**事件监听**与数据层（RoadGraph）解耦：

```
RoadGraph 事件:
  EdgeAdded    → RoadRenderer.CreateEdgeLine(edge)
  EdgeRemoved  → RoadRenderer.RemoveEdgeLine(edge)
  GraphCleared → RoadRenderer.ClearAllLines() + RebuildAll()
```

这个事件驱动模型 **不需要改动**。当前已经做得很好了。

### 8.2 节点渲染简化

迁移前渲染逻辑：
```
foreach junction in GetAllJunctions():
    if junction.ConnectionCount >= 2 → 画 Junction 圆点
    else if junction.ConnectionCount == 1 → 画 Endpoint 圆点
```

RoadGraph：
```
foreach node in GetAllNodes():
    if node.EdgeCount >= 2 → 画 Node 圆点
    else if node.EdgeCount == 1 → 画 Endpoint 圆点
```

逻辑完全一致。唯一变化是属性名从 `ConnectionCount` 变为 `EdgeCount`。

### 8.3 道路分级视觉

未来按道路分级显示时，渲染层可按 RoadType 查表决定样式：

```csharp
// 在 RoadConfig 中添加:
[Export] public RoadTypeStyle[] TypeStyles { get; set; }  // 4 元素数组

public class RoadTypeStyle
{
    public Color Color;
    public float Width;
    public float DashLength;    // 0 = 实线
    // 未来可扩展：纹理、路肩宽度、中央分隔带...
}
```

渲染时：
```csharp
var style = Config.GetStyle(edge.Type);
line.DefaultColor = style.Color;
line.Width = style.Width;
```

> 这保持了全局 `RoadConfig` 的模式，同时支持未来按等级差异化。当前 RoadRenderer 仍统一使用 RoadConfig 的基础颜色和线宽。

---

## 9. 与模拟系统集成

### 9.1 交通模拟需要的接口

Phase 6 的交通模拟（A* 寻路、OD 矩阵、拥堵模型）需要以下路网查询能力：

```csharp
// RoadGraph 提供的遍历接口（已存在于 GetAllNodes / GetAllEdges）

// 交通模拟层需要额外构建：
public class TrafficGraph
{
    // 将 RoadGraph 转换为带权有向图（用于 A* 寻路）
    public void BuildFrom(RoadGraph roadGraph);

    // A* 寻路
    public List<int> FindPath(int fromNodeID, int toNodeID);

    // 边的权重（通行时间）受 RoadType 和实时拥堵影响
    public float GetEdgeWeight(int edgeID);
    public void UpdateCongestion(int edgeID, float flowRatio); // 流量/容量

    // 拥堵后权重更新（用于 Wardrop 均衡迭代）
    public void RecalculateWeights();
}
```

**设计要点**：
- `TrafficGraph` 是模拟层的只读视图，不对 `RoadGraph` 做写操作
- `RoadGraph` 不包含寻路逻辑（保持数据层纯净）
- 边的方向性（单向/双向）由 `TrafficGraph` 按需建模，`RoadGraph` 始终无向

### 9.2 路网变更时的模拟层同步

当玩家铺路/拆路时，`TrafficGraph` 需要知道图的哪部分变了：

```csharp
// RoadGraph 事件
EdgeAdded   → TrafficGraph 增量插入新边 + 重新计算受影响区域的权重
EdgeRemoved → TrafficGraph 移除边 + 标记经过此边的所有路径为"需要重算"
```

增量更新（而非每次重建整个 TrafficGraph）是性能关键，但属于 Phase 6 的实现细节。

---

## 10. 迁移策略

### 10.1 为什么不能一刀切重写

迁移前系统已经稳定运行，存档格式已定义，渲染系统工作正常。一刀切重写风险极高。这段保留当时的迁移判断，当前 RoadGraph 重构已经完成。

### 10.2 渐进式迁移记录

> 阶段 A 和阶段 B 的 RoadGraph / SpatialIndex / GraphNode / GraphEdge / RoadGroup 迁移已经完成。以下内容保留为历史迁移记录，阶段 C 仍是未来模拟工作。

分三个阶段，每个阶段可独立交付和测试：

#### 阶段 A：内部重构（不影响公共 API）

1. **引入 `SpatialIndex (UniformGrid)`** 作为 `_posToJunctionID`/`_posToSegmentID` 的并行实现
2. **所有查询方法先查 SpatialIndex，查不到再回退字典**（双写单读）
3. 删除所有"半格点特殊分支"——
   - `IsAnyJunctionAt` → 直接用 `FindClosestNode(pos, tinyRadius)`
   - `FindSegmentAtIncludingHalfGrid` → 直接用 `FindClosestEdge(pos, radius)`
   - `GetOrCreateJunction` 中的 `IsSnapGrid` 分支 → 移除，所有节点一律对待
4. 验证：现有功能全部正常

**交付物**：RoadNetwork 内部已统一使用 SpatialIndex，但外部 API 不变

#### 阶段 B：API 清理（影响调用方）

1. **重命名**：
   - `RoadNetwork` → `RoadGraph`
   - `Junction` → `GraphNode`
   - `Segment` → `GraphEdge`
   - `Road` → `RoadGroup`
2. **移除 CellSize 参数**：从所有数据层方法签名中删除
3. **移除公共 API 中的位置索引方法**：
   - 删除 `FindSegmentAt(Vector2)` → 替换为 `FindClosestEdge(Vector2, float)`
   - 删除 `GetJunctionAt(Vector2)` → 替换为 `FindClosestNode(Vector2, float)`
   - 删除 `HasJunctionAt(Vector2)`
   - 删除 `SnapToGrid` 的 public 版本（仅保留 GridSystem 中的静态版本）
4. **简化 `RemoveSegment`（现 `RemoveEdge`）**：
   - 移除 `SplitRoadIntoConnectedComponents` 调用
   - 移除 `TryMergeAtJunction` 触发
   - 移除 `MaybeReindexJunctionInPosDict`
5. **引入 `RoadType`**：在 `RoadGroup` 上添加 `Type` 字段，Render 层按 Type 查样式
6. 更新 `RoadBuilder`、`RoadRenderer`、`GameHUD` 的 API 调用
7. **更新存档格式**：字段重命名，确保向后兼容（旧存档可加载）

**交付物**：新旧 API 完全迁移，旧字典索引完全移除

#### 阶段 C：模拟集成（Phase 6 并行）

1. 构建 `TrafficGraph`（交通模拟视图）
2. 实现 `RoadGraph` 事件的增量更新
3. 实现 A* 寻路（可在 RoadGraph 上直接做，也可在 TrafficGraph 上做）
4. 实现道路升级工具（修改 `RoadType`）

**交付物**：道路系统完整支持交通模拟

### 10.3 存档兼容性

legacy 存档格式（`RoadNetworkData`）包含字段名如 `Junctions`、`Segments`、`Roads`。当前 RoadGraph 写入 private v2 payload，但 JSON 兼容字段名仍保留为 `junctions`、`segments`、`roads`。不要仅因运行时类型改名破坏这些字段。

```csharp
// RoadNetworkData 保留 legacy public DTO
// RoadGraph v2 payload 继续使用 junctions / segments / roads 兼容字段

// 当时的备选提案：引入版本号
public class RoadNetworkData
{
    public int Version { get; set; } = 1;  // 迁移前示例；当前 RoadGraph payload 已写出 version 2
    // ...
}
```

---

## 11. 路线图与优先级

### 优先级矩阵

> 当前状态：阶段 A 和阶段 B 已完成。阶段 C、TrafficGraph、A*、道路升级工具和按 RoadType 差异化渲染仍保留为未来设计。

| 任务 | 价值 | 成本 | 风险 | 优先级 |
|------|------|------|------|--------|
| 阶段 A：引入 SpatialIndex，消除半格特判 | 🔴 极高（消除最痛的技术债务） | 🟡 中 | 🟢 低（API 不变） | **P0** |
| 阶段 B：API 清理 + 移除网格依赖 | 🟡 中（代码更干净，但功能等价） | 🔴 高（全仓库改名） | 🟡 中（存档兼容） | **P1** |
| 引入 RoadType + 分级渲染 | 🟢 较低（当前仅一种路） | 🟢 低 | 🟢 低 | **P2** |
| 阶段 C：TrafficGraph + A* 寻路 | 🔴 极高（Phase 6 阻塞项） | 🔴 高 | 🟡 中 | **P3**（Phase 6 再做） |
| 道路升级工具 | 🟡 中 | 🟡 中 | 🟢 低 | **P4**（Phase 6 再做） |

### 推荐执行顺序

```
Phase 2（分区系统）进行中
    │
    ├── 阶段 A（内部重构）可与 Phase 2 并行
    │   └── 不影响分区开发，消除技术债务
    │
    ├── Phase 2 完成后 → 阶段 B（API 清理）
    │   └── 在进入 Phase 3~5 之前清理干净，避免债务滚雪球
    │
    └── Phase 6 启动时 → 阶段 C（TrafficGraph）
        └── 基于清理后的 RoadGraph API 构建，无需回头修
```

---

## 附录 A：命名对照表

| legacy 名称 | 当前 RoadGraph 名称 | 说明 |
|----------|-----------|------|
| `RoadNetwork` | `RoadGraph` | 强调"图"而非"网络"，当前已完成 |
| `Junction` | `GraphNode` | 去掉"路口"隐含的地理语义，当前已完成 |
| `Segment` | `GraphEdge` | 去掉"段"隐含的破碎语义，当前已完成 |
| `Road` | `RoadGroup` | 强调"分组"而非"路"（与 GraphEdge 区分），当前已完成 |
| `JunctionType` | *删除* | 类型按需计算，不存储为状态 |
| `ConnectionCount` | `EdgeCount` | 名称简化 |
| `FromJunctionID` / `ToJunctionID` | `NodeA` / `NodeB` | 无向语义 |
| `_posToJunctionID` | *删除* | 并入 SpatialIndex，当前已完成 |
| `_posToSegmentID` | *删除* | 并入 SpatialIndex，当前已完成 |
| `Direction`, `DirectionUtil` | *保留但仅用于 UI 层* | RoadBuilder 仍需要 8 方向投影 |
| `GridSystem` | *保留但不再被数据层引用* | 纯 UI 工具 |
| `RoadConfig` | *保留 + 扩展 RoadTypeStyle[]* | 全局配置模式不变 |

## 附录 B：不变式对比

| 迁移前系统的不变式（易破裂） | RoadGraph 系统的不变式（由设计保证） |
|---------------------------|-------------------------------|
| `_posToSegmentID[key]` 的 key 必须经 `SnapToGrid` 标准化 | **无此不变式** — 空间索引用浮点距离匹配 |
| 半格 Junction 不能出现在 `_posToJunctionID` | **无此不变式** — 不存在 `_posToJunctionID` |
| 删段后 `MaybeReindexJunctionInPosDict` 必须补回共享条目 | **无此不变式** — 空间索引独立维护，删边不影响其他边 |
| `_inMergeOperation` 必须设 true 防止递归合并 | **无此不变式** — 删段不触发合并 |
| `SplitRoadIntoConnectedComponents` 必须保证 Group 内连通 | **无此不变式** — Group 允许不连通 |
| `RoadBuilder` 必须在半格起点时限定对角方向 | 移入 `RoadBuilder` 的局部约束，与数据层无关 |

---

## 附录 C：当前完成程度评估

> 评估基准：当前工作区源码、主场景集成状态、`docs/todo/todolist.md`，以及本指南第 2～11 节的目标定义。
>
> 本节用于区分“架构已经落地”和“仍需继续完善的行为”，不把当前明确延期的产品功能视为 V2 基础架构缺陷。

### C.1 总体结论

当前 V2 道路系统已经完成了**核心架构迁移**，但尚未完成设计指南中定义的全部行为和后续产品能力。

准确结论是：

> **V2 已完成核心骨架和主要数据迁移，当前处于“基础架构可用、关键语义待收敛、产品扩展未启用”的阶段。**

当前实现已经可以作为游戏中的基础路网使用，支持连续坐标节点、交叉拆边、空间索引、事件驱动渲染、存档恢复和基础道路编辑。但它还不能宣称完全实现本指南，因为删除事务、完整线段空间查询、数据层几何自由度、公共 API 契约和自动化验证仍未全部闭合。

### C.2 分层完成状态

| 范围 | 状态 | 已完成内容 | 尚未完成或需要确认 |
|---|---|---|---|
| 核心图模型 | **已完成** | `RoadGraph`、`GraphNode`、`GraphEdge`、`RoadGroup` 已替代旧模型；节点、边、分组作为权威数据保存 | 需要通过自动化不变式测试持续验证 |
| P1：图即真相 | **基本完成** | 旧 `_posToJunctionID`、`_posToSegmentID` 已移除；空间索引可从图重建 | 可变集合和实体数组仍需进一步封装 |
| P2：连续空间 | **部分完成** | `CellSize` 已从 `RoadGraph` API 移除；网格吸附保留在 `RoadBuilder/GridSystem` | `RoadGraph` 仍使用 `DirectionUtil`；任意角度路径尚未完整放开 |
| P3：空间索引服务 | **部分完成** | `UniformGrid` 已独立存在，节点和边引用可增删、可重建 | 边目前主要按端点/waypoint 索引；最近边查询不是完整点到折线查询，部分路径仍有全表扫描 |
| P4：简化删除 | **未完成** | 已移除旧位置字典维护和连通分量拆分 | `RemoveEdge`、`RemoveRoadGroup` 仍可能触发 `TryMergeAtNode`，删除事务尚未简化到目标语义 |
| P5：最小化不变式 | **部分完成** | 旧格点字典相关不变式已消失；核心同步路径已集中在 `RoadGraph` | 节点邻接、Group、空间引用、事件和存档之间仍需要事务性校验 |
| AddRoad 与交叉处理 | **基本完成** | 覆盖检查前置；正交/对角交叉、waypoint 交点和边拆分已有实现 | 需要完整回归测试；几何候选查询仍需优化 |
| 删除、拆分与合并 | **部分完成** | 基础删除、孤立节点清理、拆边和共线合并可工作 | 合并语义会影响 Group 标签；删除过程仍存在中间状态和递归抑制机制 |
| 公共 API | **部分完成** | `AddRoad`、`RemoveEdge`、`RemoveRoadGroup`、`FindClosestNode/Edge` 等接口已存在 | 指南定义的公共 `AddEdge` 尚未实现或正式废弃 |
| 渲染集成 | **基本完成** | `EdgeAdded`、`EdgeRemoved`、`GraphCleared` 驱动 Line2D 同步；节点和端点可绘制 | RoadType 分级视觉按当前产品决定延期 |
| 存档兼容 | **基本完成** | v2 类型字段、旧字段兼容、加载后邻接和空间索引重建已存在 | 版本分派、损坏存档校验和失败回滚尚未完成 |
| 自动化验证 | **部分完成** | 已建立可由 `dotnet test` 单命令运行的 RoadGraph 自动化测试入口，并覆盖无场景树构造与空图基线 | 交叉、拆边、删除、不变式和存档兼容等行为回归仍待补齐 |
| RoadType 产品功能 | **按需求延期** | 枚举、Edge/Group 数据和旧存档回退已保留 | 类型样式、选择 UI、道路升级当前不开发 |
| 车流模拟 | **未开始，按路线图延期** | 设计接口已在本指南中定义 | `TrafficGraph`、A*、拥堵权重和增量同步均未实现 |

### C.3 P1～P5 完成度

| 原则 | 当前判断 | 完成度说明 |
|---|---|---|
| **P1 图即真相** | **基本完成** | 图实体已成为唯一权威数据；空间索引不承担主存储职责。剩余工作主要是封装和自动化验证。 |
| **P2 连续空间，离散化是 UI 的事** | **部分完成** | `CellSize` 已留在输入层，但数据层仍以 `DirectionUtil` 参与共线合并，尚未完全独立于八方向概念。 |
| **P3 空间索引是服务，不是存储** | **部分完成** | 独立 `UniformGrid` 已落地，但线段占据 bucket 和真正的最近边几何查询还未完成。 |
| **P4 添加比修改简单，修改比删除简单** | **未完成** | 删除仍会触发自动合并和节点修复链，尚未达到“删除不触发拓扑压缩”的设计目标。 |
| **P5 不变式最小化** | **部分完成** | 旧位置字典不变式已删除，但图字典、邻接、Group、空间索引、事件和存档仍需共同维护。 |

### C.4 V2 阶段状态

| 阶段 | 状态 | 结论 |
|---|---|---|
| 阶段 A：内部引入 SpatialIndex、消除旧半格数据层特判 | **主体完成** | 新图模型和独立空间索引已经存在，但线段空间占据和性能验证仍需补齐。 |
| 阶段 B：API 清理、移除网格依赖、存档兼容 | **大部分完成** | 核心命名和主要 API 已迁移；公共 `AddEdge`、数据层方向约束、删除语义和存档失败保护仍未闭合。 |
| 阶段 C：TrafficGraph、A*、增量模拟同步 | **未开始** | 按 Phase 6 路线图延期，不作为当前 V2 基础清理的阻塞项。 |

### C.5 当前版本可以宣称的能力

当前版本可以准确宣称：

- 使用 `RoadGraph` 作为连续空间路网数据层。
- 使用 `GraphNode`、`GraphEdge` 和 `RoadGroup` 管理道路拓扑。
- 支持正交/对角道路、交叉拆边、waypoint 交点和基础拆路。
- 使用独立 `UniformGrid` 支持节点和边空间引用查询。
- 使用事件驱动方式同步道路 Line2D 渲染。
- 支持道路图保存、加载和旧格式类型回退。

当前版本不应宣称：

- 已完成无网格约束的任意角度数据层。
- 已完成真正的线段级最近道路查询和局部空间查询性能目标。
- 删除道路完全不触发拓扑合并或修复。
- 已完成 RoadType 分级视觉、类型选择或道路升级。
- 已完成 TrafficGraph、A*、拥堵模拟或车流增量同步。

### C.6 完成 V2 基础清理的剩余条件

在不启用 RoadType 产品功能和 Phase 6 车流系统的前提下，V2 基础清理仍需完成：

1. 在现有 RoadGraph 自动化测试入口中补齐交叉、拆边、删除、不变式和存档兼容回归。
2. 固化交叉、拆边、删除、不变式和存档兼容回归。
3. 将 `FindClosestEdge` 改为候选筛选加点到完整折线的精确距离查询。
4. 让空间索引表达线段覆盖范围，并移除 AddRoad 路径上的全图几何扫描。
5. 从数据层移除八方向路径限制和 `DirectionUtil` 合并依赖。
6. 收敛删除事务，移除删除路径上的自动合并和 `suppressMerge` 时序依赖。
7. 决定并实现或废弃文档定义的公共 `AddEdge` API。
8. 增加存档版本校验、引用校验和失败保护。
9. 校准本指南中的当前实现、迁移状态和延期产品范围。
