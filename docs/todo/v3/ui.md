# 第三代 UI 系统待办清单

> 系统 key：`v3-ui`
> 整理日期：2026-08-13
> 证据：`Scripts/UI/ConstructionDock.cs`、`Scripts/UI/ToolContextPanel.cs`、`Scripts/UI/GameHUD.cs`、`Scenes/UI/`、现有 UI 自动化、`tests/godot/command_center_runtime_contract.gd` 与 `docs/manuals/road-system-v3-gen.md`。
> 主导原则：UI 只呈现和编辑工具/操作状态，不直接修改 RoadGraph 或磁盘；桌面、窄屏、键盘焦点和场景重复进入必须共享同一行为契约。

## 状态总览

| ID | 发现 | 当前状态 | 处置方式 |
|---|---|---|---|
| 1.1 | 道路上下文没有 RoadType 选择控件 | 已完成 | 四段式名称与颜色 swatch 选择器写入共享 tool state |
| 1.2 | ConstructionDock 没有道路改造工具呈现 | 部分实现 | 资源化 RoadUpgrade 工具、选中态和上下文联动 |
| 1.3 | DebugPanel 仍把 RoadGroup 数量作为路网指标 | 部分实现 | 移除 Group 指标，展示 canonical Node/Edge/geometry/self-loop 结构量 |
| 1.4 | 暂停菜单没有异步 Save/Load/Delete 的独占状态机 | 开放 | generation/token 防护、Escape 独占及三类操作的明确提交边界 |

### 当前落地摘要（2026-08-16）

- `v3-ui:1.1` 已完成：RoadType 选择器、分类联动、键盘/手柄焦点、暂停返回、场景重入和三档视口。
- RoadUpgrade 已具备道路分类入口、`ToolManager` 同步与 U 快捷键；DebugPanel 已接入 V3 诊断并实现隐藏零轮询。`1.2`～`1.4` 的 surface hit 选择、完整改造呈现与异步存档状态机仍开放。
- M4 准备：已梳理 `PauseMenu` 同步存档调用点并映射到 `V3SaveOperation` 阶段（见 2026-08-16 条目）；`1.4` 仍开放。

### 设计覆盖矩阵

| 设计范围 | 当前事实 | 关联待办 |
|---|---|---|
| 命令中心基线 | ConstructionDock、ToolContextPanel、DebugPanel 和 PauseMenu 已有响应式布局、焦点链和运行时契约 | 已解决基线 |
| V3 类型建造 | 当前道路分类只有一个 `city-road` 工具；ToolContextPanel 已加入四段式 RoadType 选择器，分类联动、键盘/手柄焦点、暂停返回、场景重入和三档视口均已验证 | 1.1、`v3-tool-input:2.1` |
| V3 既有道路改造 | ConstructionDock 已渲染“城市道路/道路改造”两个工具项并同步 ToolManager 与 V3 ToolState；剩余真实 surface hit 选择与端到端验收 | 1.2、`v3-tool-input:2.2` |
| V3 规范存储诊断 | DebugPanel 已展示 Node/Edge/Geometry/SelfLoop/Parallel 且隐藏时不轮询；query fragment 指标待空间索引接入 | 1.3、`v3-road-graph:8.2`～`8.5` |
| V3 异步存档体验 | PauseMenu 调用同步 bool API；没有 busy 目标、并发禁用、取消边界、autosave skipped 与 scene generation 失效呈现 | 1.4、`v3-save-system:2.3`、`v3-tool-input:2.4` |

### 2026-08-13：1.3 诊断快照数据源（部分）

- `RoadGraphV3Diagnostics` 随事务维护；`RoadGraphV3Application` / `RoadGraphV3System` 暴露只读快照，DebugPanel 后续可 O(1) 读取。
- 完整测试套件 1230/1230 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：DebugPanel 实际移除 Group 指标并接入快照。

### 2026-08-13：1.3 DebugPanel 接入 V3 诊断快照（部分）

- `DebugPanel` 移除 RoadGroup 行，新增 Geometry/SelfLoop 行；`GameHUD` 在 `_Ready` 解析 `RoadGraphV3System` 并注入诊断提供器，面板优先读取 O(1) `RoadGraphV3Diagnostics`，V2 仅作回退。
- `Scenes/UI/GameHUD.tscn` 同步替换 RoadGroupRow 为 GeometryRow/SelfLoopRow。
- 验证：Godot `MapTest` 冻结运行后 GDScript 读取 `node=0 edge=0 geom=0 loop=0`；完整测试套件 1230/1230 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：query fragment 指标、隐藏时零轮询断言与三档视口运行时契约回归。

### 2026-08-13：1.3 DebugPanel 增加 parallel edge 指标（更新）

- `DebugPanel` 新增 Parallel 行，读取 `RoadGraphV3Diagnostics.ParallelEdgeCount`。
- 验证：Godot `MapTest` 冻结运行后 GDScript 读取 `parallel=0`；完整测试套件 1240/1240 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。

### 2026-08-13：1.1 RoadType 选择器（已完成）

- `ToolContextPanel.ConfigureRoadTypeSelector` 从 `RoadTypeStyleCatalogResult` 生成四个 toggle 按钮，按钮使用样式的 `DisplayName` 与 `Color`；`GameHUD` 在 `_Ready` 注入默认目录、`RoadGraphV3System.ToolState.SelectedRoadType` 与 `TrySelectRoadType` 回调。
- `Scenes/UI/GameHUD.tscn` 新增 `RoadTypeRow` / `RoadTypeButtons`，初始隐藏，配置后显示；`SetRoadTypeSelectorVisible` 与 `OnDockContextDisplayChanged` 联动，切换到非道路分类时隐藏、切回道路分类时恢复。
- 无效/不完整 `RoadTypeStyleCatalogResult` 不再抛异常，而是隐藏选择器并保留旧回调，避免提交错误类型。
- 四个按钮均设置 `ToggleMode = true`、`FocusMode = All` 和左右 `FocusNeighbor`。
- `project.godot` 为 `ui_accept` 补充 joypad button 0 绑定，手柄 A 可激活聚焦的 RoadType 按钮。
- 验证：Godot `MapTest` 冻结运行后 GDScript 读取 `row_visible=true`、按钮 4 个、文本 `土路/街道/主干道/高速`、`toggle_mode` 全 true、`button_pressed` 仅 `街道` 为 true；模拟点击 `高速` 后 pressed 迁移到 `高速`；按 `ui_right` 焦点从 `土路` 移到 `街道`，聚焦 `高速` 后按 Enter 或手柄 A 均能切换 pressed；切换到 `区域` 分类后 `row_visible=false`，切回 `道路` 后恢复且选中类型保持。`command_center_runtime_contract.gd` 已覆盖 RoadType 行可见、暂停返回保持、场景重入保持和三档视口，headless 输出 `PASS command center runtime contract`。完整测试套件 1254/1254 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误，编辑器错误日志为空；DAP stdout 仅引擎/bridge/embedded-window 提示，无 stderr。

### 2026-08-13：1.2 RoadUpgrade 工具呈现（部分）

- `ToolType` 新增 `RoadUpgrade`；`RoadsConstructionCategory.tres` 新增 `road-upgrade` 工具定义与独立图标；`ConstructionDock` 渲染“城市道路/道路改造”两个工具项，内置展示、焦点链和节点命名覆盖 `RoadUpgrade`。
- `ToolTypeExtensions` 将 `Select/Road/RoadRemove/RoadUpgrade` 稳定映射到 `RoadToolType.Select/Place/Remove/Upgrade`，`ToolManager` 在工具切换和 `_Ready` 时同步 V3 `ToolState`。
- `ConstructionDock` 焦点链扩展为全部工具按钮（Road → RoadUpgrade → Context），`GetLastDockFocusControl` 返回最后工具按钮。
- `InputBindingManager` 新增 `tool_road_upgrade`（默认 U），`GameHUD` 可通过 U 切换到 `RoadUpgrade`；运行时验证 `CurrentTool` 0 → 3。
- 验证：Godot `MapTest` 冻结运行后 tool list 有 `RoadToolButton` / `RoadUpgradeToolButton`；点击“道路改造”后按钮 `pressed=true` 且 `ToolContextPanel` 显示“道路改造”；`command_center_runtime_contract.gd` headless 输出 `PASS command center runtime contract`；完整测试套件 1254/1254 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 surface hit 选择、矩形批量改造、self-loop/parallel Edge 选择、失效 token 与端到端工具验收。

### 2026-08-13：1.3 DebugPanel 隐藏时零轮询（部分）

- `DebugPanel.UpdateMetrics` 在 `DebugContent` 隐藏时直接返回；V3 诊断仅在 `ChangeSequence` 变化时刷新文本，避免隐藏时逐帧轮询诊断或全图。
- `DebugPanel` 新增 `QueryRow`，显示 `RoadGraphV3Diagnostics.QueryFragmentCount`（当前按 line primitive 计算）。
- 验证：Godot `MapTest` 冻结运行后显示面板 `node=0 edge=0`；隐藏后继续步进标签保持 `0`；`command_center_runtime_contract.gd` 已更新为 V3 指标并 headless 输出 `PASS command center runtime contract`（含 1600x900、640x480、435x480 视口断言）；完整测试套件 1250/1250 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：曲线 primitive 的精细切分与空间索引完整接入。

### 2026-08-16：M4 准备——PauseMenu 同步存档调用点调查（文档）

- 调用点：`CreateNamedSave` -> `SaveManager.SaveAs`；`OverwriteConfirmedSave` -> `SaveManager.Save`；`LoadConfirmedSave` -> `SaveManager.Load`；`DeleteConfirmedSave` -> `SaveManager.DeleteSlot`；`RefreshSaveSlots` -> `SaveManager.ListSlots`。
- 现状：全部为同步 bool，无 busy/取消/generation/token；列表来自 V2 根，不能区分 `CompleteV3`/`CorruptV3`/`Foreign`/`Unsafe`。
- 映射：Save As/Overwrite -> `V3SaveOperationKind.Publish`；Load -> `Load`；Delete -> `Delete`；`Admission/Prepare/Preflight` 可取消，`Commit/Completed` 不可取消。
- 下一步：在 `1.4` 中替换为 operation token/result 状态机，并接入 V3 occupant 分类列表。

### 2026-08-16：M4 基础——V3SaveOperationUiState 映射助手（部分）

- 新增 `Scripts/UI/V3SaveOperationUiState.cs`：`V3SaveOperationUiPhase`（Idle/Busy/Cancelling/Completed/Failed）与 `V3SaveOperationUiState` record，将 `V3SaveOperationResult` 映射为 UI 可消费的 `IsBusy` / `IsCancellable` / `IsComplete` / `IsFailed` / `WarningSummary`。
- 映射规则：`Success && CommitCompleted` -> Completed；`!Success && !CommitCompleted` -> Failed；`Admission/Prepare/Preflight` 阶段视为可取消。
- 新增 `tests/SimpleCities.RoadGraph.Tests/V3SaveOperationUiStateTests.cs`：覆盖 null、成功、提交前失败、observer warning 与 Cancelling 状态。
- 尚未接入 `PauseMenu`；`1.4` 仍开放，完整异步状态机待实现。

### 2026-08-16：M4 基础——V3SaveOperationController 状态机（部分）

- 新增 `Scripts/UI/V3SaveOperationController.cs`：跟踪当前 `V3SaveOperationToken`，`TryBegin` 在 busy/cancelling 时拒绝重复提交，`Complete` 只接受匹配 token 的结果，`RequestCancel` 仅在 `Admission/Prepare/Preflight` 阶段生效并进入 `Cancelling`，`Reset` 回到 Idle。
- `V3SaveOperationUiState.Cancelling` 调整为 `IsBusy = true`，使取消请求后仍阻止新操作。
- 新增 `tests/SimpleCities.RoadGraph.Tests/V3SaveOperationControllerTests.cs`：覆盖开始、重复开始、匹配/过期/未开始完成、可取消阶段取消、不可取消阶段保持、Reset。
- 尚未接入 `PauseMenu`；`1.4` 仍开放。

### 2026-08-16：M4 基础——IV3SaveOperationBackend 与 V3ApplicationSaveOperationBackend（部分）

- 新增 `Scripts/UI/V3SaveOperationBackend.cs`：`IV3SaveOperationBackend` 抽象 Save/Load/Delete/List，`V3ApplicationSaveOperationBackend` 将 `RoadGraphV3Application` 的同步 bool API 包装为 `V3SaveOperationResult`（Publish/Load/Delete token），并统一生成 `manual-{Guid:N}` 槽 ID。
- 新增 `tests/SimpleCities.RoadGraph.Tests/V3SaveOperationBackendTests.cs`：覆盖 SaveAs/Save/Load/Delete 成功与 Delete 缺失失败。
- 尚未接入 `PauseMenu`；`1.4` 仍开放。

## 执行顺序

### 阶段 7：第三代道路控件、诊断与存档交互

<a id="v3-ui1.1"></a>

- [x] **1.1 在道路上下文中提供可访问的 RoadType 选择器**
  - 当前问题：`ToolContextPanel` 只能展示当前工具说明；玩家无法选择 `Dirt`、`Street`、`Arterial` 或 `Highway`，也无法确认预览将使用哪个类型。
  - 修改：在道路建造和改造上下文中加入四段式单选控件，每项显示来自 `RoadTypeStyle` 的名称与颜色 swatch；控件只更新 `SelectedRoadType`，不调用 RoadGraph。默认选择 `Street`，运行期间切换工具保留选择，重新进入城市场景恢复默认。
  - 完成证据（2026-08-13）：`ToolContextPanel`/`GameHUD` 已接入四按钮选择器；`project.godot` 为 `ui_accept` 补充 joypad button 0 绑定；运行时键盘 Enter 与手柄 A 均能切换选中类型；`command_center_runtime_contract.gd` 覆盖 RoadType 行可见、非道路分类隐藏、暂停返回保持、场景重入保持和三档视口，headless 输出 `PASS command center runtime contract`；完整测试套件 1254/1254 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
  - 依赖：`v3-road-graph:8.4`、`v3-grid-rendering:2.1`、`v3-tool-input:2.1`。
  - 集成负责人：`v3-ui`；端到端完成判定由 `v3-road-graph:8.6` 负责。
  - 验证：四项唯一选择、鼠标、键盘/手柄焦点、无效样式降级、道路/改造上下文共享状态、切换分类、暂停返回、场景重复进入，以及 1600x900、640x480、435x480 布局。
  - 验收：选中类型始终可见且与 tool state 一致；窄屏不与 ConstructionDock、DebugPanel 或 PauseMenu 重叠；控件失效时不会提交错误类型。

<a id="v3-ui1.2"></a>

- [ ] **1.2 在道路分类中呈现并同步 RoadUpgrade 工具**
  - 当前问题：`ConstructionDock.RenderRoadsMenu` 只接受 `ToolType.Road`，内置展示只覆盖 Select 和 RoadRemove，无法资源化呈现改造工具或同步选中态。
  - 修改：扩展道路 catalog 和 dock 渲染规则，使“城市道路”和“道路改造”作为两个稳定工具项显示；改造工具使用独立图标、tooltip、焦点节点和 `ToolType.RoadUpgrade`，与 GameHUD / ToolManager 实际状态双向同步。切出工具时 UI 只触发输入层取消，不自行提交改造。
  - 进度（2026-08-13）：`ConstructionDock` 已显示两个道路工具，点击“道路改造”会同步 `ToolManager` 与 V3 `ToolState`；剩余 surface hit 与端到端验收见状态总览进度记录。
  - 依赖：`v3-tool-input:2.2`、`v3-ui:1.1`。
  - 集成负责人：`v3-ui`；端到端完成判定由 `v3-road-graph:8.6` 负责。
  - 验证：catalog 唯一 ID、排序、图标资源、选中态、分类展开/折叠、焦点循环、快捷动作显示、工具切换取消预览、场景生命周期和三档视口运行时契约。
  - 验收：玩家可从道路分类进入建造或改造并明确看到目标类型；工具显示、ToolManager 状态和输入会话始终一致。

<a id="v3-ui1.3"></a>

- [ ] **1.3 将 DebugPanel 改用 canonical RoadGraph 指标**
  - 当前问题：`DebugPanel` 和 `command_center_runtime_contract.gd` 通过 `GetAllGroups()` 显示/断言 RoadGroup 数；V3 移除 Group 后该指标既无法编译，也不能说明连续存储是否生效。若直接在 `_Process` 中调用 `GetAllNodes/Edges` 统计新指标，会每帧复制全图并让 geometry-dense 长 Edge 产生额外扫描/分配。
  - 修改：删除 RoadGroup 行和相关场景节点/引用，改为显示 Node、canonical Edge、原生 geometry segment、query fragment 和 self-loop 数；标签明确区分拓扑量、权威几何量与派生索引量，不把 parallel Edge 按邻居去重。只读取 `v3-road-graph:8.5` 随事务维护的不可变 diagnostics snapshot；面板可见且 sequence 改变时刷新文本，隐藏时不轮询/复制全图。
  - 进度（2026-08-13）：Node/Edge/Geometry/SelfLoop/Parallel 与隐藏时零轮询已落地；query fragment 指标仍待空间索引接入。
  - 依赖：`v3-road-graph:8.2`、`v3-road-graph:8.3`、`v3-road-graph:8.5`。
  - 集成负责人：`v3-ui`；端到端完成判定由 `v3-road-graph:8.6` 负责。
  - 验证：直路、非共线多段、简单环、两路口环、交叉和删除重归一化后的指标；面板隐藏/显示、普通 delta/full reset、sequence 连跳、10k/100k 和单 Edge N geometry 下每帧分配；命令中心宽/窄屏和场景重复进入契约。
  - 验收：N 段无分支道路显示 2 Node / 1 Edge / N geometry segment；简单环显示 1 Node / 1 Edge / 1 self-loop；面板无失效 Group 文案或引用，隐藏或图未变化时无逐帧全图枚举/分配。

<a id="v3-ui1.4"></a>

- [ ] **1.4 呈现并约束异步保存、加载与删除操作**
  - 当前问题：PauseMenu 以同步 bool 结果驱动槽列表和关闭行为；异步后，重复按钮、Enter、Escape 或旧 continuation 可能产生重复请求、错误覆盖/删除提示或提前关闭菜单。V3 还必须只展示独立根中的 `CompleteV3` / `CorruptV3` 槽，不能让 V2/`Foreign` 内容进入普通操作。
  - 修改：消费 coordinator 的不可变 operation state/result，显示操作类型、目标存档名及 Save 的 Capture/Prepare/Publish、Load 的 Admission/Prepare/Preflight/Commit、Delete 的 Recover/Commit/Cleanup 阶段；busy 时禁用冲突按钮并防止重复提交。每个 continuation 同时校验 `SceneGeneration + MenuOpenGeneration + OperationToken`。手动 Load 从 admission 到 commit 始终保持菜单打开和场景暂停：Admission/Prepare/Preflight 期间 Escape 只发送一次取消请求并继续消费输入；进入短 non-yield commit 后只消费 Escape，不能关闭菜单、恢复游戏或再取消。Load 的关键 graph/tool/mesh/surface 失败必须在 Preflight，成功 commit 同时发布 matching `PresentationReady`；提交后只有普通 observer warning，可显示 `SucceededWithObserverWarnings`，不存在表现重试页。覆盖与删除二次确认必须显示精确 display name、slot ID 和 occupant 状态；`CorruptV3` 只允许确认删除，`Foreign` / `Unsafe` 不显示为可操作槽。删除越过 tombstone move 后即显示逻辑删除，cleanup pending 作为 warning，不把槽重新加入列表。autosave `SkippedBusy` 只更新诊断，不弹错误。
  - 依赖：`v3-save-system:2.2`～`2.3`、`v3-tool-input:2.4`、`v3-grid-rendering:2.2`。
  - 集成负责人：`v3-ui`；端到端完成判定由 `v3-road-graph:8.6` 负责。
  - 验证：保存/另存/覆盖/加载/删除各阶段，鼠标/键盘重复激活；Admission/Prepare/Preflight 连按 Escape、commit 按 Escape；成功、observer warning、cleanup pending、提交前失败/取消；scene/menu generation 和旧 continuation；五类 occupant 的列表/按钮策略、精确确认、默认焦点、V2/Foreign 不可见；pending autosave 合并/跳过，菜单关闭重开、场景退出重入，以及三档视口。
  - 验收：每次命令只对应一个 operation token；旧 generation/continuation 无法改变当前菜单或磁盘；冲突按钮、Enter 和 Escape 不重复发起或提前恢复游戏。Publish、Load、Delete 结果不混淆，Load 不改盘；失败/取消不关闭菜单或误切 `CurrentSlotID`，observer/cleanup warning 不误报失败；成功 Load 只在所有根和 matching presentation token 一次交换后恢复游戏，V2 槽从不出现在 V3 UI，autosave busy 不产生错误噪音。

## 暂不执行

### 道路类型详情与模拟数据

- 延期原因：V3 不实现速度、容量、维护费、建筑接入和拥堵，UI 没有可信数据可展示。
- 保持现状：类型选择器只显示名称和颜色，不伪造玩法数值或优劣排序。
- 重新开启条件：相应 owning system 已实现并提供稳定只读查询契约。

## 已解决基线

- [x] **ConstructionDock 已使用资源化道路 catalog 生成工具按钮。** 分类、工具选中态、焦点和响应式布局已有 C# 与 Godot 契约。
- [x] **ToolContextPanel 已支持宽屏和 760px 以下折叠/展开。** 新控件必须复用现有布局边界并保持内容可滚动。
- [x] **GameHUD 统一路由工具、暂停和撤销重做动作。** 子控件不得绕过 ToolManager 或 RoadEditHistory 写图。
- [x] **命令中心已验证 1600x900、640x480 和 435x480。** 新 RoadType 控件和第二个道路工具必须保留这些视口门禁。
- [x] **RoadType 选择器已支持分类联动、键盘与手柄焦点。** 默认 `Street`，切换工具/分类/暂停/场景重入保持选择；非道路分类隐藏选择器。
- [x] **RoadUpgrade 已具备道路分类入口与 U 快捷键。** ConstructionDock 显示“道路改造”，ToolManager 同步 V3 `ToolState`，`InputBindingManager` 提供 U 切换。

## 完成标准

1. 1.1～1.4 通过 C# 结构契约和真实 Godot 主场景交互。
2. 四类选择和两个道路工具在桌面、窄屏、鼠标与键盘焦点下均可操作且不重叠。
3. UI 只修改 tool/operation state；类型化建造、改造、磁盘事务和图提交仍由 owning system 执行。
4. 场景退出/重入、暂停和工具切换不会保留失效引用、重复信号或未提交选择。
5. Load 的成功一次交换 graph/tool/mesh/surface/token/`CurrentSlotID`，关键失败仅发生在 Preflight；Publish、Load、Delete 的授权和提交边界分别可见，V2/Foreign 内容不可操作。
