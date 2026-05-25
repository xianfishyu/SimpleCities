# 道路网络系统设计

> 状态：草案 | 最后更新：2026-05-26

---

## 1. 设计原则

- **网络模型**：道路 = 路口（Node）+ 路段（Edge）。**不存储逐格数据，不做 GridMap。**
- **8 方向**：路段方向限定为正交（N/S/E/W）+ 斜交（NE/NW/SE/SW）
- **网格仅用于对齐**：路口坐标 snap 到 `CellSize` 整数倍格点中心，对齐是纯函数，不产生数据结构
- 完全俯视（Top-Down），矢量极简渲染：线条 + 纯色填充，无纹理

---

## 2. 坐标对齐

### 2.1 网格对齐（纯函数）

```csharp
static Vector2 SnapToGrid(Vector2 worldPos, float cellSize)
    => new(
        Mathf.Floor(worldPos.X / cellSize) * cellSize + cellSize / 2,
        Mathf.Floor(worldPos.Y / cellSize) * cellSize + cellSize / 2
    );
```

路口位置 = 鼠标世界坐标 → snap → 得到格点中心。仅此而已，不创建 GridCoord 类型或 GridMap 存储。

### 2.2 8 方向位移表

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

## 3. 道路网络拓扑

### 3.1 网络模型

```
路网 = 路口 + 路段

  Node#0 ─── Edge#0 ─── Node#1 ─── Edge#1 ─── Node#2
                             │
                          Edge#2
                             │
                          Node#3
```

- **路口（Junction）**：道路交汇点，核心属性是位置 + 邻接路口列表
- **路段（Road）**：连接两个路口，有方向和长度

### 3.2 连通规则

```
路段 = (JunctionA, JunctionB) where 两节点在 8 方向上相邻
即 |Δx| = CellSize 或 0，|Δy| = CellSize 或 0，且 (Δx, Δy) ≠ (0, 0)
```

### 3.3 交叉口自动判定

一个路口根据其邻接方向的数量和空间分布，自动判定类型：

| 邻接边数 | 方向分布 | 类型 |
|----------|----------|------|
| 1 | — | Endpoint（端点） |
| 2 | 相邻（如 N+NE） | Curve（弯道） |
| 2 | 对向（如 N+S） | Straight（直通） |
| 3 | — | TJunction（T型路口） |
| 4 | 正交四方（N+S+E+W） | Cross（十字路口） |
| 4 | 斜交四方（NE+SE+SW+NW） | XCross（X型路口） |
| 5+ | — | MultiWay（多岔路口） |

---

## 4. 数据结构

### 4.1 RoadNetwork — 路网容器

```csharp
class RoadNetwork
{
    Dictionary<int, Junction> _junctions;        // id → 路口
    Dictionary<int, Road> _roads;                // id → 路段
    Dictionary<Vector2, int> _posToJunctionId;   // 格点位置 → 路口 id

    bool AddRoad(Vector2 from, Vector2 to);      // 铺路
    bool RemoveRoad(Vector2 from, Vector2 to);   // 拆路
    Junction? GetJunctionAt(Vector2 pos);        // 查询路口
    bool HasJunctionAt(Vector2 pos);
    IEnumerable<Road> GetAllRoads();
    IEnumerable<Junction> GetAllJunctions();

    event Action<Road> RoadAdded;                // 供渲染器订阅
    event Action<Road> RoadRemoved;
}
```

**AddRoad 内部逻辑**：
1. 两个端点 snap 到格点
2. 校验：必须在 8 方向相邻
3. 两端无路口 → 创建 Junction
4. 创建 Road，双向更新邻接表
5. 重算两端路口 `RecalculateType()`
6. 触发 `RoadAdded` 事件

**RemoveRoad 内部逻辑**：
1. 删除 Road
2. 从两端邻接表移除对方
3. 若某端无边 → 删除该 Junction
4. 重算剩余路口类型
5. 触发 `RoadRemoved` 事件

### 4.2 Junction — 路口

```csharp
class Junction
{
    int Id;
    Vector2 Position;           // 世界坐标（已对齐到格点中心）
    HashSet<int> NeighborIds;   // 相邻路口 ID
    JunctionType Type;          // 自动判定

    void RecalculateType();     // 遍历邻接方向，判定类型
}
```

### 4.3 Road — 路段

```csharp
class Road
{
    int Id;
    int FromJunctionId, ToJunctionId;
    Direction Dir;              // 8 方向之一
    float Length;               // 正交=CellSize, 斜交=CellSize×√2
}
```

### 4.4 Direction — 方向工具

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

`RoadRenderer` 订阅 `RoadNetwork.RoadAdded` / `RoadRemoved` → `QueueRedraw()`，不每帧轮询。

---

## 7. 与 GridMap 方案对比

| | GridMap 模式 | 网络模式 ✅ |
|------|------------|------------|
| 存储 | 每格一个类型 | 仅路口 + 路段 |
| 100 格路数据量 | ~100 条 GridMap + 图 | ~20 节点 + ~20 边 |
| 查询"是否道路" | `GridMap.GetCell(pos)` | `RoadNetwork.HasNodeAt(pos)` |
| 寻路 | 网格 A* | 图 A*（更快） |
| 数据层数 | 2 层需同步 | 1 层，数据即真相 |

---

## 8. 已确认决策

- [x] **网络模型**：Junction + Road + RoadNetwork，不存逐格数据
- [x] 8 方向路段
- [x] 路口坐标对齐格点（纯函数 snap，无 GridMap）
- [x] 交叉口类型根据邻接方向自动判定
- [x] 事件驱动渲染（RoadAdded / RoadRemoved）
- [x] 图 A* 寻路（非网格 A*）
- [ ] 是否需要网格细分（SubCell）用于建筑内部布局？
- [ ] 最小道路闭合区域面积？（一键填充时需要下限避免 1×1 孤立格）
