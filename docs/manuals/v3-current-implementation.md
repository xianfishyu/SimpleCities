# 第三代道路系统当前实现与验证状态

> 整理日期：2026-08-16
> 用途：记录 V3 重构已落地的模块、验证证据和仍开放的工作项，供跨会话继续推进时快速对齐。
> 权威工作项：`docs/todo/v3/` 下各系统路线图；本文只做汇总，不替代路线图。

## 1. 已落地模块

### 1.1 RoadGraph（`v3-road-graph`）

- V3 canonical Node/Edge、self-loop A/B incidence、parallel Edge、RoadType 已进入 `RoadGraphV3Revision` / `RoadGraphV3Facade` / `RoadGraphV3Controller`。
- 不可变 root 骨架、统一 `GraphChanged`、`RoadGraphV3ChangeSummary`、`GraphStateToken`、`RoadGraphV3Delta` 已建立。
- `RoadGraphV3Application` / `RoadGraphV3System` 提供建造、改造、拆除、撤销/重做、保存/加载、诊断、表面命中、cap/semantic join 构建等入口。
- `RoadGraphV3Diagnostics` 随事务维护 Node/Edge/Geometry/SelfLoop/Parallel/ChangeSequence。

### 1.2 存档（`v3-save-system`）

- V3 保存根 `user://saves-v3`、V3 format v1、manifest v1、槽 ID/路径/occupant/digest 已落地。
- `V3RoadSaveLoadCoordinator`、`V3SlotAutosaveCoordinator`、`V3SlotTransactionCoordinator`、`V3RoadLoadPipeline` 已支持 aggregate Load commit，并可传入 tool/presentation/renderer inside-commit 参与者。

### 1.3 渲染（`v3-grid-rendering`）

- `RoadTypeStyle` / `RoadTypeStyleCatalog` / `RoadConfigV3` 默认四类样式已落地。
- `RoadRibbonBuilder`、`RoadCapBuilder`、`RoadSemanticJoinBuilder`、`RoadSurfaceSnapshotBuilder`、`RoadSurfaceHitTester` / `HitProvider` 已建立。
- `RoadGraphV3Renderer` 使用 ribbon 填充预览并闭合 self-loop seam。

### 1.4 工具输入（`v3-tool-input`）

- `RoadPlacementSessionV3`、`RoadUpgradeSessionV3`、`RoadRemovalSessionV3`、`RoadToolState`、`RoadToolInputRouter`、`RoadToolCommandExecutor`、`RoadGraphV3InputHandler` 已落地。
- 支持闭环草稿、类型化建造、改造/拆除选择、矩形批量选择、P/R/U 工具切换。
- 切换 RoadType 会取消未提交铺路并同步改造目标。

### 1.5 UI（`v3-ui`）

- `v3-ui:1.1` 已完成：RoadType 选择器、分类联动、键盘/手柄焦点、暂停返回、场景重入、三档视口。
- RoadUpgrade 已具备道路分类入口、`ToolManager` 同步与 U 快捷键。
- DebugPanel 已接入 V3 诊断并实现隐藏零轮询。

## 2. 验证证据

- C# 测试套件最近一次完整通过：1254/1254（环境恢复后需重新运行确认）。
- `dotnet build SimpleCities.sln` 最近一次通过：0 警告 / 0 错误（环境恢复后需重新运行确认）。
- `command_center_runtime_contract.gd` 最近一次输出：`PASS command center runtime contract`。
- Godot `MapTest` 冻结运行验证：RoadType 行可见、四按钮、默认 Street、键盘 Enter 与手柄 A 可切换、分类切换显隐、暂停返回与场景重入保持。

## 3. 模块验证状态矩阵

| 模块 | 实现状态 | 最近验证 | 仍开放 |
| --- | --- | --- | --- |
| RoadGraph 核心 | 已落地 | 1254 测试套件 | 结构共享、query fragment、性能门禁 |
| 存档 V3 | 已落地 | 1254 测试套件 | 真实场景装配、端到端 Load |
| 渲染 V3 | 已落地 | 1254 测试套件 | junction patch、真实 mesh、表现接管 |
| 工具输入 V3 | 已落地 | 1254 测试套件 | 真实 surface hit、失效 token、端到端工具 |
| UI 1.1 | 已完成 | 运行时 + 契约 PASS | 无 |
| UI 1.2 | 部分实现 | 运行时入口/快捷键 | surface hit 选择、完整改造呈现 |
| UI 1.3 | 部分实现 | 运行时 + 契约 PASS | query fragment 指标 |
| UI 1.4 | 未完成 | 无 | 异步存档状态机 |

## 4. 环境恢复后的验证步骤

1. `dotnet restore SimpleCities.sln`
2. `dotnet build SimpleCities.sln`
3. `dotnet test tests/SimpleCities.RoadGraph.Tests/SimpleCities.RoadGraph.Tests.csproj`
4. `godot --headless --path . --script tests/godot/command_center_runtime_contract.gd`
5. `godot --editor --path .` 后运行 `MapTest`，检查 RoadType 选择器、RoadUpgrade 快捷键与 DebugPanel 指标

## 5. 最近更新记录

- 2026-08-16：新增本文档、`v3-next-steps.md`，并在 `docs/README.md` / `docs/todo/v3/README.md` 加入链接。
- 2026-08-16：`docs/todo/v3/` 各系统文档补充当前落地摘要。
- 2026-08-16：`docs/manuals/road-system-v3-gen.md` Phase 7 补充当前落地状态。

## 6. 仍开放的工作项

- `v3-road-graph:8.0`～`8.5` 的完整结构共享、query fragment 接入与性能门禁。
- `v3-save-system:2.3` 的真实 Godot 场景装配与端到端 aggregate Load。
- `v3-grid-rendering:2.2`～`2.3` 的 junction patch、真实 mesh 资源与表现接管。
- `v3-tool-input:2.2` 的真实 surface hit 选择、失效 token 与端到端工具验收。
- `v3-ui:1.2`～`1.4` 的完整改造呈现、异步存档状态机。
- `v3-road-graph:8.6` 最终组合验收（Vulkan、10k/100k、Windows 导出）。

## 7. 关键源码索引

- RoadGraph：`Scripts/Road/V3/RoadGraphV3*.cs`、`RoadGraphV3Application.cs`、`RoadGraphV3System.cs`
- 存档：`Scripts/Core/V3/*.cs`
- 渲染：`Scripts/Road/V3/RoadRibbonBuilder.cs`、`RoadCapBuilder.cs`、`RoadSemanticJoinBuilder.cs`、`RoadGraphV3Renderer.cs`
- 工具：`Scripts/Road/V3/RoadTool*.cs`、`RoadToolInputRouter.cs`、`RoadGraphV3InputHandler.cs`
- UI：`Scripts/UI/ToolContextPanel.cs`、`GameHUD.cs`、`ConstructionDock.cs`、`DebugPanel.cs`

## 8. 测试覆盖索引

- RoadGraph：`tests/SimpleCities.RoadGraph.Tests/V3/RoadGraphV3*Tests.cs`、`RoadGraphV3ApplicationTests.cs`
- 存档：`tests/SimpleCities.RoadGraph.Tests/V3/V3*Tests.cs`
- 渲染：`tests/SimpleCities.RoadGraph.Tests/V3/RoadRibbonBuilderTests.cs`、`RoadCapBuilderTests.cs`、`RoadSemanticJoinBuilderTests.cs`、`RoadSurfaceHitTesterTests.cs`
- 工具：`tests/SimpleCities.RoadGraph.Tests/V3/RoadToolInputRouterTests.cs`、`RoadToolCommandExecutorTests.cs`、`RoadPlacementSessionV3Tests.cs`
- UI 契约：`tests/godot/command_center_runtime_contract.gd`

## 9. 架构组件关系

- `RoadGraphV3System`（场景根）→ `RoadGraphV3Application` → `Controller` / `Facade` / `Revision` → `HitProvider` / `Presentation` / `ToolState` / `Diagnostics`
- 存档：`V3RoadSaveLoadCoordinator` → `V3RoadLoadPipeline` → inside-commit 参与者（tool / presentation / renderer）
- 渲染：`RoadGraphV3Renderer` → `RoadRibbonBuilder` / `RoadCapBuilder` / `RoadSemanticJoinBuilder` → `RoadSurfaceSnapshot` / `RoadSurfaceHitProvider`
- UI：`ConstructionDock` / `ToolContextPanel` / `DebugPanel` → `ToolManager` / `InputBindingManager` → V3 `ToolState`
