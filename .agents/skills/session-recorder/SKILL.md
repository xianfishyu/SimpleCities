---
name: session-recorder
description: "MUST USE when the user asks to record, summarize, preserve, hand off, or organize the current Codex or OpenCode session in a durable project note. Writes a factual session summary under docs/session-notes/, which may be tracked by Git. Do not use for project bugfix records, durable todo roadmaps, implementation plans, or ordinary conversational summaries unless the user asks for a session record."
---

# Session Recorder

Use this Skill to preserve the useful context of the current Codex or OpenCode session in a project note that is available to later sessions and may be committed to the project repository. The canonical output directory for SimpleCities is:

```text
docs/session-notes/
```

This Skill records the session itself. It does not replace `bugfix-recorder`, `todo-manager`, project documentation, or the built-in session handoff mechanism.

## Scope Gate

Use this Skill when the user asks to:

- record or整理 the current session;
- preserve decisions and completed work for a later session;
- create a local session summary or handoff note;
- organize the current conversation into a durable project document.

Do not use it for:

- recording a verified bug fix in `docs/bugfix/`; use `bugfix-recorder`;
- maintaining long-lived work items in `docs/todo/`; use `todo-manager`;
- writing a project implementation plan;
- ordinary answers that do not request a durable session record;
- copying the full raw transcript or tool logs into a note.

## Output Location and Naming

Write the note under `docs/session-notes/`. Use a stable, readable filename:

```text
YYYY-MM-DD-<简短中文主题>.md
```

Use a short, readable Chinese phrase for `<简短中文主题>`. Keep established technical names such as `OpenCode`、`MCP`、`LSP`、`C#` in their original spelling when that is clearer. Do not use Windows-invalid filename characters (`< > : " / \\ | ? *`) or trailing dots/spaces. If a file with the same name already exists, append `-2`, `-3`, and so on rather than overwriting an earlier record, for example:

```text
2026-07-20-OpenCode工具链与会话记录-2.md
```

Before writing, verify that `docs/session-notes/` exists or can be created under the project documentation tree. The directory is allowed to be tracked by Git. Do not stage, commit, push, or change ignore rules unless the user also explicitly requests the corresponding Git or configuration action. When the user requests both a session record and a commit for the work recorded by that note, follow **Joint Archive Commit** below instead of treating the note as a separate documentation commit.

## Evidence Rules

Record facts from the current conversation and actual tool output. Separate statements into these categories whenever they matter:

- **已完成**：the requested work was actually performed;
- **已验证**：a command, diagnostic, test, or manual check produced the stated result;
- **未完成**：planned or discussed work that was not done;
- **阻塞**：work that could not be performed, with the missing prerequisite;
- **既有状态**：pre-existing dirty files, warnings, failures, or decisions not caused by this session.

Do not:

- invent commands, test results, dates, issue numbers, warnings, or approvals;
- call a proposed change completed merely because it was planned;
- call a build, test, runtime scenario, or review passed unless it actually ran;
- copy secrets, API keys, credentials, tokens, personal data, or large raw logs;
- include machine-specific absolute paths when a repository-relative path is sufficient;
- silently attribute unrelated dirty-worktree changes to the current session.

When a fact comes from an earlier session note rather than current verification, label it as historical context instead of presenting it as newly verified.

## Required Note Structure

Use this structure unless the user requests a different format:

```markdown
# YYYY-MM-DD 会话整理：<主题>

> 作用域：SimpleCities 项目本地会话记录
> 存放位置：`docs/session-notes/`
> Git 状态：`docs/session-notes/` 当前是否可被 Git 跟踪

## 会话目标

<用户希望本次会话达成的结果。>

## 已完成

- <实际完成的工作，附 repository-relative 路径或符号。>

## 已验证

- `<实际命令或工具操作>`：<真实结果。>

## 重要决策

- <采用的方案、原因和明确的边界。>

## 未完成与阻塞

- <未完成事项、阻塞原因或缺少的验证。>

## 既有工作区状态

- <与本会话无关的脏文件、既有警告或用户改动。>

## 相关文件

- `<path>`：<作用。>

## 后续建议

1. <下一步行动，只有在有事实依据时列出。>
```

Omit empty sections when they add no information, but keep `已验证` separate from `已完成` for technical work. If no verification was run, state that explicitly instead of implying success.

## Session Summary Workflow

1. **Identify the request**：use the current user request as the authority for what should be recorded. If the session is long and session history tools are available, inspect the session history before reconstructing earlier user requests.
2. **Collect evidence**：review relevant tool results, changed files, diagnostics, tests, build output, and Git status. Do not re-run unrelated expensive checks solely to fill the note.
3. **Classify the state**：separate completed work, verification evidence, decisions, pending work, blockers, and pre-existing changes.
4. **Write the note**：use repository-relative paths and concise summaries; link to project files only when the links will remain useful locally.
5. **Validate the note**：check Markdown formatting, confirm the output path exists and has the intended Git visibility, and ensure no secret or accidental raw log was included.
6. **Report back**：tell the user the exact local note path, what it contains, and any important facts that remain unverified.

## Joint Archive Commit

When the user explicitly requests both session archiving and committing the implementation described by the note:

1. Finish and verify the implementation before finalizing the note.
2. Write the note before staging the commit. Describe the verified work and the intended joint commit, but do not invent a commit hash that does not exist yet.
3. Use `git-master` for all Git operations. Stage the note with that implementation, its direct tests, and its canonical bugfix/todo documentation as one atomic group.
4. Do not split the session note into a later documentation-only commit merely because it lives under `docs/session-notes/`.
5. Keep unrelated earlier notes and unrelated dirty files out of the commit.

If the implementation is already committed but the note is not, adding it to the same commit requires amending or rewriting history. Do not create a separate archive commit as a silent fallback and do not rewrite history without explicit user authorization; report the state and request the required Git action.

## Boundaries with Other Skills

- A verified repair belongs in `docs/bugfix/<system>.md` through `bugfix-recorder`; the session note may link to it but must not duplicate it as the canonical record.
- A durable project task belongs in `docs/todo/<system>.md` through `todo-manager`; the session note may list it as pending but must not silently change its status.
- A work plan belongs in the project planning workflow; the session note records the decision and plan path after the plan exists.
- Git status may be recorded for context, but this Skill never performs Git writes itself. When the user has explicitly requested a commit, delegate staging and committing to `git-master` and apply **Joint Archive Commit**.
- The `docs/session-notes/` note is continuity state that may be tracked, but it is not a substitute for canonical project documentation.

## Completion Gate

Do not declare the session record complete until:

- the note exists under `docs/session-notes/`;
- the path exists and its Git visibility is reported accurately;
- completed and verified work are distinguished;
- pending, blocked, and pre-existing items are not misclassified;
- no secrets, credentials, or unnecessary machine-specific paths are present;
- the user has been given the exact note path.
