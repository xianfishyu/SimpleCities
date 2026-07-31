# 网格渲染系统待办清单

> 系统 key：`grid-rendering`
> 复核日期：2026-07-31
> 证据：`.omo/backups/system-doc-split/docs/todo/todolist.md`（已移除旧版待办的归档）、`.omo/evidence/split-system-docs/task-3/ownership-map.json` 与当前工作区源码。
> 主导原则：负责视觉配置、`RoadRenderer` 行为和 `RoadType` 样式输出。

## 状态总览

| 遗留 ID | 发现 | 当前状态 | 处置方式 |
| --------- | --------------------------------------------------- | -------------- | ------------------------------- |
| 无 | 没有任何旧版状态总览行属于该系统 | 无 | 见执行项或暂不执行项 |

### 设计覆盖矩阵

| 设计范围 | 当前事实 | 关联待办或基线 |
| --------------------------------------- | ------------------------------------------------------------------ | -------------------------------- |
| <a id="grid-rendering7a82ab6271cd"></a> |                                                                    |                                  |
| §8 渲染与道路分级 | 事件驱动渲染和节点绘制已完成；道路分级视觉和类型选择按当前需求延期 | 0.3；已解决基线；延期 D5.1～D5.3 |

## 执行顺序

旧版执行顺序中没有任何活动复选框项属于该系统。

### 阶段 5：兼容性命名整理，原问题 11

<a id="grid-rendering5.3"></a>
**5.3 Junction → Node 命名迁移的渲染侧**

- 范围：仅包含视觉资源字段 `JunctionRadius` 和 `JunctionColor`。
- 约束：存档字段 `Junctions`、`Segments` 和 `Roads` 仍归 `save-system:5.3` 负责；此项不重复 `save-system` 的文本。
- 验收：旧渲染资源可加载，新公开视觉术语使用 Node 命名，兼容性结果与 `save-system:5.3` 一致。
- 关联引用：`save-system:5.3`。
- 来源 key：`todo:item:5.3` 组合拆分。这里不新增额外的复选框状态。

## 暂不执行

<a id="grid-renderingb75f1d496647"></a>

### RoadType 产品功能

<a id="grid-renderingd5.1"></a>

- [ ] **D5.1 按产品需求定义 `RoadType` 分级样式**
  - 当前判断：`RoadRenderer` 统一使用 `RoadWidth`/`RoadColor`，但当前阶段不需要道路类型视觉差异。
  - 保留现状：继续把现有道路按默认 Street 数据处理；不新增 `RoadTypeStyle` 或类型专属渲染配置。
  - 重新开启条件：产品明确需要玩家识别不同道路等级，并确定至少颜色、宽度或其他可观察差异。
  - 验收：四种类型均映射到确定样式，配置缺失有稳定回退；本项启用前不计入当前里程碑。
  - 关联引用：`grid-rendering:D5.1`。
  - 来源 key：`todo:deferred:D5.1`。

<a id="grid-renderingd5.2"></a>

- [ ] **D5.2 让 `RoadRenderer` 按 `edge.Type` 渲染**
  - 延期原因：依赖 D5.1，当前统一样式符合现阶段产品需求。
  - 重新开启条件：D5.1 的样式契约获确认。
  - 测试：同场景四种类型、悬停高亮、保存加载后的视觉一致性。
  - 验收：`CreateEdgeLine` 使用类型样式，不影响独立的悬停和预览样式。
  - 关联引用：`grid-rendering:D5.2`。
  - 来源 key：`todo:deferred:D5.2`。

## 已解决基线

<a id="grid-rendering0854f0250cc2"></a>

- [X] **事件驱动 Edge 渲染与加载后全量重建已经落地。** `RoadRenderer.SetGraph` 监听 `EdgeAdded`、`EdgeRemoved`、`GraphCleared`。

  - 关联引用：`grid-rendering:D5.1`、`grid-rendering:D5.2`。
  - 来源 key：`todo:baseline:0854f0250cc2`。

## 完成标准

- 本系统当前仅包含暂不执行项；启用条件满足前不计入当前里程碑。
