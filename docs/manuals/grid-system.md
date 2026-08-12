# 网格系统设计

> 状态：当前输入策略说明 + 未来寻路网格设计 | 最后更新：2026-08-04
>
> **注意**：当前铺路实现使用 `IRoadInputStrategy` 和 `RoadPathDraft`。玩家默认是 `SquareEightRoadInputStrategy`；三角形与六边形策略用于验证替换能力。
> `IGridGeometry`、邻居成本和寻路启发式仍是未来设计，不是当前输入 API。
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
RoadBuilder    | 输入生命周期、预览、提交      | 否 — 只消费策略草稿
InputStrategy  | 指针吸附、投影、路径草稿      | 是 — 输入网格规则在此
GridSystem     | 旧方格吸附工具                | 是 — 供其他 UI/调试使用
```

---

## 2. 当前实现与未来抽象边界

当前运行时代码：

- `IRoadInputStrategy` 定义 `InteractionRadius`、`SnapPointer` 和 `BuildDraft`。
- `RoadPathDraft` 防御性保存预览点和可选 `RoadPath`，`FromPolyline` 生成连续原生 line 段。
- `SquareEightRoadInputStrategy` 负责当前玩家默认的方格吸附、8 方向投影和半格对角约束。
- `TriangularThreeRoadInputStrategy` 把锚点放在三角单元中心，每个中心只有 3 个跨边邻居；相邻三角形交替方向，长路径呈确定锯齿。
- `HexSixRoadInputStrategy` 使用 pointy-top 六边形单元中心、轴向/立方坐标取整和 6 个等长方向。
- `RoadPlacementSession` 组合已固定策略草稿与可移动末端，保留完整预览点和原生几何段；连续拐点不复制任何网格规则。
- `RoadBuilder` 只负责输入事件、会话、预览、一次确认提交、取消和策略切换；RoadGraph 不引用任何策略类型。
- `GridSystem` 仍供调试等其他 UI 使用，但不再参与 RoadBuilder 的铺路生命周期。

未来 `IGridGeometry` 可以在输入策略之外增加邻居枚举、移动成本和寻路启发式；不要把该草案写成当前已实现接口。

## 3. 完整网格几何接口（未来设计）

### 3.1 IGridGeometry

若未来需要让网格参与寻路或区域规则，可在当前输入策略之外设计更宽的接口：

```csharp
/// <summary>
/// 网格几何抽象。定义"合法位置集合"和"位置间的关系"。
/// 未来可由寻路或区域规则消费；铺路输入仍通过 IRoadInputStrategy 适配。
/// RoadBuilder 和 RoadGraph 都不直接依赖此接口。
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
| 输入与寻路接口分开 | 当前铺路只需草稿；邻居标签、成本和启发式不应提前进入 `IRoadInputStrategy` |
| `Cost()` 独立于 `GetNeighbors()` | A* 寻路的启发函数可能需要不同于实际移动代价的估计值 |
| `Project()` 封装了方向选择逻辑 | 正方网格：投影到 8 方向选最长；六边形网格：投影到 6 方向选最长 |

---

## 4. 正方 8 方向输入策略（当前默认）

`SquareEightRoadInputStrategy` 是当前玩家默认实现。它从 `RoadConfig.CellSize` 构造，在策略内部使用 `Direction` 和 `DirectionUtil`，不把方格规则暴露给 RoadBuilder 或 RoadGraph。

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

### 4.4 当前实现边界

- 指针先吸附到 `cellSize` 整数倍坐标。
- 拖拽向量投影到 8 个方向，格数按该方向真实步长取整。
- 偏移起点只允许对角延伸，并用半格 anchor 保持后续点回到整格。
- 每个预览步转换为一个原生 `LineRoadGeometrySegment`。
- `GridSystem`、`Direction` 和 `DirectionUtil` 仍有其他调用方；本阶段只保证 RoadBuilder 不直接依赖它们。

---

## 5. 替换验证策略与未来方案

三角形与六边形实现是自动化验证策略，不是当前玩家可选择的产品模式。它们证明不同吸附和邻接规则无需修改 RoadBuilder 或 RoadGraph。

### 5.1 六边形单元中心（当前验证实现）

```
    NW  NE
     ╲ ╱
   W ─ ● ─ E
     ╱ ╲
    SW  SE

特点：`HexSixRoadInputStrategy` 使用 pointy-top 单元中心和轴向坐标；6 个方向步长相等。
```

`SnapPointer` 通过立方坐标误差最大的轴回调完成稳定取整；`BuildDraft` 选择投影最大的六方向并按等长步数生成 line 草稿。

### 5.2 三角形单元中心（当前验证实现）

`TriangularThreeRoadInputStrategy` 使用三角单元中心作为锚点。每个三角形通过三条边连接 3 个邻居；相邻单元朝向交替，因此长拖拽根据剩余指针方向逐步选择邻居，形成符合三角邻接的锯齿路径。

### 5.3 正方 4 方向网格（未来）

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

### 5.4 自由角度网格（未来）

```
任意角度拖拽，RoadBuilder 不限定方向。格点 = 鼠标位置的最近已有节点。
适用：有机生长的道路网络（如欧洲老城），但寻路更复杂。
```

这可能不需要 `Snap()`，而是依赖空间索引找最近节点。`Project()` 退化为"沿鼠标方向直接指向鼠标位置"。

### 5.5 混合网格（未来）

同一场景中不同区域使用不同网格。例如：
- 市中心：正方 4 方向（严格街区）
- 郊区：六边形（自然扩张）
- 工业区：自由角度

未来可以根据鼠标所在区域调用 `RoadBuilder.SetInputStrategy(...)`，但当前没有玩家运行时切换 UI。

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

`RoadRenderer` 使用 `RoadGeometryDisplaySampler` 从每条 `GraphEdge.GeometrySegments` 生成显示点列，再把全部 Edge 合并为一个抗锯齿 `ArrayMesh` ribbon。每个显示点共享左右边界，避免逐段圆形覆盖和接缝；端点与交叉节点统一写入一个圆形 shader `MultiMesh`，固定渲染子节点数不会随 Edge 数增长。

```
GeometrySegments → RoadGeometryDisplaySampler → cached display points
                 → ArrayMesh road ribbon + MultiMesh node markers
```

Edge 增删事件通过 `ScheduleStaticBatchRebuild` 在同一事件循环中合并，`GraphCleared` 同步完成一次全量重建。显示采样只服务渲染和高亮，不写回路网或存档；10k 基础规模与 100k 压测结果见 `../performance/road-rendering-v2-baseline.md`。

### 7.2 网格背景

背景网格由 `MapBackground`（ShaderMaterial）渲染，与道路逻辑解耦。
网格背景的视觉效果（是否显示网格线、间距、颜色）由 Shader 参数控制，
**不受活跃 `IRoadInputStrategy` 影响**（背景显示只是视觉辅助）。

### 7.3 道路样式

未来可按 `RoadType`（土路/普通/主干/高速）查表决定颜色和线宽。
当前 RoadRenderer 仍使用 RoadConfig 的基础颜色和线宽；样式映射扩展属于未来工作。

---

## 8. 已确认决策

- [x] 网格是 UI 输入层概念，数据层（RoadGraph）不感知网格
- [x] 当前默认行为：`SquareEightRoadInputStrategy` 提供正方 8 方向，CellSize 仍由配置提供
- [x] 铺路输入实现 `IRoadInputStrategy` 接口，可在不修改 RoadBuilder/RoadGraph 时替换
- [x] 三角形 3 邻接与六边形 6 邻接策略通过同一草稿、交叉拆分和存档契约
- [ ] 完整 `IGridGeometry` 邻居/成本/启发式接口仅在寻路或区域规则需要时再实现
- [x] 地图地形由 Godot 编辑器手工制作

## 9. 待定问题

- [ ] 是否需要在游戏运行时切换网格类型？（混合网格场景）
- [ ] 六边形网格的视觉背景（Shader 需要支持六边形线）
- [ ] 网格切换是否影响已铺设道路的视觉？（如：正方网格道路在六边形背景下如何显示）
- [ ] 是否需要网格细分（SubCell）用于建筑内部布局？
