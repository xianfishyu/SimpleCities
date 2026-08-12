# OpenCode MCP 与 C# 诊断排障指南

本文说明 SimpleCities 当前 OpenCode 工具链中 Godot、GDScript 和 C# 诊断通道的分工。仓库配置与实际会话能力可能不同；执行 QA 时以当前会话暴露的工具和实际输出为准。

相关文件：

- [项目 OpenCode 配置](../../opencode.json)
- [Godot C# QA Skill](../../.agents/skills/godot-csharp-qa/SKILL.md)
- [项目 Skill 说明](skills.md#godot-csharp-qa)

## 当前结论

[opencode.json](../../opencode.json) 声明三个项目本地 MCP：

| MCP | 命令 | 职责 |
|---|---|---|
| `godot` | `npx -y @satelliteoflove/godot-mcp` | Godot 编辑器、场景、运行时、输入和节点检查 |
| `godot-minimal` | `npx -y @ryanmazzolini/minimal-godot-mcp@0.1.6` | GDScript 诊断和运行中游戏的 DAP 控制台 |
| `roslyn-codelens` | `roslyn-codelens-mcp <SimpleCities.sln>` | C# 语义与分析器诊断 |

`roslyn-codelens` 必须指向当前工作区的 `SimpleCities.sln`。配置中可能使用机器绝对路径，但文档和排障记录不应复制该路径。

旧的 Oh My OpenAgent `lsp-daemon -> csharp-ls` 链路不再是本项目的 C# QA 契约。即使某个会话额外暴露 `lsp_*` 工具，也不得用 `csharp-ls` 代替 QA Skill 要求的 Roslyn CodeLens 诊断。

## 工具职责边界

### `roslyn-codelens`

先确认 MCP 已加载 `SimpleCities.sln`，再对每个改动的 `.cs` 文件运行 `get_diagnostics`。跨文件 API 改动还要检查入口或消费者。需要分析器诊断时，先通过 MCP 暴露的信任操作信任解决方案。

Roslyn CodeLens 不替代构建。聚焦诊断和 `dotnet build SimpleCities.sln` 都是 C# 改动的 Tier 1 门禁；两者冲突时保留两边结果并调查缓存、项目恢复、生成文件和 SDK 解析差异。

### `dotnet build`

`dotnet build SimpleCities.sln` 是真实编译门禁。每次验证都记录当次退出码、错误数和警告数，不把历史计数写成永久事实。

### `godot`

`godot` MCP 用于验证 Godot 编辑器和运行时的结构化状态，包括场景与资源加载、节点属性、输入、信号、运行态和编辑器日志。

### `godot-minimal`

`godot-minimal` MCP 提供 GDScript 诊断和运行中游戏的 DAP 控制台。运行时 QA 应在场景前清空控制台缓存，执行场景后读取错误、警告和打印输出；空控制台不能单独证明行为通过。

## 每个会话的验证方法

1. 检查当前会话是否暴露 `roslyn-codelens`、`godot` 和 `godot-minimal`。
2. 确认 Roslyn CodeLens 已加载当前 `SimpleCities.sln`。
3. 对本次相关 `.cs` 文件运行聚焦诊断；需要分析器覆盖时先信任解决方案。
4. 对 C# 或项目改动运行 `dotnet build SimpleCities.sln`。
5. 场景、资源或玩家可见行为按 [Godot C# QA Skill](../../.agents/skills/godot-csharp-qa/SKILL.md) 选择 Tier 2 或 Tier 3。

## 常见故障与判断

### `roslyn-codelens` 不存在或没有加载解决方案

不要回退到 `csharp-ls` 并宣称聚焦诊断通过。继续执行构建门禁，并明确报告 Roslyn 聚焦语义诊断被阻塞及缺少的证据。

### 分析器诊断要求信任解决方案

使用 MCP 暴露的信任操作后重新请求诊断。如果当前工具没有信任入口，普通语义诊断与构建仍可执行，但分析器覆盖应报告为阻塞。

### Roslyn 诊断和构建结果不同

保留两边输出，重新确认解决方案路径并重跑聚焦诊断和构建。不要选择更方便的一边，也不要把其中一个结果静默忽略。

### Godot 运行时有错误，但诊断和构建通过

问题位于运行面。用 `godot` 复现场景并检查结构化状态和编辑器日志，用 `godot-minimal` 读取 DAP 控制台，再按 Tier 3 流程验证。

## 排障清单

- [ ] `opencode.json` 仍声明 `godot`、`godot-minimal` 和 `roslyn-codelens`。
- [ ] `roslyn-codelens` 指向当前工作区的 `SimpleCities.sln`。
- [ ] 当前会话已确认 Roslyn CodeLens 加载了解决方案。
- [ ] 每个相关 `.cs` 文件均运行了聚焦诊断，或已明确报告阻塞。
- [ ] 需要分析器覆盖时已信任解决方案，或已报告该门禁阻塞。
- [ ] C# 改动已运行 `dotnet build SimpleCities.sln`，并记录实际错误数和警告数。
- [ ] Godot 行为改动已按风险使用 `godot` 和 `godot-minimal` 验证。

## 维护原则

仓库配置说明“项目声明了什么”，当前会话工具说明“这次实际能调用什么”，QA 输出说明“这次验证观察到什么”。三者必须分开记录，且 [Godot C# QA Skill](../../.agents/skills/godot-csharp-qa/SKILL.md) 是验证流程的权威规则。
