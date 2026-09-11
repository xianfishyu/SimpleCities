## Parent

https://github.com/xianfishyu/SimpleCities/issues/1

## What to build

玩家可以按一次有效手势撤销或重做建造、局部编辑及批量编辑，最多保留最近64次。

## Acceptance criteria

- [ ] 一笔有效编辑对应一条领域变更历史，取消、失败、无变化不增加记录；undo/redo 共用保留记录。
- [ ] 超过64条时淘汰最旧记录；历史保存实际变化的领域内容，不保存绘图资源或逐笔全图副本。
- [ ] 撤销恢复内容但不倒退变更序列与 ID watermark，过期历史或跨 lineage 请求不能作用于当前路网。
- [ ] 闭环、局部删除及批量改造的撤销/重做与画面、位置和存档内容保持一致。
- [ ] 历史准备与核心发布协调，不依赖提交后普通回调成功；处理中不接受另一笔编辑或撤销。
- [ ] 记录历史成本供第20票测量，不把旧16 MiB直接冻结为新预算或擅自选择单笔超限策略。

## Blocked by

- https://github.com/xianfishyu/SimpleCities/issues/11 — V4-10 闭环、自环与不同路径平行边
- https://github.com/xianfishyu/SimpleCities/issues/15 — V4-14 拖选批量删除与改造

<!-- simple-cities:v4-ticket:15 -->
