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

## 验收证据记录模板

| 模块 | 命令/场景 | 实际结果 | 通过 |
| --- | --- | --- | --- |
| 构建 | `dotnet build SimpleCities.sln` | 0 警告 / 0 错误 |  |
| 测试 | `dotnet test ...` | 全部通过 |  |
| 契约 | `command_center_runtime_contract.gd` | `PASS ...` |  |
| 运行时 | `MapTest` RoadType/Upgrade/DebugPanel | 状态符合预期 |  |
