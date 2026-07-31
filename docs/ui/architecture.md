# UI 架构

本文是当前可运行 UI 架构的事实来源。源码、场景和资源仍是最终事实来源；本文只记录已经实现并被测试覆盖的行为，不声明未实现系统。

## 运行时组成

`Scenes/UI/GameHUD.tscn` 是 HUD 组合根，作为 `Scenes/MapTest.tscn` 的 `GameHUD` 节点实例运行。`GameHUD` 是 `CanvasLayer`，脚本为 `Scripts/UI/GameHUD.cs`。

```text
GameHUD (CanvasLayer, Scripts/UI/GameHUD.cs)
+-- ConstructionDock (Scenes/UI/ConstructionDock.tscn, Scripts/UI/ConstructionDock.cs)
|   +-- DockPanel
|       +-- DockStack
|           +-- ToolTray
|           |   +-- TrayMargin
|           |       +-- ToolScroll
|           |           +-- ToolList
|           |               +-- RoadToolButton, only while Roads menu is rendered
|           |               +-- future disabled placeholders, only while a future category is rendered
|           +-- CategoryBar
|               +-- RoadsCategoryButton
|               +-- ZoningCategoryButton
|               +-- FacilitiesCategoryButton
|               +-- TransitCategoryButton
|               +-- LandscapingCategoryButton
+-- ToolContextPanel (Scripts/UI/ToolContextPanel.cs)
|   +-- PanelMargin/Rows/ContextFocusEntryButton
|   +-- PanelMargin/Rows/ContextContentScroll/ContextContent
+-- SystemControls (Scripts/UI/SystemControls.cs)
|   +-- PanelMargin/Controls/Buttons/SaveButton
|   +-- PanelMargin/Controls/Buttons/LoadButton
|   +-- PanelMargin/Controls/StatusLabel
+-- DebugPanel (Scripts/UI/DebugPanel.cs)
    +-- PanelMargin/Rows/DebugToggleButton
    +-- PanelMargin/Rows/DebugContent
```

`GameHUD._Ready()` resolves `ToolManager.Instance`, `RoadSystem.Instance.Graph`, and the exported `RoadConfig`. If `ToolManager`, `RoadSystem`, or `RoadConfig` is missing, it logs a warning and keeps the HUD usable with degraded tool, debug, or config display.

`UIManager` is a child of each `GameHUD`. `GameHUD.EnsureUIManager()` reuses an existing `UIManager` child or creates one named `UIManager`. `GameHUD.RegisterManagedPanels()` registers only `ContextPanel`, `DebugPanel`, and `SystemControls`. `ConstructionDock` is always visible and is never registered with `UIManager`. `UIManager` is not a process global singleton, and `tests/godot/command_center_runtime_contract.gd` checks that two HUD instances keep separate managers and panel registrations.

## ConstructionDock layout and states

`ConstructionDock` is a bottom flush, full width `Control` owned by `Scenes/UI/ConstructionDock.tscn` and `Scripts/UI/ConstructionDock.cs`.

Current serialized root and runtime layout contract:

| Item | Current value |
| --- | --- |
| Root anchors | `anchor_left = 0.0`, `anchor_top = 1.0`, `anchor_right = 1.0`, `anchor_bottom = 1.0` |
| Root offsets | `offset_left = 0.0`, `offset_right = 0.0`, `offset_bottom = 0.0` |
| Collapsed height | `CollapsedHeight = 76f` |
| Expanded height | `ExpandedHeight = 122f` |
| Category row height | `CategoryBarHeight = 76f`, serialized as `custom_minimum_size = Vector2(0, 76)` |
| Tool tray height | `ToolTrayHeight = 46f`, serialized as `custom_minimum_size = Vector2(0, 46)` |
| Category button size | `DockButtonWidth = 104f`, serialized as `custom_minimum_size = Vector2(104, 76)` |
| Asset list | `ToolList` is an `HBoxContainer` |
| Asset scrolling | `ToolScroll.horizontal_scroll_mode = 1`, `ToolScroll.vertical_scroll_mode = 0` |

`ConstructionDock.ApplyDockLayout()` reapplies the full width anchors, bottom flush offsets, collapsed or expanded height, `DockPanel` full rect anchors, `ToolTray` 46px minimum height, `ToolScroll` 46px minimum height, and `CategoryBar` 76px minimum height. There is no `MaximumWidth` clamp in the current dock source.

The dock has two high level visual states:

1. Collapsed, `ToolTray.Visible == false`, root height 76px.
2. Expanded, `ToolTray.Visible == true`, root height 122px, with the 46px asset strip above the 76px category row.

Clicking the active category toggles the shared tray without rebuilding its current menu. Switching to a different category renders that category menu and opens the shared tray. `ConstructionDock.TrayVisibilityChanged` notifies `GameHUD` whenever visibility changes.

## ConstructionDockButton reusable control

`Scenes/UI/ConstructionDockButton.tscn` is the reusable button scene. Its script is `Scripts/UI/ConstructionDockButton.cs`, and it derives from `Button`, so disabled state, toggle state, keyboard activation, focus, tooltip, and pressed semantics remain Godot native.

Stable scene shape:

```text
ConstructionDockButton (Button, Scripts/UI/ConstructionDockButton.cs)
+-- Presentation (VBoxContainer, mouse_filter = Ignore)
    +-- Icon (TextureRect, 32x32, mouse_filter = Ignore)
    +-- Label (Label, mouse_filter = Ignore)
    +-- SelectedUnderline (ColorRect, 3px high, mouse_filter = Ignore)
```

Exported properties:

| Property | Type | Role |
| --- | --- | --- |
| `IconTexture` | nullable `Texture2D` | Texture assigned to `Presentation/Icon` |
| `DisplayText` | `string` | Text assigned to `Presentation/Label` |
| `Selected` | `bool` | Shows `SelectedUnderline` and switches presentation colors |

Lifecycle behavior is idempotent. `_Ready()` resolves child nodes and calls `SynchronizePresentation()`. `_Notification(NotificationThemeChanged)` and `_Draw()` also synchronize presentation. `_ExitTree()` clears cached child references and marks the control not ready, so property setters before the next `_Ready()` do not touch stale nodes.

Color resolution uses the button's current theme type variation. `ConstructionDockButton.ResolvePresentationColors()` chooses disabled colors first, then selected colors, then primary colors. `ResolveThemeColor()` checks semantic names such as `selected_color`, `selected_label_color`, `disabled_color`, and `primary_color`, with font color fallbacks and `Colors.White` as the final fallback. The dock theme defines the `ConstructionDockButton` variation in `Scenes/UI/Themes/ConstructionDockTheme.tres`.

`ConstructionDock.BuildDockButtonPresentation()` builds the same `Presentation/Icon/Label/SelectedUnderline` structure for dynamic tray buttons and placeholders, so runtime created entries follow the reusable scene contract.

## Scene and resource owned icons

The five category icon references are scene owned. `Scenes/UI/ConstructionDock.tscn` declares these `Texture2D` resources and assigns them to the five category button instances:

| Category node | Display text | Texture resource |
| --- | --- | --- |
| `RoadsCategoryButton` | `道路` | `res://Assets/UI/Icons/construction-road.svg` |
| `ZoningCategoryButton` | `区域` | `res://Assets/UI/Icons/construction-zoning.svg` |
| `FacilitiesCategoryButton` | `公共设施` | `res://Assets/UI/Icons/construction-facilities.svg` |
| `TransitCategoryButton` | `交通` | `res://Assets/UI/Icons/construction-transit.svg` |
| `LandscapingCategoryButton` | `景观` | `res://Assets/UI/Icons/construction-landscaping.svg` |

The one live Roads asset icon is resource owned by `Scenes/UI/RoadsConstructionCategory.tres`, not hard coded in C#. Its `city-road` `ConstructionToolDefinition` serializes `Icon = ExtResource("3_road_icon")`, where that ext resource points to `res://Assets/UI/Icons/construction-road.svg`.

`Scripts/UI/ConstructionToolDefinition.cs` exports `Texture2D? Icon`, and a newly constructed definition has `Icon == null`. The nullable default is intentional so invalid or future data can exist without a placeholder texture path in code.

Godot imports each production SVG through generated sidecar files beside the source assets:

```text
Assets/UI/Icons/construction-road.svg
Assets/UI/Icons/construction-road.svg.import
Assets/UI/Icons/construction-zoning.svg
Assets/UI/Icons/construction-zoning.svg.import
Assets/UI/Icons/construction-facilities.svg
Assets/UI/Icons/construction-facilities.svg.import
Assets/UI/Icons/construction-transit.svg
Assets/UI/Icons/construction-transit.svg.import
Assets/UI/Icons/construction-landscaping.svg
Assets/UI/Icons/construction-landscaping.svg.import
```

Runtime resources must stay under `res://Assets/UI/Icons/`. The docs concept directory is not a runtime source. `tests/SimpleCities.RoadGraph.Tests/ConstructionDockContractTests.cs` and `tests/godot/command_center_runtime_contract.gd` both reject `res://docs/ui/concepts/` runtime coupling.

## Category and asset data flow

`ConstructionDock` has five built in category descriptors in `Scripts/UI/ConstructionDock.cs`:

| Category ID | Display name | Node name | Live behavior |
| --- | --- | --- | --- |
| `roads` | `道路` | `RoadsCategoryButton` | Renders the Roads catalog |
| `zoning` | `区域` | `ZoningCategoryButton` | Renders disabled placeholders |
| `facilities` | `公共设施` | `FacilitiesCategoryButton` | Renders disabled placeholders |
| `transit` | `交通` | `TransitCategoryButton` | Renders disabled placeholders |
| `landscaping` | `景观` | `LandscapingCategoryButton` | Renders disabled placeholders |

`ConstructionDock.BuildCategoryBar()` resolves each category button from `DockPanel/DockStack/CategoryBar`, writes its display text, sets `ToggleMode = true`, `FocusMode = FocusModeEnum.All`, `Disabled = false`, clears tooltip text, connects a pressed handler, and stores the button in `_categoryButtons` by category ID. Connected handlers are recorded in `_disconnectActions` and removed by `TeardownRuntimeState()`.

The live Roads data path is:

1. `Scenes/UI/ConstructionDock.tscn` exports `Category = ExtResource("3_category")`.
2. `Scenes/UI/RoadsConstructionCategory.tres` loads `Scripts/UI/ConstructionCategoryDefinition.cs` and contains one `ConstructionToolDefinition` subresource.
3. That subresource has `Id = "city-road"`, `DisplayName = "城市道路"`, `ShortcutHint = ""`, `ToolType = 1`, `Icon = ExtResource("3_road_icon")`, `SortOrder = 10`, and `Description = "拖拽铺设道路。"`.
4. `ConstructionDock.RenderRoadsMenu()` filters `Category.Tools` to `ToolType.Road`, sorts by `SortOrder`, and calls `AddToolButton()`.
5. `AddToolButton()` creates a `ConstructionDockButton` named `RoadToolButton`, assigns `DisplayText`, `IconTexture`, tooltip, focus, native toggle behavior, and a pressed handler that sets `ToolManager.CurrentTool = ToolType.Road`.
6. `GameHUD._Process()` updates `ToolContextPanel` from the current `ToolManager.CurrentTool` only while `ConstructionDock.UsesCatalogContext` is not false.

`ConstructionCategoryDefinition.TryValidate()` rejects an empty category ID, empty display name, null `Tools`, null tool references, invalid tool IDs or display names, and duplicate tool IDs. If validation fails, `ConstructionDock` hides the tray, disables all five category buttons, and logs a warning.

## Future placeholders

The four non Roads categories are current UI placeholders, not implemented gameplay. They are the only future categories rendered in the live CategoryBar.

| Category | Placeholder node names | Display text |
| --- | --- | --- |
| `zoning` | `ResidentialZonePlaceholder`, `CommercialZonePlaceholder` | `住宅区`, `商业区` |
| `facilities` | `SchoolPlaceholder`, `ClinicPlaceholder` | `学校`, `诊所` |
| `transit` | `BusStopPlaceholder`, `MetroStationPlaceholder` | `公交站`, `地铁站` |
| `landscaping` | `ParkPlaceholder`, `PlazaPlaceholder` | `公园`, `广场` |

`ConstructionDock.RenderPlaceholderMenu()` creates those entries as `ConstructionDockButton` instances with `Disabled = true`, `FocusMode = FocusModeEnum.None`, `CustomMinimumSize = new Vector2(104f, 46f)`, and `TooltipText = "尚未开放"`. It builds the same presentation children as live tool buttons. The placeholders do not register a pressed handler, do not create catalog entries, and do not alter `ToolManager.CurrentTool`, even if a test forces a `pressed` signal.

When a future category is active, `ConstructionDock.UsesCatalogContext` is false. The dock emits `ContextDisplayChanged(categoryDisplayName, false)`, and `GameHUD.OnDockContextDisplayChanged()` calls `ToolContextPanel.ShowUnavailableCategory(categoryDisplayName)`. The context panel shows the category name and unavailable text, hides shortcut information, and hides Roads specific config rows.

## ToolManager contract

`Scripts/Tools/ToolManager.cs` owns current tool state and input forwarding.

Current `ToolType` values are `Select`, `Road`, and `RoadRemove`. Only Escape is a keyboard tool reset:

| Input or action | Owner | Effect |
| --- | --- | --- |
| `Esc` | `ToolManager._Input()` | Sets `CurrentTool = ToolType.Select` |
| `R` | `ToolManager._Input()` | No tool switch |
| `E` | `ToolManager._Input()` | No tool switch |
| `RoadToolButton` | `ConstructionDock.OnToolPressed()` | Sets `ToolManager.CurrentTool = ToolType.Road` |
| Programmatic `CurrentTool = ToolType.RoadRemove` | Any caller with the instance | Supported state, no visible dock button |

`ToolManager._Input()` forwards input to `RoadBuilder.HandlePlaceInput()` only while the current tool is `Road`. It forwards input to `RoadBuilder.HandleRemoveInput()` only while the current tool is `RoadRemove`.

Switching away from `Road` calls `RoadBuilder.CancelPlaceDrag()`. Switching away from `RoadRemove` clears remove hover through `SetRemoveHoverActive(false)`. Switching into `RoadRemove` enables remove hover through `SetRemoveHoverActive(true)`. There is no `SelectToolButton` or `RoadRemoveToolButton` in `ConstructionDock`.

`ConstructionDock.TryGetBuiltInToolPresentation()` provides player facing fallback text for tools that are not catalog assets. The current built ins are `Select` with `选择`, `查看当前状态，取消建造操作。`, `Esc`, and `RoadRemove` with `拆路`, `点击已有道路进行拆除。`, and an empty shortcut hint.

## Context synchronization

Context synchronization has two modes:

1. Roads catalog mode, where `ConstructionDock.UsesCatalogContext` is true. `GameHUD._Process()` calls `ToolContextPanel.UpdateContext(currentTool, Config)`. For `ToolType.Road`, the context reads `ConstructionDock.Category` and `ConstructionToolDefinition` data, including `城市道路`, `拖拽铺设道路。`, and the empty shortcut hint. Empty shortcut hints hide the entire shortcut row.
2. Future category mode, where `ConstructionDock.UsesCatalogContext` is false. `ConstructionDock.NotifyContextDisplay()` emits the active category display name and false, and `GameHUD.OnDockContextDisplayChanged()` calls `ToolContextPanel.ShowUnavailableCategory(categoryDisplayName)`. While this mode is active, Escape still changes the underlying `ToolManager.CurrentTool` to `Select`, but the future category context remains the unavailable category context.

`GameHUD.ConfigureComponents()` also passes `ConstructionDock.Category` to `ToolContextPanel.SetCategory()`, so Roads context uses the same resource instance as the dock.

## Focus chain

`GameHUD.ConfigureFocusChain()` wires focus across the dock, context panel, system controls, and debug toggle. The exact forward chain depends on whether the tray is visible and whether an active tool button can receive focus.

Roads expanded:

```text
RoadsCategoryButton -> ZoningCategoryButton -> FacilitiesCategoryButton -> TransitCategoryButton -> LandscapingCategoryButton -> RoadToolButton -> ContextFocusEntryButton -> SaveButton -> LoadButton -> DebugToggleButton -> RoadsCategoryButton
```

Future category expanded:

```text
RoadsCategoryButton -> ZoningCategoryButton -> FacilitiesCategoryButton -> TransitCategoryButton -> LandscapingCategoryButton -> ContextFocusEntryButton -> SaveButton -> LoadButton -> DebugToggleButton -> RoadsCategoryButton
```

Collapsed:

```text
RoadsCategoryButton -> ZoningCategoryButton -> FacilitiesCategoryButton -> TransitCategoryButton -> LandscapingCategoryButton -> ContextFocusEntryButton -> SaveButton -> LoadButton -> DebugToggleButton -> RoadsCategoryButton
```

`ConstructionDock.UpdateFocusChain()` sets category focus order and appends `RoadToolButton` only when the tray is visible and the active category is Roads. `GameHUD.ConfigureFocusChain()` sets `ToolContextPanel` previous focus to `ConstructionDock.GetLastDockFocusControl()`, then wires `ContextFocusEntryButton -> SaveButton -> LoadButton -> DebugToggleButton -> RoadsCategoryButton`. Reverse traversal is expected to mirror the forward chain. Disabled future placeholders are non focusable and stay outside both directions of traversal.

## Responsive reservation and non overlap

`GameHUD` reserves space above the dock by reading the live dock top. `ApplyResponsiveLayout()` sets `ToolContextPanel.ReservedBottomTop = _constructionDock.Position.Y`, then calls `ToolContextPanel.ApplyResponsiveLayoutForViewport(viewportSize)` and places the top right panels.

Layout refresh is triggered by:

| Trigger | Code path |
| --- | --- |
| `GameHUD._Ready()` | `QueueResponsiveLayoutRefresh()` |
| viewport resize | `ConnectViewportResize()` and `OnViewportSizeChanged()` |
| dock tray visibility | `OnDockTrayVisibilityChanged()` |
| dock context display change | `OnDockContextDisplayChanged()` |
| panel resize | `WireLayoutSignals()` and `OnPanelResized()` |

`QueueResponsiveLayoutRefresh()` defers work to `ApplyResponsiveLayoutAfterContainersSettled()` and coalesces duplicate requests with `_layoutRefreshQueued`. This avoids stale panel geometry while Godot containers settle.

The runtime contract checks these target cases:

| Viewport | Dock states | Required outcome |
| --- | --- | --- |
| `1600x900` | collapsed and expanded | `ConstructionDock`, `ToolContextPanel`, `SystemControls`, and `DebugPanel` stay inside the viewport and pairwise non overlapping |
| `640x480` | collapsed and expanded | Same non overlap requirement; compact `ToolContextPanel` is 44px wide before expansion and uses scrollable content after expansion |

`tests/godot/command_center_runtime_contract.gd` also asserts `ToolContextPanel.ReservedBottomTop` matches `ConstructionDock.Position.Y` after ready, after tray changes, and after same instance reentry resize cycles.

## Same instance lifecycle reentry

`GameHUD._ExitTree()` disconnects viewport resize, system control events, dock events, and layout resize signals. It unregisters `ContextPanel`, `DebugPanel`, and `SystemControls` from its own `UIManager`, clears `_layoutRefreshQueued`, and calls `RequestReadyOnReentry()`.

`RequestReadyOnReentry()` calls `RequestReady()` on `GameHUD`, `ConstructionDock`, `ToolContextPanel`, `DebugPanel`, and `SystemControls`. This supports removing the same HUD instance from a tree and adding it again.

`ConstructionDock._EnterTree()` starts by calling `TeardownRuntimeState()`, then resolves nodes, rebuilds category handlers, validates category data, renders the active menu, sets tray visibility, syncs from `ToolManager`, and reapplies layout. `ConstructionDock._ExitTree()` also calls `TeardownRuntimeState()` and clears cached node references.

`TeardownRuntimeState()` disconnects every action in `_disconnectActions`, clears runtime tool buttons and definitions, clears category buttons, nulls `_toolManager`, resets sync flags, resets degraded logging, marks the category invalid, and resets `_activeCategoryId` to `roads`. This is what makes repeated `_EnterTree()` and `_ExitTree()` cycles idempotent. `tests/godot/command_center_runtime_contract.gd` exercises two same instance reentry cycles and verifies single press behavior for dock, debug, save/load, context, focus, and responsive reservation.

## Debug and System isolation

`DebugPanel` and `SystemControls` remain separate HUD panels. They use `Scenes/UI/Themes/CommandCenterTheme.tres`, not `Scenes/UI/Themes/ConstructionDockTheme.tres`. The dock local K theme applies to `ConstructionDock`, `ConstructionDockAssetStrip`, and `ConstructionDockButton`.

`DebugPanel` remains default collapsed through `DebugContent.visible = false` in `Scenes/UI/GameHUD.tscn`. It displays FPS, grid position, `RoadGroup`, `GraphEdge`, and `GraphNode` metrics. The runtime contract mutates the road graph and calls `DebugPanel.UpdateMetrics()` to verify those metrics continue to change after the dock work.

`SystemControls` remains right side and independent, with `SaveButton`, `LoadButton`, and `StatusLabel`. `GameHUD._Input()` still maps `F5` to save and `F9` to load, while `SystemControls.SaveRequested` and `LoadRequested` are wired to the same handlers.

## SVG import and concept boundary

Production icon SVGs live only under `Assets/UI/Icons/`. Godot generated `.svg.import` sidecars sit next to them and are import artifacts, not hand authored runtime contracts.

Concept SVGs under `docs/ui/concepts/` are references for design discussion and comparison. Runtime scenes, `.tres` resources, and C# source must not depend on `res://docs/ui/concepts/` or on copied concept resources. Current tests search `Scenes/UI/ConstructionDock.tscn`, `Scenes/UI/RoadsConstructionCategory.tres`, and `Scripts/UI/ConstructionDock.cs` for that forbidden runtime dependency.

## Test entry points

Current architecture tests are split across static source or scene contracts and Godot runtime contracts:

| Entry point | Coverage |
| --- | --- |
| `tests/SimpleCities.RoadGraph.Tests/ConstructionDockContractTests.cs` | Dock scene shape, five category resources, reusable button instances, 76/46/122 geometry, horizontal tray, missing old dock buttons, Roads catalog resource wiring, no runtime concept path |
| `tests/SimpleCities.RoadGraph.Tests/ConstructionCategoryDefinitionTests.cs` | `ToolType` enum shape, category validation, nullable exported `ConstructionToolDefinition.Icon` |
| `tests/SimpleCities.RoadGraph.Tests/ToolManagerContractTests.cs` | Escape only keyboard reset, R/E no op, road input forwarding, remove input forwarding |
| `tests/godot/roads_construction_category_contract.gd` | Runtime loading and validation of `Scenes/UI/RoadsConstructionCategory.tres` |
| `tests/godot/command_center_runtime_contract.gd` | Runtime category switching, placeholders, context sync, focus, lifecycle reentry, multi HUD isolation, responsive geometry, 1600x900 and 640x480 non overlap, K resources and states |

Focused .NET test command used by the plan:

```powershell
dotnet test tests/SimpleCities.RoadGraph.Tests/SimpleCities.RoadGraph.Tests.csproj --filter "FullyQualifiedName~ConstructionDockContractTests|FullyQualifiedName~ConstructionCategoryDefinitionTests|FullyQualifiedName~ToolManagerContractTests"
```

Godot contract commands used by the plan:

```powershell
godot --headless --path . --script tests/godot/roads_construction_category_contract.gd
godot --headless --path . --script tests/godot/command_center_runtime_contract.gd
```

## Known intentional warnings

Some runtime tests intentionally instantiate HUD or dock scenes without the full `Scenes/MapTest.tscn` dependency graph. The expected degraded warnings are:

| Warning source | Condition |
| --- | --- |
| `GameHUD: ToolManager.Instance is missing; tool display is degraded.` | Isolated HUD without `ToolManager` |
| `GameHUD: RoadSystem.Instance is missing; debug metrics are degraded.` | Isolated HUD without `RoadSystem` |
| `GameHUD: Config (RoadConfig resource) is not assigned; using fallback RoadConfig for UI display.` | Isolated HUD without exported config |
| `ConstructionDock: ToolManager.Instance is missing; tool commands are disabled until ToolManager exists.` | Standalone dock without `ToolManager` |
| `ConstructionDock: Category resource is not assigned; construction tools are disabled.` | Malformed dock test with null category |
| `ConstructionDock: Category resource is invalid: ...` | Invalid category validation path |

These warnings are intentional only for missing dependency and malformed resource test paths. They are not expected in the normal `Scenes/MapTest.tscn` HUD path.
