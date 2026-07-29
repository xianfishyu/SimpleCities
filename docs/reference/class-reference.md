# SimpleCities 类与 API 参考

> 最后更新：2026-07-17 | Godot 4.7 | Godot.NET.Sdk 4.7.0 | .NET 10.0 | C# 14.0 | Nullable enabled

本文档只覆盖项目自有 API：`Scripts/` 下 23 个 C# 文件和 `Shaders/MapTerrain.gdshader`。`addons/` 为第三方插件，不纳入本参考。

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

当前道路运行时模型已经从旧术语迁移到新术语：`RoadGraph` 是纯数据核心，`GraphNode` 是拓扑节点，`GraphEdge` 是几何边，`RoadGroup` 是一次铺路操作形成的边集合。旧 `RoadNetwork` / `Road` / `Segment` / `Junction` 不再是运行时类，只在存档 DTO 的类型名、属性名和 JSON 字段名中作为兼容词汇保留。

| 模块 | 文件 | 职责 |
|---|---|---|
| Core | `ISaveable.cs`, `SaveManager.cs`, `SaveJson.cs`, `SaveData.cs` | 独立 JSON 存档、manifest、DTO |
| Camera | `MainCamera.cs` | 2D 相机移动、缩放、相机存档 |
| Grid | `GridSystem.cs`, `MapBackground.cs`, `MapTerrain.gdshader` | 网格数学、背景 CanvasLayer、Shader 网格绘制 |
| Road data | `Direction.cs`, `GraphNode.cs`, `GraphEdge.cs`, `RoadGroup.cs`, `RoadType.cs`, `SpatialIndex.cs`, `RoadGraph.cs` | 方向、拓扑、路网、空间索引、持久化 |
| Road scene | `RoadBuilder.cs`, `RoadConfig.cs`, `RoadRenderer.cs`, `RoadSystem.cs` | 输入投影、共享配置、事件驱动渲染、依赖注入 |
| Tools | `ToolManager.cs`, `ToolType.cs` | 工具切换和输入转发 |
| UI | `GameHUD.cs`, `ConstructionDock.cs`, `ToolContextPanel.cs`, `DebugPanel.cs`, `SystemControls.cs`, `UIManager.cs` | 命令中心 HUD、建造坞、上下文、诊断、系统操作和面板管理 |

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
| 相机 | `MainCamera._Ready()` | 设置 `Instance`，记录初始位置，注册到 `SaveManager` |
| 道路系统 | `RoadSystem._Ready()` | 创建 `RoadGraph`，注入 `RoadRenderer` 和 `RoadBuilder`，设置 `GridSystem.Config`，注册 `RoadGraph` |
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
| `_Ready` | `public override void _Ready()` | 设置 `Instance` |
| `Register` | `public void Register(ISaveable saveable)` | 注册可存档系统，重复注册会被忽略 |
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
| `_Process` | `public override void _Process(double delta)` | 更新键盘移动、缩放和中键拖拽 |
| `_Input` | `public override void _Input(InputEvent @event)` | WASD、滚轮、中键输入 |
| `SaveFileName` | `public string SaveFileName => "camera"` | 相机存档文件名 |
| `CaptureState` | `public object CaptureState()` | 返回 `CameraData` |
| `RestoreState` | `public void RestoreState(string json)` | 从 `CameraData` 恢复 `Position` 和 `defaultScale`，并同步 `nextPos` |

| 输入动作 | 来源 | 作用 |
|---|---|---|
| `KeyBoard_MoveUp` / `Down` / `Left` / `Right` | `project.godot` | `Input.GetVector(...)` 平移相机 |
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
| `GraphNode` | `Edges` | `public IReadOnlyList<EdgeRef> Edges => _edges` | 邻接表只读视图 |
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
| `Points` | `public Vector2[] Points { get; }` | 中间途经点，不含端点 |
| `GroupID` | `public int GroupID { get; internal set; }` | 所属 `RoadGroup` |
| `Type` | `public RoadType Type { get; internal set; }` | 道路类型 |
| `Length` | `public float Length { get; }` | 几何长度 |
| 构造函数 | `public GraphEdge(int id, int nodeA, int nodeB, Vector2[] points, int groupID, RoadType type, float length)` | 创建边 |
| `GetFullPath` | `public Vector2[] GetFullPath(Func<int, GraphNode?> getNode)` | 返回 `[NodeA.Position, ...Points, NodeB.Position]`，端点缺失时返回 `Points` |

### RoadGroup

**文件**：`Scripts/Road/RoadGroup.cs`

| 成员 | 签名 | 说明 |
|---|---|---|
| `ID` | `public int ID { get; }` | 组 ID |
| `Type` | `public RoadType Type { get; internal set; }` | 组道路类型 |
| `EdgeIDs` | `public IReadOnlyCollection<int> EdgeIDs => _edgeIDs` | 组内边 ID |
| `EdgeCount` | `public int EdgeCount => _edgeIDs.Count` | 边数量 |
| `IsEmpty` | `public bool IsEmpty => _edgeIDs.Count == 0` | 是否为空 |
| 构造函数 | `public RoadGroup(int id, RoadType type = RoadType.Street)` | 默认类型 `Street` |

`AddEdge(int)` 和 `RemoveEdge(int)` 是 `internal`，只由 `RoadGraph` 更新。

### RoadType

**文件**：`Scripts/Road/RoadType.cs`

| 枚举值 | 数值 | 当前用途 |
|---|---:|---|
| `Dirt` | `0` | 数据和存档支持 |
| `Street` | `1` | 当前 `RoadBuilder` 固定创建的类型 |
| `Arterial` | `2` | 数据和存档支持 |
| `Highway` | `3` | 数据和存档支持 |

当前 `RoadType` 已贯穿 `RoadGroup`、`GraphEdge` 和存档往返。当前 UI 和 `RoadBuilder.EndDragAndCommit()` 固定调用 `AddRoad(..., RoadType.Street)`，`RoadRenderer` 也尚未按 `RoadType` 改变颜色、宽度或其他视觉样式。

### SpatialIndex

**文件**：`Scripts/Road/SpatialIndex.cs`

| 类型 | 公开成员 | 签名/值 | 说明 |
|---|---|---|---|
| `ISpatialRef` | `Position` | `Vector2 Position { get; }` | 空间位置 |
| `ISpatialRef` | `Kind` | `SpatialRefKind Kind { get; }` | 引用类别 |
| `SpatialRefKind` | 枚举值 | `Node`, `EdgePoint` | 节点或边途经点 |
| `NodeSpatialRef` | `NodeID` | `public int NodeID { get; }` | 节点 ID |
| `NodeSpatialRef` | `Position` | `public Vector2 Position { get; }` | 节点位置 |
| `NodeSpatialRef` | `Kind` | `public SpatialRefKind Kind => SpatialRefKind.Node` | 类别 |
| `NodeSpatialRef` | 构造函数 | `public NodeSpatialRef(int nodeID, Vector2 position)` | 创建节点引用 |
| `EdgePointRef` | `EdgeID` | `public int EdgeID { get; }` | 边 ID |
| `EdgePointRef` | `Position` | `public Vector2 Position { get; }` | 端点或 waypoint 位置 |
| `EdgePointRef` | `Kind` | `public SpatialRefKind Kind => SpatialRefKind.EdgePoint` | 类别 |
| `EdgePointRef` | 构造函数 | `public EdgePointRef(int edgeID, Vector2 position)` | 创建边点引用 |
| `UniformGrid` | 构造函数 | `public UniformGrid(float bucketSize)` | bucket 下限为 `1f` |
| `UniformGrid` | `Insert` | `public void Insert(ISpatialRef entity)` | 插入引用 |
| `UniformGrid` | `Remove` | `public void Remove(ISpatialRef entity)` | 按对象引用移除 |
| `UniformGrid` | `QueryRadius` | `public IEnumerable<ISpatialRef> QueryRadius(Vector2 center, float radius)` | 半径查询，桶过滤加精确距离 |
| `UniformGrid` | `Clear` | `public void Clear()` | 清空索引 |

空间索引是查询加速结构，不是权威数据源。`RoadGraph` 同步维护 `_nodes`、`_edges`、`_groups`、`_nodeRefs`、`_edgeRefs` 和 `_spatialIndex`。

### RoadGraph

**文件**：`Scripts/Road/RoadGraph.cs`
**类型**：`public class RoadGraph : ISaveable`

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
| `EdgeAdded` | `public event Action<GraphEdge>? EdgeAdded` | 新边创建后触发 |
| `EdgeRemoved` | `public event Action<GraphEdge>? EdgeRemoved` | 边删除后触发 |
| `GraphCleared` | `public event Action? GraphCleared` | 加载并重建后触发 |
| 构造函数 | `public RoadGraph()` | 使用默认 `IndexBucketSize` |
| 构造函数 | `public RoadGraph(float bucketSize)` | 指定空间索引 bucket |
| `AddRoad` | `public int AddRoad(Vector2 start, Vector2 end, Vector2[] waypoints, RoadType type = RoadType.Street)` | 添加折线路径，返回 group ID，失败或完全重复返回 `-1` |
| `RemoveEdge` | `public bool RemoveEdge(int edgeID)` | 删除单边并修复孤立节点与共线节点 |
| `RemoveRoadGroup` | `public bool RemoveRoadGroup(int groupID)` | 批量删除组内边，再统一 merge repair |
| `GetEdge` | `public GraphEdge? GetEdge(int edgeID)` | 取边 |
| `GetNode` | `public GraphNode? GetNode(int nodeID)` | 取节点 |
| `GetGroup` | `public RoadGroup? GetGroup(int groupID)` | 取组 |
| `FindClosestEdge` | `public GraphEdge? FindClosestEdge(Vector2 position, float maxRadius)` | 基于空间索引查最近边点 |
| `FindClosestNode` | `public GraphNode? FindClosestNode(Vector2 position, float maxRadius)` | 基于空间索引查最近节点 |
| `GetAllEdges` | `public IEnumerable<GraphEdge> GetAllEdges()` | 枚举边 |
| `GetAllNodes` | `public IEnumerable<GraphNode> GetAllNodes()` | 枚举节点 |
| `GetAllGroups` | `public IEnumerable<RoadGroup> GetAllGroups()` | 枚举道路组 |
| `CaptureState` | `public object CaptureState()` | 返回私有 `RoadGraphSaveData`，写入 version 2、NextID、junctions、segments、roads |
| `RestoreState` | `public void RestoreState(string json)` | 反序列化、清图、恢复实体、重建邻接和索引、触发 `GraphCleared` |

| `AddRoad` 关键阶段 | 行为 |
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

| 存档恢复阶段 | 行为 |
|---|---|
| `RestoreFromSavedData` | 先恢复节点和组，再恢复边；边类型依次取 `SegmentData.Type`、所属 `RoadGroup.Type`，两者都不可用时回退 `Street` |
| `RebuildNodeEdges` | 根据 `_edges` 重建 `GraphNode` 邻接表 |
| `RebuildSpatialIndex` | 清空并重新插入所有节点和边点引用 |
| `EnsureNextIDBeyondLoadedEntities` | 确保 `_nextID` 大于已加载实体最大 ID |

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
| 提交 | `EndDragAndCommit()` 调用 `_graph.AddRoad(_dragStartPos, endPos, waypoints, RoadType.Street)` |
| 道路类型 | 当前固定创建 `RoadType.Street` |
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

当前 `CreateEdgeLine` 只使用 `RoadConfig.RoadWidth` 和 `RoadConfig.RoadColor`。虽然 `GraphEdge.Type` 已存在，渲染器尚未按 `RoadType` 分别绘制宽度、颜色或材质。

### RoadSystem

**文件**：`Scripts/Road/RoadSystem.cs`
**继承**：`public partial class RoadSystem : Node2D`

| 成员 | 签名 | 说明 |
|---|---|---|
| `Graph` | `public RoadGraph Graph { get; private set; } = null!` | 当前路网数据层 |
| `Instance` | `public static RoadSystem Instance { get; private set; } = null!` | 单例引用 |
| `_Ready` | `public override void _Ready()` | 创建 `RoadGraph`，注入 renderer/builder，设置 `GridSystem.Config`，注册存档 |

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
| `_Input` | `public override void _Input(InputEvent @event)` | 快捷键切换工具并把输入转发给 `RoadBuilder` |

| 输入 | 行为 |
|---|---|
| `R` | `CurrentTool = ToolType.Road` |
| `E` | `CurrentTool = ToolType.RoadRemove` |
| `Escape` | `CurrentTool = ToolType.Select` |
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
| `_Input` | `public override void _Input(InputEvent @event)` | F5 保存，F9 加载 |
| `_Process` | `public override void _Process(double delta)` | 协调子组件刷新当前工具、catalog 上下文、调试指标和响应式布局 |

| UI/快捷键 | 当前调用 | 说明 |
|---|---|---|
| 选择按钮 | `ConstructionDock` 调用 `ToolManager.CurrentTool = ToolType.Select` | 切换选择工具 |
| 铺路按钮 | `ConstructionDock` 调用 `ToolManager.CurrentTool = ToolType.Road` | 切换铺路工具 |
| 拆路按钮 | `ConstructionDock` 调用 `ToolManager.CurrentTool = ToolType.RoadRemove` | 切换拆路工具 |
| 保存按钮 | `OnSave()` | `SaveManager.Instance.Save("autosave")` |
| 加载按钮 | `OnLoad()` | `SaveManager.Instance.Load("autosave")` |
| F5 | `OnSave()` | autosave 保存 |
| F9 | `OnLoad()` | autosave 加载 |

| HUD 数据 | 所属组件 / 来源 |
|---|---|
| 当前工具显示和工具按钮 | `ConstructionDock` 读取 bundled Roads catalog，并写入 `ToolManager.Instance.CurrentTool` |
| catalog 上下文 | `ToolContextPanel` 读取当前 `ToolType`、`RoadConfig` 和 `ConstructionCategoryDefinition` |
| FPS | `DebugPanel` 读取 `Engine.GetFramesPerSecond()` |
| 鼠标格点 | `DebugPanel` 读取 `MainCamera.Instance.GetGlobalMousePosition()` + `GridSystem.SnapToGrid(...)` |
| 是否有节点 | `DebugPanel` 读取 `RoadGraph.FindClosestNode(snapped, Config.CellSize * 0.1f)` |
| Group/Edge/Node 数量 | `DebugPanel` 读取 `RoadSystem.Instance.Graph.GetAllGroups/Edges/Nodes().Count()` |

### Command Center UI Components

**文件**：`Scripts/UI/ConstructionDock.cs`, `ToolContextPanel.cs`, `DebugPanel.cs`, `SystemControls.cs`

| 组件 | 说明 |
|---|---|
| `ConstructionDock` | 底部 Roads 分类、ToolTray、工具按钮创建和当前工具显示；工具按钮来自 bundled Roads catalog |
| `ToolContextPanel` | 右侧只读 catalog 上下文，读取工具显示名、说明、快捷键和道路配置 |
| `DebugPanel` | 默认折叠，拥有 FPS、鼠标格点、RoadGroup、GraphEdge、GraphNode 指标显示 |
| `SystemControls` | 独立 Save / Load 操作区，显示成功或失败状态 |

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
| 4 | `RoadBuilder.EndDragAndCommit()` | 生成 waypoints，固定 `RoadType.Street` |
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
| 保存入口 | `GameHUD.OnSave()` 或 F5 | `SaveManager.Instance.Save("autosave")` |
| 保存文件 | `SaveManager.Save()` | 写 `camera.json`、`road_network.json`、`manifest.json` |
| 加载入口 | `GameHUD.OnLoad()` 或 F9 | `SaveManager.Instance.Load("autosave")` |
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
| JSON 字段 `junctions` | 节点列表 | 为兼容旧存档保留 |
| JSON 字段 `segments` | 边列表 | 为兼容旧存档保留 |
| JSON 字段 `roads` | 道路组列表 | 为兼容旧存档保留 |

文档中不再保留旧运行时 `RoadNetwork`、`Road`、`Segment`、`Junction` 的类章节。若后续添加道路分级 UI，应从工具或 HUD 把用户选择传入 `RoadGraph.AddRoad(..., RoadType type)`，并同步扩展 `RoadRenderer` 的视觉规则，而不是只修改 `RoadType` 枚举或存档 DTO。
