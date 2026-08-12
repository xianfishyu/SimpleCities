# 第二代道路系统迭代设计指南

> 状态：活动设计、迁移记录与验收清单 | 创建日期：2026-06-04 | 范围确认日期：2026-08-02
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
14. [附录 C：范围确认前的历史完成度快照](#附录-c范围确认前的历史完成度快照)
15. [附录 D：第二代最终范围与验收记录](#附录-d第二代最终范围与验收记录)

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
│   RoadBuilder     — 铺路输入生命周期、预览、提交与取消          │
│   InputStrategy   — 指针吸附/投影 → 不可变 RoadPathDraft      │
│   Grid Strategies — 米字型默认 + 三角/六边形可替换验证         │
│   ToolManager     — 工具切换，输入路由                        │
├─────────────────────────────────────────────────────────────┤
│ Layer 2: Topology (Pure Graph)                              │
│   RoadGraph       — 节点 + 边的增删改查，图遍历，拓扑操作      │
│   SpatialIndex    — 空间哈希网格，提供局部几何候选             │
│   RoadGroup       — 逻辑分组（替代当前 Road 概念）             │
├─────────────────────────────────────────────────────────────┤
│ Layer 1: Rendering                                          │
│   RoadRenderer    — 监听图事件，同步道路网格 / 节点批处理      │
│   RoadStyle       — 第三代候选：RoadType → Color/Width/etc    │
└─────────────────────────────────────────────────────────────┘

关键变化：
  - RoadNetwork 重命名为 RoadGraph，不再包含任何网格/位置索引逻辑
  - SpatialIndex 是独立的辅助层
  - CellSize 不出现在 Layer 2
```

**当前运行时状态**：RoadGraph、GraphNode、GraphEdge、RoadGroup 和 SpatialIndex 已落地。`RoadBuilder` 只消费 `IRoadInputStrategy` 与 `RoadPlacementSession`；默认 `SquareEightRoadInputStrategy` 在内部使用 DirectionUtil 承担 8 方向约束，GridSystem 仅供其他 UI/调试组件使用。RoadGraph 不引用这些输入类型，也不回到 legacy RoadNetwork 命名。

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
  ├── GeometrySegments         ← 权威原生几何段及控制参数
  ├── Points: Vector2[]        ← 段边界兼容副本（非曲线真相）
  └── GroupID: int             ← 所属 RoadGroup

RoadGroup (逻辑分组)
  ├── ID: int
  └── EdgeIDs: HashSet<int>    ← 玩家一次操作产生的边集合

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
    public IReadOnlyList<RoadGeometrySegment> GeometrySegments { get; }
    public Vector2[] Points { get; }       // 原生段边界的防御性兼容副本
    public int GroupID { get; internal set; }

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

### 4.4 RoadType（第三代未来契约）

```csharp
public enum RoadType
{
    Dirt = 0,       // 土路 — 最低速、最低维护费
    Street = 1,     // 普通街道 — 默认
    Arterial = 2,   // 主干道 — 高速、中等维护费
    Highway = 3,    // 高速公路 — 最高速、高维护费、不能直接连接建筑
}
```

> 上述枚举是早期设计草案，不是第二代当前实现。附录 D 已将 RoadType 分级数据、样式、选择和升级移至第三代；当前 `GraphEdge`、`RoadGroup`、提交 API 和 v2 存档 schema 均不含类型字段。junction-to-junction 规范 Edge、自环、平行 Edge 和 RoadGroup 移除同样属于第二代之后的未来范围。

---

## 5. 空间索引层

### 5.1 设计动机

消除 `Dictionary<Vector2, int>` 后需要一个替代方案来回答："玩家点击的位置 (x, y) 附近有哪些节点或边？"

### 5.2 UniformGrid（空间哈希网格）

当前 `UniformGrid` 以 `ISpatialRef` 为值存储。Node 注册到一个桶；每个 `EdgeGeometryRef` 以原生几何的保守 `Bounds` 注册到全部覆盖桶。`QueryRadius` 先遍历半径包围盒覆盖的桶、按引用身份去重，再调用引用的 `IntersectsCircle` 做权威过滤；`QueryBounds` 返回覆盖桶内的去重候选，由调用方完成几何相交、覆盖或拆分判定。

复杂度取决于覆盖桶数和桶内引用数。跨桶几何的插入/移除会访问每个覆盖桶，移除还会扫描桶内 `List<ISpatialRef>`；半径与矩形查询也会遍历全部覆盖桶并对候选去重。因此本文不再使用无条件 `O(1)` 插入/删除或 `O(1 + k)` 查询的表述。固定 10k/100k 数据集的实际结果由性能基线和 `--enforce-budget` 验证。

### 5.3 使用模式

`RoadGraph` 在 `_nodeRefs` / `_edgeRefs` 中保留已登记引用，以便按对象身份精确移除和从权威图重建索引。最近 Edge 查询从 `EdgeGeometryRef` 收集局部候选后，对每条候选的原生几何执行 `FindClosestPoint`；覆盖、交点、锚点和拆分使用 `QueryBounds` 候选，不把显示采样或 Edge 端点折线当作曲线真相。

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

**RoadGraph 当前流程**：

```
SubmitPolyline(points) / SubmitPath(nativeGeometry):

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
         c. 创建 GraphEdge(nodeA, nodeB, 原生几何段, groupID)
         d. 按每个原生几何段的保守 Bounds 在 SpatialIndex 中注册引用

  4. 合并降级（可选，性能优化）：
     IF 某节点连接数 == 2 且两侧边共线:
       合并为一条边
     — 这是一个"压缩"操作，不影响正确性，仅减少节点/边数量

  5. 返回结构化 RoadPathSubmissionResult；AddRoad 兼容入口仅映射为 groupID / -1
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

**RoadGraph 当前流程**：

```
RemoveEdges(edgeIDs):

  1. 对 Edge ID 去重并稳定排序，跳过已经失效的目标
  2. DetachEdge：从 _edges、SpatialIndex、两端 EdgeRef 和 RoadGroup 中移除全部命中边
  3. CommitEdgeMutation：一次清理所有受影响的孤立节点和空 Group
  4. Debug 构建在事件发布前验证完整图不变式
  5. 按 Edge ID 触发 EdgeRemoved 事件（处理器看到最终提交图）

RemoveEdge(edgeID):

  1. 复用 RemoveEdgesCore([edgeID])
  2. 保持相同的清理、不变式和事件契约

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
  2. 从 EdgeGeometryRef 收集去重 Edge 候选
  3. 对每条候选的原生几何段执行权威 FindClosestPoint
  4. 取距离最近的 Edge；近似等距时选择较小 Edge ID

FindEdgeIDsNear(position, radius):

  1. 复用 QueryRadius 的原生几何圆相交过滤
  2. 收集、去重并按 ID 排序全部命中 Edge

FindEdgeIDsIntersecting(bounds):

  1. QueryBounds 只收集与矩形覆盖相同 bucket 的 Edge 候选
  2. 用端点包含或原生几何与矩形边界交点排除 AABB 假阳性
  3. 去重并按 ID 排序返回
```

对比迁移前的三级回退（字典 → waypoint 扫描 → Junction ConnectedSegment），当前方案是局部空间候选 + 原生几何精确过滤。成本取决于半径覆盖桶数和桶内引用数，不宣称无条件 `O(1 + k)`。

---

## 7. API 设计

### 7.1 RoadGraph 公共接口

```csharp
public class RoadGraph
{
    // ── 创建 ──
    public RoadPathSubmissionResult SubmitPath(RoadPath? path);
    // 原生几何权威入口；成功返回完整变更摘要，失败返回结构化原因且无副作用。

    public RoadPathSubmissionResult SubmitPolyline(IReadOnlyList<Vector2>? points);
    // 折线结构化入口。

    public int AddRoad(Vector2 start, Vector2 end, Vector2[] waypoints);
    // 兼容入口：复用 SubmitPolyline，成功返回 groupID，失败返回 -1。

    // ── 删除 ──
    public bool RemoveEdge(int edgeID);
    // 移除单条边。自动清理孤立节点和空 Group。

    public bool RemoveEdges(IEnumerable<int>? edgeIDs);
    // 去重、稳定排序并批量删除仍存在的 Edge；只执行一次提交清理。

    public bool RemoveRoadGroup(int groupID);
    // 移除整个 Group 的所有边。

    // ── 查询 ──
    public GraphEdge? GetEdge(int edgeID);
    public GraphNode? GetNode(int nodeID);
    public RoadGroup? GetGroup(int groupID);

    public GraphEdge? FindClosestEdge(Vector2 position, float maxRadius);
    public IReadOnlyList<int> FindEdgeIDsNear(Vector2 position, float radius);
    public IReadOnlyList<int> FindEdgeIDsIntersecting(Rect2 bounds);
    public GraphNode? FindClosestNode(Vector2 position, float maxRadius);

    // ── 遍历 ──
    // 返回调用时稳定快照；后续图变更不会使既有枚举失效或改变其成员。
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
| `AddRoad(from, to, wps, cellSize, extendRoadID)` | `AddRoad(start, end, wps)` / `SubmitPolyline(points)` / `SubmitPath(path)` | 去掉 `cellSize`、`extendRoadID` 和 RoadType；新增结构化折线与原生几何入口 |
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
  EdgeAdded    → RoadRenderer.CacheEdgePoints(edge) + 安排合并批次重建
  EdgeRemoved  → RoadRenderer 移除缓存点列 + 安排合并批次重建
  GraphCleared → 清空缓存 + 从全部 Edge 重新采样和批处理
```

这个事件驱动模型 **不需要改动**。当前已经做得很好了。

### 8.2 原生几何显示采样

`RoadGeometryDisplaySampler` 只读取 `RoadGeometrySegment` 的 `Start`、`End`、`Length`、`GetPosition()` 和 `Split()`，以 `RoadConfig.CurveDisplayTolerance` 控制确定的世界空间误差。默认容差为 `0.25`，在相机最大 `4x` 缩放下对应约 1 个屏幕像素；曲线最多递归细分 16 层，line 使用精确两点快路径。每个源段的最后一点强制使用权威 `End`，相邻段共用连接点且不重复。

显示点列是可重建派生数据，不写回 `GraphEdge.GeometrySegments`，也不进入道路 JSON。`RoadRenderer` 按 Edge ID 缓存采样点列，拆除高亮直接复用缓存；静态道路将全部点列构造成一个共享边界的连续 `ArrayMesh` ribbon。同一事件循环内的 Edge 增删只触发一次延迟批次重建，避免批量删除或交叉拆分重复重建全图；`RoadBuilder` 对带有效 `RoadPath` 的草稿使用同一采样器，因此曲线预览和提交后形状使用一致求值路径。相机缩放不会改变世界空间点列；存档恢复触发 `GraphCleared` 后会从原生参数同步确定性重建。

### 8.3 节点渲染简化

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

判定逻辑一致，属性名从 `ConnectionCount` 变为 `EdgeCount`。实现不再逐节点调用 `DrawCircle`：全部端点与交叉口写入一个启用实例颜色的 `MultiMeshInstance2D`，共享圆形 canvas shader 和单位 `QuadMesh`。

### 8.4 批处理与规模基线

静态道路由一个抗锯齿 `ArrayMesh` ribbon 和一个节点 `MultiMesh` 组成，`RoadRenderer` 子节点数固定为 2。真实 `MapTest` / Vulkan 性能契约在 10k 和 100k 的固定直线数据集上测量镜头移动、施工预览、命中高亮与图恢复重建；连续帧测量关闭 VSync，避免 60 Hz 等待时间污染实际提交成本。

10k 的镜头/预览/高亮 P95 分别为 0.788/0.717/0.436 ms，满足 16.67 ms 硬门槛；100k 分别为 5.240/4.612/4.739 ms。两个规模的静态/高亮帧均为 4 draw calls / 4 objects，预览帧为 5 / 56，静态渲染子节点固定为 2。10k/100k 全图恢复及批次重建分别为 159.151 ms 和 1,170.055 ms，作为独立加载基线记录，不混入连续帧 P95。完整数据、优化前对照与测量口径见 `docs/performance/road-rendering-v2-baseline.md`。

### 8.5 道路分级视觉

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

未来渲染时需要按样式拆分 ribbon 批次：
```csharp
var style = Config.GetStyle(edge.Type);
styleBatches[style].Append(edgePoints, style.Width, style.Color);
```

> 这保持了全局 `RoadConfig` 的模式，同时支持未来按等级差异化。当前 `RoadRenderer` 只有一个道路 ribbon，统一使用 `RoadConfig` 的基础颜色和线宽。

---

## 9. 与模拟系统集成

### 9.1 交通模拟需要的接口

第三代道路分级完成后的交通模拟（A* 寻路、OD 矩阵、拥堵模型）需要以下路网查询能力：

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

增量更新（而非每次重建整个 TrafficGraph）是未来性能关键，但属于第三代之后的模拟实现细节。

---

## 10. 迁移策略

### 10.1 为什么不能一刀切重写

迁移前系统已经稳定运行，存档格式已定义，渲染系统工作正常。一刀切重写风险极高。这段保留当时的迁移判断，当前 RoadGraph 重构已经完成。

### 10.2 渐进式迁移记录

> 阶段 A 和阶段 B 的 RoadGraph / SpatialIndex / GraphNode / GraphEdge / RoadGroup 迁移已经完成。以下编号步骤是当时的迁移方案，只用于解释当前结构的来历，不是待执行清单；阶段 C 仍是未来模拟工作。

| 阶段 | 当前处置 | 当前事实或边界 |
|---|---|---|
| A：内部重构 | **已完成** | 旧位置字典和半格数据层分支已移除；`UniformGrid` 作为可重建查询服务，按原生几何包围盒登记 Edge 引用。 |
| B：API 清理 | **已完成** | 运行时已统一为 `RoadGraph` / `GraphNode` / `GraphEdge` / `RoadGroup`，数据层不接收 `CellSize`；公共路径入口为 `SubmitPath`，折线兼容入口为 `SubmitPolyline` / `AddRoad`。 |
| B 中的 RoadType 提案 | **已取代** | 附录 D 将 `RoadType` 排除出第二代；当前运行时、公共 API 和 v2 存档 schema 均不包含该字段，第三代必须以新契约和新 schema 版本引入。 |
| B 中的旧存档兼容提案 | **已取代** | 附录 D 明确第二代不兼容旧道路存档；缺失版本、旧版本、未知未来版本和损坏内容必须安全拒绝。 |
| C：模拟集成 | **延期** | `TrafficGraph`、A*、拥堵和车流增量同步晚于第三代 canonical RoadGraph 与道路分级，不计入第二代。 |

分三个阶段，每个阶段可独立交付和测试：

#### 阶段 A：内部重构（历史计划，已完成）

1. **引入 `SpatialIndex (UniformGrid)`** 作为 `_posToJunctionID`/`_posToSegmentID` 的并行实现
2. **所有查询方法先查 SpatialIndex，查不到再回退字典**（双写单读）
3. 删除所有"半格点特殊分支"——
   - `IsAnyJunctionAt` → 直接用 `FindClosestNode(pos, tinyRadius)`
   - `FindSegmentAtIncludingHalfGrid` → 直接用 `FindClosestEdge(pos, radius)`
   - `GetOrCreateJunction` 中的 `IsSnapGrid` 分支 → 移除，所有节点一律对待
4. 验证：现有功能全部正常

**交付物**：RoadNetwork 内部已统一使用 SpatialIndex，但外部 API 不变

#### 阶段 B：API 清理（历史计划，已完成或取代）

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
5. **原提案：引入 `RoadType`**：已由附录 D 取代；第二代不保留类型字段或兼容层
6. 更新 `RoadBuilder`、`RoadRenderer`、`GameHUD` 的 API 调用
7. **原提案：更新存档格式并兼容旧存档**：已由附录 D 取代；第二代使用严格 v2 schema 并拒绝旧格式

**交付物**：新旧 API 完全迁移，旧字典索引完全移除

#### 阶段 C：模拟集成（未来路线，第二代不执行）

1. 构建 `TrafficGraph`（交通模拟视图）
2. 实现 `RoadGraph` 事件的增量更新
3. 实现 A* 寻路（可在 RoadGraph 上直接做，也可在 TrafficGraph 上做）
4. 实现道路升级工具（修改 `RoadType`）

**交付物**：道路系统完整支持交通模拟

### 10.3 存档兼容性

旧公开 `RoadNetworkData`、`JunctionData`、`SegmentData`、`RoadData` 和 `Vector2Data` 已删除。第二代只写 private V2 payload：`schemaVersion = 1`、`nextID`、`nodes`、`edges`、`groups`；Edge 的 `geometry` 保存原生类型、版本和控制参数。

```csharp
// 当前私有 payload 的逻辑形状
RoadGraphSaveData {
    schemaVersion: 1,
    nextID,
    nodes,
    edges: [{ nodeAID, nodeBID, groupID, geometry }],
    groups
}
```

加载先在临时状态中全量校验版本、未知字段、ID、引用、Group 双向关系、孤立节点、原生几何和 `nextID`，只有成功后才替换活动图。缺失版本、旧 `version/junctions/segments/roads` 格式、未知未来版本和损坏内容均拒绝且不修改当前图；旧道路存档兼容不属于第二代。

---

## 11. 路线图与优先级

### 优先级矩阵

> 下表将原始优先级映射到当前处置状态。历史 P0～P4 不再作为当前迭代顺序；第二代实际执行顺序以附录 D 和 `docs/todo/` 中的系统待办为准。

| 原始任务 | 原优先级 | 当前状态 | 当前执行依据 |
|---|---:|---|---|
| 阶段 A：引入 SpatialIndex，消除半格特判 | P0 | **已完成** | `road-graph:3.1`～`3.3` 已验证局部几何候选、10k 门槛和 100k 压测。 |
| 阶段 B：API 清理、移除网格依赖和收敛图事务 | P1 | **已完成** | `road-graph:2.1`～`2.7`、`4.1`～`4.4` 已完成公共路径、原生几何、封装、不变式和删除事务。 |
| RoadType 与分级渲染 | P2 | **第三代** | 第二代契约已移除 RoadType；未来需新 API、schema、样式和迁移决策。 |
| TrafficGraph、A* 与拥堵 | P3 | **第三代之后** | 依赖道路分级与模拟产品设计，不计入第二代验收。 |
| 道路升级工具 | P4 | **第三代** | 与 RoadType 一并设计和交付。 |

### 推荐执行顺序

```
已完成：阶段 A / B 的 RoadGraph 核心迁移与事务收敛
    │
    ├── 当前：按附录 D 和 docs/todo/ 完成第二代输入、渲染、编辑、存档与最终集成验收
    │
    ├── 第三代：规范化 junction-to-junction Edge 与环路，再引入 RoadType、分级样式和道路改造
    │
    └── 第三代之后：基于稳定 RoadGraph 构建 TrafficGraph、A*、拥堵和车流增量同步
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
| `Direction`, `DirectionUtil` | *保留但仅用于默认输入策略* | `SquareEightRoadInputStrategy` 执行 8 方向投影 |
| `GridSystem` | *保留但不再被 RoadGraph/RoadBuilder 引用* | 供其他 UI 与调试组件使用 |
| `RoadConfig` | *当前保留；第三代可扩展 RoadTypeStyle[]* | 第二代只使用统一样式 |

## 附录 B：不变式对比

| 迁移前系统的不变式（易破裂） | RoadGraph 系统的不变式（由设计保证） |
|---------------------------|-------------------------------|
| `_posToSegmentID[key]` 的 key 必须经 `SnapToGrid` 标准化 | **无此不变式** — 空间索引用浮点距离匹配 |
| 半格 Junction 不能出现在 `_posToJunctionID` | **无此不变式** — 不存在 `_posToJunctionID` |
| 删段后 `MaybeReindexJunctionInPosDict` 必须补回共享条目 | **无此不变式** — 空间索引独立维护，删边不影响其他边 |
| `_inMergeOperation` 必须设 true 防止递归合并 | **无此不变式** — 删段不触发合并 |
| `SplitRoadIntoConnectedComponents` 必须保证 Group 内连通 | **无此不变式** — Group 允许不连通 |
| 半格起点必须限定对角方向 | 移入 `SquareEightRoadInputStrategy` 的局部约束，与生命周期和数据层无关 |

---

## 附录 C：范围确认前的历史完成度快照

> 评估基准：当前工作区源码、主场景集成状态、`docs/todo/README.md`，以及本指南第 2～11 节的目标定义。
>
> 本节记录范围确认当时对“架构已经落地”和“仍需继续完善行为”的判断，不把当时明确延期的产品功能视为 V2 基础架构缺陷。
>
> 本节保留范围确认前的实现快照，不是当前状态表。其中关于 RoadType、旧存档兼容、删除、空间索引和阶段边界的描述已经过时；当前事实见第 3～11 节，最终交付范围与完成判定以附录 D 和 `docs/todo/` 为准。

### C.1 当时的总体结论

范围确认时，V2 道路系统已经完成了**核心架构迁移**，但尚未完成设计指南中定义的全部行为和后续产品能力。

准确结论是：

> **V2 已完成核心骨架和主要数据迁移，当前处于“基础架构可用、关键语义待收敛、产品扩展未启用”的阶段。**

当前实现已经可以作为游戏中的基础路网使用，支持连续坐标节点、交叉拆边、空间索引、事件驱动渲染、存档恢复和基础道路编辑。但它还不能宣称完全实现本指南，因为删除事务、完整线段空间查询、数据层几何自由度、公共 API 契约和自动化验证仍未全部闭合。

### C.2 当时的分层完成状态

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

### C.3 当时的 P1～P5 完成度

| 原则 | 当前判断 | 完成度说明 |
|---|---|---|
| **P1 图即真相** | **基本完成** | 图实体已成为唯一权威数据；空间索引不承担主存储职责。剩余工作主要是封装和自动化验证。 |
| **P2 连续空间，离散化是 UI 的事** | **部分完成** | `CellSize` 已留在输入层，但数据层仍以 `DirectionUtil` 参与共线合并，尚未完全独立于八方向概念。 |
| **P3 空间索引是服务，不是存储** | **部分完成** | 独立 `UniformGrid` 已落地，但线段占据 bucket 和真正的最近边几何查询还未完成。 |
| **P4 添加比修改简单，修改比删除简单** | **未完成** | 删除仍会触发自动合并和节点修复链，尚未达到“删除不触发拓扑压缩”的设计目标。 |
| **P5 不变式最小化** | **部分完成** | 旧位置字典不变式已删除，但图字典、邻接、Group、空间索引、事件和存档仍需共同维护。 |

### C.4 当时的 V2 阶段状态

| 阶段 | 状态 | 结论 |
|---|---|---|
| 阶段 A：内部引入 SpatialIndex、消除旧半格数据层特判 | **主体完成** | 新图模型和独立空间索引已经存在，但线段空间占据和性能验证仍需补齐。 |
| 阶段 B：API 清理、移除网格依赖、存档兼容 | **大部分完成** | 核心命名和主要 API 已迁移；公共 `AddEdge`、数据层方向约束、删除语义和存档失败保护仍未闭合。 |
| 阶段 C：TrafficGraph、A*、增量模拟同步 | **未开始** | 按 Phase 6 路线图延期，不作为当前 V2 基础清理的阻塞项。 |

### C.5 当时版本可以宣称的能力

当时版本可以准确宣称：

- 使用 `RoadGraph` 作为连续空间路网数据层。
- 使用 `GraphNode`、`GraphEdge` 和 `RoadGroup` 管理道路拓扑。
- 支持正交/对角道路、交叉拆边、waypoint 交点和基础拆路。
- 使用独立 `UniformGrid` 支持节点和边空间引用查询。
- 使用事件驱动方式同步道路 Line2D 渲染。
- 支持道路图保存、加载和旧格式类型回退。

当时版本不应宣称：

- 已完成无网格约束的任意角度数据层。
- 已完成真正的线段级最近道路查询和局部空间查询性能目标。
- 删除道路完全不触发拓扑合并或修复。
- 已完成 RoadType 分级视觉、类型选择或道路升级。
- 已完成 TrafficGraph、A*、拥堵模拟或车流增量同步。

### C.6 当时列出的 V2 基础清理剩余条件

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

---

## 附录 D：第二代最终范围与验收记录

> 本附录记录 2026-08-02 确认的第二代交付边界，是后续验收第二代道路系统的范围依据。完成后保留本附录作为历史验收记录，不删除本表。

### D.1 交付目标

第二代交付一个成熟、可扩展的铺路系统和道路存档系统：当前玩家玩法继续使用米字型八方向网格，但 `RoadGraph` 和几何模型不得依赖该网格；底层必须能表达任意拓扑和原生曲线，并允许未来输入策略在不修改 RoadGraph 的前提下替换。

道路存档只要求持久化道路网络。其他系统未来继续通过独立 JSON 接入同一槽位，不要求第二代保存人口、资金或镜头状态；人口和资金当前只作为存档列表元数据占位符。

### D.2 第二代包含的能力

- 原生保存直线、Bézier、样条、圆弧/圆锥曲线及铁路常用缓和曲线（至少包括回旋线/clothoid）等几何段的类型和控制参数，而不是只保存离散采样点。
- 对上述曲线执行长度计算、最近点、空间索引、覆盖判断、交点求解、拆分、删除、渲染和保存恢复，同时保持曲线类型与参数语义。
- 所有同一二维平面内的几何交叉自动产生拓扑连接；桥梁、隧道、立交和高程分层不属于第二代。
- 提供独立于 `RoadBuilder` 的公共路径提交 API，接受任意合法路径并返回可诊断的成功或失败结果。
- 当前玩家输入继续采用米字型策略；输入约束通过可替换策略提供，并以三角形网格和六边形网格实现验证可替换性。
- 玩家可以连续铺设多段道路、增加拐点、调整预览、取消操作；拆路支持单段、连续拆除和框选删除，并提供撤销与重做。
- 支持多个玩家命名的手动存档、另存为、覆盖确认、列表、加载、删除和独立自动存档。
- 存档列表显示存档名称、保存时间、城市名称、人口、资金和缩略图；尚无数据来源的城市名称、人口和资金允许使用明确占位值，缩略图必须有稳定的占位或实际截图路径。
- 旧道路存档不兼容。第二代使用新的明确版本，缺少版本、旧版本、未来未知版本或损坏内容均安全拒绝，且不修改当前道路图。
- 10k Edge 是 60 FPS 下的硬性基础规模；交互路径必须满足单帧预算。100k Edge 只做压力测试并记录结果，不阻塞第二代完成。
- 完成后必须在 Godot 主场景执行完整系统评估，覆盖铺路、交叉、曲线、拆除、撤销重做、命名存档、覆盖、删除、自动存档和加载失败保护。

### D.3 明确排除的能力

- `RoadType` 分级数据、差异化样式、类型选择和道路升级全部属于第三代；第二代的图 API、几何模型和存档 schema 不以 `RoadType` 为契约组成部分。
- `TrafficGraph`、A*、拥堵、车流增量同步及其他交通模拟晚于第三代 canonical RoadGraph 与道路分级，不计入第二代。
- 玩家自由曲线编辑器不作为第二代硬性玩法要求；第二代只要求底层原生曲线能力、公共路径 API 和可替换输入策略。
- 桥梁、隧道、立交、高程层和二维交叉不连接规则不属于第二代。
- 旧存档迁移和旧 JSON 字段兼容不属于第二代。

### D.4 范围事项与当前状态

> 2026-08-04 最终状态：V2-1～V2-11 及其所属系统工作项均已完成。最终组合契约已在同一 `MapTest` 实例中覆盖图、输入、渲染、命名存档、自动存档和失败保护；完整自动化、真实 Vulkan 视觉、10k 硬门槛、100k 压测和 Windows 导出边界均有持久证据。

| ID | 范围事项 | 所属系统 | 验收摘要 |
|---|---|---|---|
| V2-1 | 原生曲线几何模型与曲线拓扑运算 | `road-graph:2.5`、`road-graph:2.6` | 曲线类型和控制参数可保存；查询、交叉、拆分与删除不降级为权威折线 |
| V2-2 | 公共路径提交 API 与失败结果 | `road-graph:2.4` | 任意合法路径可从非 UI 调用方提交；失败无副作用且原因可诊断 |
| V2-3 | 移除数据层网格与 RoadType 契约 | `road-graph:2.1`、`road-graph:2.2`、`road-graph:2.7` | RoadGraph 不依赖方向枚举、CellSize 或道路分级 |
| V2-4 | 曲线空间索引与性能 | `road-graph:3.1`～`road-graph:3.3`、`grid-rendering:1.2` | 10k Edge 满足 60 FPS 硬门槛；100k Edge 压测结果已记录 |
| V2-5 | 删除事务和图不变式 | `road-graph:4.1`～`road-graph:4.4` | 单删、批量删、拆分、撤销和失败路径结束后图一致 |
| V2-6 | 可替换网格策略 | `tool-input:1.2`、`tool-input:1.3` | 米字型、三角形和六边形策略使用同一接口，RoadGraph 无需修改 |
| V2-7 | 成熟铺路与拆路交互 | `tool-input:1.4`～`tool-input:1.6` | 连续铺路、拐点、预览、取消、连续拆除、框选删除、撤销重做可用 |
| V2-8 | 原生曲线渲染与预览 | `grid-rendering:1.1`、`grid-rendering:1.2` | 曲线显示、交点和预览与权威几何一致 |
| V2-9 | 多命名存档与元数据 | `save-system:1.1`～`save-system:1.4` | 手动槽、自动槽、覆盖、删除、列表元数据和缩略图流程完整 |
| V2-10 | 新道路 schema 与失败保护 | `save-system:0.3`～`save-system:0.5`、`save-system:0.9`～`save-system:0.11` | 只保存道路网络；旧/未知/损坏存档安全拒绝且不改变当前图 |
| V2-11 | 完整系统评估 | `road-graph:7.1` | 已完成；自动化、构建、主场景组合流程、真实渲染、导出和性能证据全部通过并记录 |

### D.5 完成判定

只有 D.4 中所有事项及其所属系统待办均有实际验证证据，且 `road-graph:7.1` 的 Godot 主场景完整评估通过，第二代道路系统才可标记完成。RoadType、交通模拟、高程道路和旧存档兼容不得作为第二代完成的阻塞项，也不得被误写为第二代已实现能力。

> 完成判定（2026-08-04）：上述条件全部满足，第二代道路系统验收完成。后续规范 Edge/环路、RoadType、交通模拟、高程道路和旧存档迁移必须作为第三代或更晚范围重新立项，不重新打开第二代完成状态。

### D.6 最终验收证据

| 门禁 | 最终证据 |
|---|---|
| 主场景组合流程 | `tests/godot/road_system_v2_final_runtime_contract.gd` 在同一 `MapTest` 中完成内部交叉、三段连续铺路与取消、单删/连续删/框选删、撤销重做、两个同名手动槽的覆盖/切换/删除、六类原生几何、自动存档和损坏加载保护，清理测试槽并恢复运行前 autosave 后输出 `PASS`。 |
| 主场景专项回归 | `command_center_runtime_contract.gd`、`road_input_strategy_runtime_contract.gd`、`autosave_runtime_contract.gd`、`pause_menu_runtime_contract.gd` 和 `road_curve_rendering_runtime_contract.gd` 均再次输出 `PASS`；确认真实工具输入、存档确认 UI、键盘/鼠标路径和曲线重建没有被组合流程掩盖。 |
| 自动化与构建 | 同一 C# 工作树本批早先的 `csharp-ls --diagnose --solution SimpleCities.sln --loglevel warning` 无诊断，Debug 构建为 0 警告、0 错误；最终重跑构建仍为 0 错误，但 NuGet 官方漏洞源不可达产生 1 个外部 `NU1900`。`dotnet test SimpleCities.sln --configuration Debug --no-build --no-restore` 为 474/474。 |
| RoadGraph 性能 | Release `--enforce-budget` 复测中，10k 七类操作最慢为多交叉 P95 5.231 ms，全部低于 16.67 ms；100k 七类场景完整记录，最慢为长路提交 12.211 ms，不参与硬门槛。 |
| Vulkan 渲染与视觉 | 10k 镜头/预览/高亮 P95 为 0.565/0.659/0.497 ms；100k 为 4.794/4.545/5.292 ms，静态帧保持 4 draw calls / 4 objects。六类曲线截图为 72,483 字节，画面非空、端点完整、曲线可辨识且连续 ribbon 无可见段缝。 |
| Windows 导出与存档根 | QA 导出包在隔离 `user://saves` 中完成中文/路径字符命名槽写入和清理；真实拒绝写入 ACL 下 `SaveAs` 明确失败、`CurrentSlotID` 不变且无发布文件。正式导出不携带 `tests/`、`saves/` 或 `docs/`，仍启动 `MapTest`；最终 QA pack 复核确认只携带 `exported_save_runtime_contract`，不携带 `road_system_v2_final_runtime_contract`。 |

独立 CLI Godot 进程仍会报告 Windows 根证书读取错误，以及脚本直接实例化 `MapTest` 时 `ConstructionDock` 先于 `ToolManager.Instance` 的既有降级警告；两者均不影响契约断言、退出码、Vulkan 渲染或清理结果。损坏加载场景产生的 `SaveManager` 错误日志是被明确断言的预期失败证据。
