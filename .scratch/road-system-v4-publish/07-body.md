## Parent

https://github.com/xianfishyu/SimpleCities/issues/1

## What to build

玩家拖出与已有道路共线重叠的草稿时看见准确的红色冲突区段，整笔建造不生效。

## Acceptance criteria

- [ ] 同异类型、正反方向、部分全部覆盖遵守同一正长度共线重叠规则，包含跨已合并折线边的覆盖。
- [ ] 仅标红本次草稿的冲突范围并说明原因，已有道路不被改色或修改；不能偷偷跳过覆盖后建设剩余部分。
- [ ] 完全重复建造也拒绝，不产生空变更、ID 消耗或历史记录。
- [ ] 端点接触和几何点交叉不按重叠拒绝；路面宽度交叠不作为中心线重叠依据。
- [ ] 预览判断和最终核心验证一致，来源过期不能绕过检查；拒绝前后公开状态及存档内容不变。

## Blocked by

- https://github.com/xianfishyu/SimpleCities/issues/7 — V4-06 从端点续建与转弯

<!-- simple-cities:v4-ticket:07 -->
