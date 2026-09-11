## Parent

https://github.com/xianfishyu/SimpleCities/issues/1

## What to build

清除切换后不再使用的旧道路生产入口，验证正式运行与导出只依赖 V4，完成迁移收口。

## Acceptance criteria

- [ ] 移除旧道路写入口、reader/writer、重复事件及临时兼容形式，核心及应用仅保留规定的V4生产装配。
- [ ] 旧曲线或V3道路实现不作为常驻生产路径导出；历史说明可保留，相关旧测试按新契约替换或明确退出。
- [ ] 根工程、程序集引用、资源和导出过滤均与实际目录一致，不重复编译核心源码。
- [ ] 清理后复验核心测试、构建、编辑器资源、真实主场景、存档和导出；适用门缺失时不得标记全量通过。
- [ ] 公开记录已完成范围、验证证据、保留的历史资料和仍未解决事项，不能通过降低性能或正确性标准收口。
- [ ] 规格父issue保持不变，本票完成状态不等于自动关闭父issue或授权额外Git提交/推送。

## Blocked by

- https://github.com/xianfishyu/SimpleCities/issues/22 — V4-21 正式场景切换到 V4

<!-- simple-cities:v4-ticket:22 -->
