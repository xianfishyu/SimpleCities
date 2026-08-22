# Godot 集成 Bug 修复记录

> 日期：2026-08-14
> 影响文件：`export_presets.cfg`、`tests/SimpleCities.RoadGraph.Tests/ExportPresetContractTests.cs`
> 关联事项：`v3-save-system:2.2` Windows 导出门禁

---

<a id="godot-integration-bug-1"></a>
## BUG-1：Windows QA 导出包泄漏无关测试与锁探针资源

### 症状

`Windows Desktop QA` 使用 `export_filter="all_resources"`，但排除列表只覆盖部分测试目录和 GDScript。RoadGraph 性能项目、独立进程锁探针以及若干相机、renderer lifecycle 等运行时测试资源仍会进入 QA 包；这些文件不是 exported-save 契约的运行依赖，会扩大包内容并让导出验证受到无关测试资源影响。

### 根因分析

QA 预设依赖逐项 `exclude_filter`，新增测试入口后没有任何自动契约比较仓库 `tests/` 文件集合与导出白名单。遗漏的目录或脚本因此默认被 `all_resources` 收入，直到实际检查导出包才暴露。

### 修复方案

扩展 `Windows Desktop QA` 的 `exclude_filter`，排除 `tests/SimpleCities.RoadGraph.Performance/**`、`tests/SimpleCities.SaveLockProbe/**` 以及所有与 exported-save 无关的 Godot 契约和测试场景。新增 `ExportPresetContractTests` 解析目标预设并对当前 `tests/` 树应用 glob，要求导出候选精确等于 `exported_save_runtime_contract` 的脚本/UID/场景和 `v3_save_fixture` 的脚本/UID。

### 影响范围

只影响 `Windows Desktop QA` 的测试资源打包范围；普通 `Windows Desktop` 预设、生产脚本、场景和 exported-save 契约本身不变。锁探针仍由 .NET 测试构建和启动，不再作为 Godot 资源进入 QA 包。

---

## 验证状态

- `ExportPresetContractTests.WindowsDesktopQa_ExportsOnlyTheDedicatedSaveContractResourcesFromTests` 通过，导出候选精确为 5 个 dedicated save contract/fixture 文件。
- 实际 Windows Desktop QA 导出成功；包内测试资源检查只发现 `exported_save_runtime_contract` 与 `v3_save_fixture`。
- 导出包在隔离可写 profile 输出 `PASS exported save writable user data contract`，在临时只读 ACL profile 输出 `PASS exported save read-only ACL contract`；临时 DENY ACE 已移除。
- 保存相关聚焦测试 118/118、完整 `dotnet test SimpleCities.sln --no-restore` 698/698，Debug 与 `ExportRelease` build 均为 0 警告、0 错误。Roslyn compiler/analyzer 为 0 diagnostics，Godot editor error 为 0；全库 GDScript 只有 3 条与本修复无关的既有 warning。

---

<a id="godot-integration-bug-2"></a>
## BUG-2：新增 closed-ribbon 运行时契约被打入 Windows QA 导出包

> 修复日期：2026-08-14
> 影响文件：`export_presets.cfg`
> 关联事项：`v3-save-system:2.2` Windows 导出门禁；延续 `godot-integration:BUG-1` 的资源白名单契约

### 症状

新增 `tests/godot/road_closed_ribbon_runtime_contract.gd` 及其 UID 后，完整自动化中的 QA 导出候选检查失败：`Windows Desktop QA` 原本应只保留 exported-save contract 与共享 V3 fixture，但新 closed-ribbon 脚本因未进入排除列表而成为第六类测试资源。若不修复，下一次 QA 导出会再次携带与存档导出验收无关的运行时道路测试。

### 根因分析

该预设继续使用 `export_filter="all_resources"`，测试资源边界依赖 `exclude_filter`。`godot-integration:BUG-1` 已用 `ExportPresetContractTests` 将当前仓库测试树约束为精确白名单，但新增 GDScript 契约时没有同步更新排除 glob；自动契约因此按设计捕获了资源集合漂移。

### 修复方案

在 `Windows Desktop QA` 的 `exclude_filter` 中加入 `tests/godot/road_closed_ribbon_runtime_contract.gd*`，一次覆盖脚本及其 `.uid`。exported-save runtime contract、对应 UID/场景和 `v3_save_fixture` 脚本/UID 仍是 QA 包中唯一允许的测试资源。

### 影响范围

只影响 `Windows Desktop QA` 的测试资源筛选。普通 `Windows Desktop` 预设、closed-ribbon 契约的本地执行、生产资源和 exported-save contract/fixture 内容不变。

## BUG-2 验证状态

- `ExportPresetContractTests.WindowsDesktopQa_ExportsOnlyTheDedicatedSaveContractResourcesFromTests` 通过，候选集合重新精确收敛为 5 个 exported-save contract/fixture 文件。
- `dotnet test SimpleCities.sln --no-restore`：727/727 通过；Debug 与 `ExportRelease` build 均为 0 警告、0 错误；Roslyn compiler/analyzer 为 0 diagnostics。
- `road_closed_ribbon_runtime_contract.gd` 仍可在本地独立运行并输出 `PASS`；本条没有把 `ExportRelease` build 等同于重新生成并执行 Windows QA 导出包。

---

<a id="godot-integration-bug-3"></a>
## BUG-3：道路 Load 故障契约在打印 PASS 后于托管终结阶段崩溃

> 修复日期：2026-08-23
> 影响文件：`tests/godot/RoadLoadPreflightResourceFailureProbe.cs`、`tests/godot/road_load_preflight_resource_failure_runtime_contract.gd`
> 关联事项：第三代道路系统 Phase 7 真实 `Load` 故障边界契约

### 症状

`road_load_preflight_resource_failure_runtime_contract.gd` 已完成全部断言并打印 `PASS road load preflight resource failure runtime contract`，但 Godot CLI 随后在退出阶段以 `0xC0000005` 结束；堆栈落在 `Godot.Collections.Array.Finalize()`。契约内容已经通过，但进程退出码仍把正式运行判为失败。

### 根因分析

契约清理原先只把 `RoadLoadPreflightResourceFailureProbe` 引用设为 `null`，没有在 Godot 托管绑定仍有效时等待其持有的 Godot 集合包装器完成终结。相关终结器因而可能延迟到引擎退出、原生绑定开始拆卸之后运行，`Godot.Collections.Array.Finalize()` 再访问已经失效的原生状态并触发访问冲突。

### 修复方案

在测试专用 probe 中增加 `FlushPendingManagedFinalizers()`，依次执行 `GC.Collect()`、`GC.WaitForPendingFinalizers()` 和第二次 `GC.Collect()`。GDScript 清理流程先删除测试槽、释放测试场景并等待一帧，再调用该入口，最后才释放 probe 引用并执行 `quit()`，从而把托管终结阶段约束在 Godot 互操作运行时仍有效的窗口内。该入口只存在于测试 partial，不改变生产 `ExportRelease` 行为。

### 影响范围

只影响道路 Load 故障运行时契约的 CLI 清理顺序；生产存档流程、RoadGraph、renderer、工具状态和正式导出程序集不变。故障契约仍会在成功与失败退出路径中删除临时槽和测试场景。

## BUG-3 验证状态

- 正式 Vulkan 1.4.341 Forward+ 运行 `road_load_preflight_resource_failure_runtime_contract.gd`：打印 `PASS`、退出码为 0，minimal stderr/console 均为空，未再出现 `Godot.Collections.Array.Finalize()` 访问冲突。
- Debug 与 `ExportRelease` build 均为 0 警告、0 错误；Debug 反射确认 `FlushPendingManagedFinalizers()` 存在，`ExportRelease` 中测试 probe 类型不存在。
- `RoadRendererLifecycleContractTests` 48/48、生命周期与 QA export 聚焦测试 49/49、完整自动化 906/906 通过；Roslyn production/test compiler/analyzer 与目标 GDScript 均为 0 diagnostics。
- Godot editor 游标 1989 后无新增 error；唯一输出 warning 是与本修复无关的既有 `ConstructionDock: ToolManager.Instance is missing`。
