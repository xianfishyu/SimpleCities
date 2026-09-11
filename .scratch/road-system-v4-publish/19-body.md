## Parent

https://github.com/xianfishyu/SimpleCities/issues/1

## What to build

地图道路增多时，鼠标附近的道路查询和高亮仍只承担与局部候选相关的工作，并可解释超限原因。

## Acceptance criteria

- [ ] 空间索引从不可变快照可重建，只做粗筛；长线段按有界片段索引，来源区间与端点所有权不丢失。
- [ ] 固定局部路网不变时增加远端道路，不改变该局部查询的精确几何工作量，不静默退化成全图扫描。
- [ ] 分别报告 bucket、片段候选、精确检查、整边访问和命中结果数量，不能用结果数充当候选数。
- [ ] 候选预算超限、非法参数明确返回结果状态；用于高亮的查询不把超限当成可建或无道路。
- [ ] 闭环、不同路径平行边、密集路口与长道路的核心查询和真实拾取保持版本及格段范围一致。
- [ ] 记录可重现的公开查询和场景指标，交由第20票冻结性能数据集与时间门。

## Blocked by

- https://github.com/xianfishyu/SimpleCities/issues/11 — V4-10 闭环、自环与不同路径平行边
- https://github.com/xianfishyu/SimpleCities/issues/12 — V4-11 格段预选、拖选与高亮

<!-- simple-cities:v4-ticket:19 -->
