# 第三代网格渲染系统待办清单

> 系统 key：`v3-grid-rendering`
> 整理日期：2026-08-13
> 证据：当前工作区 `Scripts/Road/RoadGeometryDisplaySampler.cs`、`RoadRenderer.cs`、`RoadBuilder.cs`、`RoadConfig.cs`，V2 显示与性能契约，`docs/performance/road-rendering-v2-baseline.md`、`docs/manuals/road-system-v2-gen.md` 附录 D 及 `docs/manuals/road-system-v3-gen.md`。
> 主导原则：负责第三代 canonical Edge、self-loop、平行 Edge 和 RoadType 的确定性可视化，生成与实际 mesh 同源的道路表面命中，并为普通 mutation 与 Load 提供各自正确的表现接管协议；视觉样式和派生表面不是 RoadGraph 的事实来源。

## 状态总览

| ID | 发现 | 当前状态 | 处置方式 |
|---|---|---|---|
| 2.0 | V2 renderer 不理解 self-loop incidence 与固定 loop seam | 开放 | 构造 closed ribbon、隐藏 seam，并保持平行 Edge 独立命中与高亮 |
| 2.1 | `RoadConfig` 没有完整且可验证的四类 RoadType 样式 | 开放 | 建立 `RoadTypeStyle` 资源、唯一覆盖和运行时校验 |
| 2.2 | 单一全局样式的开放 ribbon 不能形成可命中的混合宽度完整路面 | 开放 | 建立 per-edge 样式、junction patch、surface snapshot、完整 token 与 Load participant |
| 2.3 | 混合类型、full reset 和批量改造没有 V3 性能与视觉门禁 | 开放 | 建立 10k 硬门槛、离散延迟指标、token 接管验证和 100k 压测 |

### 设计覆盖矩阵

| 设计范围 | 当前事实 | 关联待办 |
|---|---|---|
| canonical Edge、self-loop 与平行 Edge | 当前 `AppendRoadRibbon` 把 Edge 当作开放点列；切线和节点分类不能区分 self-loop 的 A/B incidence、loop seam 与真实结构边界 | 2.0、`v3-road-graph:8.1`～`8.3` |
| RoadType 与完整道路表面 | 当前全图共享 `RoadColor` / `RoadWidth`，开放 ribbon 加圆形节点不能确定覆盖混合宽度 junction、semantic boundary 和宽路边缘命中 | 2.1～2.2、`v3-road-graph:8.4`～`8.5` |
| 表现事务与 Load 原子接管 | 普通 mutation 允许领域提交后异步构建并在失败时 stalled/retry；Load 不能暴露提交后表现窗口，关键资源必须在 Preflight 完成 | 2.2、`v3-save-system:2.3`、`v3-tool-input:2.4` |
| V2 显示与规模基线 | 六类原生几何已有统一只读显示采样；统一样式的 10k Edge 已通过 60 FPS 门槛并记录 100k 压测 | `grid-rendering:1.1`～`1.2`（V2 已完成）、2.3 |

## V3 实施记录

### 2026-08-13：2.1 RoadTypeStyle 数据与目录校验（部分）

- 新增 `Scripts/Road/V3/RoadTypeStyle.cs`：纯 C# 样式数据类，包含稳定 `RoadType`、展示名称、颜色与正有限宽度，并校验非法枚举、空名称、非有限颜色与非正宽度。
- 新增 `Scripts/Road/V3/RoadTypeStyleCatalog.cs`：校验样式集合恰好覆盖 `Dirt`/`Street`/`Arterial`/`Highway` 且无重复，返回可查询目录，并提供 `CreateDefault()` 默认四类样式。
- 新增 8 个 xUnit 用例；完整测试套件 943/943 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：Godot `Resource`/`.tres` 封装、`RoadConfig` 接入与真实渲染使用。

### 2026-08-13：2.2 RoadSurfaceHit 数据模型（部分）

- 新增 `Scripts/Road/V3/RoadSurfaceHit.cs`：定义 `RoadSurfaceOwnerKind` 与带完整 `GraphStateToken`、owner、Node/Edge/Endpoint、`RoadLocation` 和距离的命中记录，并提供基本有效性校验。
- 新增 4 个 xUnit 用例；完整测试套件 947/947 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：surface snapshot、hit provider、mesh 生成与表现接管协议。

### 2026-08-13：2.2 RoadSurfaceSnapshot 数据模型（部分）

- 新增 `Scripts/Road/V3/RoadSurfaceSnapshot.cs`：定义 `RoadSurfaceOwner` 与不可变 `RoadSurfaceSnapshot`，绑定完整 token，并提供按 Edge/Node 查询 owner 的辅助方法。
- 新增 4 个 xUnit 用例；完整测试套件 951/951 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：hit provider、mesh 生成与表现接管协议。

### 2026-08-13：2.2 RoadRenderToken 数据模型（部分）

- 新增 `Scripts/Road/V3/RoadRenderToken.cs`：定义包含场景、图、样式与请求世代的不可变表现 token，并提供有效性校验与精确匹配。
- 新增 4 个 xUnit 用例；完整测试套件 955/955 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：hit provider、mesh 生成与表现接管协议。

### 2026-08-13：2.2 RoadPresentationState 表现接管状态（部分）

- 新增 `Scripts/Road/V3/RoadPresentationState.cs`：维护 desired/presented token 与已呈现快照，只允许与 desired 完全匹配的后台结果发布，并暴露 `IsStalled`。
- 新增 4 个 xUnit 用例；完整测试套件 959/959 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：hit provider、mesh 生成与完整接管协议。

### 2026-08-13：2.2 RoadSurfaceHitProvider 命中提供器（部分）

- 新增 `Scripts/Road/V3/RoadSurfaceHitProvider.cs`：仅接受与已呈现 snapshot 同 token 且 owner 仍存在的命中，stalled 或过期 owner 被拒绝。
- 新增 4 个 xUnit 用例；完整测试套件 963/963 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：mesh 生成与完整接管协议。

## 执行顺序

### 阶段 2：第三代道路表面、分级表现与接管门禁

<a id="v3-grid-rendering2.0"></a>

- [ ] **2.0 正确渲染 canonical Edge、self-loop 与平行 Edge**
  - 当前问题：`AppendRoadRibbon` 把每条 Edge 当作开放点列，首尾分别计算端部；`TryGetOutgoingDirection` 在 self-loop 上总会选择 NodeA 分支，节点标记也无法区分 loop seam、terminal 和真实 junction。
  - 修改：消费 `EdgeIncidence.Endpoint`；self-loop 使用循环相邻方向生成无裂缝 closed ribbon，seam 不绘制 endpoint/junction 标记；degree 1 绘制 endpoint，degree 大于等于 3 的最终表面由 2.2 junction patch 负责，degree 2 的 seam/semantic boundary 不画伪节点。高亮、矩形选择和中心线命中不得按端点对去重平行 Edge。
  - 依赖：`v3-road-graph:8.1`～`8.3`。
  - 集成负责人：`v3-grid-rendering`；端到端完成判定由 `v3-road-graph:8.6` 负责。
  - 验证：非共线多段 Edge、简单环、全圆原生弧、棒棒糖、两路口环、八字形、支路删除后的 seam 重定位、平行 Edge 独立高亮，以及缩放/重建截图。
  - 验收：闭环首尾无裂缝、端帽或伪节点；junction/endpoint 数与 incidence 拓扑一致；同端点平行 Edge 均可见且可独立命中。

<a id="v3-grid-rendering2.1"></a>

- [ ] **2.1 建立四类 `RoadTypeStyle` 资源与校验**
  - 当前问题：`RoadConfig` 只有全局道路颜色和宽度，无法为稳定 RoadType 提供单一、可检查的展示映射。
  - 修改：增加以 RoadType 为显式 key 的 `RoadTypeStyle`，V3 首版只含展示名称、颜色和正有限宽度；`RoadConfig` 必须恰好覆盖 `Dirt`、`Street`、`Arterial`、`Highway` 且无重复。虚线、纹理、路肩和中央分隔带延期。
  - 依赖：`v3-road-graph:8.4`。
  - 集成负责人：`v3-grid-rendering`；端到端完成判定由 `v3-road-graph:8.6` 负责。
  - 验证：缺失、重复、空名称、非有限颜色、非正宽度、`.tres` 往返和场景启动诊断测试。
  - 验收：所有合法 Edge 类型都能确定查到唯一样式；无效资源不会静默产生不可见道路或修改图数据。

<a id="v3-grid-rendering2.2"></a>

- [ ] **2.2 在单道路批次中渲染并原子刷新差异化表面**
  - 当前问题：`RoadRenderer` 以一个全局 half-width 构建开放 ribbon，并用 `_roadBatchLayer.Modulate` 统一着色；属性更新没有刷新入口。即使逐 Edge 改宽，开放 ribbon 加圆形节点也无法填满混合宽度 T/X/锐角、self-loop 加支路或 degree-2 semantic boundary，容易出现洞、端帽重叠和无限 miter。工具若仍以中心线 interaction radius 查询，宽路边缘不会稳定进入候选。表现异步接管若只比较 sequence 或分步替换 mesh/index，会产生混代命中；把普通 mutation 的 stalled/retry 模型套到 Load，则会暴露“图已交换但关键表现失败”的非法状态。
  - 修改：保持一个道路 mesh，按 Edge 样式生成顶点位置和 vertex color；缓存点列仍只来自权威几何。Edge ribbon 在 Node incidence 的切线/half-width 截面以 butt cut 终止；degree 1 用稳定端帽，degree 2 semantic boundary 生成无洞宽度/颜色过渡，degree 大于等于 3 构造 junction patch。incidence 以 outward direction 的 exact half-plane/cross comparator 排序，self-loop A/B 都参与；在 `RoadNumericPolicy` 约束的整数坐标上用固定版本的 Clipper2（或经同等审计的成熟库）完成 offset/union，并用固定 triangulator 处理 canonical ring，固定量化误差、miter limit、bevel/round fallback、RoadType sector 优先级和 Edge ID 最终同值规则，不能依赖存储反向或字典遍历。mesh 构建同步产出不可变 `RoadSurfaceSnapshot`，覆盖 ribbon、cap、semantic join、junction patch 的稳定 owner，统一提供带完整 token、owner kind、Node/Edge/Endpoint、surface/centerline distance 和 canonical location 的 `RoadSurfaceHit` provider。
  - 接管协议：消费一次 `GraphChanged`：created/updated 重读几何与样式，removed 清缓存，full reset 从不可变 render snapshot 重建。`RoadRenderToken` 至少含 `SceneGeneration + GraphFacadeID + GraphFacadeGeneration + ChangeSequence + RoadStyleRevision + RenderRequestID`；后台结果只有完全等于 `DesiredToken` 才能在主线程一次交换 mesh、surface index 和 `PresentedRenderToken`。普通 mutation 可异步构建；desired/presented 不同时进入 `RoadPresentationStalled` 门禁并由 provider 拒绝 hit，失败保留上一份完整表现且允许诊断和重试。Load participant 必须在 Preflight 预建隐藏 Mesh/RID、surface snapshot、hit index 和不可抛交换 plan；任何关键创建或 generation 失败只在 commit 前返回。成功时由 aggregate non-yield commit 将 graph、empty tool/overlay root、Mesh/RID、surface/hit index、desired/presented token 与 `CurrentSlotID` 一次联合交换，提交后只允许普通 observer 产生 warning，不存在关键表现失败、表现重试或 `CommittedPresentationFailed` 分支。2.2 只负责 provider/participant，不把真实 RoadUpgrade 或四工具接线当作本项完成条件；纯类型更新不得反向改写几何。
  - 依赖：`v3-grid-rendering:2.0`～`2.1`、`v3-road-graph:8.4`～`8.5`、`v3-save-system:2.3`。
  - 集成负责人：`v3-grid-rendering`；Load 的工具接管由 `v3-tool-input:2.4` 协作，最终组合验收由 `v3-road-graph:8.6` 负责。
  - 验证：四类直线与六类曲线；相同/混合宽度的 T/X/锐角、近共线、self-loop 加支路、平行 Edge 和 semantic boundary；量化边界/坐标上限、miter fallback、canonical ring、三角形方向/NaN/洞；反转 Edge/扰动枚举后的像素与 owner 相同，按一一 ID 重命名构造等价图后像素相同且 owner 按同一映射等价；fake hit consumer 覆盖 cap/miter/junction/宽路边缘、重叠宽窄路、sector 平局和矩形 primitive；分别改变六个 token 分量；普通领域 commit 后接管成功/失败/持续门禁/重试；Load hidden resource 创建失败、generation 失配，以及 fake aggregate 将 graph/tool/mesh/surface/token/`CurrentSlotID` 一次交换后的 observer warning。
  - 验收：颜色/宽度可辨识且不改变几何 JSON；合法路面无洞、尖刺、翻转三角形、伪节点或端帽叠层，遍历/反向不改变像素或 owner，ID 重命名后的 owner 等价；provider 只返回与已呈现 mesh 同 token 的 hit，平行 Edge 不去重；一次图事务最多安排一次目标接管，六个 token 分量任一过期都不发布，mesh/surface/presented token 不混代。普通构建失败保持上一份完整表现并持续门禁，可在同一 desired token 或更新 token 上重试；Load 关键资源失败只发生在 Preflight 且活动 graph/tool/mesh/surface/token/`CurrentSlotID` 逐值不变，成功 plan 只执行一次联合引用交换，提交后无关键表现失败分支；静态节点数不随 Edge 数线性增长。

<a id="v3-grid-rendering2.3"></a>

- [ ] **2.3 建立混合类型视觉、接管与性能门禁**
  - 当前问题：V2 基线只含统一样式，没有类型切换到 mesh 可见的离散延迟，也无法证明 per-edge 颜色/宽度和 junction patch 仍满足规模目标。现有全图 mesh 重建基线约为 10k 159 ms、100k 1170 ms，不能把后台总耗时误报为主线程无卡顿，也不能用普通 mutation 的最终重试成功掩盖 Load 的提交后关键失败。
  - 修改：同时使用 junction-dense 10k Edge 和 geometry-dense 长 Edge 数据集，记录 Node/Edge/geometry/query fragment/self-loop/parallel Edge/junction patch/surface primitive 数；分别测量主线程 render snapshot capture、mesh+surface presentation commit、full-reset barrier 总时长、Load hidden resource Preflight 与联合 commit、后台或分帧 polygon/tessellation/index 总耗时，及镜头、闭环/类型化建造预览、各 owner kind 命中、拆除/改造预览、1/100/1000 Edge 改造与撤销重做、draw calls、objects、primitives、子节点和分配量。扰动完整 `RoadRenderToken` 的每个维度验证过期构建丢弃；普通 mutation 接管前旧 mesh 完整可见且所有 `RoadSurfaceHit`/道路命令被锁，失败进入可观测 stalled 状态并可重试；Load 在 Preflight 关键失败时不提交，在成功时联合交换且不经过 stalled/retry。100k 使用相同口径压力测试。先记录同机 V2 对照并固定主线程离散门槛，再决定是否需要分块 mesh。
  - 依赖：`v3-grid-rendering:2.2`、`v3-tool-input:2.2`、`v3-tool-input:2.4`。
  - 集成负责人：`v3-grid-rendering`；最终组合验收由 `v3-road-graph:8.6` 负责。
  - 验证：真实 `MapTest` / Vulkan Forward+ 自动化；四类 T/X/锐角、semantic boundary、简单环/棒棒糖/两路口环可辨识截图及像素差；性能与 sequence 接管文档；普通 mutation 的成功/失败/重试时序，以及 Load 每个关键 Preflight 故障点、一次联合交换、普通 observer warning 和无提交后关键表现失败断言。
  - 验收：10k 连续交互 P95 不超过 16.67 ms，静态道路节点和 draw call 不随 Edge 数线性增长；snapshot capture、普通 presentation commit、full-reset barrier、Load Preflight/联合 commit 和离散改造满足 Phase 0 固定门槛；旧/新 mesh、surface index 和 token 不混代，过期任务不覆盖新图，barrier 内无道路交互；Load 成功后 graph/tool/mesh/surface/token/`CurrentSlotID` 同代可见，失败只保留旧会话，observer warning 不回滚成功提交；100k 结果完整记录但不阻塞 V3。

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
