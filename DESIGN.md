# SimpleCities Design System

## 1. Atmosphere & Identity

SimpleCities 的 UI 是**深色指挥台**：玩家像规划局长坐在一张发光的市政规划图前，界面只在需要下达命令、查看当前工具或确认系统状态时出现。视觉签名是“地图之上的低亮度控制层”：深炭色半透明表面、冷灰文字、低饱和琥珀交互强调，以及用 tonal-shift 而不是厚重描边或阴影区分层级。现有 HUD 已经形成了这一方向：`GameHUD.tscn` 使用接近 `Color(0.06, 0.07, 0.09, 0.92)` 的深色面板、`Color(1, 0.76, 0.35, 1)` 的琥珀标题、`Color(0.95, 0.95, 0.95, 1)` 的主文字和冷灰辅助文字；本设计系统将这些隐式值收敛为 Theme 可实现的令牌。

首个 live 阶段只呈现真实可用的道路能力：一个 Roads 分类，三个工具 Select / Road / RoadRemove。Zoning / 区域、Public Facilities / 公共设施、Transit / 交通、Landscaping / 景观只作为未来-only 架构方向记录；本阶段不会为它们生成按钮、空托盘、禁用占位或“即将推出”卡片。

## 2. Color

颜色实现以 Godot `Theme` 的 `Color`、`StyleBoxFlat.bg_color`、`StyleBoxFlat.border_color` 和控件 `font_color` 为准。令牌名是语义契约，不是 CSS 变量；写入 `.tres` 时使用十六进制或 Godot `Color(r, g, b, a)` 等价值。

| 角色 | Token | Hex / Alpha | Godot 用途 | 使用规则 |
| --- | --- | --- | --- | --- |
| Surface/canvas | `Surface.Canvas` | `#090B0F` / 1.00 | 根 `CanvasLayer` 上方 UI 的视觉基底参考 | 不覆盖游戏地图，只用于 HUD 暗部校准 |
| Surface/panel | `Surface.Panel` | `#0F1217` / 0.92 | `StyleBoxFlat.bg_color`，ConstructionDock、ContextPanel、DebugPanel 默认表面 | 来自现有 `Color(0.06, 0.07, 0.09, 0.92)` |
| Surface/raised | `Surface.Raised` | `#151A22` / 0.96 | ToolTray、弹出层、展开面 | 比 Panel 亮一级，表示可操作层 |
| Surface/control | `Surface.Control` | `#1B222C` / 1.00 | ToolButton default、CategoryBar item default | 可点击控件底色 |
| Surface/controlHover | `Surface.ControlHover` | `#243041` / 1.00 | Button hover `StyleBoxFlat.bg_color` | 只用于指针悬停 |
| Surface/controlPressed | `Surface.ControlPressed` | `#2B3748` / 1.00 | Button pressed / selected | 表示按下或当前工具 |
| Surface/disabled | `Surface.Disabled` | `#11151B` / 0.72 | disabled 控件背景 | 不用于当前 Roads 工具，保留给真实不可用状态 |
| Text/primary | `Text.Primary` | `#F2F4F7` / 1.00 | Label、Button 文本 | 正文和当前值 |
| Text/secondary | `Text.Secondary` | `#A7AFBA` / 1.00 | 说明、快捷键、ContextPanel 次级信息 | 对比度必须高于 4.5:1 |
| Text/muted | `Text.Muted` | `#808896` / 1.00 | DebugPanel 标签、非关键元数据 | 来自现有冷灰辅助文字 |
| Text/disabled | `Text.Disabled` | `#58606B` / 1.00 | disabled 文本 | 不与 hover/pressed 混用 |
| Accent/primary | `Accent.Primary` | `#FFC259` / 1.00 | 分类选中、标题、当前工具强调 | 来自现有琥珀标题色 |
| Accent/hover | `Accent.Hover` | `#FFD27A` / 1.00 | 主要交互 hover 文本或细线 | 面积小于按钮总面积的 20% |
| Accent/pressed | `Accent.Pressed` | `#D99B32` / 1.00 | 主要交互 pressed | 不用于警告或错误 |
| Accent/road | `Accent.Road` | `#6F7884` / 1.00 | 道路工具图标或道路类标签 | 呼应规划图道路深灰 `#37474F`，但在暗 UI 上提亮 |
| Status/success | `Status.Success` | `#52C878` / 1.00 | 保存成功、有效操作反馈 | 只用于状态反馈 |
| Status/warning | `Status.Warning` | `#FFC259` / 1.00 | 可恢复警告 | 与 Accent 同色但语义以状态文案区分 |
| Status/error | `Status.Error` | `#FF6B6B` / 1.00 | 读档失败、拆除危险确认 | 不用于普通 RoadRemove 按钮底色 |
| Status/info | `Status.Info` | `#7DA8FF` / 1.00 | 只读提示、帮助信息 | 不作为主要 CTA |
| Focus/ring | `Focus.Ring` | `#FFE08A` / 1.00 | `StyleBoxFlat.border_color` 或外层 FocusRect | 键盘焦点必须可见 |
| Border/subtle | `Border.Subtle` | `#242933` / 0.50 | 非层级分隔线、HSeparator | 来自现有边框色 |
| Border/focusOutside | `Border.FocusOutside` | `#5C4A25` / 1.00 | 焦点外圈低亮补边 | 与 Focus.Ring 成双使用 |

规则：所有新 UI 颜色必须从上表映射；需要新增颜色时先扩展本节，再写 Theme。游戏地图的未来分区色（住宅、商业、工业、水域等）属于地图渲染层，不进入当前 command-center HUD Theme。图标必须使用统一风格的矢量或纹理资源，推荐同一 stroke/填充规则；禁止用 emoji 作为按钮图标、状态图标或占位图形。

## 3. Typography

字体实现以 Godot `Theme/default_font`、`Theme/default_font_size` 和控件 `theme_override_font_sizes/font_size` 为准。最多两组字体：一组 UI Sans，一组 Mono；CJK 字形必须由同一字体资源或 Godot fallback 链覆盖，不能在单个按钮内混用第三套字体。

| Level | Size px | Weight | Line Height | Godot 用途 | 使用位置 |
| --- | ---: | --- | ---: | --- | --- |
| HUD Title | 16 | 600 | 20 | Section 标题、ContextPanel 当前工具名 | 少量标题，不全大写 |
| Button | 14 | 600 | 18 | ToolButton、CategoryBar、SystemControls | 默认交互文字 |
| Body | 14 | 400 | 20 | ContextPanel 说明正文 | 玩家可读说明，最低正文尺寸 |
| Label | 13 | 500 | 18 | 键值行左侧标签 | 延续现有 12-13px HUD 密度并提高可读性 |
| Caption | 12 | 400 | 16 | DebugPanel、快捷键提示、状态时间戳 | 只用于短文本 |
| Mono/Data | 12 | 500 | 16 | FPS、GraphEdge/GraphNode 数字、坐标 | 使用 Mono 字体 |

字体栈：

| Token | 字体 | CJK 策略 | 用途 |
| --- | --- | --- | --- |
| `Font.UI` | `Noto Sans CJK SC` 或系统 sans fallback | 优先覆盖简体中文、英文和数字 | 所有玩家可见 UI |
| `Font.Mono` | `JetBrains Mono` 或 Godot bundled mono fallback | CJK 回退到 `Font.UI` | DebugPanel 数字、坐标、快捷键 |

规则：玩家正文不低于 14px；12px 只用于短标签和调试数值。按钮文字采用“中文名称 + 快捷键”格式，例如 `铺路 R` 或 `拆路 E`；快捷键不必加括号，但同一容器内必须统一。英文枚举名 Select / Road / RoadRemove 可在 DebugPanel 或开发文档中出现，玩家主路径优先显示中文动词。

## 4. Spacing & Layout

所有尺寸基于 4px 网格，写入 Godot 时使用 `custom_minimum_size`、Container `separation`、`StyleBoxFlat.content_margin_*`、Control anchors 和 offsets。不得使用随机像素值修补视觉；若场景需要新间距，先在本节补充。

| Token | Value | Godot 属性 | 用途 |
| --- | ---: | --- | --- |
| `Space.1` | 4px | `separation` | 图标到文字、紧凑按钮内间隔 |
| `Space.2` | 8px | `content_margin_*` | 小容器内边距、行距 |
| `Space.3` | 12px | `offset` / margin | 面板边缘缓冲、组内间隔 |
| `Space.4` | 16px | dock 外边距 | HUD 与视口边界、安全区 |
| `Space.5` | 20px | panel padding | ContextPanel 内部主边距 |
| `Space.6` | 24px | group gap | 大组分隔、托盘顶部留白 |
| `Radius.panel` | 8px | `StyleBoxFlat.corner_radius_*` | 面板圆角，承接现有 6px 并系统化 |
| `Radius.button` | 6px | `StyleBoxFlat.corner_radius_*` | Button 默认 |
| `Stroke.1` | 1px | `border_width_*` | 只用于 focus、细分隔、非深度层级 |

核心布局约束：

| 区域 | 尺寸与锚点 | 规则 |
| --- | --- | --- |
| ConstructionDock | `anchor_left=0.5`、`anchor_right=0.5`、`anchor_bottom=1.0`；底部 offset 距视口 16px；折叠高度 64px；展开后包含 CategoryBar + ToolTray | 固定在底部中央，不随地图相机移动；宽度为 `clamp(520px, viewport_width - 32px, 920px)` |
| CategoryBar | 高度 48px；内部按钮最小 96x40px；左右 padding 12px；按钮间隔 8px | 始终可见；首轮仅 Roads 一个分类按钮；保存/加载不得放入此栏 |
| ToolTray | 位于 CategoryBar 上方，向上展开；最大高度 `floor(viewport_height / 3)`，同时不超过 240px；内容超出时内部滚动 | 当前 Roads 托盘只显示 Select、Road、RoadRemove；不显示未来分类占位 |
| ToolButton | 最小交互目标 44x36px；推荐 112x40px；图标区 20x20px；文字与快捷键间隔 8px | 鼠标和键盘都必须可用；焦点环不能被裁切 |
| ContextPanel | 右侧锚定：`anchor_right=1.0`；宽度范围 280-360px，推荐 320px；距右/上/下边 16px；高度按内容，最大 `viewport_height - 32px` | 只读解释当前工具和现有配置；不提供未来参数编辑 |
| SystemControls | 右上或 ContextPanel 顶部独立组；按钮最小 88x36px | Save / Load 是系统操作，不属于 ConstructionDock 或 ToolTray |
| DebugPanel | 默认 collapsed；展开宽度 260-320px；不遮挡 ConstructionDock；内容使用 Mono/Data | FPS、格点、RoadGroup/GraphEdge/GraphNode 计数移入此处 |

小窗口规则：当视口宽度低于 760px 时，ContextPanel 默认折叠为右侧 44px 宽图标/文本标签；ConstructionDock 保持底部，宽度为 `viewport_width - 24px`；ToolTray 仍不得超过视口高度三分之一。

## 5. Components

### ConstructionDock

- 结构：`Control` 根节点，底部锚定；包含 `ToolTray` 和 `CategoryBar`，两者共享 `Surface.Panel` / `Surface.Raised` tonal-shift。
- 责任：承载建造分类和当前分类工具，不处理保存、加载、调试指标或地图业务逻辑。
- 当前 live：只注册 Roads 分类，工具为 Select、Road、RoadRemove。
- 未来-only 架构：Zoning / 区域、Public Facilities / 公共设施、Transit / 交通、Landscaping / 景观只能在数据定义和文档中预留扩展步骤；在对应功能、资源、可用性和 QA 完成前不得渲染。

### CategoryBar

- 结构：`HBoxContainer`，高度 48px，位于 ConstructionDock 底部。
- Roads 状态：default 使用 `Surface.Control`；hover 使用 `Surface.ControlHover`；pressed/selected 使用 `Surface.ControlPressed` 加 `Accent.Primary` 文本或 2px 内侧强调线；focus 使用 `Focus.Ring` 1px 外框；disabled 使用 `Surface.Disabled` + `Text.Disabled`。
- 键盘：必须可 Tab 聚焦；Enter/Space 展开或收起当前分类；重复激活 Roads 只切换 ToolTray 可见性，不改变 `ToolManager.CurrentTool`。

### ToolTray

- 结构：`VBoxContainer` 或 `PanelContainer` 内嵌工具列表，位于 CategoryBar 上方。
- 行为：展开方向向上；显示当前分类工具；高度封顶为视口三分之一；内容多于可见空间时使用 Godot ScrollContainer。
- Roads 内容：Select、Road、RoadRemove 三个 ToolButton；不得显示道路等级、桥梁、隧道或未来工具。

### ToolButton

- 结构：`Button` 或可聚焦 `BaseButton` 派生控件，包含统一风格矢量/纹理图标、中文名称、快捷键提示。
- 状态：default `Surface.Control` + `Text.Primary`；hover `Surface.ControlHover` + `Accent.Hover` 细节；pressed `Surface.ControlPressed` + 轻微下沉 1px；selected `Surface.ControlPressed` + `Accent.Primary` 左/上 2px 指示；focus `Focus.Ring` + `Border.FocusOutside` 双层可见焦点；disabled `Surface.Disabled` + `Text.Disabled` 且不可响应。
- 可访问性：最小 44x36px；Tab 顺序为 CategoryBar → ToolTray → ContextPanel → SystemControls；焦点态必须在手柄/键盘模式下独立于 hover 显示。

### ContextPanel

- 结构：右侧 `PanelContainer`，标题、当前工具说明、快捷键、只读参数和提示分组。
- 当前内容：Select 显示“查看和取消操作”；Road 显示“拖拽铺设道路，R 快捷键”；RoadRemove 显示“点击已有 GraphEdge 拆路，E 快捷键”；可读取 `RoadConfig.CellSize` 等已有值，但不新增编辑控件。
- 状态：只读；信息状态用 `Status.Info`，失败或不可用提示才使用 `Status.Warning` / `Status.Error`。

### SystemControls

- 结构：独立 `HBoxContainer` 或 `VBoxContainer`，不作为 ConstructionDock 子节点。
- 内容：Save / Load 按钮与最近一次操作反馈；F5/F9 快捷键语义保留。
- 状态：保存成功使用 `Status.Success`；加载失败使用 `Status.Error` 文案；按钮本身仍遵循 ToolButton 的 default/hover/pressed/focus/disabled 状态。

### DebugPanel

- 结构：默认折叠的 `PanelContainer`，通过小型按钮展开；使用 `Font.Mono` 和 `Caption` / `Mono.Data`。
- 内容：FPS、鼠标格点、RoadGroup、GraphEdge、GraphNode 计数，以及必要的开发诊断。
- 规则：默认不向玩家暴露内部拓扑术语；展开后也不得占用 ConstructionDock 的底部操作空间。

## 6. Motion & Interaction

动效使用 Godot `Tween` 或 AnimationPlayer，优先改变 `modulate:a`、`position:y`、`scale` 等不会触发复杂布局重排的属性；Container 尺寸变化只在展开/收起开始和结束时计算，不逐帧写魔法 offset。

| 类型 | Duration | Easing | 用途 |
| --- | ---: | --- | --- |
| Micro | 80-120ms | `Tween.TransitionType.Sine` + `EaseType.Out` | Button hover、pressed 回弹 |
| Standard | 140-180ms | `Tween.TransitionType.Cubic` + `EaseType.Out` | ToolTray 展开/收起、ContextPanel 折叠 |
| Feedback | 700-1200ms | fade out | Save/Load 状态短提示 |

交互规则：

- 鼠标 hover 不能替代键盘 focus；所有可点击控件都必须有 default、hover、pressed、focus、disabled 五种 Theme 状态，当前工具另加 selected 表现。
- Esc 始终回到 Select；R 切到 Road；E 切到 RoadRemove；这些权威行为属于 `ToolManager`，UI 只发出命令并同步状态。
- ToolTray 展开不应改变当前工具；只有点击 ToolButton 或快捷键才改变工具。
- RoadRemove 的视觉可以使用错误色小面积提示危险性，但按钮整体仍保持 command-center 中性色，避免把普通拆路误读成系统错误。
- 反馈文案必须是玩家可理解的短句，例如“已保存 autosave”“读档失败：存档不存在或损坏”。

## 7. Depth & Surface

深度策略固定为 **tonal-shift**。层级通过表面亮度、透明度和少量焦点描边区分，不使用厚阴影建立 UI 层级。`StyleBoxFlat.shadow_*` 默认保持 0；需要悬浮感时先提高表面 token 一级，而不是添加投影。

| Level | Surface Token | Alpha | 用途 | 分隔方式 |
| --- | --- | ---: | --- | --- |
| L0 Map Overlay | `Surface.Canvas` | 0.00-0.20 | 非交互暗化或调试遮罩 | 不常驻 |
| L1 Dock Base | `Surface.Panel` | 0.92 | ConstructionDock 折叠主体、ContextPanel 默认 | tonal shift + 8px 圆角 |
| L2 Active Tray | `Surface.Raised` | 0.96 | ToolTray、展开层、DebugPanel 展开 | 比 L1 亮一级 |
| L3 Control | `Surface.Control` | 1.00 | ToolButton、CategoryBar item、SystemControls | 控件内部 tonal shift |
| L4 Interaction | `Surface.ControlHover` / `Surface.ControlPressed` | 1.00 | hover、pressed、selected | 状态色变化和 1px focus 边 |

允许的线条：`Border.Subtle` 仅用于内容分组、HSeparator 和非层级分隔；`Focus.Ring` 仅用于键盘焦点。禁止用多重边框或大面积纯黑遮罩制造“窗口感”。UI 应像贴在地图上的专业控制材料：轻、清楚、克制，但每个可操作层都能被玩家立即识别。
