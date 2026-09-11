## Parent

https://github.com/xianfishyu/SimpleCities/issues/1

## What to build

从已有端点继续画下一笔道路，形成可转弯的连续折线，同时保持规范道路边与显示的正确连接。

## Acceptance criteria

- [ ] 每笔仍沿一个合法方向，多笔相连形成折线，不把鼠标拖动轨迹当作折点列表。
- [ ] 相同类型的非结构二度连接可规范化合并，普通格点或转弯不强制永久保留节点。
- [ ] 不同类型连接保留必要结构分界，开放道路方向和反向续建结果稳定。
- [ ] 越界拖动停止在当前方向上界内最远合法格点，核心拒绝越界请求；表面可自然伸出地图边缘。
- [ ] 连通性、端帽和转弯显示、来源位置及保存重载通过核心和真实场景验证。

## Blocked by

- https://github.com/xianfishyu/SimpleCities/issues/6 — V4-05 建造并重载一条独立道路

<!-- simple-cities:v4-ticket:06 -->
