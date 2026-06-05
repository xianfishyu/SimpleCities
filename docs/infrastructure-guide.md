# 基础设施指南 — 框架代码说明

> 本文档描述项目中已存在的基础设施代码，AI 生成道路系统时**不需要重写这些文件**，
> 但需要理解它们的接口和约定以正确集成。

---

## 1. 项目骨架

### 1.1 引擎与语言

- **引擎**: Godot 4.6
- **语言**: C# (.NET 10.0)
- **项目名**: SimpleCities-LLM
- **项目文件**: `SimpleCities.csproj` / `SimpleCities.sln`
- **主场景**: `Scenes/MapTest.tscn`（在 `project.godot` 中注册为 `run/main_scene`）

### 1.2 Autoload（自动加载单例）

`project.godot` 注册了两个 Autoload：

| Autoload | 脚本 | 用途 |
|----------|------|------|
| `SaveManager` | `Scripts/Core/SaveManager.cs` | 全局存档/读档管理器 |
| `ImGuiRoot` | `addons/imgui-godot/` | ImGui 调试 UI（已弃用，仅保留） |

### 1.3 目录结构

```
Scripts/
├── Core/          ← 持久化基础设施（保留）
├── Grid/          ← 网格数学 + Shader 背景（保留）
├── Road/          ← 道路系统（目标：AI 从零生成）
├── Tools/         ← 工具管理器（保留，需扩展）
├── UI/            ← HUD + UI 框架（保留，需更新统计字段）
└── MainCamera.cs  ← 2D 相机（保留）

Scenes/
├── MapTest.tscn        ← 主场景（保留节点层级，AI 修复脚本引用）
├── map_background.tscn ← Shader 网格背景
├── road_config.tres    ← 道路配置（AI 从零创建）
└── UI/GameHUD.tscn     ← HUD 场景布局

Shaders/
└── MapTerrain.gdshader ← 背景网格着色器

addons/imgui-godot/     ← ImGui 插件（保留，不涉及道路）
```

---

## 2. 持久化系统（`Scripts/Core/`）

### 2.1 ISaveable 接口

```csharp
// 文件: Scripts/Core/ISaveable.cs
public interface ISaveable
{
    string SaveFileName { get; }           // 存档文件名，如 "road_network"
    object CaptureState();                 // 返回纯数据 DTO
    void RestoreState(string json);        // 从 JSON 恢复
}
```

**约定**：
- `CaptureState()` 返回一个 DTO 对象（如 `RoadNetworkData`），由 `SaveJson.Serialize()` 转为 JSON
- `RestoreState(string json)` 接收原始 JSON，内部调用 `SaveJson.Deserialize<YourDTO>(json)` 后自行重建状态
- 各子系统在 `_Ready()` 中调用 `SaveManager.Instance.Register(this)` 注册

### 2.2 SaveManager

```csharp
// Autoload 单例，全局通过 SaveManager.Instance 访问
public partial class SaveManager : Node
{
    public static SaveManager Instance { get; }

    public void Register(ISaveable saveable);    // 注册持久化系统
    public bool Save(string slotName = "autosave");   // 保存所有已注册系统
    public bool Load(string slotName = "autosave");   // 加载所有已注册系统
    public bool SaveSlotExists(string slotName);
    public void DeleteSlot(string slotName);
}
```

**存档流程**：
1. `Save()` 遍历所有 `_saveables`，对每个调 `CaptureState()` → `SaveJson.Serialize()` → 原子写入 `.json`（先写 `.tmp` 再 rename）
2. 存档文件写入 `user://saves/{slotName}/` 目录
3. `Load()` 读取 manifest → 按文件名匹配 `_saveables` → 调 `RestoreState(json)`

**存档目录结构**：
```
user://saves/autosave/
├── manifest.json          ← 元数据（槽名、时间戳、文件列表）
├── road_network.json      ← 路网数据（需 AI 定义 DTO）
├── main_camera.json       ← 相机数据（已存在，CameraData）
└── ...
```

### 2.3 SaveJson

```csharp
// 文件: Scripts/Core/SaveJson.cs
public static class SaveJson
{
    public static string Serialize(object data);
    public static T Deserialize<T>(string json);
}
```

使用 `System.Text.Json`，配置为 `WriteIndented = true`、`PropertyNameCaseInsensitive = true`。

### 2.4 SaveData.cs — DTO 定义

`Scripts/Core/SaveData.cs` 包含所有存档 DTO 类。当前定义了：

```csharp
// 已存在的 DTO（保留）：
ManifestData     — 存档槽元数据
CameraData       — 相机位置 + 缩放
Vector2Data      — Vector2 的 JSON 安全表示（{x, y}）

// 道路系统 DTO（AI 需重新定义，因为 RoadNetwork → RoadGraph 重命名）：
RoadNetworkData  — 旧名，AI 应重命名为 RoadGraphData
JunctionData     — 旧名，AI 应重命名为 NodeData
SegmentData      — 旧名，AI 应重命名为 EdgeData
RoadData         — 旧名，AI 应重命名为 GroupData
```

**AI 的任务**：在 `SaveData.cs` 中替换道路 DTO 为新的命名和结构（或用独立文件）。`ManifestData`、`CameraData`、`Vector2Data` **保持不变**。

---

## 3. 网格系统（`Scripts/Grid/`）

### 3.1 GridSystem（静态工具类）

```csharp
// 文件: Scripts/Grid/GridSystem.cs
public static class GridSystem
{
    public static RoadConfig Config { get; set; }   // 由 RoadSystem._Ready() 注入
    public static float CellSize { get; }           // 读取 Config.CellSize

    public static Vector2 SnapToGrid(Vector2 pos);  // 吸附到最近的 CellSize 整数倍
    public static bool IsSnapGrid(Vector2 pos);     // 位置是否在整数倍格点上（容差 1e-3）
}
```

**设计指南中的建议**：`SnapToGrid` / `IsSnapGrid` 仅用于 UI 层（RoadBuilder），数据层（RoadGraph）不依赖它们。

### 3.2 MapBackground（Shader 网格渲染）

CanvasLayer + ShaderMaterial 渲染无限网格背景。三层网格：主网格线 / 次网格线 / 点网格。相机位置和缩放自动适配。**与道路系统无直接耦合。**

---

## 4. 工具系统（`Scripts/Tools/`）

### 4.1 ToolType 枚举

```csharp
// 文件: Scripts/Tools/ToolType.cs
public enum ToolType
{
    Select,      // 默认工具，无操作
    Road,        // 铺路工具（拖拽画线）
    RoadRemove   // 拆除工具（点击拆段）
}
```

### 4.2 ToolManager

```csharp
// 文件: Scripts/Tools/ToolManager.cs
// 单例: ToolManager.Instance
public partial class ToolManager : Node2D
{
    public ToolType CurrentTool { get; set; }   // 设置时自动处理切换上下文
}
```

**键盘快捷键**：

| 按键 | 工具 |
|------|------|
| `R` | `Road` — 铺路 |
| `E` | `RoadRemove` — 拆路 |
| `Esc` | `Select` — 选择 |

**输入转发**：`_Input()` 中根据 `CurrentTool` 将鼠标事件转发给 `RoadBuilder.HandlePlaceInput()` 或 `RoadBuilder.HandleRemoveInput()`。

**工具切换守卫**：切出 `Road` 时调 `RoadBuilder.CancelPlaceDrag()`；切出/入 `RoadRemove` 时调 `RoadBuilder.SetRemoveHoverActive(bool)`。

---

## 5. UI 系统（`Scripts/UI/`）

### 5.1 GameHUD

CanvasLayer 浮层，常驻显示：
- FPS
- 当前工具名
- 鼠标世界坐标（吸附到格点后的坐标，标记 [路口] 若该位置有 Junction）
- 路网统计（Road/Segment/Junction 数量）
- 工具切换按钮（选择 / 铺路 / 拆路）
- 存读档按钮 + F5/F9 快捷键

**依赖**（通过单例访问）：
- `ToolManager.Instance` → 工具状态
- `RoadSystem.Instance.Network` → 路网统计
- `MainCamera.Instance` → 鼠标世界坐标
- `SaveManager.Instance` → 存读档

**AI 需更新的部分**：`UpdateMousePos()` 和 `UpdateRoadStats()` 中的方法名随 RoadGraph API 变化（如 `GetAllRoads()` → `GetAllGroups()`，`HasJunctionAt()` → `FindClosestNode()`）。

### 5.2 UIHelpers

静态工厂类：`CreateLabel()`、`CreateToolButton()`、`CreateDarkPanel()` — 统一样式。

### 5.3 UIManager

面板生命周期管理：`Register()`、`Show()`、`Hide()`、`PushModal()`、`PopModal()`。

---

## 6. 相机（`Scripts/MainCamera.cs`）

Camera2D 单例。支持：
- WASD 键盘平移
- 鼠标中键拖拽平移
- 滚轮缩放（0.125× ~ 4×）
- Lerp 平滑过渡
- 实现 `ISaveable`（存档位置 + 缩放）

---

## 7. 主场景结构（`Scenes/MapTest.tscn`）

```
MapTest (Node2D)
├── Camera2D                    ← MainCamera.cs
├── 31245427P0 (Sprite2D)       ← 参考图（visible=false）
├── RoadSystem (Node2D)         ← RoadSystem.cs [AI 需生成]
│   ├── RoadRenderer (Node2D)   ← RoadRenderer.cs [AI 需生成]
│   └── RoadBuilder (Node2D)    ← RoadBuilder.cs [AI 需生成]
├── ToolManager (Node2D)        ← ToolManager.cs [已存在]
├── GameHUD (CanvasLayer)       ← GameHUD.cs [已存在，需更新]
└── MapBackground (CanvasLayer) ← MapBackground.cs [已存在]
```

**AI 需要处理**：`MapTest.tscn` 中 RoadSystem/RoadRenderer/RoadBuilder 的 `ext_resource` 引用指向即将被删除的脚本文件。AI 生成新脚本后，需修复 `.tscn` 中的 `uid` 引用（或在 Godot 编辑器中重新挂载脚本）。

---

## 8. AI 不需要修改的文件清单

以下文件**完全不涉及道路逻辑**，AI 不需要阅读或修改：

```
Scripts/Core/ISaveable.cs
Scripts/Core/SaveManager.cs
Scripts/Core/SaveJson.cs
Scripts/Grid/MapBackground.cs
Scripts/Tools/ToolType.cs
Scripts/Tools/ToolManager.cs
Scripts/UI/UIHelpers.cs
Scripts/UI/UIManager.cs
Scripts/MainCamera.cs
Scenes/map_background.tscn
Scenes/UI/GameHUD.tscn
Shaders/MapTerrain.gdshader
project.godot
addons/imgui-godot/*
```

AI **需要生成**的新文件：
```
Scripts/Road/（全部 .cs 文件）
Scenes/road_config.tres
```

AI **需要修改**的现有文件：
```
Scripts/Core/SaveData.cs          ← 替换道路 DTO
Scripts/UI/GameHUD.cs             ← 更新统计方法名
Scenes/MapTest.tscn               ← 修复脚本引用 uid
```
