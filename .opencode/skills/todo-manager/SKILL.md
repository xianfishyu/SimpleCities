---
name: todo-manager
description: "MUST USE when adding, reviewing, prioritizing, splitting, deferring, completing, reopening, cancelling, superseding, or otherwise maintaining durable project work items in the dedicated docs/todo/<system>.md roadmap for the owning system. Different systems must never be mixed in one todo document. Also use after verified work that corresponds to an existing project todo. Do not use as a substitute for the session task tracker, for general implementation review, for speculative ideas without actionable acceptance criteria, for bugfix documentation, or for Git commits."
---

# Todo Manager

Manage the project's durable roadmaps as one document per owning system under `docs/todo/`. Keep them grounded in the current working tree, current requirements, and actual verification evidence. These files track work across sessions; they are different from the agent's temporary in-session todo list.

## Scope Gate

Use this skill for project-level work items that should survive the current session:

- Add an actionable feature, refactor, test, performance, documentation, or architecture task.
- Review whether an existing item is still valid against the current code.
- Split an item that is too broad to implement or verify safely.
- Reorder phases or prerequisites after dependencies change.
- Mark an item completed after its acceptance criteria have been verified.
- Defer, cancel, supersede, or reopen an item with an evidence-based reason.

Do not add:

- Transient execution steps that belong only in the current session tracker.
- Vague ideas such as "improve roads" without scope and acceptance criteria.
- A bug that has already been fixed solely to make the list look complete; keep verified fixes in `docs/bugfix/` and, when useful, retain only their missing regression-test work as todo items.
- Work already represented by an equivalent active item.
- Claims copied from design documents without checking current source first.

## Classify and Route by System

Before reading or editing a todo document, identify the system that owns the work:

- Use stable lowercase kebab-case keys such as `road-graph`, `persistence`, `grid-rendering`, `tool-input`, `ui`, and `godot-integration`.
- Route by the system that owns the requirement or invariant, not by the file most likely to be edited. For example, save schema work belongs to `persistence`; topology and spatial-index work belongs to `road-graph`; toolbar and panel work belongs to `ui`.
- The canonical path is `docs/todo/<system>.md`. Reuse an existing system document and key; do not create aliases such as both `save.md` and `persistence.md`.
- A document must contain active, deferred, completed, and baseline items for one system only. Do not add a general `misc`, `project`, or catch-all roadmap.
- When one initiative spans systems, split it into one independently verifiable item per system. Link the items with `依赖` or `关联` fields and state which system owns the integration acceptance criterion.
- If a task cannot be split without losing meaning, place it in the system that owns the final contract and link to collaborating system documents. Do not copy the full task into multiple files.
- Existing mixed or legacy documents may remain unchanged for history, but new items must go to canonical system documents. When touching a legacy item, migrate only that item to its owning system document and leave a concise superseded pointer; do not silently duplicate it. If the user's current request explicitly forbids migration, do not migrate: update only canonical documents for new work and report the legacy item as unchanged.

## Ground Truth

Before editing, list `docs/todo/`, read the entire target system document, inspect linked system documents when dependencies cross boundaries, and inspect the source, configuration, or documentation relevant to the affected item.

1. Treat current code and runnable verification as primary evidence.
2. Treat `docs/bugfix/` as evidence of completed repairs, not as a source of new implementation work unless regression coverage is missing.
3. Treat design documents as intent; distinguish current behavior from future plans.
4. Preserve existing numbering and references whenever possible so links and historical context remain understandable.
5. If the evidence is incomplete, leave the item open and state what remains unknown.

## System Document Structure

Each `docs/todo/<system>.md` document should preserve this structure unless the user explicitly requests a redesign:

1. `# <系统名称>待办清单`
2. Metadata block with system key, 整理日期, evidence sources, and governing principle
3. `## 状态总览`
4. `## 执行顺序`, grouped into dependency-ordered phases
5. `## 暂不执行`, with reasons and reopening conditions
6. `## 已解决基线`, for verified behavior that future changes must preserve
7. `## 完成标准`

The summary table is an index, not a second independent source of truth. Whenever an item's status or disposition changes, update both its detailed entry and the corresponding summary row in the same edit.

Numbering is local to one system document. Identical numeric IDs may exist in different documents, so references outside the document must include the system key, for example `road-graph:1.2` or `persistence:0.4`.

## Item Format

Use stable phase-based identifiers such as `0.4`, `1.2`, or `5.3`. Insert a new item into the phase that reflects its prerequisites; do not append everything to the end.

```markdown
- [ ] **<阶段.序号> <可执行且结果导向的标题>**
  - 当前问题：<可由源码、行为或日志验证的现状与风险>。
  - 修改：<明确的实现边界；必要时列出关键文件或符号>。
  - 测试：<必须执行的自动化或手工场景>。
  - 验收：<可观察、可判定的完成条件>。
```

Use only the fields that fit the work, but every active item must include:

- **Why**: the current problem, requirement, or dependency.
- **Where/How**: the subsystem, files, symbols, or strategy involved.
- **Verification**: tests, build, metrics, or manual scenario.
- **Expected result**: a binary or measurable acceptance condition.

For cross-system work, also include:

- **Dependency**: the exact `<system>:<id>` prerequisite or collaborating item.
- **Integration owner**: the one system document responsible for the end-to-end acceptance result.

For performance tasks, require a baseline, dataset size, metric, and target or comparison method. For migrations, require backward-compatibility and rollback/error behavior. For documentation tasks, name the documents and the source facts they must match.

## Status Transitions

### Open

Use `- [ ]` for actionable items that are not fully verified. Partial implementation remains open; add a concise progress note instead of checking the item.

### Completed

Change to `- [x]` only when all stated acceptance criteria are satisfied by actual evidence.

Before marking complete:

1. Confirm the relevant implementation or document exists in the current working tree.
2. Run the item's specified tests, build, diagnostics, metrics, or manual scenario where applicable.
3. Record the exact evidence concisely in the owning system's item or `已解决基线`.
4. Update the summary row to `已完成` or `已修复` and describe any remaining follow-up separately.

If some acceptance criteria remain unverified, do not mark complete. Split the remaining work into a new item only when the completed portion is independently valuable and verifiable.

### Deferred

Move non-actionable work to `## 暂不执行` when a product decision, prerequisite, or external requirement is missing. Include:

- Why it is deferred now.
- What remains unchanged in the meantime.
- A precise reopening condition.

Deferred is not completed. Keep its checkbox open unless the item describes a decision that has itself been completed.

### Cancelled or Superseded

Do not silently delete historical work. Mark the summary disposition as `取消` or `已取代`, explain why, and point to the replacing item or current behavior. Remove the detailed item only when it has no lasting context and the user explicitly requests cleanup.

### Reopened

Change `[x]` back to `[ ]`, record the regression or invalidated assumption, and add new verification criteria. If the reopened issue is a confirmed bug, use `bugfix-recorder` after the new repair is implemented and verified.

## Prioritization and Dependencies

Order work by dependency and risk, not by when it was mentioned:

1. Regression protection and reproducible test entry points.
2. Correctness, data integrity, save compatibility, and destructive-operation safety.
3. User-visible behavior and interaction defects.
4. Architectural refactors protected by tests.
5. Performance work with measured baselines.
6. Visual polish, naming cleanup, and documentation calibration.

An item that enables several later items in the same system belongs in an earlier phase. Cross-system prerequisites must use explicit `<system>:<id>` references because ordering in one document cannot represent global order. Do not move a task into an earlier phase merely because it is easy.

## Updating After Implementation

When completed work corresponds to an existing todo:

1. Identify the owning system first, then match the work by behavior and acceptance criteria, not only by title or file name.
2. Compare the actual implementation with every requirement in that item.
3. Mark it completed only if all requirements pass.
4. Update source line references if edits made them stale; prefer symbol names over fragile line-only references.
5. Add or update `已解决基线` when the behavior is important regression protection.
6. Set `整理日期` to the actual current date only when the document was materially reviewed or changed.

Do not automatically close nearby items just because the same file was modified. Do not close an item in another system unless that document's own acceptance criteria were verified. Do not weaken acceptance criteria to match an incomplete implementation.

## Cross-Skill Boundaries

- **Session tracker**: use the agent's todo tool for immediate execution steps. This skill manages only system roadmaps under `docs/todo/`.
- **Bug fixes**: after a verified repair, use `bugfix-recorder` for the owning system's durable incident record. Update a system todo document only if one of its listed items was completed or follow-up work remains.
- **Git**: todo maintenance never authorizes staging, committing, rebasing, or pushing. Use `git-master` only when the user explicitly requests Git work.
- **Implementation**: editing the todo does not authorize implementing the listed work unless the user's current request explicitly asks for implementation.

## Writing Rules

- Preserve Chinese prose and exact code identifiers.
- Use repository-relative paths; never include machine-specific absolute paths.
- Keep tasks concise but decision-complete.
- State facts, not confidence language or unsupported estimates.
- Never invent test results, performance numbers, issue IDs, dates, or completion evidence.
- Avoid duplicate requirements across multiple active items; use references when concerns overlap.
- Never place unrelated systems in one document for convenience. Split and cross-reference instead.
- Do not rewrite the whole roadmap for a small status update.

## Final Report

After editing, tell the user:

- Which owning system document or documents were selected.
- Which todo IDs were added, changed, completed, deferred, reopened, or superseded.
- Why their status changed.
- What verification evidence supports completed items.
- Which items remain blocked or unverified.
- For cross-system initiatives, which document owns final integration acceptance and which dependencies were linked.

Report Git state only when relevant, and never create a commit unless explicitly requested.
