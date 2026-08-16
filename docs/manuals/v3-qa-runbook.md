# 第三代道路系统 QA 运行手册

> 整理日期：2026-08-16
> 用途：给出 V3 变更的最小充分验证步骤，按 Tier 1～3 执行。

## Tier 1：C# 静态与构建

1. `dotnet restore SimpleCities.sln`
2. `dotnet build SimpleCities.sln`
3. `dotnet test tests/SimpleCities.RoadGraph.Tests/SimpleCities.RoadGraph.Tests.csproj`

预期：
- 构建 0 警告 / 0 错误；
- 测试全部通过（当前基线 1254+）。

## Tier 2：Godot 编辑器与资源集成

1. 打开 `Scenes/MapTest.tscn` 与 `Scenes/UI/GameHUD.tscn`。
2. 修改 `.tscn` / `.tres` 后执行 `godot_scene reload`。
3. 检查编辑器日志无新增错误。
4. 对关键资源使用 `godot_resource get_info` 验证实际加载结果。

## Tier 3：运行时行为

1. 运行 `MapTest`（可 frozen）。
2. 使用 `godot_game_time` 步进，注入键盘/手柄/鼠标输入。
3. 使用 `godot_exec` 读取节点状态（如 RoadType 按钮、DebugPanel 文本、ToolManager.CurrentTool）。
4. 运行 headless 契约：

```powershell
godot --headless --path . --script tests/godot/command_center_runtime_contract.gd
```

预期：输出 `PASS command center runtime contract`。

## M4 异步存档 UI 专项验证

前置：环境已恢复，`MapTest` 可运行，V3 后端通过 `GameHUD.ConfigureV3Backend` 注入。

1. 单元测试：
   - `V3SaveOperationUiStateTests`
   - `V3SaveOperationControllerTests`
   - `V3SaveOperationUiCoordinatorTests`
   - `V3AsyncSaveOperationCoordinatorTests`
   - `V3SaveOperationBackendTests`
   - `V3SaveSlotUiSummaryTests`
   - `PauseMenuContractTests`
2. 打开 PauseMenu 的存档管理，验证：
   - V3 槽列表只显示 `CompleteV3` / `CorruptV3`；`Foreign` / `Unsafe` 不出现。
   - 另存为成功后列表刷新并显示新槽；覆盖/加载/删除成功与失败均有明确状态文案。
   - 操作进行中按钮禁用且显示“正在…”；Escape 只请求取消，不关闭菜单。
   - `CorruptV3` 槽只能删除，不能加载/覆盖。
3. 检查 `CurrentSlotID` 只在成功 commit 后更新；失败/取消不切换当前槽。
4. 场景退出重入后旧 operation token 不生效（`ConfigureV3Backend` 递增 scene generation）。

预期：上述单元测试全部通过；手工场景无重复提交、无提前关闭菜单、无 V2/Foreign 槽进入普通操作列表。

## M5 最终组合验收专项

前置：M1～M4 全部通过，环境可运行 `MapTest` 与 Windows 导出。

1. 真实 `MapTest` 场景完成：
   - 连续折线、环路、四类型建造/改造、撤销重做、保存/加载、表面命中、混合 junction。
2. 性能门禁：
   - 10k 硬门槛：构建/查询/保存/加载在预算内完成。
   - 100k 压测：记录耗时与峰值内存，确认无全图扫描退化。
3. 视觉与导出：
   - Vulkan 渲染下检查 Ribbon/Cap/JunctionPatch 外观。
   - Windows 导出后运行同一验收场景。
4. 证据写回 `docs/manuals/road-system-v3-gen.md` 附录 D。

预期：`v3-road-graph:8.6` 联合 `v3-grid-rendering:2.2`、`v3-tool-input:2.4`、`v3-ui:1.4`、`v3-save-system:2.3` 的端到端证据齐全。

## 清理

- 停止运行中的项目；
- 删除测试产生的临时槽、日志和运行时节点；
- 恢复被编辑器改写的无关 `.tscn` / `.tres`。

## 验收门禁

- `dotnet build SimpleCities.sln` 退出码 0，0 警告 / 0 错误。
- `dotnet test` 全部通过，无跳过/失败。
- `command_center_runtime_contract.gd` 输出 `PASS command center runtime contract`。
- Godot 编辑器日志无新增错误；运行中 DAP stderr 为空。
- 运行时状态（RoadType 按钮、DebugPanel 文本、ToolManager.CurrentTool）与预期一致。

## 相关文档

- 架构与验收规范：`docs/manuals/road-system-v3-gen.md`
- 当前实现与验证状态：`docs/manuals/v3-current-implementation.md`
- 下一步执行计划：`docs/manuals/v3-next-steps.md`
- 关键决策记录：`docs/manuals/v3-decisions.md`
- 术语表：`docs/manuals/v3-glossary.md`
- 代码地图：`docs/manuals/v3-code-map.md`

## 验收证据记录模板

| 模块 | 命令/场景 | 实际结果 | 通过 |
| --- | --- | --- | --- |
| 构建 | `dotnet build SimpleCities.sln` | 0 警告 / 0 错误 |  |
| 测试 | `dotnet test ...` | 全部通过 |  |
| 契约 | `command_center_runtime_contract.gd` | `PASS ...` |  |
| 运行时 | `MapTest` RoadType/Upgrade/DebugPanel | 状态符合预期 |  |
| M4 UI | PauseMenu V3 另存/覆盖/加载/删除/取消 | busy 禁用、Escape 取消、V2/Foreign 不可见 |  |
| M5 组合 | `MapTest` 10k/100k/Vulkan/Windows 导出 | 全部通过，证据写入附录 D |  |
