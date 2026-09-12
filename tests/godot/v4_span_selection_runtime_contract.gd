extends SceneTree

const SAVE := preload("res://tests/godot/v3_save_fixture.gd")
var _rows: Array = []
var _passed: bool = true

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	var map = load("res://Scenes/V4MapTest.tscn").instantiate()
	root.add_child(map)
	current_scene = map
	await process_frame
	var manager = root.get_node("SaveManager")
	check("long_map_created", map.CreateMap(100))
	focus(map, Vector2(500, 0), 0.45)
	await process_frame
	check("long_road_built", await build(map, Vector2.ZERO, Vector2(1000, 0)))
	var before: Dictionary = map.GetRoadState()
	check("one_canonical_edge", before.get("edges", []).size() == 1)
	var saved: bool = await SAVE.save_as(manager, "V4 span selection QA")
	var slot: String = manager.CurrentSlotID if saved else ""
	var payload_path: String = "user://saves-v4/" + slot + "/road_network_v4.json"
	var original_bytes: PackedByteArray = FileAccess.get_file_as_bytes(payload_path) if saved else PackedByteArray()
	check("fixture_saved", saved and not original_bytes.is_empty())
	check("selection_mode", map.SetToolMode(1))
	motion(map, Vector2(450, 0), false)
	await process_frame
	var hover: Dictionary = first_stroke(map, false)
	var hover_color: Color = hover.get("color", Color.BLACK)
	check("long_edge_internal_grid_hover", count(map) == 0 and map.GetSelectionState().get("hasHover", false) and exact_points(hover, [Vector2(400, 0), Vector2(500, 0)]) and exact_range(hover, 0.4, 0.5) and hover.get("sourceToken") == map.StateToken and hover.get("edgeId") == before.get("edgeId"), hover)
	press(map, Vector2(450, 0))
	await process_frame
	motion(map, Vector2(650, 0))
	await process_frame
	var selected: Dictionary = first_stroke(map, true)
	var selected_color: Color = selected.get("color", Color.BLACK)
	check("held_drag_accumulates_three", count(map) == 3 and map.GetSelectionState().get("selecting", false))
	check("hover_and_selected_distinct", hover_color != selected_color and hover_color.b > hover_color.r and selected_color.r > selected_color.b and selected.get("width", 0.0) > hover.get("width", 0.0))
	release(map, Vector2(650, 0))
	await process_frame
	motion(map, Vector2(650, 180), false)
	await process_frame
	check("release_and_leave_preserve_selection", count(map) == 3 and not map.GetSelectionState().get("selecting", true) and not map.GetSelectionState().get("hasHover", true))
	# Exactly one motion event traverses all ten intervals; no intermediate frame samples.
	press(map, Vector2(50, 0))
	await process_frame
	motion(map, Vector2(950, 0))
	await process_frame
	check("single_motion_covers_ten_spans", count(map) == 10, map.GetSelectionState())
	motion(map, Vector2(50, 0))
	await process_frame
	release(map, Vector2(50, 0))
	await process_frame
	check("return_drag_deduplicates", count(map) == 10)
	escape()
	await process_frame
	check("escape_clears_without_network_change", count(map) == 0 and map.GetSelectionState().get("strokes", []).is_empty() and map.GetRoadState() == before)
	if saved:
		var resaved: bool = await SAVE.save(manager, slot)
		check("selection_preserves_codec_bytes", resaved and FileAccess.get_file_as_bytes(payload_path) == original_bytes)
		press(map, Vector2(450, 0))
		await process_frame
		release(map, Vector2(450, 0))
		await process_frame
		var old_source: String = map.GetSelectionState().get("sourceToken", "")
		var had_selection: bool = count(map) == 1
		var loaded: bool = await SAVE.operation_succeeded(manager, map.LoadSlot(slot))
		await process_frame
		check("load_invalidates_selection_source", had_selection and loaded and map.StateToken != old_source and map.GetSelectionState().get("sourceToken", "").is_empty() and count(map) == 0 and map.GetSelectionState().get("strokes", []).is_empty() and map.GetRoadState().get("edges") == before.get("edges"))
		var deleted: bool = await SAVE.delete_slot(manager, slot)
		check("fixture_deleted", deleted and not DirAccess.dir_exists_absolute(ProjectSettings.globalize_path("user://saves-v4/" + slot)))
	else:
		check("save_load_fixture_unavailable", false)

	check("cross_map_created", map.SetToolMode(0) and map.CreateMap(100))
	focus(map, Vector2.ZERO, 1.2)
	await process_frame
	check("cross_horizontal_built", await build(map, Vector2(-200, 0), Vector2(200, 0)))
	check("cross_vertical_built", await build(map, Vector2(0, -200), Vector2(0, 200)))
	check("cross_selection_mode", map.SetToolMode(1))
	press(map, Vector2.ZERO)
	await process_frame
	check("junction_center_is_ambiguous", count(map) == 0 and not map.GetSelectionState().get("hasHover", true))
	motion(map, Vector2(50, 0))
	await process_frame
	release(map, Vector2(50, 0))
	await process_frame
	var branch: Dictionary = first_stroke(map, true)
	check("moving_toward_branch_selects_only_it", count(map) == 1 and point_set_matches(branch, [Vector2.ZERO, Vector2(100, 0)]), branch)
	press(map, Vector2(-150, 0))
	await process_frame
	motion(map, Vector2(150, 0))
	await process_frame
	release(map, Vector2(150, 0))
	await process_frame
	var cross_strokes: Array = selected_strokes(map)
	var only_horizontal: bool = cross_strokes.all(func(stroke): return Array(stroke.get("points", PackedVector2Array())).all(func(point): return is_zero_approx(point.y)))
	check("fast_drag_crosses_without_side_branches", count(map) == 4 and only_horizontal, cross_strokes)

	check("center_map_created", map.SetToolMode(0) and map.CreateMap(100))
	focus(map, Vector2(50, 50), 3.0)
	await process_frame
	check("center_first_diagonal", await build(map, Vector2.ZERO, Vector2(100, 100)))
	check("center_second_diagonal", await build(map, Vector2(0, 100), Vector2(100, 0)))
	check("center_selection_mode", map.SetToolMode(1))
	motion(map, Vector2(25, 25), false)
	await process_frame
	var half_a: Dictionary = first_stroke(map, false)
	press(map, Vector2(25, 25))
	await process_frame
	release(map, Vector2(25, 25))
	await process_frame
	motion(map, Vector2(75, 75), false)
	await process_frame
	var half_b: Dictionary = first_stroke(map, false)
	check("cell_center_halves_independent", count(map) == 1 and point_set_matches(half_a, [Vector2.ZERO, Vector2(50, 50)]) and point_set_matches(half_b, [Vector2(50, 50), Vector2(100, 100)]) and half_a.get("edgeId") != half_b.get("edgeId") and exact_range(half_a, 0.0, 1.0) and exact_range(half_b, 0.0, 1.0))
	await process_frame
	await RenderingServer.frame_post_draw
	var capture_dir: String = ProjectSettings.globalize_path("res://.scratch/v4-10-11-qa")
	var directory_error: int = DirAccess.make_dir_recursive_absolute(capture_dir)
	check("selection_screenshot", (directory_error == OK or directory_error == ERR_ALREADY_EXISTS) and root.get_texture().get_image().save_png(capture_dir.path_join("selection.png")) == OK)
	press(map, Vector2(25, 25))
	await process_frame
	motion(map, Vector2(75, 75))
	await process_frame
	release(map, Vector2(75, 75))
	await process_frame
	check("center_crossing_only_two_diagonal_halves", count(map) == 2 and selected_strokes(map).all(func(stroke): return Array(stroke.get("points", PackedVector2Array())).all(func(point): return is_equal_approx(point.x, point.y))))

	check("loop_map_created", map.SetToolMode(0) and map.CreateMap(100))
	check("loop_first_arm", await build(map, Vector2(50, 50), Vector2.ZERO))
	check("loop_bottom", await build(map, Vector2.ZERO, Vector2(100, 0)))
	check("loop_closed", await build(map, Vector2(100, 0), Vector2(50, 50)))
	check("loop_selection_mode", map.SetToolMode(1))
	press(map, Vector2(40, 40))
	await process_frame
	motion(map, Vector2(60, 40))
	await process_frame
	release(map, Vector2(60, 40))
	await process_frame
	var wrap: Dictionary = first_stroke(map, true)
	var wrap_ranges: Array = wrap.get("ranges", [])
	var wrap_points: PackedVector2Array = wrap.get("points", PackedVector2Array())
	check("synthetic_center_seam_is_one_wrap_span", count(map) == 1 and wrap_ranges.size() == 2 and wrap_points.size() == 3 and wrap_points.has(Vector2(50, 50)) and point_set_matches(wrap, [Vector2.ZERO, Vector2(50, 50), Vector2(100, 0)]), wrap)
	print("V4_SPAN_SELECTION_RESULT ", JSON.stringify({"rows": _rows, "passed": _passed}))
	map.queue_free()
	await process_frame
	if _passed:
		print("PASS V4 span selection runtime contract")
		quit(0)
	else:
		push_error("FAIL V4 span selection runtime contract")
		quit(1)

func check(name: String, passed: bool, details: Variant = null) -> void:
	_rows.append({"name": name, "passed": passed, "details": details})
	_passed = passed and _passed

func count(map: Node) -> int:
	return map.GetSelectionState().get("selectedCount", 0)

func selected_strokes(map: Node) -> Array:
	return map.GetSelectionState().get("strokes", []).filter(func(stroke): return stroke.get("selected", false))

func first_stroke(map: Node, selected: bool) -> Dictionary:
	var strokes: Array = map.GetSelectionState().get("strokes", []).filter(func(stroke): return stroke.get("selected", false) == selected)
	return strokes[0] if not strokes.is_empty() else {}

func exact_points(stroke: Dictionary, expected: Array) -> bool:
	return Array(stroke.get("points", PackedVector2Array())) == expected

func point_set_matches(stroke: Dictionary, expected: Array) -> bool:
	var points: Array = Array(stroke.get("points", PackedVector2Array()))
	return points.size() == expected.size() and points.all(func(point): return expected.has(point))

func exact_range(stroke: Dictionary, start: float, end: float) -> bool:
	var ranges: Array = stroke.get("ranges", [])
	return ranges.size() == 1 and is_equal_approx(ranges[0].get("startParameter", -1.0), start) and is_equal_approx(ranges[0].get("endParameter", -1.0), end)

func focus(map: Node, world: Vector2, zoom: float) -> void:
	var camera: Camera2D = map.get_node("Camera2D")
	camera.zoom = Vector2(zoom, zoom)
	camera.position = world - (Vector2(1000, 450) - root.get_visible_rect().size / 2.0) / zoom

func build(map: Node, start: Vector2, end: Vector2) -> bool:
	var token: String = map.StateToken
	press(map, start)
	motion(map, end)
	release(map, end)
	var deadline: int = Time.get_ticks_msec() + 5000
	while Time.get_ticks_msec() < deadline:
		await process_frame
		if not map.IsBuildBusy: return map.StateToken != token and map.IsPresentationCurrent
	return false

func press(map: Node, point: Vector2) -> void:
	button(map.get_canvas_transform() * point, true)

func release(map: Node, point: Vector2) -> void:
	button(map.get_canvas_transform() * point, false)

func button(position: Vector2, pressed: bool) -> void:
	var event := InputEventMouseButton.new()
	event.position = position
	event.button_index = MOUSE_BUTTON_LEFT
	event.pressed = pressed
	Input.parse_input_event(event)

func motion(map: Node, point: Vector2, held: bool = true) -> void:
	var event := InputEventMouseMotion.new()
	event.position = map.get_canvas_transform() * point
	event.button_mask = MOUSE_BUTTON_MASK_LEFT if held else 0
	Input.parse_input_event(event)

func escape() -> void:
	var event := InputEventKey.new()
	event.keycode = KEY_ESCAPE
	event.pressed = true
	Input.parse_input_event(event)
	var released := InputEventKey.new()
	released.keycode = KEY_ESCAPE
	Input.parse_input_event(released)
