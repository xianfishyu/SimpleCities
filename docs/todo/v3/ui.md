# 第三代 UI 系统待办清单

> 系统 key：`v3-ui`
> 整理日期：2026-08-13
> 证据：`Scripts/UI/ConstructionDock.cs`、`Scripts/UI/ToolContextPanel.cs`、`Scripts/UI/GameHUD.cs`、`Scenes/UI/`、现有 UI 自动化、`tests/godot/command_center_runtime_contract.gd` 与 `docs/manuals/road-system-v3-gen.md`。
> 主导原则：UI 只呈现和编辑工具/操作状态，不直接修改 RoadGraph 或磁盘；桌面、窄屏、键盘焦点和场景重复进入必须共享同一行为契约。

## 状态总览

| ID | 发现 | 当前状态 | 处置方式 |
|---|---|---|---|
| 1.1 | 道路上下文没有 RoadType 选择控件 | 开放 | 四段式名称与颜色 swatch 选择器写入共享 tool state |
| 1.2 | ConstructionDock 没有道路改造工具呈现 | 开放 | 资源化 RoadUpgrade 工具、选中态和上下文联动 |
| 1.3 | DebugPanel 仍把 RoadGroup 数量作为路网指标 | 开放 | 移除 Group 指标，展示 canonical Node/Edge/geometry/self-loop 结构量 |
| 1.4 | 暂停菜单没有异步 Save/Load/Delete 的独占状态机 | 开放 | generation/token 防护、Escape 独占及三类操作的明确提交边界 |

### 设计覆盖矩阵

| 设计范围 | 当前事实 | 关联待办 |
|---|---|---|
| 命令中心基线 | ConstructionDock、ToolContextPanel、DebugPanel 和 PauseMenu 已有响应式布局、焦点链和运行时契约 | 已解决基线 |
| V3 类型建造 | 当前道路分类只有一个 `city-road` 工具，ToolContextPanel 只显示只读文本和 CellSize | 1.1、`v3-tool-input:2.1` |
| V3 既有道路改造 | `ToolType` 和 catalog 没有 RoadUpgrade，ConstructionDock 只渲染 Road 工具定义 | 1.2、`v3-tool-input:2.2` |
| V3 规范存储诊断 | DebugPanel 仍读取 `GetAllGroups()`，无法观察 Edge 压缩、原生几何数量或 self-loop | 1.3、`v3-road-graph:8.2`～`8.5` |
| V3 异步存档体验 | PauseMenu 调用同步 bool API；没有 busy 目标、并发禁用、取消边界、autosave skipped 与 scene generation 失效呈现 | 1.4、`v3-save-system:2.3`、`v3-tool-input:2.4` |

## 执行顺序

### 阶段 7：第三代道路控件、诊断与存档交互

<a id="v3-ui1.1"></a>

- [ ] **1.1 在道路上下文中提供可访问的 RoadType 选择器**
  - 当前问题：`ToolContextPanel` 只能展示当前工具说明；玩家无法选择 `Dirt`、`Street`、`Arterial` 或 `Highway`，也无法确认预览将使用哪个类型。
  - 修改：在道路建造和改造上下文中加入四段式单选控件，每项显示来自 `RoadTypeStyle` 的名称与颜色 swatch；控件只更新 `SelectedRoadType`，不调用 RoadGraph。默认选择 `Street`，运行期间切换工具保留选择，重新进入城市场景恢复默认。
  - 依赖：`v3-road-graph:8.4`、`v3-grid-rendering:2.1`、`v3-tool-input:2.1`。
  - 集成负责人：`v3-ui`；端到端完成判定由 `v3-road-graph:8.6` 负责。
  - 验证：四项唯一选择、鼠标、键盘/手柄焦点、无效样式降级、道路/改造上下文共享状态、切换分类、暂停返回、场景重复进入，以及 1600x900、640x480、435x480 布局。
  - 验收：选中类型始终可见且与 tool state 一致；窄屏不与 ConstructionDock、DebugPanel 或 PauseMenu 重叠；控件失效时不会提交错误类型。

<a id="v3-ui1.2"></a>

- [ ] **1.2 在道路分类中呈现并同步 RoadUpgrade 工具**
  - 当前问题：`ConstructionDock.RenderRoadsMenu` 只接受 `ToolType.Road`，内置展示只覆盖 Select 和 RoadRemove，无法资源化呈现改造工具或同步选中态。
  - 修改：扩展道路 catalog 和 dock 渲染规则，使“城市道路”和“道路改造”作为两个稳定工具项显示；改造工具使用独立图标、tooltip、焦点节点和 `ToolType.RoadUpgrade`，与 GameHUD / ToolManager 实际状态双向同步。切出工具时 UI 只触发输入层取消，不自行提交改造。
  - 依赖：`v3-tool-input:2.2`、`v3-ui:1.1`。
  - 集成负责人：`v3-ui`；端到端完成判定由 `v3-road-graph:8.6` 负责。
  - 验证：catalog 唯一 ID、排序、图标资源、选中态、分类展开/折叠、焦点循环、快捷动作显示、工具切换取消预览、场景生命周期和三档视口运行时契约。
  - 验收：玩家可从道路分类进入建造或改造并明确看到目标类型；工具显示、ToolManager 状态和输入会话始终一致。

<a id="v3-ui1.3"></a>

- [ ] **1.3 将 DebugPanel 改用 canonical RoadGraph 指标**
  - 当前问题：`DebugPanel` 和 `command_center_runtime_contract.gd` 通过 `GetAllGroups()` 显示/断言 RoadGroup 数；V3 移除 Group 后该指标既无法编译，也不能说明连续存储是否生效。若直接在 `_Process` 中调用 `GetAllNodes/Edges` 统计新指标，会每帧复制全图并让 geometry-dense 长 Edge 产生额外扫描/分配。
  - 修改：删除 RoadGroup 行和相关场景节点/引用，改为显示 Node、canonical Edge、原生 geometry segment、query fragment 和 self-loop 数；标签明确区分拓扑量、权威几何量与派生索引量，不把 parallel Edge 按邻居去重。只读取 `v3-road-graph:8.5` 随事务维护的不可变 diagnostics snapshot；面板可见且 sequence 改变时刷新文本，隐藏时不轮询/复制全图。
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

## 完成标准

1. 1.1～1.4 通过 C# 结构契约和真实 Godot 主场景交互。
2. 四类选择和两个道路工具在桌面、窄屏、鼠标与键盘焦点下均可操作且不重叠。
3. UI 只修改 tool/operation state；类型化建造、改造、磁盘事务和图提交仍由 owning system 执行。
4. 场景退出/重入、暂停和工具切换不会保留失效引用、重复信号或未提交选择。
5. Load 的成功一次交换 graph/tool/mesh/surface/token/`CurrentSlotID`，关键失败仅发生在 Preflight；Publish、Load、Delete 的授权和提交边界分别可见，V2/Foreign 内容不可操作。
