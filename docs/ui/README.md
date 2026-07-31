# UI Documentation

This directory is the canonical navigation root for current SimpleCities UI design and architecture. Runtime truth still comes from Godot scenes, resources, C# code, and tests.

## Current contracts

- [Design system](design-system.md): live visual rules, K dock palette, full-width 76px collapsed and 140px expanded geometry, CJK rules, icon semantics, and production asset boundaries.
- [Architecture](architecture.md): live HUD composition, ConstructionDock resource flow, focus contracts, responsive reservation, Debug isolation, and verification entry points.
- [Pause menu](pause-menu.md): configurable pause lifecycle, save/load behavior, audio and persistent key settings, focus, confirmation flow, and main-menu return semantics.

## Implemented iteration

- [ConstructionDock two-level menu iteration](construction-dock-iteration.md): implemented design record for the 64px secondary shelf, globally centered secondary groups, bottom-anchored primary amber indicator, and filled secondary selection state.

## Supporting records

- [Concept art](concepts/README.md): historical SVG discussion files only. They are not production assets and must not be loaded by runtime scenes or resources.
- [UI bugfix record](../bugfix/ui.md): verified UI fix history. Historical sections can describe superseded behavior, while current implementation facts should point back to the canonical UI docs above.

Compatibility pointers remain at [`../../DESIGN.md`](../../DESIGN.md) and [`../reference/ui-architecture.md`](../reference/ui-architecture.md) for older links.
