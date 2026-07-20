---
name: bugfix-recorder
description: "MUST USE after completing and verifying any bug fix in this project. Records each fix in the dedicated docs/bugfix/<system>.md document for its owning system; different systems must never be mixed in one document. Also use when the user asks to document, summarize, or record a bug fix. Do not use for unverified investigations, feature work, refactors without a bug, or speculative fixes."
---

# Bugfix Recorder

Use this skill after a bug fix has been implemented and verified. Preserve a durable, evidence-based explanation in the owning system's document under `docs/bugfix/` so a future maintainer can understand both the failure and why the repair works.

## Completion Gate

Do not describe a bug as fixed until all applicable checks have actually passed:

1. The reported symptom or a focused regression test has been reproduced or otherwise grounded in concrete evidence.
2. The root cause has been identified in the code or data flow.
3. The smallest appropriate repair has been applied.
4. Relevant diagnostics, tests, build, or manual reproduction steps have been run.

If verification is blocked or failing, report that status to the user instead of creating a completed bugfix record. Never invent commands, test results, logs, dates, issue numbers, or reproduction details.

## Classify the Owning System

Read the existing files in `docs/bugfix/` before writing.

- Identify the system that owns the broken invariant, not merely the file where the symptom surfaced. Examples include `road-graph`, `save-system`, `grid-rendering`, `tool-input`, `ui`, and `godot-integration`.
- Use one stable lowercase kebab-case system key. The canonical save/load key is `save-system`; do not create or reuse aliases such as `save` or `persistence`.
- Route by responsibility. A save/load bug belongs to `save-system` even when detected while loading roads; a topology bug belongs to `road-graph`; an editor addon connection bug belongs to `godot-integration`.
- If one repair fixes independent root causes in different systems, create or update one document per system. In each document, describe only that system's cause, change, impact, and verification, then cross-reference the companion document.
- If ownership is genuinely shared and cannot be separated, choose the system that owns the violated contract and add a short `关联文档` reference to the collaborating system. Never create a generic mixed document such as `misc-fixes.md`.

## Choose the System Document

- The canonical path is `docs/bugfix/<system>.md`.
- Append only when the existing document has the same owning system. Continue that document's existing `BUG-N` numbering.
- `BUG-N` numbering is local to one system document. Cross-document references must include the system key, for example `save-system:BUG-2` or `road-graph:BUG-1`.
- Otherwise create the canonical system document. Name it after the system, not the date, symptom, incident, branch, or a generic label such as `fix-1.md`.
- A follow-up or regression remains in the same system document even when it comes from a different incident. Separate incidents with distinct `BUG-N` sections, not separate symptom-named files.
- Preserve the existing document's language, headings, terminology, and level of detail. This project currently uses Chinese prose with code identifiers left unchanged.
- Do not rewrite or reorder historical entries merely to add a new fix.
- Existing mixed or legacy documents may remain unchanged for history. Do not append a new entry to a legacy document when its system ownership is ambiguous; route the new entry to the canonical system document and link back using the repository-relative legacy path and heading, for example `docs/bugfix/legacy-record.md#BUG-3`.

## Required Content

For a new document, use this structure:

```markdown
# <系统名称> Bug 修复记录

> 日期：YYYY-MM-DD
> 影响文件：`path/to/file`
> 关联事项：<issue、重构或用户报告；没有则省略>

---

## BUG-1：<精确描述故障及结果>

### 症状

<用户可观察行为、错误信息，以及必要的最小复现条件。>

### 根因分析

<说明相关代码路径、失效条件以及为什么会产生该症状。>

### 修复方案

<说明实际采用的最小修复及其正确性；必要时附精简代码片段。>

### 影响范围

<受影响的入口、数据或场景，以及明确不受影响的部分。>

---

## 验证状态

- `<实际执行的命令>`：<真实结果>
- <实际执行的回归或手工验证及结果>
- <仍存在的无关警告或未验证项，如有>
```

When appending to an existing document, follow that document's headings and prose style. Add the new section after the document's current last `BUG-N`, even if a historical verification section appears earlier; do not reorder old entries merely to normalize the layout. Update the existing `## 验证状态` with an explicitly labeled item for the new bug, or add that heading at the end when none exists. If one repair resolves multiple distinct root causes, record separate `BUG-N` sections; if there is one root cause with several symptoms, keep one section.

Before writing, confirm all files and symbols described in the new entry belong to the selected system or are explicitly identified as external collaborators. If the entry needs unrelated architecture explanations from another system, replace them with a repository-relative cross-reference instead of copying them into the document.

## Writing Rules

- Record facts from the completed work and tool output, not assumptions.
- Explain causality rather than merely listing changed lines.
- Include code snippets only when they make the failure mechanism or repair materially clearer; keep them focused.
- Use repository-relative paths and exact symbol names.
- Distinguish warnings unrelated to the fix from errors caused by it.
- Do not claim broad coverage from a narrow test.
- Do not include secrets, personal data, machine-specific absolute paths, or large raw logs.

## Final Report

After writing, tell the user which owning system was selected, which bugfix document was created or updated, and summarize the verification evidence recorded there. If a repair crossed systems, list every system document touched and why the records were split. Bugfix documentation is part of finishing the fix, but it does not replace tests or runtime validation.
