## Parent

https://github.com/xianfishyu/SimpleCities/issues/1

## What to build

在类型改造工具中选择一个格段并抬起，仅将该区间变为目标道路类型。

## Acceptance criteria

- [ ] 四种内置类型任意双向互换，局部 profile 变化按需建立分界，不按枚举顺序限制升级方向。
- [ ] 只处理选中格段及路口截断后的范围，其余道路和分支保持原类型。
- [ ] 已是目标类型时静默结束，不切分、不交换快照、不递增 token、不消耗 ID 或产生变更历史。
- [ ] 高亮范围与最终宽度/颜色及实际类型变化一致，改变类型不等于重叠建造。
- [ ] 核心、实际鼠标操作和保存重载覆盖双向改造、无变化及长边中间一格；多选批量执行留给第14票。

## Blocked by

- https://github.com/xianfishyu/SimpleCities/issues/12 — V4-11 格段预选、拖选与高亮

<!-- simple-cities:v4-ticket:13 -->
