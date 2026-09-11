## Parent

https://github.com/xianfishyu/SimpleCities/issues/1

## What to build

玩家能在道路上预选和拖选准确格段，即使底层是一条长规范道路边，也能看清实际作用区间。

## Acceptance criteria

- [ ] 道路格段由主网格间隔与路口截断共同定义，格心路口两侧独立，不等于整条 Edge 或整个方格。
- [ ] 悬停预选与按住后累积选择可区分，鼠标离开后已选范围保持，重复经过不重复加入。
- [ ] 路口中心有歧义时不新增选择，鼠标移向具体分支才选中；经过路口不自动扩散。
- [ ] 快速移动沿轨迹覆盖可选格段，不能仅用逐帧落点导致漏选。
- [ ] 选择只改变会话状态，高亮、表面来源与公开位置区间一致；Esc 清除选择不写路网。
- [ ] 真实输入验证长道路内部一格、格心半段及跨路口拖选；核心位置读取保持版本绑定。

## Blocked by

- https://github.com/xianfishyu/SimpleCities/issues/10 — V4-09 格心路口与对角起建

<!-- simple-cities:v4-ticket:11 -->
