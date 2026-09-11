## Parent

https://github.com/xianfishyu/SimpleCities/issues/1

## What to build

在必要功能和性能证据具备后，将正式场景一次切换为唯一 V4 路网及编辑、显示和存档装配。

## Acceptance criteria

- [ ] 逐项核对规格的核心公开行为和真实Godot场景证据，测试边界及必要参数已冻结，不仅检查第20票是否关闭。
- [ ] 如果第20票未达标或仍有影响准入的未定参数，先形成经确认的具体优化/参数票，并把其作为本票原生阻塞；达标前不切换。
- [ ] 正式场景只实例化一套V4路网、工具、history、renderer和save participant，完整执行建造、格段删改、取消及保存加载。
- [ ] 保留可复现的切换前状态和明确回退办法，按已批准流程切换；不自动转换V3存档。
- [ ] 实际主场景完成核心操作、输入到可见结果及导出前检查，V3不再承担生产道路请求。
- [ ] 本票完成正式装配切换，剩余旧生产文件与导出残留由第22票收拢；不以临时双运行模式冒充完成。

## Blocked by

- https://github.com/xianfishyu/SimpleCities/issues/21 — V4-20 四档地图的性能与历史资源基线

<!-- simple-cities:v4-ticket:21 -->
