# 2026-08-05 会话整理：Roslyn CodeLens 与 OpenCode C# 诊断

> 作用范围：SimpleCities 项目本地会话记录
> 存放位置：`docs/session-notes/`
> Git 状态：本记录与所述配置在同一原子提交中。

## 会话目标

为 Codex 和 OpenCode 配置同一套 Roslyn CodeLens C# 语义诊断，并禁用旧的 `csharp-ls` 自动 LSP 路径。

## 已完成

- 安装用户级 `.NET` 工具 `RoslynCodeLens.Mcp 2.16.1`。
- 在 `.codex/config.toml` 注册 `roslyn-codelens`，并加载 `SimpleCities.sln`。
- 在 `opencode.json` 注册同名 OpenCode MCP 服务。
- 新增 `.codex/lsp-client.json` 与 `.opencode/lsp.json`，将内建 `csharp` LSP 设为 `disabled: true`。
- 更新 `.agents/skills/godot-csharp-qa/SKILL.md`：C# 诊断改用 Roslyn CodeLens；服务不可用时仅执行构建检查并报告诊断受阻，不再回退到 `csharp-ls`。

## 已验证

- `dotnet tool list --global` 显示 `roslyncodelens.mcp 2.16.1`，命令为 `roslyn-codelens-mcp`。
- `codex mcp list` 识别 `roslyn-codelens` 为已启用的项目 MCP。
- `opencode mcp list --print-logs --log-level DEBUG` 显示 `roslyn-codelens` 已连接，并记录 `all LSPs are disabled`。Oh My OpenAgent 的通用 `lsp` MCP 外壳仍会连接，但不会启动 C# 的 `csharp-ls`。
- 三份 JSON 配置均通过 PowerShell `ConvertFrom-Json` 解析。
- `python -X utf8 .../quick_validate.py .agents/skills/godot-csharp-qa` 输出 `Skill is valid!`。
- `git diff --check` 通过。

## 重要决策

- 保留全局 `csharp-ls` 工具而不卸载；项目配置阻止 Codex 和 OpenCode 自动使用它，仍可在明确需要时手动调用。
- 共享 QA Skill 以 Roslyn CodeLens 为优先诊断来源，并保留 `dotnet build SimpleCities.sln` 作为强制编译门禁。

## 未完成与阻塞

- 未运行 `dotnet build`、Godot 编辑器或游戏运行时测试：本次仅修改代理配置和 Skill，不改变游戏代码或资源，且 QA Skill 明确将此类配置变更排除在 Godot QA 之外。
- 已打开的 Codex 任务需要新建或重启后才会加载新增的 MCP；OpenCode 在本次 CLI 验证中已重新加载项目配置。

## 既有工作区状态

- 多个 `Scripts/` 与 `tests/` 下的未跟踪 `.uid` 文件在本次配置工作前已存在，未修改也不会纳入本次提交。

## 相关文件

- `.codex/config.toml`：Codex 的 Roslyn CodeLens MCP 注册。
- `opencode.json`：OpenCode 的 Roslyn CodeLens MCP 注册。
- `.codex/lsp-client.json` 与 `.opencode/lsp.json`：禁用自动 C# LSP。
- `.agents/skills/godot-csharp-qa/SKILL.md`：跨代理的 Godot C# QA 流程。
