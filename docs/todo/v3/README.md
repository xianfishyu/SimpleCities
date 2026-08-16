# 第三代道路系统路线图索引

> 适用范围：仅包含第三代道路系统（V3）的系统路线图导航、全局阶段依赖和最终集成归属。
>
> 整理日期：2026-08-13
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

## 当前已落地摘要（2026-08-16）

- `v3-ui:1.1` 已完成：RoadType 选择器、分类联动、键盘/手柄焦点、暂停返回、场景重入和三档视口。
- RoadUpgrade 已具备道路分类入口、`ToolManager` 同步、U 快捷键与运行时手柄确认（`v3-ui:1.2` / `v3-tool-input:2.2` 部分实现）。
- DebugPanel 已接入 V3 诊断并实现隐藏零轮询（`v3-ui:1.3` 部分实现）。
- 切换 RoadType 会取消未提交铺路并同步改造目标（`v3-tool-input:2.1` / `2.2` 部分实现）。

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

各 Phase 是实现分支中的可编译检查点，不是玩家可选的运行模式。V3 可以完全重写现有架构，但产品装配始终只有一套新 runtime/API/event/format；不得用 feature gate、兼容适配器、双事件或双 writer 保留 V2 生产路径。V2 存档根只作为未触碰的历史数据保留。

## 最终集成归属

V3 跨系统计划只有一个最终集成负责人：[`v3-road-graph:8.6`](./road-graph.md#v3-road-graph8.6)。其余工作项负责各自系统的可独立验证产出，但不得单独宣称第三代道路系统完成。
