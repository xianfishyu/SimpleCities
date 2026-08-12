# 基础设施指南 - 框架代码说明

> 本文档描述 SimpleCities 当前已存在的基础设施代码。道路系统的 RoadGraph 迁移已经完成，
> 后续工作应基于现有文件扩展，而不是按旧草案重新生成同名系统。
>
> 当前道路架构与 V2 历史验收见 `road-system-v2-gen.md`，第三代 canonical Edge、环路与道路分级实施契约见 `road-system-v3-gen.md`，网格系统规范见 `grid-system.md`，存档系统详细契约见 `../reference/save-system-plan.md`。

---

## 1. 项目骨架

### 1.1 引擎与语言

- **引擎**: Godot 4.7
- **语言**: C# (.NET 10.0)
- **项目名**: SimpleCities
- **项目文件**: `SimpleCities.csproj` / `SimpleCities.sln`
- **主场景**: `Scenes/MapTest.tscn`（在 `project.godot` 中注册为 `run/main_scene`）

### 1.2 Autoload（自动加载单例）

| Autoload | 脚本 | 用途 |
|----------|------|------|
| `ImGuiRoot` | `addons/imgui-godot/data/ImGuiRoot.tscn` | ImGui.NET 插件根场景（脚本为 `scripts/ImGuiRoot.gd`） |
| `SaveManager` | `Scripts/Core/SaveManager.cs` | 全局存档/读档管理器 |
| `InputBindingManager` | `Scripts/Core/InputBindingManager.cs` | 可配置键盘动作、冲突校验和 `user://input_bindings.cfg` 持久化 |
| `MCPGameBridge` | `addons/godot_mcp/game_bridge/mcp_game_bridge.gd` | Godot MCP 运行时桥接 |

### 1.3 目录结构

```
Scripts/
├── Core/          ← 持久化基础设施
├── Grid/          ← GridSystem + Shader 背景
├── Road/          ← 当前 RoadGraph 运行时
├── Tools/         ← 工具管理器
├── UI/            ← HUD + UI 框架
└── MainCamera.cs  ← 2D 相机

Scenes/
├── MapTest.tscn        ← 主场景
├── map_background.tscn ← Shader 网格背景
└── UI/GameHUD.tscn     ← HUD 场景布局

Shaders/
└── MapTerrain.gdshader ← 背景网格着色器
```

---

## 2. 存档系统（`Scripts/Core/`）

本章只说明基础设施边界。存档目录、schema、运行时验证状态、已知限制和未来目标见 [存档系统当前参考](../reference/save-system-plan.md)。

### 2.1 ISaveable 与两阶段恢复

```csharp
// 文件: Scripts/Core/ISaveable.cs
public interface ISaveable
{
    string SaveFileName { get; }
    object CaptureState();
    void RestoreState(string json);
}

public interface IPreparedSaveable : ISaveable
{
    object PrepareRestoreState(string json);
    void RestorePreparedState(object preparedState);
}
```

**约定**：
- 各子系统在 `_Ready()` 中调用 `SaveManager.Instance.Register(this)` 注册并检查冲突结果，在 `_ExitTree()` 中调用 `Unregister(this)`
- `CaptureState()` 返回一个 DTO 对象，由 `SaveJson.Serialize()` 转为 JSON
- 正式业务系统应实现 `IPreparedSaveable`，在准备阶段完成解析与校验，统一提交阶段只应用已验证模型
- 注册表是扩展入口；当前 V2 配置只选择 `RoadGraph.SaveFileName == "road_network"`

### 2.2 SaveManager

```csharp
// Autoload 单例，全局通过 SaveManager.Instance 访问
public partial class SaveManager : Node
{
    public bool Register(ISaveable saveable);
    public bool Unregister(ISaveable saveable);
    public bool Save(string slotID = AutosaveSlotID);
    public bool SaveAs(string displayName);
    public bool SaveAutosave();
    public bool Load(string slotID = AutosaveSlotID);
    public bool SaveSlotExists(string slotID);
    public IReadOnlyList<SaveSlotSummary> ListSlots();
    public bool DeleteSlot(string slotID);
    public string CurrentSlotID { get; }
    public int RegisteredSaveableCount { get; }
}
```

`Register` 对同一对象幂等，但会拒绝另一个活动对象占用相同 `SaveFileName`。`RoadSystem` 和 `MainCamera` 都在 `_ExitTree()` 注销，避免返回主菜单再进入城市后保留上一场景的对象；相机不进入当前 V2 槽。

**存档流程**：选择当前配置 → 全部 `CaptureState()` 和内存序列化 → 写完整 `.<slotID>.staging` → 旧槽移动为 backup → 整目录发布。失败恢复旧槽，读写入口会恢复中断事务。

**加载流程**：验证 manifest 和全部必需文件 → 解析全部 JSON → 准备全部临时模型 → 统一提交。当前 V2 只有 RoadGraph 一个业务提交；未来多系统提交回滚仍需单独设计。

**存档目录结构**：
```
<save-root>/
├── autosave/
│   ├── manifest.json
│   └── road_network.json
└── manual-<GUID>/
    ├── manifest.json
    └── road_network.json
```

编辑器的 `<save-root>` 是全局化后的 `res://saves`；导出版本的 `<save-root>` 是 Godot 全局化后的 `user://saves`，在 Windows 上位于当前 profile 的 `Godot/app_userdata/SimpleCities/saves`，不要求安装目录或可执行文件所在目录可写。内部槽 ID 只允许安全 ASCII 字符；玩家显示名独立写入 manifest，可使用中文、空格或重复名称。

`Windows Desktop QA` 导出预设通过自定义 `qa` feature 选择 `tests/godot/exported_save_runtime_contract.tscn`，用于验证真实导出包的可写用户目录和只读 ACL 失败语义；正式 `Windows Desktop` 预设仍启动 `MapTest`，并排除测试、现有存档和文档资源。

### 2.3 SaveJson

```csharp
// 文件: Scripts/Core/SaveJson.cs — 静态工具类
public static class SaveJson
{
    public static string Serialize(object data);
    public static T Deserialize<T>(string json);
}
```

使用 `System.Text.Json`，`WriteIndented = true`，`PropertyNameCaseInsensitive = true`。

### 2.4 SaveData.cs - DTO 定义

`Scripts/Core/SaveData.cs` 只保留通用 manifest/摘要和相机 DTO；RoadGraph 的严格 V2 DTO 位于 `RoadGraph.Persistence.cs` 私有边界：

```csharp
ManifestData     - schemaVersion, slotId, displayName, timestamp,
                   cityName, population, funds, thumbnailFile, files[]
SaveSlotSummary  - 列表摘要、有效/损坏状态和 IsAutosave
CameraData       - 注册扩展仍使用的相机 DTO，不进入当前 V2 槽

RoadGraph V2 私有 payload：
  ├── schemaVersion / nextID
  ├── nodes
  ├── edges + geometry
  └── groups
```

**约定**：DTO 是纯数据 POCO，用 `[JsonPropertyName("...")]` 固定字段名。第二代不兼容旧 `junctions/segments/roads` 或 RoadType 字段；格式变化必须提升版本并显式决定迁移或拒绝。

---

## 3. 网格系统（`Scripts/Grid/`）

### 3.1 GridSystem（静态工具类）

```csharp
// 文件: Scripts/Grid/GridSystem.cs
public static class GridSystem
{
    // 当前实现：静态 GridSystem，依赖 RoadConfig 提供 CellSize。
    // 提供 SnapToGrid() 和 IsSnapGrid()。
}
```

当前 `GridSystem.cs` 仍为调试组件等 UI 提供 `SnapToGrid()` 和 `IsSnapGrid()`。铺路不再直接调用它：`RoadBuilder` 消费 `IRoadInputStrategy`，默认 `SquareEightRoadInputStrategy` 内部复用 `Direction` / `DirectionUtil`；`TriangularThreeRoadInputStrategy` 与 `HexSixRoadInputStrategy` 以不同吸附和邻接规则通过同一草稿契约。完整邻居枚举、成本和寻路启发式仍是未来更宽的网格几何设计，不属于当前输入接口。

### 3.2 MapBackground（Shader 网格渲染）

CanvasLayer + ShaderMaterial 渲染无限网格背景。三层网格：主网格线 / 次网格线 / 点网格。**与道路系统无直接耦合，AI 不需要修改。**

---

## 4. 工具系统（`Scripts/Tools/`）

### 4.1 ToolType 枚举

```csharp
// 文件: Scripts/Tools/ToolType.cs
public enum ToolType { Select, Road, RoadRemove }
```

### 4.2 ToolManager

```csharp
// 文件: Scripts/Tools/ToolManager.cs — 单例: ToolManager.Instance
public partial class ToolManager : Node2D
{
    public ToolType CurrentTool { get; set; }
}
```

**键盘动作**由 `InputBindingManager` 定义并由 `GameHUD` 消费，以下是默认值，玩家可在暂停菜单中重绑：

| 按键 | 工具 |
|------|------|
| `Q` | `Select` — 选择 |
| `R` | `Road` — 铺路 |
| `E` | `RoadRemove` — 拆路 |
| `Z` | 撤销最近一次成功道路编辑 |
| `Y` | 重做最近一次已撤销道路编辑 |
| `Esc` | 打开暂停菜单，不改变当前工具 |

**约定**：`ToolManager._Input()` 不负责键盘切换，只根据 `CurrentTool` 将输入转发给 `RoadBuilder` 的对应方法。切出 `Road` 时调 `CancelPlaceSession()`；切出/入 `RoadRemove` 时调 `SetRemoveHoverActive(bool)`。

**注意**：`ToolManager.cs` 直接引用当前 `RoadBuilder` 类型。该类提供以下公共方法：

```csharp
public void HandlePlaceInput(InputEvent @event);
public void HandleRemoveInput(InputEvent @event);
public void CancelPlaceSession();
public void CancelRemoveSession();
public void SetRemoveHoverActive(bool active);
public bool UndoLastEdit();
public bool RedoLastEdit();
```

铺路保留旧式“按住拖拽、释放提交”单段手势。点击起点会进入连续会话：鼠标移动调整活动末端，左键固定拐点，Enter 或双击确认，右键回退最后拐点并在零拐点时取消。`RoadPlacementSession` 将固定策略草稿和活动草稿组合为同一个 `RoadPathDraft`；有效草稿的原生段先经 `RoadGeometryDisplaySampler` 生成完整 `RoadRenderer.PreviewPoints`，最终只通过一次 `RoadGraph.SubmitPath` 提交。

`RoadGeometryDisplaySampler` 以默认 `0.25` 世界单位容差和最多 16 层递归，把六类权威几何确定性细分为显示折线；直线保持两个精确端点。已提交 Edge、拆除高亮和曲线建造预览复用同一派生点列，显示采样不会写回 RoadGraph 或存档。

`RoadRenderer` 按 Edge ID 缓存这些点列，并将全部静态道路合并为一个带 miter 边界和像素抗锯齿的 `ArrayMesh` ribbon；端点/交叉口由一个圆形 shader `MultiMeshInstance2D` 绘制。同一事件循环内的 Edge 增删会合并为一次延迟批次重建，`GraphCleared` 则同步全量重建；静态渲染固定为 2 个子节点。真实 Vulkan 基线中，10k 镜头/预览/高亮 P95 为 0.788/0.717/0.436 ms，100k 为 5.240/4.612/4.739 ms；完整口径见 `docs/performance/road-rendering-v2-baseline.md`。

拆路采用“先选择、后提交”：普通左键拖动沿轨迹累积 Edge，`Shift+左键` 动态框选与矩形相交的 Edge；松开左键后才把排序去重的稳定 ID 集交给一次 `RoadGraph.RemoveEdges`。右键、切出拆路工具或替换输入策略会取消整个选择，图保持不变；简单点击仍是单 Edge 拆除。

铺路和拆路的成功提交由 `RoadEditHistory` 包装为完整图状态事务，最多保留 64 次。撤销/重做前取消未提交的铺路或拆路会话，再通过严格 RoadGraph 恢复重建拓扑、原生几何和渲染；Node/Edge/Group ID 保持，但运行时实体对象引用会重建。失败编辑不入栈，外部恢复或其他外部图修改会使旧历史失效。

---

## 5. UI 系统（`Scripts/UI/`）

### 5.1 GameHUD

CanvasLayer 浮层，组合底部 ConstructionDock、右侧 ToolContextPanel、默认折叠 DebugPanel 和全屏 PauseMenu。

**依赖**（通过单例访问）：
- `ToolManager.Instance` → 工具状态
- `RoadSystem.Instance.Graph` → 路网统计
- `MainCamera.Instance` → 鼠标世界坐标
- `SaveManager.Instance` → 存读档
- `InputBindingManager.Instance` → 暂停、工具动作和快捷键显示

**当前状态**：`DebugPanel` 从 `RoadSystem.Instance.Graph` 读取 Group / Edge / Node 数量，并使用当前 GridSystem 显示鼠标格点；`ToolContextPanel` 读取道路工具目录显示只读说明。

### 5.2 Theme 和 UIManager

`CommandCenterTheme.tres`：统一 Label / Button / Panel 的命令中心样式。
`UIManager`：真实受管面板生命周期管理（`Register`、`Show`、`Hide`、`PushModal`、`PopModal`）。

**AI 不需要修改这两个文件。**

---

## 6. 相机（`Scripts/MainCamera.cs`）

Camera2D 单例。支持 WASD 平移、中键拖拽、滚轮缩放（0.125× ~ 4×）、Lerp 平滑过渡。实现 `ISaveable`（存档位置 + 缩放）。

**AI 不需要修改。**

---

## 7. 主场景结构（`Scenes/MapTest.tscn`）

```
MapTest (Node2D)
├── Camera2D                    ← MainCamera.cs [已存在]
├── 31245427P0 (Sprite2D)       ← 参考图（visible=false）
├── RoadSystem (Node2D)         ← RoadSystem.cs
│   ├── RoadRenderer (Node2D)   ← RoadRenderer.cs
│   └── RoadBuilder (Node2D)    ← RoadBuilder.cs
├── ToolManager (Node2D)        ← ToolManager.cs
├── GameHUD (CanvasLayer)       ← GameHUD.cs
└── MapBackground (CanvasLayer) ← MapBackground.cs [已存在]
```

**当前状态**：`MapTest.tscn` 已挂载当前 RoadSystem / RoadRenderer / RoadBuilder 运行时。未来修改场景资源时应通过 Godot 编辑器或 scene reload 验证实际加载状态。

---

## 8. 当前状态与未来设计边界

### 当前已实现

```
Scripts/Core/ISaveable.cs
Scripts/Core/SaveManager.cs
Scripts/Core/SaveJson.cs
Scripts/Core/SaveData.cs
Scripts/Core/InputBindingManager.cs
Scripts/Grid/GridSystem.cs
Scripts/Grid/MapBackground.cs
Scripts/Road/RoadGraph.cs
Scripts/Road/GraphNode.cs
Scripts/Road/GraphEdge.cs
Scripts/Road/RoadGroup.cs
Scripts/Road/RoadSystem.cs
Scripts/Road/RoadBuilder.cs
Scripts/Road/Input/IRoadInputStrategy.cs
Scripts/Road/Input/RoadPathDraft.cs
Scripts/Road/Input/RoadPlacementSession.cs
Scripts/Road/Input/RoadRemovalSession.cs
Scripts/Road/Input/RoadEditHistory.cs
Scripts/Road/Input/SquareEightRoadInputStrategy.cs
Scripts/Road/Input/TriangularThreeRoadInputStrategy.cs
Scripts/Road/Input/HexSixRoadInputStrategy.cs
Scripts/Road/RoadRenderer.cs
Scripts/Road/RoadGeometryDisplaySampler.cs
Scripts/Road/SpatialIndex.cs
Scripts/Road/RoadConfig.cs
Scripts/Road/Direction.cs
Scripts/Tools/ToolType.cs
Scripts/Tools/ToolManager.cs
Scenes/UI/Themes/CommandCenterTheme.tres
Scripts/UI/UIManager.cs
Scripts/UI/ConstructionDock.cs
Scripts/UI/ToolContextPanel.cs
Scripts/UI/DebugPanel.cs
Scripts/UI/PauseMenu.cs
Scripts/UI/MainMenu.cs
Scripts/MainCamera.cs
Scenes/MainMenu.tscn
Scenes/map_background.tscn
Scenes/UI/PauseMenu.tscn
Scenes/UI/GameHUD.tscn
Shaders/MapTerrain.gdshader
project.godot
```

### 未来设计边界

可替换铺路边界已经由 `IRoadInputStrategy` 和 `RoadPathDraft` 落地；默认米字型、三角单元中心和六边形单元中心策略均通过共享契约。`RoadPlacementSession` 与 `RoadBuilder` 已支持连续多段、拐点回退、完整预览、确认和取消，并只经 `RoadGraph.SubmitPath` 一次提交；`RoadRemovalSession` 已支持连续轨迹和矩形框选，并只经 `RoadGraph.RemoveEdges` 一次提交。`RoadEditHistory` 已用完整 RoadGraph 状态提供容量受限的撤销重做，并经 `GraphCleared` 让渲染同步重建。六类原生曲线显示采样与 10k/100k 批处理规模验收均已完成；canonical Edge、自环/平行 Edge、RoadGroup 移除、`TrafficGraph`、A* 寻路、道路分级 UI 和按 RoadType 差异化渲染仍属于未来工作。第二代道路 JSON 只使用严格版本化的 `nodes/edges/groups` 与原生几何；第三代将以 schema 2 同时迁移规范存储和 RoadType，详细契约见第三代指南。
