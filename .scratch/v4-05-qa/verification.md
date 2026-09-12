# V4-05 独立道路验证

2026-09-12，GitHub #6，基线 `7c89aba1775c29a1e1e527603323f85c06449dca`。

## 交付范围

隔离 `Scenes/V4MapTest.tscn` 支持空图上一条独立道路。左键按下、拖动预览、松开提交；八方向、四类显示样式、表面来源位置、保存重载已接通。第二条道路请求明确拒绝，续建、交叉、合并、完整历史及性能指标仍由后续工单交付。正式MapTest未切换。

核心提供强类型NodeId、EdgeId、RoadProfileId和binary64位置。PlanBuild生成私有目标，只读RoadPlan允许表现准备；Build和Load统一使用TryCommit交换核心快照。后台计划与纯表面准备不访问Godot资源；主线程预检ArrayMesh的1个surface、4个顶点和6个索引，再同步发布核心与表现引用。

绘制和Hit使用同一份显式转换为binary32的四边形，命中返回来源token、EdgeId、参数和表面中心位置。悬停可提亮并显示道路位置；表面参数不冒充核心精确最近点。

## 核心与构建

- 八方向公开建造测试先因缺少入口失败，实现后通过。四类型往返测试曾4/4失败（重载节点数0，预期2），实体codec实现后通过。
- 格式升级为simple-cities-v4/schema 2，仍使用saves-v4根和road_network_v4.json。只接受空图或两节点/一边，限4096字节、JSON深度4；拒绝schema 1、未知字段、重复/悬空身份、非法坐标或类型、水位不一致。没有迁移器。
- 最终 `dotnet test SimpleCities.sln --no-restore --verbosity minimal`：核心48/48，既有965/965，无跳过。覆盖八方向发布、确定性往返、位置来源、无变化/拒绝不消耗身份、过期计划、边界方向约束、四种宽度、非法实体及极限水位。
- Debug构建与 `dotnet build SimpleCities.sln --configuration ExportRelease --no-restore --verbosity minimal` 均0警告0错误。Roslyn加载5个项目，compiler/analyzer诊断为空；两个V4运行脚本的LSP诊断为空。
- 新运行脚本已加入QA导出排除；核心仍通过ProjectReference使用，不重复编译核心源码，不引入Godot依赖。

## 真实引擎

- Godot 4.7 Mono，Forward+/Vulkan，音频Dummy。新runtime契约先因缺少道路类型选择器退出1，见runtime-red；实现后退出0且明确PASS，stderr为空。
- `v4_independent_road_runtime_contract.gd` 四类道路均验证真实按下/拖动/松开、预览期间零实体、结束后无残留手势、表面中点t=0.5、样式宽度内外命中边界、mesh已预检、来源token相同、存档端点/类型/格长恢复及Load新身份。结果见runtime-final。
- 八个方向的原始鼠标事件都建成预期端点；零长度不建造；Esc在草稿阶段和松开后主线程发布前均可取消，晚到结果没有发布。截图independent-road.png在frame_post_draw后取得，已检查道路表面、类型选择器及地图信息可见。
- schema更新后再次执行 `v4_empty_map_runtime_contract.gd -- --capture=res://.scratch/v4-05-qa/empty-map.png`：四档空图保存重载、错误版本/非法格长拒绝与当前状态保留、相机缩放和平移均通过，退出0、stderr为空。没有覆盖前票的历史截图。
- 测试脚本编写期间修正了OptionButton变量的类型推断及Esc变量声明顺序问题；最终LSP与实际执行均通过。

## 编辑器与诊断边界

MCP连接到V4MapTest，已从磁盘reload；类型选择器有效属性可读，运行时包含4项。编辑器内冻结启动、调整相机后，原始鼠标事件建造从(-300,0)到(300,0)的street：meshSurfaces=1、道路和Hit来源token一致、参数0.5、current=true、draft=false。编辑器增量错误检查无新错误，游戏已停止。

本次DAP缓冲始终为0，未捕获主动打印的标记，因此不把空DAP输出记为成功运行错误检查；运行错误结论依据独立CLI进程的stderr和结构化PASS。现有编辑器没有被关闭。

## Standards

初评0项硬违规/阻塞性设计建议；额外指出与Spec相同的持久化极限问题。修复后增量确认无剩余问题。

## Spec

初评1项P2：建造可产生reader拒绝的MaxValue水位/内容版本。新增公开API边界回归修复前3/3失败，修复后通过；见save-system:BUG-17。复核0项剩余问题。

## 清理

独立测试进程均已退出，编辑器测试游戏已停止；所有新建V4测试槽位通过正常删除入口清理，saves-v4仅剩框架根锁文件。没有修改V3用户存档。场景中的UID、unique_id及属性顺序变化由Godot资源保存生成，与本场景修改一同保留。
