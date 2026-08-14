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
