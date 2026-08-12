# 系统待办索引

> 复核日期：2026-08-13
> 来源：`.omo/backups/system-doc-split/docs/todo/todolist.md` 和 `.omo/evidence/split-system-docs/task-3/ownership-map.json`。
> 范围：仅包含导航、遗留 source key 映射、组合说明、集成负责人和总体依赖说明。本 README 不包含独立要求。

## 导航

- `road-graph`: [docs/todo/road-graph.md](./road-graph.md)
- `save-system`: [docs/todo/save-system.md](./save-system.md)
- `grid-rendering`: [docs/todo/grid-rendering.md](./grid-rendering.md)
- `tool-input`: [docs/todo/tool-input.md](./tool-input.md)
- `traffic-simulation`: [docs/todo/traffic-simulation.md](./traffic-simulation.md)

## 组合说明与集成负责人

- 第二代最终范围和未完成事项保存在 `docs/manuals/road-system-v2-gen.md` 附录 D；最终集成负责人是 `road-graph:7.1`，完成后保留附录作为历史验收记录。
- `todo:item:5.3` 的第二代活动范围只保留 `save-system:5.3` 的新 Node/Edge/Group schema；旧存档不兼容。`grid-rendering:5.3` 是非阻塞资源命名清理，不计入第二代完成条件。

## 遗留来源 Key 映射

> 下表的“遗留文本或 ID”仅用于追溯拆分前的原文，不代表当前要求；与活动系统待办或附录 D 冲突时，以后两者为准。

| 遗留 source key | 类型 | 遗留文本或 ID | 目标引用 | 备注 |
|---|---|---|---|---|
| `todo:summary:1` | `summary_table_row` | \| 1 \| `AddRoad` 返回 `-1` 时泄漏副作用 \| 已修复 \| 补回归测试，不再修改流程 \| | [road-graph:1](./road-graph.md#road-graph1) |  |
| `todo:summary:2` | `summary_table_row` | \| 2 \| 存档丢失 `RoadType` \| 已修复；v2 命名迁移未完成 \| 补兼容性测试；字段改名延期 \| | [save-system:2](./save-system.md#save-system2) |  |
| `todo:summary:3` | `summary_table_row` | \| 3 \| 几何查询仍有全表扫描 \| 成立 \| 优化候选边查询并建立性能基线 \| | [road-graph:3](./road-graph.md#road-graph3) |  |
| `todo:summary:4` | `summary_table_row` | \| 4 \| 数据层强制 8 方向 \| 成立 \| 将方向约束移回 `RoadBuilder` \| | [road-graph:4](./road-graph.md#road-graph4) |  |
| `todo:summary:5` | `summary_table_row` | \| 5 \| `RemoveEdge` 自动清理节点导致合并补回节点 \| 部分成立，属于架构债务 \| 在行为测试保护下重构删除事务 \| | [road-graph:5](./road-graph.md#road-graph5) |  |
| `todo:summary:6` | `summary_table_row` | \| 6 \| `RoadGroup` 在合并时丢失用户操作语义 \| 成立 \| 禁止跨 Group/Type 自动合并 \| | [road-graph:6](./road-graph.md#road-graph6) |  |
| `todo:summary:7` | `summary_table_row` | \| 7 \| `FindClosestEdge` 只命中离散采样点 \| 成立 \| 改为候选筛选 + 点到折线精确距离 \| | [road-graph:7](./road-graph.md#road-graph7) |  |
| `todo:summary:8` | `summary_table_row` | \| 8 \| `RoadBuilder` 仍有半格特殊分支 \| 事实成立，但属于 UI 约束 \| 当前不改；连续输入需求出现时再设计 \| | [tool-input:8](./tool-input.md#tool-input8) |  |
| `todo:summary:9` | `summary_table_row` | \| 9 \| `GetNeighborIDs().Distinct()` 隐藏平行边 \| 当前为设计选择 \| 保留；明确邻居查询与边查询语义 \| | [road-graph:9](./road-graph.md#road-graph9) |  |
| `todo:summary:10` | `summary_table_row` | \| 10 \| 交点判断使用严格浮点相等 \| 成立，低风险 \| 统一改用 epsilon 判断 \| | [road-graph:10](./road-graph.md#road-graph10) |  |
| `todo:summary:11` | `summary_table_row` | \| 11 \| 命名过时且 `RoadType` 视觉样式未落地 \| 事实成立；RoadType 产品功能暂不需要 \| 命名迁移独立处理；分级样式和类型选择延期 \| | [save-system:11](./save-system.md#save-system11) |  |
| `todo:summary:P1` | `summary_table_row` | \| P1 \| 图是节点、边和分组的唯一事实来源 \| 主体已完成；一致性未自动验证 \| 保留为基线；由 0.1、0.5、4.1、4.2 验证和收紧 \| | [road-graph:P1](./road-graph.md#road-graphp1) |  |
| `todo:summary:P2` | `summary_table_row` | \| P2 \| 连续空间、离散输入留在 UI 层 \| 部分完成 \| 由 0.7、2.1～2.3 清除数据层方向约束并固定节点容差 \| | [tool-input:P2](./tool-input.md#tool-inputp2) |  |
| `todo:summary:P3` | `summary_table_row` | \| P3 \| SpatialIndex 是可重建查询服务 \| 部分完成；尚未表达线段占据范围 \| 由 1.2、3.1～3.3 完成真实边查询和局部候选 \| | [road-graph:P3](./road-graph.md#road-graphp3) |  |
| `todo:summary:P4` | `summary_table_row` | \| P4 \| 删除操作不触发拓扑修复链 \| 未完成 \| 由 0.6、4.1、4.3、4.4 移除删除后的自动合并 \| | [road-graph:P4](./road-graph.md#road-graphp4) |  |
| `todo:summary:P5` | `summary_table_row` | \| P5 \| 最小化并可验证图不变式 \| 部分完成；缺少自动化入口 \| 由阶段 0 与阶段 4 建立校验、事务和封装边界 \| | [road-graph:P5](./road-graph.md#road-graphp5) |  |
| `todo:summary:API` | `summary_table_row` | \| API \| 文档定义的公共 `AddEdge` 契约 \| 未实现 \| 2.4 先验证契约，再实现或明确以文档修订取代 \| | [road-graph:API](./road-graph.md#road-graphapi) |  |
| `todo:summary:RoadType` | `summary_table_row` | \| RoadType \| 第二代运行时与存档已移除；视觉、选择与升级当前不需要 \| 第三代产品功能延期 \| D5.1～D5.3 与 P6.3/P6.5 等在产品需求确认后启用 \| | [save-system:RoadType](./save-system.md#save-systemroadtype) |  |
| `todo:summary:SaveSystem` | `summary_table_row` | \| SaveSystem \| 当前保存路径、槽名输入和整槽加载事务边界不满足生产存档安全 \| 未完成 \| 0.9～0.11 在 0.1、0.5、0.8 基础上补测试、路径迁移、槽名校验和整槽预检 \| | [save-system:SaveSystem](./save-system.md#save-systemsavesystem) |  |
| `todo:summary:Phase 6` | `summary_table_row` | \| Phase 6 \| `TrafficGraph`、A*、拥堵和增量同步 \| 未实现，按路线图延期 \| RoadGraph 前置契约已完成；按模拟需求和 RoadType 依赖分别启用 P6.1～P6.5 \| | [traffic-simulation:Phase 6](./traffic-simulation.md#traffic-simulationphase-6) |  |
| `todo:design-matrix:8e20b5c5228b` | `design_matrix_row` | \| §2 P1、§3 纯图架构、§4 数据结构 \| `RoadGraph`、`GraphNode`、`GraphEdge`、`RoadGroup` 已落地；仍需封装可变状态并验证跨容器一致性 \| 0.1、0.5、4.1、4.2；... | [road-graph:8e20b5c5228b](./road-graph.md#road-graph8e20b5c5228b) |  |
| `todo:design-matrix:3c3216c8f123` | `design_matrix_row` | \| §2 P2 连续空间 \| `CellSize` 已退出数据层 API，但 `RoadGraph` 仍依赖 `DirectionUtil`，节点身份容差未形成公开契约 \| 0.7、2.1～2.3 \| | [tool-input:3c3216c8f123](./tool-input.md#tool-input3c3216c8f123) |  |
| `todo:design-matrix:adbff35eb926` | `design_matrix_row` | \| §2 P3、§5 SpatialIndex \| `UniformGrid` 可从图重建，但边只按端点/waypoint 索引，查询仍有全表扫描 \| 1.2、3.1～3.3 \| | [road-graph:adbff35eb926](./road-graph.md#road-graphadbff35eb926) |  |
| `todo:design-matrix:5a1f82051412` | `design_matrix_row` | \| §2 P4、§6.2 删除算法 \| 旧位置字典和连通分量拆分已删除，但单边和整组删除仍触发 `TryMergeAtNode` \| 0.6、4.1、4.3、4.4 \| | [road-graph:5a1f82051412](./road-graph.md#road-graph5a1f82051412) |  |
| `todo:design-matrix:07fc815075a3` | `design_matrix_row` | \| §2 P5 不变式最小化 \| 旧位置字典不变式已消失；节点邻接、Group、空间引用、事件和存档仍需事务性同步 \| 0.4～0.7、4.1～4.4 \| | [road-graph:07fc815075a3](./road-graph.md#road-graph07fc815075a3) |  |
| `todo:design-matrix:6c685ce73a7e` | `design_matrix_row` | \| §6.1 AddRoad 与交叉/覆盖算法 \| 主流程已落地；完整覆盖检查已前置，交叉与 waypoint 拆分已有修复 \| 0.2、0.6、2.1～2.4、3.3；已解决基线 \| | [road-graph:6c685ce73a7e](./road-graph.md#road-graph6c685ce73a7e) |  |
| `todo:design-matrix:541e1cb3f3d8` | `design_matrix_row` | \| §6.3、§7 查询和公共 API \| 最近节点 API 已有；最近边语义不完整；文档中的公共 `AddEdge` 缺失 \| 1.2、2.4、3.2、4.2 \| | [road-graph:541e1cb3f3d8](./road-graph.md#road-graph541e1cb3f3d8) |  |
| `todo:design-matrix:7a82ab6271cd` | `design_matrix_row` | \| §8 渲染与道路分级 \| 事件驱动渲染和节点绘制已完成；道路分级视觉和类型选择按当前需求延期 \| 0.3；已解决基线；延期 D5.1～D5.3 \| | [grid-rendering:7a82ab6271cd](./grid-rendering.md#grid-rendering7a82ab6271cd) | grid-rendering 中的视觉配置已延期 |
| `todo:design-matrix:111e2827dafb` | `design_matrix_row` | \| §10 迁移与存档兼容 \| 编辑器与导出版本的存档根目录和槽名边界已实现；仍缺少版本拒绝、损坏数据保护、真实导出包验证和整槽事务... | [save-system:111e2827dafb](./save-system.md#save-system111e2827dafb) |  |
| `todo:design-matrix:95732cb62c0e` | `design_matrix_row` | \| §9、§10 阶段 C、§11 Phase 6 \| `TrafficGraph`、A*、拥堵、增量同步和道路升级工具均未实现 \| 延期 P6.1～P6.5 \| | [traffic-simulation:95732cb62c0e](./traffic-simulation.md#traffic-simulation95732cb62c0e) |  |
| `todo:item:0.1` | `todo_checkbox_heading` | 0.1 建立 RoadGraph 自动化测试入口 | [road-graph:0.1](./road-graph.md#road-graph0.1) |  |
| `todo:item:0.2` | `todo_checkbox_heading` | 0.2 固化已修复的 `AddRoad` 无副作用行为（原问题 1） | [road-graph:0.2](./road-graph.md#road-graph0.2) |  |
| `todo:item:0.3` | `todo_checkbox_heading` | 0.3 固化 `RoadType` 存档往返行为（原问题 2） | [save-system:0.3](./save-system.md#save-system0.3) |  |
| `todo:item:0.4` | `todo_checkbox_heading` | 0.4 固化路网与清单的存档版本策略 | [save-system:0.4](./save-system.md#save-system0.4) |  |
| `todo:item:0.5` | `todo_checkbox_heading` | 0.5 为 `RoadGraph` 恢复增加引用校验与失败保护 | [save-system:0.5](./save-system.md#save-system0.5) |  |
| `todo:item:0.6` | `todo_checkbox_heading` | 0.6 固化交叉、waypoint 拆分和删除不自动合并的目标行为 | [road-graph:0.6](./road-graph.md#road-graph0.6) |  |
| `todo:item:0.7` | `todo_checkbox_heading` | 0.7 明确并固化节点身份吸附半径 | [road-graph:0.7](./road-graph.md#road-graph0.7) |  |
| `todo:item:0.8` | `todo_checkbox_heading` | 0.8 为 `SaveManager` 增加注销机制并绑定场景生命周期 | [save-system:0.8](./save-system.md#save-system0.8) |  |
| `todo:item:0.9` | `todo_checkbox_heading` | 0.9 建立 `SaveManager` 自动化契约测试 | [save-system:0.9](./save-system.md#save-system0.9) |  |
| `todo:item:0.10` | `todo_checkbox_heading` | 0.10 固化编辑器与导出版本的存档根目录和槽名边界 | [save-system:0.10](./save-system.md#save-system0.10) |  |
| `todo:item:0.11` | `todo_checkbox_heading` | 0.11 建立整槽加载预检与提交边界 | [save-system:0.11](./save-system.md#save-system0.11) |  |
| `todo:item:1.1` | `todo_checkbox_heading` | 1.1 禁止跨 `RoadGroup` 或跨 `RoadType` 自动合并（原问题 6） | [road-graph:1.1](./road-graph.md#road-graph1.1) |  |
| `todo:item:1.2` | `todo_checkbox_heading` | 1.2 将 `FindClosestEdge` 改为真实折线距离查询（原问题 7） | [road-graph:1.2](./road-graph.md#road-graph1.2) |  |
| `todo:item:1.3` | `todo_checkbox_heading` | 1.3 用 epsilon 替代交点端点的严格相等（原问题 10） | [road-graph:1.3](./road-graph.md#road-graph1.3) |  |
| `todo:item:2.1` | `todo_checkbox_heading` | 2.1 为任意 R² 折线路径补数据层测试（原问题 4） | [road-graph:2.1](./road-graph.md#road-graph2.1) |  |
| `todo:item:2.2` | `todo_checkbox_heading` | 2.2 从 `RoadGraph.IsPathValid` 移除 8 方向判断 | [road-graph:2.2](./road-graph.md#road-graph2.2) |  |
| `todo:item:2.3` | `todo_checkbox_heading` | 2.3 复核依赖方向枚举的合并逻辑 | [road-graph:2.3](./road-graph.md#road-graph2.3) |  |
| `todo:item:2.4` | `todo_checkbox_heading` | 2.4 落实设计文档定义的公共 `AddEdge` 契约 | [road-graph:2.4](./road-graph.md#road-graph2.4) |  |
| `todo:item:3.1` | `todo_checkbox_heading` | 3.1 建立当前 `AddRoad` 性能基线（原问题 3） | [road-graph:3.1](./road-graph.md#road-graph3.1) |  |
| `todo:item:3.2` | `todo_checkbox_heading` | 3.2 为“线段经过的空间桶”建立候选查询能力 | [road-graph:3.2](./road-graph.md#road-graph3.2) |  |
| `todo:item:3.3` | `todo_checkbox_heading` | 3.3 优化覆盖与交点查询 | [road-graph:3.3](./road-graph.md#road-graph3.3) |  |
| `todo:item:4.1` | `todo_checkbox_heading` | 4.1 为删除过程定义并验证图不变式 | [road-graph:4.1](./road-graph.md#road-graph4.1) |  |
| `todo:item:4.2` | `todo_checkbox_heading` | 4.2 封闭 `RoadGraph` 的可变内部状态暴露 | [road-graph:4.2](./road-graph.md#road-graph4.2) |  |
| `todo:item:4.3` | `todo_checkbox_heading` | 4.3 将底层删边与孤立节点清理解耦 | [road-graph:4.3](./road-graph.md#road-graph4.3) |  |
| `todo:item:4.4` | `todo_checkbox_heading` | 4.4 统一单边删除、整组删除、拆分与合并的清理阶段 | [road-graph:4.4](./road-graph.md#road-graph4.4) |  |
| `todo:item:5.3` | `todo_checkbox_heading` | 5.3 独立处理 Junction → Node 命名迁移 | [save-system:5.3](./save-system.md#save-system5.3)<br>[grid-rendering:5.3](./grid-rendering.md#grid-rendering5.3) | 遗留 5.3 已拆分为 save-system 和 grid-rendering 条目，集成负责人为 `save-system`。 |
| `todo:item:6.1` | `todo_checkbox_heading` | 6.1 区分历史架构、当前实现与未来路线图 | [road-graph:6.1](./road-graph.md#road-graph6.1) |  |
| `todo:item:6.2` | `todo_checkbox_heading` | 6.2 同步当前合并、命中和空间索引语义 | [road-graph:6.2](./road-graph.md#road-graph6.2) |  |
| `todo:item:6.3` | `todo_checkbox_heading` | 6.3 同步活动存档 schema 与迁移策略 | [save-system:6.3](./save-system.md#save-system6.3) |  |
| `todo:deferred-section:b75f1d496647` | `deferred_heading` | RoadType 产品功能 | [grid-rendering:b75f1d496647](./grid-rendering.md#grid-renderingb75f1d496647) |  |
| `todo:deferred:D5.1` | `deferred_checkbox_heading` | D5.1 按产品需求定义 `RoadType` 分级样式 | [grid-rendering:D5.1](./grid-rendering.md#grid-renderingd5.1) |  |
| `todo:deferred:D5.2` | `deferred_checkbox_heading` | D5.2 让 `RoadRenderer` 按 `edge.Type` 渲染 | [grid-rendering:D5.2](./grid-rendering.md#grid-renderingd5.2) |  |
| `todo:deferred:D5.3` | `deferred_checkbox_heading` | D5.3 让 `RoadBuilder` 提交用户选择的 `RoadType` | [tool-input:D5.3](./tool-input.md#tool-inputd5.3) |  |
| `todo:deferred-section:35b9c59e1fd7` | `deferred_heading` | 原问题 8：RoadBuilder 半格分支 | [tool-input:35b9c59e1fd7](./tool-input.md#tool-input35b9c59e1fd7) |  |
| `todo:deferred:a371ae88d7d5` | `deferred_checkbox_heading` | 需求触发后再重新设计连续输入 | [tool-input:a371ae88d7d5](./tool-input.md#tool-inputa371ae88d7d5) |  |
| `todo:deferred-section:121c26a6947a` | `deferred_heading` | 原问题 9：`GetNeighborIDs().Distinct()` | [road-graph:121c26a6947a](./road-graph.md#road-graph121c26a6947a) |  |
| `todo:deferred:31883cfb1c78` | `deferred_checkbox_heading` | 交通模拟设计时明确平行边策略 | [road-graph:31883cfb1c78](./road-graph.md#road-graph31883cfb1c78) |  |
| `todo:deferred-section:def6b8230b9f` | `deferred_heading` | 产品阶段 6：交通模拟与道路升级 | [traffic-simulation:def6b8230b9f](./traffic-simulation.md#traffic-simulationdef6b8230b9f) |  |
| `todo:deferred:P6.1` | `deferred_checkbox_heading` | P6.1 构建 `TrafficGraph` 只读带权有向视图 | [traffic-simulation:P6.1](./traffic-simulation.md#traffic-simulationp6.1) |  |
| `todo:deferred:P6.2` | `deferred_checkbox_heading` | P6.2 实现 A* 寻路与确定的不可达行为 | [traffic-simulation:P6.2](./traffic-simulation.md#traffic-simulationp6.2) |  |
| `todo:deferred:P6.3` | `deferred_checkbox_heading` | P6.3 建立 RoadType 通行权重、容量与拥堵重算 | [traffic-simulation:P6.3](./traffic-simulation.md#traffic-simulationp6.3) |  |
| `todo:deferred:P6.4` | `deferred_checkbox_heading` | P6.4 按已提交的 RoadGraph 变更增量同步模拟图 | [traffic-simulation:P6.4](./traffic-simulation.md#traffic-simulationp6.4) |  |
| `todo:deferred:P6.5` | `deferred_checkbox_heading` | P6.5 实现既有道路升级工具 | [traffic-simulation:P6.5](./traffic-simulation.md#traffic-simulationp6.5) |  |
| `todo:baseline:cb2d49752724` | `solved_baseline_checkbox` | 原问题 1：覆盖路径检查已前置。 `Scripts/Road/RoadGraph.cs:48` | [road-graph:cb2d49752724](./road-graph.md#road-graphcb2d49752724) |  |
| `todo:baseline:878b6f92c0cc` | `solved_baseline_checkbox` | 原问题 2：Edge/Group 的 `RoadType` 已写入并兼容恢复。 `Scripts/Road/RoadGraph.cs:195`、`Scripts/Road/RoadGraph.cs:211`、`Scripts/Road... | [save-system:878b6f92c0cc](./save-system.md#save-system878b6f92c0cc) |  |
| `todo:baseline:b3bbd674df7c` | `solved_baseline_checkbox` | waypoint 交叉、锚点拆分和 waypoint 精确拆边已有修复。 `Scripts/Road/RoadGraph.cs:401`、`Scripts/Road/RoadGraph.cs:464`、`Scripts/Road/Ro... | [road-graph:b3bbd674df7c](./road-graph.md#road-graphb3bbd674df7c) |  |
| `todo:baseline:4efd03c22f37` | `solved_baseline_checkbox` | P1 主体和纯图数据模型已经落地。 权威实体位于 `_nodes`、`_edges`、`_groups`；旧位置字典已移除，空间索引可重建。 | [road-graph:4efd03c22f37](./road-graph.md#road-graph4efd03c22f37) |  |
| `todo:baseline:df59848d1fce` | `solved_baseline_checkbox` | `CellSize` 已从 RoadGraph API 移除。 网格吸附和半格输入留在 `RoadBuilder` / `GridSystem`。 | [tool-input:df59848d1fce](./tool-input.md#tool-inputdf59848d1fce) |  |
| `todo:baseline:0854f0250cc2` | `solved_baseline_checkbox` | 事件驱动 Edge 渲染与加载后全量重建已经落地。 `RoadRenderer.SetGraph` 监听 `EdgeAdded`、`EdgeRemoved`、`GraphCleared`。 | [grid-rendering:0854f0250cc2](./grid-rendering.md#grid-rendering0854f0250cc2) |  |
| `todo:completion:822fd09c14ca` | `completion_criterion` | 1. 当前清理里程碑要求阶段 0～6 中保留的活动项全部完成，包括 2.4；RoadType 产品功能 D5.1～D5.3、`P6.*` 与其他需求触发项明确排除，直到满足各自启用条件。 | [road-graph:822fd09c14ca](./road-graph.md#road-graph822fd09c14ca) |  |
| `todo:completion:57b3e1c6c3fa` | `completion_criterion` | 2. 每个行为项先有失败的自动化测试，再做最小实现并通过完整回归；不能仅凭源码检查标记完成。 | [road-graph:57b3e1c6c3fa](./road-graph.md#road-graph57b3e1c6c3fa) |  |
| `todo:completion:936efe9cdd8b` | `completion_criterion` | 3. 几何、拓扑、删除事务、存档兼容、`SaveManager` 契约、保存路径边界、整槽加载预检和公共 API 测试全部通过；当前只要求 RoadType 数据/旧存档兼容回归，不要求类型样式或选择 UI。 | [road-graph:936efe9cdd8b](./road-graph.md#road-graph936efe9cdd8b) |  |
| `todo:completion:c78164d23e9b` | `completion_criterion` | 4. `dotnet build` 退出码为 0，修改文件无新增诊断；构建成功不能替代自动化或运行时测试证据。 | [road-graph:c78164d23e9b](./road-graph.md#road-graphc78164d23e9b) |  |
| `todo:completion:af4fd4e8bade` | `completion_criterion` | 5. 在 Godot 主场景真实完成铺路、交叉、拆除、保存、加载、非法槽名拒绝和加载失败保护验证，并记录实际观察结果；RoadType 产品功能启用后再增加类型选择和样式验证。 | [save-system:af4fd4e8bade](./save-system.md#save-systemaf4fd4e8bade) |  |
| `todo:completion:bfd7554b1b07` | `completion_criterion` | 6. 10k+ Edge 性能场景有可复现的优化前后数据，且局部查询不再随全图规模线性退化。 | [road-graph:bfd7554b1b07](./road-graph.md#road-graphbfd7554b1b07) |  |
| `todo:completion:cb8d79634afd` | `completion_criterion` | 7. 文档项必须引用最终代码或测试事实；只有在对应测试和必要的 Godot 运行验证完成后，才能把目标描述为已实现。 | [road-graph:cb8d79634afd](./road-graph.md#road-graphcb8d79634afd) |  |

## 总体依赖说明

- `save-system:0.9` 依赖 `road-graph:0.1` 提供自动化测试入口。
- `road-graph:4.4` 已提供事务后稳定事件，`traffic-simulation:P6.1` 仍等待交通模拟产品范围和有向权重语义。
- `traffic-simulation:P6.3` 和 `traffic-simulation:P6.5` 依赖 `grid-rendering:D5.1`、`grid-rendering:D5.2` 与 `tool-input:D5.3` 提供 RoadType 产品行为。
- `grid-rendering:5.3` 与 `save-system:5.3` 关联；最终兼容性验收由 `save-system` 负责。
