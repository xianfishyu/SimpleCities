# Traffic simulation system todo list

> System key: `traffic-simulation`
> Review date: 2026-07-19
> Evidence: `.omo/backups/system-doc-split/docs/todo/todolist.md`, `.omo/evidence/split-system-docs/task-3/ownership-map.json`, current workspace source, and the legacy `docs/todo/todolist.md`.
> Principle: Owns TrafficGraph, pathfinding, congestion, incremental sync, and upgrade simulation after graph contracts stabilize.

## Status Summary

| Legacy ID | Finding | Current status | Disposition |
|---|---|---|---|
<a id="traffic-simulationphase-6"></a>
| Phase 6 | `TrafficGraph`、A*、拥堵和增量同步 | 未实现，按路线图延期 | P6.1～P6.5 在当前 RoadGraph 契约稳定后启用 |

### Design Coverage Matrix

| Design scope | Current fact | Related todo or baseline |
|---|---|---|
<a id="traffic-simulation95732cb62c0e"></a>
| §9、§10 阶段 C、§11 Phase 6 | `TrafficGraph`、A*、拥堵、增量同步和道路升级工具均未实现 | 延期 P6.1～P6.5 |

## Execution Order

No active checkbox item from the legacy execution order belongs to this system.

## Deferred

<a id="traffic-simulationdef6b8230b9f"></a>
### Product Phase 6：交通模拟与道路升级

<a id="traffic-simulationp6.1"></a>
- [ ] **P6.1 构建 `TrafficGraph` 只读带权有向视图**
  - 延期原因：当前 RoadGraph 的空间查询、删除事务和事件契约尚未稳定，模拟层不应建立在会继续变化的拓扑契约上。
  - 启用条件：阶段 0～4 完成，并明确平行 Edge 与 `GetNeighborIDs` 的模拟语义。
  - 修改：从 `RoadGraph` 构建模拟专用的有向邻接和 Edge 映射；不得通过该视图写回 RoadGraph。
  - 测试：双向边映射、单向扩展策略、平行 Edge、图变更前后只读一致性。
  - 验收：模拟层可遍历带权有向图，且无法绕过 RoadGraph API 修改拓扑。

  - Related refs: `road-graph:4.4`, `road-graph:31883cfb1c78`.
  - Source key: `todo:deferred:P6.1`.

<a id="traffic-simulationp6.2"></a>
- [ ] **P6.2 实现 A* 寻路与确定的不可达行为**
  - 延期原因：依赖 P6.1 的有向图和权重契约。
  - 启用条件：P6.1 完成并具有自动化测试入口。
  - 修改：实现节点到节点路径查询，定义启发式、平局处理、空路径和不可达结果。
  - 测试：最短路径、多条等价路径、断路、环、起终点相同和不存在节点。
  - 验收：固定输入得到确定路径；不可达或非法输入不会返回部分伪路径。

  - Related refs: `traffic-simulation:P6.1`.
  - Source key: `todo:deferred:P6.2`.

<a id="traffic-simulationp6.3"></a>
- [ ] **P6.3 建立 RoadType 通行权重、容量与拥堵重算**
  - 延期原因：依赖道路分级体验和 P6.1/P6.2；当前 `RoadType` 只有枚举和存档数据。
  - 启用条件：道路类型产品需求获确认，D5.1、P6.1 和 P6.2 完成。
  - 修改：为每种类型定义速度、容量和基础权重；实现 `GetEdgeWeight`、`UpdateCongestion`、`RecalculateWeights`，并记录使用的拥堵公式。
  - 测试：四种类型基础权重、零/正常/过饱和流量、权重单调性和路径随拥堵切换。
  - 验收：相同长度下类型与拥堵产生可解释、可重复的权重，A* 使用最新权重。

  - Related refs: `grid-rendering:D5.1`, `traffic-simulation:P6.1`, `traffic-simulation:P6.2`.
  - Source key: `todo:deferred:P6.3`.

<a id="traffic-simulationp6.4"></a>
- [ ] **P6.4 按已提交的 RoadGraph 变更增量同步模拟图**
  - 延期原因：当前事件只描述逐 Edge 增删，复合拓扑操作的事务后事件顺序尚由 4.4 定义。
  - 启用条件：4.4 和 P6.1 完成。
  - 修改：消费事务后事件或批量变更摘要，增量添加/移除模拟边，失效经过已删除 Edge 的缓存路径，并只重算受影响区域。
  - 测试：铺路、拆路、交叉拆边、整组删除、存档全图重建及连续复合操作。
  - 验收：增量结果与从同一 RoadGraph 全量重建结果一致；消费者不会永久缓存中间拓扑。

  - Related refs: `road-graph:4.4`, `traffic-simulation:P6.1`.
  - Source key: `todo:deferred:P6.4`.

<a id="traffic-simulationp6.5"></a>
- [ ] **P6.5 实现既有道路升级工具**
  - 延期原因：道路升级当前不需要，并依赖 D5.1～D5.3 的 RoadType 产品体验以及 P6.3/P6.4 的权重和同步规则。
  - 启用条件：道路类型与升级产品需求获确认，D5.1～D5.3、P6.3 和 P6.4 完成，并明确升级成本与作用粒度。
  - 修改：允许玩家选择 Edge 或 RoadGroup 修改 `RoadType`，同步 Group/Edge 数据、渲染、存档和 TrafficGraph 权重。
  - 测试：单 Edge/整组升级策略、无效降级、保存加载、视觉刷新和寻路权重更新。
  - 验收：升级操作原子完成；失败无部分修改；成功后数据、视觉、存档和模拟权重一致。

  - Related refs: `grid-rendering:D5.1`, `grid-rendering:D5.2`, `tool-input:D5.3`, `traffic-simulation:P6.3`, `traffic-simulation:P6.4`.
  - Source key: `todo:deferred:P6.5`.

## Solved Baselines

No solved baseline from the legacy list belongs to this system.

## Completion Criteria

- 本系统当前仅包含延期项；启用条件满足前不计入当前里程碑。
