# SimpleCities 文档索引

`docs/` 按文档用途组织。源码和可运行行为是当前实现的最终事实来源；设计与路线图文档可能包含尚未实现的目标。

## 当前参考

- [类与 API 参考](reference/class-reference.md)
- [系统逻辑与运行流程](reference/game-logic.md)
- [UI 文档导航](ui/README.md)：当前 UI 设计、架构、概念稿和修复记录入口。
- [UI 架构](ui/architecture.md)：当前命令中心实现与未来扩展边界。
- [存档系统当前参考](reference/save-system-plan.md)：命名槽、自动存档、manifest、道路 schema 与失败语义的详细契约。

## 当前待办

- [系统待办索引](todo/README.md)
- [路网图待办](todo/road-graph.md)
- [存档系统待办](todo/save-system.md)
- [网格渲染待办](todo/grid-rendering.md)
- [工具输入待办](todo/tool-input.md)
- [第三代道路系统待办索引](todo/v3/README.md)
- [交通模拟待办](todo/traffic-simulation.md)

## 已验证修复

- [Bugfix 索引](bugfix/README.md)

各系统的 road-graph、save-system、grid-rendering、ui、camera、road-rendering 和 tool-input 修复记录统一从 Bugfix 索引进入，避免根索引重复维护不完整的子列表。

## 性能基线

- [RoadGraph V2 性能基线](performance/road-graph-v2-baseline.md)
- [道路渲染 V2 性能基线](performance/road-rendering-v2-baseline.md)

## 设计

- [视觉设计系统](ui/design-system.md)：当前 UI 视觉设计系统事实来源。
- [设计总览](design/overview.md)
- [游戏风格讨论](design/game-style-discussion.md)
- [模拟数学模型](design/math-model.md)
- [模拟系统设计](design/simulation-systems.md)

这些文档主要记录产品方向、系统方案和远期模型，不应直接视为当前实现契约。

## 路线图与工作记录

- [实现路线图](roadmaps/implementation-roadmap.md)
- [系统待办索引](todo/README.md)：按 owning system 拆分的长期任务与验收标准。
- [会话记录](session-notes/)：重要工作阶段的上下文与迁移说明。

## 开发手册

- [网格系统](manuals/grid-system.md)
- [基础设施开发指南](manuals/infrastructure-guide.md)
- [RoadGraph 重构与演进说明](manuals/road-system-v2-gen.md)
- [第三代道路系统迭代指南](manuals/road-system-v3-gen.md)：可完全重构且不向后兼容的 canonical Edge、自环/平行边、RoadGroup 移除、RoadType、分级渲染和独立 V3 存档契约。
- [第三代道路系统当前实现与验证状态](manuals/v3-current-implementation.md)：汇总 V3 已落地模块、验证证据与开放工作项。

## OpenCode 工具链

- [项目 Skill 说明](opencode-tooling/skills.md)
- [OpenCode MCP 与 LSP](opencode-tooling/opencode-mcp-lsp.md)
