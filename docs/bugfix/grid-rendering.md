# Grid Rendering Bug 修复记录

> 日期：2026-07-18
> 影响文件：`Scripts/Grid/MapBackground.cs`
> 关联事项：用户报告的 nullable 警告

---

<a id="grid-rendering-bug-1"></a>
## BUG-1：Godot 生命周期字段触发 nullable 初始化警告

### 症状

启用 nullable 引用类型后，`MapBackground.Instance`、Inspector 注入的 `Display`，以及在 `_Ready()` 中获取的 `_shaderMaterial` 无法从声明处证明已初始化或始终非空。

### 根因分析

`Instance` 和 `Display` 分别由 Godot 节点生命周期及场景反序列化赋值，C# 的构造时静态分析无法识别这些保证。`Display.Material as ShaderMaterial` 则确实可能返回 `null`，原字段类型没有表达这一运行时状态。

### 修复方案

为生命周期保证注入的 `Instance` 和 `Display` 添加 `null!` 初始化，保留它们的非空使用契约；将 `_shaderMaterial` 声明为 `ShaderMaterial?`，并继续使用 `_Process()` 中已有的空值检查保护后续访问。未改变场景结构或渲染逻辑。

### 影响范围

修改仅影响 `MapBackground` 的 nullable 类型标注。网格参数、Shader 参数更新、相机读取和显示行为不受影响。

---

## 验证状态

- `dotnet build SimpleCities.sln`：构建成功，0 个警告，0 个错误。
- 未执行场景手工验证；本次修改不改变运行时控制流。
