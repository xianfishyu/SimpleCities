## Parent

https://github.com/xianfishyu/SimpleCities/issues/1

## What to build

把现有存档协调、场景和工具的加载调用迁到通用契约，让完整 V3 加载流程通过新形式运行。

## Acceptance criteria

- [ ] 实际存档协调器、场景上下文和工具预备状态都使用通用入口，不仅新增一层未使用的接口。
- [ ] 正常保存加载和取消、预检失败保持原有外部结果，不改变 V3 存档代际或自动转换数据。
- [ ] 所有参与者准备完成后再发布，通知次序及工具清理仍受统一协调。
- [ ] 本票迁移调用方，保留旧形式供尚未收拢的内部连接使用；不删除 V3 正式道路实现。
- [ ] 按调用影响运行加载和工具回归，以及真实场景保存重载；本批次独立保持可验证。

## Blocked by

- https://github.com/xianfishyu/SimpleCities/issues/2 — V4-01 扩展通用加载契约

<!-- simple-cities:v4-ticket:02 -->
