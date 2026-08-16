# 第三代道路系统术语表

> 整理日期：2026-08-16
> 用途：统一 V3 文档与代码中的核心术语。

## A

- **canonical Edge**：连续道路的规范拓扑边，可包含多条原生几何 primitive；是 V3 存储、渲染、命中和存档的基本单位。
- **self-loop**：起点和终点为同一 Node 的 Edge，按 seam 根化为线性 geometry 数组；A/B incidence 各注册一次。
- **parallel Edge**：同一 Node 对之间允许存在多条 Edge，不按邻居去重。

## G

- **GraphStateToken**：`(LineageID, DomainRevisionID, ChangeSequence)` 三元组，用于校验异步消费者和 undo/redo 方向。
- **GraphChanged**：V3 唯一图变更事件，发布排序去重的 created/removed/updated 摘要。

## Q

- **query fragment**：空间索引中的派生片段，含 `(EdgeID, GeometryIndex, ParameterRange, ConservativeBounds)`，不持久化。

## R

- **RoadType**：`Dirt` / `Street` / `Arterial` / `Highway`，只保存在 canonical Edge 上。
- **RoadGraphV3Revision**：不可变 root，保存 Node/Edge 与 ID watermark。
- **RoadSurfaceHit**：带完整 token、owner、Node/Edge/Endpoint、`RoadLocation` 和距离的表面命中记录。

## S

- **semantic boundary**：同拓扑但 RoadType 不同的 Edge 之间的边界；改造可能消除该边界并触发合并。
- **V3 format v1**：`simple-cities-v3` / `road-network` / schemaVersion 1 的存档格式。

## I

- **inside-commit**：Load non-yield 临界区内执行的回调，用于一次性交换 renderer 等表现资源。

## 相关文档

- 架构与验收规范：`docs/manuals/road-system-v3-gen.md`
- 当前实现与验证状态：`docs/manuals/v3-current-implementation.md`
- 下一步执行计划：`docs/manuals/v3-next-steps.md`
- QA 运行手册：`docs/manuals/v3-qa-runbook.md`
- 关键决策记录：`docs/manuals/v3-decisions.md`
- 代码地图：`docs/manuals/v3-code-map.md`
