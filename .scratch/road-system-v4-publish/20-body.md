## Parent

https://github.com/xianfishyu/SimpleCities/issues/1

## What to build

以真实可构造路网测量完整编辑体验和历史成本，明确后续达标与资源预算的证据基础。

## Acceptance criteria

- [ ] 冻结10K计数对象、典型建造/拖选规模、四档格长、硬件、渲染设置与采样窗口，不把10K直接解释为一万个路口。
- [ ] 逐档验证数据集可构造，100K仅在合法可构造时记录压力结果；不使用非法几何凑数量。
- [ ] 分别报告帧时间P95/P99/最大值和超预算帧、后台准备/预检/发布耗时，以及输入到正确结果首次绘制的延迟。
- [ ] 对照144 FPS约6.9444 ms帧预算及100–300 ms响应目标给出明确达标/未达标结论，配置帧率上限不算证据。
- [ ] 测量代表性64次历史的估算与实际保留成本，提出有依据的字节预算、单笔超限策略和取消清理预算；待确认值明确标记。
- [ ] 未达标时定位具体瓶颈并提出可单独验收的优化票，不能把未知优化藏在本票内；后续切换仍受真实达标与参数确认约束。

## Blocked by

- https://github.com/xianfishyu/SimpleCities/issues/19 — V4-18 表现预检、失败提示与显示重试
- https://github.com/xianfishyu/SimpleCities/issues/20 — V4-19 局部拾取与查询工作量约束

<!-- simple-cities:v4-ticket:20 -->
