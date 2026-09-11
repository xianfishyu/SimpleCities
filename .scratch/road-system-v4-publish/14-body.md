## Parent

https://github.com/xianfishyu/SimpleCities/issues/1

## What to build

按住鼠标拖选多处格段，抬起后把整次删除或类型改造作为一笔操作执行。

## Acceptance criteria

- [ ] 选中区间稳定累积和去重，跨多条规范道路边、路口两侧及往返拖动保持准确高亮。
- [ ] 按住期间不写路网，抬起冻结整组区间并完成所需切分、删除/改造与规范化后一次发布。
- [ ] 局部删除保留所有未选区间；升级混合选择只改变不同目标类型的区间且不弹无变化提示。
- [ ] 整笔无变化、取消或失效选择不产生状态变更；不能先提交一部分再因后续失败回滚。
- [ ] 形成一份完整变更供历史消费，并验证真实拖动、快速轨迹、结果显示及保存重载。

## Blocked by

- https://github.com/xianfishyu/SimpleCities/issues/13 — V4-12 单格段删除
- https://github.com/xianfishyu/SimpleCities/issues/14 — V4-13 单格段道路类型改造

<!-- simple-cities:v4-ticket:14 -->
