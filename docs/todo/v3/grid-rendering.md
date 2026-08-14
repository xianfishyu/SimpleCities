# 第三代网格渲染系统待办清单

> 系统 key：`v3-grid-rendering`
> 整理日期：2026-08-14
> 证据：当前工作区 `Scripts/Road/RoadGeometryDisplaySampler.cs`、`RoadRenderer.cs`、`RoadBuilder.cs`、`RoadConfig.cs`，V2 显示与性能契约，`docs/performance/road-rendering-v2-baseline.md`、`docs/manuals/road-system-v2-gen.md` 附录 D 及 `docs/manuals/road-system-v3-gen.md`。
> 主导原则：负责第三代 canonical Edge、self-loop、平行 Edge 和 RoadType 的确定性可视化，生成与实际 mesh 同源的道路表面命中，并为普通 mutation 与 Load 提供各自正确的表现接管协议；视觉样式和派生表面不是 RoadGraph 的事实来源。

## 状态总览

| ID | 发现 | 当前状态 | 处置方式 |
|---|---|---|---|
| 2.0 | V2 renderer 不理解 self-loop incidence 与固定 loop seam | 开放（部分实现） | closed ribbon、复杂环路与 seam 重定位已验证；继续覆盖缩放/重建和独立平行 Edge 命中/高亮 |
| 2.1 | `RoadConfig` 没有完整且可验证的四类 RoadType 样式 | 已完成 | 四类 `RoadTypeStyle`、唯一覆盖、严格查询和运行时校验已验证 |
| 2.2 | 单一全局样式的开放 ribbon 不能形成可命中的混合宽度完整路面 | 开放（部分实现） | 分级批次、六分量 token、同代 `EdgeRibbon` + `TerminalCap` surface、canonical `RoadLocation` 与基础空间索引已建立；继续实现 semantic join、junction patch 与 stalled/retry |
| 2.3 | 混合类型、full reset 和批量改造没有 V3 性能与视觉门禁 | 开放 | 建立 10k 硬门槛、离散延迟指标、token 接管验证和 100k 压测 |

### 设计覆盖矩阵

| 设计范围 | 当前事实 | 关联待办 |
|---|---|---|
| canonical Edge、self-loop 与平行 Edge | 普通与 Load mesh 已按 `NodeA == NodeB` 生成 closed ribbon，并以 A/B incidence 隐藏纯 loop seam；两路口环、八字形和支路删除后 seam 重定位已有回归，缩放/重建视觉矩阵及基于 surface owner 的平行 Edge 独立命中仍未完成 | 2.0、`v3-road-graph:8.1`～`8.3` |
| RoadType 与完整道路表面 | 普通 rebuild 与 Load preparer 已从同一不可变 `RoadTypeStyleSnapshot` 为每条 Edge 写入宽度和 vertex color，并从真实 mesh triangle 同步生成 `EdgeRibbon`；degree-1 Node 另生成同样式解析 `TerminalCap` disc。两类 primitive 共用不可变 AABB 索引并返回 canonical `RoadLocation`，混合宽度 semantic boundary 与 junction patch 仍未实现 | 2.1～2.2、`v3-road-graph:8.4`～`8.5` |
| 表现事务与 Load 原子接管 | 普通 mutation 已在完整 mesh/node batch/`EdgeRibbon` + `TerminalCap` surface/index 交换后推进 matching presented token；Load worker 预建 `RoadSurfaceSnapshot.PreparedData`，Preflight 创建 `ArrayMesh`/`MultiMesh` 并只绑定 reserved token，aggregate commit 再交换 snapshot 与 matching token。普通 mutation 尚无 stalled/retry | 2.2、`v3-save-system:2.3`、`v3-tool-input:2.4` |
| V2 显示与规模基线 | 六类原生几何已有统一只读显示采样；统一样式的 10k Edge 已通过 60 FPS 门槛并记录 100k 压测 | `grid-rendering:1.1`～`1.2`（V2 已完成）、2.3 |

## 执行顺序

### 阶段 2：第三代道路表面、分级表现与接管门禁

<a id="v3-grid-rendering2.0"></a>

- [ ] **2.0 正确渲染 canonical Edge、self-loop 与平行 Edge**
  - 当前问题：基础 closed ribbon、纯 seam 标记、两路口环、八字形和支路删除后的 seam 重定位已实现并验证，但尚未完成缩放/重建视觉矩阵及平行 Edge 独立 surface 命中/高亮，不能仅凭当前统一样式 mesh 关闭本项。
  - 修改：消费 `EdgeIncidence.Endpoint`；self-loop 使用循环相邻方向生成无裂缝 closed ribbon，seam 不绘制 endpoint/junction 标记；degree 1 绘制 endpoint，degree 大于等于 3 的最终表面由 2.2 junction patch 负责，degree 2 的 seam/semantic boundary 不画伪节点。高亮、矩形选择和中心线命中不得按端点对去重平行 Edge。
  - 依赖：`v3-road-graph:8.1`～`8.3`。
  - 集成负责人：`v3-grid-rendering`；端到端完成判定由 `v3-road-graph:8.6` 负责。
  - 验证：非共线多段 Edge、简单环、全圆原生弧、棒棒糖、两路口环、八字形、支路删除后的 seam 重定位、平行 Edge 独立高亮，以及缩放/重建截图。
  - 验收：闭环首尾无裂缝、端帽或伪节点；junction/endpoint 数与 incidence 拓扑一致；同端点平行 Edge 均可见且可独立命中。
  - 阶段进展（2026-08-14）：`AppendRoadRibbon` 现在显式消费 Edge 的闭合拓扑，保留首尾重复的显示点列供中心线/高亮使用，但 mesh 只为唯一逻辑点生成顶点；首点用循环前后方向计算 miter，最后一段索引回连首点。普通 mutation 与 `RoadRendererLoadPreparer` 共用该实现；只有同一 self-loop Edge 的 A/B 两个 incidence 才视为纯 seam 并隐藏，self-loop 加支路仍是 junction。
  - 当前证据（2026-08-14）：`RoadRendererLoadPrepareTests` 覆盖方形 rooted loop、`+Tau` 全圆弧、棒棒糖、两路口环、八字形、删除支路后的 seam 重定位、开放 ribbon 布局和 worker/direct 确定性；`RoadGraphClosedPathV3Tests.RemoveEdge_TwoJunctionLoopRelocatesSeamToRemainingJunction` 直接锁定领域 seam 重定位。本轮相关聚焦组合为 33/33，完整自动化为 727/727。真实 `MapTest` 的普通提交和 aggregate Load 都得到 `1 Edge / 8 mesh vertices / 0 node markers`；aggregate Load 后的两路口环在删除 seam 侧支路前为 `4 Edge / 20 mesh vertices / 4 node markers`，删除后为 `2 Edge / 12 mesh vertices / 2 node markers`。`road_input_strategy_runtime_contract.gd` 与 `road_closed_ribbon_runtime_contract.gd` 均输出 PASS，Godot MCP 冻结截图显示原 seam 无伪标记，剩余 junction 与 endpoint 标记正确。
  - 仍缺（保持开放）：缩放/重建视觉矩阵，以及依赖 2.2 `RoadSurfaceHit` 的平行 Edge 独立命中/选择尚未验证。

<a id="v3-grid-rendering2.1"></a>

- [x] **2.1 建立四类 `RoadTypeStyle` 资源与校验**
  - 当前问题：`RoadConfig` 只有全局道路颜色和宽度，无法为稳定 RoadType 提供单一、可检查的展示映射。
  - 修改：增加以 RoadType 为显式 key 的 `RoadTypeStyle`，V3 首版只含展示名称、颜色和正有限宽度；`RoadConfig` 必须恰好覆盖 `Dirt`、`Street`、`Arterial`、`Highway` 且无重复。虚线、纹理、路肩和中央分隔带延期。
  - 依赖：`v3-road-graph:8.4`。
  - 集成负责人：`v3-grid-rendering`；端到端完成判定由 `v3-road-graph:8.6` 负责。
  - 验证：缺失、重复、空名称、非有限颜色、非正宽度、`.tres` 往返和场景启动诊断测试。
  - 验收：所有合法 Edge 类型都能确定查到唯一样式；无效资源不会静默产生不可见道路或修改图数据。
  - 完成实现（2026-08-14）：新增 `[GlobalClass] RoadTypeStyle`，只导出 `RoadType`、`DisplayName`、`Color` 与 `Width`。`RoadConfig` 默认及 `Scenes/road_config.tres` 均恰好配置 `Dirt / 土路 / #8A6652 / 14`、`Street / 街道 / #60727C / 20`、`Arterial / 主干道 / #D7A928 / 26`、`Highway / 高速道路 / #C84B3A / 32`；校验拒绝空集合、空引用、数量不为四、重复/未定义类型、空名称、非有限或透明颜色及非正/非有限宽度。`GetRoadTypeStyle` 对非法映射或非法目标严格抛错，不提供静默 fallback；`RoadRenderer._Ready()` 会报告非法样式资源，但本切片不提前改动 renderer 的全局宽度/颜色消费。
  - 完成证据（2026-08-14）：`RoadTypeStyleTests` 聚焦组合 23/23、完整自动化 749/749；Debug 与 `ExportRelease` build 均为 0 警告/0 错误，Roslyn compiler/analyzer 与新增 GDScript 契约均为 0 diagnostics。`road_type_style_runtime_contract.gd` 完成生产 `.tres` 的四类值校验、全部非法变体拒绝及 `ResourceSaver`/`ResourceLoader` 往返，并在真实 Vulkan `MapTest` 中确认 builder/renderer 共用生产配置、四类查询稳定、静态 renderer 节点仍为 2；editor error 与 DAP `stderr` 为空。

<a id="v3-grid-rendering2.2"></a>

- [ ] **2.2 在单道路批次中渲染并原子刷新差异化表面**
  - 当前问题：`RoadRenderer` 的普通 rebuild 与 Load participant 已按 Edge 消费四类宽度/颜色，并以六分量 desired/presented token 区分 scene、facade、full reset、graph change、样式和 render request；当前会从每个真实 ribbon mesh triangle 同步生成 `EdgeRibbon` owner，并为每个 degree-1 Node 生成与相邻样式一致的解析 `TerminalCap` disc。两类 primitive 均返回 canonical `RoadLocation`，点/矩形查询也已使用构造期不可变 AABB 层级限制局部候选；但 surface 尚无 semantic join 或 junction patch owner。既有 junction 圆形 marker 仍无法填满混合宽度 T/X/锐角、self-loop 加支路或 degree-2 semantic boundary，容易出现洞、叠层和无限 miter。现有工具尚未消费该 provider。普通 mutation 也尚无可观测 stalled/retry 与道路交互门禁。
  - 修改：保持一个道路 mesh，按 Edge 样式生成顶点位置和 vertex color；缓存点列仍只来自权威几何。Edge ribbon 在 Node incidence 的切线/half-width 截面以 butt cut 终止；degree 1 用稳定端帽，degree 2 semantic boundary 生成无洞宽度/颜色过渡，degree 大于等于 3 构造 junction patch。incidence 以 outward direction 的 exact half-plane/cross comparator 排序，self-loop A/B 都参与；在 `RoadNumericPolicy` 约束的整数坐标上用固定版本的 Clipper2（或经同等审计的成熟库）完成 offset/union，并用固定 triangulator 处理 canonical ring，固定量化误差、miter limit、bevel/round fallback、RoadType sector 优先级和 Edge ID 最终同值规则，不能依赖存储反向或字典遍历。mesh 构建同步产出不可变 `RoadSurfaceSnapshot`，覆盖 ribbon、cap、semantic join、junction patch 的稳定 owner，统一提供带完整 token、owner kind、Node/Edge/Endpoint、surface/centerline distance 和 canonical location 的 `RoadSurfaceHit` provider。
  - 接管协议：消费一次 `GraphChanged`：created/updated 重读几何与样式，removed 清缓存，full reset 从不可变 render snapshot 重建。`RoadRenderToken` 至少含 `SceneGeneration + GraphFacadeID + GraphFacadeGeneration + ChangeSequence + RoadStyleRevision + RenderRequestID`；后台结果只有完全等于 `DesiredToken` 才能在主线程一次交换 mesh、surface index 和 `PresentedRenderToken`。普通 mutation 可异步构建；desired/presented 不同时进入 `RoadPresentationStalled` 门禁并由 provider 拒绝 hit，失败保留上一份完整表现且允许诊断和重试。Load participant 必须在 Preflight 预建隐藏 Mesh/RID、surface snapshot、hit index 和不可抛交换 plan；任何关键创建或 generation 失败只在 commit 前返回。成功时由 aggregate non-yield commit 将 graph、empty tool/overlay root、Mesh/RID、surface/hit index、desired/presented token 与 `CurrentSlotID` 一次联合交换，提交后只允许普通 observer 产生 warning，不存在关键表现失败、表现重试或 `CommittedPresentationFailed` 分支。2.2 只负责 provider/participant，不把真实 RoadUpgrade 或四工具接线当作本项完成条件；纯类型更新不得反向改写几何。
  - 依赖：`v3-grid-rendering:2.0`～`2.1`、`v3-road-graph:8.4`～`8.5`、`v3-save-system:2.3`。
  - 集成负责人：`v3-grid-rendering`；Load 的工具接管由 `v3-tool-input:2.4` 协作，最终组合验收由 `v3-road-graph:8.6` 负责。
  - 验证：四类直线与六类曲线；相同/混合宽度的 T/X/锐角、近共线、self-loop 加支路、平行 Edge 和 semantic boundary；量化边界/坐标上限、miter fallback、canonical ring、三角形方向/NaN/洞；反转 Edge/扰动枚举后的像素与 owner 相同，按一一 ID 重命名构造等价图后像素相同且 owner 按同一映射等价；fake hit consumer 覆盖 cap/miter/junction/宽路边缘、重叠宽窄路、sector 平局和矩形 primitive；分别改变六个 token 分量；普通领域 commit 后接管成功/失败/持续门禁/重试；Load hidden resource 创建失败、generation 失配，以及 fake aggregate 将 graph/tool/mesh/surface/token/`CurrentSlotID` 一次交换后的 observer warning。
  - 验收：颜色/宽度可辨识且不改变几何 JSON；合法路面无洞、尖刺、翻转三角形、伪节点或端帽叠层，遍历/反向不改变像素或 owner，ID 重命名后的 owner 等价；provider 只返回与已呈现 mesh 同 token 的 hit，平行 Edge 不去重；一次图事务最多安排一次目标接管，六个 token 分量任一过期都不发布，mesh/surface/presented token 不混代。普通构建失败保持上一份完整表现并持续门禁，可在同一 desired token 或更新 token 上重试；Load 关键资源失败只发生在 Preflight 且活动 graph/tool/mesh/surface/token/`CurrentSlotID` 逐值不变，成功 plan 只执行一次联合引用交换，提交后无关键表现失败分支；静态节点数不随 Edge 数线性增长。
  - 阶段进展（2026-08-14）：`RoadRendererLoadPreparer` 已在后台从 prepared `RoadGraphRevision` 采样 Edge 点列、基础 open/closed ribbon 顶点/索引和节点 marker；`PreflightPreparedLoad` 在主线程创建未发布的 `ArrayMesh`/`MultiMesh`。`PreparedAggregateLoad` 只在全部 participant generation 与 render reservation 有效时，将这些资源、matching desired/presented `RoadRenderToken`、graph、空工具状态和槽目标一起交换，并在完成后发布 `PresentationReady(RoadRenderToken)`。移除 renderer 后执行 Load 的运行时回归会在 commit 前失败，且 `CurrentSlotID`、活动图 payload、undo/redo 与未完成 placement 均保持不变。
  - 当前证据（2026-08-14）：`RoadRendererLoadPrepareTests`、`PreparedAggregateLoadTests` 及完整 727/727 自动化通过；`road_renderer_lifecycle_runtime_contract.gd` 和 `road_closed_ribbon_runtime_contract.gd` 分别覆盖提交前 participant 失败与闭环基础 mesh 成功联合接管，后者还覆盖两路口环从 `4 Edge / 20 vertices / 4 markers` 到支路删除后 `2 Edge / 12 vertices / 2 markers` 的同代刷新。最新统一样式 Vulkan 数据记录在 2.3；这些证据只证明既有批次与基础 Load participant，没有证明 mixed-width surface 的 `2.2`/`2.3` 完成。
  - 分级 ribbon 进展（2026-08-14）：`RoadConfig.CaptureRoadTypeStyleSnapshot()` 在主线程严格校验并复制四类值；普通 `RebuildStaticBatches` 与后台 `RoadRendererLoadPreparer` 都按 `GraphEdge.RoadType` 解析同一值快照，以对应 half-width 和 vertex color 写入一个 `ArrayMesh`，道路层保持白色 modulate。Load 的 prepared payload 同步携带 `RoadColors`，Preflight 创建的隐藏 mesh 与普通 mutation 重建因而使用相同批次格式；样式资源不会进入 worker，也不会写回图或 JSON。
  - 分级 ribbon 证据（2026-08-14）：`RoadRendererLoadPrepareTests` 聚焦 9/9，直接覆盖四类型宽度/颜色、worker/direct 确定性、closed/open ribbon 数组等长；完整自动化 750/750，Debug 与 `ExportRelease` build 均为 0 警告/0 错误，Roslyn compiler/analyzer 和新 GDScript 契约为 0 diagnostics。真实 Vulkan `road_type_mesh_style_runtime_contract.gd` 先经 aggregate Load 发布 14/20/26/32 宽的四类单 mesh ribbon，再以普通 mutation 触发同批次重建，两条路径均 PASS；截图人工确认四类颜色/宽度可辨识。Godot MCP 主场景 smoke 的 editor error 与 DAP `stderr` 为空。
  - 表现 token 进展（2026-08-14）：新增严格六分量 `RoadRenderToken` 与纯 CLR `RoadPresentationTokenTracker`；每个 `RoadGraph` 分配进程内稳定且不回绕的 `FacadeID`。普通 graph change/full reset、renderer 改绑、scene generation 和显式 `RefreshRoadStyles()` 分别推进所拥有的身份；普通 rebuild 只在 desired 未过期时一次交换 mesh/node batch 后提交 presented。Load admission 只接受 current presentation，预留下一次 facade generation/request，并在 aggregate commit 中同时发布 matching desired/presented token；随后同一 full-reset `GraphChanged` 由 graph token 精确消重。
  - 表现 token 证据（2026-08-14）：`RoadRenderTokenTests` 覆盖六分量相等性、非法身份、普通/full-reset/改绑/scene/style 推进、Load 预留/失效/原子提交及 facade ID 稳定唯一；完整自动化为 763/763，Debug 与 `ExportRelease` build 均为 0 警告/0 错误，Roslyn compiler/analyzer 与 `road_render_token_runtime_contract.gd` 均为 0 diagnostics。真实 `MapTest` 通过 Godot MCP 逐步得到 `initial=(1,1,1,0,1,1)`、`mutation=(1,1,1,1,1,2)`、`style=(1,1,1,1,2,3)`、`Load=(1,1,2,2,2,4)`，每步 desired 等于 presented；Save、Load、删除测试槽和场景清理均成功。独立 GDScript runner 未执行，DAP 读取因 `Max client limits reached` 受阻。
  - Edge ribbon surface 进展（2026-08-14）：新增不可变 `RoadSurfaceSnapshot`、稳定 `RoadSurfaceOwner`/`RoadSurfaceHit` 数据契约，以及点命中和矩形 owner 查询。`AppendRoadRibbon` 每写入一个真实 mesh triangle 就同步写入一个同顶点、同顺序的 `EdgeRibbon` primitive；普通 rebuild 一次交换 mesh、surface snapshot 与 matching presented token，Load preparer 则在 worker 生成同一 surface 的 `PreparedData`，aggregate commit 再与 graph/tool/mesh/slot/token 联合交换。provider 在 desired/presented/snapshot token 任一不一致时拒绝查询；点命中按 surface distance、centerline distance、sector、Edge ID 和稳定 primitive 顺序破同值，矩形查询返回排序去重的 Edge ID。
  - Edge ribbon surface 证据（2026-08-14）：`RoadSurfaceSnapshotTests` 覆盖真实 triangle 内外距离、边界包含、稳定破同值、矩形接触、defensive copy 与非法输入；`RoadRendererLoadPrepareTests` 逐 triangle 对照 mesh index 和 owner，完整自动化为 774/774。Debug 与 `ExportRelease` build、Roslyn compiler/analyzer 和 `road_render_token_runtime_contract.gd` 均为 0 diagnostics。真实 `MapTest` 验证普通 mutation、样式刷新和 aggregate Load 后 `surfacePrimitiveCount=2`，点命中均为 `EdgeRibbon`、`surfaceDistance=0` 且 hit token 等于 desired/presented；Load 前新增道路的旧 surface 已消失，矩形查询只返回已加载 Edge。临时存档槽已删除，editor 无新增错误，DAP `stderr` 为空。
  - Canonical location 进展（2026-08-14）：`RoadGeometryDisplaySampler` 在保持公开点列 API 不变的同时，为内部 `RoadGeometryDisplayPath` 的每个可见区间记录 `GeometryIndex + ParameterStart/ParameterEnd`；普通 cache 与 Load prepared payload 携带同一 provenance。`RoadSurfaceTriangle` 对内部显示细分点和 geometry join 使用左闭右开的 location 所有权，只有开放 Edge 最后一段拥有 `t = 1`，self-loop 最后一段不拥有 seam 终点并由第一 geometry 的 `t = 0` 唯一表示；完全同值的 primitive 优先返回拥有 location 的候选。
  - Canonical location 证据（2026-08-14）：`RoadGeometryDisplaySamplerTests`、`RoadSurfaceSnapshotTests` 与 `RoadRendererLoadPrepareTests` 覆盖曲线显示 span 参数、location 插值、geometry join、开放 B 端、self-loop seam，以及普通/Load triangle provenance；完整自动化为 779/779。Debug 与 `ExportRelease` build 均为 0 警告/0 错误，Roslyn compiler/analyzer 与修改后的 GDScript 为 0 diagnostics。隔离用户目录的 `road_render_token_runtime_contract.gd` 输出 PASS；Godot MCP 逐步验证普通 mutation、样式刷新和 aggregate Load 后的 hit 均返回 `Edge 2 / geometry 0 / parameter 0.5`，且 render token 与 desired/presented 完全匹配。Save、Load、删除测试槽和临时用户目录均已清理，editor 无新增错误，DAP `stderr` 为空。
  - Surface 空间索引进展（2026-08-14）：`RoadSurfaceSnapshot` 为 defensive-copy triangle/disc 构建统一稳定 AABB 层级，叶容量为 8；点/矩形查询先裁剪 node 与 primitive bounds，再按 kind 执行精确 triangle/circle 判定和确定性破同值。查询使用有界 `stackalloc` 遍历栈，不分配候选数组。`RoadSurfaceSnapshot.PreparedData` 以分离数组保存两类 primitive 且不暴露可变状态；普通构造入口继续执行 defensive copy，Load worker 则在 `RoadRendererLoadPreparer.Prepare()` 内完成复制与建树，主线程 Preflight 只将 reserved token 绑定到 prepared 数据。
  - Surface 空间索引证据（2026-08-14）：`RoadSurfaceSnapshotTests` 与 `RoadRendererLoadPrepareTests` 合计 27/27；1,024 个远隔 quad 的点/矩形查询和 128 条 Load prepared 道路均把 primitive 候选限制为 2～8 个、精确测试限制为 2 个。完整自动化 782/782，Debug/`ExportRelease` 0 警告、0 错误，Roslyn/GDScript 0 diagnostics。隔离用户目录的 token 契约与 Godot MCP aggregate Load 均验证 matching token、`surfacePrimitiveCount=2` 和 `Edge 2 / geometry 0 / parameter 0.5`；editor 无新增错误且 DAP `stderr` 为空。测试槽、动态探针、临时用户目录和日志已清理；本切片未重跑 10k/100k Vulkan 性能契约。
  - Terminal cap 进展（2026-08-14）：degree-1 Node 现在从唯一 incidence 解析相邻 Edge 与端点切线，按 `RoadTypeStyle.Width / 2` 生成解析 `RoadSurfaceDisc`，并用相同样式宽度/颜色生成 marker；端点高亮也使用该样式半宽。`RoadSurfaceOwner.TerminalCap` 携带 Edge/Node/Endpoint，A/B 分别绑定 geometry `0 / t=0` 和最后 geometry `/ t=1`。普通 rebuild 与 Load worker 走同一 `CreateTerminalCapSurface`，junction marker 和纯 loop seam 行为不变。
  - Terminal cap 证据（2026-08-14）：`RoadSurfaceSnapshotTests` 与 `RoadRendererLoadPrepareTests` 聚焦组合 34/34，覆盖精确圆内外距离、矩形圆相交、非法半径/bounds、ribbon 同值优先、样式直径/颜色、A/B canonical location、普通/Load primitive 一致与 defensive copy；完整自动化 786/786。Debug/`ExportRelease` 0 警告、0 错误，Roslyn/GDScript 0 diagnostics。隔离运行时契约覆盖 mutation/style/Load；Godot MCP 验证单路 `surfacePrimitiveCount=4`、A/B `TerminalCap` 参数 `0/1` 及 matching hit/desired/presented token，editor 无新增错误且 DAP `stderr` 为空。1k Vulkan camera/preview/highlight P95 为 0.393/0.378/0.339 ms、renderer rebuild 135.918 ms、静态节点为 2，并输出 PASS；本轮未重跑 10k/100k。
  - 仍缺（保持开放）：尚无 semantic join、junction patch、平行 Edge 工具接线，以及普通 mutation stalled/retry 与道路交互门禁。

<a id="v3-grid-rendering2.3"></a>

- [ ] **2.3 建立混合类型视觉、接管与性能门禁**
  - 当前问题：V2 基线只含统一样式，没有类型切换到 mesh 可见的离散延迟，也无法证明 per-edge 颜色/宽度和 junction patch 仍满足规模目标。现有全图 mesh 重建基线约为 10k 159 ms、100k 1170 ms，不能把后台总耗时误报为主线程无卡顿，也不能用普通 mutation 的最终重试成功掩盖 Load 的提交后关键失败。
  - 修改：同时使用 junction-dense 10k Edge 和 geometry-dense 长 Edge 数据集，记录 Node/Edge/geometry/query fragment/self-loop/parallel Edge/junction patch/surface primitive 数；分别测量主线程 render snapshot capture、mesh+surface presentation commit、full-reset barrier 总时长、Load hidden resource Preflight 与联合 commit、后台或分帧 polygon/tessellation/index 总耗时，及镜头、闭环/类型化建造预览、各 owner kind 命中、拆除/改造预览、1/100/1000 Edge 改造与撤销重做、draw calls、objects、primitives、子节点和分配量。扰动完整 `RoadRenderToken` 的每个维度验证过期构建丢弃；普通 mutation 接管前旧 mesh 完整可见且所有 `RoadSurfaceHit`/道路命令被锁，失败进入可观测 stalled 状态并可重试；Load 在 Preflight 关键失败时不提交，在成功时联合交换且不经过 stalled/retry。100k 使用相同口径压力测试。先记录同机 V2 对照并固定主线程离散门槛，再决定是否需要分块 mesh。
  - 依赖：`v3-grid-rendering:2.2`、`v3-tool-input:2.2`、`v3-tool-input:2.4`。
  - 集成负责人：`v3-grid-rendering`；最终组合验收由 `v3-road-graph:8.6` 负责。
  - 验证：真实 `MapTest` / Vulkan Forward+ 自动化；四类 T/X/锐角、semantic boundary、简单环/棒棒糖/两路口环可辨识截图及像素差；性能与 sequence 接管文档；普通 mutation 的成功/失败/重试时序，以及 Load 每个关键 Preflight 故障点、一次联合交换、普通 observer warning 和无提交后关键表现失败断言。
  - 验收：10k 连续交互 P95 不超过 16.67 ms，静态道路节点和 draw call 不随 Edge 数线性增长；snapshot capture、普通 presentation commit、full-reset barrier、Load Preflight/联合 commit 和离散改造满足 Phase 0 固定门槛；旧/新 mesh、surface index 和 token 不混代，过期任务不覆盖新图，barrier 内无道路交互；Load 成功后 graph/tool/mesh/surface/token/`CurrentSlotID` 同代可见，失败只保留旧会话，observer warning 不回滚成功提交；100k 结果完整记录但不阻塞 V3。
  - 当前基线（2026-08-14）：统一样式 Vulkan 完整运行中，10k camera/preview/highlight P95 为 0.657/0.779/0.672 ms，Load 与 renderer rebuild 为 608.025 ms；首轮 100k 为 13.517/0.722/0.699 ms、重建 4108.956 ms，独立 100k 复跑为 0.616/0.661/0.637 ms、重建 4492.629 ms，两轮均输出 PASS，静态 renderer 节点为 2。首轮 100k camera 的 13.517 ms 尾延迟与复跑值同时保留，不能只报告热复跑。
  - 分级 ribbon 基线（2026-08-14）：加入 per-edge vertex color 后，独立真实 Vulkan 10k camera/preview/highlight P95 为 0.564/0.827/0.833 ms、Load 与 renderer rebuild 为 665.452 ms；随后独立 100k 为 8.674/0.977/0.737 ms、重建 4719.153 ms。两档均输出 PASS，draw call 为 4/5/4，静态 renderer 节点为 2；100k 仍只记录压力结果，不取代 10k 硬门槛。
  - Terminal cap 增量基线（2026-08-14）：本轮只执行 1k Vulkan，camera/preview/highlight P95 为 0.393/0.378/0.339 ms、renderer rebuild 135.918 ms、静态 renderer 节点为 2，并输出 PASS。该结果不刷新既有 10k/100k 基线，也不满足 2.3 的规模门禁。
  - 仍缺（保持开放）：当前数据尚无 semantic join、junction patch、普通 mutation stalled/retry 或离散类型改造时延；六分量 token 已进入有空间索引的 ribbon/cap surface provider 和 Load hidden snapshot Preflight，但该索引尚未进入 10k/100k 完整 owner 性能/扰动矩阵。四条分级直线截图、ribbon hit 和 1k cap PASS 不满足本项的混合 junction、完整接管与性能门禁。

## 暂不执行

### 高级 RoadType 视觉

- 延期原因：V3 首版先验证四类道路的稳定名称、颜色、正有限宽度和混合路面拓扑；虚线、纹理、路肩及中央分隔带会扩大资源、几何和视觉验收范围。
- 保持现状：`RoadTypeStyle` 仅包含 2.1 明确的首版字段，派生视觉不得进入 RoadGraph 或存档事实。
- 重新开启条件：2.0～2.3 全部完成，并有明确产品样式、资源预算与生命周期约束及 V3 视觉验收要求。

### 分块道路 mesh

- 延期原因：现阶段没有 V3 同机指标证明单道路批次无法满足主线程离散门槛；提前分块会增加 surface owner、token 和原子接管复杂度。
- 保持现状：2.2 使用单道路 mesh，并把 snapshot、tessellation、commit 和后台总耗时分开测量。
- 重新开启条件：2.3 的 junction-dense 或 geometry-dense 10k 数据证明单批次方案无法满足已固定门槛，且瓶颈能由分块策略消除。

## 已解决基线

- [x] **基础 surface 查询已使用不可变空间索引。** 普通 snapshot 与 Load worker prepared payload 共用同一 AABB 层级；局部点/矩形查询不会随远隔 primitive 总数线性执行精确 triangle/disc 测试，Preflight 不重复复制或建树。semantic join、junction patch 和性能矩阵仍由开放的 2.2～2.3 负责。
- [x] **Degree-1 `TerminalCap` 已与道路样式及 surface 同源。** 普通与 Load 路径按相邻 `RoadTypeStyle` 生成同宽同色 marker 和解析 disc；A/B cap 携带稳定 Edge/Node/Endpoint 与 canonical 参数 `0/1`，并与 ribbon 共用同代 token 和 AABB 查询。semantic join、junction patch、工具消费和性能矩阵仍由开放的 2.2～2.3 负责。
- [x] **V2 六类原生几何共享只读显示采样。** `grid-rendering:1.1` 已验证 Edge、有效建造预览和拆除高亮复用 `RoadGeometryDisplaySampler`，显示容差不会反向修改权威控制参数。
- [x] **V2 大规模静态道路保持常数级渲染节点。** `grid-rendering:1.2` 已用道路 `ArrayMesh` 与节点 `MultiMesh` 把 10k/100k 的 `RoadRenderer` 静态子节点固定为 2。
- [x] **V2 规模性能证据已经记录。** `docs/performance/road-rendering-v2-baseline.md` 记录统一样式下 10k 三类交互满足 16.67 ms，以及 10k/100k 重建、draw calls、objects 和压测结果；V3 以同机同口径对照，不把这些结果当作分级表现已经完成。
- [x] **V2 RoadGraph 原生几何和显示边界已经验收。** `road-graph:2.5`～`2.6` 与 `grid-rendering:1.1` 是 V3 必须保留的基线，不转成 `v3-grid-rendering` 活动项。

## 完成标准

1. 2.0～2.3 全部通过各自自动化、真实 Godot/Vulkan 视觉和性能门禁；六类原生几何按 V3 新契约重新验证，并达到同机 junction-dense/geometry-dense 10k 门槛，不要求复用或兼容 V2 渲染 API、测试或实现。
2. canonical Edge、self-loop、平行 Edge、四类 RoadType、semantic boundary 和混合宽度 junction 形成无洞、可确定、可独立命中的同源 mesh/surface 表现，派生数据不反向修改 RoadGraph 或存档。
3. 普通 mutation 只在完整 token 匹配时一次发布 mesh/surface/presented token；失败保留旧表现、持续禁止道路交互并允许诊断重试，过期结果永不覆盖新图。
4. Load 在 Preflight 完成隐藏 Mesh/RID、surface snapshot、hit index 和不可抛 plan；关键失败只发生在 commit 前，成功与 graph、empty tool/overlay root、mesh/surface、token 和 `CurrentSlotID` 一次联合交换，提交后只有普通 observer warning，不存在关键表现失败或表现重试结果。
5. junction-dense 与 geometry-dense 10k 满足 Phase 0 固定主线程及 60 FPS 门槛；100k 使用相同口径记录完整压测但不阻塞 V3。
6. `v3-grid-rendering` 只负责本系统产出；第三代道路系统最终完成由 `v3-road-graph:8.6` 汇总 `v3-save-system`、`v3-tool-input`、`v3-ui` 和本路线图证据后判定。
