# SimpleCities — AI 编码助手指南

## 项目概述

Godot 4.6 C# 项目，集成了 ImGui 调试 UI。**Phase 1（网格 + 道路系统）已基本完成。**

项目能力：
- 8 方向道路铺设（正交 + 对角），鼠标拖拽式输入
- X 形交叉自动劈分，生成半格点路口
- 道路拆除（点击单段 Segment），删除后自动修复拓扑
- 工具切换：选择 (Esc) / 铺路 (R) / 拆路 (E)，带 HUD 按钮 + 快捷键
- 存档/加载：JSON 序列化，F5 保存 / F9 加载，支持路网 + 相机状态
- HUD 实时统计：FPS、当前工具、鼠标格点、Road / Segment / Junction 计数
- 无限网格背景渲染（shader 驱动）

主场景：`Scenes/MapTest.tscn`（uid: `uid://baxamkfym8atd`）

## 构建与运行

- **引擎**：Godot 4.6
- **框架**：.NET 10.0、C# 14.0、`AllowUnsafeBlocks: true`
- **依赖**：ImGui.NET 1.91.6.1、Godot.NET.Sdk 4.6.1
- **主场景**：`Scenes/MapTest.tscn`
- **ImGui 自动加载**：`ImGuiRoot.tscn`
- **SaveManager 自动加载**：`Scripts/Core/SaveManager.cs`
- **共享配置资源**：`Scenes/road_config.tres`（`RoadConfig` GlobalClass），由 `RoadBuilder`、`RoadRenderer`、`MapBackground`、`GameHUD` 共同引用，确保 CellSize 全局一致

## 项目结构

```
Scripts/
├── MainCamera.cs
├── Core/           ← 存档/核心
│   ├── ISaveable.cs
│   ├── SaveManager.cs
│   ├── SaveData.cs
│   └── SaveJson.cs
├── Grid/           ← 网格系统
│   ├── GridSystem.cs       ← 静态工具类
│   └── MapBackground.cs    ← CanvasLayer + Shader 渲染
├── Road/           ← 道路系统（核心子系统）
│   ├── RoadSystem.cs       ← 单例根节点
│   ├── RoadNetwork.cs      ← 纯数据层（核心，~1200 行）
│   ├── RoadBuilder.cs      ← 输入处理 + 拖拽投影
│   ├── RoadRenderer.cs     ← 事件驱动渲染
│   ├── RoadConfig.cs       ← [GlobalClass] Resource 配置
│   ├── Road.cs             ← 数据类
│   ├── Segment.cs          ← 数据类
│   ├── Junction.cs         ← 数据类
│   └── Direction.cs        ← 枚举 + DirectionUtil 静态工具
├── Tools/          ← 工具管理
│   ├── ToolManager.cs      ← 单例
│   └── ToolType.cs         ← 枚举
└── UI/             ← UI 系统
    ├── GameHUD.cs          ← CanvasLayer 主 HUD
    ├── UIHelpers.cs        ← 静态工厂方法
    └── UIManager.cs        ← 面板生命周期管理

Scenes/
├── MapTest.tscn            ← 主场景
├── road_config.tres        ← RoadConfig Resource
└── UI/
    └── GameHUD.tscn        ← HUD 布局

Textures/
Shaders/
addons/imgui-godot/         ← ImGui 插件，不要手动修改
docs/                       ← 设计文档
```

## 核心架构

### RoadSystem（Singleton 根节点）

场景树根节点。`_Ready()` 中：
1. 注册自身为 `Instance`
2. 创建 `RoadNetwork`（纯数据层）
3. 从 `RoadBuilder.Config` 提取 `RoadConfig` 注入 `GridSystem.Config`
4. 将 `Network` 注入 `RoadRenderer` 和 `RoadBuilder`
5. 调用 `SaveManager.Instance.Register(Network)`

```csharp
public partial class RoadSystem : Node2D
{
    public RoadNetwork Network { get; private set; } = null!;
    public static RoadSystem Instance { get; private set; } = null!;
}
```

### RoadNetwork（纯数据层，非 Godot 节点）

不继承任何 Godot 类型，通过事件与外部通信：

- **三层模型**：`Road` → `Segment` → `Junction`
- **Road**：玩家一次画线操作的逻辑集合，可由多个 Segment 组成（被路口劈分仍是同一条 Road）
- **Segment**：两个相邻 Junction 之间的几何边，含 waypoints + 总长度
- **Junction**：多 Segment 交汇点，自动计算类型（Endpoint / Straight / Curve / TJunction / Cross / XCross / MultiWay）
- **事件**：`SegmentAdded`、`SegmentRemoved`（渲染层订阅）；`NetworkReloaded`（存档加载后触发全量重建）
- **空间索引**：`Dictionary<Vector2, int>` 按位置反查 Segment/Junction（仅 snap 格点；半格 Junction 不索引，通过几何扫描访问）
- **实现 `ISaveable`**：`SaveFileName = "road_network"`

### GridSystem（静态工具类）

集中管理 CellSize，消除到处传参：

```csharp
public static class GridSystem
{
    public static RoadConfig Config { get; set; } = null!;
    public static float CellSize => Config?.CellSize ?? 64f;
    public static Vector2 SnapToGrid(Vector2 pos);
    public static bool IsSnapGrid(Vector2 pos);
}
```

初始化路径：`RoadSystem._Ready()` → `GridSystem.Config = config`

### ToolManager（Singleton + 输入转发）

- 单例模式，通过 `R`/`E`/`Esc` 快捷键切换工具
- `_Input()` 中根据 `CurrentTool` 将事件转发给 `RoadBuilder` 的不同方法
- 切换工具时自动取消进行中的拖拽（`CancelPlaceDrag()`）和清除拆路悬停高亮

```csharp
public enum ToolType { Select, Road, RoadRemove }
```

### RoadRenderer（事件驱动渲染）

- 订阅 `RoadNetwork.SegmentAdded` → 创建 `Line2D` 节点
- 订阅 `RoadNetwork.SegmentRemoved` → `QueueFree()` 对应 `Line2D`
- 订阅 `RoadNetwork.NetworkReloaded` → 全量重建所有 `Line2D`
- 交叉口绘制在独立 `Node2D` 子节点上（`_junctionLayer`），确保渲染在所有路段之上
- `_Draw()` 只画施工预览虚线和拆路悬停高亮

### RoadBuilder（输入处理 + 拖拽投影）

- `HandlePlaceInput()`：鼠标左键按下开始拖拽，释放提交道路
- `HandleRemoveInput()`：鼠标左键点击拆除该格点所在的 Segment
- `_Process()` 中 `UpdateProjection()`：将鼠标向量投影到 8 方向，计算格数预览
- 半格起点（从 X 交叉点延伸）仅允许对角方向

### UIManager（Singleton + 面板生命周期）

- 注册/注销面板：`Register(name, panel)` / `Unregister(name)`
- 可见性控制：`Show` / `Hide` / `Toggle` / `HideAll`
- 模态栈：`PushModal` / `PopModal`（阻塞游戏输入）
- 当前仅 GameHUD 使用，构造函数中自动注册 `Instance`

### 存档/加载

- `ISaveable` 接口：`SaveFileName` + `CaptureState()` + `RestoreState(string json)`
- `SaveManager`（Autoload）：遍历所有已注册 `ISaveable`，每个系统存为独立 JSON 文件
- 当前已注册：`RoadNetwork`（`RoadSystem._Ready`）、`MainCamera`（自身 `_Ready`）
- 原子写入：先写 `.tmp`，成功后再 rename 覆盖正式文件

## Godot C# 编码约定

### 类声明模式

项目使用四种不同的类声明模式，按用途选择：

| 模式 | 声明 | 用途 | 示例 |
|------|------|------|------|
| **Singleton** | `public partial class Xxx : Node2D` | Godot 节点，需挂在场景树 | `RoadSystem`, `ToolManager` |
| **Data class** | `public class Xxx` | 纯数据模型，不继承 Godot | `Road`, `Segment`, `Junction`, `RoadNetwork` |
| **Resource** | `[GlobalClass] public partial class XxxConfig : Resource` | 共享配置 `.tres` | `RoadConfig` |
| **Static utility** | `public static class Xxx` | 纯函数工具 | `GridSystem`, `DirectionUtil`, `UIHelpers` |

### Singleton 模式（强制约定）

所有系统级单例统一使用以下模式：

```csharp
public partial class MySystem : Node2D
{
    public static MySystem Instance { get; private set; } = null!;

    public override void _Ready()
    {
        Instance = this;  // 或 Instance ??= this 用于可能重复初始化的场景
    }
}
```

### 数据类（非 Godot 节点）

`RoadNetwork`、`Road`、`Segment`、`Junction`、SaveData DTO 都是普通 C# 类，不继承任何 Godot 类型。使用纯 C# 集合（`Dictionary`、`List`、`HashSet`）管理数据，通过 C# 事件（`event Action<T>?`）通知外部。

### GlobalClass Resource 模式

`RoadConfig` 是 `[GlobalClass] Resource`，在 Godot 编辑器中创建 `.tres` 文件，所有消费者通过 `[Export]` 引用同一份资源：

```csharp
[Export] public RoadConfig Config { get; set; } = null!;
```

### 事件驱动渲染

渲染层订阅数据层事件，而非在 `_Process` 中轮询：

```csharp
_network.SegmentAdded += OnSegmentAdded;   // 创建 Line2D
_network.SegmentRemoved += OnSegmentRemoved; // 销毁 Line2D
_network.NetworkReloaded += OnNetworkReloaded; // 全量重建
```

### 输入转发

`ToolManager._Input()` 根据当前工具类型将事件转发给 `RoadBuilder` 的不同方法：

```csharp
case ToolType.Road:
    _roadBuilder.HandlePlaceInput(@event);
    break;
case ToolType.RoadRemove:
    _roadBuilder.HandleRemoveInput(@event);
    break;
```

### Vector2 作为字典键

`Dictionary<Vector2, T>` 用于空间查找（`_posToJunctionID`、`_posToSegmentID`）。Godot 的 `Vector2` 实现了值相等（`Equals` 按分量比较），可直接用作字典键。

### 其他约定

- **类声明**：`public partial class MyClass : Node2D`（Godot 节点必须 `partial`）
- **导出属性**：`[Export] private int _myField;`（下划线前缀私有字段）
- **生命周期**：`_Ready()` → `_Process(double delta)` → `_Input(InputEvent @event)`
- **文件路径**：`ProjectSettings.GlobalizePath("res://...")` 将资源路径转为绝对路径
- **场景实例化**：`GD.Load<PackedScene>("res://Scenes/MyScene.tscn").Instantiate<MyNode>()`
- **输入**：`Input.GetVector("KeyBoard_MoveLeft", ...)` 用于 WASD 移动

### ImGui 集成

ImGui 通过 `addons/imgui-godot/` 插件提供，自动加载为单例。核心 API：

- `ImGuiGD.ImGuiBegin(string title)` / `ImGuiGD.ImGuiEnd()` — 窗口包裹
- `ImGuiGD.ImGuiText(string text)` — 文本显示
- `ImGuiGD.ImGuiButton(string label)` — 按钮（返回 bool）
- `ImGuiGD.ImGuiSliderFloat(...)` — 滑块控件
- 详细 API 见 `addons/imgui-godot/ImGuiGodot/ImGuiGD.cs`

使用模式：在 `_Process` 中调用 ImGui API，每帧渲染。

## Road System 详解

这是项目最复杂的子系统（`RoadNetwork.cs` ~1200 行），核心流程：

### 添加道路流程

`RoadBuilder.EndDragAndCommit()` → `RoadNetwork.AddRoad(from, to, waypoints, cellSize)`：

1. Snap 起点/终点/waypoints 到网格（已在路上的半格起点跳过 snap）
2. 校验：8 方向连续、无重复点、非自环、非完全重叠
3. X 交叉处理（`ResolveInteriorCrossings`）：扫描新路径与现有 Segment 的内部几何交点，在每个交点处调用 `SplitSegmentAtPosition` 切开旧 Segment，并将交点作为额外锚点插入新路径
4. 两端撞旧 Segment 检查：`SplitSegmentAtWaypoint` 在端点处劈开
5. 按路径上所有 Junction 位置将新路切成多段 Segment
6. 共线重叠检测：避免在已有 Segment 上铺冗余路线
7. 合并降级（`TryMergeAtJunction`）：若某 Junction 从 1 连接变为 2 连接且两侧对向直通，合并回单段 Segment
8. 为每个 Segment 创建 Junction（`GetOrCreateJunction`），仅 snap 格点 Junction 进 `_posToJunctionID` 字典

### 拆除 Segment 流程

`RoadBuilder.HandleRemoveInput()` → `RoadNetwork.RemoveSegment(segmentID)`：

1. 清除 `_segments` 字典和所有反向索引（`_posToSegmentID`）
2. 断开 from/to Junction 的 Segment 连接
3. 清孤立 Junction（ConnectionCount == 0）
4. 修复共享 Junction 的空间索引（`MaybeReindexJunctionInPosDict`）
5. 从所属 Road 摘除 Segment；Road 变空则清理
6. 调用 `SplitRoadIntoConnectedComponents`：若删除中间一段导致 Road 的剩余 Segment 不再连通，自动切成多个独立 Road
7. 触发 `SegmentRemoved` 事件
8. 合并降级检查：被删段两端的 Junction 若 ConnectionCount 降到 2 且对向直通，自动合并

### 半格 Junction

X 形交叉产生的交点可能不在标准网格点上（"半格"）。这些 Junction：
- 不加入 `_posToJunctionID` 字典（仅通过 ID 访问）
- 不加入 `_posToSegmentID` 字典（拆除工具通过被切 Segment 的另一侧间接访问）
- `FindSegmentAtIncludingHalfGrid`：先查字典，再几何扫描 waypoints + Junction
- `IsAnyJunctionAt`：先查字典，再无差别扫所有 Junction 的几何位置

### 方向工具

- `DirectionUtil.FromDisplacement(from, to, cellSize)`：严格单位距离方向判定（内部 waypoint 段使用）
- `DirectionUtil.FromDisplacementAnyLength(from, to)`：任意距离方向判定（首尾半格段使用，基于归一化向量余弦匹配）
- `DirectionUtil.GetDisplacement(d)`：获取方向的 (dx, dy) 单位位移

### RoadConfig.tres 配置

共享配置资源，所有模块通过 `[Export]` 引用同一份 `.tres`：

- `CellSize`：网格单元尺寸（默认 64）
- `RoadColor` / `RoadWidth`：路段颜色和线宽
- `JunctionRadius` / `JunctionColor`：真路口圆点样式
- `EndpointRadius` / `EndpointColor`：端点圆点样式
- `HoverHighlightColor` / `HoverHighlightWidth`：拆路悬停高亮样式

## 常见任务

### 添加新的道路功能

修改三层：`RoadBuilder`（输入处理）→ `RoadNetwork`（数据逻辑）→ `RoadRenderer`（视觉表现）。例如增加曲线道路：`RoadBuilder` 处理新的鼠标手势，`RoadNetwork` 修改 AddRoad 接受曲线参数，`RoadRenderer` 使用贝塞尔曲线渲染。

### 添加新工具

1. 在 `ToolType` 枚举中添加新项
2. 在 `ToolManager._Input()` 中处理新工具的按键切换和输入转发
3. 若需要新交互模式，在 `RoadBuilder` 或其他处理类中添加对应的 Handle 方法
4. 在 `GameHUD` 中添加对应的工具按钮

### 添加新 UI 面板

1. 在 Godot 编辑器中创建 Control 场景（`.tscn` + C# 脚本）
2. 在面板的 `_Ready()` 中调用 `UIManager.Instance.Register("panelName", this)` 注册
3. 使用 `UIHelpers` 静态方法确保控件样式一致（`CreateLabel`、`CreateDarkPanel`、`CreateToolButton`）
4. 通过 `UIManager.Instance.Show("panelName")` 控制可见性
5. 模态弹窗使用 `PushModal("panelName")` 阻塞游戏输入

### 添加可存档数据

1. 创建 DTO 类（纯数据类，字段用 `{ get; set; }`）
2. 在目标系统上实现 `ISaveable` 接口（`SaveFileName` + `CaptureState()` + `RestoreState(string json)`）
3. 在系统的 `_Ready()` 中调用 `SaveManager.Instance.Register(this)` 注册
4. 序列化/反序列化使用 `SaveJson.Serialize()` / `SaveJson.Deserialize<T>()`

### 修改网格行为

- 纯数学逻辑：修改 `GridSystem` 静态类
- 视觉渲染：修改 `MapBackground` 及其关联的 `.gdshader` 文件（`Shaders/` 目录）
- 网格尺寸：修改 `RoadConfig.tres` 中的 `CellSize`

### 添加新路口类型

1. 在 `JunctionType` 枚举中添加新类型
2. 更新 `Junction.RecalculateType()` 中的 switch 逻辑
3. 更新 `RoadRenderer.OnDrawJunctions()` 中的绘制逻辑（不同路口类型可能用不同颜色/形状）

## 设计文档参考

`docs/` 目录包含独立的设计文档，不要将其内容重复写入此文件：

- `docs/class-reference.md` — 完整类 API 参考
- `docs/grid-system.md` — 网格系统 + 半格点设计方案
- `docs/implementation-roadmap.md` — Phase 进度与里程碑
- `docs/math-model.md` — 模拟数学模型（远期 Phase）
- `docs/design-overview.md` — 设计总览
- `docs/persistence-plan.md` — 存档系统设计
- `docs/ui-architecture.md` — UI 架构方案
- `docs/simulation-systems.md` — 远期模拟系统设计
- `docs/game-style-discussion.md` — 游戏风格讨论
