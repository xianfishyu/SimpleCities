# SimpleCities - AI 编码助手指南

## 项目概述
SimpleCities 是一个 Godot 4.5 C# 项目，用于模拟城市铁路系统。从 OpenStreetMap 导出的 JSON 文件加载铁路网络数据，在 2D 画布上显示轨道，并模拟列车沿路线运动。项目集成了 ImGui 进行调试可视化。

## 架构与数据流

### 核心组件
1. **铁路数据管道**：`RailwayParser`（Track/）→ 从 `Railwaydata/` 文件夹反序列化 JSON
   - 解析 GeoJSON 格式的经纬度坐标，转换为本地 2D 空间（单位：米）
   - 数据存储在 `Dictionary<int, RailwayData>` 中，以元素 ID 为键
   - 基准点计算：第一个坐标作为原点，其他坐标相对偏移（约 86414.25 经度单位/米，111194.93 纬度单位/米）

2. **轨道显示**：`TrackManager` 将 `RailwayData` 渲染为 `Line2D` 节点
   - 通过 `highLight` 导出数组过滤轨道；为空则显示所有
   - 线宽固定为 1.435 米（铁轨标准距离），Z 轴索引 = -1
   - 每条轨道命名为 "ID_{id}/Name_{name}" 便于调试

3. **列车仿真**：`TrainBehavior` 沿路径移动列车单元
   - 使用 `PathFollow2D` 节点，通过 `Progress` 属性（沿路径的距离）控制位置
   - 速度转换：km/h ÷ 3.6 = m/s，每帧应用增量
   - 列车由多个车厢组成，通过配对的 `PathFollow2D` 节点定位
   - 路径结束时循环检测阻止进一步移动

4. **相机控制**：`Camera2DController` 实现平移/缩放
   - 键盘：WASD（映射到 project.godot 中的 `KeyBoard_Move*`）
   - 鼠标：中键拖动
   - 缩放：鼠标滚轮，范围 0.125x 到 4x

5. **调试 UI**：`DebugGUI` 使用属性驱动的渲染
   - 通过反射自动发现标记了 `[DebugGUI("分类名")]` 的静态方法
   - 在可收起的 ImGui 分类中渲染
   - 示例：`DebugInfo.RenderInfo()` 显示 FPS，`MemoryInfo()` 显示内存使用

### 数据类型
- **RailwayData**：封装轨道元数据（类型、名称、ID、几何形状为 Vector2 数组、节点 ID、客运线路）
- **GeoJSON 结构**：根 → 元素（ways） → 几何形状（经纬度点）+ 标签（name_zh、name 等）

## 开发模式

### 导出属性
- `[Export]` 标记 Godot 检查器属性；使用 `PropertyHint.None` 加 `suffix:` 显示单位
- 示例：`[Export(PropertyHint.Range, "0.125,4")]` 用于缩放限制

### 反射初始化
- 使用反射 + 自定义属性实现运行时注册（见 `DebugGUIInitializer`）
- 避免硬编码依赖；支持插件式扩展

### C# 中的文件路径
- 使用 `ProjectSettings.GlobalizePath(path)` 将 `res://` 相对路径转为绝对路径
- JSON 数据位置：`Railwaydata/` 文件夹，包含城市特定的文件（BeijingRoundRail.json、Shanghai.json 等）

## 构建与运行
- **框架**：.NET 10.0、C# 14.0（允许不安全代码块）
- **依赖**：ImGui.NET 1.91.6.1、Godot.NET.Sdk 4.5.1
- **主场景**：Menu（project.godot 中的 uid），测试场景在 Scenes/（TimetableTest.tscn、TrackTest.tscn）
- **ImGui 自动加载**：ImGuiRoot.tscn 作为单例加载

## 关键文件
- `Scripts/Track/RailwayParser.cs` - JSON 解析与坐标转换逻辑
- `Scripts/Track/TrackManager.cs` - 轨道渲染与过滤
- `Scripts/Track/TrainBehavior.cs` - 列车物理与运动
- `Scripts/DebugGUI.cs` - 调试 UI 框架
- `project.godot` - 引擎配置、输入映射、自动加载
- `SimpleCities.csproj` - 构建目标、包引用

## 常见任务
- **添加新的铁路网络**：将城市 JSON 放在 `Railwaydata/`，更新 TrackTest.tscn 的 `TrackInfoPath` 属性
- **自定义列车模型**：修改 `train_model` 属性，更新 `Prefabs/Trains/` 中的预制体
- **添加调试功能**：创建静态方法，标记为 `[DebugGUI("功能名")]`，反射发现在 DebugGUI._Ready() 运行
- **调整速度**：编辑 TrainBehavior 中的 `operation_speed`（km/h），会通过属性自动转换为 m/s
