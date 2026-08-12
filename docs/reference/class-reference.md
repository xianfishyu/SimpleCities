# SimpleCities 类与 API 参考

> 最后更新：2026-08-13 | Godot 4.7 | Godot.NET.Sdk 4.7.0 | .NET 10.0 | C# 14.0 | Nullable enabled

本文档聚焦项目自有 API；当前事实源包括 `Scripts/` 下 60 个 C# 文件和 `Shaders/MapTerrain.gdshader`。`addons/` 为第三方插件，不纳入本参考。

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
| Core | `ISaveable.cs`, `SaveManager.cs`, `SaveSlotStore.cs`, `SaveJson.cs`, `SaveData.cs` | Godot 存档适配、纯文件存储、manifest、DTO |
| Camera | `MainCamera.cs` | 2D 相机移动、缩放和可扩展状态捕获；V2 槽不选择相机 |
| Grid | `GridSystem.cs`, `MapBackground.cs`, `MapTerrain.gdshader` | 网格数学、背景 CanvasLayer、Shader 网格绘制 |
| Road data | `Direction.cs`, `GraphNode.cs`, `GraphEdge.cs`, `RoadGroup.cs`, `RoadPath.cs`, `SpatialIndex.cs`, `RoadGraph*.cs`, `Geometry/*.cs` | 输入方向、拓扑、原生几何、空间索引、提交与持久化 |
| Road scene | `RoadBuilder.cs`, `Input/*.cs`, `RoadConfig.cs`, `RoadRenderer.cs`, `RoadSystem.cs` | 输入生命周期、可替换投影策略、共享配置、事件驱动渲染、依赖注入 |
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

`IPreparedSaveable : ISaveable` 额外提供 `PrepareRestoreState(string)` 与 `RestorePreparedState(object)`。`SaveSlotStore.Load` 先读取并解析整槽 JSON，再准备全部实现，最后才提交；`RoadGraph` 和 `MainCamera` 均实现该两阶段契约。

当前注册实现：`RoadGraph.SaveFileName == "road_network"`，`MainCamera.SaveFileName == "camera"`。

### SaveManager

**文件**：`Scripts/Core/SaveManager.cs`
**继承**：`public partial class SaveManager : Node`

存档契约的详细说明见 [存档系统当前参考](save-system-plan.md)。本文只保留 API 速查和当前实现摘要。

| 成员 | 签名 | 说明 |
|---|---|---|
| `Instance` | `public static SaveManager Instance { get; private set; }` | Autoload 单例 |
| `AutosaveSlotID` | `public const string AutosaveSlotID = "autosave"` | 保留的自动存档内部 ID |
| `AutosaveDisplayName` | `public const string AutosaveDisplayName = "自动存档"` | 保留自动槽的玩家可见名称 |
| `CurrentSlotID` | `public string CurrentSlotID { get; private set; } = AutosaveSlotID` | 当前槽位内部 ID |
| `RegisteredSaveableCount` | `public int RegisteredSaveableCount` | 当前活动注册数量 |
| V2 持久化配置 | `V2SaveFileNames = ["road_network"]` | 第二代只选择 RoadGraph；相机保持注册但不进入 V2 槽 |
| `_Ready` | `public override void _Ready()` | 设置 `Instance` |
| `Register` | `public bool Register(ISaveable saveable)` | 同一对象幂等；拒绝另一活动对象使用相同 `SaveFileName` |
| `Unregister` | `public bool Unregister(ISaveable saveable)` | 移除离开场景树的可存档对象 |
| `Save` | `public bool Save(string slotID = AutosaveSlotID)` | 按内部 ID 覆盖已存在槽位；首次只允许保留的 autosave |
| `SaveAutosave` | `public bool SaveAutosave()` | 覆盖保留自动槽，不改变玩家当前选中的手动槽 |
| `SaveAs` | `public bool SaveAs(string displayName)` | 以玩家可见名称创建具有独立安全 ID 的手动槽位 |
| `Load` | `public bool Load(string slotID = AutosaveSlotID)` | 按内部 ID 完成 manifest、全部文件和临时模型预检后提交恢复 |
| `SaveSlotExists` | `public bool SaveSlotExists(string slotID)` | 按内部 ID 检查 manifest 是否存在 |
| `ListSlots` | `public IReadOnlyList<SaveSlotSummary> ListSlots()` | 无需加载业务 JSON 即可列举有效及损坏槽摘要 |
| `DeleteSlot` | `public bool DeleteSlot(string slotID)` | 按内部 ID 递归删除非空槽位；当前槽被删时回到 `autosave` |

| 存档规则 | 当前实现 |
|---|---|
| 基础目录 | 编辑器使用全局化的 `res://saves/<slotID>/`；导出版本使用 Godot 可写用户目录 `user://saves/<slotID>/` |
| 命名边界 | 目录只使用受限内部 ID；玩家显示名只进入 manifest，最大 128 个 UTF-16 字符 |
| 单系统文件 | `<SaveFileName>.json` |
| 写入策略 | 捕获与序列化全部完成后写同级 `.staging` 目录；旧槽移到 `.backup` 后切换完整目录，失败恢复旧槽，读写入口自动恢复崩溃残留 |
| Manifest | `manifest.json`，字段来自 `ManifestData` |
| V2 业务范围 | 新槽有且只要求 `road_network.json`；未来配置可增加独立系统 JSON，无需修改 RoadGraph DTO |
| 加载策略 | manifest、必需文件、JSON 语法及 `IPreparedSaveable` 临时模型全部成功后才进入提交阶段 |
| 错误处理 | 捕获异常，`GD.PushError(...)`，返回 `false` |

`SaveSlotStore` 是不依赖 Godot Node 的内部文件存储边界，负责生成 `manual-<GUID>` ID、约束路径、整槽 staging/backup 发布、事务残留恢复、读写 manifest、存在性检查和递归删除；事务目录使用保留名称且不会进入 `ListSlots`。`SaveManager` 负责注册生命周期、Godot 日志和 `CurrentSlotID`。隔离目录 xUnit 直接验证 `SaveSlotStore`，Godot 运行时契约验证适配层。

### AutosaveController

**文件**：`Scripts/Core/AutosaveController.cs`
**继承**：`public partial class AutosaveController : Node`

| 成员 | 签名 | 说明 |
|---|---|---|
| `IntervalSeconds` | `[Export] public double IntervalSeconds { get; set; } = 300d` | 自动存档周期；只接受正有限值 |
| `AutosaveEnabled` | `[Export] public bool AutosaveEnabled { get; set; } = true` | 场景进入时是否启动周期调度 |
| `SetAutosaveEnabled` | `public void SetAutosaveEnabled(bool enabled)` | 启停周期；重新启用时从完整周期开始计时 |
| `RunAutosaveNow` | `public bool RunAutosaveNow()` | 立即调用 `SaveManager.SaveAutosave()` 并更新计数 |
| 结果状态 | `AttemptCount`、`SuccessfulSaveCount`、`FailedSaveCount`、`LastAttemptSucceeded` | 当前场景生命周期内的自动保存结果 |
| `AutosaveCompleted` | `signal(bool success)` | 每次周期或立即尝试完成后发出 |

该节点挂载在 `MapTest`，内部 `Timer` 继承场景树暂停状态；暂停菜单打开时周期不推进，离开游戏场景后计时器随节点释放。

### InputBindingManager

**文件**：`Scripts/Core/InputBindingManager.cs`
**继承**：`public partial class InputBindingManager : Node`

| 成员 | 签名 | 说明 |
|---|---|---|
| `Instance` | `public static InputBindingManager Instance { get; private set; }` | Autoload 单例 |
| `Definitions` | `public static IReadOnlyList<BindingDefinition> Definitions` | WASD、Q/R/E、Z/Y 编辑和暂停动作目录 |
| `EditUndoAction` / `EditRedoAction` | `"edit_undo"` / `"edit_redo"` | 默认 Z/Y 的道路编辑撤销与重做动作名 |
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
| `ManifestData` | `public int? SchemaVersion { get; set; }` | `schemaVersion` | 反序列化无默认值；保存入口显式写入 `1` |
| `ManifestData` | `public string SlotID { get; set; }` | `slotId` | 内部目录标识 |
| `ManifestData` | `public string DisplayName { get; set; }` | `displayName` | 玩家可见名称，不参与路径计算 |
| `ManifestData` | `public string Timestamp { get; set; }` | `timestamp` | `""` |
| `ManifestData` | `public string CityName { get; set; }` | `cityName` | `"Unknown City"` 占位值 |
| `ManifestData` | `public long? Population { get; set; }` | `population` | `null` 表示暂无数据源 |
| `ManifestData` | `public decimal? Funds { get; set; }` | `funds` | `null` 表示暂无数据源 |
| `ManifestData` | `public string? ThumbnailFile { get; set; }` | `thumbnailFile` | `null` 表示使用 UI 占位图 |
| `ManifestData` | `public List<string> Files { get; set; }` | `files` | 文件名列表 |
| `SaveSlotSummary` | `SlotID`、`DisplayName`、`SavedAtUtc`、城市元数据、`ThumbnailPath`、`Files`、`IsValid`、`Error`、`IsAutosave` | 非 JSON 摘要 | `IsAutosave` 只按保留内部 ID 判定；有效槽按 UTC 时间倒序并以 ID 稳定排序，损坏槽排在末尾 |
| `CameraData` | `public float PositionX { get; set; }` | `positionX` | 相机 X |
| `CameraData` | `public float PositionY { get; set; }` | `positionY` | 相机 Y |
| `CameraData` | `public float Zoom { get; set; }` | `zoom` | 相机缩放目标 |

道路 V2 的 Node/Edge/Group 与原生几何 DTO 是 `RoadGraph.Persistence.cs` 的私有存档边界；旧 `RoadNetworkData`、`JunctionData`、`SegmentData`、`RoadData` 和 `Vector2Data` 已删除，不再构成公开兼容 schema。

---

## 4. MainCamera

**文件**：`Scripts/MainCamera.cs`
**继承**：`public partial class MainCamera : Camera2D, IPreparedSaveable`

| 导出成员 | 签名 | 默认值 | 说明 |
|---|---|---|---|
| `defaultScale` | `[Export] private float defaultScale = 1f` | `1f` | 目标缩放，可由相机自身捕获；V2 槽不选择相机状态 |
| `scaleFactor` | `[Export] public float scaleFactor = 0.125f` | `0.125f` | 鼠标滚轮缩放因子 |
| `minScale` | `[Export] public float minScale = 0.125f` | `0.125f` | 最小目标缩放；支持六位小数精度 |
| `maxScale` | `[Export] public float maxScale = 4f` | `4f` | 最大目标缩放 |
| `smoothing` | `[Export] private float smoothing = 0.25f` | `0.25f` | 参考帧率下的缩放平滑权重 |
| `referenceFps` | `[Export] private float referenceFps = 60f` | `60f` | 缩放平滑的参考帧率 |
| `panSpeed` | `[Export] private float panSpeed = 2048f` | `2048f` | 缩放为 1 时的基础屏幕移动速度，单位为像素/秒 |
| `zoomInfluence` | `[Export] private float zoomInfluence = 0.75f` | `0.75f` | 世界速度恒定与屏幕速度恒定之间的混合比例 |
| `accelerationTime` | `[Export] private float accelerationTime = 0.175f` | `0.175f` | 达到约 95% 目标移动速度所需秒数 |
| `decelerationTime` | `[Export] private float decelerationTime = 0.175f` | `0.175f` | 松键后消除约 95% 移动速度所需秒数 |

| 公开成员 | 签名 | 说明 |
|---|---|---|
| `Instance` | `public static MainCamera Instance { get; private set; }` | 单例引用 |
| `_Ready` | `public override void _Ready()` | 规范化缩放配置、设置单例并注册到 `SaveManager` |
| `_ExitTree` | `public override void _ExitTree()` | 从 `SaveManager` 注销并清理当前单例 |
| `_Process` | `public override void _Process(double delta)` | 更新缩放、键盘移动和中键拖拽 |
| `_UnhandledInput` | `public override void _UnhandledInput(InputEvent @event)` | 处理未被 UI 消费的 WASD、滚轮和中键输入 |
| `SaveFileName` | `public string SaveFileName => "camera"` | 相机扩展文件名；当前 V2 配置不选择 |
| `CaptureState` | `public object CaptureState()` | 返回 `CameraData` |
| `PrepareRestoreState` | `public object PrepareRestoreState(string json)` | 解析并校验有限坐标与正缩放，不修改相机 |
| `RestorePreparedState` | `public void RestorePreparedState(object preparedState)` | 提交已准备的 `CameraData`，同步 `Position` 和缩放目标，并清空移动速度与缩放锚点 |
| `RestoreState` | `public void RestoreState(string json)` | 兼容入口，依次调用准备与提交 |

| 输入动作 | 来源 | 作用 |
|---|---|---|
| `KeyBoard_MoveUp` / `Down` / `Left` / `Right` | `InputBindingManager`，默认 W/A/S/D | `Input.GetVector(...)` 平移相机 |
| `MouseButton.WheelUp` | `_UnhandledInput` | 以鼠标视口位置为锚，将缩放目标乘以 `1 + scaleFactor` 并钳制到范围 |
| `MouseButton.WheelDown` | `_UnhandledInput` | 以鼠标视口位置为锚，将缩放目标乘以 `1 - scaleFactor` 并钳制到范围 |
| `MouseButton.Middle` | `_UnhandledInput` | 按鼠标相对位移和当前缩放拖拽相机；拖拽期间清空键盘移动速度 |

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
**类型**：`public partial class RoadGraph : IPreparedSaveable`

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
| `RemoveEdges` | `public bool RemoveEdges(IEnumerable<int>? edgeIDs)` | 对 ID 去重排序，跳过失效目标并在一次事务中删除全部有效 Edge |
| `RemoveRoadGroup` | `public bool RemoveRoadGroup(int groupID)` | 按 Edge ID 稳定顺序批量 detach，一次清理后发布事件，不触发合并 |
| `GetEdge` | `public GraphEdge? GetEdge(int edgeID)` | 取边 |
| `GetNode` | `public GraphNode? GetNode(int nodeID)` | 取节点 |
| `GetGroup` | `public RoadGroup? GetGroup(int groupID)` | 取组 |
| `FindClosestEdge` | `public GraphEdge? FindClosestEdge(Vector2 position, float maxRadius)` | 从原生几何空间候选中计算权威最近点；等距时选较小 Edge ID |
| `FindEdgeIDsNear` | `public IReadOnlyList<int> FindEdgeIDsNear(Vector2 position, float radius)` | 返回与圆形命中范围相交的原生几何 Edge ID 稳定序列 |
| `FindEdgeIDsIntersecting` | `public IReadOnlyList<int> FindEdgeIDsIntersecting(Rect2 bounds)` | 以空间候选和原生几何/矩形边界精确过滤返回稳定 Edge ID 序列 |
| `FindClosestNode` | `public GraphNode? FindClosestNode(Vector2 position, float maxRadius)` | 基于空间索引查最近节点 |
| `GetAllEdges` | `public IEnumerable<GraphEdge> GetAllEdges()` | 返回调用时的边稳定快照 |
| `GetAllNodes` | `public IEnumerable<GraphNode> GetAllNodes()` | 返回调用时的节点稳定快照 |
| `GetAllGroups` | `public IEnumerable<RoadGroup> GetAllGroups()` | 返回调用时的道路组稳定快照 |
| `CaptureState` | `public object CaptureState()` | 返回私有 `RoadGraphSaveData`，写入 `schemaVersion = 1`、`nextID`、`nodes`、`edges`、`groups` |
| `PrepareRestoreState` | `public object PrepareRestoreState(string json)` | 构造并全量校验私有临时图，不修改活动图 |
| `RestorePreparedState` | `public void RestorePreparedState(object preparedState)` | 提交临时图、重建邻接与索引并触发 `GraphCleared` |
| `RestoreState` | `public void RestoreState(string json)` | 兼容入口，依次调用准备与提交 |

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

单删、任意 Edge 集合、整组删、原生拆分和共线合并都在完整 detach/替代 Edge 创建后调用一次提交清理。批量入口忽略重复和已经失效的 ID；Debug 构建在事件发布前执行 `AssertInvariants`，复合操作先发布全部移除事件，再发布全部新增事件，事件处理器观察到的是最终一致图。

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
| `CurveDisplayTolerance` | `[Export] public float CurveDisplayTolerance { get; set; } = 0.25f` | `0.25f` | 原生曲线生成显示折线时允许的最大世界空间误差 |
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
| `IsPlacing` / `FixedCornerCount` / `CurrentDraft` | 只读属性 | 当前连续铺路会话状态、已固定拐点数和完整组合草稿 |
| `IsRemoving` | `public bool IsRemoving { get; }` | 当前是否持有尚未提交的拆除选择会话 |
| `CanUndo` / `CanRedo` | 只读属性 | 当前是否存在可撤销或可重做的成功道路编辑 |
| `SetGraph` | `public void SetGraph(RoadGraph graph)` | 取消活动会话、替换数据层并为新图建立独立编辑历史 |
| `SetInputStrategy` | `public void SetInputStrategy(IRoadInputStrategy inputStrategy)` | 取消当前会话并替换输入策略 |
| `_Ready` | `public override void _Ready()` | 获取相邻 `RoadRenderer`，校验 `Config`，按需创建默认米字型策略 |
| `HandlePlaceInput` | `public void HandlePlaceInput(InputEvent @event)` | 处理旧式拖拽和点击式连续会话的移动、拐点、确认、回退与取消 |
| `BeginPlace` | `public bool BeginPlace(Vector2 pointerPosition)` | 通过策略吸附起点并建立空的 `RoadPlacementSession` |
| `UpdatePlace` | `public void UpdatePlace(Vector2 pointerPosition)` | 移动当前末端并更新完整组合预览 |
| `AddPlacePoint` / `RemoveLastPlacePoint` | `public bool ...(Vector2 pointerPosition)` | 固定新拐点或回退最后一个固定拐点 |
| `ConfirmPlace` | `public bool ConfirmPlace(Vector2 pointerPosition)` | 只经一次 `RoadGraph.SubmitPath` 确认完整草稿；拒绝时保留会话 |
| `CommitPlace` | `public bool CommitPlace(Vector2 pointerPosition)` | 兼容既有调用的 `ConfirmPlace` 别名 |
| `CancelPlaceSession` | `public void CancelPlaceSession()` | 取消完整会话并清空预览，不修改图 |
| `CancelPlaceDrag` | `public void CancelPlaceDrag()` | 兼容既有调用的取消别名 |
| `_Process` | `public override void _Process(double delta)` | 仅在拆除工具活动时更新 hover；铺路由输入事件驱动 |
| `HandleRemoveInput` | `public void HandleRemoveInput(InputEvent @event)` | 处理连续轨迹、Shift 矩形框选、松开提交和右键取消 |
| `BeginRemove` / `UpdateRemove` | `public bool/void ...(Vector2 pointerPosition, ...)` | 建立并更新只读图的拆除选择会话 |
| `ConfirmRemove` | `public bool ConfirmRemove(Vector2 pointerPosition)` | 将稳定 Edge ID 集一次性交给 `RoadGraph.RemoveEdges` |
| `CancelRemoveSession` | `public void CancelRemoveSession()` | 取消选择并清空预览，不修改图 |
| `SetRemoveHoverActive` | `public void SetRemoveHoverActive(bool active)` | 切入/切出拆除工具时开关 hover；切出时取消选择 |
| `UndoLastEdit` / `RedoLastEdit` | `public bool ...()` | 先取消尚未提交的铺路/拆路会话，再恢复上一/下一提交状态 |
| `GetUndoEditCount` / `GetRedoEditCount` | `public int ...()` | 供运行时契约与诊断读取两侧历史数量 |

| 当前铺路行为 | 说明 |
|---|---|
| 输入语义 | 按住拖拽后释放保持单段提交；点击起点进入连续会话，左键固定拐点，Enter/双击确认，右键回退或零段取消 |
| 半格起点 | `SquareEightRoadInputStrategy` 对偏移起点只允许对角延伸，并反向定位整格 anchor |
| 组合与提交 | `RoadPlacementSession` 保留每段策略草稿的原生几何；`ConfirmPlace()` 把完整 `RoadPath` 一次性交给 `_graph.SubmitPath(...)` |
| 失败行为 | 无有效段时不提交；RoadGraph 拒绝时图不变且会话/完整预览保留，可继续调整或取消 |
| 道路类型 | 第二代输入与数据层不包含 RoadType |
| 拆除 | 简单点击删除单 Edge；普通左键拖动累积轨迹命中，`Shift+左键` 动态框选，松开后批量提交；右键或切出工具取消 |
| 编辑历史 | 成功的 `SubmitPath` / `RemoveEdges` 状态变化进入容量 64 的历史；失败或无变化不入栈，新成功编辑清空重做栈 |

### Road 输入策略

**文件**：`Scripts/Road/Input/IRoadInputStrategy.cs`、`RoadPathDraft.cs`、`RoadPlacementSession.cs`、`RoadRemovalSession.cs`、`SquareEightRoadInputStrategy.cs`、`TriangularThreeRoadInputStrategy.cs`、`HexSixRoadInputStrategy.cs`

| 类型/成员 | 签名 | 说明 |
|---|---|---|
| `IRoadInputStrategy.InteractionRadius` | `float InteractionRadius { get; }` | 当前策略用于道路起点吸附和拆除命中的半径 |
| `IRoadInputStrategy.SnapPointer` | `Vector2 SnapPointer(Vector2 worldPosition)` | 把世界指针映射到策略定义的吸附点 |
| `IRoadInputStrategy.BuildDraft` | `RoadPathDraft BuildDraft(Vector2 startPosition, Vector2 pointerPosition)` | 生成预览点和可选的权威 `RoadPath` |
| `RoadPathDraft` | `public sealed class RoadPathDraft` | 防御性复制预览点；`Path == null` 表示当前不可提交；`FromPolyline` 把连续预览点转换为原生 line 段 |
| `RoadPlacementSession` | `public sealed class RoadPlacementSession` | 组合已固定草稿和可移动末端；支持增加/回退拐点并保持完整 `PreviewPoints` 与原生 `RoadPath` |
| `RoadRemovalSession` | `public sealed class RoadRemovalSession` | 维护排序去重的 Edge ID；连续模式累积轨迹命中，矩形模式按当前框重新生成选择，不直接写图 |
| `SquareEightRoadInputStrategy` | `public sealed class SquareEightRoadInputStrategy : IRoadInputStrategy` | 封装方格吸附、八方向投影、半格对角约束和逐格原生直线段 |
| `TriangularThreeRoadInputStrategy` | `public sealed class TriangularThreeRoadInputStrategy : IRoadInputStrategy` | 吸附到三角单元中心；主/次中心分别有 3 个跨边邻居，长路径交替两组邻接 |
| `HexSixRoadInputStrategy` | `public sealed class HexSixRoadInputStrategy : IRoadInputStrategy` | pointy-top 六边形单元中心轴向取整；沿 6 个等长方向投影 |

`RoadBuilder` 不直接引用 `Direction`、`DirectionUtil`、`GridSystem` 或 `CellSize`，也不包含三角形/六边形条件分支。当前玩家默认仍使用米字型策略；另外两种实现用于自动化验证可替换性。三者只需返回连续的 `RoadPathDraft`，RoadGraph 不感知网格类型。

### RoadEditHistory

**文件**：`Scripts/Road/Input/RoadEditHistory.cs`
**类型**：`public sealed class RoadEditHistory : IDisposable`

| 成员 | 签名 | 说明 |
|---|---|---|
| `DefaultCapacity` | `public const int DefaultCapacity = 64` | 默认最多保留的成功编辑数量 |
| `CanUndo` / `CanRedo` | 只读属性 | 对应历史栈是否非空 |
| `UndoCount` / `RedoCount` | 只读属性 | 当前两侧事务数 |
| `Execute` | `public bool Execute(Func<bool> edit)` | 捕获严格 RoadGraph 前后状态；只记录产生状态变化的成功编辑 |
| `Undo` / `Redo` | `public bool ...()` | 校验当前图仍匹配历史边界，再经 `RoadGraph.RestoreState` 恢复完整状态 |
| `Clear` | `public void Clear()` | 清空两侧历史 |

历史快照使用 `SaveJson.Serialize(graph.CaptureState())`，因此恢复会保留 Node/Edge/Group ID、原生几何、Group 成员关系和 `_nextID`，但会重建运行时实体对象。`GraphCleared` 的外部恢复会立即清空历史；其他外部修改在撤销、重做或下一次编辑尝试时被检测并使旧历史失效。编辑抛异常或返回失败却修改图时会先恢复事务前状态；失败编辑本身不进入历史。

### RoadGeometryDisplaySampler

**文件**：`Scripts/Road/RoadGeometryDisplaySampler.cs`
**类型**：`public static class RoadGeometryDisplaySampler`

| 成员 | 签名 | 说明 |
|---|---|---|
| `DefaultTolerance` | `public const float DefaultTolerance = 0.25f` | 默认世界空间显示误差；在当前相机最大 4x 缩放下约为 1 像素 |
| `MaxSubdivisionDepth` | `public const int MaxSubdivisionDepth = 16` | 自适应细分深度上界 |
| `SampleSegment` | `public static Vector2[] SampleSegment(RoadGeometrySegment geometry, float tolerance = ...)` | 采样单个原生段；line 保持精确两点 |
| `SampleSegments` | `public static Vector2[] SampleSegments(IEnumerable<RoadGeometrySegment?> geometries, float tolerance = ...)` | 采样连续复合路径并去除相邻段重复连接点 |

采样器只调用原生几何的 `GetPosition()` 和 `Split()`，使用四分之一、中点、四分之三点到端点弦的距离以及弧长/弦长差判断平坦度。每个源段最终以权威 `End` 封口，避免解析拆分的微小端点残差进入显示点列；输入几何和控制参数始终保持不变。

### RoadRenderer

**文件**：`Scripts/Road/RoadRenderer.cs`
**继承**：`public partial class RoadRenderer : Node2D`

| 公开/导出成员 | 签名 | 说明 |
|---|---|---|
| `Config` | `[Export] public RoadConfig Config { get; set; } = null!` | 场景注入共享配置 |
| `PreviewPoints` | `public Vector2[] PreviewPoints { get; set; }` | 防御性复制的完整施工预览点列 |
| `GetPreviewPointCount` / `GetPreviewPoint` | 运行时查询方法 | Godot 契约和调试调用读取当前完整预览 |
| `RemovalPreviewEdgeIDs` | `public int[] RemovalPreviewEdgeIDs { get; set; }` | 防御性保存并排序去重的拆除预览 Edge ID |
| `RemovalSelectionBounds` | `public Rect2? RemovalSelectionBounds { get; set; }` | 矩形拆除选择的当前世界坐标边界 |
| `GetRemovalPreviewEdgeCount` | `public int GetRemovalPreviewEdgeCount()` | Godot 运行时契约读取拆除预览数量 |
| `GetRenderedEdgeCount` | `public int GetRenderedEdgeCount()` | Godot 运行时契约与诊断读取当前已缓存道路数量 |
| `GetRenderedPointCount` / `GetRenderedPoint` | 运行时查询方法 | Godot 契约读取指定 Edge 的确定显示点列 |
| `GetStaticRenderNodeCount` | `public int GetStaticRenderNodeCount()` | 返回固定的道路 mesh 与节点 MultiMesh 子节点数 2 |
| `GetRoadMeshVertexCount` | `public int GetRoadMeshVertexCount()` | Godot 契约读取连续道路 ribbon 顶点数 |
| `HoveredEdgeID` | `public int? HoveredEdgeID { get; set; }` | 拆除工具悬停边 |
| `_Ready` | `public override void _Ready()` | 校验 `Config`，创建道路 `MeshInstance2D` 与节点 `MultiMeshInstance2D` |
| `SetGraph` | `public void SetGraph(RoadGraph graph)` | 订阅 `EdgeAdded`、`EdgeRemoved`、`GraphCleared` |
| `_Draw` | `public override void _Draw()` | 绘制拆除 hover/稳定选择/矩形框线和完整多段施工虚线预览 |

| 事件响应 | 行为 |
|---|---|
| `EdgeAdded` | 缓存 Edge 的确定显示点列，安排同一事件循环合并的静态批次重建 |
| `EdgeRemoved` | 删除对应缓存点列，安排同一事件循环合并的静态批次重建 |
| `GraphCleared` | 清空缓存，用 `GetAllEdges()` 重新采样并全量重建批次 |

当前 `CacheEdgePoints` 用 `RoadGeometryDisplaySampler` 从 `GraphEdge.GeometrySegments` 生成缓存点列；拆除高亮复用同一点列，`RoadBuilder` 对有效原生草稿也使用相同采样入口。`AppendRoadRibbon` 为每个点生成共享左右边界并把全部 Edge 合成一个抗锯齿 `ArrayMesh`，端点/交叉口写入一个圆形 shader `MultiMesh`。Edge 增删事件通过 `ScheduleStaticBatchRebuild` 在同一事件循环中合并，`GraphCleared` 仍同步完成全量重建。显示点列不写回图或存档，缩放与重建不会改变控制参数。道路仍统一使用 `RoadConfig.RoadWidth` 和 `RoadConfig.RoadColor`；按 `RoadType` 分批绘制宽度、颜色或材质属于第三代，当前 `GraphEdge` 不包含类型字段。

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
| `UndoRoadEdit` / `RedoRoadEdit` | `public bool ...()` | 委托 `RoadBuilder` 执行道路编辑撤销/重做，不解析具体按键 |
| `CanUndoRoadEdit` / `CanRedoRoadEdit` | `public bool ...()` | 查询当前道路历史能力 |

| 输入 | 行为 |
|---|---|
| 当前 `tool_select` / `tool_road` / `tool_remove` 绑定（默认 Q/R/E） | `GameHUD` 切换 `CurrentTool`；`ToolManager` 不解析按键 |
| 当前 `edit_undo` / `edit_redo` 绑定（默认 Z/Y） | `GameHUD` 调用 `ToolManager.UndoRoadEdit()` / `RedoRoadEdit()`；工具选择不变 |
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
| `_Input` | `public override void _Input(InputEvent @event)` | 通过 `InputBindingManager` 处理当前暂停、撤销重做和工具动作 |
| `_Process` | `public override void _Process(double delta)` | 协调子组件刷新当前工具、catalog 上下文、调试指标和响应式布局 |

| UI/快捷键 | 当前调用 | 说明 |
|---|---|---|
| 当前暂停绑定 / 默认 Esc | `GameHUD._Input()` 打开 `PauseMenu` | 暂停场景树且保留当前工具；再次按当前绑定或“继续游戏”恢复 |
| 当前编辑绑定 / 默认 Z/Y | `GameHUD._Input()` 调用 `ToolManager.UndoRoadEdit()` / `RedoRoadEdit()` | 仅在暂停菜单和模态 UI 关闭时处理；不切换当前工具 |
| 当前工具绑定 / 默认 Q/R/E | `GameHUD._Input()` 设置 `ToolManager.CurrentTool` | 模态菜单关闭时切换选择、铺路或拆路 |
| 铺路按钮 | `ConstructionDock` 的 `RoadToolButton` 调用 `ToolManager.CurrentTool = ToolType.Road` | 切换铺路工具，按钮来自 Roads catalog |
| 拆路 | `tool_remove` 动作或程序设置 `ToolManager.CurrentTool = ToolType.RoadRemove` | UI 显示内建中文文案和当前绑定；仍不提供 Roads 子菜单按钮 |
| 存档后端注入 | `PauseMenu.ConfigureSaveManager(...)` | HUD 组合根提供当前 `SaveManager`；暂停菜单负责命名槽交互 |

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
| `PauseMenu` | 当前暂停动作打开的全屏模态菜单；可列举有效及损坏存档、另存为独立命名槽，并经目标摘要确认覆盖、加载或删除；损坏槽禁用覆盖/加载。另可继续游戏、调整会话音频、持久化键位，或经确认返回主菜单/退出桌面 |

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
| 2 | `RoadBuilder.HandlePlaceInput()` | 左键按下/释放转发到公开铺路生命周期 |
| 3 | `RoadBuilder.BeginPlace()` / `UpdatePlace()` | 当前策略吸附起点并生成不可变 `RoadPathDraft` 预览 |
| 4 | `RoadBuilder.CommitPlace()` | 刷新最终草稿并通过 `RoadEditHistory.Execute(...)` 提交其中的 `RoadPath`，不向数据层传递网格或 RoadType |
| 5 | `RoadGraph.SubmitPath(...)` | 校验原生几何，创建/复用节点，拆分交点，跳过覆盖段，创建 group/edge；成功状态变化进入撤销栈 |
| 6 | `RoadGraph.EdgeAdded` | 通知渲染器缓存显示点列并安排合并静态批次重建 |
| 7 | `GameHUD._Process()` -> `DebugPanel.UpdateMetrics()` | 调试组件轮询并显示 Group/Edge/Node 数量 |

### 拆路数据流

| 步骤 | 调用 | 数据变化 |
|---|---|---|
| 1 | `ToolManager.CurrentTool = ToolType.RoadRemove` | 开启 `RoadBuilder.SetRemoveHoverActive(true)` |
| 2 | `RoadBuilder._Process()` | `UpdateRemoveHover()` 更新 `RoadRenderer.HoveredEdgeID` |
| 3 | `RoadRenderer._Draw()` | 绘制 hover 高亮 |
| 4 | `RoadBuilder.HandleRemoveInput()` | 左键拖动累积轨迹命中，`Shift+左键` 从当前矩形生成选择；预览阶段不写图 |
| 5 | `RoadBuilder.ConfirmRemove()` | 松开左键后通过 `RoadEditHistory.Execute(...)` 将排序去重的 Edge ID 集一次性交给 `RoadGraph.RemoveEdges(...)` |
| 6 | `RoadGraph.RemoveEdges(...)` | 跳过失效目标，批量 detach 后只执行一次清理和不变式验证；成功状态变化进入撤销栈 |
| 7 | `RoadGraph.EdgeRemoved` | 按稳定 ID 顺序通知渲染器移除缓存点列并安排合并静态批次重建 |

### 道路编辑历史流

| 阶段 | 调用 | 内容 |
|---|---|---|
| 记录 | `RoadEditHistory.Execute(...)` | 捕获成功道路编辑前后的严格 RoadGraph JSON；容量超过 64 时淘汰最旧事务 |
| 撤销/重做入口 | `GameHUD` -> `ToolManager` -> `RoadBuilder` | 当前 `edit_undo` / `edit_redo` 绑定触发；先取消尚未提交的铺路/拆路会话 |
| 恢复 | `RoadEditHistory.Undo/Redo()` -> `RoadGraph.RestoreState(...)` | 恢复完整拓扑、原生几何、Group、ID 和 `_nextID`；实体对象引用重建 |
| 渲染同步 | `RoadGraph.GraphCleared` -> `RoadRenderer.OnGraphCleared()` | 清空旧缓存并按恢复后的全部 Edge 重建道路 mesh 与节点 MultiMesh |
| 历史失效 | 外部 `GraphCleared` 或状态不匹配 | 清空旧撤销/重做栈，避免把外部图变化覆盖掉 |

### 存档流

| 阶段 | 调用 | 内容 |
|---|---|---|
| 注册 | `MainCamera._Ready()` | `SaveManager.Instance.Register(this)` |
| 注册 | `RoadSystem._Ready()` | `SaveManager.Instance.Register(Graph)` |
| 注销 | `MainCamera._ExitTree()` | `SaveManager.Instance.Unregister(this)` |
| 注销 | `RoadSystem._ExitTree()` | `SaveManager.Instance.Unregister(Graph)` |
| 周期入口 | `AutosaveController` 的场景内 `Timer` | 默认每 300 秒调用 `SaveManager.SaveAutosave()`；场景暂停时不计时 |
| 自动保存 | `SaveManager.SaveAutosave()` | 覆盖保留 `autosave` 槽，不切换当前手动槽；事务失败保留上一份有效自动存档 |
| 列举入口 | `PauseMenu` 打开存档管理视图 | `SaveManager.ListSlots()` 返回有效及损坏槽摘要，不加载业务 JSON |
| 新建入口 | `PauseMenu` 提交新显示名 | `SaveManager.SaveAs(displayName)` 生成独立 `manual-<GUID>` 槽 |
| 覆盖入口 | `PauseMenu` 确认目标摘要 | `SaveManager.Save(slotID)` 覆盖已存在槽；取消不写文件 |
| 保存文件 | `SaveManager.Save/SaveAs/SaveAutosave()` | V2 槽写 `road_network.json` 与 `manifest.json`，不写相机状态 |
| 加载入口 | `PauseMenu` 确认目标摘要 | `SaveManager.Load(slotID)`；取消不改变当前槽位或活动道路 |
| 删除入口 | `PauseMenu` 确认目标摘要 | `SaveManager.DeleteSlot(slotID)` 递归删除非空有效或损坏槽 |
| RoadGraph 恢复 | `SaveSlotStore.Load()` -> `RoadGraph.PrepareRestoreState/RestorePreparedState` | 整槽预检后一次提交，重建邻接与空间索引并触发 `GraphCleared` |
| 渲染恢复 | `RoadRenderer.OnGraphCleared()` | 清空并全量重建连续道路 mesh 与节点 MultiMesh |

---

## 11. 道路存档词汇说明

| 词汇 | 当前含义 | 状态 |
|---|---|---|
| `RoadGraphSaveData` | `RoadGraph.Persistence.cs` 的私有 V2 存档根对象 | 使用 `schemaVersion = 1` 严格校验 |
| JSON 字段 `nodes` / `edges` / `groups` | `GraphNode` / `GraphEdge` / `RoadGroup` 存档集合 | 当前活动字段，保存六类原生几何参数 |
| JSON 字段 `junctions` / `segments` / `roads` | 已移除的旧格式词汇 | 当前版本明确拒绝，不提供迁移或默认回退 |
| `RoadType` / `type` | 第三代以后才可能重新设计的道路分级语义 | 第二代运行时和存档均不存在 |

文档中不再保留旧运行时 `RoadNetwork`、`Road`、`Segment`、`Junction` 的类章节。第三代若引入道路分级，必须同时定义新的提交 API、运行时字段、渲染规则、存档 schema 版本和迁移/拒绝策略，不能向第二代契约静默补回 `RoadType`。
