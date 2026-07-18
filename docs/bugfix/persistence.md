# Persistence Bug 修复记录

> 日期：2026-06-06
> 影响文件：`Scripts/Road/RoadGraph.cs`
> 关联重构：road-system-v2-gen（阶段 A+B）

---

<a id="persistence-bug-1"></a>
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

- 关联提交：`6ec0a66`（`修复：保持道路类型存档并避免重复铺路副作用`）
- `dotnet build SimpleCities.sln`：构建成功，0 个错误，4 个既有的 `Scripts/Grid/MapBackground.cs` nullable 警告
- 已核对当前代码路径：存档捕获与恢复均处理 `RoadType`，且完整覆盖检查位于 `ResolveIntersections`、`SplitEdgesAtPathAnchors` 等变更操作之前
- 当前仓库未发现覆盖上述两个场景的自动化测试；本次未执行 Godot 运行时存档往返或重复铺路手工测试，因此不声明运行时回归验证已完成
