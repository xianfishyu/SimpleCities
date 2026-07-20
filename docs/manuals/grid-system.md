# 网格系统设计

> 状态：当前实现说明 + 未来抽象设计 | 最后更新：2026-07-20
>
> **注意**：当前实现仍是 `GridSystem` + `Direction` + `DirectionUtil`，提供 8 方向行为并通过 `RoadConfig` 获取 CellSize。
> `IGridGeometry` 与 `Square8Grid` 是未来可替换网格的设计草案，不是现有类。
> 具体的数据结构（GraphNode / GraphEdge / RoadGroup / SpatialIndex）见 `road-system-v2-gen.md`。

---

## 1. 设计目标

- 定义**抽象的网格概念**：不绑定正方网格、8 方向、固定 CellSize
- 道路铺设基于网格约束（鼠标输入离散化），但**路网数据层是纯连续图**
- 支持未来替换为不同网格结构（六边形、三角、变尺寸等）而不改动数据层

### 1.1 分层职责

```
Layer          | 职责                        | 是否感知网格？
───────────────┼─────────────────────────────┼─────────────
RoadGraph      | 图存储（节点、边、拓扑操作） | 否 — 纯连续坐标
SpatialIndex   | 空间查询                    | 否 — 仅按距离
RoadBuilder    | 鼠标→拓扑操作转换            | 是 — 这是网格的"家"
GridSystem     | 网格数学（吸附、邻居枚举）   | 是 — 网格逻辑集中在此
```

---

## 2. 当前实现与未来抽象边界

当前运行时代码：

- `GridSystem` 是静态 helper，集中提供 `SnapToGrid()` 和 `IsSnapGrid()`。
- `GridSystem.Config` 由 `RoadSystem._Ready()` 注入，CellSize 来自 `RoadConfig`。
- `Direction` / `DirectionUtil` 负责 8 方向判定、位移和正交/对角长度。
- `RoadBuilder` 使用这些工具完成鼠标吸附、拖拽投影和半格起点规则。

未来设计目标仍是把网格几何抽象成可替换对象，但当前不要声称 `IGridGeometry` 或 `Square8Grid` 已经存在。

## 3. 抽象网格接口（未来设计）

### 3.1 IGridGeometry

未来所有网格结构建议实现的接口：

```csharp
/// <summary>
/// 网格几何抽象。定义"合法位置集合"和"位置间的关系"。
/// RoadBuilder 依赖此接口做鼠标吸附和方向投影。
/// RoadGraph 完全不依赖此接口。
/// </summary>
public interface IGridGeometry
{
    /// <summary>将任意世界坐标吸附到最近的合法格点。</summary>
    Vector2 Snap(Vector2 worldPos);

    /// <summary>判断一个位置是否为合法格点（容差比较）。</summary>
    bool IsGridPoint(Vector2 pos);

    /// <summary>从起点出发，沿最接近鼠标向量的方向，返回推荐方向和格数。</summary>
    GridProjection Project(Vector2 from, Vector2 mouseWorld);

    /// <summary>枚举从某格点出发的所有合法邻居方向。</summary>
    IEnumerable<GridStep> GetNeighbors(Vector2 from);

    /// <summary>计算两格点之间的移动代价（用于 A* 寻路启发函数）。</summary>
    float Cost(Vector2 from, Vector2 to);
}

public struct GridStep
{
    public Vector2 To;          // 邻居格点坐标
    public string Direction;    // 方向标签（如 "N", "NE"；可用于 UI 显示）
    public float Cost;          // 移动代价（欧氏距离或自定义）
}

public struct GridProjection
{
    public string Direction;    // 方向标签
    public int Cells;           // 沿该方向的格数（取整）
    public Vector2 EndPoint;    // 投影终点坐标（用于预览）
}
```

### 3.2 关键设计决策

| 决策 | 理由 |
|------|------|
| 接口不含 CellSize | 不同网格可能用不同方式定义"步长"（六边形有多个半径参数） |
| 返回方向是 `string` 而非枚举 | 不同网格的方向数量不同（正方 8 个、六边形 6 个、三角 12 个），字符串标签灵活且可读 |
| `Cost()` 独立于 `GetNeighbors()` | A* 寻路的启发函数可能需要不同于实际移动代价的估计值 |
| `Project()` 封装了方向选择逻辑 | 正方网格：投影到 8 方向选最长；六边形网格：投影到 6 方向选最长 |

---

## 4. 正方 8 方向网格（Square8Grid，未来设计）

这是未来可替换网格方案中的默认候选。当前项目的运行时代码还没有 `Square8Grid` 类，而是由 `GridSystem`、`Direction` 和 `DirectionUtil` 共同实现同等的 8 方向行为。

### 4.1 坐标系

```
World Position:  (x, y)   ∈ ℝ²    (Godot 2D，Y 轴向下)
Grid Point:      cellSize 的整数倍坐标

转换：c = Round(x / cellSize),  r = Round(y / cellSize)
格点 = (c × cellSize, r × cellSize)
```

### 4.2 8 方向位移表

```
方向     (dx, dy)    角度      步长
N        ( 0, -1)     0°      cellSize
NE       (+1, -1)    45°      cellSize × √2
E        (+1,  0)    90°      cellSize
SE       (+1, +1)   135°      cellSize × √2
S        ( 0, +1)   180°      cellSize
SW       (-1, +1)   225°      cellSize × √2
W        (-1,  0)   270°      cellSize
NW       (-1, -1)   315°      cellSize × √2
```

### 4.3 交叉口类型（渲染用）

当多条边交汇于同一节点时，按连接数分类：

| 连接数 | 类型 | 示例 |
|--------|------|------|
| 1 | 端点 (Endpoint) | 断头路 |
| 2（对向） | 直通 (Straight) | 一条直线穿过节点 |
| 2（非对向） | 弯道 (Curve) | L 形直角转弯 |
| 3 | T 型路口 | ┬ 形 |
| 4（全正交） | 十字路口 | ┼ 形 |
| 4（全对角） | X 型路口 | ✕ 形 |
| 5+ | 多岔路口 | 圆形枢纽 |

> 节点类型**不存储为状态**，而是从当前的边连接关系**按需计算**（连接数 + 方向几何判定）。

### 4.4 Square8Grid 实现要点（草案）

```csharp
public class Square8Grid : IGridGeometry
{
    public float CellSize { get; }

    public Vector2 Snap(Vector2 pos) =>
        new(Mathf.Round(pos.X / CellSize) * CellSize,
            Mathf.Round(pos.Y / CellSize) * CellSize);

    public bool IsGridPoint(Vector2 pos) =>
        Mathf.Abs(pos.X % CellSize) < 1e-3f &&
        Mathf.Abs(pos.Y % CellSize) < 1e-3f;

    public GridProjection Project(Vector2 from, Vector2 mouse)
    {
        // 将向量 (mouse - from) 投影到 8 个方向，选投影长度最大的
        // 格数 = 投影长度 / 该方向步长（取整）
    }

    public IEnumerable<GridStep> GetNeighbors(Vector2 from)
    {
        // 8 个方向各返回一个 GridStep
    }

    public float Cost(Vector2 from, Vector2 to)
    {
        float dx = Mathf.Abs(to.X - from.X);
        float dy = Mathf.Abs(to.Y - from.Y);
        float dMax = Mathf.Max(dx, dy);
        float dMin = Mathf.Min(dx, dy);
        return CellSize * (dMax - dMin) + CellSize * Mathf.Sqrt(2) * dMin;
        // Octile 距离
    }
}
```

**注意**：`Square8Grid` 是未来替代方案。当前不要删除 `GridSystem`、`Direction` 或 `DirectionUtil`，它们仍是运行时 8 方向行为的来源。

---

## 5. 备选网格方案（供未来实验）

以下方案无需现在实现，但未来的 `IGridGeometry` 接口应能容纳它们。

### 5.1 六边形网格（HexGrid）

```
    NW  NE
     ╲ ╱
   W ─ ● ─ E
     ╱ ╲
    SW  SE

特点：6 方向，每步距离相等（√3 × 半径），无"对角更快"的偏斜问题。
适用：更自然的城市扩展模式，常用于策略游戏。
```

实现 `HexGrid : IGridGeometry` 时需处理：
- 六边形的"格点"定义（点顶 hex vs 平顶 hex）
- `Snap()` 需要六边形坐标系的取整逻辑（轴向坐标或立方坐标）
- `Cost()` 始终返回常数（与方向无关）

### 5.2 正方 4 方向网格（Square4Grid）

```
      N
      │
  W ─ ● ─ E
      │
      S

特点：仅正交方向，无对角线。更严格的网格约束。
适用：曼哈顿风格城市（如纽约网格），交通模拟更简单。
```

直接复用 `Square8Grid` 的大部分逻辑，仅 `GetNeighbors()` 和 `Project()` 过滤到 4 方向。

### 5.3 自由角度网格（FreeAngleGrid）

```
任意角度拖拽，RoadBuilder 不限定方向。格点 = 鼠标位置的最近已有节点。
适用：有机生长的道路网络（如欧洲老城），但寻路更复杂。
```

这可能不需要 `Snap()`，而是依赖空间索引找最近节点。`Project()` 退化为"沿鼠标方向直接指向鼠标位置"。

### 5.4 混合网格

同一场景中不同区域使用不同网格。例如：
- 市中心：正方 4 方向（严格街区）
- 郊区：六边形（自然扩张）
- 工业区：自由角度

`RoadBuilder` 根据鼠标所在区域切换活跃的 `IGridGeometry` 实例。

---

## 6. 寻路系统

### 6.1 通用 A* 框架

寻路在 `RoadGraph`（纯图）上进行，不依赖网格：

```csharp
// 寻路器接收一个图 + 边权重函数，返回节点 ID 序列
public static class PathFinder
{
    public static List<int> FindPath(
        RoadGraph graph,
        int fromNodeID,
        int toNodeID,
        Func<GraphEdge, float> weightFunc  // 边权重（可含拥堵/限速修正）
    );
}
```

### 6.2 启发函数

启发函数可以根据活跃网格选择：

| 网格类型 | 启发函数 |
|----------|----------|
| 正方 8 向 | Octile 距离：`D × max(dx,dy) + (D√2 - D) × min(dx,dy)` |
| 正方 4 向 | 曼哈顿距离：`D × (dx + dy)` |
| 六边形 | 六边形距离：`max(dx, dy, dx+dy)`（立方坐标） |
| 自由角度 | 欧几里得距离：`√(dx² + dy²)` |

或者在 `IGridGeometry` 上增加一个方法：

```csharp
/// <summary>两位置间的启发式估计距离（用于 A*）。可大于实际代价，但不能低估。</summary>
float Heuristic(Vector2 from, Vector2 to);
```

---

## 7. 渲染方案

### 7.1 道路渲染

使用 Godot `Line2D` 节点（每边一个）。边上的点序列来自 `GraphEdge.GetFullPath()`：

```
NodeA.Position → waypoint[0] → waypoint[1] → ... → NodeB.Position
```

交叉口节点通过 `_Draw()` 绘制圆点（按连接数决定半径和颜色）。

### 7.2 网格背景

背景网格由 `MapBackground`（ShaderMaterial）渲染，与道路逻辑解耦。
网格背景的视觉效果（是否显示网格线、间距、颜色）由 Shader 参数控制，
**不受活跃 `IGridGeometry` 影响**（背景显示只是视觉辅助）。

### 7.3 道路样式

未来可按 `RoadType`（土路/普通/主干/高速）查表决定颜色和线宽。
当前 RoadRenderer 仍使用 RoadConfig 的基础颜色和线宽；样式映射扩展属于未来工作。

---

## 8. 已确认决策

- [x] 网格是 UI 输入层概念，数据层（RoadGraph）不感知网格
- [x] 当前默认行为：正方 8 方向，由 `GridSystem` + `Direction` + `DirectionUtil` 实现
- [ ] 网格实现 `IGridGeometry` 接口，支持替换
- [ ] 当前默认实现迁移为 `Square8Grid`（CellSize 仍由配置提供）
- [x] 地图地形由 Godot 编辑器手工制作

## 9. 待定问题

- [ ] 是否需要在游戏运行时切换网格类型？（混合网格场景）
- [ ] 六边形网格的视觉背景（Shader 需要支持六边形线）
- [ ] 网格切换是否影响已铺设道路的视觉？（如：正方网格道路在六边形背景下如何显示）
- [ ] 是否需要网格细分（SubCell）用于建筑内部布局？
