extends SceneTree

const SAVE := preload("res://tests/godot/v3_save_fixture.gd")
const WORK_PROBE = preload("res://tests/godot/V4OperationWorkProbe.cs")
var _rows: Array = []
var _passed: bool = true

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	var map = load("res://Scenes/V4MapTest.tscn").instantiate()
	root.add_child(map)
	current_scene = map
	await process_frame
	check("long_map_created", map.CreateMap(100))
	focus(map, Vector2(250, 0), 1.2)
	await process_frame
	check("long_road_built", await build(map, Vector2.ZERO, Vector2(500, 0)))
	var before: Dictionary = map.GetRoadState()
	check("remove_mode", map.SetToolMode(2))
	press(map, Vector2(150, 0))
	await process_frame
	motion(map, Vector2(250, 0))
	await process_frame
	var preview: Dictionary = selected_stroke(map)
	check("drag_previews_both_crossed_spans", map.GetRoadState() == before and map.GetSelectionState().get("selectedCount", 0) == 2 and point_set_matches(preview, [Vector2(100, 0), Vector2(200, 0)]), preview)
	escape()
	await process_frame
	release(map, Vector2(250, 0))
	await settle(map)
	check("escape_then_release_does_not_delete", map.GetRoadState() == before and map.GetSelectionState().get("selectedCount", -1) == 0)
	var probe = WORK_PROBE.new()
	probe.Install(map)
	press(map, Vector2(250, 0))
	await process_frame
	release(map, Vector2(250, 0))
	var deadline: int = Time.get_ticks_msec() + 5000
	while not probe.Started and Time.get_ticks_msec() < deadline:
		await process_frame
	check("delete_prepares_in_background_without_publish", probe.Started and probe.UsedBackgroundThread and map.IsBuildBusy and map.GetRoadState() == before)
	check("pending_delete_retains_exact_highlight", map.GetSelectionState().get("selectedCount", 0) == 1 and point_set_matches(selected_stroke(map), [Vector2(200, 0), Vector2(300, 0)]), map.GetSelectionState())
	escape()
	await process_frame
	check("delete_cancel_waits_for_worker", map.BuildPhase == "Cancelling" and map.IsBuildBusy and map.GetRoadState() == before)
	check("accepted_cancel_clears_highlight_immediately", map.GetSelectionState().get("strokes", []).is_empty())
	probe.Release()
	var cancelled: bool = await settle(map)
	probe.Remove(map)
	check("cancelled_delete_rejects_late_result", cancelled and map.BuildPhase == "Cancelled" and map.GetRoadState() == before and probe.Completed)
	if probe.Completed: probe.Cleanup()
	press(map, Vector2(250, 0))
	await process_frame
	check("press_only_previews", map.GetRoadState() == before)
	release(map, Vector2(250, 0))
	await settle(map)
	check("middle_span_removed", map.PickRoad(Vector2(250, 0)).is_empty() and not map.PickRoad(Vector2(150, 0)).is_empty() and not map.PickRoad(Vector2(350, 0)).is_empty() and map.GetRoadState().get("edges", []).size() == 2)
	var left: Dictionary = map.PickRoad(Vector2(203, 0))
	var right: Dictionary = map.PickRoad(Vector2(297, 0))
	check("cut_caps_have_current_distinct_owners", not left.is_empty() and not right.is_empty() and left.get("edgeId") != right.get("edgeId") and left.get("edgeId") == map.PickRoad(Vector2(150, 0)).get("edgeId") and right.get("edgeId") == map.PickRoad(Vector2(350, 0)).get("edgeId") and left.get("sourceToken") == map.StateToken and right.get("sourceToken") == map.StateToken and map.StateToken != before.get("sourceToken") and map.PickRoad(Vector2(207, 0)).is_empty() and map.PickRoad(Vector2(293, 0)).is_empty(), [left, right])
	await roundtrip(map, "removed_middle")
	check("reloaded_gap_and_caps", map.PickRoad(Vector2(250, 0)).is_empty() and not map.PickRoad(Vector2(203, 0)).is_empty() and not map.PickRoad(Vector2(297, 0)).is_empty())
	check("type_map_created", map.SetToolMode(0) and map.CreateMap(100))
	check("type_long_road_built", await build(map, Vector2.ZERO, Vector2(500, 0)))
	check("type_mode", map.SetToolMode(3))
	var profile: OptionButton = map.get_node("HUD/Panel/Margin/Controls/Profile")
	profile.select(3)
	before = map.GetRoadState()
	press(map, Vector2(250, 0))
	await process_frame
	preview = selected_stroke(map)
	check("type_press_only_highlights_middle_span", map.GetRoadState() == before and point_set_matches(preview, [Vector2(200, 0), Vector2(300, 0)]), preview)
	release(map, Vector2(250, 0))
	await settle(map)
	check("only_middle_span_becomes_highway", map.PickRoad(Vector2(250, 0)).get("profile") == "highway" and map.PickRoad(Vector2(150, 0)).get("profile") == "street" and map.PickRoad(Vector2(350, 0)).get("profile") == "street" and map.GetRoadState().get("edges", []).size() == 3)
	check("new_width_matches_changed_span", map.PickRoad(Vector2(250, 14)).get("profile") == "highway" and map.PickRoad(Vector2(250, 17)).is_empty() and map.PickRoad(Vector2(150, 7)).is_empty())
	await roundtrip(map, "changed_middle", true)
	# Representative reverse transitions visit every built-in profile as both source and target.
	# The complete pairwise profile contract belongs at the public core seam.
	var names: Array = ["dirt", "street", "arterial", "highway"]
	for index in [0, 2, 3, 2, 0, 3, 1]:
		profile.select(index)
		var source_profile: String = map.PickRoad(Vector2(250, 0)).get("profile", "")
		var token: String = map.StateToken
		press(map, Vector2(250, 0))
		await process_frame
		release(map, Vector2(250, 0))
		var settled: bool = await settle(map)
		check("type_" + source_profile + "_to_" + names[index], settled and map.StateToken != token and map.PickRoad(Vector2(250, 0)).get("profile") == names[index] and map.PickRoad(Vector2(150, 0)).get("profile") == "street" and map.PickRoad(Vector2(350, 0)).get("profile") == "street")
	check("return_to_street_removes_profile_boundaries", map.GetRoadState().get("edges", []).size() == 1 and map.GetRoadState().get("nodeCount") == 2 and map.PickRoad(Vector2(250, 7)).is_empty())

	check("center_map_created", map.SetToolMode(0) and map.CreateMap(100))
	profile.select(1)
	focus(map, Vector2(50, 50), 3.0)
	await process_frame
	check("center_first_diagonal", await build(map, Vector2.ZERO, Vector2(100, 100)))
	check("center_second_diagonal", await build(map, Vector2(0, 100), Vector2(100, 0)))
	check("center_change_mode", map.SetToolMode(3))
	profile.select(3)
	before = map.GetRoadState()
	press(map, Vector2(75, 75))
	await process_frame
	check("center_change_highlight_is_one_half", point_set_matches(selected_stroke(map), [Vector2(50, 50), Vector2(100, 100)]) and map.GetRoadState() == before)
	release(map, Vector2(75, 75))
	await settle(map)
	check("center_change_preserves_other_three_branches", map.PickRoad(Vector2(75, 75)).get("profile") == "highway" and map.PickRoad(Vector2(25, 25)).get("profile") == "street" and map.PickRoad(Vector2(25, 75)).get("profile") == "street" and map.PickRoad(Vector2(75, 25)).get("profile") == "street")
	check("center_remove_mode", map.SetToolMode(2))
	before = map.GetRoadState()
	press(map, Vector2(25, 25))
	await process_frame
	check("center_delete_highlight_is_one_half", point_set_matches(selected_stroke(map), [Vector2.ZERO, Vector2(50, 50)]) and map.GetRoadState() == before)
	release(map, Vector2(25, 25))
	await settle(map)
	check("center_delete_preserves_other_three_branches", map.PickRoad(Vector2(25, 25)).is_empty() and map.PickRoad(Vector2(75, 75)).get("profile") == "highway" and map.PickRoad(Vector2(25, 75)).get("profile") == "street" and map.PickRoad(Vector2(75, 25)).get("profile") == "street" and map.GetRoadState().get("junctions", []).size() == 1 and map.GetRoadState().get("junctions", [])[0].get("incidences", []).size() == 3)
	await roundtrip(map, "center_local_edits")
	check("reloaded_center_half_and_types", map.PickRoad(Vector2(25, 25)).is_empty() and map.PickRoad(Vector2(75, 75)).get("profile") == "highway" and map.PickRoad(Vector2(25, 75)).get("profile") == "street" and map.PickRoad(Vector2(75, 25)).get("profile") == "street")
	motion(map, Vector2(75, 75), false)
	await process_frame
	await RenderingServer.frame_post_draw
	var capture_path: String = OS.get_environment("V4_QA_OUTPUT")
	var capture_dir: String = ProjectSettings.globalize_path(capture_path if not capture_path.is_empty() else "res://.scratch/v4-12-13-qa")
	var directory_error: int = DirAccess.make_dir_recursive_absolute(capture_dir)
	check("local_edit_screenshot", (directory_error == OK or directory_error == ERR_ALREADY_EXISTS) and root.get_texture().get_image().save_png(capture_dir.path_join("single-span-edits.png")) == OK)
	print("V4_SINGLE_SPAN_EDIT_RESULT ", JSON.stringify({"rows": _rows, "passed": _passed}))
	map.queue_free()
	await process_frame
	if _passed:
		print("PASS V4 single span edit runtime contract")
		quit(0)
	else:
		push_error("FAIL V4 single span edit runtime contract")
		quit(1)

func check(name: String, passed: bool, details: Variant = null) -> void:
	_rows.append({"name": name, "passed": passed, "details": details})
	_passed = passed and _passed

func selected_stroke(map: Node) -> Dictionary:
	var strokes: Array = map.GetSelectionState().get("strokes", []).filter(func(stroke): return stroke.get("selected", false))
	return strokes[0] if not strokes.is_empty() else {}

func point_set_matches(stroke: Dictionary, expected: Array) -> bool:
	var points: Array = Array(stroke.get("points", PackedVector2Array()))
	return points.size() == expected.size() and points.all(func(point): return expected.has(point))

func roundtrip(map: Node, label: String, check_no_change: bool = false) -> void:
	var manager: Node = root.get_node("SaveManager")
	var before: Dictionary = map.GetRoadState()
	var saved: bool = await SAVE.save_as(manager, "V4 single span QA " + label)
	check(label + "_saved", saved)
	if not saved: return
	var slot: String = manager.CurrentSlotID
	var payload_path: String = "user://saves-v4/" + slot + "/road_network_v4.json"
	var bytes: PackedByteArray = FileAccess.get_file_as_bytes(payload_path)
	check(label + "_payload_present", not bytes.is_empty())
	if check_no_change:
		# The selected target remains Highway; codec bytes include content revision and ID allocators.
		var previous_phase: String = map.BuildPhase
		var previous_elapsed: float = map.BuildElapsedMilliseconds
		var previous_drawn: float = map.BuildDrawnElapsedMilliseconds
		motion(map, Vector2(250, 0), false)
		await process_frame
		check("same_profile_has_no_hover_highlight", map.GetSelectionState().get("strokes", []).is_empty())
		press(map, Vector2(250, 0))
		await process_frame
		check("same_profile_has_no_pending_highlight", map.GetSelectionState().get("strokes", []).is_empty())
		release(map, Vector2(250, 0))
		await settle(map)
		check("same_profile_does_not_start_operation", not map.IsBuildBusy and map.BuildPhase == previous_phase and map.BuildElapsedMilliseconds == previous_elapsed and map.BuildDrawnElapsedMilliseconds == previous_drawn)
		check("same_profile_keeps_snapshot_token_and_ids", map.GetRoadState() == before)
		var no_change_saved: bool = await SAVE.save(manager, slot)
		check("same_profile_preserves_codec_bytes", no_change_saved and FileAccess.get_file_as_bytes(payload_path) == bytes)
	var loaded: bool = await SAVE.operation_succeeded(manager, map.LoadSlot(slot))
	await process_frame
	check(label + "_reload", loaded and map.IsPresentationCurrent and map.StateToken != before.get("sourceToken") and map.GetRoadState().get("edges") == before.get("edges"))
	var resaved: bool = await SAVE.save(manager, slot)
	check(label + "_resave", resaved and FileAccess.get_file_as_bytes(payload_path) == bytes)
	var deleted: bool = await SAVE.delete_slot(manager, slot)
	check(label + "_fixture_deleted", deleted and not DirAccess.dir_exists_absolute(ProjectSettings.globalize_path("user://saves-v4/" + slot)))

func focus(map: Node, world: Vector2, zoom: float) -> void:
	var camera: Camera2D = map.get_node("Camera2D")
	camera.zoom = Vector2(zoom, zoom)
	camera.position = world - (Vector2(1000, 450) - root.get_visible_rect().size / 2.0) / zoom

func build(map: Node, start: Vector2, end: Vector2) -> bool:
	var token: String = map.StateToken
	press(map, start)
	motion(map, end)
	release(map, end)
	return await settle(map) and map.StateToken != token

func settle(map: Node) -> bool:
	var deadline: int = Time.get_ticks_msec() + 5000
	while Time.get_ticks_msec() < deadline:
		await process_frame
		if not map.IsBuildBusy: return map.IsPresentationCurrent
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
