extends "res://tests/godot/v4_single_span_edit_runtime_contract.gd"

func run() -> void:
	var map = load("res://Scenes/V4MapTest.tscn").instantiate()
	root.add_child(map)
	current_scene = map
	await process_frame
	check("batch_map", map.CreateMap(100))
	focus(map, Vector2(500, 0), 0.65)
	await process_frame
	check("batch_road", await build(map, Vector2.ZERO, Vector2(1000, 0)))
	var before: Dictionary = map.GetRoadState()
	check("batch_remove_mode", map.SetToolMode(2))
	press(map, Vector2(150, 0))
	await process_frame
	# One motion must collect every crossed grid span, without writes while held.
	motion(map, Vector2(850, 0))
	await process_frame
	check("fast_drag_freezes_eight_spans_without_write", map.GetSelectionState().get("selectedCount") == 8 and map.GetRoadState() == before, map.GetSelectionState())
	escape()
	await process_frame
	release(map, Vector2(850, 0))
	await settle(map)
	check("cancel_entire_batch", map.GetRoadState() == before and map.GetSelectionState().get("selectedCount") == 0)
	var probe = WORK_PROBE.new()
	probe.Install(map)
	press(map, Vector2(150, 0))
	await process_frame
	motion(map, Vector2(850, 0))
	await process_frame
	motion(map, Vector2(150, 0))
	await process_frame
	check("return_drag_deduplicates_eight", map.GetSelectionState().get("selectedCount") == 8 and map.GetRoadState() == before)
	release(map, Vector2(150, 0))
	var deadline: int = Time.get_ticks_msec() + 5000
	while not probe.Started and Time.get_ticks_msec() < deadline:
		await process_frame
	check("released_batch_keeps_frozen_highlight", probe.Started and map.IsBuildBusy and map.GetRoadState() == before and map.GetSelectionState().get("selectedCount") == 8)
	probe.Release()
	var observed_tokens: Array = [map.StateToken]
	while map.IsBuildBusy and Time.get_ticks_msec() < deadline:
		await process_frame
		if observed_tokens[-1] != map.StateToken: observed_tokens.append(map.StateToken)
	probe.Remove(map)
	check("whole_batch_publishes_once_and_draws", not map.IsBuildBusy and map.IsPresentationCurrent and observed_tokens.size() == 2 and probe.Completed, observed_tokens)
	if probe.Completed: probe.Cleanup()
	check("unselected_end_spans_survive", not map.PickRoad(Vector2(50, 0)).is_empty() and not map.PickRoad(Vector2(950, 0)).is_empty() and map.GetRoadState().get("edges", []).size() == 2)
	for x in range(150, 900, 100):
		check("selected_span_deleted_" + str(x), map.PickRoad(Vector2(x, 0)).is_empty())
	await roundtrip(map, "batch_removed")
	check("reloaded_batch_gap", map.PickRoad(Vector2(500, 0)).is_empty() and not map.PickRoad(Vector2(50, 0)).is_empty() and not map.PickRoad(Vector2(950, 0)).is_empty())
	check("mixed_map", map.SetToolMode(0) and map.CreateMap(100))
	focus(map, Vector2.ZERO, 1.3)
	await process_frame
	check("cross_horizontal", await build(map, Vector2(-200, 0), Vector2(200, 0)))
	check("cross_vertical", await build(map, Vector2(0, -200), Vector2(0, 200)))
	check("change_mode", map.SetToolMode(3))
	var profile: OptionButton = map.get_node("HUD/Panel/Margin/Controls/Profile")
	profile.select(3)
	press(map, Vector2(50, 0))
	await process_frame
	release(map, Vector2(50, 0))
	await settle(map)
	check("mixed_fixture", map.PickRoad(Vector2(50, 0)).get("profile") == "highway" and map.PickRoad(Vector2(-50, 0)).get("profile") == "street")
	before = map.GetRoadState()
	press(map, Vector2(-150, 0))
	await process_frame
	motion(map, Vector2(150, 0))
	await process_frame
	var strokes: Array = map.GetSelectionState().get("strokes", []).filter(func(stroke): return stroke.get("selected", false))
	check("mixed_drag_highlights_only_three_changes", map.GetSelectionState().get("selectedCount") == 3 and strokes.size() == 3 and strokes.all(func(stroke): return Array(stroke.get("points", [])).all(func(point): return is_zero_approx(point.y))) and map.GetRoadState() == before, strokes)
	check("target_type_frozen_while_held", profile.disabled)
	await RenderingServer.frame_post_draw
	check("batch_highlight_screenshot", root.get_texture().get_image().save_png(ProjectSettings.globalize_path("res://.scratch/v4-14-19-qa/batch-highlight.png")) == OK)
	motion(map, Vector2(-150, 0))
	await process_frame
	check("mixed_return_drag_deduplicates", map.GetSelectionState().get("selectedCount") == 3)
	release(map, Vector2(-150, 0))
	await settle(map)
	check("all_selected_horizontal_profiles_changed", [-150, -50, 50, 150].all(func(x): return map.PickRoad(Vector2(x, 0)).get("profile") == "highway"))
	check("crossing_did_not_fan_out", [-150, -50, 50, 150].all(func(y): return map.PickRoad(Vector2(0, y)).get("profile") == "street"))
	before = map.GetRoadState()
	var old_phase: String = map.BuildPhase
	var old_elapsed: float = map.BuildElapsedMilliseconds
	press(map, Vector2(-150, 0))
	await process_frame
	motion(map, Vector2(150, 0))
	await process_frame
	check("all_same_type_has_no_pending_highlights", map.GetSelectionState().get("strokes", []).is_empty())
	release(map, Vector2(150, 0))
	await settle(map)
	check("whole_no_change_is_silent", map.GetRoadState() == before and map.BuildPhase == old_phase and map.BuildElapsedMilliseconds == old_elapsed and not map.IsBuildBusy)
	await roundtrip(map, "batch_mixed_profiles")
	print("V4_BATCH_EDIT_RESULT ", JSON.stringify({"rows": _rows, "passed": _passed}))
	map.queue_free()
	await process_frame
	if _passed:
		print("PASS V4 batch edit runtime contract")
		quit(0)
	else:
		push_error("FAIL V4 batch edit runtime contract")
		quit(1)
