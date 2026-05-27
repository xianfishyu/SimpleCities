# 道路网络系统设计

> 状态：草案 | 最后更新：2026-05-27

---

## 1. 设计原则

- **网络模型（双层）**：
  - **Junction**：路口/端点，仅在道路交汇或终止处存在
  - **Segment**：相邻两个 Junction 之间的"几何边"——含中间 waypoints + 总长度，是寻路与渲染的基本单位
  - **Road**：玩家"一次画线"产生的逻辑聚合，引用一组 Segment ID。一条 Road 在劈分后仍是同一条 Road（语义保留）；多条 Road 在端点对齐合并时较小 RoadID 吸收较大 RoadID
  - **不存储逐格数据**
- **8 方向**：路段方向限定为正交（N/S/E/W）+ 斜交（NE/NW/SE/SW），这是游戏风格决策而非技术约束
- **网格对齐是纯风格**：坐标 snap 到 CellSize 格点仅为了让道路"看起来整齐"，**网格不产生任何数据结构**。去掉 SnapToGrid 即可支持自由曲线
- **共享配置**：CellSize / RoadColor / RoadWidth / JunctionRadius 集中在 `RoadConfig` Resource，编辑器一处可调
- 完全俯视（Top-Down），矢量极简渲染

---

## 2. 网格：风格工具，非数据层

### 2.1 核心认知

> 网格对齐 = 美术风格选择，等价于"像素画用整数坐标"。
> 没有 GridMap、没有 CellType、没有 GridCoord。
> 道路可以自由画——只是每个顶点会被 snap 到格点，视觉上保持规整。
> **如果未来想支持自由曲线（对标 Cities: Skylines Bézier），去掉 SnapToGrid 即可，数据结构不变。**

### 2.2 对齐函数（纯函数，零状态）

```csharp
static Vector2 SnapToGrid(Vector2 pos, float cellSize)
    => new(
        Mathf.Floor(pos.X / cellSize) * cellSize + cellSize / 2,
        Mathf.Floor(pos.Y / cellSize) * cellSize + cellSize / 2
    );
```

所有道路顶点的坐标都经过这个函数对齐。仅此而已。无副作用，无状态。

### 2.3 8 方向位移表

```
方向     (dx, dy)    角度
N        ( 0, -1)    0°
NE       (+1, -1)    45°
E        (+1,  0)    90°
SE       (+1, +1)    135°
S        ( 0, +1)    180°
SW       (-1, +1)    225°
W        (-1,  0)    270°
NW       (-1, -1)    315°
```

---

## 3. 道路网络拓扑（双层模型）

### 3.1 三层结构一览

```
玩家视角            数据层
─────────          ──────────────────────────────────────────
"我画了一条路"  →  Road（聚合 ID 集合）
                       ↓ 引用 N 个
                   Segment（几何边：A → B 的连续 waypoint 路径）
                       ↓ 端点是
                   Junction（路口/端点：世界坐标 + 邻接信息）
```

- **Junction**：世界坐标点，仅在交叉口 / 端点存在
- **Segment**：连接两个 Junction 的几何边，内含中间 waypoints。**寻路、渲染的基本单位**
- **Road**：玩家"一次画线"产生的逻辑聚合，含一组 Segment ID

### 3.2 与单层模型的区别

```
单层模型（旧）：每个格点都是 Junction
  ●──●──●──●──●──●──●    7 个 Junction, 6 条边

双层模型（现）：Junction 只在交叉口和端点
  ●──────────────────●    2 个 Junction, 1 个 Segment（内含 5 个 waypoint）, 1 个 Road
```

**Junction = 道路开始 / 结束 / 交汇的地方。** 一条很长的直路中间没有 Junction。

### 3.3 Road vs Segment 的关系

```
玩家分两次操作：
  ①  画 (0,0) → (10,0) → (10,10)（L 折线）
  ②  画 (5,0) → (5,5)（T 进入 ① 中间）

数据状态：
  Junction：(0,0), (5,0), (10,0), (10,10), (5,5)   共 5 个
  Segment：
    s0: (0,0)→(5,0)        属于 Road A   ┐
    s1: (5,0)→(10,0)       属于 Road A   ├ ① 被 ② 劈成 2 段，
    s2: (10,0)→(10,10)     属于 Road A   │  RoadID 不变，仍是同一条 Road
    s3: (5,0)→(5,5)        属于 Road B  ─┘
  Road：
    Road A: { s0, s1, s2 }   语义："那条 L 折线"
    Road B: { s3 }           语义："T 字进来那一笔"
```

**关键不变量**：
- 劈分（split）**不会**改变 Segment 所属的 RoadID（语义保留）
- 端点对齐合并（merge）时较小 RoadID **吸收**较大 RoadID，被吸收 Road 从 `_roads` 移除

### 3.4 Segment 的 waypoint 示例

```
Segment {
    From: Junction#0 pos=(0, 64)
    Waypoints:    [(64,64), (128,64), (192,64), (256,64), (320,0), (384,0)]
    To:   Junction#1 pos=(448, 0)
}

视觉（每个 ● 是格点，但只有两端的 ● 是 Junction）：
  (0,64)                                                (448,0)
     ●──●──●──●──●──●──●──●──●──●──●──●──●──●──●──●──●
     ← 4格直行 E →  ← 1格转45° →  ← 3格斜行 NE →
```

渲染时遍历 Segment 的 waypoints 两两之间的子段，每个子段一段直线。

**转弯不创建 Junction**：方向变化（如 E → NE）只是 Segment 内部的 waypoint。
只有"多 Segment 在某点交汇"或"Segment 的起止点"才是 Junction。

### 3.5 交叉口自动判定

判定依据：**每个连接到该 Junction 的 Segment，取其在该 Junction 处的出入方向**（waypoint 的首段或末段方向）。

| 连接 Segment 数 | 方向分布 | JunctionType |
|----------------|---------|--------------|
| 1 | — | Endpoint |
| 2 | 两 Segment 方向对向 | Straight |
| 2 | 两 Segment 方向不对向 | Curve |
| 3 | — | TJunction |
| 4 | 全正交（N,S,E,W） | Cross |
| 4 | 全斜交（NE,SE,SW,NW） | XCross |
| 5+ | — | MultiWay |

---

## 4. 数据结构

### 4.1 RoadNetwork — 路网容器

```csharp
class RoadNetwork
{
    Dictionary<int, Junction> _junctions;
    Dictionary<int, Segment>  _segments;
    Dictionary<int, Road>     _roads;
    Dictionary<Vector2, int>  _posToJunctionID;   // 仅存 Junction 位置，不含 waypoint
    Dictionary<Vector2, int>  _posToSegmentID;    // 任一格点 → 占用它的 Segment（含 waypoint）

    // ────────────────────────────────────────────────────────────────
    // 主入口：玩家"画一条路"的入口
    //   from / to       两端坐标（可命中已有 Junction，也可命中现有 Segment 中段）
    //   waypoints       中间途经格点（可为空数组）
    //   cellSize        网格大小（来自 RoadConfig.CellSize）
    //   extendRoadID    占位参数（"将本次新画段并入已有 Road"），当前未实现
    //   返回值          成功 = 新建 / 参与的 RoadID；失败 = -1
    // ────────────────────────────────────────────────────────────────
    int AddRoad(Vector2 from, Vector2 to, Vector2[] waypoints,
                float cellSize, int? extendRoadID = null);

    bool RemoveSegment(int segmentID);   // 拆掉单一几何边
    bool RemoveRoad(int roadID);         // 拆掉整条 Road（包含的所有 Segment）

    Junction? GetJunction(int id);
    Junction? GetJunctionAt(Vector2 pos);
    bool      HasJunctionAt(Vector2 pos);
    Segment?  GetSegment(int id);
    int       FindSegmentAt(Vector2 pos);   // -1 表示未命中
    Road?     GetRoad(int id);

    IEnumerable<Junction> GetAllJunctions();
    IEnumerable<Segment>  GetAllSegments();
    IEnumerable<Road>     GetAllRoads();

    static Vector2 SnapToGrid(Vector2 pos, float cellSize);

    event Action<Segment> SegmentAdded;
    event Action<Segment> SegmentRemoved;
}
```

**AddRoad 流程**（关键步骤）：

1. snap `from` / `to` / 所有 waypoint 到格点
2. 校验：相邻 waypoint 必须 8 方向相邻；路径无重复点；非自环
3. 完全重叠预检（IsPathFullyCovered）→ 整段路径已被现有 Segment 覆盖则拒绝
4. 创建 newRoad 占位（占用一个 RoadID）
5. **劈分阶段**：路径上每个落在已有 Segment 中段的点，把那条 Segment 切成两段（继承原 RoadID）
6. **生成阶段**：按 Junction 把路径切成若干 Segment，每段挂到 newRoad
7. **合并阶段**：对"snap 前 1 连接、snap 后 2 连接"的 Junction 调用 `TryMergeAtJunction`，合并时较小 RoadID 吸收较大
8. 若 newRoad 经合并后变空，从 `_roads` 移除
9. 返回 newRoad.ID（成功）或 -1（失败）

**RemoveSegment 流程**：

1. 从两端 Junction 移除 SegmentID 连接
2. Junction 若孤立（ConnectionCount == 0）→ 删除该 Junction
3. 从所属 Road 的 SegmentIDs 中移除；Road 若空 → 删除该 Road
4. **若 Road 非空**：调用 `SplitRoadIntoConnectedComponents` 检查剩余 Segment 是否仍构成"连续路径"——
   通过 BFS 按"两 Segment 是否共享 Junction"判定连通性。若分裂为多个连通分量，第一个分量保留原 RoadID，
   其余每个分量分配新 RoadID。这维持了"Road = 连续路径的总和"的语义不变量。
5. 触发 `SegmentRemoved`

**RemoveRoad 流程**：遍历该 Road 的所有 Segment，逐个 RemoveSegment（连通性切分对单 Road 内全删场景无影响，
因为最后 Road 必定空被清理）。

### 4.2 Junction — 路口

```csharp
class Junction
{
    int ID { get; }
    Vector2 Position { get; }
    JunctionType Type { get; private set; }

    // 每个连接到该 Junction 的 Segment 一项（多重边 + 同 Road 多 Segment 都正确）
    // key = SegmentID, value = (邻居 JunctionID, 该 Segment 在此处的方向)
    Dictionary<int, (int neighborJunctionID, Direction dir)> _connections;

    void AddSegmentConnection(int segmentID, int neighborJunctionID, Direction dirAtThisJunction);
    void RemoveSegmentConnection(int segmentID);

    int                       ConnectionCount   { get; }
    IEnumerable<int>          ConnectedSegmentIDs { get; }
    IReadOnlyList<int>        NeighborJunctionIDs { get; }   // 含重复（多重边）
    IReadOnlyList<Direction>  IncomingDirections  { get; }

    void RecalculateType();   // 分析 IncomingDirections
}
```

**为什么用 SegmentID 作 key 而不是 RoadID 或 NeighborJunctionID？**
- 劈分场景：同一条 Road 的两个 Segment 会同时接到中间 Junction → RoadID 作 key 会冲突
- 多重边场景：A↔B 之间可能有多条 Segment（不同形状/路径） → NeighborJunctionID 作 key 会冲突
- SegmentID 是唯一身份，能正确表达上述拓扑

### 4.3 Segment — 几何边

```csharp
class Segment
{
    int ID             { get; }
    int FromJunctionID { get; }
    int ToJunctionID   { get; }

    // 所属 Road。劈分时继承原 Segment 的 RoadID；合并时被吸收方改为 keepRoadID
    int RoadID { get; internal set; }

    Vector2[] Waypoints { get; }   // 中间途经格点（不含两端 Junction 坐标）
    float     TotalLength { get; }

    // 供渲染遍历：返回 (from, to, dir) 序列，从 FromJunction → Waypoints → ToJunction
    IEnumerable<(Vector2 from, Vector2 to, Direction dir)> GetSubSegments(
        Junction fromJunction, Junction toJunction, float cellSize);
}
```

**GetSubSegments 示例**：

```
FromJunction pos = (0, 64)
Waypoints         = [(64,64), (128,64)]
ToJunction pos    = (192, 64)

输出：
  ((0,64)   → (64,64),  Direction.E)
  ((64,64)  → (128,64), Direction.E)
  ((128,64) → (192,64), Direction.E)
```

### 4.4 Road — 玩家画线聚合

```csharp
class Road
{
    int ID { get; }
    IReadOnlyCollection<int> SegmentIDs { get; }   // HashSet<int> 视图

    void AddSegment(int segmentID);
    void RemoveSegment(int segmentID);
    bool ContainsSegment(int segmentID);

    bool IsEmpty      { get; }
    int  SegmentCount { get; }
}
```

**Road 的语义**：玩家心目中"那条路"——一次画线生成的所有 Segment 共享同一 RoadID。
- 劈分（被其他 Road 穿过）：仍是同一条 Road（SegmentCount 增加）
- 合并（与另一条 Road 端点对齐拼接）：较小 RoadID 吸收较大 RoadID
- 玩家显式"扩展已有 Road"：`AddRoad(..., extendRoadID: X)`，**当前未实现**（参数已占位）

### 4.5 RoadConfig — 共享配置 Resource

```csharp
[GlobalClass]
partial class RoadConfig : Resource
{
    [Export] public float CellSize       { get; set; } = 64f;
    [Export] public Color RoadColor      { get; set; } = new("#37474F");
    [Export] public float RoadWidth      { get; set; } = 12f;
    [Export] public float JunctionRadius { get; set; } = 10f;
}
```

**消费者**：`RoadBuilder` / `RoadRenderer` 通过 `[Export] RoadConfig Config` 绑定同一个 `Scenes/road_config.tres`。
**好处**：编辑器一处可调（CellSize、外观），运行时自动同步。

### 4.6 Direction — 方向工具

```csharp
enum Direction { N, NE, E, SE, S, SW, W, NW }

static class DirectionUtil
{
    static Vector2I GetDisplacement(Direction d);
    static Direction? FromDisplacement(Vector2 posA, Vector2 posB, float cellSize);
    static bool IsOrthogonal(Direction d);
    static bool IsDiagonal(Direction d);
    static float Length(Direction d, float cellSize);
    static Direction[] All { get; }
}
```

---

## 5. 寻路系统

直接在 RoadNetwork 图上 A* 寻路（非网格 A*）。

### 5.1 图 A*

```
起点 → 最近的 RoadNode（欧几里得距离）
终点 → 最近的 RoadNode
边权重 = Edge.Length
启发式 = 节点间欧几里得距离
```

### 5.2 相比网格寻路的优势

- 搜索空间小得多（只搜路口节点，不搜所有格子）
- 天然考虑道路拓扑（断头路不可达）
- 容易加入交叉口转向惩罚

---

## 6. 渲染方案

### 6.1 矢量绘制策略

使用 Godot `CanvasItem._Draw()` API：

| 元素 | 绘制方式 |
|------|----------|
| 路段（正交） | `DrawRect()` — 以路段中心线为轴的填充矩形 |
| 路段（斜交） | `DrawColoredPolygon()` — 填充旋转矩形 |
| 交叉口 | `DrawCircle()` — 填充圆（非 Endpoint 节点） |
| 施工预览（拖拽中） | 半透明虚线 |

### 6.2 渲染层级

```
Layer 0: 地形底色（水域 / 绿地）
Layer 1: 道路网络（路段 + 交叉口）
Layer 2: 分区色块
Layer 3: 调试叠加（预览线、悬停高亮）
```

### 6.3 事件驱动渲染

`RoadRenderer` 订阅 `RoadNetwork.SegmentAdded` / `SegmentRemoved` → 增删对应 `Line2D` 节点。
渲染按 **Segment** 而非 Road（一条 Road 在劈分后含多个 Segment，每个 Segment 一个 Line2D）。
颜色 / 宽度从 `RoadConfig` 读取，编辑器调整后下次渲染生效。

### 6.4 Junction 视觉区分（端点 vs 真路口）

`Junction` 在视觉上按 `ConnectionCount` 分两类，避免"T 路口跟一条直路看起来一样"的歧义：

| ConnectionCount | 类型 | 视觉 | 配置字段 |
|---|---|---|---|
| 1 | 端点（路尽头） | 小灰色圆 | `EndpointRadius` / `EndpointColor` |
| ≥ 2 | 真路口（T、十字、转弯、X 半格交点） | 大高亮色圆（默认琥珀黄） | `JunctionRadius` / `JunctionColor` |

合并阶段已把"对向直通"的 ConnectionCount==2 节点降级回 waypoint，所以剩下的 ConnectionCount==2 一定是非对向的转弯点（Curve），仍当真路口画。

合并触发时机有两处：
- **AddRoad 时**：第四步对快照中 ConnectionCount==1 → 2 的 Junction 调 `TryMergeAtJunction`
- **RemoveSegment 末尾**：被删段两端 Junction 若从 ≥3 降到 2 且对向直通，触发合并降级（如 T 路口拆除分支后水平左右两半合并回单段）

合并判定（严格）：从 Junction 出发指向两段相邻点的方向必须互为反向（dispA + dispB == 0）。L 形 / 直角弯虽每段单独合法 8 方向，但合并后不是直线序列，违反"junction 降级回 waypoint"语义——waypoint 仅用于直线序列上的过渡点，故 Curve 保留为真路口。

`TryMergeAtJunction` 内部 `RemoveSegment` 受 `_inMergeOperation` 守卫位保护，避免递归触发末尾合并造成级联误并。

### 6.5 HUD 统计区

`GameHUD` 显示三层数据，对应 §3 的双层模型：

| 显示 | 数据来源 | 含义 |
|---|---|---|
| 道路 (Road) | `GetAllRoads().Count()` | 玩家"一次画线"产生的连续路径数；劈分不增、合并减；范围内含 1..N 个 Segment |
| 路段 (Segment) | `GetAllSegments().Count()` | 节点间的几何边数；劈分增、合并减 |
| 路口 (Junction) | `GetAllJunctions().Count()` | 节点数；含端点 + 真路口 + 半格交点 Junction |

`GameHUD` 也通过 `[Export] RoadConfig Config` 接同一份 `road_config.tres`，保证 HUD 鼠标格点显示与建造逻辑使用同一个 `CellSize`。

---

## 7. 方案对比

| | 单层（Junction + Road） | 双层（Junction + Segment + Road） ✅ |
|------|------|------|
| 100 格直路 | 100 Junction + 99 边 | 2 Junction + 1 Segment + 1 Road |
| 路口语义 | 每格都是路口 / 模糊 | 只在端点 / 交汇处 |
| 几何 vs 语义 | 混在一起 | 分离：Segment = 几何，Road = 玩家语义 |
| 拆除操作 | 逐段删 | RemoveSegment（拆边）/ RemoveRoad（拆整条玩家画线） |
| 寻路图大小 | 大 | 小（仅 Junction，边权 = Segment.TotalLength） |
| 多重边支持 | 难（key 冲突） | 原生（SegmentID 作 key） |
| 玩家"扩展已有路"语义 | 无 | 有（extendRoadID 占位） |
| 未来扩展 Bézier | 困难 | 容易（Segment.Waypoints → 控制点） |

---

## 8. 已确认决策

- [x] **双层模型**：Junction（路口/端点）+ Segment（几何边）+ Road（玩家画线聚合）
- [x] **网格 = 纯风格**：SnapToGrid() 纯函数，无数据结构，无 GridMap/CellType/GridCoord 等类型
- [x] 8 方向为风格约束，非技术限制
- [x] **事件按 Segment 粒度**：`SegmentAdded` / `SegmentRemoved`，无 Road 级事件
- [x] **共享配置 RoadConfig**：CellSize / RoadColor / RoadWidth / JunctionRadius 集中在一个 Resource
- [x] **AddRoad 返回 RoadID**：成功返回 RoadID（int），失败返回 -1
- [x] **Junction `_connections` 用 SegmentID 作 key**：支持劈分 + 多重边
- [x] **合并语义**：端点对齐合并时较小 RoadID 吸收较大 RoadID
- [x] **extendRoadID 参数已占位**，"玩家显式指定多 Segment 同 Road"未来再实现
- [x] 图 A* 寻路（非网格 A*）
- [x] 未来可平滑升级至 Bézier 曲线（对标 Cities: Skylines）
