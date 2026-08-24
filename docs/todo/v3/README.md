# 第三代道路系统路线图索引

> 适用范围：仅包含第三代道路系统（V3）的系统路线图导航、全局阶段依赖和最终集成归属。
>
> 整理日期：2026-08-21
>
> 架构与验收规范：[`docs/manuals/road-system-v3-gen.md`](../../manuals/road-system-v3-gen.md)

本目录是 V3 路线图的唯一索引。第二代道路系统及其历史收尾继续由 [`docs/todo/README.md`](../README.md) 导航；V3 之后才启用的交通模拟仍属于 [`docs/todo/traffic-simulation.md`](../traffic-simulation.md)，不纳入本目录。

系统文档是工作项状态、依赖和验收标准的唯一事实来源，本索引不重复各系统的详细要求。目录外引用工作项时必须使用完整的 `<system-key>:<id>`，不能省略 `v3-` 前缀。

## 系统导航

| 系统 key | 路线图 | 所属范围 | V3 工作项 |
| --- | --- | --- | --- |
| `v3-road-graph` | [RoadGraph](./road-graph.md) | 数值与容量、拓扑、规范 Edge、环路、RoadType、图事务 | `8.0`～`8.6` |
| `v3-save-system` | [存档系统](./save-system.md) | V3 独立 format v1 与保存根、有界 I/O、发布/删除恢复、聚合加载 | `2.1`～`2.3` |
| `v3-grid-rendering` | [网格渲染](./grid-rendering.md) | 道路表面、分级表现、呈现事务和性能门禁 | `2.0`～`2.3` |
| `v3-tool-input` | [工具输入](./tool-input.md) | 闭合建造、类型化编辑、可逆历史和 full reset 接管 | `2.0`～`2.4` |
| `v3-ui` | [UI](./ui.md) | 道路控件、工具入口、诊断与存档操作状态 | `1.1`～`1.4` |

## Phase 0～8 全局依赖

阶段名称和边界以架构指南第 12 节为准。下表只表达跨系统先后关系；Phase 0 的测试入口与基线由后续工作项共同消费，因此不另建重复 todo ID。

| Phase | 主要工作项 | 全局前置 | 阶段关系 |
| --- | --- | --- | --- |
| Phase 0 | 五个系统工作项共用的测试基础设施与基线 | 无 | 所有后续 Phase 的共同前置 |
| Phase 1 | `v3-road-graph:8.0`～`8.1` | Phase 0 | 为规范 Edge、环路、存档预算和表现层提供领域基础 |
| Phase 2 | `v3-road-graph:8.2` | Phase 1 | 完成后才能定义无 Group 的 V3 format v1，并让长 Edge 进入后续路径 |
| Phase 3 | `v3-road-graph:8.3`、`v3-tool-input:2.0` | Phase 2；阶段内依次为领域环路和闭合输入草稿 | 固定闭合/自交提交与预览契约；真实 closed ribbon 留到 Phase 7 |
| Phase 4 | `v3-road-graph:8.4`～`8.5` | Phase 2～3 | 为 V3 format v1、可逆历史、改造工具和异步消费者提供稳定事务契约 |
| Phase 5 | `v3-save-system:2.1`～`2.3` | Phase 4；阶段内按 `2.1` → `2.2` → `2.3` | 提供独立保存根、新格式、发布/删除恢复和 full-reset Load 协议；真实表现接管留到 Phase 7 |
| Phase 6 | `v3-tool-input:2.3` | Phase 4、`v3-save-system:2.1`～`2.2` | 把领域 delta 接入有界撤销重做，为工具 full reset 接管提供前置 |
| Phase 7 | `v3-grid-rendering:2.0`～`2.3`、`v3-tool-input:2.0`～`2.4`、`v3-ui:1.1`～`1.4`、`v3-save-system:2.3` | Phase 3～6 | 完成表现、工具、UI、加载参与者和唯一 V3 应用装配；各条目的精确依赖以所属路线图为准 |
| Phase 8 | `v3-road-graph:8.6` | `v3-road-graph:8.0`～`8.5`、`v3-save-system:2.1`～`2.3`、`v3-grid-rendering:2.0`～`2.3`、`v3-tool-input:2.0`～`2.4`、`v3-ui:1.1`～`1.4` | 汇总全部跨系统证据并完成最终组合验收 |

当前进度（2026-08-24，Phase 8 重新开始）：Phase 1～6、`v3-save-system:2.3` 与 `v3-ui:1.4` 已完成；当前开放项为 `v3-grid-rendering:2.2`～`2.3`、`v3-tool-input:2.4` 与最终集成负责人 `v3-road-graph:8.6`。`b95e295` 上 BUG-21 聚焦 33/33、完整自动化 959/959、双配置 build 0 警告/0 错误，junction-dense 与 geometry-dense 10k 正式 Vulkan 门均 PASS；100k 不作为必需项。V3 综合、输入策略和 render token 三项代表性 Vulkan 组合，以及 Windows QA 导出的可写/只读 ACL profile，也均以退出码 0 输出 PASS。Roslyn production/test compiler+analyzer、GDScript、Godot MCP 冻结 `MapTest` 与 DAP 双通道也已通过；当前只剩开放项状态和附录 D 的最终一致性审计。

## 历史进度记录

历史进度快照（2026-08-20）：Phase 1～4 已完成；Phase 5 已完成 `v3-save-system:2.1`～`2.2`，`2.3` 已部分实现 async coordinator、结构化 token/state/result、手动优先与 pending autosave、取消/退出收敛，以及 RoadGraph + 空工具/history + renderer mesh/indexed ribbon-cap-join-patch surface + 槽目标的 non-yield aggregate Load；等待 gate 时外部取消不会再遗留占锁 lease。Phase 6 的 `v3-tool-input:2.3` 已完成：delta/history 双预算通过自动化与 Release 证据，真实 V3 Load 已验证新 lineage 清空旧 undo/redo 与 token。

Phase 7 的 `v3-grid-rendering:2.0` 已部分实现普通与 Load 共用的 closed ribbon、循环 seam join、纯 seam marker 隐藏，并完成两路口环、八字形和删除支路后的 seam 重定位；`v3-grid-rendering:2.1` 已完成四类 `RoadTypeStyle` 资源、严格唯一覆盖/查询、生产 `.tres` 往返与场景启动校验；`v3-grid-rendering:2.2` 已让普通 rebuild 与 Load preparer 从不可变样式快照向同一个 mesh 写入 per-edge width/color，建立六分量 desired/presented token，并同步发布带 canonical `RoadLocation` 的 `EdgeRibbon`、degree-1 `TerminalCap`、degree-2 `SemanticJoin` 与 degree≥3 `JunctionPatch`。四类 primitive 共用 matching token 和不可变 AABB 索引；fixed-quantization Clipper2 patch 以确定 incidence sector 覆盖 T/X/锐角、self-loop 加支路和平行 Edge，degree≥2 伪 marker 已移除。Load worker 用 `RoadSurfaceSnapshot.PreparedData` 完成 triangle/disc defensive copy、patch 细分与统一建树，Preflight 只绑定 reserved token。普通 rebuild 现在也复用该纯 preparer，只重采样 created/updated Edge；失败会保留上一代完整表现并以同一 desired token 进入可诊断 stalled，provider 拒绝 hit，显式重试成功后才一次推进 presented。placement、拆除与 RoadUpgrade 会话均冻结完整表现 token，失配时清空草稿/选择，确认前再校验 graph facade 与 change sequence；undo/redo 的可用性和执行入口也即时复核同一 provider/graph 代际。`v3-tool-input:2.1` 的 `SelectedRoadType` 默认 `Street`，placement 冻结合法类型并显式提交；full reset 清空 transient state 但保留选择。

`v3-tool-input:2.2` 已完成独立 RoadUpgrade 会话：连续/矩形选择消费 current presented surface，冻结目标类型与完整 token，成功批次只产生一次图事件和历史；取消、NoChanges、旧 token 与 full-reset Load 均不会留下可提交选择或 overlay。类型化 placement 也已接入表现 barrier：pending/stalled 时拒绝开始，活动会话被新 token 取代后立即清空且不能修改图/history，Load admission 则先逐值保留旧状态。undo/redo 现同样在 provider/graph 不 current 时拒绝，且不会清栈或修改图；每次回放产生的新 pending 窗口会锁住相反方向，直到 matching presentation 发布。源码复核确认当前生产输入层没有独立道路事务命令队列；`RoadRenderer` 的真实 deferred 普通重建现捕获 continuation generation，Load admission 同步 flush、full reset、换图、退树和 aggregate commit 都使旧 callable 失效。`PreparedAggregateLoad` 现在另有直接契约锁定 post-commit cleanup 异常逐参与者转为 warning 且不阻断后续 cleanup；fake participant 的 observer/cleanup 组合以及真实 `RoadGraph` observer 与 fake presentation/slot cleanup 组合也已覆盖 warning 聚合与后续 cleanup 继续执行；同一场景和槽连续两次 aggregate Load 也会各自推进完整 token，并让四类 surface 绑定第二次 presented token。普通 rebuild、Load Preflight 与未提交 renderer plan 现拥有明确的资源转交边界：创建异常、过期/失败或 plan 放弃会确定性释放隐藏 `ArrayMesh`/`MultiMesh`，成功 commit 后表现层继续持有资源；本轮另以真实 `RoadGraph` participant 验证 commit-boundary generation 失配会在任何 reference swap 前拒绝，并在 admission 释放后恢复 graph mutation。`v3-ui:1.1` 的 RoadType 上下文选择器与 `v3-ui:1.2` 的 RoadUpgrade 双工具入口均已由结构/运行时契约关闭：Roads catalog 现在按稳定顺序呈现“城市道路”和“道路改造”，后者具有独立图标、默认 T 动作、旧配置兼容迁移、互斥选中态、catalog 上下文和稳定焦点链。`v3-grid-rendering:2.2`、`v3-tool-input:2.4` 与 `v3-ui:1.4` 仍保持开放；下一入口是 renderer/tool/slot 的真实 generation 失配、关键 renderer/Load Preflight 逐点 Resource 故障、真实 renderer/tool/slot observer/cleanup 组合与完整性能矩阵。缩放/重建视觉矩阵和平行 Edge 完整工具矩阵也仍缺失。完整自动化当前为 849/849，`RoadRendererLifecycleContractTests` 为 10/10，`PreparedAggregateLoadTests` 为 11/11，`RoadInputStrategyTests` 为 21/21，RoadUpgrade UI 相关 C# 契约为 25/25；本轮隔离 `APPDATA` 的 `road_renderer_lifecycle_runtime_contract.gd` 与 `road_render_token_runtime_contract.gd` 均 PASS，后者继续覆盖同一槽连续两次 Load 的 token/surface 再接管，既有输入、catalog 与命令中心契约、三档视口和 1600x900 OpenGL 截图证据继续有效。2026-08-20 Debug 与 `ExportRelease` build 均为 0 警告、0 错误，临时目录和日志已清理且原有 Godot PID 未受影响。xUnit 宿主无法初始化 Godot native Resource，因此这些 `PreparedAggregateLoadTests` 不扩展原生 `ArrayMesh`/`MultiMesh` 生命周期证据；当前会话没有暴露 Roslyn/Godot MCP 与 DAP 调用工具，未刷新 focused diagnostics、editor bridge 或 DAP console；生命周期脚本主动移除 renderer 后的既有 disposed-object 输出也未记作干净 console 门禁。本轮没有重新声称历史 MCP 证据，既有 10k/100k 性能数据继续沿用第 17 行记录；上述证据不替代 Phase 7 的混合 junction/surface、离散改造和完整 token 故障矩阵。

RoadGraph Load 线性恢复收口（2026-08-20）：`AssertInvariants()` 的批量 coverage 校验经完整 `849/849`、Debug/`ExportRelease` 0 警告/0 错误和 Release `--enforce-budget` 复验；隔离 APPDATA 的 Godot Vulkan 10k/100k fixture 均输出 `PASS`，camera/preview/highlight P95 为 `0.549/0.541/0.572 ms` 与 `0.710/0.682/0.689 ms`，重建为 `718.363/4617.638 ms`，静态 renderer 节点为 2。该证据刷新当前 CLI 热运行结果，不关闭 `v3-grid-rendering:2.3` 的 junction-dense/geometry-dense、四类 owner、类型改造离散时延和完整 token 故障矩阵；Roslyn/Godot MCP/DAP 当前未暴露，headless editor 的 UID/6550 环境错误保留为 blocked。

RoadGraph Load 线性恢复追加复验（2026-08-21）：完整自动化为 `864/864`，Debug/`ExportRelease` build 为 0 警告/0 错误；Release `--enforce-budget` 多交叉 P95 为 `7.247 ms`（10k）与 `7.431 ms`（100k），10k 全场景低于 `16.67 ms` 硬门槛。该证据不改变 `v3-road-graph:8.6` 的开放状态，junction-dense/geometry-dense、真实参与者 generation 失配和完整 Phase 7/8 故障矩阵仍由各自路线图负责。

Aggregate 提交边界失配追加复验（2026-08-21）：`PreparedAggregateLoadTests` 聚焦 `13/13`，完整自动化为 `865/865`；graph、tool、presentation、slot 任一 fake participant 在 commit boundary 失效时均不会发生引用交换或通知。该 CLR 证据不改变 `v3-save-system:2.3`、`v3-grid-rendering:2.2`、`v3-tool-input:2.4` 或 `v3-road-graph:8.6` 的开放状态。

真实 Godot generation 失配追加验证（2026-08-21）：新增 `road_load_generation_runtime_contract.gd`，用 10k Edge worker Prepare 分别使真实 `RoadRenderer` admission 失效，以及通过真实 `ToolManager._ExitTree()` 推进 scene generation 并取消 Load。两条路径均在 commit 前结束，旧 graph payload、slot、renderer token/surface/mesh、工具、placement 和 history 保持；两个独立 APPDATA 运行均 PASS，完整自动化 `865/865`、双配置 build 0 警告/0 错误，GDScript/Roslyn、Godot editor 与 DAP 门通过。该证据把此前“renderer/tool/slot 真实 generation 失配”的下一入口收窄为 slot-target commit plan 的真实 commit-boundary 失配；真实 renderer/tool/slot observer/cleanup、逐关键 Resource、其余工具状态和完整 Phase 7/8 矩阵仍开放，最终集成负责人继续为 `v3-road-graph:8.6`。

真实 slot-target 提交边界追加验证（2026-08-21）：`SlotTargetLoadCommitPlan` 现在同时冻结 scene request 与 `_currentSlotGeneration`，`RealSlotTargetGenerationMismatchAtCommitBoundary_RejectsEveryReferenceSwap` 在 `CrossCommitBoundary` 内只推进槽目标代际，并验证 graph/tool/presentation/`CurrentSlotID` 均保持旧值。`PreparedAggregateLoadTests` 为 `14/14`，完整自动化 `866/866`、双配置 build 与 Roslyn diagnostics 为 0，隔离 APPDATA 的真实 Load 契约和编辑器内 `MapTest` smoke 通过。真实 renderer/tool/slot observer/cleanup、逐关键 Resource、其余工具状态和完整 Phase 7/8 矩阵仍开放，最终集成负责人继续为 `v3-road-graph:8.6`。

各 Phase 是实现分支中的可编译检查点，不是玩家可选的运行模式。V3 可以完全重写现有架构，但产品装配始终只有一套新 runtime/API/event/format；不得用 feature gate、兼容适配器、双事件或双 writer 保留 V2 生产路径。V2 存档根只作为未触碰的历史数据保留。

## 最终集成归属

V3 跨系统计划只有一个最终集成负责人：[`v3-road-graph:8.6`](./road-graph.md#v3-road-graph8.6)。其余工作项负责各自系统的可独立验证产出，但不得单独宣称第三代道路系统完成。
