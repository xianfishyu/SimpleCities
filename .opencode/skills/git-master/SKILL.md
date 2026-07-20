---
name: git-master
description: "MUST USE whenever a task needs a commit or git-history investigation. Covers atomic commits, staging, commit-message style, rebase, squash, fixup/autosquash, blame, bisect, reflog, git log -S/-G, and questions like who wrote this or when was this added. Do not use for ordinary code edits unless the user asks for git work."
---

# Git Master

Use this skill when the user asks you to operate on Git history or answer a Git-history question. Be exact, conservative, and evidence-led. Read the repository state before you infer anything.

## Mode Gate

Classify the request first:

- `COMMIT`: stage and commit local changes.
- `REBASE`: rebase, squash, fixup, autosquash, reorder, split, or otherwise rewrite branch history.
- `HISTORY`: answer when, where, who, why, or which commit changed something.
- `STATUS`: inspect branch, diff, or working-tree state without changing it.

Do not commit, rebase, push, force-push, reset, stash-pop, or delete anything unless the user explicitly asked for that operation. If the request is only investigative, report findings and stop.

## Ground Truth

Gather independent facts in parallel when the tools allow it:

```bash
git status --short
git diff --stat
git diff --staged --stat
git branch --show-current
git log -30 --oneline
git log -30 --pretty=format:%s
git rev-parse --abbrev-ref @{upstream}
git merge-base HEAD origin/main
git merge-base HEAD origin/master
```

Missing upstream or missing `main`/`master` is normal. Fall back to the best available branch or report the missing fact. Never treat a failed lookup as proof.

## Commit Mode

Commit only the user's requested changes. Preserve unrelated dirty work.

1. Detect message style from recent history. Use the dominant local pattern, language, and casing. Do not default to Conventional Commits unless the repo uses them.
2. Inspect the full diff, not only filenames. Separate unrelated user edits from the requested commit.
3. Build atomic groups by behavior, module, and revertability. Keep implementation and its direct tests together.
4. Prefer multiple commits for unrelated concerns. A single commit is acceptable only when the changed files form one indivisible behavior or the user explicitly asks for one commit.
5. Stage by path or hunk so each commit contains only its atomic group.
6. Before each commit, verify `git diff --staged --stat` and enough staged diff to prove the group is right.
7. Commit with the detected style. After each commit, verify `git log -1 --oneline`.

Grouping rules:

- Split different features, modules, generated artifacts, config, docs, and test-only changes unless they are inseparable.
- Keep generated files with the source change that produced them when omitting them would leave the repo inconsistent.
- Never hide failing or unrelated changes inside a broad commit.

Final report: list commit hashes, messages, and any remaining uncommitted files.

## SimpleCities Commit Convention

Apply this repository-specific convention after inspecting the current history. Re-check at least the latest 15 non-merge commits before committing because the project style may evolve; current history takes precedence if it establishes a newer consistent pattern.

### Evidence and Current Direction

The history through 2026-07-15 shows a transition:

- Recent structured subjects use Chinese action labels without a leading hash, for example `修复：保持道路类型存档并避免重复铺路副作用`, `整理：将4份设计指南移入 docs/manuals/`, and `修订：grid-system.md 改为抽象接口设计…`.
- Older commits frequently use `#修复：`, `#文档：`, `#fix:`, `#debug:`, or experimental `###...` prefixes. Treat these as legacy history, not the target format.
- Most focused commits affect one subsystem or one coherent documentation concern. A few broad commits combine unrelated cleanup, dependency upgrades, and renames; do not copy that bundling when the changes can be reverted independently.
- Some older useful commit bodies explain failure mechanisms or documentation-change details in short paragraphs or bullets. Treat that explanatory structure as useful precedent, but do not infer that every recent commit requires a body.

### Subject Format

Use:

```text
<类型>：<具体结果>
```

Rules:

- Write the subject primarily in Chinese. Keep code symbols, filenames, APIs, and established product names in their original spelling.
- Do not start the subject with `#`, Markdown headings, emoji, ticket boilerplate, or English Conventional Commit prefixes such as `feat:` and `fix:`.
- Use a full-width Chinese colon `：` after the type.
- Describe the completed behavior or outcome, not the activity. Prefer `修复：保持 RoadType 存档并阻止重复铺路` over `修复：修改 RoadGraph.cs`.
- Keep one subject to one coherent concern. Do not enumerate unrelated work with commas merely to force it into one commit.
- Do not add a trailing period.

Choose the narrowest accurate type:

| Type | Use for |
|---|---|
| `修复` | User-visible defects, regressions, incorrect behavior, build failures |
| `添加` | New capabilities, files, systems, or documentation |
| `更新` | Existing behavior, dependencies, configuration, or documentation brought forward without being a bug fix |
| `重构` | Internal restructuring with intentionally unchanged behavior |
| `整理` | Moves, removals, naming cleanup, repository organization |
| `修订` | Design or documentation corrections and refinements |
| `测试` | Test-only additions or corrections |

Use `文档` only when it is more precise than `添加`, `更新`, or `修订`; prefer the action-specific type so the subject explains what happened.

Examples:

```text
修复：在 waypoint 交叉处正确拆分道路
添加：道路系统下一代迭代设计指南
整理：将设计指南迁移到 docs/manuals
更新：升级 Godot 至 4.7
重构：以 RoadGraph 替换旧路网数据模型
测试：覆盖 RoadType 存档往返
```

Avoid:

```text
#fix: bug
#debug: 加log
###进行重构
更新代码
清理配置，升级引擎，修改项目名
```

### Body Format

The body is optional for a self-explanatory small change. Add one when a reviewer needs causality, migration context, a compatibility warning, or verification details.

For bug fixes, prefer:

```text
<症状或触发条件>导致<错误结果>。

根因：<具体代码或数据流原因>。
修复：<采用的行为性改动及为什么有效>。
验证：<实际执行的测试、构建或复现场景>。
```

For multi-file features or documentation work, short bullets are acceptable. Wrap for readability, avoid copying large logs, and do not claim verification that was not run.

### Atomic Grouping

- Keep implementation together with its direct tests and required scene/resource metadata.
- Keep a changed bugfix record in `docs/bugfix/` with the corresponding fix when it documents that exact change; do not create or stage an unrelated record solely to satisfy commit grouping.
- Split dependency or engine upgrades, project renames, unrelated cleanup, and independent features into separate commits.
- Temporary diagnostics such as debug logging should normally be removed before committing. If the user explicitly wants diagnostic instrumentation preserved, use a clear `添加` or `更新` subject rather than `debug`.
- A refactor and a behavior change belong in separate commits unless separating them would leave either commit uncompilable or misleading.

### Pre-Commit Message Check

Before creating each commit, verify:

1. The staged diff matches exactly one subject.
2. The selected type describes the staged behavior.
3. The subject uses Chinese punctuation and has no legacy `#` prefix.
4. Symbols and paths are spelled exactly as they appear in the diff.
5. Any body statements and verification claims are supported by actual evidence.
6. The mandatory attribution footer and co-author trailer are added exactly once.

## Rebase Mode

History rewriting is a shared-impact operation.

- Never rebase or rewrite `main`, `master`, `dev`, release branches, or a protected branch unless the user explicitly named that exact operation.
- If commits may already be pushed, ask before force-pushing. Use `--force-with-lease`, never plain `--force`.
- If the worktree is dirty, preserve it intentionally before rebasing. Do not stash-pop over conflicts without checking what changed.
- For fixups, prefer `git commit --fixup=<hash>` followed by `GIT_SEQUENCE_EDITOR=: git rebase -i --autosquash <base>`.
- For conflicts, read the conflicting files and resolve by intent. Do not choose ours/theirs blindly.
- If a rebase goes wrong, use `git rebase --abort` first. Use reflog only after explaining the recovery path.

After rewriting, run the relevant tests or at least the project's cheapest smoke check, then show the new branch log from base to HEAD.

## History Mode

Choose the Git tool by the question:

- `git log -S "text"`: when the count of an exact string changed.
- `git log -G "regex"`: when diffs touched lines matching a pattern.
- `git blame -L start,end -- file`: who last changed specific lines.
- `git log --follow -- file`: history across renames for one file.
- `git show <hash>`: inspect the commit that appears relevant.
- `git bisect`: find the first bad commit when there is a deterministic pass/fail command and known good/bad bounds.
- `git reflog`: recover or explain recent local history movement.

Always cite the exact command evidence in the answer: commit hash, subject, file path, and line or diff context when relevant. If the evidence is ambiguous, say what remains unproven.

## Safety Checks

Before any write to Git history:

- Current branch is known.
- Dirty work is accounted for.
- Upstream/pushed status is known or explicitly unknown.
- The operation matches the user's request.
- Recovery path is known (`rebase --abort`, reflog hash, or untouched worktree).

Before finishing:

- Run the most relevant verification available for the changed behavior or history operation.
- Report commands that passed and any command you could not run.
- Leave the worktree state explicit.
