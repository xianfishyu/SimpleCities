# SimpleCities - AI 编码助手指南

## 项目概述

Godot 4.6 C# 项目，集成了 ImGui 调试 UI。**当前处于脚手架阶段，仅实现了相机控制和 ImGui 集成。**

## 构建与运行

- **引擎**：Godot 4.6
- **框架**：.NET 10.0、C# 14.0、`AllowUnsafeBlocks: true`
- **依赖**：ImGui.NET 1.91.6.1、Godot.NET.Sdk 4.6.1
- **主场景**：`Scenes/MapTest.tscn`（uid: `uid://ddksnh3bvem1q`）
- **ImGui 自动加载**：`ImGuiRoot.tscn`（uid: `uid://dugmpnsxaagba`）
- **⚠️ 已知问题**：`project.godot` 中 `DebugGUI` 自动加载指向不存在的 `*res://Scripts/DebugGUI.cs`，在创建该文件前引擎启动会报错

## 项目结构

```
Scripts/              ← 所有 C# 源码
Scenes/               ← .tscn 场景文件
Textures/             ← 图片资源
addons/imgui-godot/   ← ImGui 插件（v6.3.2），不要手动修改
```

## 现有代码

### `Scripts/MainCamera.cs` — 2D 相机控制

唯一的 C# 源码文件。`Camera2D` 子类，提供：
- **键盘平移**：WASD（`KeyBoard_MoveUp/Left/Down/Right` 输入映射）
- **鼠标拖动**：中键拖拽平移
- **缩放**：鼠标滚轮，范围 0.125× ~ 4×
- **单例**：`public static MainCamera Instance { get; }` 供全局访问
- **导出属性**：`defaultScale`、`scaleFactor`、`minScale`、`maxScale`、`keyMoveFactor`、`moveSpeed`
- **平滑过渡**：所有变换使用 `Mathf.Lerp` 逐帧插值

### `Scenes/MapTest.tscn` — 当前主场景

- `Node2D` 根节点
- 子节点：`Camera2D`（挂载 `MainCamera.cs`）、`Sprite2D`（背景图 `Textures/31245427_p0.jpg`，缩放 0.5）

## ImGui 集成

ImGui 通过 `addons/imgui-godot/` 插件提供，自动加载为单例。核心 API：

- `ImGuiGD.ImGuiBegin(string title)` / `ImGuiGD.ImGuiEnd()` — 窗口包裹
- `ImGuiGD.ImGuiText(string text)` — 文本显示
- `ImGuiGD.ImGuiButton(string label)` — 按钮（返回 bool）
- `ImGuiGD.ImGuiSliderFloat(...)` — 滑块控件
- 详细 API 见 `addons/imgui-godot/ImGuiGodot/ImGuiGD.cs`

使用模式：在 `_Process` 中调用 ImGui API，每帧渲染。示例：
```csharp
public override void _Process(double delta)
{
    ImGuiGD.ImGuiBegin("调试面板");
    ImGuiGD.ImGuiText($"FPS: {Engine.GetFramesPerSecond()}");
    ImGuiGD.ImGuiEnd();
}
```

## Godot C# 编码约定

- **类声明**：`public partial class MyClass : Node2D`（必须 `partial`）
- **导出属性**：`[Export] private int _myField;`（下划线前缀私有字段）
- **生命周期**：`_Ready()` → `_Process(double delta)` → `_Input(InputEvent @event)`
- **文件路径**：使用 `ProjectSettings.GlobalizePath("res://...")` 将资源路径转为绝对路径
- **场景实例化**：`GD.Load<PackedScene>("res://Scenes/MyScene.tscn").Instantiate<MyNode>()`
- **输入**：`Input.GetVector("KeyBoard_MoveLeft", "KeyBoard_MoveRight", "KeyBoard_MoveUp", "KeyBoard_MoveDown")`

## 常见任务

- **添加新 C# 脚本**：创建 `public partial class`，如需挂载到节点则继承对应 Godot 类型
- **添加新场景**：在 Godot 编辑器中创建 `.tscn`，或通过 `[GlobalClass]` 注册 C# 类后在编辑器中可用
- **添加 ImGui 调试面板**：在 `_Process` 中调用 ImGui API 即可
