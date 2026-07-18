# Grid and rendering system todo list

> System key: `grid-rendering`
> Review date: 2026-07-19
> Evidence: `.omo/backups/system-doc-split/docs/todo/todolist.md`, `.omo/evidence/split-system-docs/task-3/ownership-map.json`, current workspace source, and the legacy `docs/todo/todolist.md`.
> Principle: Owns visual configuration, RoadRenderer behavior, and RoadType style output.

## Status Summary

| Legacy ID | Finding | Current status | Disposition |
|---|---|---|---|
| none | No legacy status-summary row belongs to this system | none | See execution or deferred items |

### Design Coverage Matrix

| Design scope | Current fact | Related todo or baseline |
|---|---|---|
<a id="grid-rendering7a82ab6271cd"></a>
| §8 渲染与道路分级 | 事件驱动渲染和节点绘制已完成；道路分级视觉和类型选择按当前需求延期 | 0.3；已解决基线；延期 D5.1～D5.3 |

## Execution Order

No active checkbox item from the legacy execution order belongs to this system.

### Stage 5: compatibility naming cleanup, original issue 11

<a id="grid-rendering5.3"></a>
**5.3 Rendering side of the Junction to Node naming migration**
- Scope: visual resource fields `JunctionRadius` and `JunctionColor` only.
- Constraint: save fields `Junctions`, `Segments`, and `Roads` stay owned by `persistence:5.3`; this item does not duplicate persistence text.
- Acceptance: old rendering resources load, new public visual terms use Node naming, and the compatibility result matches `persistence:5.3`.
- Related refs: `persistence:5.3`.
- Source key: `todo:item:5.3` composite split. No extra checkbox status is created here.

## Deferred

<a id="grid-renderingb75f1d496647"></a>
### RoadType 产品功能

<a id="grid-renderingd5.1"></a>
- [ ] **D5.1 按产品需求定义 `RoadType` 分级样式**
  - 当前判断：`RoadRenderer` 统一使用 `RoadWidth`/`RoadColor`，但当前阶段不需要道路类型视觉差异。
  - 保留现状：继续把现有道路按默认 Street 数据处理；不新增 `RoadTypeStyle` 或类型专属渲染配置。
  - 重新开启条件：产品明确需要玩家识别不同道路等级，并确定至少颜色、宽度或其他可观察差异。
  - 验收：四种类型均映射到确定样式，配置缺失有稳定回退；本项启用前不计入当前里程碑。

  - Related refs: `grid-rendering:D5.1`.
  - Source key: `todo:deferred:D5.1`.

<a id="grid-renderingd5.2"></a>
- [ ] **D5.2 让 `RoadRenderer` 按 `edge.Type` 渲染**
  - 延期原因：依赖 D5.1，当前统一样式符合现阶段产品需求。
  - 重新开启条件：D5.1 的样式契约获确认。
  - 测试：同场景四种类型、悬停高亮、保存加载后的视觉一致性。
  - 验收：`CreateEdgeLine` 使用类型样式，不影响独立的悬停和预览样式。

  - Related refs: `grid-rendering:D5.2`.
  - Source key: `todo:deferred:D5.2`.

## Solved Baselines

<a id="grid-rendering0854f0250cc2"></a>
- [x] **事件驱动 Edge 渲染与加载后全量重建已经落地。** `RoadRenderer.SetGraph` 监听 `EdgeAdded`、`EdgeRemoved`、`GraphCleared`。

  - Related refs: `grid-rendering:D5.1`, `grid-rendering:D5.2`.
  - Source key: `todo:baseline:0854f0250cc2`.

## Completion Criteria

- 本系统当前仅包含延期项；启用条件满足前不计入当前里程碑。
