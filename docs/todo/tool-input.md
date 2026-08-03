# 工具输入系统待办清单

> 系统 key：`tool-input`
> 复核日期：2026-08-04
> 证据：`Scripts/Road/RoadBuilder.cs`、`Scripts/Road/Input/`、`tests/SimpleCities.RoadGraph.Tests/RoadInputStrategyTests.cs`、`tests/godot/road_input_strategy_runtime_contract.gd` 及 `docs/manuals/road-system-v2-gen.md` 附录 D。
> 主导原则：负责玩家输入动作、可替换铺路策略、连续铺路、拆路选择和操作历史；网格规则不得进入 RoadGraph 数据层。

## 状态总览

<a id="tool-input1.1"></a>
<a id="tool-input8"></a>
<a id="tool-inputp2"></a>

| ID | 发现 | 当前状态 | 处置方式 |
|---|---|---|---|
| 1.1 | 相机、工具和暂停输入缺少统一可重绑入口 | 已完成 | 由 `InputBindingManager` 统一管理 |
| 1.2 | `RoadBuilder` 直接依赖静态方格和八方向枚举 | 已完成 | 默认米字型规则已迁入可替换策略 |
| 1.3 | 其他网格不能在不改 RoadBuilder/RoadGraph 的情况下测试 | 已完成 | 三角形与六边形策略已通过共享契约 |
| 1.4 | 一次拖拽只能生成单方向直路 | 已完成 | 连续会话支持拐点、完整预览、回退、确认和取消 |
| 1.5 | 拆路只支持单点命中 | 未完成 | 增加连续拆除和框选删除 |
| 1.6 | 铺路与拆路没有撤销/重做 | 未完成 | 建立可逆命令历史 |
| D5.3 | RoadType 类型选择 | 第三代 | 第二代不包含道路分级数据或交互 |

### 设计覆盖矩阵

<a id="tool-input3c3216c8f123"></a>

| 设计范围 | 当前事实 | 关联待办 |
|---|---|---|
| V2 数据层与输入层分离 | `RoadBuilder` 只消费 `IRoadInputStrategy`；方格尺寸和八方向投影位于 `SquareEightRoadInputStrategy` | 1.2、`road-graph:2.1`～`road-graph:2.2` |
| V2 可替换网格 | 米字型、三角形 3 邻接和六边形 6 邻接策略使用同一 `IRoadInputStrategy` / `RoadPathDraft` 契约 | 1.2～1.3 |
| V2 成熟铺路交互 | 连续多段铺路已完成；拆除仍为单 Edge 命中，操作历史仍待实现 | 1.4～1.6 |

## 执行顺序

### 阶段 1：可替换铺路策略

<a id="tool-input1.2"></a>

- [x] **1.2 抽取可替换的铺路输入策略**
  - 当前问题：`RoadBuilder` 直接使用 `GridSystem.SnapToGrid`、`DirectionUtil.All`、`Direction` 和 `Config.CellSize`，输入生命周期与米字型规则耦合。
  - 修改：定义策略接口和稳定的路径草稿结果；RoadBuilder 只管理开始、更新、预览、提交和取消，当前米字型规则迁入默认策略。
  - 依赖：`road-graph:2.2`、`road-graph:2.4`。
  - 集成负责人：`tool-input`。
  - 测试：默认米字型策略保持当前吸附、方向和最小长度行为；替换策略时不修改 RoadBuilder 或 RoadGraph。
  - 验收：RoadBuilder 不再直接枚举八方向或计算方格步长；策略输出可通过公共路径 API 提交。
  - 完成证据（2026-08-04）：新增 `IRoadInputStrategy`、不可变 `RoadPathDraft` 和 `SquareEightRoadInputStrategy`；`RoadBuilder` 通过 `BeginPlace`、`UpdatePlace`、`CommitPlace`、`CancelPlaceDrag` 管理生命周期，并只用 `RoadGraph.SubmitPath` 提交策略结果。拆除吸附与命中半径也由当前策略提供。策略测试 8/8、完整解决方案测试 434/434、Debug 构建 0 警告/0 错误；Godot 主场景策略契约、真实鼠标事件命令中心契约和授权后的暂停菜单契约均输出 PASS。逐文件 LSP、Godot editor bridge 与 DAP console 因当前会话未提供对应通道而阻塞。

<a id="tool-input1.3"></a>

- [x] **1.3 用三角形和六边形网格验证策略可替换性**
  - 当前问题：仅有米字型实现无法证明接口能表达不同邻接关系和吸附规则。
  - 修改：实现可自动化测试的三角形网格与六边形网格策略；它们只负责输入投影和路径生成，不改变 RoadGraph。
  - 依赖：`tool-input:1.2`、`road-graph:2.4`。
  - 集成负责人：`tool-input`。
  - 测试：相同鼠标轨迹分别产生符合三种网格规则的路径；切换策略后交叉、拆分和保存继续由同一 RoadGraph 处理。
  - 验收：米字型、三角形和六边形策略通过同一契约测试，RoadGraph 源码无需条件分支。
  - 完成证据（2026-08-04）：新增 `TriangularThreeRoadInputStrategy`，以三角单元中心和交替的 3 个跨边邻居生成锯齿路径；新增 `HexSixRoadInputStrategy`，以 pointy-top 单元中心、轴向坐标取整和 6 个等长邻居生成直线路径。三种策略对同一轨迹产生不同且确定的草稿，并都通过原生 line、最小长度和 `RoadGraph.SubmitPath` 共享契约；方格与六边形输出在同一图内完成内部交叉拆边，随后提交三角形输出并完成严格存档往返。输入策略聚焦测试 16/16、完整测试 442/442、Debug 构建 0 警告/0 错误；默认主场景策略契约和命令中心真实鼠标事件契约均输出 PASS。RoadBuilder 与 RoadGraph 未增加三角形/六边形分支；逐文件 LSP、Godot editor bridge 与 DAP console 仍因当前通道不可用而阻塞。

### 阶段 2：成熟铺路和拆路工作流

<a id="tool-input1.4"></a>

- [x] **1.4 实现连续多段铺路工作流**
  - 当前问题：一次拖拽只能提交单方向直路，不能连续添加拐点或在提交前调整完整路径。
  - 修改：支持连续增加拐点、移动当前末端、显示完整路径预览、确认提交和取消；当前玩家玩法继续使用米字型策略。
  - 依赖：`tool-input:1.2`、`road-graph:2.4`。
  - 关联：`grid-rendering:1.1` 负责使用同一路径草稿绘制预览。
  - 集成负责人：`tool-input`。
  - 测试：零段取消、单段、连续多段、回退最后拐点、非法段、交叉和最终提交。
  - 验收：一次建造会话可安全生成多段道路；取消或失败不修改图，预览与提交路径一致。
  - 完成证据（2026-08-04）：新增 `RoadPlacementSession`，把已固定策略草稿与可移动末端组合成同一个不可变 `RoadPathDraft`，原生 geometry segment 直接进入最终 `RoadPath`；`RoadRenderer.PreviewPoints` 绘制完整点列。既有按住拖拽并释放的单段玩法保持可用；点击起点可进入连续会话，后续左键固定拐点，鼠标移动调整末端，Enter/双击确认，右键逐级回退并在零拐点时取消，切出工具取消整场会话。确认只调用一次 `RoadGraph.SubmitPath`；重复点等拒绝保留可编辑会话且活动图、存档均不变。新增会话测试覆盖零段、单段、多段、回退、非法活动段、交叉和失败原子性；完整测试 449/449、Debug 构建 0 警告/0 错误。Godot 主场景契约验证三段完整预览、回退重加、Enter 确认、右键取消、重复点拒绝、2/3 Group 存档计数及临时槽清理并输出 PASS；命令中心真实拖拽回归输出 PASS。`csharp-ls --diagnose` 成功加载解决方案；逐文件 LSP、Godot editor bridge 与 DAP console 因当前通道不可用而阻塞。

<a id="tool-input1.5"></a>

- [ ] **1.5 实现连续拆除和框选删除**
  - 当前问题：拆除工具只按当前鼠标命中单条 Edge，缺少批量选择和连续操作。
  - 修改：提供按拖动轨迹连续拆除及矩形框选删除；先生成稳定选择集，再以单次图事务提交。
  - 依赖：`road-graph:4.1`～`road-graph:4.4`。
  - 集成负责人：`tool-input`。
  - 测试：空选区、单 Edge、多 Edge、跨 Group、重复命中、拖动取消和部分无效目标。
  - 验收：批量删除结果确定且无重复副作用；操作完成后图不变式成立。

<a id="tool-input1.6"></a>

- [ ] **1.6 为铺路和拆路提供撤销与重做**
  - 当前问题：已提交的铺路和拆路不能恢复，批量操作出错时只能手工重建。
  - 修改：以已提交图事务为命令边界记录创建、拆分、删除和恢复所需状态；新操作清空重做栈，失败操作不进入历史。
  - 依赖：`road-graph:4.4`、`tool-input:1.4`、`tool-input:1.5`。
  - 集成负责人：`tool-input`。
  - 测试：单段/多段铺路、交叉拆边、连续拆除、框选删除、多步撤销重做和失败操作。
  - 验收：撤销恢复操作前的完整拓扑和几何，重做得到相同 ID/引用语义或文档规定的等价结果；渲染和保存状态同步。

## 暂不执行

### 第三代 RoadType 产品功能

<a id="tool-inputd5.3"></a>

- [ ] **D5.3 让 RoadBuilder 提交玩家选择的 RoadType**
  - 延期原因：道路分级数据、样式、选择和升级已明确属于第三代。
  - 保持现状：第二代输入策略和公共路径 API 不以 RoadType 为参数或分支条件。
  - 重新开启条件：第三代道路分级 schema、样式和选择交互确定。
  - 关联引用：`grid-rendering:D5.1`、`grid-rendering:D5.2`。
  - 来源 key：`todo:deferred:D5.3`。

### 已取代的连续输入延期决定

<a id="tool-input35b9c59e1fd7"></a>
<a id="tool-inputa371ae88d7d5"></a>

- [x] **原“需求触发后再重新设计连续输入”已由 1.2～1.4 取代**
  - 处置原因：第二代范围已明确要求可替换网格策略和连续多段铺路，原延期条件已经满足。
  - 来源 key：`todo:deferred:a371ae88d7d5`。

## 已解决基线

<a id="tool-input1.1-baseline"></a>

- [x] **1.1 建立可持久化的键盘绑定与工具动作分发。** 输入动作由 `InputBindingManager` 管理，既有自动化和 Godot 运行时契约已通过。
<a id="tool-inputdf59848d1fce"></a>
- [x] **CellSize 已从 RoadGraph API 与 RoadBuilder 生命周期移除。** 当前米字型吸附、步长和半格约束由 `SquareEightRoadInputStrategy` 封装；`GridSystem` 仍供其他 UI/调试组件使用。
- [x] **三种输入网格共用同一提交边界。** 米字型 8 方向、三角单元中心 3 邻接和六边形单元中心 6 邻接只产生 `RoadPathDraft`；交叉、拆分、不变式和存档继续由 RoadGraph 统一处理。
- [x] **连续铺路预览与提交共用组合草稿。** `RoadPlacementSession` 保留每个策略草稿的原生段，完整预览来自同一草稿的 `PreviewPoints`；确认、取消和拒绝路径都不产生部分图写入。

## 完成标准

1. 1.2～1.6 全部通过自动化和 Godot 主场景验证。
2. 当前米字型玩法保持可用，三角形和六边形策略通过同一契约测试，切换策略不修改 RoadGraph。
3. 连续铺路、连续拆除、框选删除、撤销和重做均不破坏图、渲染或存档状态。
4. RoadType 不计入第二代；D5.3 只在第三代重新开启。
