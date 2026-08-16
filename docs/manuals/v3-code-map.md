# 第三代道路系统代码地图

> 整理日期：2026-08-16
> 用途：快速定位 V3 核心源码与测试文件。

## 1. 领域核心 `Scripts/Road/V3/`

- `RoadGraphV3*.cs`：不可变 revision、facade、controller、system 与事务摘要。
- `RoadTool*.cs`：工具状态、输入路由、命令执行器与会话。
- `RoadRibbonBuilder.cs` / `RoadCapBuilder.cs` / `RoadSemanticJoinBuilder.cs`：道路表面网格数据构建。
- `RoadSurfaceSnapshot*.cs` / `RoadSurfaceHit*.cs`：表面快照与命中查询。
- `RoadGraphV3Renderer.cs` / `RoadGraphV3InputHandler.cs`：场景渲染与输入处理。

## 2. 存档与协调 `Scripts/Core/V3/`

- `V3Manifest*.cs` / `V3Slot*.cs`：manifest、槽 ID、路径、occupant、digest。
- `V3RoadSaveLoadCoordinator.cs` / `V3RoadLoadPipeline.cs`：保存/加载协调与 prepared aggregate。
- `RoadGraphV3Application.cs`：V3 应用门面，聚合 controller、presentation、tool、hit provider 与保存协调。

## 3. UI `Scripts/UI/`

- `ConstructionDock.cs`：底部建造栏与工具按钮。
- `ToolContextPanel.cs`：工具说明与 RoadType 选择器。
- `DebugPanel.cs`：V3 诊断指标显示。
- `GameHUD.cs`：HUD 协调、输入路由与依赖注入。

## 4. 测试

- `tests/SimpleCities.RoadGraph.Tests/V3/`：RoadGraph、存档、渲染、工具相关 xUnit 测试。
- `tests/godot/command_center_runtime_contract.gd`：命令中心运行时契约。
