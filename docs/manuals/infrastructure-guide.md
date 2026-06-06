# 基础设施指南 — 框架代码说明

> 本文档描述项目中已存在的基础设施代码。AI 生成道路系统时**不需要重写这些文件**，
> 但需要理解它们的接口和约定以正确集成。
>
> 道路系统的架构蓝图见 `road-system-next-gen.md`，网格系统规范见 `grid-system.md`。

---

## 1. 项目骨架

### 1.1 引擎与语言

- **引擎**: Godot 4.6
- **语言**: C# (.NET 10.0)
- **项目名**: SimpleCities-LLM
- **项目文件**: `SimpleCities.csproj` / `SimpleCities.sln`
- **主场景**: `Scenes/MapTest.tscn`（在 `project.godot` 中注册为 `run/main_scene`）

### 1.2 Autoload（自动加载单例）

| Autoload | 脚本 | 用途 |
|----------|------|------|
| `SaveManager` | `Scripts/Core/SaveManager.cs` | 全局存档/读档管理器 |

### 1.3 目录结构

```
Scripts/
├── Core/          ← 持久化基础设施（保留，不修改）
├── Grid/          ← 网格数学 + Shader 背景（保留）
├── Road/          ← 道路系统（目标：AI 从零生成）
├── Tools/         ← 工具管理器（保留，AI 不改）
├── UI/            ← HUD + UI 框架（保留，AI 需更新统计方法名）
└── MainCamera.cs  ← 2D 相机（保留）

Scenes/
├── MapTest.tscn        ← 主场景（保留节点层级，AI 修复脚本引用）
├── map_background.tscn ← Shader 网格背景
└── UI/GameHUD.tscn     ← HUD 场景布局

Shaders/
└── MapTerrain.gdshader ← 背景网格着色器
```

---

## 2. 持久化系统（`Scripts/Core/`）

### 2.1 ISaveable 接口

```csharp
// 文件: Scripts/Core/ISaveable.cs
public interface ISaveable
{
    string SaveFileName { get; }           // 存档文件名，如 "road_graph"
    object CaptureState();                 // 返回纯数据 DTO
    void RestoreState(string json);        // 从 JSON 恢复
}
```

**约定**：
- 各子系统在 `_Ready()` 中调用 `SaveManager.Instance.Register(this)` 注册
- `CaptureState()` 返回一个 DTO 对象，由 `SaveJson.Serialize()` 转为 JSON
- `RestoreState(string json)` 接收原始 JSON，内部自行反序列化和重建状态

### 2.2 SaveManager

```csharp
// Autoload 单例，全局通过 SaveManager.Instance 访问
public partial class SaveManager : Node
{
    public void Register(ISaveable saveable);
    public bool Save(string slotName = "autosave");
    public bool Load(string slotName = "autosave");
    public bool SaveSlotExists(string slotName);
    public void DeleteSlot(string slotName);
}
```

**存档流程**：遍历所有 `_saveables` → `CaptureState()` → `SaveJson.Serialize()` → 原子写入 `.json`（先写 `.tmp` 再 rename）。

**存档目录结构**：
```
user://saves/autosave/
├── manifest.json          ← 元数据
├── road_graph.json        ← 路网数据（AI 需定义 DTO）
├── main_camera.json       ← 相机数据（已存在：CameraData）
└── ...
```

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

### 2.4 SaveData.cs — DTO 定义

`Scripts/Core/SaveData.cs` 包含所有存档 DTO 类。**AI 需要在此文件中定义路网 DTO**：

```csharp
// 已存在的 DTO（保留，AI 不改）：
ManifestData     — 存档槽元数据（schemaVersion, slotName, timestamp, files[]）
CameraData       — 相机位置 + 缩放（positionX, positionY, zoom）
Vector2Data      — Vector2 的 JSON 安全表示（{x, y}），含 ToVector2() 方法

// 道路系统 DTO（AI 需定义，建议命名）：
RoadGraphData    — 路网存档根对象
  ├── NextID
  ├── Nodes[]    — 每个含 {id, x, y}
  ├── Edges[]    — 每个含 {id, nodeA, nodeB, waypoints[], length, groupID, roadType}
  └── Groups[]   — 每个含 {id, type, edgeIDs[]}
```

**约定**：DTO 类是纯数据 POCO，用 `[JsonPropertyName("...")]` 标注字段名。不引用任何 Godot 运行时类型（除了 `Vector2Data` 替代 `Vector2`）。

---

## 3. 网格系统（`Scripts/Grid/`）

### 3.1 GridSystem（静态工具类）

```csharp
// 文件: Scripts/Grid/GridSystem.cs
public static class GridSystem
{
    // AI 需重新设计：不再依赖 RoadConfig。
    // 建议改为持有 IGridGeometry 实例，由 RoadBuilder 初始化时注入。
    // 默认使用 Square8Grid（正方 8 方向，CellSize 可由构建参数指定）。
}
```

当前 `GridSystem.cs` 中有 `SnapToGrid()` 和 `IsSnapGrid()` 两个静态方法，它们与 `RoadConfig.CellSize` 耦合。AI 需要将这些逻辑迁移到 `Square8Grid : IGridGeometry` 实现中，`GridSystem` 变为薄封装。

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

**键盘快捷键**：

| 按键 | 工具 |
|------|------|
| `R` | `Road` — 铺路 |
| `E` | `RoadRemove` — 拆路 |
| `Esc` | `Select` — 选择 |

**约定**：`_Input()` 中根据 `CurrentTool` 将鼠标事件转发给 `RoadBuilder` 的对应方法。切出 `Road` 时调 `CancelPlaceDrag()`；切出/入 `RoadRemove` 时调 `SetRemoveHoverActive(bool)`。

**注意**：`ToolManager.cs` 直接引用 `RoadBuilder` 类型。AI 需要确保生成的 `RoadBuilder` 类提供以下公共方法：

```csharp
public void HandlePlaceInput(InputEvent @event);
public void HandleRemoveInput(InputEvent @event);
public void CancelPlaceDrag();
public void SetRemoveHoverActive(bool active);
```

---

## 5. UI 系统（`Scripts/UI/`）

### 5.1 GameHUD

CanvasLayer 浮层，常驻显示：FPS、当前工具、鼠标格点坐标、路网统计、工具按钮、存读档按钮。

**依赖**（通过单例访问）：
- `ToolManager.Instance` → 工具状态
- `RoadSystem.Instance.Graph` → 路网统计（**方法名随 AI 实现变化**）
- `MainCamera.Instance` → 鼠标世界坐标
- `SaveManager.Instance` → 存读档

**AI 需更新的部分**：`GameHUD.cs` 中目前调用了 `RoadNetwork.SnapToGrid()`、`HasJunctionAt()`、`GetAllRoads()` 等旧方法。AI 生成新的 RoadGraph 后，需将 `GameHUD.cs` 中的统计查询更新为对应的新 API（如 `GetAllGroups()`、`FindClosestNode()` 等）。

### 5.2 UIHelpers 和 UIManager

`UIHelpers`：静态工厂（`CreateLabel`、`CreateToolButton`、`CreateDarkPanel`）。
`UIManager`：面板生命周期管理（`Register`、`Show`、`Hide`、`PushModal`、`PopModal`）。

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
├── RoadSystem (Node2D)         ← [AI 需生成 RoadSystem.cs]
│   ├── RoadRenderer (Node2D)   ← [AI 需生成 RoadRenderer.cs]
│   └── RoadBuilder (Node2D)    ← [AI 需生成 RoadBuilder.cs]
├── ToolManager (Node2D)        ← ToolManager.cs [已存在]
├── GameHUD (CanvasLayer)       ← GameHUD.cs [已存在，AI 需更新]
└── MapBackground (CanvasLayer) ← MapBackground.cs [已存在]
```

**AI 需要处理**：`MapTest.tscn` 中 RoadSystem/RoadRenderer/RoadBuilder 的 `ext_resource` 引用指向即将被删除的旧脚本。AI 生成新脚本后，需修复 `.tscn` 中的 `uid` 引用（可在 Godot 编辑器中重新挂载脚本来自动生成正确的 uid）。

---

## 8. 文件变动汇总

### AI 不需要修改（14 个文件）

```
Scripts/Core/ISaveable.cs
Scripts/Core/SaveManager.cs
Scripts/Core/SaveJson.cs
Scripts/Grid/GridSystem.cs         ← AI 可改写内部实现，保留类名
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
```

### AI 需要生成（新文件）

```
Scripts/Road/RoadGraph.cs          ← 核心数据层（替代 RoadNetwork）
Scripts/Road/GraphNode.cs          ← 图节点（替代 Junction）
Scripts/Road/GraphEdge.cs          ← 图边（替代 Segment）
Scripts/Road/RoadGroup.cs          ← 逻辑分组（替代 Road）
Scripts/Road/RoadSystem.cs         ← 根节点（创建 RoadGraph + 注入子节点）
Scripts/Road/RoadBuilder.cs        ← 输入处理（拖拽铺路 / 点击拆路）
Scripts/Road/RoadRenderer.cs       ← 事件驱动渲染
Scripts/Road/SpatialIndex.cs       ← 空间哈希索引（替代 Vector2 字典）
Scripts/Road/Square8Grid.cs        ← 正方 8 方向网格（实现 IGridGeometry）
Scripts/Road/IGridGeometry.cs      ← 网格抽象接口
Scripts/Road/RoadConfig.cs         ← 道路样式配置（AI 自行设计）
```
> 文件数量和命名是建议，AI 可自行组织。

### AI 需要修改（3 个文件）

| 文件 | 改什么 |
|------|--------|
| `Scripts/Core/SaveData.cs` | 将旧 DTO（RoadNetworkData 等）替换为 RoadGraphData 等 |
| `Scripts/UI/GameHUD.cs` | 更新统计查询的方法名 |
| `Scenes/MapTest.tscn` | 修复 RoadSystem/RoadBuilder/RoadRenderer 的 ext_resource uid |
