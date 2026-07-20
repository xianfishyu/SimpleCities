# SimpleCities — AI 编码助手指南

## 项目概述

SimpleCities 是 Godot 4.7 C# 城市建造原型。当前核心是无限网格、8 方向道路编辑、路网拓扑维护、事件驱动渲染，以及 JSON 存档/加载。项目集成 ImGui 调试 UI，Phase 1 的网格与道路系统已基本可用。

当前能力：

- 8 方向道路铺设（正交 + 对角），鼠标拖拽预览并提交
- X 形交叉、端点落边和 waypoint 交叉自动拆边并创建节点
- 点击单条 `GraphEdge` 拆路，删除后自动清理孤立节点和修复共线拓扑
- 完整重复路径无副作用拒绝；部分重叠只添加未覆盖区段
- `RoadType` 数据模型：Dirt / Street / Arterial / Highway，并支持存档往返
- 工具切换：选择 (Esc) / 铺路 (R) / 拆路 (E)
- F5 保存、F9 加载，支持 RoadGraph 和相机状态
- HUD 显示 FPS、工具、鼠标格点，以及 Group / Edge / Node 数量
- Shader 驱动的无限网格背景

主场景：`Scenes/MapTest.tscn`（uid: `uid://baxamkfym8atd`）。

## 构建与运行

- **引擎**：Godot 4.7
- **SDK**：`Godot.NET.Sdk/4.7.0`
- **框架**：.NET 10.0、C# 14.0、nullable enabled、`AllowUnsafeBlocks: true`
- **依赖**：ImGui.NET 1.91.6.1
- **构建**：`dotnet build SimpleCities.sln`
- **主场景**：`Scenes/MapTest.tscn`
- **Autoload**：`ImGuiRoot`、`SaveManager`、`MCPGameBridge`
- **共享道路配置**：`Scenes/road_config.tres`

修改 C# 后至少运行一次 `dotnet build SimpleCities.sln`。以当前构建输出为准判断 warning 来源；不要把既有 warning 描述成新改动导致的问题。

## 项目结构

```text
Scripts/
├── MainCamera.cs
├── Core/
│   ├── ISaveable.cs
│   ├── SaveManager.cs
│   ├── SaveData.cs
│   └── SaveJson.cs
├── Grid/
│   ├── GridSystem.cs
│   └── MapBackground.cs
├── Road/
│   ├── RoadSystem.cs       ← 场景根节点和依赖注入
│   ├── RoadGraph.cs        ← 纯数据路网核心
│   ├── GraphNode.cs        ← 拓扑节点 + 邻接 EdgeRef
│   ├── GraphEdge.cs        ← 两节点间的几何边
│   ├── RoadGroup.cs        ← 一次铺路产生的边集合
│   ├── SpatialIndex.cs     ← UniformGrid 空间哈希
│   ├── RoadType.cs         ← 道路等级枚举
│   ├── RoadBuilder.cs      ← 输入、拖拽和 8 方向投影
│   ├── RoadRenderer.cs     ← 事件驱动 Line2D 渲染
│   ├── RoadConfig.cs       ← [GlobalClass] 共享配置
│   └── Direction.cs        ← 方向枚举与工具
├── Tools/
│   ├── ToolManager.cs
│   └── ToolType.cs
└── UI/
    ├── GameHUD.cs
    ├── UIHelpers.cs
    └── UIManager.cs

Scenes/
├── MapTest.tscn
├── map_background.tscn
├── road_config.tres
└── UI/GameHUD.tscn

Shaders/
addons/imgui-godot/         ← 第三方插件，不要无关修改
docs/                       ← 设计、实现和 bugfix 文档
```

不要重新引入已经删除的 `RoadNetwork`、`Road`、`Segment`、`Junction` 类型。当前数据层术语统一为 `RoadGraph`、`RoadGroup`、`GraphEdge`、`GraphNode`。

## 核心架构

### RoadSystem（场景节点）

`RoadSystem._Ready()` 是道路系统组装入口：

1. 注册 `RoadSystem.Instance`
2. 创建纯数据对象 `RoadGraph`
3. 从 `RoadBuilder.Config` 获取共享 `RoadConfig` 并注入 `GridSystem.Config`
4. 调用 `RoadRenderer.SetGraph(Graph)` 和 `RoadBuilder.SetGraph(Graph)`
5. 向 `SaveManager` 注册 `RoadGraph`

```csharp
public partial class RoadSystem : Node2D
{
    public RoadGraph Graph { get; private set; } = null!;
    public static RoadSystem Instance { get; private set; } = null!;
}
```

### RoadGraph（纯数据层）

`RoadGraph` 不继承 Godot 节点，只负责拓扑、几何、空间查询和持久化：

- `_nodes: Dictionary<int, GraphNode>`：节点实体
- `_edges: Dictionary<int, GraphEdge>`：几何边实体
- `_groups: Dictionary<int, RoadGroup>`：玩家一次铺路操作形成的边集合
- `_spatialIndex: UniformGrid`：节点和边途经点的半径查询
- `_nodeRefs` / `_edgeRefs`：保存实际插入索引的引用，保证移除精确对应
- 事件：`EdgeAdded`、`EdgeRemoved`、`GraphCleared`
- `ISaveable.SaveFileName = "road_network"`

三层模型：

- **RoadGroup**：持有 `EdgeIDs` 和 `RoadType`；不是连通性算法的替代品
- **GraphEdge**：`NodeA`、`NodeB`、中间 `Points`、`GroupID`、`Type`、`Length`
- **GraphNode**：位置和 `EdgeRef` 邻接表；`EdgeCount` 用于端点/路口判断和自动合并

### 空间索引

`UniformGrid` 按固定 bucket 保存 `NodeSpatialRef` 和 `EdgePointRef`。它用于 `FindClosestNode`、`FindClosestEdge`、交叉候选筛选和节点复用，而不是权威数据源。

维护规则：

- 新增节点时同时写入 `_nodes`、`_nodeRefs` 和 `_spatialIndex`
- 新增边时为 NodeA、所有 `Points`、NodeB 建立空间引用，并保存到 `_edgeRefs[edge.ID]`
- 删除边时必须使用 `_edgeRefs` 中的原对象移除索引，随后清除该映射
- 加载存档后必须重建节点邻接关系与完整空间索引
- 不要仅修改字典而遗漏邻接表、RoadGroup、空间索引或事件

### GridSystem 与方向

`GridSystem` 集中持有 `RoadConfig`，提供 `CellSize`、`SnapToGrid()` 和 `IsSnapGrid()`。初始化路径为 `RoadSystem._Ready()` → `GridSystem.Config = config`。

- `DirectionUtil.FromDisplacement(...)`：按单位格距离判断方向
- `DirectionUtil.FromDisplacementAnyLength(...)`：按任意长度判断 8 方向
- `DirectionUtil.GetDisplacement(...)`：返回方向的整数位移
- `DirectionUtil.Length(...)`：返回正交/对角步长

半格起点仅允许对角延伸；`RoadBuilder` 会先反向定位整格 anchor，再生成终点和 waypoints。

### RoadBuilder（输入与投影）

- `SetGraph(RoadGraph)` 注入数据层
- `HandlePlaceInput()`：左键按下开始拖拽，释放调用 `EndDragAndCommit()`
- `UpdateProjection()`：将鼠标向量投影到 8 个归一化方向并计算格数
- `EndDragAndCommit()`：构造 waypoints 后调用 `RoadGraph.AddRoad(..., RoadType.Street)`
- `HandleRemoveInput()`：优先按 snap 位置、再按原始鼠标位置查找最近 `GraphEdge`，调用 `RemoveEdge(edge.ID)`
- `CancelPlaceDrag()` 和拆路 hover 状态由 `ToolManager` 在工具切换时维护

目前 UI 固定创建 `RoadType.Street`；添加道路分级 UI 时需要把用户选择传到 `AddRoad`，不能只修改枚举或存档。

### RoadRenderer（事件驱动）

- 订阅 `EdgeAdded`：创建对应 `Line2D`
- 订阅 `EdgeRemoved`：释放对应 `Line2D`
- 订阅 `GraphCleared`：加载后清空并按 `GetAllEdges()` 全量重建
- `_junctionLayer` 绘制节点圆点：`EdgeCount >= 2` 为路口，`EdgeCount == 1` 为端点
- `_Draw()` 只处理施工预览和拆路 hover 高亮

当前 `RoadType` 已进入数据和存档，但 `RoadRenderer` 仍统一使用 `RoadConfig.RoadColor/RoadWidth`。不要声称不同道路类型已经具有不同视觉样式。

### ToolManager 与 UI

- `ToolType` 当前为 `Select`、`Road`、`RoadRemove`
- `R` / `E` / `Esc` 切换工具
- `ToolManager._Input()` 把输入转发给 `RoadBuilder.HandlePlaceInput()` 或 `HandleRemoveInput()`
- `GameHUD` 从 `RoadSystem.Instance.Graph` 读取 Group / Edge / Node 计数
- `UIManager` 管理面板注册、可见性和模态栈

## RoadGraph 关键流程

### 添加道路

`RoadBuilder.EndDragAndCommit()` → `RoadGraph.AddRoad(start, end, waypoints, type)`：

1. 拒绝起终点相同的路径
2. 组装完整折线路径
3. **在任何拆分前调用 `IsPathFullyCovered`**；完整重复路径直接返回 `-1`，不得产生副作用
4. `ResolveIntersections`：查找候选边、创建交点并拆分已有边
5. `SplitEdgesAtPathAnchors`：处理新路径点落在已有边内部或 waypoint 的情况
6. `InsertExistingNodeAnchors`：把路径经过的既有节点插入路径
7. 再次执行完整覆盖检查，处理路径重建后的最终状态
8. 创建 `RoadGroup`，逐段跳过已覆盖区间并调用 `AddEdge`
9. 没有实际新增边时清理空 group 并返回 `-1`
10. 对触及节点执行 `TryMergeAtNode`，将共线 2-edge 节点降级为 waypoint
11. merge 后再次清理可能变空的 group，再返回 group ID

关键不变量：覆盖检查的前置位置不可后移。`ResolveIntersections` 和 `SplitEdgesAtPathAnchors` 都会修改现有路网。

### 拆分边

`SplitEdgeAtPosition` 必须同时支持：

- 交点位于子线段内部
- 交点恰好等于一个内部 waypoint（相邻子线段的共享端点）

拆分流程需要保留原边的 `GroupID` 和 `RoadType`，移除旧边后创建拆分节点及左右新边。不要在 `RemoveEdge` 后再读取已失效的拓扑状态。

### 删除边和道路组

`RemoveEdge(edgeID)`：

1. 从 `_edges` 和空间索引移除边
2. 从两端 `GraphNode` 移除邻接关系
3. 清理孤立节点及其空间引用
4. 从 `RoadGroup` 移除 edge；空 group 自动删除
5. 触发 `EdgeRemoved`
6. 对仍存在的两端节点尝试共线合并

`RemoveRoadGroup(groupID)` 会先收集所有端点，批量抑制逐边 merge，删除完成后再对存活节点执行统一 merge repair。

### 合并节点

`TryMergeAtNode` 只合并 `EdgeCount == 2` 且两边方向共线的节点。合并期间需注意：

- 两条边必须连接不同的远端节点
- 合并路径必须保留正确顺序和中间 points
- 必须选择并保持一致的 group/type
- 删除旧边可能暂时让远端节点孤立；创建合并边前要确保节点仍在字典与空间索引中
- 使用 `suppressMerge` 防止递归或批处理中的级联合并

## 存档与加载

- `SaveManager` 是 Autoload 单例；编辑器保存到 `res://saves/<slot>/`，导出版本保存到可执行文件旁的 `saves/<slot>/`
- 每个 `ISaveable` 写独立 JSON；manifest 记录该槽包含的文件
- 文件先写 `.tmp` 再移动为正式文件，降低中断损坏风险
- 当前注册对象：`RoadGraph` 和 `MainCamera`
- `RoadGraph.CaptureState()` 写入 version 2、NextID、nodes、edges、groups 和 RoadType
- `RestoreState()` 清空图后恢复实体，再调用 `RebuildNodeEdges()`、`RebuildSpatialIndex()`、`EnsureNextIDBeyondLoadedEntities()`，最后触发 `GraphCleared`
- `SegmentData.Type` / `RoadData.Type` 为 nullable；旧存档没有 type 时回退到 `RoadType.Street`

存档 DTO 仍沿用 `junctions`、`segments`、`roads` JSON 字段以兼容已有格式；不要仅因运行时类型改名就破坏字段兼容性。

## Godot C# 编码约定

| 模式 | 声明 | 用途 | 示例 |
|---|---|---|---|
| Godot 节点 | `public partial class Xxx : Node2D` | 场景树生命周期；按职责也可继承其他 Godot 节点 | `RoadSystem`, `ToolManager`, `RoadRenderer` |
| 纯数据类 | `public class Xxx` | 数据、拓扑、DTO | `RoadGraph`, `GraphEdge`, `GraphNode`, `RoadGroup` |
| Resource | `[GlobalClass] public partial class Xxx : Resource` | `.tres` 共享配置 | `RoadConfig` |
| 静态工具 | `public static class Xxx` | 无状态工具 | `GridSystem`, `DirectionUtil`, `UIHelpers` |

其他约定：

- Godot 节点脚本必须是 `partial`
- 私有字段使用 `_camelCase`；现有旧文件可能不完全统一，新代码遵循当前模块风格
- 导出依赖使用 `[Export]`，场景缺失关键资源时记录明确错误
- 数据层通过 C# 事件通知渲染层，不在 `_Process()` 中轮询路网变化
- 不使用 `null!` 隐藏本应在运行时检查的可选依赖；仅用于 Godot 生命周期保证注入的字段
- 不修改 `addons/imgui-godot/`，除非任务明确针对插件
- 不手工编辑 `.godot/` 生成内容

## 常见任务

### 修改道路行为

按职责定位：

- 输入与拖拽：`RoadBuilder`
- 拓扑、交叉、覆盖、拆分、合并：`RoadGraph`
- 数据实体：`GraphNode` / `GraphEdge` / `RoadGroup`
- 视觉表现：`RoadRenderer` / `RoadConfig`
- HUD 和工具：`GameHUD` / `ToolManager`

修改 `RoadGraph` 时，逐项检查字典、节点邻接、group、空间索引、事件和存档是否仍一致。

### 添加道路类型能力

1. 更新 `RoadType`（如确实需要新等级）
2. 确认 `RoadGroup.Type` 和 `GraphEdge.Type` 的传播规则
3. 在 `RoadBuilder` 或 UI 中提供选择并传给 `AddRoad`
4. 若需要视觉差异，扩展 `RoadConfig` 和 `RoadRenderer`
5. 验证保存/加载后类型保持，旧存档仍回退到 Street

### 添加可存档系统

1. 创建纯数据 DTO
2. 实现 `ISaveable.SaveFileName`、`CaptureState()`、`RestoreState(string json)`
3. 在系统初始化时调用 `SaveManager.Instance.Register(this)`
4. 使用 `SaveJson`，保持旧 JSON 字段兼容或提供明确迁移
5. 验证保存、加载、缺失文件和旧版本数据

### 添加新工具或 UI 面板

- 新工具：更新 `ToolType`、`ToolManager` 切换和输入转发、`GameHUD` 按钮，并处理离开工具时的状态清理
- 新面板：通过 `UIManager.Register` 注册，使用 `Show/Hide/Toggle`；模态面板使用 `PushModal/PopModal`
- 使用 `UIHelpers` 保持现有控件风格

### 修复 bug

1. 先复现或用代码证据锁定症状与根因
2. 做最小修复，不顺带重构无关代码
3. 运行 `dotnet build SimpleCities.sln` 和适用的手工场景
4. 将已验证修复记录到 `docs/bugfix/`，包含症状、根因、修复、影响范围和真实验证结果
5. 未执行的测试不得写成已通过

## 文档索引

- `docs/README.md` — 文档分类与完整导航
- `docs/reference/class-reference.md` — 类与 API 参考（使用前仍需和源码核对）
- `docs/manuals/grid-system.md` — 网格设计
- `docs/manuals/road-system-v2-gen.md` — RoadGraph 重构设计
- `docs/manuals/infrastructure-guide.md` — 基础设施开发指南
- `docs/design/overview.md` — 设计总览
- `docs/roadmaps/implementation-roadmap.md` — 实现进度
- `docs/reference/save-system-plan.md` — 存档系统当前参考与演进计划
- `docs/reference/ui-architecture.md` — UI 架构
- `docs/reference/game-logic.md` — 系统逻辑图
- `docs/design/simulation-systems.md` — 远期模拟系统设计
- `docs/design/math-model.md` — 模拟数学模型
- `docs/design/game-style-discussion.md` — 游戏风格讨论
- `docs/opencode-tooling/skills.md` — 项目 Skill 与维护规则
- `docs/opencode-tooling/opencode-mcp-lsp.md` — OpenCode MCP 与 LSP 排障
- `docs/bugfix/README.md` — 按系统拆分的修复记录索引

源码是最终事实来源。设计文档可能描述未来目标；实现任务前必须先核对当前类、方法和调用关系。
