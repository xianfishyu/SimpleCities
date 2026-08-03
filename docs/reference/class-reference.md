# SimpleCities 类与 API 参考

> 最后更新：2026-08-04 | Godot 4.7 | Godot.NET.Sdk 4.7.0 | .NET 10.0 | C# 14.0 | Nullable enabled

本文档聚焦项目自有 API；当前事实源包括 `Scripts/` 下 49 个 C# 文件和 `Shaders/MapTerrain.gdshader`。`addons/` 为第三方插件，不纳入本参考。

---

## 目录

- [1. 项目元数据与总览](#1-项目元数据与总览)
- [2. 单例与初始化](#2-单例与初始化)
- [3. Core 持久化模块](#3-core-持久化模块)
- [4. MainCamera](#4-maincamera)
- [5. Grid 与地图背景](#5-grid-与地图背景)
- [6. RoadGraph 道路数据模型](#6-roadgraph-道路数据模型)
- [7. Road 输入、渲染与系统装配](#7-road-输入渲染与系统装配)
- [8. Tools 工具模块](#8-tools-工具模块)
- [9. UI 模块](#9-ui-模块)
- [10. 数据流、事件流与存档流](#10-数据流事件流与存档流)
- [11. 兼容性 DTO 词汇说明](#11-兼容性-dto-词汇说明)

---

## 1. 项目元数据与总览

| 项 | 当前值 | 来源 |
|---|---|---|
| Godot 功能标记 | `4.7`, `C#`, `Forward Plus` | `project.godot` |
| Godot .NET SDK | `Godot.NET.Sdk/4.7.0` | `SimpleCities.csproj` |
| TargetFramework | `net10.0` | `SimpleCities.csproj` |
| LangVersion | `14.0` | `SimpleCities.csproj` |
| Nullable | `enable` | `SimpleCities.csproj` |
| AllowUnsafeBlocks | `true` | `SimpleCities.csproj` |
| 主场景 | `uid://baxamkfym8atd` | `project.godot` |
| Autoload | `ImGuiRoot`, `SaveManager`, `MCPGameBridge` | `project.godot` |

当前道路运行时模型已经从旧术语迁移到新术语：`RoadGraph` 是纯数据核心，`GraphNode` 是拓扑节点，`GraphEdge` 是原生几何边，`RoadGroup` 是一次铺路操作形成的边集合。旧 `RoadNetwork` / `Road` / `Segment` / `Junction` 不再是运行时类，只在未被当前 RoadGraph 使用的 legacy 公开 DTO 中保留；当前私有 v2 payload 使用 `nodes` / `edges` / `groups`。

| 模块 | 文件 | 职责 |
|---|---|---|
| Core | `ISaveable.cs`, `SaveManager.cs`, `SaveJson.cs`, `SaveData.cs` | 独立 JSON 存档、manifest、DTO |
| Camera | `MainCamera.cs` | 2D 相机移动、缩放、相机存档 |
| Grid | `GridSystem.cs`, `MapBackground.cs`, `MapTerrain.gdshader` | 网格数学、背景 CanvasLayer、Shader 网格绘制 |
| Road data | `Direction.cs`, `GraphNode.cs`, `GraphEdge.cs`, `RoadGroup.cs`, `RoadPath.cs`, `SpatialIndex.cs`, `RoadGraph*.cs`, `Geometry/*.cs` | 输入方向、拓扑、原生几何、空间索引、提交与持久化 |
| Road scene | `RoadBuilder.cs`, `RoadConfig.cs`, `RoadRenderer.cs`, `RoadSystem.cs` | 输入投影、共享配置、事件驱动渲染、依赖注入 |
| Tools | `ToolManager.cs`, `ToolType.cs` | 工具切换和输入转发 |
| UI | `GameHUD.cs`, `ConstructionDock.cs`, `ToolContextPanel.cs`, `DebugPanel.cs`, `PauseMenu.cs`, `UIManager.cs` | 命令中心 HUD、建造坞、上下文、诊断、暂停菜单和面板管理 |

---

## 2. 单例与初始化

| 单例 | 类型 | 创建位置 | 主要消费者 |
|---|---|---|---|
| `SaveManager.Instance` | `SaveManager` | Autoload `_Ready()` | `MainCamera`, `RoadSystem`, `GameHUD` |
| `MainCamera.Instance` | `MainCamera` | `MainCamera._Ready()` | `MapBackground`, `GameHUD` |
| `RoadSystem.Instance` | `RoadSystem` | `RoadSystem._Ready()` | `GameHUD` |
| `ToolManager.Instance` | `ToolManager` | `ToolManager._Ready()` | `GameHUD` |
| `MapBackground.Instance` | `MapBackground` | `MapBackground._Ready()` | 目前无直接调用者 |
| `UIManager` 子节点 | `UIManager` | `GameHUD.EnsureUIManager()` | 所属 `GameHUD` 内面板 |

| 初始化阶段 | 关键调用 | 结果 |
|---|---|---|
| Core autoload | `SaveManager._Ready()` | 建立全局持久化入口 |
| 相机 | `MainCamera._Ready()` / `_ExitTree()` | 设置 `Instance`、记录初始位置并注册到 `SaveManager`；退出时注销并清理单例 |
| 道路系统 | `RoadSystem._Ready()` / `_ExitTree()` | 创建并注册 `RoadGraph`，注入 renderer/builder；退出时注销并清理单例 |
| HUD | `GameHUD._Ready()` | 获取 `ToolManager` 和 `RoadSystem.Graph`，解析控件，绑定工具和存档按钮 |

---

## 3. Core 持久化模块

### ISaveable

**文件**：`Scripts/Core/ISaveable.cs`
**类型**：`public interface ISaveable`

| 成员 | 签名 | 说明 |
|---|---|---|
| `SaveFileName` | `string SaveFileName { get; }` | 存档文件名，不含 `.json` |
| `CaptureState` | `object CaptureState()` | 捕获纯 DTO 状态 |
| `RestoreState` | `void RestoreState(string json)` | 从 raw JSON 恢复状态 |

当前注册实现：`RoadGraph.SaveFileName == "road_network"`，`MainCamera.SaveFileName == "camera"`。

### SaveManager

**文件**：`Scripts/Core/SaveManager.cs`
**继承**：`public partial class SaveManager : Node`

存档契约的详细说明见 [存档系统当前参考](save-system-plan.md)。本文只保留 API 速查和当前实现摘要。

| 成员 | 签名 | 说明 |
|---|---|---|
| `Instance` | `public static SaveManager Instance { get; private set; }` | Autoload 单例 |
| `CurrentSlotName` | `public string CurrentSlotName { get; private set; } = "autosave"` | 当前槽位名 |
| `RegisteredSaveableCount` | `public int RegisteredSaveableCount` | 当前活动注册数量 |
| `_Ready` | `public override void _Ready()` | 设置 `Instance` |
| `Register` | `public bool Register(ISaveable saveable)` | 同一对象幂等；拒绝另一活动对象使用相同 `SaveFileName` |
| `Unregister` | `public bool Unregister(ISaveable saveable)` | 移除离开场景树的可存档对象 |
| `Save` | `public bool Save(string slotName = "autosave")` | 保存所有已注册系统 |
| `Load` | `public bool Load(string slotName = "autosave")` | 加载 manifest 中匹配已注册系统的文件 |
| `SaveSlotExists` | `public bool SaveSlotExists(string slotName)` | 检查 manifest 是否存在 |
| `DeleteSlot` | `public void DeleteSlot(string slotName)` | 对槽位目录调用一次 `DirAccess.RemoveAbsolute(...)`；当前实现不检查删除结果 |

| 存档规则 | 当前实现 |
|---|---|
| 基础目录 | 编辑器使用全局化的 `res://saves/<slot>/`；导出版本使用可执行文件旁的 `saves/<slot>/` |
| 单系统文件 | `<SaveFileName>.json` |
| 写入策略 | 每个文件先写 `.tmp`，再移动为正式文件；这不是整槽原子事务 |
| Manifest | `manifest.json`，字段来自 `ManifestData` |
| 错误处理 | 捕获异常，`GD.PushError(...)`，返回 `false` |

### InputBindingManager

**文件**：`Scripts/Core/InputBindingManager.cs`
**继承**：`public partial class InputBindingManager : Node`

| 成员 | 签名 | 说明 |
|---|---|---|
| `Instance` | `public static InputBindingManager Instance { get; private set; }` | Autoload 单例 |
| `Definitions` | `public static IReadOnlyList<BindingDefinition> Definitions` | WASD、Q/R/E 和暂停动作目录 |
| `EventMatchesAction` | `public bool EventMatchesAction(InputEvent inputEvent, string actionName)` | 以当前物理键绑定匹配真实输入 |
| `TryGetToolForEvent` | `public bool TryGetToolForEvent(InputEvent inputEvent, out ToolType tool)` | 把当前工具动作映射为 `ToolType` |
| `TryRebind` | `public bool TryRebind(string actionName, Key key, out string error)` | 拒绝非法或冲突按键，成功时更新并持久化 |
| `ResetToDefaults` | `public bool ResetToDefaults(out string error)` | 恢复全部默认绑定并持久化 |
| `GetBindingText` | `public string GetBindingText(string actionName)` | 返回当前玩家可读键名 |

配置写入 `user://input_bindings.cfg`。载入配置存在非法键或重复值时，整套保留默认绑定，不应用部分配置。

### SaveJson

**文件**：`Scripts/Core/SaveJson.cs`
**类型**：`public static class SaveJson`

| 成员 | 签名 | 说明 |
|---|---|---|
| `Serialize` | `public static string Serialize(object data)` | 使用统一 `JsonSerializerOptions` 序列化 |
| `Deserialize` | `public static T Deserialize<T>(string json)` | 使用统一 `JsonSerializerOptions` 反序列化 |

| 选项 | 值 |
|---|---|
| `WriteIndented` | `true` |
| `PropertyNameCaseInsensitive` | `true` |

### SaveData DTO

**文件**：`Scripts/Core/SaveData.cs`
**类型**：公开 DTO 类，均为纯数据对象

活动 schema、验证状态和未来迁移边界见 [存档系统当前参考](save-system-plan.md)。本节只列当前 DTO 形状。

| DTO | 公开属性签名 | JSON 字段 | 默认值/说明 |
|---|---|---|---|
| `ManifestData` | `public int SchemaVersion { get; set; }` | `schemaVersion` | `1` |
| `ManifestData` | `public string SlotName { get; set; }` | `slotName` | `"autosave"` |
| `ManifestData` | `public string Timestamp { get; set; }` | `timestamp` | `""` |
| `ManifestData` | `public string CityName { get; set; }` | `cityName` | `"My City"` |
| `ManifestData` | `public List<string> Files { get; set; }` | `files` | 文件名列表 |
| `RoadNetworkData` | `public int NextID { get; set; }` | `nextID` | 未被当前 `RoadGraph` 直接使用的旧结构公开 DTO |
| `RoadNetworkData` | `public float CellSize { get; set; }` | `cellSize` | 同上 |
| `RoadNetworkData` | `public List<JunctionData> Junctions { get; set; }` | `junctions` | 同上 |
| `RoadNetworkData` | `public List<SegmentData> Segments { get; set; }` | `segments` | 同上 |
| `RoadNetworkData` | `public List<RoadData> Roads { get; set; }` | `roads` | 同上 |
| `JunctionData` | `public int ID { get; set; }` | `id` | 节点 ID |
| `JunctionData` | `public float X { get; set; }` | `x` | 节点 X |
| `JunctionData` | `public float Y { get; set; }` | `y` | 节点 Y |
| `SegmentData` | `public int ID { get; set; }` | `id` | Edge ID |
| `SegmentData` | `public int FromJunctionID { get; set; }` | `fromJunctionID` | NodeA |
| `SegmentData` | `public int ToJunctionID { get; set; }` | `toJunctionID` | NodeB |
| `SegmentData` | `public int RoadID { get; set; }` | `roadID` | GroupID |
| `SegmentData` | `public List<Vector2Data> Waypoints { get; set; }` | `waypoints` | 中间点 |
| `SegmentData` | `public float TotalLength { get; set; }` | `totalLength` | 边长 |
| `SegmentData` | `public int? Type { get; set; }` | `type` | nullable，旧存档缺失时回退 `Street` |
| `RoadData` | `public int ID { get; set; }` | `id` | Group ID |
| `RoadData` | `public List<int> SegmentIDs { get; set; }` | `segmentIDs` | Edge ID 集合 |
| `RoadData` | `public int? Type { get; set; }` | `type` | nullable，旧存档缺失时回退 `Street` |
| `Vector2Data` | `public float X { get; set; }` | `x` | X |
| `Vector2Data` | `public float Y { get; set; }` | `y` | Y |
| `CameraData` | `public float PositionX { get; set; }` | `positionX` | 相机 X |
| `CameraData` | `public float PositionY { get; set; }` | `positionY` | 相机 Y |
| `CameraData` | `public float Zoom { get; set; }` | `zoom` | 相机缩放目标 |

| DTO 方法/构造 | 签名 | 说明 |
|---|---|---|
| `Vector2Data` | `public Vector2Data()` | JSON 反序列化构造函数 |
| `Vector2Data` | `public Vector2Data(Vector2 v)` | 从 Godot `Vector2` 构造 |
| `Vector2Data.ToVector2` | `public Vector2 ToVector2()` | 转回 `Vector2` |

---

## 4. MainCamera

**文件**：`Scripts/MainCamera.cs`
**继承**：`public partial class MainCamera : Camera2D, ISaveable`

| 导出成员 | 签名 | 默认值 | 说明 |
|---|---|---|---|
| `defaultScale` | `[Export] private float defaultScale = 1f` | `1f` | 目标缩放，参与存档 |
| `scaleFactor` | `[Export] public float scaleFactor = 0.125f` | `0.125f` | 鼠标滚轮缩放因子 |
| `minScale` | `[Export] public float minScale = 0.125f` | `0.125f` | 最小目标缩放 |
| `maxScale` | `[Export] public float maxScale = 4f` | `4f` | 最大目标缩放 |
| `keyMoveFactor` | `[Export] public float keyMoveFactor = 10f` | `10f` | 键盘移动系数 |
| `moveSpeed` | `[Export] public float moveSpeed = 1.25f` | `1.25f` | 键盘移动速度 |

| 公开成员 | 签名 | 说明 |
|---|---|---|
| `Instance` | `public static MainCamera Instance { get; private set; }` | 单例引用 |
| `_Ready` | `public override void _Ready()` | 设置单例、记录 `nextPos`、注册到 `SaveManager` |
| `_ExitTree` | `public override void _ExitTree()` | 从 `SaveManager` 注销并清理当前单例 |
| `_Process` | `public override void _Process(double delta)` | 更新键盘移动、缩放和中键拖拽 |
| `_Input` | `public override void _Input(InputEvent @event)` | WASD、滚轮、中键输入 |
| `SaveFileName` | `public string SaveFileName => "camera"` | 相机存档文件名 |
| `CaptureState` | `public object CaptureState()` | 返回 `CameraData` |
| `RestoreState` | `public void RestoreState(string json)` | 从 `CameraData` 恢复 `Position` 和 `defaultScale`，并同步 `nextPos` |

| 输入动作 | 来源 | 作用 |
|---|---|---|
| `KeyBoard_MoveUp` / `Down` / `Left` / `Right` | `InputBindingManager`，默认 W/A/S/D | `Input.GetVector(...)` 平移相机 |
| `MouseButton.WheelUp` | `_Input` | `defaultScale += scaleFactor * defaultScale` |
| `MouseButton.WheelDown` | `_Input` | `defaultScale -= scaleFactor * defaultScale` |
| `MouseButton.Middle` | `_Input` / `_Process` | 记录鼠标世界坐标并拖拽平移 |

---

## 5. Grid 与地图背景

### GridSystem

**文件**：`Scripts/Grid/GridSystem.cs`
**类型**：`public static class GridSystem`

| 成员 | 签名 | 说明 |
|---|---|---|
| `Config` | `public static RoadConfig Config { get; set; } = null!` | 由 `RoadSystem._Ready()` 注入 |
| `CellSize` | `public static float CellSize => Config?.CellSize ?? 64f` | 未初始化时回退 `64f` |
| `SnapToGrid` | `public static Vector2 SnapToGrid(Vector2 pos)` | 按 `CellSize` 四舍五入到格点 |
| `IsSnapGrid` | `public static bool IsSnapGrid(Vector2 pos)` | 判断是否在格点上，容差 `1e-3f` |

### MapBackground

**文件**：`Scripts/Grid/MapBackground.cs`
**继承**：`public partial class MapBackground : CanvasLayer`

| 公开/导出成员 | 签名 | 默认值 | 说明 |
|---|---|---|---|
| `Instance` | `public static MapBackground Instance { get; private set; }` | 无 | 单例引用 |
| `Config` | `[Export] public RoadConfig Config { get; set; } = null!` | 场景注入 | 网格尺寸来源 |
| `BackgroundColor` | `[Export] public Color BackgroundColor = new(0.118f, 0.118f, 0.118f)` | 深灰 | 背景色 |
| `MajorGridCells` | `[Export(PropertyHint.Range, "1,20,1")] public int MajorGridCells = 5` | `5` | 主网格倍数 |
| `MainLineWidth` | `[Export] public float MainLineWidth = 1.5f` | `1.5f` | 主网格线宽 |
| `MajorGridColor` | `[Export] public Color MajorGridColor = new(0.25f, 0.25f, 0.25f)` | 灰 | 主网格色 |
| `MinorGridCells` | `[Export(PropertyHint.Range, "1,10,1")] public int MinorGridCells = 1` | `1` | 次网格倍数 |
| `LineWidth` | `[Export] public float LineWidth = 0.5f` | `0.5f` | 次网格线宽 |
| `MinorGridColor` | `[Export] public Color MinorGridColor = new(0.18f, 0.18f, 0.18f)` | 灰 | 次网格色 |
| `DotGridSize` | `[Export] public float DotGridSize = 10f` | `10f` | 点网格间距 |
| `DotRadius` | `[Export] public float DotRadius = 0.5f` | `0.5f` | 点半径 |
| `DotColor` | `[Export] public Color DotColor = new(0.20f, 0.20f, 0.20f)` | 灰 | 点颜色 |
| `ShowGrid` | `[Export] public bool ShowGrid = true` | `true` | 总开关 |
| `ShowMainGrid` | `[Export] public bool ShowMainGrid = true` | `true` | 主网格开关 |
| `ShowMinorGrid` | `[Export] public bool ShowMinorGrid = true` | `true` | 次网格开关 |
| `ShowDotGrid` | `[Export] public bool ShowDotGrid = true` | `true` | 点网格开关 |
| `Display` | `[Export] public ColorRect Display` | 场景注入 | 全屏背景矩形 |
| `_Ready` | `public override void _Ready()` | 无 | 设置单例、校验 `Config`、铺满 `Display`、获取 `ShaderMaterial` |
| `_Process` | `public override void _Process(double delta)` | 无 | 每帧写 shader uniforms |
| `ToggleGrid` | `public void ToggleGrid()` | 无 | 翻转 `ShowGrid` |

### MapTerrain.gdshader

**文件**：`Shaders/MapTerrain.gdshader`
**类型**：`shader_type canvas_item`，`render_mode unshaded`

| Uniform | 类型 | 默认值 | 由谁更新 | 说明 |
|---|---|---|---|---|
| `background_color` | `vec3` | `vec3(0.118, 0.118, 0.118)` | `MapBackground` | 背景色 |
| `grid_offset` | `vec2` | `vec2(50.0, 50.0)` | `MapBackground` | 当前代码传入 `_gridOffset == Vector2.Zero` |
| `major_grid_size` | `float` | `500.0` | `MapBackground` | `Config.CellSize * MajorGridCells` |
| `major_line_width` | `float` | `1.5` | `MapBackground` | 主线宽 |
| `major_grid_color` | `vec3` | `vec3(0.25, 0.25, 0.25)` | `MapBackground` | 主线颜色 |
| `minor_grid_size` | `float` | `100.0` | `MapBackground` | `Config.CellSize * MinorGridCells` |
| `minor_line_width` | `float` | `0.5` | `MapBackground` | 次线宽 |
| `minor_grid_color` | `vec3` | `vec3(0.18, 0.18, 0.18)` | `MapBackground` | 次线颜色 |
| `dot_grid_size` | `float` | `10.0` | `MapBackground` | 点间距 |
| `dot_radius` | `float` | `0.5` | `MapBackground` | 点半径 |
| `dot_color` | `vec3` | `vec3(0.20, 0.20, 0.20)` | `MapBackground` | 点颜色 |
| `show_major_grid` | `bool` | `true` | `MapBackground` | 主网格显示 |
| `show_minor_grid` | `bool` | `true` | `MapBackground` | 次网格显示 |
| `show_dot_grid` | `bool` | `true` | `MapBackground` | 点网格显示 |
| `camera_pos` | `vec2` | `vec2(0.0)` | `MapBackground` | 相机世界位置 |
| `camera_zoom` | `float` | `1.0` | `MapBackground` | 相机 X 缩放 |
| `viewport_size` | `vec2` | `vec2(1920.0, 1080.0)` | `MapBackground` | 可见视口尺寸 |

Shader 的 `fragment()` 将 `UV` 转成世界坐标，减去 `grid_offset` 后依次绘制点网格、次网格线、主网格线，并用 `fwidth(world_pos)` 做抗锯齿。

---

## 6. RoadGraph 道路数据模型

### Direction 与 DirectionUtil

**文件**：`Scripts/Road/Direction.cs`

| 类型 | 签名/值 | 说明 |
|---|---|---|
| `Direction` | `public enum Direction { N, NE, E, SE, S, SW, W, NW }` | 8 方向枚举 |
| `GetDisplacement` | `public static Vector2I GetDisplacement(Direction d)` | 返回单位格位移 |
| `FromDisplacement` | `public static Direction? FromDisplacement(Vector2 from, Vector2 to, float cellSize)` | 按一个格距识别方向 |
| `FromDisplacementAnyLength` | `public static Direction? FromDisplacementAnyLength(Vector2 from, Vector2 to)` | 按归一化向量识别任意长度方向 |
| `IsOrthogonal` | `public static bool IsOrthogonal(Direction d)` | N/E/S/W |
| `IsDiagonal` | `public static bool IsDiagonal(Direction d)` | NE/SE/SW/NW |
| `Length` | `public static float Length(Direction d, float cellSize)` | 正交为 `cellSize`，对角为 `cellSize * sqrt(2)` |
| `All` | `public static Direction[] All { get; }` | 顺序：`N, NE, E, SE, S, SW, W, NW` |

### GraphNode 与 EdgeRef

**文件**：`Scripts/Road/GraphNode.cs`

| 类型 | 公开成员 | 签名 | 说明 |
|---|---|---|---|
| `EdgeRef` | `EdgeID` | `public int EdgeID { get; }` | 邻接边 ID |
| `EdgeRef` | `NeighborNodeID` | `public int NeighborNodeID { get; }` | 邻接节点 ID |
| `EdgeRef` | 构造函数 | `public EdgeRef(int edgeID, int neighborNodeID)` | 创建邻接引用 |
| `GraphNode` | `ID` | `public int ID { get; }` | 节点 ID |
| `GraphNode` | `Position` | `public Vector2 Position { get; }` | 世界坐标 |
| `GraphNode` | `Edges` | `public IReadOnlyList<EdgeRef> Edges` | 由 `ReadOnlyCollection` 提供的实时只读视图 |
| `GraphNode` | `EdgeCount` | `public int EdgeCount => _edges.Count` | 邻接边数 |
| `GraphNode` | 构造函数 | `public GraphNode(int id, Vector2 position)` | 创建节点 |
| `GraphNode` | `GetNeighborIDs` | `public IEnumerable<int> GetNeighborIDs()` | 去重后的邻居节点 ID |

`GraphNode.AddEdge(...)` 和 `GraphNode.RemoveEdge(...)` 是 `internal`，由 `RoadGraph` 维护，不是外部公开 API。

### GraphEdge

**文件**：`Scripts/Road/GraphEdge.cs`

| 成员 | 签名 | 说明 |
|---|---|---|
| `ID` | `public int ID { get; }` | 边 ID |
| `NodeA` | `public int NodeA { get; internal set; }` | 起点节点 ID |
| `NodeB` | `public int NodeB { get; internal set; }` | 终点节点 ID |
| `GeometrySegments` | `public IReadOnlyList<RoadGeometrySegment> GeometrySegments` | 保留类型与控制参数的权威原生几何，只读包装 |
| `Points` | `public Vector2[] Points { get; }` | 原生段边界的防御性兼容副本，不含端点 |
| `GroupID` | `public int GroupID { get; internal set; }` | 所属 `RoadGroup` |
| `Length` | `public float Length { get; }` | 几何长度 |
| 构造函数 | `public GraphEdge(int id, int nodeA, int nodeB, IReadOnlyList<RoadGeometrySegment> geometrySegments, int groupID)` | 创建至少包含一个连续原生几何段的边 |
| `GetFullPath` | `public Vector2[] GetFullPath(Func<int, GraphNode?> getNode)` | 返回 `[NodeA.Position, ...Points, NodeB.Position]`；端点缺失时抛出 `InvalidOperationException` |

### RoadGroup

**文件**：`Scripts/Road/RoadGroup.cs`

| 成员 | 签名 | 说明 |
|---|---|---|
| `ID` | `public int ID { get; }` | 组 ID |
| `EdgeIDs` | `public IReadOnlyCollection<int> EdgeIDs` | 组内边 ID 的防御性快照 |
| `EdgeCount` | `public int EdgeCount => _edgeIDs.Count` | 边数量 |
| `IsEmpty` | `public bool IsEmpty => _edgeIDs.Count == 0` | 是否为空 |
| 构造函数 | `public RoadGroup(int id)` | 创建空分组 |

`AddEdge(int)` 和 `RemoveEdge(int)` 是 `internal`，只由 `RoadGraph` 更新。

### RoadType 边界

第二代运行时、公共提交 API 和 `RoadGraph` v2 存档 schema 均不包含 `RoadType`，仓库中不存在 `Scripts/Road/RoadType.cs`。道路分级、差异化样式、类型选择和升级工具属于第三代；届时必须使用新契约和新 schema 版本引入，不得把旧字段静默映射为默认类型。

### SpatialIndex

**文件**：`Scripts/Road/SpatialIndex.cs`

| 类型 | 公开成员 | 签名/值 | 说明 |
|---|---|---|---|
| `ISpatialRef` | `Position` | `Vector2 Position { get; }` | 空间位置 |
| `ISpatialRef` | `Kind` | `SpatialRefKind Kind { get; }` | 引用类别 |
| `ISpatialRef` | `IntersectsCircle` | `bool IntersectsCircle(Vector2 center, float radius)` | 权威圆形命中过滤 |
| `SpatialRefKind` | 枚举值 | `Node`, `EdgePoint`, `EdgeSegment`, `EdgeGeometry` | 节点、兼容点、直线段或原生几何 |
| `NodeSpatialRef` | `NodeID` | `public int NodeID { get; }` | 节点 ID |
| `NodeSpatialRef` | `Position` | `public Vector2 Position { get; }` | 节点位置 |
| `EdgeGeometryRef` | `EdgeID` / `Geometry` / `Bounds` | 原生几何引用及其保守包围盒 | 当前 RoadGraph 的 Edge 索引引用 |
| `UniformGrid` | 构造函数 | `public UniformGrid(float bucketSize)` | bucket 下限为 `1f` |
| `UniformGrid` | `Insert` | `public void Insert(ISpatialRef entity)` | 插入引用 |
| `UniformGrid` | `Remove` | `public void Remove(ISpatialRef entity)` | 按对象引用移除 |
| `UniformGrid` | `InsertGeometry` / `RemoveGeometry` | `public void ...(EdgeGeometryRef geometry)` | 按原生几何 Bounds 覆盖的全部桶增删引用 |
| `UniformGrid` | `QueryRadius` | `public IEnumerable<ISpatialRef> QueryRadius(Vector2 center, float radius)` | 半径查询，桶过滤加精确距离 |
| `UniformGrid` | `QueryBounds` | `public IEnumerable<ISpatialRef> QueryBounds(Rect2 bounds)` | 返回覆盖桶内去重引用，调用方再做权威几何过滤 |
| `UniformGrid` | `Clear` | `public void Clear()` | 清空索引 |

空间索引是查询加速结构，不是权威数据源。`RoadGraph` 同步维护 `_nodes`、`_edges`、`_groups`、`_nodeRefs`、`_edgeRefs` 和 `_spatialIndex`。成本取决于查询或几何 Bounds 覆盖的桶数以及这些桶内的引用数；跨桶引用会在查询时去重，移除会扫描每个覆盖桶内的 `List<ISpatialRef>`，因此不宣称无条件 `O(1)` 删除或 `O(1 + k)` 查询。

### RoadGraph

**文件**：`Scripts/Road/RoadGraph.cs`
**类型**：`public partial class RoadGraph : ISaveable`

| 常量/内部结构 | 当前值/职责 |
|---|---|
| `SnapRadius` | `0.5f`，节点复用半径 |
| `GeometryEpsilon` | `1e-4f`，几何容差 |
| `IndexBucketSize` | `64f`，默认空间索引桶尺寸 |
| `_nodes` | `Dictionary<int, GraphNode>`，权威节点表 |
| `_edges` | `Dictionary<int, GraphEdge>`，权威边表 |
| `_groups` | `Dictionary<int, RoadGroup>`，权威道路组表 |
| `_nodeRefs` / `_edgeRefs` | 记录插入到 `UniformGrid` 的原引用，保证删除精确 |

| 公开成员 | 签名 | 说明 |
|---|---|---|
| `SaveFileName` | `public string SaveFileName => "road_network"` | 路网存档文件名 |
| `EdgeAdded` | `public event Action<GraphEdge>? EdgeAdded` | 新边所在变更提交完成后触发 |
| `EdgeRemoved` | `public event Action<GraphEdge>? EdgeRemoved` | 删边所在变更提交完成后触发 |
| `GraphCleared` | `public event Action? GraphCleared` | 加载并重建后触发 |
| 构造函数 | `public RoadGraph()` | 使用默认 `IndexBucketSize` |
| 构造函数 | `public RoadGraph(float bucketSize)` | 指定空间索引 bucket |
| `AddRoad` | `public int AddRoad(Vector2 start, Vector2 end, Vector2[] waypoints)` | 折线兼容入口，返回 group ID，失败或完全覆盖返回 `-1` |
| `SubmitPolyline` | `public RoadPathSubmissionResult SubmitPolyline(IReadOnlyList<Vector2>? points)` | 无网格参数的结构化折线提交入口 |
| `SubmitPath` | `public RoadPathSubmissionResult SubmitPath(RoadPath? path)` | 提交连续原生几何路径并返回完整变更摘要 |
| `RemoveEdge` | `public bool RemoveEdge(int edgeID)` | 删除单边，一次清理孤立节点与空 Group，不触发合并 |
| `RemoveRoadGroup` | `public bool RemoveRoadGroup(int groupID)` | 按 Edge ID 稳定顺序批量 detach，一次清理后发布事件，不触发合并 |
| `GetEdge` | `public GraphEdge? GetEdge(int edgeID)` | 取边 |
| `GetNode` | `public GraphNode? GetNode(int nodeID)` | 取节点 |
| `GetGroup` | `public RoadGroup? GetGroup(int groupID)` | 取组 |
| `FindClosestEdge` | `public GraphEdge? FindClosestEdge(Vector2 position, float maxRadius)` | 从原生几何空间候选中计算权威最近点；等距时选较小 Edge ID |
| `FindClosestNode` | `public GraphNode? FindClosestNode(Vector2 position, float maxRadius)` | 基于空间索引查最近节点 |
| `GetAllEdges` | `public IEnumerable<GraphEdge> GetAllEdges()` | 返回调用时的边稳定快照 |
| `GetAllNodes` | `public IEnumerable<GraphNode> GetAllNodes()` | 返回调用时的节点稳定快照 |
| `GetAllGroups` | `public IEnumerable<RoadGroup> GetAllGroups()` | 返回调用时的道路组稳定快照 |
| `CaptureState` | `public object CaptureState()` | 返回私有 `RoadGraphSaveData`，写入 `schemaVersion = 1`、`nextID`、`nodes`、`edges`、`groups` |
| `RestoreState` | `public void RestoreState(string json)` | 先全量解析校验临时状态；成功后替换图、重建邻接与索引并触发 `GraphCleared` |

| 折线提交关键阶段 | 行为 |
|---|---|
| 1 | 起终点相同直接返回 `-1` |
| 2 | 组装 `start + waypoints + end` |
| 3 | 在任何拆分前执行完整覆盖检查，完整重复路径无副作用返回 `-1` |
| 4 | `ResolveIntersections` 查交点并拆分既有边 |
| 5 | `SplitEdgesAtPathAnchors` 处理新路径锚点落在既有边内部或 waypoint 的情况 |
| 6 | `InsertExistingNodeAnchors` 把既有节点插入路径 |
| 7 | 再次完整覆盖检查 |
| 8 | 创建 `RoadGroup`，逐段跳过已覆盖区间并添加边 |
| 9 | 没有实际新增边时清理空组并返回 `-1` |
| 10 | 对触及节点执行共线合并，清理可能变空的组 |

`SubmitPath` 对六类原生几何执行类型、有限值、连续性、节点身份、交叉、相切、重叠与覆盖校验。成功结果的 `RoadGraphChangeSummary` 按 ID 排序列出创建/删除的 Node、Edge、Group；拒绝结果不修改图且返回结构化 `RoadPathSubmissionError`。

单删、整组删、原生拆分和共线合并都在完整 detach/替代 Edge 创建后调用一次提交清理。Debug 构建在事件发布前执行 `AssertInvariants`；复合操作先发布全部移除事件，再发布全部新增事件，事件处理器观察到的是最终一致图。

| 存档恢复阶段 | 行为 |
|---|---|
| `ParseAndValidateState` | 严格要求 `schemaVersion = 1`，拒绝未知字段、重复/冲突 ID、缺失引用、孤立节点、空 Group、Group 双向不一致、非法原生几何和无效 `nextID` |
| 提交恢复状态 | 只有全量预检成功后才清空活动图并装入临时 Node/Edge/Group；失败保持原图不变 |
| `RebuildNodeEdges` | 根据 `_edges` 重建 `GraphNode` 邻接表 |
| `RebuildSpatialIndex` | 清空并重新插入所有节点及每个原生几何段的 `EdgeGeometryRef` |

---

## 7. Road 输入、渲染与系统装配

### RoadConfig

**文件**：`Scripts/Road/RoadConfig.cs`
**继承**：`[GlobalClass] public partial class RoadConfig : Resource`

| 导出属性 | 签名 | 默认值 | 说明 |
|---|---|---|---|
| `CellSize` | `[Export] public float CellSize { get; set; } = 64f` | `64f` | 网格单元尺寸 |
| `RoadColor` | `[Export] public Color RoadColor { get; set; } = new("#37474F")` | `#37474F` | 统一道路颜色 |
| `RoadWidth` | `[Export] public float RoadWidth { get; set; } = 12f` | `12f` | 统一道路线宽 |
| `JunctionRadius` | `[Export] public float JunctionRadius { get; set; } = 10f` | `10f` | 节点圆半径，当前 `EdgeCount >= 2` 绘制 |
| `JunctionColor` | `[Export] public Color JunctionColor { get; set; } = new("#FFC107")` | `#FFC107` | 节点圆颜色 |
| `EndpointRadius` | `[Export] public float EndpointRadius { get; set; } = 6f` | `6f` | 端点圆半径，`0` 可隐藏 |
| `EndpointColor` | `[Export] public Color EndpointColor { get; set; } = new("#90A4AE")` | `#90A4AE` | 端点圆颜色 |
| `HoverHighlightColor` | `[Export] public Color HoverHighlightColor { get; set; } = new(1f, 0.8f, 0.2f, 0.6f)` | 半透明黄 | 拆除悬停高亮 |
| `HoverHighlightWidth` | `[Export] public float HoverHighlightWidth { get; set; } = 18f` | `18f` | 拆除悬停高亮宽度 |

### RoadBuilder

**文件**：`Scripts/Road/RoadBuilder.cs`
**继承**：`public partial class RoadBuilder : Node2D`

| 公开/导出成员 | 签名 | 说明 |
|---|---|---|
| `Config` | `[Export] public RoadConfig Config { get; set; } = null!` | 场景注入共享配置 |
| `SetGraph` | `public void SetGraph(RoadGraph graph)` | 注入数据层 |
| `_Ready` | `public override void _Ready()` | 获取相邻 `RoadRenderer`，校验 `Config` |
| `HandlePlaceInput` | `public void HandlePlaceInput(InputEvent @event)` | 左键按下开始拖拽，释放提交 |
| `_Process` | `public override void _Process(double delta)` | 拖拽时更新 8 方向投影，拆除工具时更新 hover |
| `HandleRemoveInput` | `public void HandleRemoveInput(InputEvent @event)` | 左键点击删除最近 `GraphEdge` |
| `CancelPlaceDrag` | `public void CancelPlaceDrag()` | 切出铺路工具时取消拖拽并清预览 |
| `SetRemoveHoverActive` | `public void SetRemoveHoverActive(bool active)` | 切入/切出拆除工具时开关 hover |

| 当前铺路行为 | 说明 |
|---|---|
| 拖拽语义 | 一次拖拽生成一条单方向直路，方向从 8 个方向中按投影选择 |
| 半格起点 | 起点不在整格时仅允许对角延伸，并反向定位整格 anchor |
| 提交 | `EndDragAndCommit()` 调用 `_graph.AddRoad(_dragStartPos, endPos, waypoints)` |
| 道路类型 | 第二代输入与数据层不包含 RoadType |
| 拆除 | 优先按 snap 位置查边，再按原始鼠标位置查边 |

### RoadRenderer

**文件**：`Scripts/Road/RoadRenderer.cs`
**继承**：`public partial class RoadRenderer : Node2D`

| 公开/导出成员 | 签名 | 说明 |
|---|---|---|
| `Config` | `[Export] public RoadConfig Config { get; set; } = null!` | 场景注入共享配置 |
| `PreviewFrom` | `public Vector2? PreviewFrom { get; set; }` | 施工预览起点 |
| `PreviewTo` | `public Vector2? PreviewTo { get; set; }` | 施工预览终点 |
| `HoveredEdgeID` | `public int? HoveredEdgeID { get; set; }` | 拆除工具悬停边 |
| `_Ready` | `public override void _Ready()` | 校验 `Config`，创建 junction 绘制层 |
| `SetGraph` | `public void SetGraph(RoadGraph graph)` | 订阅 `EdgeAdded`、`EdgeRemoved`、`GraphCleared` |
| `_Draw` | `public override void _Draw()` | 绘制拆除 hover 高亮和施工虚线预览 |

| 事件响应 | 行为 |
|---|---|
| `EdgeAdded` | `CreateEdgeLine(edge)`，创建 `Line2D`，更新节点层 |
| `EdgeRemoved` | 删除对应 `Line2D`，更新节点层 |
| `GraphCleared` | 清空所有 `Line2D`，用 `GetAllEdges()` 全量重建 |

当前 `CreateEdgeLine` 统一使用 `RoadConfig.RoadWidth` 和 `RoadConfig.RoadColor`。按 `RoadType` 分别绘制宽度、颜色或材质属于第三代，当前 `GraphEdge` 不包含类型字段。

### RoadSystem

**文件**：`Scripts/Road/RoadSystem.cs`
**继承**：`public partial class RoadSystem : Node2D`

| 成员 | 签名 | 说明 |
|---|---|---|
| `Graph` | `public RoadGraph Graph { get; private set; } = null!` | 当前路网数据层 |
| `Instance` | `public static RoadSystem Instance { get; private set; } = null!` | 单例引用 |
| `_Ready` | `public override void _Ready()` | 创建 `RoadGraph`，注入 renderer/builder，设置 `GridSystem.Config`，注册存档 |
| `_ExitTree` | `public override void _ExitTree()` | 注销 `RoadGraph` 并清理当前单例 |

`RoadSystem` 是场景侧装配根。它不直接处理输入、不直接绘制道路，也不持有旧 `RoadNetwork` 对象。

---

## 8. Tools 工具模块

### ToolType

**文件**：`Scripts/Tools/ToolType.cs`

| 枚举值 | 说明 |
|---|---|
| `Select` | 选择/空工具 |
| `Road` | 铺路工具 |
| `RoadRemove` | 拆路工具 |

### ToolManager

**文件**：`Scripts/Tools/ToolManager.cs`
**继承**：`public partial class ToolManager : Node2D`

| 成员 | 签名 | 说明 |
|---|---|---|
| `Instance` | `public static ToolManager Instance { get; private set; } = null!` | 单例引用 |
| `CurrentTool` | `public ToolType CurrentTool { get; set; }` | 切换工具，负责清理 Road/RoadRemove 状态 |
| `_Ready` | `public override void _Ready()` | 设置单例并获取 `../RoadSystem/RoadBuilder` |
| `_Input` | `public override void _Input(InputEvent @event)` | 只按当前工具把输入转发给 `RoadBuilder`；键盘工具和暂停动作由 `GameHUD` 处理 |

| 输入 | 行为 |
|---|---|
| 当前 `tool_select` / `tool_road` / `tool_remove` 绑定（默认 Q/R/E） | `GameHUD` 切换 `CurrentTool`；`ToolManager` 不解析按键 |
| 当前 `pause_menu` 绑定（默认 Escape） | 不改变工具；由 `GameHUD` 打开暂停菜单 |
| 当前工具为 `Road` | 转发到 `RoadBuilder.HandlePlaceInput(@event)` |
| 当前工具为 `RoadRemove` | 转发到 `RoadBuilder.HandleRemoveInput(@event)` |

---

## 9. UI 模块

### GameHUD

**文件**：`Scripts/UI/GameHUD.cs`
**继承**：`public partial class GameHUD : CanvasLayer`

| 公开/导出成员 | 签名 | 说明 |
|---|---|---|
| `Config` | `[Export] public RoadConfig Config { get; set; } = null!` | HUD 将道路配置分发给上下文和调试组件 |
| `_Ready` | `public override void _Ready()` | 作为命令中心组合协调器，解析子组件、确保本 HUD 的 `UIManager`、绑定组件事件 |
| `_Input` | `public override void _Input(InputEvent @event)` | 通过 `InputBindingManager` 处理当前暂停和工具动作 |
| `_Process` | `public override void _Process(double delta)` | 协调子组件刷新当前工具、catalog 上下文、调试指标和响应式布局 |

| UI/快捷键 | 当前调用 | 说明 |
|---|---|---|
| 当前暂停绑定 / 默认 Esc | `GameHUD._Input()` 打开 `PauseMenu` | 暂停场景树且保留当前工具；再次按当前绑定或“继续游戏”恢复 |
| 当前工具绑定 / 默认 Q/R/E | `GameHUD._Input()` 设置 `ToolManager.CurrentTool` | 模态菜单关闭时切换选择、铺路或拆路 |
| 铺路按钮 | `ConstructionDock` 的 `RoadToolButton` 调用 `ToolManager.CurrentTool = ToolType.Road` | 切换铺路工具，按钮来自 Roads catalog |
| 拆路 | `tool_remove` 动作或程序设置 `ToolManager.CurrentTool = ToolType.RoadRemove` | UI 显示内建中文文案和当前绑定；仍不提供 Roads 子菜单按钮 |
| 暂停菜单保存 | `OnPauseSave()` | `SaveManager.Instance.Save("autosave")` 并在菜单内回显结果 |
| 暂停菜单读档 | `OnPauseLoad()` | `SaveManager.Instance.Load("autosave")` 并在菜单内回显结果 |

| HUD 数据 | 所属组件 / 来源 |
|---|---|
| 建造分类和工具按钮 | `ConstructionDock` 读取 bundled Roads catalog，并写入 `ToolManager.Instance.CurrentTool` |
| catalog 上下文 | `ToolContextPanel` 读取当前 `ToolType`、`RoadConfig` 和 `ConstructionCategoryDefinition` |
| FPS | `DebugPanel` 读取 `Engine.GetFramesPerSecond()` |
| 鼠标格点 | `DebugPanel` 读取 `MainCamera.Instance.GetGlobalMousePosition()` + `GridSystem.SnapToGrid(...)` |
| 是否有节点 | `DebugPanel` 读取 `RoadGraph.FindClosestNode(snapped, Config.CellSize * 0.1f)` |
| Group/Edge/Node 数量 | `DebugPanel` 读取 `RoadSystem.Instance.Graph.GetAllGroups/Edges/Nodes().Count()` |

### Command Center UI Components

**文件**：`Scripts/UI/ConstructionDock.cs`, `ToolContextPanel.cs`, `DebugPanel.cs`, `PauseMenu.cs`

| 组件 | 说明 |
|---|---|
| `ConstructionDock` | 底部全宽五分类 CategoryBar 和 ToolTray；折叠高度 76px，展开高度 140px，由 64px 资产条加 76px 分类栏组成；Roads catalog 创建一个 `城市道路` 按钮；重复当前分类折叠/重开，不同分类切换内容并保持打开；没有当前工具标签或桌面宽度上限 |
| `ToolContextPanel` | 右侧只读上下文，Road 读取 catalog；Select / RoadRemove 使用内建玩家文案但不要求 submenu/catalog 资源 |
| `DebugPanel` | 默认折叠，拥有 FPS、鼠标格点、RoadGroup、GraphEdge、GraphNode 指标显示 |
| `PauseMenu` | 当前暂停动作打开的全屏模态菜单；暂停场景树而保持菜单输入，可继续、保存、读档、调整会话音频、持久化键位，或经确认返回主菜单/退出桌面 |

### UIManager

**文件**：`Scripts/UI/UIManager.cs`
**继承**：`public partial class UIManager : Node`

| 成员 | 签名 | 说明 |
|---|---|---|
| `IsModalActive` | `public bool IsModalActive => _modalStack.Count > 0` | 是否有模态面板 |
| `Register` | `public void Register(string name, Control panel)` | 注册面板 |
| `Unregister` | `public void Unregister(string name)` | 注销面板 |
| `Show` | `public void Show(string name)` | 显示面板 |
| `Hide` | `public void Hide(string name)` | 隐藏面板 |
| `Toggle` | `public void Toggle(string name)` | 切换可见性 |
| `IsVisible` | `public bool IsVisible(string name)` | 查询可见性 |
| `HideAll` | `public void HideAll()` | 隐藏所有已注册面板 |
| `PushModal` | `public void PushModal(string name)` | 显示并压入模态栈 |
| `PopModal` | `public void PopModal()` | 关闭最顶层模态面板 |
| `GetPanel` | `public T? GetPanel<T>(string name) where T : Control` | 获取已注册面板 |
| `GetPanel` | `public Control? GetPanel(string name)` | 供 GDScript/runtime tests 查询已注册面板 |

---

## 10. 数据流、事件流与存档流

### 铺路数据流

| 步骤 | 调用 | 数据变化 |
|---|---|---|
| 1 | `ToolManager._Input()` | 当前工具为 `Road` 时转发输入 |
| 2 | `RoadBuilder.HandlePlaceInput()` | 左键按下记录 `_dragStartPos`，释放提交 |
| 3 | `RoadBuilder._Process()` | `UpdateProjection()` 计算 8 方向预览 |
| 4 | `RoadBuilder.EndDragAndCommit()` | 生成 waypoints，不向数据层传递 RoadType |
| 5 | `RoadGraph.AddRoad(...)` | 创建/复用节点，拆分交点，跳过覆盖段，创建 group/edge |
| 6 | `RoadGraph.EdgeAdded` | 通知渲染器创建 `Line2D` |
| 7 | `GameHUD._Process()` -> `DebugPanel.UpdateMetrics()` | 调试组件轮询并显示 Group/Edge/Node 数量 |

### 拆路数据流

| 步骤 | 调用 | 数据变化 |
|---|---|---|
| 1 | `ToolManager.CurrentTool = ToolType.RoadRemove` | 开启 `RoadBuilder.SetRemoveHoverActive(true)` |
| 2 | `RoadBuilder._Process()` | `UpdateRemoveHover()` 更新 `RoadRenderer.HoveredEdgeID` |
| 3 | `RoadRenderer._Draw()` | 绘制 hover 高亮 |
| 4 | `RoadBuilder.HandleRemoveInput()` | 左键查最近边并调用 `RoadGraph.RemoveEdge(edge.ID)` |
| 5 | `RoadGraph.EdgeRemoved` | 通知渲染器释放对应 `Line2D` |

### 存档流

| 阶段 | 调用 | 内容 |
|---|---|---|
| 注册 | `MainCamera._Ready()` | `SaveManager.Instance.Register(this)` |
| 注册 | `RoadSystem._Ready()` | `SaveManager.Instance.Register(Graph)` |
| 注销 | `MainCamera._ExitTree()` | `SaveManager.Instance.Unregister(this)` |
| 注销 | `RoadSystem._ExitTree()` | `SaveManager.Instance.Unregister(Graph)` |
| 保存入口 | `PauseMenu` 的保存操作 | `SaveManager.Instance.Save("autosave")` |
| 保存文件 | `SaveManager.Save()` | 写 `camera.json`、`road_network.json`、`manifest.json` |
| 加载入口 | `PauseMenu` 的读档操作 | `SaveManager.Instance.Load("autosave")` |
| RoadGraph 恢复 | `RoadGraph.RestoreState(json)` | 清图、恢复实体、重建邻接、重建空间索引、触发 `GraphCleared` |
| 渲染恢复 | `RoadRenderer.OnGraphCleared()` | 清空并全量重建 `Line2D` |

---

## 11. 兼容性 DTO 词汇说明

| 旧词汇 | 当前运行时对应 | 状态 |
|---|---|---|
| `RoadNetworkData` | 旧路网结构的公开 DTO；当前 `RoadGraph` 实际使用私有 `RoadGraphSaveData` 作为存档根对象 | 当前未被 `RoadGraph` 直接使用，不是运行时 `RoadNetwork` 类 |
| `JunctionData` | `GraphNode` 存档行 | 仅 DTO 词汇 |
| `SegmentData` | `GraphEdge` 存档行 | 仅 DTO 词汇 |
| `RoadData` | `RoadGroup` 存档行 | 仅 DTO 词汇 |
| JSON 字段 `junctions` / `segments` / `roads` | legacy 公开 DTO 字段 | 当前私有 v2 payload 不读取这些字段 |
| JSON 字段 `nodes` / `edges` / `groups` | 当前 RoadGraph 私有存档字段 | 与 `schemaVersion = 1` 一同严格校验，不作为旧存档兼容层 |

文档中不再保留旧运行时 `RoadNetwork`、`Road`、`Segment`、`Junction` 的类章节。第三代若引入道路分级，必须同时定义新的提交 API、运行时字段、渲染规则、存档 schema 版本和迁移/拒绝策略，不能向第二代契约静默补回 `RoadType`。
