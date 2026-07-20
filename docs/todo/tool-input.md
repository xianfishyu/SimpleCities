# 工具输入系统待办清单

> 系统 key：`tool-input`
> 复核日期：2026-07-19
> 证据：`.omo/backups/system-doc-split/docs/todo/todolist.md`、`.omo/evidence/split-system-docs/task-3/ownership-map.json`、当前工作区源码，以及旧版 `docs/todo/todolist.md`。
> 主导原则：负责玩家交互、网格吸附、半格输入规则和 `RoadType` 选择入口。

## 状态总览

| 遗留 ID | 发现 | 当前状态 | 处置方式 |
|---|---|---|---|
<a id="tool-input8"></a>
| 8 | `RoadBuilder` 仍有半格特殊分支 | 事实成立，但属于 UI 约束 | 当前不改；连续输入需求出现时再设计 |
<a id="tool-inputp2"></a>
| P2 | 连续空间、离散输入留在 UI 层 | 部分完成 | 由 0.7、2.1～2.3 清除数据层方向约束并固定节点容差 |

### 设计覆盖矩阵

| 设计范围 | 当前事实 | 关联待办或基线 |
|---|---|---|
<a id="tool-input3c3216c8f123"></a>
| §2 P2 连续空间 | `CellSize` 已退出数据层 API，但 `RoadGraph` 仍依赖 `DirectionUtil`，节点身份容差未形成公开契约 | 0.7、2.1～2.3 |

## 执行顺序

旧版执行顺序中没有任何活动复选框项属于该系统。

## 暂不执行

### RoadType 产品功能

<a id="tool-inputd5.3"></a>
- [ ] **D5.3 让 `RoadBuilder` 提交用户选择的 `RoadType`**
  - 当前判断：`RoadBuilder.EndDragAndCommit` 固定创建 Street；当前阶段不需要玩家选择其他道路类型。
  - 保留现状：不新增类型选择 UI，已有 `RoadType` 数据和旧存档兼容继续由 0.3 保护。
  - 重新开启条件：产品明确开放至少两种可建造道路类型，并确定选择交互。
  - 测试：默认 Street、每种开放类型传入 `AddRoad`、切换选择不修改既有 Edge、保存加载保持。
  - 验收：启用后 Builder 不再硬编码 Street；本项启用前不计入当前里程碑。

- 关联引用：`save-system:5.3`。
  - 来源 key：`todo:deferred:D5.3`。

<a id="tool-input35b9c59e1fd7"></a>
### 原问题 8：RoadBuilder 半格分支

<a id="tool-inputa371ae88d7d5"></a>
- [ ] **需求触发后再重新设计连续输入**
  - 当前判断：半格判断存在于 `RoadBuilder`，没有重新侵入 `RoadGraph`；这符合“离散化属于 UI 层”的核心分层。
  - 暂不修改原因：当前产品交互明确是 8 方向网格铺路，从非格点交叉口限制输入方向属于 UI 规则，不是数据层错误。
  - 重新开启条件：支持自由角度、曲线道路，或产品要求从任意交点向任意方向延伸。

  - 关联引用：`tool-input:D5.3`。
  - 来源 key：`todo:deferred:a371ae88d7d5`。

## 已解决基线

<a id="tool-inputdf59848d1fce"></a>
- [x] **`CellSize` 已从 RoadGraph API 移除。** 网格吸附和半格输入留在 `RoadBuilder` / `GridSystem`。
  - 关联引用：`tool-input:D5.3`。
  - 来源 key：`todo:baseline:df59848d1fce`。

## 完成标准

- 本系统当前仅包含暂不执行项；启用条件满足前不计入当前里程碑。
