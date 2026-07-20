# OpenCode MCP 与 LSP 架构排障指南

本文说明 SimpleCities 当前 OpenCode 工具链里 MCP 与 LSP 的分工。重点是一个容易误判的现象：OpenCode 原生 LSP 或独立的 `Lsp*` 工具可能显示为禁用，但 Oh My OpenAgent 提供的 `lsp_diagnostics` 仍然可以正常工作。

相关文件：

- [项目 OpenCode 配置](../../opencode.json)
- [Godot C# QA 技能](../../.opencode/skills/godot-csharp-qa/SKILL.md)
- [项目 Skill 说明](skills.md#godot-csharp-qa)

## 一句话结论

项目本地 [opencode.json](../../opencode.json) 只声明了 `godot` 和 `godot-minimal` 两个 MCP。LSP 能力来自当前会话的合并配置，它启用了名为 `lsp` 的外部 MCP，并通过 Oh My OpenAgent 的 `packages/lsp-daemon/dist/cli.js mcp` 启动。这个外部 MCP 再连接 `csharp-ls`，所以即使 OpenCode 原生 LSP 没有启用，或独立的 `LspHover`、`LspCodeActions`、`LspCodeActionResolve` 工具被禁用，`lsp_diagnostics` 和其他允许的 `lsp_*` 工具仍然可以工作。

## 本地 MCP 声明

[opencode.json](../../opencode.json) 中的项目本地 MCP 只有两个。

`godot`：

```json
["npx", "-y", "@satelliteoflove/godot-mcp"]
```

`godot-minimal`：

```json
["npx", "-y", "@ryanmazzolini/minimal-godot-mcp@0.1.6"]
```

`godot-minimal` 还带有 `GODOT_WORKSPACE_PATH` 环境变量，用来指向本项目工作区。文档和排障记录里不要复制机器上的绝对路径，只需要知道它是项目工作区路径。

## 没有写在 opencode.json 里的部分

[opencode.json](../../opencode.json) 没有声明 `lsp-daemon`，也没有声明 `csharp-ls`。这不是缺配置。

本会话观察到的有效合并配置里，外部注入了一个已启用的 MCP，名称是 `lsp`。它启动 Oh My OpenAgent 的 `packages/lsp-daemon/dist/cli.js mcp`。`lsp-daemon` 再按语言把请求交给对应语言服务器。对于本项目的 C# 文件，目标语言服务器是 `csharp-ls`。

因此，排查 LSP 时要分清两层：

- 项目本地配置层：只管 `godot` 和 `godot-minimal`。
- 会话合并配置层：提供 `lsp` MCP，并由它连接 `csharp-ls`。

## OpenCode 原生 LSP 与 MCP LSP

OpenCode 官方的原生 LSP 配置使用顶层 `lsp` 键，但它与名为 `lsp` 的 MCP 不是同一个配置对象。根据 [OpenCode LSP 文档](https://opencode.ai/docs/lsp/) 和 [配置文档](https://opencode.ai/docs/config/)：

- 省略原生 `lsp` 配置时，原生 LSP 服务器默认不启用。
- `"lsp": false` 禁用全部原生 LSP 服务器。
- `"lsp": true` 启用内置服务器。
- `"lsp": { ... }` 用于启用、覆盖或增加具体服务器；单个服务器可通过 `"disabled": true` 禁用。

OpenCode 会合并远程、全局、自定义、项目、`.opencode`、内联和受管配置，后加载且优先级更高的配置可以覆盖前面的值。因此排障时应运行 `opencode debug config` 查看最终合并结果，而不是只阅读仓库里的 `opencode.json`。

本项目当前成功工作的 C# 路径不是上述原生 LSP 配置，而是会话合并配置中的 MCP 条目：

```text
MCP named lsp -> Oh My OpenAgent lsp-daemon -> csharp-ls
```

两个层次恰好都使用 `lsp` 这个名称，但一个是 OpenCode 原生配置键，另一个是 MCP 服务名。判断时必须同时看对象所在的配置区域和实际提供的工具名。

## ASCII 架构图

```text
OpenCode session
    |
    |-- project config: ../../opencode.json
    |       |
    |       |-- MCP: godot
    |       |       command: npx -y @satelliteoflove/godot-mcp
    |       |       role: Godot editor, scene, runtime, input, node inspection
    |       |
    |       `-- MCP: godot-minimal
    |               command: npx -y @ryanmazzolini/minimal-godot-mcp@0.1.6
    |               role: GDScript diagnostics and running-game console output
    |
    `-- externally injected merged config
            |
            `-- MCP: lsp
                    command: Oh My OpenAgent packages/lsp-daemon/dist/cli.js mcp
                    |
                    `-- csharp-ls
                            role: focused C# LSP diagnostics, references, definitions, rename
```

## 工具职责边界

### `lsp-daemon` 与 `csharp-ls`

`lsp-daemon` 是 MCP 入口，`csharp-ls` 是实际理解 C# 项目的语言服务器。OpenCode 的 `lsp_diagnostics`、`lsp_find_references`、`lsp_goto_definition`、`lsp_rename` 等工具走这条路径。

它适合回答这些问题：

- 当前 `.cs` 文件有没有语法、类型或可空性诊断。
- 某个符号的定义和引用在哪里。
- 一次重命名是否能由语言服务器安全应用。

它不替代 `dotnet build`。LSP 诊断是编辑器级反馈，能更快指出局部问题，但最终编译结果仍以构建输出为准。

### `dotnet build`

`dotnet build SimpleCities.sln` 是 C# 项目的真实编译门禁。它会经过 SDK、项目文件、引用、生成目标和编译器。不要把一次成功的 LSP 诊断当作构建通过，也不要把历史上的警告数写成永久事实。每次验证都记录当次输出。

### `godot`

`godot` MCP 负责 Godot 编辑器和运行中游戏的结构化控制。它能打开和保存场景、读写节点属性、运行项目、冻结时间、注入输入、读取运行态，以及检查编辑器日志。

它适合验证 Godot 侧行为：场景节点是否正确连接，资源是否被编辑器加载，运行时输入是否产生预期状态，视觉或 3D 网格问题是否真的出现在游戏里。

### `godot-minimal`

`godot-minimal` MCP 负责更轻量的 Godot 配套通道。它提供 GDScript 诊断和运行中游戏的 DAP 控制台输出。做运行时 QA 时，通常先清空它的控制台缓存，再执行场景，最后读取错误、警告和打印输出。

## 独立 Lsp* 工具为什么会显示禁用

有些配置或权限视图里会看到 `LspHover`、`LspCodeActions`、`LspCodeActionResolve` 为 `false`。这些是与当前 `lsp_*` MCP 工具分开的工具名，不是当前项目的有效 C# 诊断调用链。

当前有效路径是：

```text
lsp_diagnostics and related lsp_* tools
    -> MCP named lsp
        -> lsp-daemon
            -> csharp-ls
```

所以判断当前 MCP LSP 是否可用时，不要只看这些 `Lsp*` 工具名或 OpenCode 原生 LSP 是否启用。应该看本会话里 `lsp` MCP 是否存在并启用，以及 `lsp_diagnostics` 对一个具体 `.cs` 文件是否返回诊断结果。

## 每个会话的验证方法

每个新会话都重新验证一次，不要沿用上次状态。

1. 查看 MCP 状态，确认存在已启用的 `lsp` MCP，且项目本地的 `godot`、`godot-minimal` 仍启用。
2. 对一个相关 C# 文件运行一次聚焦诊断，例如 `Scripts/Road/RoadGraph.cs` 或本次要修改的 `.cs` 文件。
3. 确认诊断工具有响应。没有诊断不等于没有响应，空结果可以表示该文件当前没有 LSP 问题。
4. 如果本次改动涉及 C#，再运行 `dotnet build SimpleCities.sln`，以当次错误数和警告数为准。
5. 如果本次改动涉及 Godot 场景、资源或运行行为，按 [项目 Skill 说明](skills.md#godot-csharp-qa) 或 [Godot C# QA Skill 源文件](../../.opencode/skills/godot-csharp-qa/SKILL.md) 选择对应 QA 层级。

一次健康的聚焦验证应该能回答两个问题：

- `lsp_diagnostics` 是否能通过 `lsp-daemon` 连接到 `csharp-ls`。
- 当前文件的问题是新问题、既有问题，还是没有问题。

## 常见故障与判断

### `lsp_diagnostics` 可用，但 `LspHover` 显示禁用

这是预期情况。`LspHover` 不代表当前 MCP LSP 通道。以 `lsp_diagnostics` 和其他 `lsp_*` 工具的实际响应为准。

### `opencode.json` 里找不到 `lsp-daemon`

这也是预期情况。[opencode.json](../../opencode.json) 只保存项目本地 MCP。`lsp-daemon` 来自会话级合并配置，不由本仓库声明。

### `csharp-ls` 诊断和 `dotnet build` 结果不同

先保留两边输出，不要选更方便的一边。常见原因包括语言服务器缓存、项目恢复状态、生成文件时机、SDK 解析差异。处理顺序建议是：重新运行聚焦诊断，重新运行构建，再根据当前输出定位差异。

### Godot 运行时有错误，但 LSP 和构建都通过

这说明问题在运行面，而不是 C# 静态层。用 `godot` 复现场景状态，用 `godot-minimal` 读取 DAP 控制台，再按 [项目 Skill 说明](skills.md#godot-csharp-qa) 或 Skill 源文件中的 Tier 3 流程验证。

### `godot-minimal` 没有输出

先确认游戏是通过 Godot 调试会话运行的，并且在场景执行前清空过控制台缓存。空控制台不能单独证明成功，它只能说明该通道没有捕获到输出。还需要结构化运行态、节点属性、截图或信号观察来证明行为正确。

## 排障清单

- [ ] [opencode.json](../../opencode.json) 中的 `godot` 命令仍是 `["npx", "-y", "@satelliteoflove/godot-mcp"]`。
- [ ] [opencode.json](../../opencode.json) 中的 `godot-minimal` 命令仍是 `["npx", "-y", "@ryanmazzolini/minimal-godot-mcp@0.1.6"]`。
- [ ] 不把 `lsp-daemon` 或 `csharp-ls` 写成项目本地 MCP 声明。
- [ ] 当前会话状态里能看到启用的 `lsp` MCP。
- [ ] 对一个具体 `.cs` 文件运行过聚焦 `lsp_diagnostics`。
- [ ] C# 改动已用 `dotnet build SimpleCities.sln` 验证，并记录当次错误数和警告数。
- [ ] Godot 行为改动已用 `godot` 和 `godot-minimal` 做对应运行验证。

## 维护原则

修改这类文档时，把事实分成三类写清楚：仓库里声明了什么，会话合并配置注入了什么，当前验证实际看到了什么。不要把一次会话里的警告数、机器路径或外部注入路径写成永久项目配置。
