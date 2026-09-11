## Parent

https://github.com/xianfishyu/SimpleCities/issues/1

## What to build

通过多笔网格建造形成闭环或两节点之间的不同路径，显示、读取和保存仍保持正确道路语义。

## Acceptance criteria

- [ ] 多笔形成的纯环采用确定的 rooted seam 与方向，不因普通折点阻止必要合并。
- [ ] 自环在同一节点保留可区分的 A/B 端接，不同路径平行边按 EdgeId 区分，不按邻居节点去重。
- [ ] 带分支闭环及格心路口参与的环有明确连接结果，合法路径不被当作几何重复覆盖。
- [ ] 闭合路面、路口 owner 和位置参数正确，端接读取及几何转向描述保持可区分。
- [ ] 规范保存重载保持闭环和身份关系；核心与真实场景覆盖纯环、带分支环及不同路径平行边。

## Blocked by

- https://github.com/xianfishyu/SimpleCities/issues/10 — V4-09 格心路口与对角起建

<!-- simple-cities:v4-ticket:10 -->
