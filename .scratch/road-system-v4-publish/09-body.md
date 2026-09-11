## Parent

https://github.com/xianfishyu/SimpleCities/issues/1

## What to build

两条对角道路在方格中心自动形成格心路口，并允许玩家从该路口沿对角网格线继续建设。

## Acceptance criteria

- [ ] 四档格长下格心位置为两个轴各半格偏移，不把半格解释为固定半米。
- [ ] 格心路口是真实连通的结构节点，各分支来源和端接可稳定查询。
- [ ] 主格点允许八方向，格心仅允许对角起建；横竖起建及会生成非法分数格点的请求明确拒绝。
- [ ] 格心接入、边界终点、路口显示及拾取与实际核心位置一致。
- [ ] 核心输入/结果及真实建造、保存重载覆盖格心路口和允许/拒绝方向的代表性对照。

## Blocked by

- https://github.com/xianfishyu/SimpleCities/issues/9 — V4-08 主格点交叉与路口

<!-- simple-cities:v4-ticket:09 -->
