# 第三代道路系统关键决策记录

> 整理日期：2026-08-16
> 用途：记录 V3 重构中已确定且已落地的关键架构决策，避免后续重复讨论。

## ADR-001：独立 V3 存档根与 format v1

- 决策：生产环境统一使用 `user://saves-v3`，只接受 `simple-cities-v3` / schemaVersion 1；不读取、迁移、转换或覆盖 V2 存档。
- 理由：V2 的 Node/Edge/Group 与 RoadType 移除历史不能安全承载 V3 canonical Edge、self-loop、parallel Edge 和类型。
- 状态：已落地。

## ADR-002：RoadType 只保存在 canonical Edge

- 决策：`Dirt` / `Street` / `Arterial` / `Highway` 作为 Edge 级稳定字段；构造使用显式 `RoadBuildRequest`，类型进入 merge key，完全覆盖不隐式改造。
- 理由：避免碎片化 Edge 先带类型再重构，造成不稳定语义边界。
- 状态：已落地。

## ADR-003：V3 只发布统一 `GraphChanged`

- 决策：删除逐 Edge 事件和 `GraphCleared`，所有 mutation/full reset 统一发布排序去重的 created/removed/updated、`IsFullReset` 与单调 `ChangeSequence`。
- 理由：让消费者只观察最终规范图，避免中间状态和事件顺序歧义。
- 状态：已落地。

## ADR-004：Load 使用 prepared aggregate 与 non-yield commit

- 决策：`V3RoadLoadPipeline` 在 Preflight 准备 graph/tool/presentation/renderer 计划，commit 临界区内一次交换，并支持 inside-commit 参与者与快照回滚。
- 理由：避免提交后关键表现失败窗口，保证 Load 全有或全无。
- 状态：已落地。

## 相关文档

- 架构与验收规范：`docs/manuals/road-system-v3-gen.md`
- 当前实现与验证状态：`docs/manuals/v3-current-implementation.md`
- 下一步执行计划：`docs/manuals/v3-next-steps.md`
- QA 运行手册：`docs/manuals/v3-qa-runbook.md`
- 术语表：`docs/manuals/v3-glossary.md`
