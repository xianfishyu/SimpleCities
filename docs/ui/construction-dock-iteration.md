# ConstructionDock 两级菜单设计迭代

> 状态：已实施并通过运行时契约（2026-07-31）
> 范围：底部一级分类栏、二级工具栏及其选中状态
> 当前运行时事实以 `docs/ui/design-system.md`、`docs/ui/architecture.md` 和源码为准

## 1. 迭代目标

ConstructionDock 应表现为一套连续的底部建造工具架，而不是两个互不关联的按钮行。玩家点击一级分类后，二级工具在全宽工具架的中心区域展开；一级分类负责表达“当前打开哪一类建造内容”，二级工具负责表达“当前正在使用哪个具体工具”。

本轮解决三个视觉问题：

1. 二级工具栏只有 46px，图标和中文标签被迫压缩，信息层级过弱。
2. 二级工具曾以一级分类位置为锚点，分类越靠近窗口边缘，子菜单越偏离全局视觉中心，甚至把末端工具推到视口外。
3. 同一个 3px 琥珀色下划线同时用于一级分类和二级工具，二级下划线悬在两层菜单之间，破坏整体连接感。

## 2. 布局规格

```text
Expanded ConstructionDock, 140px

+--------------------------------------------------------------+
| Secondary tool shelf, 64px                                   |
|                  [ 24px icon ]                               |
|                  [ 城市道路  ]  selected surface             |
+--------------------------------------------------------------+
| Primary category row, 76px                                   |
|   [道路] [区域] [公共设施] [交通] [景观]                     |
|   ====== primary amber indicator, fixed to dock bottom ===== |
+--------------------------------------------------------------+ <- viewport bottom
```

| Area | Target size | Rule |
| --- | ---: | --- |
| Collapsed dock | 76px | Only the primary category row is visible |
| Secondary tool shelf | 64px | Fixed height; never compress icon or CJK label to preserve an older 46px value |
| Expanded dock | 140px | `76px primary + 64px secondary` |
| Primary category button | Up to 104x76px | May shrink responsively to 72px wide; height remains 76px |
| Primary icon | 32x32px | Icon above label |
| Secondary tool button | 104x64px | Minimum pointer target exceeds 44x44px |
| Secondary icon | 24x24px | Icon above label |
| Label | 13px / 18px line height | Exact CJK labels must not clip |
| Item separation | 8px | Shared horizontal separation |
| Icon-to-label gap | 3-4px | Never consume label height |

The secondary tool group is centered against the full ConstructionDock width, independent of the active primary category position. For `N` fixed-width tools, the content group width is `N * 104px + (N - 1) * 8px`, and its left edge is `(dock width - content group width) / 2`. Switching from Public Facilities to Landscaping must therefore replace the content without shifting the secondary group's center axis.

The primary-to-secondary relationship is communicated by the primary selected surface, the bottom-anchored amber indicator, and the changed shelf content. Spatially attaching the secondary group to the selected category is explicitly prohibited. When the secondary content is wider than the viewport, it becomes a full-width horizontally scrollable list and ensures the selected tool is visible; it does not shrink tool cells or use the primary category as a fallback anchor.

## 3. Selection hierarchy

Primary category and secondary tool represent different state levels and must not share the same indicator treatment.

### 3.1 Primary category selection

The primary category owns the dock's only amber bottom indicator.

- Use a 4px amber bar anchored to the absolute bottom edge of the primary category button and the ConstructionDock.
- The bar spans the selected primary button width and has `bottom = 0`; it must never participate in the icon/label `VBoxContainer` layout.
- The selected primary button also uses the pressed surface plus amber icon and label.
- The indicator remains at the viewport bottom in both collapsed and expanded states.
- A line between the secondary shelf and primary row is not a primary selection indicator.

### 3.2 Secondary tool selection

Secondary tools do not render an amber underline.

- Use the pressed/selected neutral surface as the structural state.
- Use amber icon and label colors for the selected tool.
- Preserve a separate 1px focus ring for keyboard focus.
- Hover, selected, focus, and disabled states must remain visually distinct.
- The selected surface fills the 104x64px tool cell; it does not extend into the primary row.

This division removes the suspended amber line: the primary indicator terminates the entire dock at the viewport bottom, while the secondary state is expressed inside its own shelf.

## 4. Surface relationship

| Surface | Treatment |
| --- | --- |
| Dock base / primary row | `ConstructionDock.Base`, dark neutral |
| Secondary shelf | `ConstructionDock.AssetStrip`, one tonal step lighter |
| Boundary between levels | 1px `ConstructionDock.Divider`; structural separation only |
| Primary selected indicator | `ConstructionDock.Selected`, 4px, bottom anchored |
| Secondary selected surface | `ConstructionDock.PressedSurface` |
| Secondary selected icon/label | `ConstructionDock.Selected` |

Do not add shadows, floating cards, rounded submenu containers, duplicate amber underlines, or decorative arrows. The full-width shelf and vertical alignment already communicate expansion.

## 5. Interaction and motion

| Action | Required response |
| --- | --- |
| Select an inactive category | Replace secondary contents, center the complete tool group in the dock, then open the shelf |
| Select the active category | Collapse or reopen the shelf |
| Select a secondary tool | Keep the category open and update the tool's filled selected state |
| Resize the viewport | Recompute primary button widths and secondary group alignment without changing row heights |
| Keyboard focus moves into a tool | Keep the tool visible and show the independent focus ring |

Opening and closing may use a 120-160ms ease-out transition combining `position:y` and `modulate:a`. Layout dimensions change once at the transition boundary; do not continuously tween container minimum sizes.

## 6. Responsive behavior

At 1600x900, 640x480, and 435x480:

- The dock remains full-width and bottom-flush.
- The primary indicator touches the viewport bottom exactly.
- All five primary categories remain visible at 435px by shrinking button width, not label size.
- The secondary group uses the dock's global horizontal center and does not move toward the active primary category.
- A content group narrower than the viewport has equal left and right free space within 1px rounding tolerance.
- A content group wider than the viewport uses horizontal scrolling and keeps the selected tool visible.
- Icon and label rectangles stay inside their button rectangles.
- No horizontal scrollbar consumes vertical space in the primary row.
- ConstructionDock must not overlap ContextPanel or DebugPanel after the expanded height changes to 140px.

Below the width where five 72px buttons plus four 8px gaps fit, the primary row may become horizontally scrollable. It must not shrink buttons below 72px or reduce CJK font size.

## 7. Component implementation

The reusable `ConstructionDockButton` now exposes `VisualRole` and separates role-specific presentation:

```text
PrimaryCategoryButton
+-- Presentation (icon + label)
+-- PrimarySelectionIndicator (absolute bottom overlay, 4px)

SecondaryToolButton
+-- Presentation (24px icon + label)
+-- no underline; selected state comes from surface + colors
```

Both roles remain in one reusable script. Primary scene instances set `PrimaryCategory`; dynamic tools and placeholders set `SecondaryTool`. The role is never inferred from node names, button text, or button height.

## 8. Acceptance criteria

The implementation is accepted because all of the following are observable through the running Godot contract for `Scenes/MapTest.tscn`:

1. Collapsed dock is 76px and expanded dock is 140px.
2. Clicking any primary category opens a 64px secondary shelf whose complete tool group is centered against the full dock, regardless of that category's horizontal position.
3. `城市道路` uses a 24px icon and an unclipped 13px label inside a 104x64px cell.
4. `城市道路` selected state has no underline; it uses filled surface plus amber icon and text.
5. `道路` selected state has one 4px amber indicator touching the absolute bottom edge of the dock.
6. The indicator remains bottom-anchored after expand, collapse, category switching, tool selection, focus traversal, and viewport resize.
7. Public Facilities and Landscaping produce the same secondary center axis even though their primary buttons occupy different horizontal positions; no tool is pushed outside the viewport while the group fits.
8. 1600x900, 640x480, and 435x480 runtime geometry checks pass without HUD overlap.
9. Focus, disabled placeholders, configurable tool/pause actions, pause-menu save/load, and road construction behavior remain intact.

## 9. Superseded design details

This implemented iteration supersedes these historical values:

- 46px secondary shelf
- 122px expanded dock
- 20px compact secondary icon used as a compatibility repair
- generic 3px selected underline on both primary and secondary buttons

`docs/ui/design-system.md` and `docs/ui/architecture.md` now describe the 140px live implementation. Older values remain only in historical bugfix records where they explain earlier failures.

## 10. Verification evidence

- `dotnet build SimpleCities.sln --no-restore`: passed with 0 warnings and 0 errors.
- Focused `ConstructionDockContractTests`: 9 passed, 0 failed.
- Full `dotnet test SimpleCities.sln --no-restore`: 28 passed, 0 failed.
- `tests/godot/command_center_runtime_contract.gd`: passed with 1600x900, 640x480, and 435x480 geometry, global secondary centering, selection hierarchy, focus, lifecycle, save/load, and HUD non-overlap assertions.
- `tests/godot/roads_construction_category_contract.gd`: passed; Roads catalog behavior remains intact.
