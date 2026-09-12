# V4-04 空地图验证

2026-09-12，GitHub #5，基线 `42c1a27583daa7d0217c7e04ff5e05c8b39e330f`。

## 可运行入口与范围

- `dotnet test tests/SimpleCities.RoadCore.Tests/SimpleCities.RoadCore.Tests.csproj` 独立运行核心测试，无 Godot 依赖。
- Godot 打开 `Scenes/V4MapTest.tscn`，F6 运行；新地图格长可选25/50/100/200米，默认100米。创建后地图格长固定，选择菜单只影响下一张地图。另存为后在列表中选槽位并加载；滚轮缩放，中键拖动。
- `RoadSaveParticipant` 将核心codec接到现有SaveManager与槽位事务。V4 payload为simple-cities-v4/schema 1，独立root为user://saves-v4；manifest与磁盘事务容器复用现有协议。空图载荷限4096字节，不实现实体、道路建造或后续性能验收。
- 正式主场景配置未改动。MCP实际默认启动确认res://Scenes/MapTest.tscn、RoadSystem存在、V4 View不存在、注册saveable为1。

## 核心和构建

- 创建测试先因RoadNetwork/MapDefinition缺失编译失败，再4/4通过；保存加载测试先因RoadCodec/PlanLoad缺失失败，再8/8通过。
- 最终独立核心24/24：四档米制空图、初始身份与watermark、确定性往返、Load新lineage与递增序列、跨实例/重复计划拒绝、非法字段/格长/版本/范围及损坏载荷拒绝。截断JSON测试最初过度要求精确异常类型，调整为接受JsonException派生类，行为要求未降低。
- `dotnet test SimpleCities.sln --no-restore --verbosity minimal`：核心24/24，既有套件965/965，无跳过。
- `dotnet build SimpleCities.sln --no-restore --verbosity minimal` 与 `--configuration ExportRelease`：均0警告0错误。
- Roslyn加载5个项目，compiler/analyzer诊断为空。Godot LSP对新runtime脚本诊断为空。
- MSBuild实际Compile清单中应用编译的核心源码条数为0；核心测试deps.json无Godot；`dotnet list SimpleCities.RoadCore/SimpleCities.RoadCore.csproj package --include-transitive` 无包依赖。核心源码目录用.gdignore和导出排除隔离，应用以ProjectReference消费程序集。

## 真实引擎验证

- Godot 4.7 Mono，Forward+/Vulkan，音频Dummy，脚本 `res://tests/godot/v4_empty_map_runtime_contract.gd`。
- 场景不存在时先退出1，日志runtime-red；实现后四档均保存并加载成功，日志runtime；最终runtime-final退出0、明确PASS，stderr为空。
- 最终脚本驱动格长选择、创建/保存/加载按钮；加载前创建另一档地图，加载后核心和presented格长都恢复存档值，版本一致。
- 对刚创建的50米临时槽位修改payload并同步更新manifest完整性元数据，实际触发V4 reader的版本/格长拒绝；两次均committed=false、resultKind=Failed，活动token、格长及表现一致性不变。载荷恢复后通过正式删除入口清理。
- 原始滚轮和中键拖动输入均生效，缩放未改变地图格长。`empty-map.png`在frame_post_draw后截图并人工检查：四方地图边界、米字网格、原点、地图信息和控件可见，无裁切。

## 编辑器与DAP

- 开始时无编辑器，启动本次拥有的编辑器后桥接暂被另一个客户端占用；后来连接恢复，未终止其他客户端。
- MCP确认当前V4MapTest场景、重新从磁盘加载资源，检查Camera2D实际position=(-1700,0)、zoom=(0.085,0.085)。新脚本初次扩展时出现zoom_works类型推断错误，补充显式bool后实际运行和LSP均通过；旧错误仍保留于editor.stderr历史日志。
- 编辑器冻结启动后通过按钮创建50米图并保存，创建200米图，再通过加载按钮恢复50米。结构化结果passed=true；DAP stdout捕获 `V4_EDITOR_QA_RESULT {"cell":50,"passed":true,"presented":50}`，stderr和console为空。编辑器增量错误检查无新错误。
- 清理时一次step_until谓词引用了错误holder路径，工具返回谓词错误；改用已知holder上下文查询，确认正式删除成功且slot_retained=false。随后移除holder元数据并停止游戏。此工具谓词错误不记为产品回归。
- 默认主场景验证时一次三帧步进超时，后续实际场景读取与DAP确认V3装配；不把超时步进记为通过。

## Standards

独立评审0个可行动问题；核心边界、不可变内容、单写者发布和领域术语符合本票。

## Spec

独立评审0项问题；核对#5全部验收项，槽位容器复用不构成V3道路payload兼容，实体操作继续留待后续票。

## 清理

所有测试新建槽位已通过删除入口清理，saves-v4仅剩事务框架正常的根锁文件。没有修改V3用户存档。游戏已停止，本次启动的编辑器已正常关闭，独立运行进程均已退出。编辑器导入生成新脚本UID，并补齐前票两个存档授权回归脚本的UID，作为资源元数据保留。
