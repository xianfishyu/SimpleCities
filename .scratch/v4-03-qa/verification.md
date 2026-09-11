# V4-03 验证记录

- 对应GitHub #4，开工基线e9d5e2ce45fff35b77c680693834ef8bd2a9819e。旧注册重载、PreparedLoadWork包装和全局已解析保存根字段已移除；保存根及必需payload由场景装配的不可变策略提供。
- TDD：存储策略与V3装配入口缺失时编译失败，补充后存档契约46/46通过；自定义根与payload往返证明输入数组修改不会改变已捕获策略，未选择的参与者不被提交。
- 相关契约139/139，全量965/965；Debug与ExportRelease构建均0警告0错误；Roslyn编译及分析器诊断为空。
- 生产通用协调器/上下文/载荷源码未引用RoadGraph、RoadGraphRevision、RoadRendererPreparedLoad、ToolManager或RoadRenderer具体类型，也没有V3保存根/道路文件名常量。生产与测试代码中无旧注册重载、PreparedLoadWork或旧全局保存根字段引用。
- 保存、加载及删除操作都使用SceneRequest捕获的保存根；Debug存储故障注入从当前store获取同一目录，不回读可能已切换的场景根。

## 真实引擎验证

- Godot 4.7 Mono独立进程，项目路径保持当前工作区，音频使用Dummy；不修改项目设置。
- Forward+/Vulkan运行既有road_load_preflight_resource_failure_runtime_contract：退出0，明确PASS，覆盖V3正常保存加载、准备与资源预检失败隔离、参与者及槽位代际拒绝、原资源保留与释放。
- Forward+/Vulkan运行既有road_load_generation_runtime_contract：退出0，明确PASS，覆盖实际场景加载的代际一致性。
- Compatibility/OpenGL另有一次补充运行，相同预检失败契约退出0并PASS；该结果不替代上述项目渲染后端验证。
- 各脚本通过正常存档删除入口清理自己的临时槽位，三个独立运行进程及本次启动的编辑器均已退出。

## 验证边界与日志

- Godot MCP桥接被另一客户端占用，未进行该桥接的编辑器状态及DAP运行检查；没有终止其他客户端。真实行为结论依据独立Godot进程的结构化结果与明确PASS标记。
- 日志含一次ConstructionDock在场景初始化时找不到ToolManager的提示；后续工具及加载契约通过。清理期间还报告两份2026-08-16的旧损坏存档时间戳错误，未修改或删除它们。
- 原始stdout、stderr及引擎日志保存在本目录；预期注入失败由契约捕获，不等同于未处理的运行时错误。本文不声称所有环境检查或V4后续功能已经通过。

## 提交前评审与最终复验

- Spec评审发现跨保存根旧删除授权仍有效：两个临时根中克隆相同槽位后，旧摘要可重新授权，旧token可删除新根槽位。修复注册/注销边界，统一调用InvalidateSlotListing清除授权并推进列表代际；属于本次未提交实现的问题。
- 新增scene_storage_authorization_runtime_contract真实Forward+/Vulkan回归：authorization-red日志显示退出1、两个拒绝条件及两槽保留均false；authorization-green显示退出0、三项均true，并明确PASS。测试仅操作自己创建的临时目录，已清理；预期stale authorization错误代表拒绝成功。
- 新Debug探针和GDScript加入后，首次全量测试因QA导出排除遗漏出现964通过/1失败。补齐export_presets.cfg后再次全量965/965通过，无跳过。
- 最终ExportRelease构建0警告0错误，Roslyn含分析器诊断为空；程序集检查SceneStoragePolicyProbe为Debug存在、ExportRelease不存在。
- 修复后再次执行road_load_generation_runtime_contract，退出0且明确PASS，见generation-final日志；进程已退出。场景文件无附带修改。
- Standards复核0条硬违规、0条剩余建议；Spec复核0条剩余发现，原P2已确认修复。修复记录为docs/bugfix/save-system.md的save-system:BUG-16。
