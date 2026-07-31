# SimpleCities UI Design System

This is the canonical design system for the current SimpleCities command-center HUD. Runtime facts come from Godot scenes, resources, C# code, and tests. Concept SVGs in `docs/ui/concepts/` are historical discussion artifacts only. They are not production UI assets, are not loaded by runtime scenes, and must not be referenced from Godot resources.

## 1. Atmosphere and identity

SimpleCities uses a dark command-center HUD over the city map. The UI should feel like a low-light planning console: restrained surfaces, clear CJK labels, small tonal shifts for state, and no decorative bulk. Controls appear only when the player needs to build, inspect context, save, load, or read debug metrics.

The broader HUD theme keeps the historical amber command-center accent for ContextPanel, PauseMenu, DebugPanel, status feedback, and shared controls. ConstructionDock uses the same historical command-center neutral and amber palette while retaining its local Theme resource for scope isolation, so the full-width construction surface can be maintained without restyling Debug or Context.

Current live build scope:

| Area | Current runtime truth |
| --- | --- |
| ConstructionDock | Full-width, bottom-flush K dock with 76px collapsed height and 140px expanded height |
| Category tabs | Five enabled and focusable tabs: `道路`, `区域`, `公共设施`, `交通`, `景观` |
| Roads asset | One enabled, focusable asset named `城市道路`, mapped to `ToolType.Road` |
| Future assets | Disabled, non-focusable placeholders only. They show `尚未开放` and have no gameplay side effects |
| Debug, System, Context | Preserve existing scene ownership, theme boundary, placement, behavior, and command-center amber/shared tokens |

## 2. Color tokens

Colors are Godot theme contracts. Use `Theme` colors, `StyleBoxFlat.bg_color`, `StyleBoxFlat.border_color`, and control font or icon colors. Token names describe intent, not CSS variables.

### 2.1 Broader command-center tokens

These tokens apply to ContextPanel, PauseMenu, DebugPanel, non-dock HUD surfaces, and future shared controls.

| Role | Token | Hex or alpha | Godot use | Rule |
| --- | --- | --- | --- | --- |
| Surface canvas | `Surface.Canvas` | `#090B0F` / 1.00 | Root HUD darkness reference | Never cover the game map as a permanent flat layer |
| Surface panel | `Surface.Panel` | `#0F1217` / 0.92 | ContextPanel and DebugPanel default surface | Existing command-center surface |
| Surface raised | `Surface.Raised` | `#151A22` / 0.96 | Expanded or elevated HUD surfaces | Use tonal shift instead of shadow |
| Surface control | `Surface.Control` | `#1B222C` / 1.00 | Shared buttons and controls | Default clickable state outside ConstructionDock |
| Surface hover | `Surface.ControlHover` | `#243041` / 1.00 | Shared button hover | Pointer state only |
| Surface pressed | `Surface.ControlPressed` | `#2B3748` / 1.00 | Shared pressed or selected state | Use with a structural selected mark when selection matters |
| Surface disabled | `Surface.Disabled` | `#11151B` / 0.72 | Disabled shared controls | Pair with disabled text |
| Text primary | `Text.Primary` | `#F2F4F7` / 1.00 | Labels and readable body text | Player-facing body text |
| Text secondary | `Text.Secondary` | `#A7AFBA` / 1.00 | Explanations and secondary values | Keep contrast above 4.5:1 |
| Text muted | `Text.Muted` | `#808896` / 1.00 | Debug labels and metadata | Short, low-priority text |
| Text disabled | `Text.Disabled` | `#58606B` / 1.00 | Disabled text | Never use for hover or selected state |
| Accent primary | `Accent.Primary` | `#FFC259` / 1.00 | Shared HUD headings and selected details | Amber remains the broader command-center accent |
| Accent hover | `Accent.Hover` | `#FFD27A` / 1.00 | Shared hover details | Use in small areas only |
| Accent pressed | `Accent.Pressed` | `#D99B32` / 1.00 | Shared pressed detail | Not an error color |
| Accent road | `Accent.Road` | `#6F7884` / 1.00 | Road labels or map-adjacent road affordances | Not a dock selection color |
| Status success | `Status.Success` | `#52C878` / 1.00 | Save success and valid feedback | Status only |
| Status warning | `Status.Warning` | `#FFC259` / 1.00 | Recoverable warnings | Text must clarify warning meaning |
| Status error | `Status.Error` | `#FF6B6B` / 1.00 | Load failure or destructive warning | Not for normal RoadRemove presentation |
| Status info | `Status.Info` | `#7DA8FF` / 1.00 | Read-only help or hints | Not a main CTA |
| Focus ring | `Focus.Ring` | `#FFE08A` / 1.00 | Shared keyboard focus | Must be visible without hover |
| Border subtle | `Border.Subtle` | `#242933` / 0.50 | Separators and grouping lines | No heavy window borders |

### 2.2 ConstructionDock K-local tokens

These tokens are local to `Scenes/UI/Themes/ConstructionDockTheme.tres`. They intentionally mirror the historical command-center neutral surfaces and amber interaction accents while staying scoped to the dock resource.

| Role | Token | Hex or alpha | Implementation truth |
| --- | --- | --- | --- |
| Dock base | `ConstructionDock.Base` | `#0F1217` / 0.92 | `ConstructionDock/colors/base_color`, outer dock panel |
| Asset strip | `ConstructionDock.AssetStrip` | `#151A22` / 0.96 | `ConstructionDockAssetStrip/colors/asset_strip_color` |
| Divider | `ConstructionDock.Divider` | `#242933` / 0.50 | Top strip divider and separator style |
| Primary icon and label | `ConstructionDock.Primary` | `#F2F4F7` / 1.00 | Default icon and label color |
| Selected icon, label, and indicator | `ConstructionDock.Selected` | `#FFC259` / 1.00 | Selected icon/label and the primary category's 4px bottom indicator |
| Disabled | `ConstructionDock.Disabled` | `#58606B` / 1.00 | Disabled icon and label color |
| Hover accent | `ConstructionDock.HoverAccent` | `#FFD27A` / 1.00 | Hover icon and label color |
| Hover surface | `ConstructionDock.HoverSurface` | `#243041` / 1.00 | Button hover tonal shift |
| Pressed surface | `ConstructionDock.PressedSurface` | `#2B3748` / 1.00 | Selected or pressed button background |
| Disabled surface | `ConstructionDock.DisabledSurface` | `#11151B` / 0.72 | Disabled button background |
| Focus | `ConstructionDock.Focus` | `#FFE08A` / 1.00 | Independent 1px keyboard focus ring |

ConstructionDock has two selection levels. A primary category uses the pressed surface, selected icon/label colors, and one 4px amber indicator anchored to the absolute dock bottom. A secondary tool uses the pressed surface plus selected icon/label colors and never draws an underline. Keyboard focus stays independently visible through the 1px ring at both levels.

## 3. Typography and CJK

Use at most two font families: one UI sans family and one mono family. The UI sans stack must cover Simplified Chinese, English, and numbers through the same resource or Godot fallback chain.

| Level | Size px | Weight | Line height | Godot use |
| --- | ---: | --- | ---: | --- |
| HUD title | 16 | 600 | 20 | ContextPanel section titles |
| Button | 14 | 600 | 18 | Shared buttons outside the K dock |
| Body | 14 | 400 | 20 | Player-readable descriptions |
| Label | 13 | 500 | 18 | K dock labels, key-value labels, short metadata |
| Caption | 12 | 400 | 16 | DebugPanel labels and timestamps |
| Mono data | 12 | 500 | 16 | FPS, graph counts, coordinates |

ConstructionDock category labels are exact CJK strings: `道路`, `区域`, `公共设施`, `交通`, `景观`. The Roads asset label is `城市道路`. Future placeholder tooltip text is `尚未开放`. No label may be replaced by an emoji, English enum, or icon-only affordance in the player path. CJK text must not clip at 1600x900, 640x480, or 435x480.

## 4. Spacing and layout

All dimensions use a 4px grid unless a runtime scene contract below gives an exact size.

| Token | Value | Godot use |
| --- | ---: | --- |
| `Space.1` | 4px | Icon-to-label and compact internal gaps |
| `Space.2` | 8px | Button padding and dock item separation |
| `Space.3` | 12px | Panel edge buffer and grouped content |
| `Space.4` | 16px | Shared HUD edge margin outside the bottom dock |
| `Space.5` | 20px | ContextPanel main padding |
| `Space.6` | 24px | Larger group gaps |
| `Radius.Panel` | 8px | Shared command-center panels outside ConstructionDock |
| `Radius.Button` | 6px | Shared buttons outside ConstructionDock |
| `Stroke.1` | 1px | Focus rings and subtle separators |

### 4.1 Live ConstructionDock geometry

The K dock is a full-width bottom surface. It is not a narrow floating panel.

| Node or area | Live contract |
| --- | --- |
| `Scenes/UI/ConstructionDock.tscn` root | `anchor_left = 0.0`, `anchor_right = 1.0`, `anchor_bottom = 1.0`, left/right/bottom offsets `0.0` |
| Collapsed dock | Exactly 76px tall, bottom flush with the viewport |
| Expanded dock | Exactly 140px tall, consisting of 64px asset strip plus 76px category row |
| `ToolTray` | 64px `PanelContainer` named asset strip, above the category row |
| `ToolScroll` | Full-width horizontal scrolling enabled without a visible scrollbar; vertical scrolling disabled |
| `ToolList` | Expanding, center-aligned `HBoxContainer`; the complete secondary group centers against the dock, not the active category |
| `CategoryScroll` / `CategoryBar` | 76px category viewport and centered `HBoxContainer`, five category controls, 8px separation; overflow can scroll without reserving scrollbar height |
| Category buttons | Up to 104x76; shrink to a 72px minimum on narrow viewports, icon over label, focusable and enabled |
| Category icon | 32x32 `TextureRect`, above its label |
| Secondary tool | 104x64 with a 24x24 icon and no selection underline |
| Primary indicator | 4px `ColorRect`, direct child of the selected category button and anchored to the absolute dock bottom |

At 1600x900, 640x480, and 435x480, the dock spans the viewport width and stays flush with the bottom edge in both collapsed and expanded states. It keeps the same 76px or 140px height, all five categories remain visible at 435px, and the secondary group remains globally centered. ContextPanel may use its compact 44px behavior, but ContextPanel, DebugPanel, and either dock state must remain inside the viewport and not overlap each other.

## 5. Components

### 5.1 ConstructionDock

Runtime owner paths:

| Kind | Path |
| --- | --- |
| Scene | `Scenes/UI/ConstructionDock.tscn` |
| Script | `Scripts/UI/ConstructionDock.cs` |
| Local theme | `Scenes/UI/Themes/ConstructionDockTheme.tres` |
| Category data | `Scenes/UI/RoadsConstructionCategory.tres` |
| Reusable category control | `Scenes/UI/ConstructionDockButton.tscn` and `Scripts/UI/ConstructionDockButton.cs` |

ConstructionDock owns only construction category and asset selection. It does not own Save, Load, Debug metrics, ContextPanel content layout, map logic, or future gameplay. It sends tool commands and synchronizes visible state with `ToolManager`.

Lifecycle rules:

| Moment | Required behavior |
| --- | --- |
| `_EnterTree()` | Resolve nodes, validate category resources, build category buttons, render active menu, sync with ToolManager, apply layout |
| `_Process()` | Sync selected presentation with `ToolManager`, including changes from the dock or configurable Q/R/E actions |
| Resize notification | Reapply dock layout and preserve 76/140 height truth |
| `_ExitTree()` or reentry | Disconnect signals, clear runtime tool buttons, clear dictionaries, reset active state safely |

Reentry must not duplicate signals, retain stale buttons, or leave old focus paths. Malformed or missing category resources degrade by disabling construction tools with a warning rather than throwing.

### 5.2 ConstructionDockButton

The reusable dock button scene structure is stable and testable:

```text
Button ConstructionDockButton
  VBoxContainer Presentation
    TextureRect Icon
    Label Label
  ColorRect PrimarySelectionIndicator (absolute bottom overlay, 4px)
```

The `Button` keeps native disabled, toggle, keyboard activation, tooltip, and focus semantics. `Presentation`, `Icon`, `Label`, and `PrimarySelectionIndicator` use `MouseFilter.Ignore` so the parent button receives pointer input. `VisualRole` explicitly distinguishes `PrimaryCategory` from `SecondaryTool`: selected primary buttons show the indicator, while selected secondary buttons use only their surface and presentation colors.

### 5.3 Category tabs and assets

Five category tabs are always enabled and focusable:

| Category | Label | Production icon | Runtime state |
| --- | --- | --- | --- |
| Roads | `道路` | `res://Assets/UI/Icons/construction-road.svg` | Enabled, opens Roads asset strip |
| Zoning | `区域` | `res://Assets/UI/Icons/construction-zoning.svg` | Enabled tab, shows future disabled assets |
| Public Facilities | `公共设施` | `res://Assets/UI/Icons/construction-facilities.svg` | Enabled tab, shows future disabled assets |
| Transit | `交通` | `res://Assets/UI/Icons/construction-transit.svg` | Enabled tab, shows future disabled assets |
| Landscaping | `景观` | `res://Assets/UI/Icons/construction-landscaping.svg` | Enabled tab, shows future disabled assets |

Roads exposes exactly one enabled, focusable asset:

| Asset | Label | Production icon | Tool mapping | Description |
| --- | --- | --- | --- | --- |
| City road | `城市道路` | `res://Assets/UI/Icons/construction-road.svg` | `ToolType.Road` | `拖拽铺设道路。` |

Future categories expose disabled, non-focusable placeholders only:

| Category | Placeholder labels |
| --- | --- |
| Zoning | `住宅区`, `商业区` |
| Public Facilities | `学校`, `诊所` |
| Transit | `公交站`, `地铁站` |
| Landscaping | `公园`, `广场` |

Future placeholders must not call catalogs, change `ToolManager.CurrentTool`, create modal panels, register shortcuts, or imply implemented gameplay. Their tooltip remains `尚未开放`.

### 5.4 Production icon geometry

Production SVGs are original 32x32, monochrome, 2px rounded-stroke assets under `Assets/UI/Icons/`. Godot scenes and resources must reference only production icon paths. Files in `docs/ui/concepts/` are discussion material and are never implementation targets.

| Icon | Required geometry semantics |
| --- | --- |
| `construction-road.svg` | Vertical two-line road with short center lane marks |
| `construction-zoning.svg` | Four staggered parcel rectangles with uneven positions and sizes |
| `construction-facilities.svg` | Square municipal facility mark with a plus sign |
| `construction-transit.svg` | Lightweight T network with four identical ordinary stations, each `r=2.0`; the station at `(16,10)` is not a hub, has no inner ring, no larger radius, no special color, and no special line width |
| `construction-landscaping.svg` | Simple tree crown and trunk mark |

The approved concept K described these semantics, but the concept SVG remains historical and documentation-only. Runtime icon ownership is in scene/resource data: category textures are serialized in `Scenes/UI/ConstructionDock.tscn`, and the `城市道路` asset texture is serialized in `Scenes/UI/RoadsConstructionCategory.tres` through `ConstructionToolDefinition.Icon`.

### 5.5 ContextPanel, PauseMenu, and DebugPanel boundary

ContextPanel remains a right-side read-only explanation panel. It shows current tool context, hides shortcut rows when no shortcut exists, and shows future category context as not open without claiming future tools are implemented.

PauseMenu owns the only player-facing save/load controls and their status feedback. It is modal rather than a persistent HUD surface; F5/F9 do not trigger save or load.

DebugPanel remains independent from ConstructionDock. It keeps its scene, theme, top-left placement, default collapsed state, metrics, and behavior. Debug terms such as RoadGroup, GraphEdge, and GraphNode stay in DebugPanel or developer docs, not in the primary player dock.

PauseMenu is a full-screen modal overlay using the command-center theme. It uses a dimming layer and one centered panel with mutually exclusive main, settings, bindings, and confirmation views. The main/settings/confirmation content has a 360px baseline; the binding list scrolls within the viewport and keeps every binding button at least 132px wide, including at 435x480. It is not a dock card or a decorative floating section. Main actions use the normal command button style; opening focuses Continue, confirmation deliberately focuses Cancel, and closing restores the prior valid HUD focus. See [pause-menu.md](pause-menu.md) for behavior and persistence boundaries.

## 6. Interaction and focus

Category behavior:

| Input | Required result |
| --- | --- |
| Select inactive category | Switch active category and open the asset strip |
| Select active category again | Collapse or reopen the asset strip by repeat action |
| Select `城市道路` | Set `ToolType.Road` |
| Press the current `pause_menu` binding (default Esc) | Open PauseMenu and preserve the current tool |
| Press the current `tool_select` / `tool_road` / `tool_remove` binding (default Q/R/E) | Set Select, Road, or RoadRemove while no modal is active |
| Programmatic RoadRemove | Still supported outside the visible Roads asset list |

Focus order starts with the five category buttons, then moves to enabled assets in the current strip, then to ContextPanel and DebugPanel according to configured focus paths. Disabled future placeholders are not focusable and are skipped in forward and reverse focus traversal. PauseMenu temporarily owns focus while modal, then restores the previously focused valid control on close. Focus must remain visible separately from selected, hover, and disabled states.

Hover uses the dock-local amber hover accent and neutral hover surface. Disabled uses disabled icon and label color, non-focusable state, no side effects, and the `尚未开放` tooltip. Primary selection combines the pressed surface, amber icon/label, and bottom indicator; secondary selection combines only the pressed surface and amber icon/label.

## 7. Motion and surface depth

Motion uses short Godot tweens or AnimationPlayer tracks. Prefer `modulate:a`, `position:y`, `scale`, or one-time layout changes at open and close boundaries. Do not animate layout properties continuously.

Depth is tonal-shift only. Shared HUD panels can use established command-center panel radius. ConstructionDock's outer bottom surface uses the K-local flat edge-to-edge treatment. Do not add gradients, heavy shadows, category cards, emoji icons, or decorative copied assets.

## 8. Historical concepts and superseded claims

Files in `docs/ui/concepts/` record visual exploration. They helped choose K semantics for the production dock, especially the lightweight four-station transit icon. They are not runtime resources and do not define current Godot scene behavior.

Older narrow floating dock drafts and vertical tool-list descriptions are superseded for live ConstructionDock behavior. Keep them only as historical discussion when they appear in concept notes or session records. Current live truth is the full-width, bottom-flush K dock described in this document.

## 9. ConstructionDock iteration record

The two-level menu iteration in [`construction-dock-iteration.md`](construction-dock-iteration.md) was implemented and runtime-verified on 2026-07-31. It replaced the 46px secondary shelf with a 64px shelf, raised expanded height to 140px, globally centered secondary groups, reserved a 4px bottom-anchored amber indicator for primary categories, and removed the underline from secondary selection.

The values in sections 1-8 are the current live contract. The iteration document preserves the decision context and acceptance evidence; historical bugfix entries may still mention superseded 46px, 122px, 20px, or category-relative behavior.
