# 交通模拟系统待办清单

> 系统 key：`traffic-simulation`
> 复核日期：2026-08-13
> 证据：`docs/todo/road-graph.md` 的第二代完成记录、当前 RoadGraph 与变更事件源码、`docs/design/simulation-systems.md`、`docs/manuals/road-system-v2-gen.md`、`docs/manuals/road-system-v3-gen.md` 及现有道路系统待办。
> 主导原则：以第二代稳定的 RoadGraph 事件契约为基线；在第三代 canonical Edge、self-loop/parallel Edge 与 RoadType 契约完成后，负责 `TrafficGraph`、寻路、速度/容量、拥堵和模拟增量同步；不负责道路建造或改造工具。

## 状态总览

| 遗留 ID | 发现 | 当前状态 | 处置方式 |
|---|---|---|---|
<a id="traffic-simulationphase-6"></a>
| Phase 6 | `TrafficGraph`、A*、拥堵和增量同步 | 不属于第二代；RoadGraph 前置契约已完成，但第三代 canonical Edge、模拟语义和 RoadType 产品契约仍未定义 | P6.1～P6.4 在 `v3-road-graph:8.6` 完成后另行启用 |
| P6.5 | 既有道路升级工具 | 已取代 | 改造事务、选择和 UI 分别迁移到 `v3-road-graph:8.5`、`v3-tool-input:2.2`、`v3-ui:1.2` |

### 设计覆盖矩阵

| 设计范围 | 当前事实 | 关联待办或基线 |
|---|---|---|
<a id="traffic-simulation95732cb62c0e"></a>
| §9、§10 阶段 C、§11 Phase 6 | `TrafficGraph`、A*、拥堵和模拟增量同步均未实现；道路改造已由 V3 非模拟系统接管 | 延期 P6.1～P6.4；P6.5 已取代 |

## 执行顺序

旧版执行顺序中没有任何活动复选框项属于该系统。

## 暂不执行

<a id="traffic-simulationdef6b8230b9f"></a>
### 第三代之后：交通模拟

<a id="traffic-simulationp6.1"></a>
- [ ] **P6.1 构建 `TrafficGraph` 只读带权有向视图**
  - 延期原因：第二代 RoadGraph 的空间查询、删除事务和事务后事件契约已经稳定，但 V3 canonical Edge、endpoint-role incidence、RoadType 和统一 `GraphChanged` 尚未完成；模拟层不应绑定过渡邻接或逐 Edge 事件。
  - 启用条件：`v3-road-graph:8.6` 完成；届时 RoadGraph 运行时按照 V3 契约允许 self-loop 和平行 Edge，TrafficGraph 只定义它们的模拟映射，不得回头禁止领域合法拓扑。
  - 修改：从 `RoadGraph` 的 incidence/Edge 视图构建模拟专用有向邻接和 Edge 映射；不得用 `GetNeighborIDs().Distinct()` 代替边身份，也不得通过该视图写回 RoadGraph。
  - 测试：双向边映射、单向扩展策略、自环、两节点平行 Edge、八字形、无 junction 独立环和图变更前后只读一致性。
  - 验收：模拟层可遍历带权有向图，且无法绕过 RoadGraph API 修改拓扑。

  - 关联引用：`road-graph:4.4`、`road-graph:31883cfb1c78`。
  - 来源 key：`todo:deferred:P6.1`。

<a id="traffic-simulationp6.2"></a>
- [ ] **P6.2 实现 A* 寻路与确定的不可达行为**
  - 延期原因：依赖 P6.1 的有向图和权重契约。
  - 启用条件：P6.1 完成并具有自动化测试入口。
  - 修改：实现节点到节点路径查询，定义启发式、平局处理、空路径和不可达结果。
  - 测试：最短路径、多条等价路径、断路、环、起终点相同和不存在节点。
  - 验收：固定输入得到确定路径；不可达或非法输入不会返回部分伪路径。

  - 关联引用：`traffic-simulation:P6.1`。
  - 来源 key：`todo:deferred:P6.2`。

<a id="traffic-simulationp6.3"></a>
- [ ] **P6.3 建立 RoadType 通行权重、容量与拥堵重算**
  - 延期原因：V3 只定义 RoadType 身份和视觉，不定义速度、容量或拥堵参数；这些模拟语义依赖 P6.1/P6.2。
  - 启用条件：`v3-road-graph:8.6`、P6.1 和 P6.2 完成，并确认四类道路的模拟参数与公式。
  - 修改：为每种类型定义速度、容量和基础权重；实现 `GetEdgeWeight`、`UpdateCongestion`、`RecalculateWeights`，并记录使用的拥堵公式。
  - 测试：四种类型基础权重、零/正常/过饱和流量、权重单调性和路径随拥堵切换。
  - 验收：相同长度下类型与拥堵产生可解释、可重复的权重，A* 使用最新权重。

  - 关联引用：`v3-road-graph:8.6`、`traffic-simulation:P6.1`、`traffic-simulation:P6.2`。
  - 来源 key：`todo:deferred:P6.3`。

<a id="traffic-simulationp6.4"></a>
- [ ] **P6.4 按已提交的 RoadGraph 变更增量同步模拟图**
  - 延期原因：`road-graph:4.4` 已保证复合操作完成清理后按稳定顺序发布事件，作为第二代基线；V3 的 split/merge/改造规范化可在一个事务内同时创建、移除和更新 Edge，模拟层应直接消费最终 `GraphChanged`，不能缓存过渡 ID。
  - 启用条件：`v3-road-graph:8.5`、`v3-road-graph:8.6` 和 P6.1 完成。
  - 修改：消费统一事务后变更摘要，增量添加/移除模拟边、更新类型权重、失效经过已删除 Edge 的缓存路径，并只重算受影响区域；self-loop/parallel Edge 按 Edge ID 独立同步。
  - 测试：铺路、拆路、交叉拆边、支路删除后合并、类型边界消失、self-loop/parallel Edge、存档全图重建及连续复合操作。
  - 验收：增量结果与从同一 RoadGraph 全量重建结果一致；消费者不会永久缓存中间拓扑。

  - 关联引用：`v3-road-graph:8.5`、`v3-road-graph:8.6`、`traffic-simulation:P6.1`。
  - 来源 key：`todo:deferred:P6.4`。

<a id="traffic-simulationp6.5"></a>

**P6.5 实现既有道路升级工具（已取代）**

  - 处置：已由 `v3-road-graph:8.5`、`v3-tool-input:2.2` 和 `v3-ui:1.2` 取代。V3 改造允许四类之间任意重分类，并可能在 semantic boundary 消失时合并 Edge；TrafficGraph 以后只消费提交后的 created/removed/updated 摘要，不拥有玩家工具。
  - 关联引用：`v3-road-graph:8.5`、`v3-tool-input:2.2`、`v3-ui:1.2`、`traffic-simulation:P6.4`。
  - 来源 key：`todo:deferred:P6.5`。

## 已解决基线

- [x] **第二代 RoadGraph 提供稳定模拟集成边界**
  - `road-graph:3.1`～`3.3` 已验证原生几何空间查询和 10k/100k 性能边界；`road-graph:4.1`～`4.4` 已统一图不变式、删除事务和提交后事件顺序。
  - `SubmitPath` 返回确定排序的 `RoadGraphChangeSummary`，单删、批量删除和全图恢复均有明确同步入口。
  - 该基线只解除 RoadGraph 技术前置条件，不代表 `TrafficGraph`、寻路、拥堵或 RoadType 已实现。

## 完成标准

- 本系统当前只有 P6.1～P6.4 是暂不执行项；P6.5 已取代。`v3-road-graph:8.6` 完成前不计入第三代里程碑。
