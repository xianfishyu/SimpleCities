---
name: godot-csharp-qa
description: "MUST USE after changing C# code, Godot scenes/resources, project settings, or runtime behavior in SimpleCities, and whenever the user asks to verify, test, QA, or validate the Godot C# project. Runs the smallest sufficient combination of csharp-ls diagnostics, dotnet build, Godot editor checks, deterministic runtime testing, runtime state inspection, and DAP console review. Do not use for documentation-only or Git-only changes."
---

# Godot C# QA

Use this skill to verify SimpleCities changes against the real C# compiler, Godot editor, and running game. Select the smallest QA tier that covers the changed behavior, but never substitute a lower tier for observable runtime behavior.

## Project Contract

- Workspace root: the directory containing `SimpleCities.sln` and `project.godot`.
- Build command: `dotnet build SimpleCities.sln`.
- Main scene: `Scenes/MapTest.tscn`.
- C# language server: `csharp-ls`, reached through the session's enabled Oh My OpenAgent `lsp-daemon` MCP.
- Editor/runtime bridge: `godot` MCP from `@satelliteoflove/godot-mcp`.
- GDScript diagnostics and running-game console: `godot-minimal` MCP from `@ryanmazzolini/minimal-godot-mcp`.
- Treat the current command output as truth. Do not hard-code a permanent expected warning count; compare diagnostics and build output with the baseline captured at the start of the task.

## Scope Gate

Use this skill for:

- Any change to `*.cs`, `*.csproj`, `*.sln`, `project.godot`, `*.tscn`, `*.tres`, `*.gdshader`, or non-vendored `*.gd` files.
- Changes to gameplay, input, save/load, rendering, scene composition, Godot lifecycle, signals, resources, project settings, or editor plugins.
- Bug fixes whose verification requires Godot or .NET behavior.
- Explicit requests to test, verify, validate, smoke-test, or QA the project.

Do not use it for:

- Documentation-only changes.
- Skill, Agent, Command, or OpenCode configuration changes that do not alter the Godot project; validate those with their own configuration workflow.
- Git-only operations.
- Speculative investigations where no implementation or runnable behavior is being declared complete.

## Classify the QA Tier

Choose the highest applicable tier:

### Tier 1: C# Static and Build

Use for C# changes whose contract is fully exercised by language analysis and compilation, such as DTO shape, pure helpers, or internal refactors with no changed runtime behavior.

Required gates:

1. Focused `csharp-ls` diagnostics on every changed `.cs` file.
2. `dotnet build SimpleCities.sln` with exit code 0.
3. Compare diagnostics and warnings against the pre-change baseline; no new unexplained errors or warnings.

### Tier 2: Godot Editor and Resource Integration

Use when changes affect scenes, resources, exported properties, autoloads, project settings, shaders, plugins, node paths, or Godot lifecycle wiring.

Required gates: all Tier 1 gates that apply, plus:

1. Confirm the Godot editor is connected to the correct project.
2. Reload the edited scene from disk, or restart the editor only when editor-side state can be stale: `project.godot`, autoload/input-map changes, `@tool`/addon/plugin code, or cached shaders.
3. Read editor errors after reload and require zero new task-caused errors.
4. Inspect effective node/resource/project properties instead of trusting `.tscn` or `.tres` text alone.

### Tier 3: Runtime Behavior

Use for gameplay, input, rendering, save/load, timing, physics, scene transitions, runtime state, runtime errors, or any user-visible behavior.

Required gates: Tier 1 and Tier 2 gates that apply, plus an actual running-game scenario:

1. Clear the minimal Godot MCP console buffer before the scenario.
2. Stop any prior test run, then launch the correct scene with game time frozen when deterministic setup is possible.
3. Build the exact preconditions using the running-game execution tool only for test setup; do not bake debug hooks into production code.
4. Drive the real behavior with named actions, raw input, or deterministic game-time steps. Inputs that depend on `just_pressed` must occur inside the stepped time window.
5. Observe the result through structured runtime state, node properties, signals, profiler data, or a screenshot when appearance is the contract.
6. Read both channels after the scenario:
   - Godot editor error log for editor/addon/import failures.
   - Minimal Godot MCP DAP console for running-game prints, errors, warnings, and stack traces.
7. Stop the game and clean all temporary runtime nodes, bots, timers, files, save slots, and test processes created for QA.

## Standard Workflow

### 1. Capture the Baseline

Before final verification, or before modifying behavior when this skill is used inside an already-authorized implementation task:

- Record `git status --short` so unrelated dirty files are not attributed to the task.
- Run focused diagnostics on relevant existing C# files when practical.
- Run `dotnet build SimpleCities.sln` and record exit code, errors, and warnings.
- If a reported runtime bug is being fixed, reproduce it or capture an equivalent failing automated/manual scenario before the repair.

Pre-existing failures remain visible in the report. Do not fix or hide them unless they are in scope.

### 2. Run Focused C# Diagnostics

- Request diagnostics for every changed `.cs` file.
- For cross-file changes, also check the entry point or consumer that exercises the changed API.
- An LSP warning proves the server responded; classify whether it is new, pre-existing, or unrelated.
- Do not use `as any`, suppression comments, nullable-forgiving operators, or project warning disables merely to silence diagnostics. A lifecycle-guaranteed `null!` requires an existing project pattern and a documented runtime guarantee.

### 3. Build the Real Solution

Run from the workspace root:

```powershell
dotnet build SimpleCities.sln
```

Requirements:

- Exit code must be 0.
- Report the actual error and warning counts.
- A successful build is necessary but not sufficient for Tier 2 or Tier 3.
- If build output conflicts with LSP output, report both and investigate the difference; do not choose the more convenient result.

### 4. Validate Godot Editor State

- Verify project name/path, Godot version, current scene, and play state.
- For ordinary gameplay `.cs` edits, stop then run; a launched game loads scripts from disk and does not require editor restart.
- After editing `project.godot`, check for stale editor settings and restart the editor if needed.
- After editing a `.tscn` file directly, reload the open scene from disk before inspecting it.
- Read only new editor errors using the log cursor when available.

### 5. Execute Runtime QA

Define the scenario before running it:

```text
Precondition: exact initial scene/state
Action: exact input, method, or time progression
Observable: exact state, signal, console line, or visual result
Pass: binary expected value or bounded range
Failure: observable that proves the behavior is wrong
```

Prefer cheap structured evidence:

1. Runtime state digest or explicit node properties.
2. Signal/field watch over time.
3. Profiler data for performance contracts.
4. Screenshot only when visual appearance is the requirement.

For procedural 3D rendering that appears black, missing, one-sided, or inexplicably dark without logged errors, run mesh validation before adjusting lighting or materials.

### 6. Inspect the Running-Game Console

- Clear the console before the focused scenario.
- Run the scene before requesting console output; DAP output requires an active debug session.
- Read `stderr` for errors/warnings and `console` for `GD.Print`/`print()` evidence.
- Do not treat an empty console as proof of success unless the behavior contract explicitly expects no output and another state-based assertion passed.

### 7. Cleanup

Always leave the editor and workspace in a known state:

- Stop the running project unless the user explicitly asked to leave it running.
- Remove temporary nodes attached through the runtime execution holder.
- Delete temporary save slots, screenshots, fixtures, or logs created solely for QA, while preserving requested evidence.
- Restore modified test settings and input states.
- Confirm no task-created process, port listener, or temporary file remains.
- Re-run `git status --short` and distinguish intended changes from pre-existing work.

## Failure Handling

- A task is not verified when diagnostics, build, editor checks, runtime assertions, or cleanup fail.
- Fix the root cause and rerun the failed gate plus all dependent gates.
- After two materially different failed runtime attempts, consult a debugging specialist or Oracle before further edits.
- If Godot or an MCP is unavailable, report the blocked gate and what evidence is missing. Do not replace a required runtime test with “should work.”
- Never delete a failing test, weaken an assertion, suppress a diagnostic, or omit an error from the final report to obtain a pass.

## Completion Gate

Declare QA complete only when:

- All gates for the selected tier passed.
- Every changed C# file has focused diagnostics or an explicitly reported tooling limitation.
- `dotnet build SimpleCities.sln` exited 0 for C# or project changes.
- Tier 2 changes were loaded and inspected by the Godot editor.
- Tier 3 behavior was exercised in a running game with a binary observable.
- Editor errors and DAP console output were reviewed where applicable.
- Temporary QA artifacts and processes were cleaned.
- The final report distinguishes passed, failed, blocked, skipped, and pre-existing findings.

## Final Report

Report:

1. Selected QA tier and why.
2. Files and behavior covered.
3. Exact diagnostics/build/runtime commands or MCP operations executed.
4. Actual results, including warning/error counts and runtime observables.
5. Editor-log and DAP-console findings.
6. Cleanup performed and remaining unrelated dirty files.
7. Any blocked or intentionally skipped gate with its impact on confidence.

This skill verifies behavior; it does not authorize implementation, documentation changes, Git commits, or pushes that the user did not request.
