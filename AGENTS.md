# SimpleCities Agent Instructions

## Project Skills

Project-level skills shared by Codex and OpenCode live under `.agents/skills/`.
Load a skill when its frontmatter description matches the current request or
changed files.

- `godot-csharp-qa`: verify C#, Godot scenes/resources, project settings, and
  runtime behavior.
- `bugfix-recorder`: record a completed and verified bug fix by owning system.
- `todo-manager`: maintain durable, system-owned work items in `docs/todo/`.
- `git-master`: handle requested commits, history investigation, or history
  rewriting.
- `session-recorder`: write a durable note for the current session when asked.

The `SKILL.md` files under `.agents/skills/` are the canonical project
definitions. Codex also reads each skill's optional `agents/openai.yaml`
metadata, while OpenCode discovers the same `SKILL.md` files directly. Do not
treat loading a skill as authorization for implementation, Git writes, or
other actions outside the user's request.

If a skill requires a tool or MCP server that the current session does not
expose, run the applicable local checks, report the unavailable gate as
blocked, and do not claim that the missing verification passed.

## Agent skills

### Issue tracker

处理规格、工单及阻塞关系前，读取 [GitHub 工作流](docs/agents/issue-tracker.md)。

### Triage labels

使用默认五种分流标签；应用标签前读取 [标签映射](docs/agents/triage-labels.md)。

### Domain docs

采用 single-context 布局；探索领域代码或编写设计前，读取 [领域文档规则](docs/agents/domain.md)。
