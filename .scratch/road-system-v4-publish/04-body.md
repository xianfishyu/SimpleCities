## Parent

https://github.com/xianfishyu/SimpleCities/issues/1

## What to build

在隔离 V4 场景中创建四档格长的空地图，并通过独立核心和通用存档装配保存、重新加载。

## Acceptance criteria

- [ ] 纯 .NET RoadCore 和核心测试独立构建，不引用 Godot，应用对核心源码只编译一次。
- [ ] 隔离场景可创建 8×8 km、中心原点、1 世界单位等于 1 米的地图，格长可选 25/50/100/200 米且默认 100 米。
- [ ] 格长创建后固定，空路网快照含明确版本与有效初始 ID watermark；不把格长当作显示缩放倍率。
- [ ] V4 独立格式和保存根能往返空地图及格长，错误版本或非法格长拒绝且不污染当前状态。
- [ ] Godot 中可查看地图并完成实际保存重载；正式主场景仍仅运行 V3。
- [ ] 从此票起建立核心公开操作与真实场景两层测试入口，后续行为沿用，不增加内部可写测试接口。

## Blocked by

- https://github.com/xianfishyu/SimpleCities/issues/4 — V4-03 收拢存档协调器

<!-- simple-cities:v4-ticket:04 -->
