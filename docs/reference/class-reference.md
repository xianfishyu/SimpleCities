# SimpleCities 类与 API 参考

> 最后更新：2026-08-14 | Godot 4.7 | Godot.NET.Sdk 4.7.0 | .NET 10.0 | C# 14.0 | Nullable enabled

本文档聚焦项目自有 API；当前事实源包括 `Scripts/` 下 81 个 C# 文件和 `Shaders/MapTerrain.gdshader`。`addons/` 为第三方插件，不纳入本参考。

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
- [11. 道路存档词汇说明](#11-道路存档词汇说明)

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

当前道路运行时采用 V3 领域模型：`RoadGraph` 是稳定 facade 与不可变 revision root 的事务核心，`GraphNode` 以 endpoint-role incidence 表达邻接，`GraphEdge` 保存最大连续原生几何链及 Edge 级 `RoadType`。`RoadGroup`、Group API、逐 Edge 事件、V2 RoadGraph DTO 和兼容 reader 均已删除；V3 payload 只保存 canonical `nodes` / `edges`。

| 模块 | 文件 | 职责 |
|---|---|---|
| Core | `IStreamingSaveable.cs`, `SaveManager.cs`, `SaveSlotStore.cs`, `V3Json*.cs`, `V3*Codec.cs`, `V3PngValidator.cs`, `V3VerifiedReadStream.cs`, `SaveData.cs` | V3 流式参与者、独立保存根、有界 reader、严格 manifest/descriptor、PNG 展示资产与槽位事务 |
| Camera | `MainCamera.cs` | 2D 相机移动与缩放；不参与当前 V3 存档 |
| Grid | `GridSystem.cs`, `MapBackground.cs`, `MapTerrain.gdshader` | 网格数学、背景 CanvasLayer、Shader 网格绘制 |
| Road data | `Direction.cs`, `GraphNode.cs`, `GraphEdge.cs`, `RoadType.cs`, `RoadPath.cs`, `SpatialIndex.cs`, `RoadGraph*.cs`, `Geometry/*.cs` | incidence 拓扑、原生几何、空间索引、类型化事务、delta 与 V3 持久化 |
| Road scene | `RoadBuilder.cs`, `Input/*.cs`, `RoadConfig.cs`, `RoadRenderer.cs`, `RoadSystem.cs` | 输入生命周期、可替换投影策略、共享配置、事件驱动渲染、依赖注入 |
| Tools | `ToolManager.cs`, `ToolType.cs` | 工具切换和输入转发 |
| UI | `GameHUD.cs`, `ConstructionDock.cs`, `ToolContextPanel.cs`, `DebugPanel.cs`, `PauseMenu.cs`, `UIManager.cs` | 命令中心 HUD、建造坞、上下文、诊断、暂停菜单和面板管理 |

---

## 2. 单例与初始化

| 单例 | 类型 | 创建位置 | 主要消费者 |
|---|---|---|---|
| `SaveManager.Instance` | `SaveManager` | Autoload `_Ready()` | `RoadSystem`, `GameHUD`, `AutosaveController` |
| `MainCamera.Instance` | `MainCamera` | `MainCamera._Ready()` | `MapBackground`, `GameHUD` |
| `RoadSystem.Instance` | `RoadSystem` | `RoadSystem._Ready()` | `GameHUD` |
| `ToolManager.Instance` | `ToolManager` | `ToolManager._Ready()` | `GameHUD` |
| `MapBackground.Instance` | `MapBackground` | `MapBackground._Ready()` | 目前无直接调用者 |
| `UIManager` 子节点 | `UIManager` | `GameHUD.EnsureUIManager()` | 所属 `GameHUD` 内面板 |

| 初始化阶段 | 关键调用 | 结果 |
|---|---|---|
| Core autoload | `SaveManager._Ready()` | 建立全局持久化入口 |
| 相机 | `MainCamera._Ready()` / `_ExitTree()` | 规范化缩放配置、设置或清理 `Instance`；不注册存档参与者 |
| 道路系统 | `RoadSystem._Ready()` / `_ExitTree()` | 创建并注册 `RoadGraph`，注入 renderer/builder；退出时注销并清理单例 |
| HUD | `GameHUD._Ready()` | 获取 `ToolManager` 和 `RoadSystem.Graph`，解析控件，绑定工具和存档按钮 |

---

## 3. Core 持久化模块

### IStreamingSaveable

**文件**：`Scripts/Core/IStreamingSaveable.cs`
**类型**：`public interface IStreamingSaveable`

| 成员 | 签名 | 说明 |
|---|---|---|
| `SaveFileName` | `string SaveFileName { get; }` | 业务文件名，不含 `.json` |
| `CaptureSnapshot` | `ISaveSnapshot CaptureSnapshot()` | O(1) 捕获不可变参与者快照 |
| `WriteSnapshot` | `void WriteSnapshot(Stream destination, ISaveSnapshot snapshot)` | 直接向目标字节流写 canonical payload |
| `PrepareLoad` | `IPreparedSaveState PrepareLoad(Stream source)` | 从字节流完整解析、校验并返回未提交状态 |
| `CommitPreparedLoad` | `void CommitPreparedLoad(IPreparedSaveState preparedState)` | 把已准备状态提交到活动参与者 |

`ISaveSnapshot` 与 `IPreparedSaveState` 是标记接口。当前唯一生产参与者是 `RoadGraph`，其 `RoadGraphRevision` 同时实现二者；`MainCamera` 不再参与存档。旧 `ISaveable` / `IPreparedSaveable`、字符串 `CaptureState` / `RestoreState` 兼容入口已删除。

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
| `_Ready` | `public override void _Ready()` | 设置 `Instance` |
| `Register` | `public bool Register(IStreamingSaveable saveable)` | 同一对象幂等；拒绝另一活动对象使用大小写等价的 `SaveFileName` |
| `Unregister` | `public bool Unregister(IStreamingSaveable saveable)` | 移除离开场景树的存档参与者 |
| `Save` | `public bool Save(string slotID = AutosaveSlotID)` | 按内部 ID 覆盖已存在槽位；首次只允许保留的 autosave |
| `SaveAutosave` | `public bool SaveAutosave()` | 覆盖保留自动槽，不改变玩家当前选中的手动槽 |
| `SaveAs` | `public bool SaveAs(string displayName)` | 以玩家可见名称创建具有独立安全 ID 的手动槽位 |
| `Load` | `public bool Load(string slotID = AutosaveSlotID)` | 按内部 ID 完成 manifest、全部文件和临时模型预检后提交恢复 |
| `SaveSlotExists` | `public bool SaveSlotExists(string slotID)` | 按内部 ID 检查 manifest 是否存在 |
| `ListSlots` | `public IReadOnlyList<SaveSlotSummary> ListSlots()` | 无需加载业务 JSON 即可列举有效及损坏槽摘要 |
| `RequestDeleteSlot` | `public string RequestDeleteSlot(string slotID)` | 从当前列表 generation 为有效或损坏 V3 occupant 生成一次性删除 operation token；失败返回空字符串 |
| `ConfirmDeleteSlot` | `public bool ConfirmDeleteSlot(string slotID, string operationToken)` | 仅接受与待确认目标、generation、kind 和 digest 匹配的 token；成功 tombstone 删除当前槽后回到 `autosave` |

| 存档规则 | 当前实现 |
|---|---|
| 基础目录 | 编辑器和导出统一使用 `user://saves-v3/<slotID>/`；测试可向 `SaveSlotStore` 注入隔离根 |
| 命名边界 | 目录只使用受限内部 ID；玩家显示名只进入 manifest，并受 Unicode scalar 与 UTF-8 字节预算约束 |
| 单系统文件 | `<SaveFileName>.json` |
| 写入策略 | 捕获不可变 snapshot 后写 `.save-transactions/<slot>/<operation>/staging`；payload 关闭前计算实际长度/SHA-256，manifest 最后写入；`publish.json` 绑定新旧 aggregate digest 后再以 slot/backup 目录切换发布 |
| Manifest | 严格 `simple-cities-v3` format v1；每个业务文件记录 `name`、`encodedLength` 与 `sha256` |
| V3 业务范围 | 当前有且只要求 `road_network.json`；外来、损坏或 unsafe occupant 不允许被普通 Save 覆盖 |
| 加载策略 | 先分类完整槽，逐 payload 通过有界 `V3JsonStreamReader` 在同一读取句柄校验 byte/token/depth/lexeme、长度/hash/EOF 并完成全部 `PrepareLoad`，最后提交已准备状态 |
| 根与恢复 | 每个同步操作持有 `.save-root.lock` 的 OS 独占句柄；恢复只按 publish/delete descriptor 与 digest 矩阵完成、隔离、清理或 typed 阻塞 |
| 缩略图 | 可选 `thumbnail.png` 不属于业务 aggregate；严格校验 PNG chunk/CRC、尺寸、像素和解码扫描线，缺失或损坏只产生 warning |
| 错误处理 | 捕获异常，`GD.PushError(...)`，返回 `false` |

`SaveSlotStore` 是不依赖 Godot Node 的内部同步文件存储边界，负责生成 `manual-<GUID>` ID、约束路径、`Absent | CompleteV3 | CorruptV3 | Foreign | Unsafe` 分类、operation-specific publish/delete descriptor、aggregate digest、quarantine/tombstone、跨进程 OS 根锁、严格 manifest、存在性检查和删除。发布与删除分别返回含 operation token 的 `SavePublishResult` / `SaveDeleteResult`，cleanup 失败不会反向回滚已经越界的操作；无法证明的恢复组合通过专用 exception 保留现场。尚未实现的是 `v3-save-system:2.3` 的进程内 async coordinator、publish lease、公开结构化 operation state 与不可失败 aggregate Load commit。`SaveManager` 负责注册生命周期、Godot 日志、删除确认 generation 和 `CurrentSlotID`。

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

### SaveJson 与 V3 codec 边界

**文件**：`Scripts/Core/SaveJson.cs`
**类型**：`public static class SaveJson`

| 成员 | 签名 | 说明 |
|---|---|---|
| `Serialize` | `public static string Serialize(object data)` | 供几何辅助比较/序列化使用旧统一选项 |
| `Deserialize` | `public static T Deserialize<T>(string json)` | 供几何辅助入口反序列化；不用于 V3 slot reader |

| 选项 | 值 |
|---|---|
| `WriteIndented` | `true` |
| `PropertyNameCaseInsensitive` | `true` |

`SaveJson` 不再是 V3 存档 codec。V3 manifest 由 `V3ManifestCodec` 以严格字段、token 与 family/version 契约读写；RoadGraph payload 由 `RoadGraph.WriteSnapshot` / `PrepareLoad` 直接处理 UTF-8 stream。

### SaveData 与 manifest 模型

**文件**：`Scripts/Core/SaveData.cs`
**类型**：内部 immutable manifest records 与公开 `SaveSlotSummary`

活动 schema、验证状态和未来迁移边界见 [存档系统当前参考](save-system-plan.md)。本节只列当前 DTO 形状。

| DTO | 公开属性签名 | JSON 字段 | 默认值/说明 |
|---|---|---|---|
| `V3Manifest` | `SlotID`、`DisplayName`、`Timestamp`、城市元数据、`ThumbnailFile`、`Files` | manifest 根字段 | family/schema 由 codec 固定为 `simple-cities-v3` / `1` |
| `V3ManifestFile` | `Name`、`EncodedLength`、`Sha256` | `files[]` | 绑定业务 payload 名称、实际编码长度和小写 SHA-256 |
| `SaveSlotOccupantKind` | `Absent`、`CompleteV3`、`CorruptV3`、`Foreign`、`Unsafe` | 非 JSON 分类 | 决定 list/load/save/delete 的允许边界 |
| `SavePublishResult` | `Kind`、`SlotID`、`OperationToken`、`SavedFileCount`、`Warning` | 非 JSON 结果 | `PublishedWithCleanupPending` 仍表示新槽已经发布 |
| `SaveDeleteResult` | `Kind`、`SlotID`、`OperationToken`、`Warning` | 非 JSON 结果 | `DeletedWithCleanupPending` 仍表示槽已经逻辑删除 |
| `SaveDeletionAuthorization` | `SlotID`、`UIGeneration`、`OperationToken`、`OccupantKind`、`OccupantDigest`、`ConfirmationSummary` | `delete.json` 的输入 | 把当前 UI 列表目标与待删 occupant 精确绑定 |
| `SaveSlotSummary` | `SlotID`、`DisplayName`、`SavedAtUtc`、城市元数据、`ThumbnailPath`、`Files`、`IsValid`、`Error`、`Warning`、`IsAutosave` | 非 JSON 摘要 | `IsAutosave` 只按保留内部 ID 判定；有效槽按 UTC 时间倒序并以 ID 稳定排序，损坏槽排在末尾 |

RoadGraph V3 payload 不经过这些 manifest records；`RoadGraph.Persistence.cs` 直接读写 `formatFamily`、`payloadType`、`schemaVersion`、`nextID`、`nodes` 和 `edges`，并严格拒绝 V2 Group 形状。

---

## 4. MainCamera

**文件**：`Scripts/MainCamera.cs`
**继承**：`public partial class MainCamera : Camera2D`

| 导出成员 | 签名 | 默认值 | 说明 |
|---|---|---|---|
| `defaultScale` | `[Export] private float defaultScale = 1f` | `1f` | 相机目标缩放；当前不持久化 |
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
| `_Ready` | `public override void _Ready()` | 规范化缩放配置并设置单例 |
| `_ExitTree` | `public override void _ExitTree()` | 清理当前单例 |
| `_Process` | `public override void _Process(double delta)` | 更新缩放、键盘移动和中键拖拽 |
| `_UnhandledInput` | `public override void _UnhandledInput(InputEvent @event)` | 处理未被 UI 消费的 WASD、滚轮和中键输入 |

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

### GraphNode 与 EdgeIncidence

**文件**：`Scripts/Road/GraphNode.cs`

| 类型 | 公开成员 | 签名 | 说明 |
|---|---|---|---|
| `EdgeEndpoint` | 枚举值 | `A`, `B` | incidence 在 Edge 上的端接角色 |
| `EdgeIncidence` | record struct | `EdgeID`, `Endpoint`, `NeighborNodeID` | 可区分 self-loop A/B 的邻接引用 |
| `GraphNode` | `ID` | `public int ID { get; }` | 节点 ID |
| `GraphNode` | `Position` | `public Vector2 Position { get; }` | 世界坐标 |
| `GraphNode` | `Incidences` | `public IReadOnlyList<EdgeIncidence> Incidences` | 按 Edge ID 和 endpoint 排序的 immutable incidence |
| `GraphNode` | `IncidenceCount` / `Degree` | 只读 `int` | 按端接计数；self-loop 贡献 2 |
| `GraphNode` | `IncidentEdgeCount` | `public int IncidentEdgeCount` | 去重后的 Edge 数；self-loop 为 1 |
| `GraphNode` | 构造函数 | `public GraphNode(int id, Vector2 position)` | 创建节点 |
| `GraphNode` | `GetNeighborIDs` | `public IEnumerable<int> GetNeighborIDs()` | 去重后的邻居节点 ID |

`WithAddedIncidence(...)`、`WithRemovedIncidence(...)` 和 `WithoutIncidences()` 是 `internal` copy-on-write helper；发布后的 `GraphNode` 不原地修改。self-loop 在同一 Node 上精确保存 A/B 两条 incidence。

### GraphEdge

**文件**：`Scripts/Road/GraphEdge.cs`

| 成员 | 签名 | 说明 |
|---|---|---|
| `ID` | `public int ID { get; }` | 边 ID |
| `NodeA` | `public int NodeA { get; }` | 规范方向起点 Node ID；非环总小于 `NodeB` |
| `NodeB` | `public int NodeB { get; }` | 规范方向终点 Node ID；可与 `NodeA` 相同表示 self-loop |
| `RoadType` | `public RoadType RoadType { get; }` | Edge 级道路类型，也是 merge key 的一部分 |
| `GeometrySegments` | `public IReadOnlyList<RoadGeometrySegment> GeometrySegments` | 保留类型与控制参数的权威原生几何，只读包装 |
| `Points` | `public Vector2[] Points { get; }` | 原生段边界的防御性副本，不含端点 |
| `Length` | `public float Length { get; }` | 几何长度 |
| 构造函数 | `public GraphEdge(RoadType roadType, int id, int nodeA, int nodeB, IReadOnlyList<RoadGeometrySegment> geometrySegments)` | canonicalize 几何并建立 immutable Edge |
| `GetFullPath` | `public Vector2[] GetFullPath(Func<int, GraphNode?> getNode)` | 返回 `[NodeA.Position, ...Points, NodeB.Position]`；端点缺失时抛出 `InvalidOperationException` |

构造函数要求非空、逐 bit 连续的正长度 geometry chain，并执行 geometry canonicalization。非 self-loop 会按 Node ID 升序定向并用原生反向契约翻转几何；self-loop 要求精确闭合并从正反链中选择较小 typed canonical key。对象发布后端点、类型和几何都不可变。

### RoadType

**文件**：`Scripts/Road/RoadType.cs`

| 枚举值 | 存档 token | 说明 |
|---|---|---|
| `Dirt` | `"dirt"` | 土路 |
| `Street` | `"street"` | 街道；当前 RoadBuilder 固定建造类型 |
| `Arterial` | `"arterial"` | 主干道 |
| `Highway` | `"highway"` | 高速道路 |

`RoadTypeContract` 严格校验这四个值并执行大小写敏感 token 映射。不同类型相邻 Edge 保留 semantic-boundary Node；`ChangeRoadType` 可能让边界消失并触发 canonical merge。当前领域/存档已支持四类，类型选择 UI 与 RoadUpgrade 工具仍属 Phase 7。

### SpatialIndex

**文件**：`Scripts/Road/SpatialIndex.cs`

| 类型 | 公开成员 | 签名/值 | 说明 |
|---|---|---|---|
| `ISpatialRef` | `Position` | `Vector2 Position { get; }` | 空间位置 |
| `ISpatialRef` | `Kind` | `SpatialRefKind Kind { get; }` | 引用类别 |
| `ISpatialRef` | `IntersectsCircle` | `bool IntersectsCircle(Vector2 center, float radius)` | 权威圆形命中过滤 |
| `SpatialRefKind` | 枚举值 | `Node`, `EdgePoint`, `EdgeSegment`, `EdgeGeometry` | 节点、旧辅助点/线段或当前 query fragment |
| `NodeSpatialRef` | `NodeID` | `public int NodeID { get; }` | 节点 ID |
| `NodeSpatialRef` | `Position` | `public Vector2 Position { get; }` | 节点位置 |
| `EdgeGeometryRef` | Edge/geometry/fragment ID、参数区间、`Geometry`、`Bounds`、end ownership | 原生 query fragment | 保留到 source geometry 的参数映射和半开端点所有权 |
| `UniformGrid` | 构造函数 | `public UniformGrid(float bucketSize)` | bucket 下限为 `1f` |
| `UniformGrid` | `Insert` | `public void Insert(ISpatialRef entity)` | 插入引用 |
| `UniformGrid` | `Remove` | `public void Remove(ISpatialRef entity)` | 按对象引用移除 |
| `UniformGrid` | `InsertGeometry` / `RemoveGeometry` | `public void ...(EdgeGeometryRef geometry)` | 按原生几何 Bounds 覆盖的全部桶增删引用 |
| `UniformGrid` | `QueryRadius` | `public IEnumerable<ISpatialRef> QueryRadius(Vector2 center, float radius)` | 半径查询，桶过滤加精确距离 |
| `UniformGrid` | `QueryBounds` | `public IEnumerable<ISpatialRef> QueryBounds(Rect2 bounds)` | 返回覆盖桶内去重引用，调用方再做权威几何过滤 |
| `UniformGrid` | `Clear` | `public void Clear()` | 清空索引 |

空间索引是不可持久化的派生查询结构，不是权威数据源。`RoadGraphRevision` 保存 immutable Node/Edge、Node ref、Edge fragment ref 和 `UniformGridSnapshot`；mutation builder 只复制受影响条目与 bucket 页。`UniformGrid.HasExactCoverage(...)` 以引用 identity 预计算预期 bucket coverage/count，再单次扫描全部 bucket entry，严格拒绝缺失、额外、错桶、同桶重复和计数不符，复杂度随引用与实际索引条目线性增长。

### RoadGraph

**文件**：`Scripts/Road/RoadGraph.cs`
**类型**：`public partial class RoadGraph : IStreamingSaveable`

| 常量/内部结构 | 当前值/职责 |
|---|---|
| `SnapRadius` | `0.5f`，节点复用半径 |
| `GeometryEpsilon` | `1e-4f`，几何容差 |
| `IndexBucketSize` | `64f`，默认空间索引桶尺寸 |
| `_revision` | 当前 immutable `RoadGraphRevision` root；含 lineage/revision/sequence、watermark、实体和派生索引 |
| `_nodes` / `_edges` | 当前 mutation 使用的 immutable dictionary builder；发布后冻结进 revision |
| `_nodeRefs` / `_edgeRefs` / `_spatialIndex` | query fragment 派生索引 builder；与当前 root 一起提交 |

| 公开成员 | 签名 | 说明 |
|---|---|---|
| `FacadeID` | `public long FacadeID { get; }` | 进程内稳定且唯一的 facade 实例身份；分配空间耗尽时失败而不回绕 |
| `SaveFileName` | `public string SaveFileName => "road_network"` | 路网存档文件名 |
| `GraphChanged` | `public event Action<RoadGraphChangedEvent>? GraphChanged` | 唯一事务事件；携带 delta、summary 与完整 state token |
| 构造函数 | `public RoadGraph()` | 使用默认 `IndexBucketSize` |
| 构造函数 | `public RoadGraph(float bucketSize)` | 指定空间索引 bucket |
| `SubmitPolyline` | `public RoadPathSubmissionResult SubmitPolyline(RoadType roadType, IReadOnlyList<Vector2>? points)` | 以显式类型提交折线并返回结构化结果 |
| `SubmitPath` | `public RoadPathSubmissionResult SubmitPath(RoadBuildRequest? request)` | 提交原生 `RoadPath` 与 `RoadType` 的不可缺省请求 |
| `RemoveEdge` | `public bool RemoveEdge(int edgeID)` | 删除单 Edge，并在同一事务恢复 canonical form |
| `RemoveEdges` | `public bool RemoveEdges(IEnumerable<int>? edgeIDs)` | 对 ID 去重排序，跳过失效目标并一次提交全部有效删除 |
| `ChangeRoadType` | `public RoadTypeChangeResult ChangeRoadType(IEnumerable<int>? edgeIDs, RoadType targetType)` | 原子批量改造，随后消除可合并 semantic boundary |
| `GetEdge` | `public GraphEdge? GetEdge(int edgeID)` | 取边 |
| `GetNode` | `public GraphNode? GetNode(int nodeID)` | 取节点 |
| `FindClosestEdge` | `public GraphEdge? FindClosestEdge(Vector2 position, float maxRadius)` | 从原生几何空间候选中计算权威最近点；等距时选较小 Edge ID |
| `FindEdgeIDsNear` | `public IReadOnlyList<int> FindEdgeIDsNear(Vector2 position, float radius)` | 返回与圆形命中范围相交的原生几何 Edge ID 稳定序列 |
| `FindEdgeIDsIntersecting` | `public IReadOnlyList<int> FindEdgeIDsIntersecting(Rect2 bounds)` | 以空间候选和原生几何/矩形边界精确过滤返回稳定 Edge ID 序列 |
| `FindClosestNode` | `public GraphNode? FindClosestNode(Vector2 position, float maxRadius)` | 基于空间索引查最近节点 |
| `GetAllEdges` | `public IEnumerable<GraphEdge> GetAllEdges()` | 返回调用时的边稳定快照 |
| `GetAllNodes` | `public IEnumerable<GraphNode> GetAllNodes()` | 返回调用时的节点稳定快照 |
| `CaptureRevision` | `public RoadGraphRevision CaptureRevision()` | O(1) 返回当前 immutable root |
| `CurrentStateToken` | `public GraphStateToken CurrentStateToken` | 当前 `(LineageID, DomainRevisionID, ChangeSequence)` |
| `ApplyDelta` | `public RoadGraphDeltaApplyResult ApplyDelta(...)` | 校验完整 token 后正向或反向应用可逆 delta |
| `CaptureSnapshot` | `public ISaveSnapshot CaptureSnapshot()` | 以当前 revision 作为存档快照 |
| `WriteSnapshot` | `public void WriteSnapshot(Stream destination, ISaveSnapshot snapshot)` | 写确定、无缩进的 V3 UTF-8 payload |
| `PrepareLoad` | `public IPreparedSaveState PrepareLoad(Stream source)` | 严格解析并构建完整 canonical revision，不改活动图 |
| `CommitPreparedLoad` | `public void CommitPreparedLoad(IPreparedSaveState preparedState)` | full reset 到已准备 root、创建新 lineage 并发布一次 `GraphChanged` |

| mutation 关键阶段 | 行为 |
|---|---|
| 1 | 入口校验数值、容量、RoadType、路径连续性和重入状态，建立未发布 plan/builder |
| 2 | 规划 incoming self-intersection、与既有 Edge 的原生交点/重叠、确定 cluster 和 split |
| 3 | 应用 Node/Edge 变化，按 incidence、RoadType 与 exact geometry 规则恢复最大连续 Edge |
| 4 | Debug `AssertInvariants()` 校验 topology、canonical form、容量和空间索引精确覆盖 |
| 5 | 生成 `RoadGraphDelta`；若 history admission 拒绝则恢复旧 root，且 watermark/token/event 不变 |
| 6 | 一次替换 immutable root、递增 sequence，并同步发布一次 `GraphChanged` |

`RoadGraphRevision` 保存完整不可变 root；`RoadGraphDelta` 只保存 changed Node/Edge 的 before/after 值、content revision 和估算保留字节。`RoadGraphChangeSummary` 按 ID 排序列出 created/removed/updated Node/Edge，full reset 另有明确标志。事件发布期间拒绝 mutation 重入，单个 observer 异常不会回滚已提交 root 或阻止其他订阅者。

| 存档恢复阶段 | 行为 |
|---|---|
| `PrepareLoad` family/schema gate | 只接受 `simple-cities-v3`、`payloadType = "road-network"`、`schemaVersion = 1` |
| token/资源门禁 | `V3JsonStreamReader` 以固定 buffer 限制 encoded bytes、tokens、depth、property/string/number lexeme，并在元素进入集合前应用 `RoadGraphCapacity` |
| 业务校验 | 拒绝 unknown/duplicate field、Group/groupID、错误 token、非法 ID/watermark、非规范二度节点、错误 seam/direction、内部交叉、覆盖和损坏引用 |
| prepared root | 从 payload 重建 incidence、query fragment、空间索引、容量计数并完成 invariant 检查；失败保持活动图和事件不变 |
| `CommitPreparedLoad` | 采用 payload watermark，创建新 lineage，以一次 full-reset `GraphChanged` 使旧 delta/history/token 失效 |

---

## 7. Road 输入、渲染与系统装配

### RoadTypeStyle

**文件**：`Scripts/Road/RoadTypeStyle.cs`
**继承**：`[GlobalClass] public partial class RoadTypeStyle : Resource`

| 导出属性 | 签名 | 默认值 | 说明 |
|---|---|---|---|
| `RoadType` | `[Export] public RoadType RoadType { get; set; }` | `Street` | 显式绑定的稳定道路类型 |
| `DisplayName` | `[Export] public string DisplayName { get; set; }` | 空字符串 | 面向玩家的类型名称；合法资源不得为空白 |
| `Color` | `[Export] public Color Color { get; set; }` | `White` | 必须为有限且 alpha 大于零的展示颜色 |
| `Width` | `[Export] public float Width { get; set; }` | `12f` | 必须为正有限值的世界空间宽度 |

| 公开方法 | 签名 | 说明 |
|---|---|---|
| `TryValidate` | `public bool TryValidate(out string error)` | 校验类型、名称、颜色和宽度并返回结构化失败文本 |
| `GetValidationResult` | `public Godot.Collections.Dictionary GetValidationResult()` | 为 GDScript/运行时契约公开 `valid` 与 `error` |

### RoadConfig

**文件**：`Scripts/Road/RoadConfig.cs`
**继承**：`[GlobalClass] public partial class RoadConfig : Resource`

| 导出属性 | 签名 | 默认值 | 说明 |
|---|---|---|---|
| `CellSize` | `[Export] public float CellSize { get; set; } = 64f` | `64f` | 网格单元尺寸 |
| `RoadColor` | `[Export] public Color RoadColor { get; set; } = new("#37474F")` | `#37474F` | 统一道路颜色 |
| `RoadWidth` | `[Export] public float RoadWidth { get; set; } = 12f` | `12f` | 统一道路线宽 |
| `RoadTypeStyles` | `[Export] public Array<RoadTypeStyle>? RoadTypeStyles { get; set; }` | 四类内置样式 | `Dirt`、`Street`、`Arterial`、`Highway` 的唯一展示映射 |
| `CurveDisplayTolerance` | `[Export] public float CurveDisplayTolerance { get; set; } = 0.25f` | `0.25f` | 原生曲线生成显示折线时允许的最大世界空间误差 |
| `JunctionRadius` | `[Export] public float JunctionRadius { get; set; } = 10f` | `10f` | 节点圆半径，当前 `EdgeCount >= 2` 绘制 |
| `JunctionColor` | `[Export] public Color JunctionColor { get; set; } = new("#FFC107")` | `#FFC107` | 节点圆颜色 |
| `EndpointRadius` | `[Export] public float EndpointRadius { get; set; } = 6f` | `6f` | 端点圆半径，`0` 可隐藏 |
| `EndpointColor` | `[Export] public Color EndpointColor { get; set; } = new("#90A4AE")` | `#90A4AE` | 端点圆颜色 |
| `HoverHighlightColor` | `[Export] public Color HoverHighlightColor { get; set; } = new(1f, 0.8f, 0.2f, 0.6f)` | 半透明黄 | 拆除悬停高亮 |
| `HoverHighlightWidth` | `[Export] public float HoverHighlightWidth { get; set; } = 18f` | `18f` | 拆除悬停高亮宽度 |

| 公开方法 | 签名 | 说明 |
|---|---|---|
| `TryValidateRoadTypeStyles` | `public bool TryValidateRoadTypeStyles(out string error)` | 要求四类恰好各一项，并校验每项字段；不修复无效资源 |
| `GetRoadTypeStylesValidationResult` | `public Godot.Collections.Dictionary GetRoadTypeStylesValidationResult()` | 为 GDScript/运行时契约公开 `valid` 与 `error` |
| `GetRoadTypeStyle` | `public RoadTypeStyle GetRoadTypeStyle(RoadType roadType)` | 在完整映射中返回目标样式；映射无效或类型非法时抛错，不 fallback |

生产 `Scenes/road_config.tres` 使用 `Dirt / 土路 / #8A6652 / 14`、`Street / 街道 / #60727C / 20`、`Arterial / 主干道 / #D7A928 / 26`、`Highway / 高速道路 / #C84B3A / 32`。`RoadRenderer` 在主线程校验并捕获不可变 `RoadTypeStyleSnapshot`；普通 rebuild 与 Load worker 都只消费该值快照，不把 Resource 传入后台。

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
| `ConfirmPlace` | `public bool ConfirmPlace(Vector2 pointerPosition)` | 构造 `RoadBuildRequest(draft.Path, RoadType.Street)` 并一次提交；拒绝时保留会话 |
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
| 道路类型 | 领域请求显式携带 `RoadType`；当前 builder 固定 `Street`，可选择/会话冻结状态仍属 `v3-tool-input:2.1` |
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
| `DefaultByteCapacity` | `public const long DefaultByteCapacity = 16 MiB` | 默认估算 retained delta 字节预算 |
| `CanUndo` / `CanRedo` | 只读属性 | 对应历史栈是否非空 |
| `UndoCount` / `RedoCount` | 只读属性 | 当前两侧事务数 |
| `RetainedByteSize` / `ByteCapacity` | 只读 `long` | 当前 delta 估算保留字节与配置上限 |
| `Execute` | `public bool Execute(Func<bool> edit)` | 在 graph commit 前安装 delta admission；只记录成功且产生变化的事务 |
| `Undo` / `Redo` | `public bool ...()` | 校验完整 token 后调用 `RoadGraph.ApplyDelta` 的 reverse/forward 方向 |
| `Clear` | `public void Clear()` | 清空两侧历史 |

历史不保存全图 JSON。每项保留 `RoadGraphDelta` 与下一次合法方向所需 `GraphStateToken`；entry 数和估算字节双预算按最旧项淘汰，单项超过字节上限时在 graph commit 前拒绝。外部普通 mutation 或 full reset 的 `GraphChanged` 会立即清空两栈；V3 Load 创建新 lineage，因此旧 token 无法作用于新图。失败编辑若已产生 pending delta，会先反向应用该 delta；失败本身不进入历史。

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

### RoadRenderToken

**文件**：`Scripts/Road/RoadRenderToken.cs`
**类型**：`public readonly record struct RoadRenderToken`

| 分量 | 说明 |
|---|---|
| `SceneGeneration` | 当前场景注册代际，由 `SaveManager` 注入 renderer |
| `GraphFacadeID` | 稳定 `RoadGraph` facade 实例身份 |
| `GraphFacadeGeneration` | renderer 改绑 facade 或外部 full reset 时推进的表现代际 |
| `ChangeSequence` | 当前成功 graph commit 序号 |
| `RoadStyleRevision` | 显式样式刷新代际 |
| `RenderRequestID` | 每个表现请求单调推进且不回绕的身份 |

构造会拒绝非正 identity 与负 `ChangeSequence`。内部 `RoadPresentationTokenTracker` 分别保存 `DesiredToken` / `PresentedToken`：普通 rebuild 只允许提交当前 desired；Load admission 只在表现 current 时预留 request，Preflight 生成 matching token，aggregate commit 再同时推进 desired/presented。预留后出现 scene/facade/style/request 变化会使 Load generation 失效。

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
| `GetNodeMarkerCount` | `public int GetNodeMarkerCount()` | Godot 契约读取当前节点批次实例数 |
| `GetPresentationState` | `public Godot.Collections.Dictionary GetPresentationState()` | 返回 `isReady` 及六分量 desired/presented token 的运行时诊断快照 |
| `RefreshRoadStyles` | `public bool RefreshRoadStyles()` | 严格捕获样式快照，推进 `RoadStyleRevision` 与 request，并在完整批次交换成功时返回 `true` |
| `HoveredEdgeID` | `public int? HoveredEdgeID { get; set; }` | 拆除工具悬停边 |
| `_Ready` | `public override void _Ready()` | 校验基础 `Config` 与四类 `RoadTypeStyles`，创建道路 `MeshInstance2D` 与节点 `MultiMeshInstance2D` |
| `SetGraph` | `public void SetGraph(RoadGraph graph)` | 订阅唯一 `GraphChanged`，并从当前 revision 重建初始 cache |
| `_Draw` | `public override void _Draw()` | 绘制拆除 hover/稳定选择/矩形框线和完整多段施工虚线预览 |

| `GraphChanged` 响应 | 行为 |
|---|---|
| 普通 delta | 删除 `RemovedEdgeIDs` cache，重新采样 `UpdatedEdgeIDs` 和 `CreatedEdgeIDs`，推进 desired token 并安排同一事件循环批次重建 |
| full reset | 清空 cache，从活动 revision 的全部 Edge 重新采样，推进 facade generation/desired token 并同步重建；aggregate Load 已提交的同一次 reset 以 graph token 消重 |

当前 `CacheEdgePoints` 用 `RoadGeometryDisplaySampler` 从 `GraphEdge.GeometrySegments` 生成缓存点列；拆除高亮复用同一点列，`RoadBuilder` 对有效原生草稿也使用相同采样入口。`AppendRoadRibbon` 为开放 Edge 生成共享左右边界；对 self-loop 则移除重复 seam 顶点，用循环相邻方向计算首点 miter，并以末段索引回连首段，从而生成无端帽的 closed ribbon。普通 rebuild 与 `RoadRendererLoadPreparer` 都按 `GraphEdge.RoadType` 从同一不可变样式快照读取宽度和颜色，把全部 Edge 的顶点、UV、vertex color 与索引合成一个抗锯齿 `ArrayMesh`；道路层使用白色 modulate，Load prepared payload 同步携带 `RoadColors`。纯 loop seam 不写节点 marker，其他 endpoint/junction 仍写入一个圆形 shader `MultiMesh`。普通 `GraphChanged` 通过 `ScheduleStaticBatchRebuild` 合并；完整 mesh/node batch 交换后才把 matching desired token 提升为 presented。Load 在 Preflight 前预留 request，并于 aggregate commit 同时交换基础批次与 matching desired/presented token。显示点列、样式和 token 都不写回图或存档。当前尚无 junction patch、semantic join/terminal cap、surface snapshot/hit index 或普通 mutation stalled/retry，这些仍由 Phase 7 跟踪。

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
| Edge/Node 数量 | `DebugPanel` 一次 `CaptureRevision()` 后读取 immutable `Edges.Count` / `Nodes.Count` |

### Command Center UI Components

**文件**：`Scripts/UI/ConstructionDock.cs`, `ToolContextPanel.cs`, `DebugPanel.cs`, `PauseMenu.cs`

| 组件 | 说明 |
|---|---|
| `ConstructionDock` | 底部全宽五分类 CategoryBar 和 ToolTray；折叠高度 76px，展开高度 140px，由 64px 资产条加 76px 分类栏组成；Roads catalog 创建一个 `城市道路` 按钮；重复当前分类折叠/重开，不同分类切换内容并保持打开；没有当前工具标签或桌面宽度上限 |
| `ToolContextPanel` | 右侧只读上下文，Road 读取 catalog；Select / RoadRemove 使用内建玩家文案但不要求 submenu/catalog 资源 |
| `DebugPanel` | 默认折叠，拥有 FPS、鼠标格点、GraphEdge 与 GraphNode 指标显示 |
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
| 4 | `RoadBuilder.CommitPlace()` | 刷新最终草稿并通过 `RoadEditHistory.Execute(...)` 提交 `RoadBuildRequest(path, Street)`；输入网格不进入领域请求 |
| 5 | `RoadGraph.SubmitPath(...)` | 校验原生几何/类型，规划交点、覆盖和 canonical merge，一次提交 immutable root 与 delta |
| 6 | `RoadGraph.GraphChanged` | 渲染器按 created/removed/updated Edge 更新 cache；一次成功命令只发布一次事务事件 |
| 7 | `GameHUD._Process()` -> `DebugPanel.UpdateMetrics()` | 调试组件读取单个 revision 的 Edge/Node 数量 |

### 拆路数据流

| 步骤 | 调用 | 数据变化 |
|---|---|---|
| 1 | `ToolManager.CurrentTool = ToolType.RoadRemove` | 开启 `RoadBuilder.SetRemoveHoverActive(true)` |
| 2 | `RoadBuilder._Process()` | `UpdateRemoveHover()` 更新 `RoadRenderer.HoveredEdgeID` |
| 3 | `RoadRenderer._Draw()` | 绘制 hover 高亮 |
| 4 | `RoadBuilder.HandleRemoveInput()` | 左键拖动累积轨迹命中，`Shift+左键` 从当前矩形生成选择；预览阶段不写图 |
| 5 | `RoadBuilder.ConfirmRemove()` | 松开左键后通过 `RoadEditHistory.Execute(...)` 将排序去重的 Edge ID 集一次性交给 `RoadGraph.RemoveEdges(...)` |
| 6 | `RoadGraph.RemoveEdges(...)` | 跳过失效目标，批量 detach 后只执行一次清理和不变式验证；成功状态变化进入撤销栈 |
| 7 | `RoadGraph.GraphChanged` | 以一个排序 summary 通知 renderer 删除/更新/新增 owner，并安排批次重建 |

### 道路编辑历史流

| 阶段 | 调用 | 内容 |
|---|---|---|
| 记录 | `RoadEditHistory.Execute(...)` | 在 graph commit 前检查 delta 字节预算；成功后保留可逆 delta/token，并按 entry/字节双预算淘汰 |
| 撤销/重做入口 | `GameHUD` -> `ToolManager` -> `RoadBuilder` | 当前 `edit_undo` / `edit_redo` 绑定触发；先取消尚未提交的铺路/拆路会话 |
| 恢复 | `RoadEditHistory.Undo/Redo()` -> `RoadGraph.ApplyDelta(...)` | 按 reverse/forward 应用 changed entities，恢复 content revision 并分配新 sequence |
| 渲染同步 | `RoadGraph.GraphChanged` -> `RoadRenderer.OnGraphChanged()` | 普通 delta 增量更新 cache；full reset 清空并按新 root 全量重建 |
| 历史失效 | history scope 外的 `GraphChanged` | 立即清空旧两栈；V3 Load 的新 lineage 也让旧 token 返回 `StaleGraphState` |

### 存档流

| 阶段 | 调用 | 内容 |
|---|---|---|
| 注册 | `RoadSystem._Ready()` | `SaveManager.Instance.Register(Graph)` |
| 注销 | `RoadSystem._ExitTree()` | `SaveManager.Instance.Unregister(Graph)` |
| 周期入口 | `AutosaveController` 的场景内 `Timer` | 默认每 300 秒调用 `SaveManager.SaveAutosave()`；场景暂停时不计时 |
| 自动保存 | `SaveManager.SaveAutosave()` | 覆盖保留 `autosave` 槽，不切换当前手动槽；事务失败保留上一份有效自动存档 |
| 列举入口 | `PauseMenu` 打开存档管理视图 | `SaveManager.ListSlots()` 返回有效及损坏槽摘要，不加载业务 JSON |
| 新建入口 | `PauseMenu` 提交新显示名 | `SaveManager.SaveAs(displayName)` 生成独立 `manual-<GUID>` 槽 |
| 覆盖入口 | `PauseMenu` 确认目标摘要 | `SaveManager.Save(slotID)` 覆盖已存在槽；取消不写文件 |
| 保存文件 | `SaveManager.Save/SaveAs/SaveAutosave()` | 在 `user://saves-v3` 写 canonical `road_network.json` 与严格 V3 `manifest.json`；不写相机状态 |
| 加载入口 | `PauseMenu` 确认目标摘要 | `SaveManager.Load(slotID)`；取消不改变当前槽位或活动道路 |
| 删除入口 | `PauseMenu` 请求并确认目标摘要 | `RequestDeleteSlot(slotID)` 绑定当前 UI generation/kind/digest，`ConfirmDeleteSlot(slotID, token)` 发布 delete descriptor 后 tombstone 删除 |
| RoadGraph 恢复 | `SaveSlotStore.Load()` -> `RoadGraph.PrepareLoad/CommitPreparedLoad` | 同句柄有界 token/长度/hash/EOF 与严格 payload 预检后 full reset，采用 payload watermark并创建新 lineage |
| 渲染恢复 | `RoadRenderer.OnGraphChanged(IsFullReset)` | 清空并按新活动 root 全量重建连续道路 mesh 与节点 MultiMesh |

---

## 11. 道路存档词汇说明

| 词汇 | 当前含义 | 状态 |
|---|---|---|
| `formatFamily` | manifest 与 RoadGraph payload 的必填字符串 | 只接受大小写精确的 `simple-cities-v3` |
| `schemaVersion` | manifest 与 payload 的独立必填整数 | 当前都只接受规范整数 token `1` |
| `payloadType` | RoadGraph payload 的必填 discriminator | 只接受 `road-network` |
| JSON 字段 `nodes` / `edges` | canonical `GraphNode` / `GraphEdge` 集合 | 当前活动字段；Edge 内联 `roadType` 与六类原生 geometry |
| `nextID` | RoadGraph ID watermark | 大于所有实体 ID；Load 后由新 lineage 精确采用 |
| `groups` / `groupID` | V2 提交来源字段 | V3 严格拒绝，不迁移、不忽略、不补默认值 |
| manifest `files[]` | 业务 payload 描述 | 每项绑定大小写精确名称、encoded length 与 SHA-256 |

V3 不扫描、读取、迁移、覆盖或删除 V2 保存根。V2/未知目录被复制到 V3 根后只分类为 `Foreign`；声明 V3 family 但损坏的槽分类为 `CorruptV3`。manifest/length/hash、有界 token reader、operation-specific publish/delete descriptor、跨进程 OS 根锁、quarantine/tombstone 和 digest 恢复矩阵均已由 `v3-save-system:2.2` 完成；进程内 async coordinator、publish lease 与 aggregate Load 继续由 `v3-save-system:2.3` 跟踪。
