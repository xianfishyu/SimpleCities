# 存档系统 Bug 修复记录

> 日期：2026-06-06
> 影响文件：`Scripts/Road/RoadGraph.cs`
> 关联重构：road-system-v2-gen（阶段 A+B）

---

<a id="save-system-bug-1"></a>
## BUG-1：道路类型未写入存档，加载后统一退化为 Street

关联文档：`road-graph:BUG-8`

### 症状

使用 Dirt、Arterial 或 Highway 等非默认类型建设道路并保存后，再次加载存档，道路组和边的类型都会变成 `RoadType.Street`。道路几何与拓扑仍能恢复，因此问题容易表现为加载后道路分级样式或后续分级逻辑静默丢失。

### 根因分析

`RoadGraph.CaptureState()` 原先只记录边和道路组的 ID、连接关系、几何点与长度，`SegmentData` 和 `RoadData` 中没有道路类型字段。`RestoreFromSavedData()` 重建 `RoadGroup` 和 `GraphEdge` 时只能硬编码使用 `RoadType.Street`，导致非默认类型无法完成存档往返。

### 修复方案

在存档 DTO 中为边和道路组增加可空的 `Type` 字段，并在捕获状态时分别写入 `edge.Type` 与 `group.Type`：

```csharp
public class SegmentData
{
    [JsonPropertyName("type")]
    public int? Type { get; set; }
}

public class RoadData
{
    [JsonPropertyName("type")]
    public int? Type { get; set; }
}
```

恢复时优先使用边自身保存的类型，其次使用所属道路组的类型；旧存档没有 `type` 字段时，可空值保持为 `null`，最终兼容性回退到 `RoadType.Street`：

```csharp
RoadType edgeType;
if (edgeData.Type.HasValue)
    edgeType = (RoadType)edgeData.Type.Value;
else if (_groups.TryGetValue(edgeData.RoadID, out var existingGroup))
    edgeType = existingGroup.Type;
else
    edgeType = RoadType.Street;
```

### 影响范围

影响 `RoadGraph` 的存档捕获与恢复，以及 `SegmentData`、`RoadData` 的 JSON 结构。新存档能够保留道路类型；缺少 `type` 字段的旧存档仍按 Street 加载，不需要迁移旧文件。

---

## 验证状态

### BUG-1

- 关联提交：`6ec0a66`（`修复：保持道路类型存档并避免重复铺路副作用`）
- `dotnet build SimpleCities.sln`：构建成功，0 个错误，4 个既有的 `Scripts/Grid/MapBackground.cs` nullable 警告
- 已核对当前代码路径：存档捕获与恢复均处理 `RoadType`，且完整覆盖检查位于 `ResolveIntersections`、`SplitEdgesAtPathAnchors` 等变更操作之前
- 当前仓库未发现覆盖上述两个场景的自动化测试；本次未执行 Godot 运行时存档往返或重复铺路手工测试，因此不声明运行时回归验证已完成

### BUG-2

- `dotnet build SimpleCities.sln --no-restore`：成功，0 个警告，0 个错误。
- `dotnet test SimpleCities.sln --no-build --no-restore`：34 个测试全部通过，无失败或跳过。
- `godot --headless --path . --log-file .godot/qa-pause-menu-final.log --script tests/godot/pause_menu_runtime_contract.gd`：输出 `PASS pause menu runtime contract`；`MapTest -> MainMenu -> MapTest` 中注册数依次验证为 2、0、2，新场景随后保存和加载均成功。
- `godot --headless --path . --log-file .godot/qa-command-center-final.log --script tests/godot/command_center_runtime_contract.gd`：输出 `PASS command center runtime contract`，既有 HUD、输入、道路建造和同实例重进契约保持通过。
- Godot 编辑器桥接确认项目路径为当前仓库、Godot 4.7、主场景为 `MapTest`，`project.godot` 无磁盘/编辑器差异；磁盘版 `PauseMenu.tscn` 的四个视图已由编辑器有效场景树读取。
- 当前会话未暴露 `csharp-ls`，因此没有逐文件 C# LSP 诊断；真实编译、完整 .NET 测试和 Godot 运行时契约均已通过。Headless 输出中的 Windows 根证书读取错误和缺失依赖降级警告与本修复无关，契约仍以 0 退出并输出 PASS。既有编辑器运行会话持续报告 `Remote debugger: Packet too large`，导致 live runtime 查询超时；minimal DAP `stderr` 缓冲为空，因此没有把该连接故障当作本修复的运行时通过证据。

---

<a id="save-system-bug-2"></a>
## BUG-2：返回主菜单后旧场景 saveable 仍留在全局注册表

关联事项：`save-system:0.8`

### 症状

`SaveManager` 是跨场景存活的 Autoload，而 `RoadGraph` 和 `MainCamera` 随 `MapTest` 创建。结束当前城市返回 `MainMenu` 后，如果再次进入 `MapTest`，旧场景对象仍可能留在 `_saveables`；新场景再次注册 `road_network` 和 `camera` 时会形成同名活动对象，后续保存可能重复写同一文件，加载也可能调用已经退出场景的对象。

### 根因分析

原有注册表只有 `Register(ISaveable)`，没有与 Godot 场景退出对应的注销入口。`RoadSystem._Ready()` 每次进入场景都会创建新的 `RoadGraph`，`MainCamera` 也会生成新的场景实例，但两者退出树时都没有从 Autoload 注册表移除。由于对象引用不同，原有的“同一引用不重复添加”判断无法保护跨场景重进。

### 修复方案

`SaveManager` 增加 `Unregister(ISaveable)` 和 `RegisteredSaveableCount`。`Register` 对同一实例保持幂等，同时按 `SaveFileName` 拒绝第二个活动实例并返回 `false`，把重复文件所有权从静默冲突改为明确错误。

`RoadSystem._ExitTree()` 注销其当前 `Graph`，`MainCamera._ExitTree()` 注销自身；两者同时只在静态 `Instance` 指向当前对象时清理单例。场景重进相关的 `ToolManager` 与 `MapBackground` 也按相同所有权规则清理各自单例，但不参与 `_saveables`。

### 影响范围

修复影响 `SaveManager` 的活动注册生命周期，以及 `RoadSystem`、`MainCamera` 的场景退出行为。保存格式、manifest、槽名、RoadGraph JSON 和现有 `autosave` 内容均未改变。重复 `SaveFileName` 现在会拒绝后注册者，而不是允许两个对象竞争同一文件。
