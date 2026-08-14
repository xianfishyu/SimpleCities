# 第三代道路系统路线图索引

> 适用范围：仅包含第三代道路系统（V3）的系统路线图导航、全局阶段依赖和最终集成归属。
>
> 整理日期：2026-08-15
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

当前进度（2026-08-15）：Phase 1～4 已完成；Phase 5 已完成 `v3-save-system:2.1`～`2.2`，`2.3` 已部分实现 async coordinator、结构化 token/state/result、手动优先与 pending autosave、取消/退出收敛，以及 RoadGraph + 空工具/history + renderer mesh/indexed ribbon-cap-join-patch surface + 槽目标的 non-yield aggregate Load；等待 gate 时外部取消不会再遗留占锁 lease。Phase 6 的 `v3-tool-input:2.3` 已完成：delta/history 双预算通过自动化与 Release 证据，真实 V3 Load 已验证新 lineage 清空旧 undo/redo 与 token。

Phase 7 的 `v3-grid-rendering:2.0` 已部分实现普通与 Load 共用的 closed ribbon、循环 seam join、纯 seam marker 隐藏，并完成两路口环、八字形和删除支路后的 seam 重定位；`v3-grid-rendering:2.1` 已完成四类 `RoadTypeStyle` 资源、严格唯一覆盖/查询、生产 `.tres` 往返与场景启动校验；`v3-grid-rendering:2.2` 已让普通 rebuild 与 Load preparer 从不可变样式快照向同一个 mesh 写入 per-edge width/color，建立六分量 desired/presented token，并同步发布带 canonical `RoadLocation` 的 `EdgeRibbon`、degree-1 `TerminalCap`、degree-2 `SemanticJoin` 与 degree≥3 `JunctionPatch`。四类 primitive 共用 matching token 和不可变 AABB 索引；fixed-quantization Clipper2 patch 以确定 incidence sector 覆盖 T/X/锐角、self-loop 加支路和平行 Edge，degree≥2 伪 marker 已移除。Load worker 用 `RoadSurfaceSnapshot.PreparedData` 完成 triangle/disc defensive copy、patch 细分与统一建树，Preflight 只绑定 reserved token。普通 rebuild 现在也复用该纯 preparer，只重采样 created/updated Edge；失败会保留上一代完整表现并以同一 desired token 进入可诊断 stalled，provider 拒绝 hit，显式重试成功后才一次推进 presented。拆除 hover、连续轨迹与矩形选择已消费 current surface provider；会话冻结完整 token，失配时清空选择，确认前再校验 graph facade 与 change sequence。

`v3-grid-rendering:2.2`、`v3-tool-input:2.4` 与 `v3-ui:1.4` 仍保持开放；拆除工具的 hover、连续/矩形选择和确认 admission 已完成，下一入口是类型化建造、RoadUpgrade、排队 continuation 与其余道路命令的 `hit == presented == desired == graph` admission。缩放/重建视觉矩阵、平行 Edge 完整工具矩阵及完整 Preflight/性能故障矩阵也仍缺失。完整自动化当前为 820/820，拆除/history/surface/renderer 聚焦组合为 51/51；Debug 与 `ExportRelease` build 均为 0 错误，各有 1 条既有 `NU1900`，Roslyn compiler/analyzer 与 GDScript workspace scan 为 0 diagnostics。隔离 `road_render_token_runtime_contract.gd` 与 `road_input_strategy_runtime_contract.gd` 均 PASS；Godot MCP 验证样式刷新后旧拆除确认失败且 undo 不变、current token 删除成功，以及 pending 期间旧表现完整保留后一次发布空图。测试槽、隔离目录和日志均已清理，editor 无新增错误且 DAP 两个通道为空。本轮未重跑 10k/100k；既有分级 ribbon 与 TerminalCap 性能数据只作历史证据，不替代 Phase 7 的全工具接管和混合 junction/surface 规模门禁。

各 Phase 是实现分支中的可编译检查点，不是玩家可选的运行模式。V3 可以完全重写现有架构，但产品装配始终只有一套新 runtime/API/event/format；不得用 feature gate、兼容适配器、双事件或双 writer 保留 V2 生产路径。V2 存档根只作为未触碰的历史数据保留。

## 最终集成归属

V3 跨系统计划只有一个最终集成负责人：[`v3-road-graph:8.6`](./road-graph.md#v3-road-graph8.6)。其余工作项负责各自系统的可独立验证产出，但不得单独宣称第三代道路系统完成。
