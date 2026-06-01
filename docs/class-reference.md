# SimpleCities-LLM 类引用文档

> 最后更新：2026-06-01 | Godot 4.6 C# | .NET 10.0

---

## 目录

- [1. 主模块 (Core)](#1-主模块-core)
  - [MainCamera](#maincamera)
- [2. 网格模块 (Grid)](#2-网格模块-grid)
  - [GridSystem](#gridsystem)
  - [MapBackground](#mapbackground)
  - [MapTerrain.gdshader](#apterraingdshader)
- [3. 道路模块 (Road)](#3-道路模块-road)
  - [Direction / DirectionUtil](#direction--directionutil)
  - [Junction / JunctionType](#junction--junctiontype)
  - [Road](#road)
  - [RoadBuilder](#roadbuilder)
  - [RoadConfig](#roadconfig)
  - [RoadNetwork](#roadnetwork)
  - [RoadRenderer](#roadrenderer)
  - [RoadSystem](#roadsystem)
  - [Segment](#segment)
- [4. 工具模块 (Tools)](#4-工具模块-tools)
  - [ToolManager](#toolmanager)
  - [ToolType](#tooltype)
- [5. UI 模块 (UI)](#5-ui-模块-ui)
  - [GameHUD](#gamehud)
  - [UIHelpers](#uihelpers)
  - [UIManager](#uimanager)

---

## 1. 主模块 (Core)

### MainCamera

**文件**: `Scripts/MainCamera.cs`
**继承**: `Camera2D`
**命名空间**: 无（全局）

2D 相机控制器，提供键盘平移、鼠标拖动和滚轮缩放功能。以静态单例模式供全局访问。

#### 导出属性

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `defaultScale` | `float` | `1.0` | 当前目标缩放比例 |
| `scaleFactor` | `float` | `0.125` | 每次滚轮缩放的比例因子 |
| `minScale` | `float` | `0.125` | 最小缩放倍数（最大拉远） |
| `maxScale` | `float` | `4.0` | 最大缩放倍数（最大拉近） |
| `keyMoveFactor` | `float` | `10.0` | 键盘移动速度系数 |
| `moveSpeed` | `float` | `1.25` | 键盘移动基础速度 |

#### 静态属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Instance` | `MainCamera` | 全局单例引用 |

#### 方法

| 方法 | 签名 | 说明 |
|------|------|------|
| `_Ready()` | `override void` | 初始化单例，记录初始位置 |
| `_Process(double delta)` | `override void` | 逐帧：键盘移动插值、缩放插值、鼠标拖动更新 |
| `_Input(InputEvent @event)` | `override void` | 处理 WASD 移动、鼠标滚轮缩放、中键拖动 |

#### 输入映射

| 输入动作 | 按键 | 效果 |
|----------|------|------|
| `KeyBoard_MoveUp` | W | 向上平移 |
| `KeyBoard_MoveDown` | S | 向下平移 |
| `KeyBoard_MoveLeft` | A | 向左平移 |
| `KeyBoard_MoveRight` | D | 向右平移 |
| 鼠标滚轮上 | — | 放大（defaultScale 减小） |
| 鼠标滚轮下 | — | 缩小（defaultScale 增大） |
| 鼠标中键拖动 | — | 拖拽平移视图 |

#### 技术细节

- 所有位移和缩放均使用 `Mathf.Lerp` 逐帧平滑插值（系数 0.1）
- `_Process` 中先更新位置再更新缩放（`KeyPosUpdate` → `ScaleUpdate` → `MousePosUpdate`）
- `_Input` 中非中键按下时使用 `Input.GetVector()` 读取 WASD 输入
- 缩放范围由 `minScale` / `maxScale` 限制，`Mathf.Min/Max` 钳制

---

## 2. 网格模块 (Grid)

网格模块集中管理地图网格的数学逻辑（GridSystem）和视觉渲染（MapBackground / Shader）。

### GridSystem

**文件**: `Scripts/Grid/GridSystem.cs`
**类型**: 静态类

集中式网格数学工具：替换原先各处传递 cellSize 参数的分散模式，统一管理 SnapToGrid / IsSnapGrid 逻辑。由 `RoadSystem._Ready()` 注入 `RoadConfig` 完成初始化。

#### 静态属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Config` | `RoadConfig` | 共享配置资源（在 RoadSystem._Ready 中注入），提供 CellSize 等参数 |
| `CellSize` | `float` | 当前网格单元尺寸。未初始化时返回默认 64 |

#### 静态方法

| 方法 | 签名 | 说明 |
|------|------|------|
| `SnapToGrid(Vector2 pos)` | `Vector2` | 将世界坐标对齐到最近的网格原点（CellSize 整数倍） |
| `IsSnapGrid(Vector2 pos)` | `bool` | 位置是否落在标准 snap 格点上（CellSize 整数倍，容差 1e-3） |

#### 使用场景

- `SnapToGrid` — RoadBuilder 拖拽起点吸附、RoadNetwork 端点定位
- `IsSnapGrid` — 判断起点是否半格点（决定拖拽方向限制和锚定行为）；Junction 是否进 `_posToJunctionID` 字典

---

### MapBackground

**文件**: `Scripts/Grid/MapBackground.cs`
**继承**: `CanvasLayer`
**命名空间**: 无（全局）

地图背景渲染器 — 通过 ShaderMaterial 渲染暗色底 + 三层网格线（主网格 / 次网格 / 点网格）。网格偏移对齐道路 CellSize 中心点。静态单例模式。

#### 导出属性

##### 背景设置

| 属性 | 类型 | 默认值 |
|------|------|--------|
| `BackgroundColor` | `Color` | `(0.118, 0.118, 0.118)` |

##### 网格设置

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `GridOffset` | `Vector2` | `(50, 50)` | 网格偏移，默认对齐道路 CellSize / 2 中心点 |
| `MajorGridSize` | `float` | `500` | 主网格线间距 |
| `MainLineWidth` | `float` | `1.5` | 主网格线宽 |
| `MajorGridColor` | `Color` | `(0.25, 0.25, 0.25)` | 主网格线颜色 |
| `MinorGridSize` | `float` | `100` | 次网格线间距 |
| `LineWidth` | `float` | `0.5` | 次网格线宽 |
| `MinorGridColor` | `Color` | `(0.18, 0.18, 0.18)` | 次网格线颜色 |
| `DotGridSize` | `float` | `10` | 点网格间距 |
| `DotRadius` | `float` | `0.5` | 点网格半径 |
| `DotColor` | `Color` | `(0.20, 0.20, 0.20)` | 点网格颜色 |

##### 显示设置

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `ShowGrid` | `bool` | `true` | 总网格显示开关 |
| `ShowMainGrid` | `bool` | `true` | 主网格显示 |
| `ShowMinorGrid` | `bool` | `true` | 次网格显示 |
| `ShowDotGrid` | `bool` | `true` | 点网格显示 |

##### 节点引用

| 属性 | 类型 | 说明 |
|------|------|------|
| `Display` | `ColorRect` | 全屏背景矩形节点 |

#### 静态属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Instance` | `MapBackground` | 全局单例引用 |

#### 方法

| 方法 | 签名 | 说明 |
|------|------|------|
| `_Ready()` | `override void` | 初始化 Display 全屏锚点、获取 ShaderMaterial、建立单例 |
| `_Process(double delta)` | `override void` | 逐帧更新 Shader 参数：相机位置/缩放、视口尺寸、颜色、各层网格开关 |
| `ToggleGrid()` | `void` | 切换 `ShowGrid`（全部网格开关） |

#### 渲染管线

```
_Process 每帧 → ShaderMaterial.SetShaderParameter(...)
  → MapTerrain.gdshader fragment()
    → 计算世界坐标 → 偏移 → 绘制点网格 → 次网格线 → 主网格线
```

#### 依赖

- `MainCamera.Instance` — 获取相机位置和缩放
- `Shaders/MapTerrain.gdshader` — 网格渲染着色器

---


### MapTerrain.gdshader

**文件**: `Shaders/MapTerrain.gdshader`
**类型**: `canvas_item`（渲染模式：`unshaded`）

背景网格着色器，在 GPU 端绘制三层网格叠加。由 `MapBackground` 每帧通过 `SetShaderParameter` 传入参数。

#### Uniform 参数

##### 背景

| 参数 | 类型 | 说明 |
|------|------|------|
| `background_color` | `vec3` | 背景色 |

##### 网格偏移

| 参数 | 类型 | 说明 |
|------|------|------|
| `grid_offset` | `vec2` | 全局网格偏移（对齐 CellSize 中心） |

##### 主网格

| 参数 | 类型 | 说明 |
|------|------|------|
| `major_grid_size` | `float` | 主网格间距 |
| `major_line_width` | `float` | 主网格线宽 |
| `major_grid_color` | `vec3` | 主网格颜色 |
| `show_major_grid` | `bool` | 主网格开关 |

##### 次网格

| 参数 | 类型 | 说明 |
|------|------|------|
| `minor_grid_size` | `float` | 次网格间距 |
| `minor_line_width` | `float` | 次网格线宽 |
| `minor_grid_color` | `vec3` | 次网格颜色 |
| `show_minor_grid` | `bool` | 次网格开关 |

##### 点网格

| 参数 | 类型 | 说明 |
|------|------|------|
| `dot_grid_size` | `float` | 点间距 |
| `dot_radius` | `float` | 点半径 |
| `dot_color` | `vec3` | 点颜色 |
| `show_dot_grid` | `bool` | 点网格开关 |

##### 相机

| 参数 | 类型 | 说明 |
|------|------|------|
| `camera_pos` | `vec2` | 相机世界坐标 |
| `camera_zoom` | `float` | 相机缩放（Zoom.x） |
| `viewport_size` | `vec2` | 视口像素尺寸 |

#### 绘制顺序

1. `final_color = background_color`
2. 点网格：`mod` 定位 → `smoothstep` 抗锯齿 → `mix` 叠加。若网格线重合处抑制点渲染（避免双层叠加）
3. 次网格线：`mod` 距离 → `smoothstep` 抗锯齿 → `mix` 叠加
4. 主网格线：同理，覆盖在次网格之上

#### 技术细节

- 使用 `fwidth(world_pos)` 计算像素梯度用于 `smoothstep` 抗锯齿
- 通过 `viewport_size / camera_zoom` 将 UV 转换为世界坐标
- 每层网格均支持独立开关

---

## 3. 道路模块 (Road)

道路系统是 SimpleCities 的核心子系统，采用**图数据结构**管理路网拓扑。

### 架构层次

```
RoadSystem (Node2D, 根节点)
├── RoadBuilder (Node2D, 用户输入)
├── RoadRenderer (Node2D, 视觉渲染)
│   └── JunctionLayer (Node2D, 交叉口绘制)
└── RoadNetwork (纯数据, 图结构)
    ├── Junction[]      (顶点)
    ├── Segment[]        (边)
    ├── Road[]           (逻辑分组)
    └── 索引字典          (空间查找)
```

---

### Direction / DirectionUtil

**文件**: `Scripts/Road/Direction.cs`
**类型**: 枚举 + 静态工具类

8 方向枚举及位移计算工具。

#### Direction 枚举

| 值 | 位移 | 说明 |
|----|------|------|
| `N` | `(0, -1)` | 正交 — 上 |
| `NE` | `(1, -1)` | 对角 — 右上 |
| `E` | `(1, 0)` | 正交 — 右 |
| `SE` | `(1, 1)` | 对角 — 右下 |
| `S` | `(0, 1)` | 正交 — 下 |
| `SW` | `(-1, 1)` | 对角 — 左下 |
| `W` | `(-1, 0)` | 正交 — 左 |
| `NW` | `(-1, -1)` | 对角 — 左上 |

#### DirectionUtil 静态方法

| 方法 | 签名 | 说明 |
|------|------|------|
| `GetDisplacement(Direction d)` | `Vector2I` | 获取方向的单位位移向量 |
| `FromDisplacement(Vector2 from, Vector2 to, float cellSize)` | `Direction?` | 从位移反查方向（要求位移长度 = cellSize） |
| `FromDisplacementAnyLength(Vector2 from, Vector2 to)` | `Direction?` | 从位移反查方向（允许任意长度，余弦匹配，cos 阈值 0.999）。核心用途：半格步长场景，端点→waypoint 距离 < cellSize 但仍需判定 8 方向合法性 |
| `IsOrthogonal(Direction d)` | `bool` | 是否为正交方向（N/E/S/W） |
| `IsDiagonal(Direction d)` | `bool` | 是否为对角方向（NE/SE/SW/NW） |
| `Length(Direction d, float cellSize)` | `float` | 该方向单位步长：正交 = cellSize，对角 = cellSize × √2 |

#### 静态属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `All` | `Direction[]` | 全部 8 方向数组（顺序：N, NE, E, SE, S, SW, W, NW） |

#### 使用场景

- `FromDisplacement` — 严格单位距离校验，用于 waypoint 序列的 8 方向合法性。注意：Godot 的 `Mathf.RoundToInt` 使用 Banker's rounding（0.5 → 0），位移恰好是 cellSize 的 0.5 倍时会错误舍入，导致方向判定失败。半格步长场景必须改用 `FromDisplacementAnyLength`
- `FromDisplacementAnyLength` — 半格 Junction 场景：端点位置不一定在 snap 格点上，首/尾段距离小于 cellSize 时仍正确判定 8 方向。也是 AddRoad 首步校验的首选方法

---

### Junction / JunctionType

**文件**: `Scripts/Road/Junction.cs`
**类型**: 纯数据类 + 枚举

路网图中的顶点 — 代表路口的拓扑节点。不仅包含标准格点上的路口，也支持"半格 Junction"（两段非格点交叉处）。

#### JunctionType 枚举

| 值 | 连接数 | 说明 |
|----|--------|------|
| `Endpoint` | 0~1 | 端点 / 孤立点 |
| `Straight` | 2 | 对向直通（两段方向相反，已合并降级为 waypoint） |
| `Curve` | 2 | 非对向转弯（L 形直角弯等，保留为真路口） |
| `TJunction` | 3 | T 字路口 |
| `Cross` | 4 | 正交十字路口（包含 4 个正交方向） |
| `XCross` | 4 | 对角十字路口（包含 4 个对角方向） |
| `MultiWay` | 5+ | 多路交叉（5 段及以上） |

#### Junction 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `ID` | `int` | 全局唯一标识 |
| `Position` | `Vector2` | 世界坐标。可能是整数格点（进 `_posToJunctionID` 字典）或半格位置（非整数格点坐标，不在字典中，仅通过 ID / 几何匹配访问） |
| `Type` | `JunctionType` | 当前路口类型（由连接数 + 方向自动判定） |
| `ConnectionCount` | `int` | 连接到该 Junction 的 Segment 总数 |
| `ConnectedSegmentIDs` | `IEnumerable<int>` | 所有连接 Segment 的 ID（不重复） |
| `NeighborJunctionIDs` | `IReadOnlyList<int>` | 所有邻居 Junction ID（多重边可出现重复） |
| `IncomingDirections` | `IReadOnlyList<Direction>` | 所有 Segment 在该处的入向 |

#### Junction 方法

| 方法 | 签名 | 说明 |
|------|------|------|
| `Junction(int id, Vector2 position)` | 构造 | 创建 Junction 节点 |
| `AddSegmentConnection(...)` | `void` | 添加一段 Segment 连接，自动重算类型 |
| `RemoveSegmentConnection(int segmentID)` | `void` | 移除一段连接，自动重算类型 |
| `RecalculateType()` | `void` | 根据当前连接数/方向重算 `Type` |

#### 类型判定算法

```
连入数 → 判定:
  0~1 → Endpoint
  2   → 两向位移和=0 → Straight (对向) / 否则 → Curve (转弯)
  3   → TJunction
  4   → 全正交 → Cross / 全对角 → XCross / 混合 → MultiWay
  5+  → MultiWay
```

---

### Road

**文件**: `Scripts/Road/Road.cs`
**类型**: 纯数据类

逻辑路：玩家一次拖拽产生的 Segment 集合（1..N 个 Segment）。同一条 Road 共享路名、车道数、限速等属性（未来扩展）。劈分和不连续修复时 Road 会自动调整。

#### 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `ID` | `int` | 全局唯一标识 |
| `SegmentIDs` | `IReadOnlyCollection<int>` | 当前包含的所有 Segment ID |
| `SegmentCount` | `int` | Segment 数量 |
| `IsEmpty` | `bool` | 是否为空（没有 Segment） |

#### 方法

| 方法 | 签名 | 说明 |
|------|------|------|
| `Road(int id)` | 构造 | 创建逻辑路 |
| `AddSegment(int segmentID)` | `void` | 添加一个 Segment |
| `RemoveSegment(int segmentID)` | `void` | 移除一个 Segment |
| `ContainsSegment(int segmentID)` | `bool` | 是否包含指定 Segment |

#### 生命周期

1. 创建：每次 `RoadBuilder` 拖拽提交 → `RoadNetwork.AddRoad()` 创建新 Road
2. 劈分：路径被其他 Road 交叉劈开 → 新 Segment 继承原 RoadID
3. 合并：两个对向直通 Segment 合并 → 较小 RoadID 吸收较大 RoadID
4. 拆除中间段 → `SplitRoadIntoConnectedComponents()` 将其拆为多个连通分量
5. Road 变空 → 自动清理

---

### RoadBuilder

**文件**: `Scripts/Road/RoadBuilder.cs`
**继承**: `Node2D`
**命名空间**: 无（全局）

道路建造/拆除工具 — 处理用户鼠标输入，调用 `RoadNetwork` 的 API 进行铺路和拆除操作。

#### 导出属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Config` | `RoadConfig` | 共享路网配置（CellSize、颜色等） |

#### 拖拽铺路逻辑

```
按下左键 → BeginDrag():
  1. 记录鼠标世界坐标
  2. SnapToGrid 到格点 → 若该格点无 Segment，调用 FindNearestRoadPoint() 半格回退
     （在 CellSize × 0.8 半径内搜索最近 waypoint 或 Junction，用于从已有路网点接续铺路）
  3. 初始化 _isDragging=true, _currentLength=0
  4. IsHalfGridStart 由 GridSystem.IsSnapGrid(_dragStartPos) 反值确定

拖拽中 → UpdateProjection() 每帧:
  1. 计算鼠标向量 (mouse - start)
  2. 投影到可能的 8 方向 → 选最长投影的方向
     （半格起点时仅允许对角方向 NE/SE/SW/NW，正交方向过滤掉）
  3. 投影长度 ÷ 该方向步长 → 格数 (_currentLength)
  4. 预览终点：半格起点锚定到反方向整格 + 格数；否则从起点直接计算

释放左键 → EndDragAndCommit():
  1. IsHalfGridStart=true → anchor = 起点沿当前方向反移 CellSize/2（锚到整格）
  2. waypoints 和终点全部从 anchor 整数倍计算（保证整车路落在整格上）
  3. 最终方向/格数确认（≥1 格才提交）
  4. 构建 waypoints 数组（起点→终点之间的所有中间格点）
  5. 调用 _network.AddRoad(start, end, waypoints, cellSize)
  6. 清除预览
```

#### 方法

| 方法 | 签名 | 说明 |
|------|------|------|
| `SetNetwork(RoadNetwork network)` | `void` | 注入 RoadNetwork 引用 |
| `HandlePlaceInput(InputEvent @event)` | `void` | 处理铺路工具的鼠标输入（左键按/放） |
| `HandleRemoveInput(InputEvent @event)` | `void` | 处理拆除工具的鼠标输入（点击 Segment 任一格点即拆除该段，支持整格与半格） |
| `SetRemoveHoverActive(bool active)` | `void` | 开关拆除工具悬停高亮 |
| `CancelPlaceDrag()` | `void` | 取消进行中的拖拽（工具切换时调用） |
| `UpdateProjection()` | `private void` | 拖拽中每帧更新方向和长度（半格起点仅允许对角方向） |
| `BeginDrag()` | `private void` | 开始拖拽：snap + FindNearestRoadPoint 半格回退 |
| `EndDragAndCommit()` | `private void` | 结束拖拽并提交路网（半格起点锚定到整格） |
| `FindNearestRoadPoint(Vector2 mousePos)` | `private (Vector2 pos, int segmentID)?` | 几何 O(n) 搜索所有 Segment 的 waypoint 和 Junction，找距离鼠标最近的路网点（搜索半径 = CellSize × 0.8） |
| `ComputeEndPos(Direction dir, int cells)` | `private Vector2` | 计算终点坐标 |
| `ClearPreview()` | `private void` | 清除预览虚线 |
| `IsHalfGridStart` | `bool` (属性) | 起点是否位于半格位置（由 `GridSystem.IsSnapGrid` 反值确定） |

#### 拆除工具悬停高亮

1. `SetRemoveHoverActive(true)` — 切入拆除工具时激活
2. 每帧 `UpdateRemoveHover()` — snap 到格点查 Segment，若无结果则 `FindNearestRoadPoint` 半格回退；找到则设 `HoveredSegmentID`
3. `RoadRenderer` 读取 `HoveredSegmentID` 绘制高亮
4. `SetRemoveHoverActive(false)` — 切出时清除

#### 拆除语义

- 点击 Segment 的任一 waypoint（不仅是端点）即可拆除该整段 Segment
- 如需拆除整条 Road（所有 Segment），使用 `RoadNetwork.RemoveRoad(roadID)`

#### 依赖

- `RoadNetwork` — 路网数据操作
- `RoadRenderer` — 预览虚线 + 悬停高亮
- `RoadConfig` — CellSize 等配置参数

---

### RoadConfig

**文件**: `Scripts/Road/RoadConfig.cs`
**继承**: `Resource`
**特性**: `[GlobalClass]`

共享配置资源，通过 Godot `.tres` 文件统一管理路网的尺寸和渲染参数。由 `RoadBuilder` / `RoadRenderer` / `RoadSystem` 共同引用，避免多处导出属性不一致。

#### 对应资源文件

`Scenes/road_config.tres`

#### 导出属性

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `CellSize` | `float` | `64` | 网格单元尺寸（像素）。所有端点/waypoint 对齐 cell 中心 |
| `RoadColor` | `Color` | `#37474F` | 道路颜色 |
| `RoadWidth` | `float` | `12` | 道路线宽（像素） |
| `JunctionRadius` | `float` | `10` | 真路口（≥2 连接且非对向）圆点半径 |
| `JunctionColor` | `Color` | `#FFC107` | 真路口圆点颜色（偏黄） |
| `EndpointRadius` | `float` | `6` | 端点（连接数=1）圆点半径。设 0 关闭端点显示 |
| `EndpointColor` | `Color` | `#90A4AE` | 端点圆点颜色（偏灰） |
| `HoverHighlightColor` | `Color` | `(1, 0.8, 0.2, 0.6)` | 拆除工具悬停高亮色 |
| `HoverHighlightWidth` | `float` | `18` | 拆除工具悬停高亮线宽 |

---

### RoadNetwork

**文件**: `Scripts/Road/RoadNetwork.cs`
**类型**: 纯数据类（非 Godot 节点）

路网图核心数据结构 — 管理 Junctions（顶点）/ Segments（边）/ Roads（逻辑分组）的增删改查。是整个道路子系统的数据中枢。

#### 内部数据结构

| 字段 | 类型 | 说明 |
|------|------|------|
| `_junctions` | `Dictionary<int, Junction>` | Junction ID → Junction |
| `_segments` | `Dictionary<int, Segment>` | Segment ID → Segment |
| `_roads` | `Dictionary<int, Road>` | Road ID → Road |
| `_posToJunctionID` | `Dictionary<Vector2, int>` | 格点位置 → Junction ID（仅整数格点；半格 Junction 不在此字典中） |
| `_posToSegmentID` | `Dictionary<Vector2, int>` | 格点位置 → Segment ID（用于点击拆除） |
| `_inMergeOperation` | `bool` | 合并操作守卫标志，防止 TryMergeAtJunction 内部 RemoveSegment 递归触发级联合并 |

#### 事件

| 事件 | 签名 | 说明 |
|------|------|------|
| `SegmentAdded` | `Action<Segment>` | 新 Segment 添加后触发 |
| `SegmentRemoved` | `Action<Segment>` | Segment 移除后触发 |

#### 公共方法

| 方法 | 签名 | 返回值 | 说明 |
|------|------|--------|------|
| `AddRoad(...)` | `(Vector2 from, Vector2 to, Vector2[] waypoints, float cellSize, int? extendRoadID)` | `int` | 铺设一条道路，返回 RoadID（失败返回 -1） |
| `RemoveSegment(int segmentID)` | `bool` | `bool` | 拆除单段 Segment。自动清理孤立 Junction、调整 Road |
| `RemoveRoad(int roadID)` | `bool` | `bool` | 拆除整条 Road 的所有 Segment |
| `FindSegmentAt(Vector2 pos)` | `int` | `int` | 按位置反查 Segment ID（-1 表示未找到） |
| `GetJunctionAt(Vector2 pos)` | `Junction?` | `Junction?` | 按位置查 Junction |
| `HasJunctionAt(Vector2 pos)` | `bool` | `bool` | 该位置是否有 Junction |
| `GetJunction(int id)` | `Junction?` | `Junction?` | 按 ID 查 Junction |
| `GetSegment(int id)` | `Segment?` | `Segment?` | 按 ID 查 Segment |
| `GetRoad(int id)` | `Road?` | `Road?` | 按 ID 查 Road |
| `GetAllSegments()` | `IEnumerable<Segment>` | — | 遍历所有 Segment |
| `GetAllRoads()` | `IEnumerable<Road>` | — | 遍历所有 Road |
| `GetAllJunctions()` | `IEnumerable<Junction>` | — | 遍历所有 Junction |
| `SnapToGrid(Vector2 pos, float cellSize)` | `static Vector2` | — | 将坐标 snap 到格点中心 |

#### AddRoad 处理流程

```
0. 半格处理: from/to 若已在路网上（IsOnRoadPoint），skipSnap=true 避免 snap 破坏 8 方向合法性
1. 入参校验: 自环拒绝、FromDisplacementAnyLength 8 方向校验（兼容半格步长）、内部重复格点
2. 完全重叠预检: IsPathFullyCovered（子线段区间并集判定）— 路径已存在则拒绝
3. X 形交叉处理 (ResolveInteriorCrossings):
   a. 收集新路径每段与现有 Segment 的几何内部交点
   b. 在交点处 SplitSegmentAtPosition 切开旧 Segment（交点可为非格点的"半格 Junction"）
   c. 将交点锚点按沿 path 距离插入新路径
4. 共线重叠跳过: IsApproachColinearWithSegment 判定 — 新路接近方向与已有 Segment 延伸方向一致则跳过劈分
5. 端点劈分: from/to 若落在已有 Segment 中段（非已有 Junction），用 FindSegmentAtIncludingHalfGrid / IsAnyJunctionAt 查，调 SplitSegmentAtWaypoint 切开
6. 按 path 上所有去重 Junction 位置切分新路 (splitIdx)
7. 拓扑去重: 共线端点重叠不生成冗余 Segment
8. 生成各段 Segment (归属同一新 Road)
9. 合并降级: 接入前连接数 =1、接入后 =2 且对向直通 → TryMergeAtJunction
```

#### 合并降级 (TryMergeAtJunction)

条件全部满足时才合并：
- Junction 连接数 == 2
- 两段方向互为反向（dispA + dispB == 0）—— 即对向直通（使用 departure direction: junction→neighbor，非 approach direction）
- 合并后整路 8 方向连续（首尾段用 FromDisplacementAnyLength 兼容半格）
- 合并路 RoadID 归并（较小 ID 吸收较大 ID）
- 安全护栏：拒绝自环 Segment、多重边环路、非对向转弯 Curve

`_inMergeOperation` 标志：合并期间 RemoveSegment 内部不再触发末尾合并降级，防止递归级联误并。

#### 拆除后的 Road 连通分量修复 (SplitRoadIntoConnectedComponents)

当删除中间 Segment 后，Road 可能分裂为多个不连通子集：
- BFS 遍历 Road 内各 Segment 通过共享 Junction 的邻接关系
- 保留第一个连通分量挂原 RoadID
- 其余分量各分配新 RoadID

#### 内部核心方法

##### FindSegmentAtIncludingHalfGrid
按位置反查 Segment：先查 `_posToSegmentID` 字典（整格命中），无结果则几何 O(n) 扫描所有 Segment 的 waypoint，再扫描 Junction 的 ConnectedSegmentIDs。用于半格端点劈分场景。

##### IsAnyJunctionAt
判断某位置是否有 Junction：先查 `_posToJunctionID` 字典（整格命中），无结果则几何 O(n) 扫描所有 Junction 位置。半格 Junction 不在 `_posToJunctionID` 字典中，仅通过几何匹配命中。

##### SplitSegmentAtPosition
在 Segment 几何线段的任意位置劈开（不必是 waypoint）。找到 splitPos 落在哪条子线段（fromJunction → wp[0] → ... → toJunction）的内部，拆原 Segment 为两段，连接处为 splitPos（可能是非格点的半格 Junction）。用于 X 形交叉处理。

##### SplitSegmentAtWaypoint
在指定 waypoint 位置劈开 Segment（共格点劈分场景）：原 Segment 删除，新建两段 Segment 继承原 RoadID。

##### IsPathFullyCovered
路径完全覆盖预检：将路径每对相邻格点与现有 Segment 的几何子线段并集做区间合并，若全部被覆盖则拒绝（避免重复铺路）。不要求某条 Segment 精确覆盖——X 交叉切开后的多段拼起来仍算覆盖。

##### IsApproachColinearWithSegment
判断新路从 approachPos 接近 targetPos 的方向是否与已有 Segment 在 targetPos 处的延伸方向共线。用于跳过共线重叠场景中冗余的 Segment 劈分。

##### MaybeReindexJunctionInPosDict
RemoveSegment 后修复 `_posToSegmentID` 索引：当 Junction 被多个 Segment 共享时，删除一个 Segment 可能清空该格点的字典条目。此方法从 Junction 的 ConnectedSegmentIDs 取一条存活 Segment 补回。半格 Junction 不处理（IsSnapGrid == false）。

---

### RoadRenderer

**文件**: `Scripts/Road/RoadRenderer.cs`
**继承**: `Node2D`
**命名空间**: 无（全局）

道路视觉渲染器 — 监听 `RoadNetwork` 的 Segment 增删事件，同步创建/销毁 `Line2D` 节点。同时负责交叉口圆点绘制和施工预览。

#### 导出属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Config` | `RoadConfig` | 共享路网配置（颜色、线宽等） |

#### 公共属性（供 RoadBuilder 设置）

| 属性 | 类型 | 说明 |
|------|------|------|
| `PreviewFrom` | `Vector2?` | 施工预览起点 |
| `PreviewTo` | `Vector2?` | 施工预览终点 |
| `HoveredSegmentID` | `int?` | 拆除工具悬停的 Segment ID |

#### 方法

| 方法 | 签名 | 说明 |
|------|------|------|
| `SetNetwork(RoadNetwork network)` | `void` | 注入 RoadNetwork，绑定 SegmentAdded/Removed 事件 |
| `OnSegmentAdded(Segment seg)` | `private void` | 创建 Line2D 节点并插入 JunctionLayer 之前 |
| `OnSegmentRemoved(Segment seg)` | `private void` | 回收 Line2D 节点 |
| `OnDrawJunctions()` | `private void` | 绘制交叉口端点圆点（JunctionLayer._Draw） |
| `_Draw()` | `override void` | 绘制：悬停高亮 + 施工预览虚线 |
| `DrawDashedLine(...)` | `private void` | 虚线绘制工具 |

#### 渲染层级

```
RoadRenderer
├── Line2D (Segment)          ← 道路线段
├── Line2D (Segment)          ← ...
├── JunctionLayer (Node2D)    ← 交叉口圆点 (DrawCircle)
└── 施工预览虚线              ← _Draw() 方法绘制
```

#### 交叉口绘制规则

| 连接数 | 渲染 |
|--------|------|
| ≥ 2 | `JunctionRadius` 圆点，`JunctionColor`（包含 Curve / TJunction / Cross / XCross / MultiWay） |
| = 1 | `EndpointRadius` 圆点，`EndpointColor`（仅当 EndpointRadius > 0） |
| = 0 | 不渲染 |

---

### RoadSystem

**文件**: `Scripts/Road/RoadSystem.cs`
**继承**: `Node2D`
**命名空间**: 无（全局）

路网系统根节点 — 负责创建 `RoadNetwork` 实例并注入给子节点 `RoadRenderer` 和 `RoadBuilder`。静态单例模式。

#### 静态属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Instance` | `RoadSystem` | 全局单例引用 |

#### 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Network` | `RoadNetwork` | 持有的路网数据实例 |

#### 方法

| 方法 | 签名 | 说明 |
|------|------|------|
| `_Ready()` | `override void` | 创建 RoadNetwork，注入 RoadRenderer、RoadBuilder |

#### 场景树依赖

```
RoadSystem (_Ready):
  GetNode<RoadRenderer>("RoadRenderer") → renderer.SetNetwork(Network)
  GetNode<RoadBuilder>("RoadBuilder")   → builder.SetNetwork(Network)
```

---

### Segment

**文件**: `Scripts/Road/Segment.cs`
**类型**: 纯数据类

几何边：相邻两个 Junction 之间的一段路，含端点 + 中间 waypoints + 总长度。归属于一条 Road。

#### 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `ID` | `int` | 全局唯一标识 |
| `FromJunctionID` | `int` | 起点 Junction ID |
| `ToJunctionID` | `int` | 终点 Junction ID |
| `RoadID` | `int` | 所属 Road ID（可被 RoadNetwork 内部修改） |
| `Waypoints` | `Vector2[]` | 中间途经格点（不含两端），可为空数组 |
| `TotalLength` | `float` | 总几何长度（waypoints 段 + 首尾段之和） |

#### 方法

| 方法 | 签名 | 说明 |
|------|------|------|
| `Segment(int id, int from, int to, int roadID, Vector2[] waypoints, float totalLength)` | 构造 | 创建 Segment |
| `GetSubSegments(Junction fromJ, Junction toJ, float cellSize)` | `IEnumerable<(Vector2, Vector2, Direction)>` | 遍历途经各子线段（含方向） |

---

## 4. 工具模块 (Tools)

### ToolManager

**文件**: `Scripts/Tools/ToolManager.cs`
**继承**: `Node2D`
**命名空间**: 无（全局）

工具管理器 — 处理键盘快捷键切换工具，并将鼠标输入转发给当前激活的工具。静态单例模式。

#### 静态属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Instance` | `ToolManager` | 全局单例引用 |

#### 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `CurrentTool` | `ToolType` | 当前激活的工具。设置时自动切换上下文（铺路取消、高亮清/开） |

#### 方法

| 方法 | 签名 | 说明 |
|------|------|------|
| `_Ready()` | `override void` | 初始化单例，获取 RoadBuilder 引用 |
| `_Input(InputEvent @event)` | `override void` | 键盘工具切键（R/E/Esc）+ 转发输入给 RoadBuilder |

#### 键盘快捷键

| 按键 | 工具 | 说明 |
|------|------|------|
| `R` | `Road` | 切换至铺路工具 |
| `E` | `RoadRemove` | 切换至拆除工具 |
| `Escape` | `Select` | 切换至选择工具（默认） |

#### 工具切换行为

| 切出 | 行为 |
|------|------|
| `Road` | 调用 `RoadBuilder.CancelPlaceDrag()` 取消进行中的拖拽 |
| `RoadRemove` | 调用 `RoadBuilder.SetRemoveHoverActive(false)` 清除悬停高亮 |
| 切入 `RoadRemove` | 调用 `RoadBuilder.SetRemoveHoverActive(true)` 激活悬停高亮 |

#### 输入转发

```
_Input → 键盘切换 (R/E/Esc) → 设置 CurrentTool
       → 否则按 CurrentTool 转发:
           Road       → RoadBuilder.HandlePlaceInput(@event)
           RoadRemove → RoadBuilder.HandleRemoveInput(@event)
           Select     → 不处理
```

---

### ToolType

**文件**: `Scripts/Tools/ToolType.cs`
**类型**: 枚举

工具类型枚举。

| 值 | 说明 |
|---|------|
| `Select` | 选择工具（默认，无操作） |
| `Road` | 铺路工具（拖拽画线） |
| `RoadRemove` | 拆除工具（点击拆除 Segment） |

---

## 5. UI 模块 (UI)

### GameHUD

**文件**: `Scripts/UI/GameHUD.cs`
**继承**: `CanvasLayer`
**命名空间**: 无（全局）
**对应场景**: `Scenes/UI/GameHUD.tscn`

主 HUD 浮层 — 常驻显示 FPS、当前工具、鼠标格点坐标、路网统计，并提供工具切换按钮。

#### 导出属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Config` | `RoadConfig` | 共享路网配置（用于格点 snap 计算） |

#### 控件引用（从 .tscn 解析）

| 字段 | 类型 | 节点路径 | 内容 |
|------|------|----------|------|
| `_fpsLabel` | `Label` | `Panel/VBox/FPS` | FPS 显示 |
| `_toolLabel` | `Label` | `Panel/VBox/Tool` | 当前工具名 |
| `_mouseLabel` | `Label` | `Panel/VBox/MousePos` | 鼠标格点坐标 + [路口] 标记 |
| `_statsRoadsLabel` | `Label` | `Panel/VBox/Roads` | Road 数量 |
| `_statsSegmentsLabel` | `Label` | `Panel/VBox/Segments` | Segment 数量 |
| `_statsJunctionsLabel` | `Label` | `Panel/VBox/Junctions` | Junction 数量 |
| 按钮 | `Button` | `Panel/VBox/ToolBar/SelectBtn` | 选择工具 |
| 按钮 | `Button` | `Panel/VBox/ToolBar/RoadBtn` | 铺路工具 |
| 按钮 | `Button` | `Panel/VBox/ToolBar/RemoveBtn` | 拆除工具 |

#### 方法

| 方法 | 签名 | 说明 |
|------|------|------|
| `_Ready()` | `override void` | 确保 UIManager 单例存在 → 解析子控件 → 绑定按钮事件 |
| `_Process(double delta)` | `override void` | 逐帧更新：FPS / 工具 / 鼠标格点 / 路网统计 |
| `EnsureUIManager()` | `private void` | 创建 UIManager 作为子节点（若不存在） |
| `ResolveChildNodes()` | `private void` | 解析 .tscn 中的控件引用 |
| `WireButtons()` | `private void` | 绑定三个工具按钮点击事件 |
| `UpdateFPS()` | `private void` | 更新 FPS 显示 |
| `UpdateToolInfo()` | `private void` | 更新工具名显示 |
| `UpdateMousePos()` | `private void` | 更新鼠标格点坐标，附带 [路口] 标记 |
| `UpdateRoadStats()` | `private void` | 更新 Road / Segment / Junction 计数 |

#### 依赖

- `ToolManager.Instance` → 工具状态
- `RoadSystem.Instance.Network` → 路网统计
- `MainCamera.Instance` → 鼠标世界坐标
- `UIManager` → 自动创建并注册

---

### UIHelpers

**文件**: `Scripts/UI/UIHelpers.cs`
**类型**: 静态工具类

共享 UI 工厂方法 — 统一控件外观（字体大小、颜色、尺寸），避免各处硬编码样式。

#### 静态方法

| 方法 | 签名 | 返回值 | 说明 |
|------|------|--------|------|
| `CreateLabel(string text, int fontSize = 13)` | `Label` | 创建统一样式的 Label（浅色文字） |
| `CreateToolButton(string text, ToolType tool, Action<ToolType> onPressed)` | `Button` | 创建工具切换按钮（最小尺寸 64×28，字号 12） |
| `CreateDarkPanel(Vector2 position, Vector2 size, float alpha = 0.88f)` | `Panel` | 创建半透明深色背景 Panel |

#### 默认样式

| 样式 | 值 |
|------|----|
| 文字颜色 | `(0.9, 0.9, 0.9)` — 浅灰色 |
| Label 字号 | `13` |
| Button 字号 | `12` |
| Button 最小尺寸 | `64 × 28` |
| Panel 背景色 | `(0.08, 0.08, 0.08)` — 深灰底，透明度 0.88 |

---

### UIManager

**文件**: `Scripts/UI/UIManager.cs`
**继承**: `Node`
**命名空间**: 无（全局）

UI 面板生命周期管理器 — 全局单例，负责注册、显示、隐藏、模态面板管理。

#### 静态属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Instance` | `UIManager` | 全局单例引用 |

#### 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `IsModalActive` | `bool` | 是否有模态面板正在阻塞游戏输入 |

#### 方法

| 方法 | 签名 | 说明 |
|------|------|------|
| `UIManager()` | 构造 | 创建单例 |
| `Register(string name, Control panel)` | `void` | 注册 UI 面板 |
| `Unregister(string name)` | `void` | 注销面板 |
| `Show(string name)` | `void` | 显示面板 |
| `Hide(string name)` | `void` | 隐藏面板 |
| `Toggle(string name)` | `void` | 切换面板可见性 |
| `IsVisible(string name)` | `bool` | 查询面板可见性 |
| `HideAll()` | `void` | 隐藏所有面板（HUD 除外） |
| `PushModal(string name)` | `void` | 推入模态面板（显示 + 入栈） |
| `PopModal()` | `void` | 关闭最顶层模态面板 |
| `GetPanel<T>(string name)` | `T?` | 获取已注册面板 |

#### 模态栈

- `PushModal`: 显示面板 → 压入 `_modalStack`
- `PopModal`: 弹出栈顶 → 隐藏面板
- 游戏输入应检查 `IsModalActive` 决定是否阻塞

#### 使用模式

```csharp
// 注册
UIManager.Instance.Register("Settings", settingsPanel);

// 切换
UIManager.Instance.Toggle("Settings");

// 模态弹窗
UIManager.Instance.PushModal("PauseMenu");
// ...玩家操作...
UIManager.Instance.PopModal();
```

---

## 附录

### 单例模式一览

项目中使用静态 `Instance` 单例的类：

| 类 | 文件 | 说明 |
|----|------|------|
| `MainCamera` | `Scripts/MainCamera.cs` | 相机 |
| `MapBackground` | `Scripts/Grid/MapBackground.cs` | 背景 |
| `RoadSystem` | `Scripts/Road/RoadSystem.cs` | 路网根节点 |
| `ToolManager` | `Scripts/Tools/ToolManager.cs` | 工具管理器 |
| `UIManager` | `Scripts/UI/UIManager.cs` | UI 管理器 |

### 全局事件流

```
用户输入
  → ToolManager._Input()          (键盘切换 / 转发)
    → RoadBuilder                  (铺路 / 拆除)
      → RoadNetwork                 (数据操作)
        → SegmentAdded/Removed 事件
          → RoadRenderer             (渲染同步)
          → GameHUD._Process()       (统计刷新)
```

### 数据流

```
RoadConfig (.tres)                 ← 共享配置
  ├── RoadBuilder.Config
  ├── RoadRenderer.Config
  └── GameHUD.Config

RoadNetwork (纯数据)               ← 图结构
  ├── Junction[]                   ← 顶点
  ├── Segment[]                    ← 边
  └── Road[]                       ← 逻辑分组
```
