# V4 帧长尾定位与四档目标规模达标复验

## Parent

https://github.com/xianfishyu/SimpleCities/issues/1

## Suggested label

ready-for-agent

## Problem and outcome

当前 240 边基线已经观测到少量超过 144 FPS 对应 6.9444 ms 预算的渲染帧。最终四档 P99 均低于 6.9444 ms，但平移/悬停最大帧为 26.077/27.937/30.085/32.361 ms；当前所有编辑到首绘样本最大 22.7411 ms。主线程表现预检单次最大 7.2624 ms，已超过一帧预算；后台纯表现准备最大 16.2981 ms，引用及表现发布通常为微秒级。较小数据集存在长尾，需要在真实渲染中定位后，再以 25/50/100 米各 10K 规范道路边和 200 米 9,680 边满铺验证目标。144 FPS 和输入到正确结果首次绘制 100–300 ms 的目标均保持不变。

本票交付有因果证据的修复及可重复的达标/未达标结论。主线程 preflight、资源上传、GC、调度、绘制以及测试注入开销都可能影响帧间隔，不能预先把 preflight 认定为所有长尾根因。

## Investigation and implementation

从 #21 原始帧序列和操作时间戳定位长尾，区分静止、平移/悬停、建造、拖选改造、删除、历史与取消阶段。保持 domain、worker prepare、main-thread preflight、reference commit、presentation commit、首次 frame post draw 和端到端边界分离。针对被证据确认的瓶颈优化，保持旧状态可见、预检失败不污染活动状态、一次原子提交、选择范围及灰色背景网格的行为。

先用 240 边夹具复现长尾，再用扩容票交付的数据集验证规模效应。测试同时覆盖典型视野、密集路口、密集折线及地图边缘；200 米档满铺与补充折线场景分别报告。

## Acceptance criteria

- [ ] 记录长尾帧对应的实际场景、阶段和调用/资源证据；有修复前后同环境对照。没有找到原因的长尾保留为明确限制，不以猜测关闭。
- [ ] 四档目标规模经正常生产准入载入；渲染设备、硬件、引擎版本、渲染方法、分辨率、VSync/FPS 设置、预热与采样窗口完整可复现。
- [ ] 持续真实渲染记录 P95/P99/最大帧长及 >6.9444 ms 帧数/比例，保留原始序列和持续采样窗口；报告目标规模与每个补充场景结果，不能仅凭平均 FPS 或限制 `max_fps` 宣称达标。
- [ ] 为建造、典型拖选改造/删除及历史操作记录输入事件到对应新状态首次正确绘制的端到端延迟，并分别报告各准备/预检/提交阶段；对照 100–300 ms 目标说明通过、超限或未覆盖。
- [ ] 确认至少 P95≤6.9444 ms；同时公开 P99、最大值和超限帧，不通过删去操作帧或仅保留最好窗口隐藏长尾。残留明显卡顿须有具体处置及验收结论，不能只凭 P95 关闭。
- [ ] 真实 Esc 取消报告输入到工具恢复与后台退出两个边界，区分提交前取消成功和提交已经发生的竞态；不会将已提交状态回退为取消。
- [ ] 相关正确性/可视回归与实际编辑器输入验证通过；独立标记未能验证的门。若最终规模仍未达标，列出具体残留瓶颈及可独立验收工作，不将本票标为达标完成。

## Blocked by

- https://github.com/xianfishyu/SimpleCities/issues/21 — 最终基线及长尾原始证据。

- https://github.com/xianfishyu/SimpleCities/issues/24 — 目标规模复验依赖合法数据集与容量扩展；240 边长尾诊断可并行进行。

## Evidence

`docs/performance/v4-20-baseline.md`；`.scratch/v4-20-qa/render-25.json`、`render-50.json`、`render-100.json`、`render-200.json`；`tests/godot/v4_performance_baseline.gd`。

<!-- simple-cities:v4-followup:frame-tail-targets -->
