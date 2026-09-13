extends "res://tests/godot/v4_single_span_edit_runtime_contract.gd"

func run() -> void:
	var map = load("res://Scenes/V4MapTest.tscn").instantiate()
	root.add_child(map)
	current_scene = map
	await process_frame
	check("history_map_created", map.CreateMap(100))
	var undo: Button = map.get_node("HUD/Panel/Margin/Controls/History/Undo")
	var redo: Button = map.get_node("HUD/Panel/Margin/Controls/History/Redo")
	await process_frame
	check("new_map_has_empty_disabled_history", history_counts(map, 0, 0) and undo.disabled and redo.disabled and map.GetHistoryState().get("estimatedRetainedBytes", -1) == 0)
	var empty_history: Dictionary = map.GetHistoryState()
	check("empty_history_requests_are_silent", not map.UndoRoadEdit() and not map.RedoRoadEdit() and map.GetHistoryState() == empty_history and not map.IsBuildBusy)
	focus(map, Vector2(250, 0), 1.2)
	await process_frame
	check("history_road_built", await build(map, Vector2.ZERO, Vector2(500, 0)))
	var built: Dictionary = map.GetRoadState()
	var built_history: Dictionary = map.GetHistoryState()
	check("one_build_is_one_history_entry", history_counts(map, 1, 0) and built_history.get("estimatedRetainedBytes", 0) > 0 and not undo.disabled and redo.disabled, {"history": built_history, "undoDisabled": undo.disabled, "redoDisabled": redo.disabled})
	shortcut(KEY_Z)
	await settle(map)
	check("control_z_undoes_entire_build", map.GetRoadState().get("edges", []).is_empty() and map.IsPresentationCurrent, map.GetRoadState())
	check("undo_moves_entry_without_rewinding_identity", history_counts(map, 0, 1) and history_transition_matches(built_history, map.GetHistoryState()) and map.GetHistoryState().get("estimatedRetainedBytes") == built_history.get("estimatedRetainedBytes") and undo.disabled and not redo.disabled, {"history": map.GetHistoryState(), "undoDisabled": undo.disabled, "redoDisabled": redo.disabled})
	shortcut(KEY_Z, true)
	await settle(map)
	check("control_shift_z_restores_exact_build", content_matches(map, built) and history_counts(map, 1, 0) and map.StateToken != built.get("sourceToken"), map.GetHistoryState())
	await click_control(undo)
	await settle(map)
	check("undo_button_removes_build", map.GetRoadState().is_empty() and history_counts(map, 0, 1))
	shortcut(KEY_Y)
	await settle(map)
	check("control_y_restores_build", content_matches(map, built) and history_counts(map, 1, 0))
	shortcut(KEY_Z)
	await settle(map)
	await click_control(redo)
	await settle(map)
	check("redo_button_restores_build", content_matches(map, built) and history_counts(map, 1, 0))
	await batch_history(map, built)
	await cancelled_history(map)
	await loop_history_and_load(map)
	await RenderingServer.frame_post_draw
	var capture_path: String = OS.get_environment("V4_QA_OUTPUT")
	var capture_dir: String = ProjectSettings.globalize_path(capture_path if not capture_path.is_empty() else "res://.scratch/v4-15-qa")
	var directory_error: int = DirAccess.make_dir_recursive_absolute(capture_dir)
	check("restored_loop_screenshot", (directory_error == OK or directory_error == ERR_ALREADY_EXISTS) and root.get_texture().get_image().save_png(capture_dir.path_join("history-restored-loop.png")) == OK)
	print("V4_HISTORY_RESULT ", JSON.stringify({"rows": _rows, "passed": _passed}))
	map.queue_free()
	await process_frame
	if _passed:
		print("PASS V4 history runtime contract")
		quit(0)
	else:
		push_error("FAIL V4 history runtime contract")
		quit(1)

func batch_history(map: Node, built: Dictionary) -> void:
	check("batch_change_mode", map.SetToolMode(3))
	var profile: OptionButton = map.get_node("HUD/Panel/Margin/Controls/Profile")
	profile.select(3)
	press(map, Vector2(150, 0))
	await process_frame
	motion(map, Vector2(350, 0))
	await process_frame
	check("held_batch_does_not_add_history", map.GetSelectionState().get("selectedCount") == 3 and content_matches(map, built) and history_counts(map, 1, 0))
	release(map, Vector2(350, 0))
	await settle(map)
	var changed: Dictionary = map.GetRoadState()
	check("batch_change_adds_one_entry", history_counts(map, 2, 0) and [150, 250, 350].all(func(x): return map.PickRoad(Vector2(x, 0)).get("profile") == "highway") and [50, 450].all(func(x): return map.PickRoad(Vector2(x, 0)).get("profile") == "street"))
	shortcut(KEY_Z)
	await settle(map)
	check("one_undo_restores_entire_profile_batch", content_matches(map, built) and history_counts(map, 1, 1))
	shortcut(KEY_Y)
	await settle(map)
	check("one_redo_restores_entire_profile_batch", content_matches(map, changed) and history_counts(map, 2, 0))
	# Undo/redo restores content under a fresh sequence. No-change must preserve this
	# newly published snapshot, including its token, rather than the pre-undo token.
	changed = map.GetRoadState()
	var history_before: Dictionary = map.GetHistoryState()
	var phase_before: String = map.BuildPhase
	press(map, Vector2(150, 0))
	await process_frame
	motion(map, Vector2(350, 0))
	await process_frame
	release(map, Vector2(350, 0))
	await settle(map)
	check("same_profile_batch_keeps_history_and_version", map.GetHistoryState() == history_before and map.GetRoadState() == changed and map.BuildPhase == phase_before and not map.IsBuildBusy)
	check("batch_delete_mode", map.SetToolMode(2))
	press(map, Vector2(50, 0))
	await process_frame
	motion(map, Vector2(250, 0))
	await process_frame
	check("held_delete_crosses_profile_edges_without_history_write", map.GetSelectionState().get("selectedCount") == 3 and map.GetRoadState() == changed and map.GetHistoryState() == history_before)
	release(map, Vector2(250, 0))
	await settle(map)
	var deleted: Dictionary = map.GetRoadState()
	check("batch_delete_adds_one_entry_and_preserves_remainder", history_counts(map, 3, 0) and [50, 150, 250].all(func(x): return map.PickRoad(Vector2(x, 0)).is_empty()) and map.PickRoad(Vector2(350, 0)).get("profile") == "highway" and map.PickRoad(Vector2(450, 0)).get("profile") == "street")
	shortcut(KEY_Z)
	await settle(map)
	check("one_undo_restores_entire_delete_batch", content_matches(map, changed) and history_counts(map, 2, 1))
	shortcut(KEY_Z, true)
	await settle(map)
	check("one_redo_restores_entire_delete_batch", content_matches(map, deleted) and history_counts(map, 3, 0))
	shortcut(KEY_Z)
	await settle(map)
	check("new_build_after_undo_clears_redo", map.SetToolMode(0) and await build(map, Vector2(0, 200), Vector2(500, 200)) and history_counts(map, 3, 0) and not map.RedoRoadEdit() and map.PickRoad(Vector2(250, 200)).get("profile") == "highway")

func cancelled_history(map: Node) -> void:
	var before: Dictionary = map.GetRoadState()
	var history_before: Dictionary = map.GetHistoryState()
	var probe = WORK_PROBE.new()
	probe.Install(map)
	var accepted: bool = map.UndoRoadEdit()
	var deadline: int = Time.get_ticks_msec() + 5000
	while Time.get_ticks_msec() < deadline and (not probe.Started or map.BuildElapsedMilliseconds <= 350):
		await process_frame
	var status: Label = map.get_node("HUD/Panel/Margin/Controls/Status")
	var undo: Button = map.get_node("HUD/Panel/Margin/Controls/History/Undo")
	var redo: Button = map.get_node("HUD/Panel/Margin/Controls/History/Redo")
	check("pending_undo_keeps_history_and_shows_escape_hint", accepted and probe.Started and probe.UsedBackgroundThread and map.IsBuildBusy and map.BuildPhase == "Preparing" and status.text.contains("等待") and status.text.contains("Esc") and map.GetHistoryState() == history_before and map.GetRoadState() == before)
	check("pending_undo_rejects_second_history_operation", undo.disabled and redo.disabled and not map.UndoRoadEdit() and not map.RedoRoadEdit() and map.GetHistoryState() == history_before)
	escape()
	await process_frame
	check("escape_cancels_pending_undo_without_moving_stacks", map.BuildPhase == "Cancelling" and map.IsBuildBusy and map.GetHistoryState() == history_before and map.GetRoadState() == before)
	probe.Release()
	var completed: bool = await settle(map)
	probe.Remove(map)
	check("cancelled_undo_rejects_late_result_and_keeps_all_history", completed and probe.Completed and map.BuildPhase == "Cancelled" and map.GetHistoryState() == history_before and map.GetRoadState() == before and not undo.disabled)
	if probe.Completed: probe.Cleanup()
	shortcut(KEY_Z)
	await settle(map)
	var committed: Dictionary = map.GetHistoryState()
	escape()
	await process_frame
	check("escape_after_completed_undo_does_not_undo_again", history_counts(map, 2, 1) and map.GetHistoryState() == committed and map.PickRoad(Vector2(250, 200)).is_empty() and map.PickRoad(Vector2(250, 0)).get("profile") == "highway" and not status.text.contains("Esc"))

func loop_history_and_load(map: Node) -> void:
	check("new_map_resets_existing_history", map.SetToolMode(0) and map.CreateMap(100) and history_counts(map, 0, 0))
	var profile: OptionButton = map.get_node("HUD/Panel/Margin/Controls/Profile")
	profile.select(1)
	focus(map, Vector2(300, 200), 0.6)
	await process_frame
	var built: bool = await build(map, Vector2.ZERO, Vector2(400, 0))
	built = await build(map, Vector2(400, 0), Vector2(400, 400)) and built
	built = await build(map, Vector2(400, 400), Vector2(0, 400)) and built
	built = await build(map, Vector2(0, 400), Vector2.ZERO) and built
	var pure: Dictionary = map.GetRoadState()
	var edges: Array = pure.get("edges", [])
	check("loop_gestures_have_four_entries", built and history_counts(map, 4, 0) and pure.get("nodeCount") == 1 and edges.size() == 1 and edges[0].get("startNodeId") == edges[0].get("endNodeId"))
	check("branch_moves_loop_seam_as_one_edit", await build(map, Vector2(400, 400), Vector2(600, 400)) and history_counts(map, 5, 0) and map.GetRoadState().get("nodeCount") == 2 and map.GetRoadState().get("edges", []).size() == 2)
	var branched: Dictionary = map.GetRoadState()
	shortcut(KEY_Z)
	await settle(map)
	check("undo_restores_loop_seam_and_visible_owner", content_matches(map, pure) and history_counts(map, 4, 1) and map.PickRoad(Vector2(500, 400)).is_empty() and map.PickRoad(Vector2(200, 0)).get("sourceToken") == map.StateToken)
	shortcut(KEY_Y)
	await settle(map)
	check("redo_restores_loop_branch_and_visible_owner", content_matches(map, branched) and history_counts(map, 5, 0) and map.PickRoad(Vector2(500, 400)).get("sourceToken") == map.StateToken)
	shortcut(KEY_Z)
	await settle(map)
	var history_before: Dictionary = map.GetHistoryState()
	var manager: Node = root.get_node("SaveManager")
	var saved: bool = await SAVE.save_as(manager, "V4 history restored loop QA")
	check("save_preserves_undone_loop_and_session_history", saved and content_matches(map, pure) and history_counts(map, 4, 1) and map.GetHistoryState() == history_before)
	if not saved: return
	var slot: String = manager.CurrentSlotID
	var payload_path: String = "user://saves-v4/" + slot + "/road_network_v4.json"
	var bytes: PackedByteArray = FileAccess.get_file_as_bytes(payload_path)
	shortcut(KEY_Y)
	await settle(map)
	check("redo_after_save_restores_branch", content_matches(map, branched) and history_counts(map, 5, 0))
	var loaded: bool = await SAVE.operation_succeeded(manager, map.LoadSlot(slot))
	await process_frame
	check("load_restores_saved_loop_with_empty_history", loaded and content_matches(map, pure) and history_counts(map, 0, 0) and map.GetHistoryState().get("estimatedRetainedBytes", -1) == 0 and map.StateToken != history_before.get("sourceToken") and not map.UndoRoadEdit() and not map.RedoRoadEdit())
	var resaved: bool = await SAVE.save(manager, slot)
	check("history_is_not_serialized_into_codec", resaved and not bytes.is_empty() and FileAccess.get_file_as_bytes(payload_path) == bytes)
	var deleted: bool = await SAVE.delete_slot(manager, slot)
	check("history_save_fixture_deleted", deleted and not DirAccess.dir_exists_absolute(ProjectSettings.globalize_path("user://saves-v4/" + slot)))

func history_counts(map: Node, undo_count: int, redo_count: int) -> bool:
	var state: Dictionary = map.GetHistoryState()
	return state.get("undoCount", -1) == undo_count and state.get("redoCount", -1) == redo_count and state.get("retainedCount", -1) == undo_count + redo_count

func history_transition_matches(before: Dictionary, after: Dictionary) -> bool:
	return after.get("sourceToken") != before.get("sourceToken") and after.get("changeSequence", 0) == before.get("changeSequence", 0) + 1 and after.get("nextNodeId", 0) >= before.get("nextNodeId", 0) and after.get("nextEdgeId", 0) >= before.get("nextEdgeId", 0)

func content_matches(map: Node, expected: Dictionary) -> bool:
	var actual: Dictionary = map.GetRoadState()
	return map.IsPresentationCurrent and actual.get("edges", []) == expected.get("edges", []) and actual.get("nodeCount", 0) == expected.get("nodeCount", 0)

func click_control(control: Control) -> void:
	button(control.get_global_rect().get_center(), true)
	await process_frame
	button(control.get_global_rect().get_center(), false)
	await process_frame

func shortcut(key: Key, shift: bool = false) -> void:
	var event := InputEventKey.new()
	event.keycode = key
	event.ctrl_pressed = true
	event.shift_pressed = shift
	event.pressed = true
	Input.parse_input_event(event)
	var released := InputEventKey.new()
	released.keycode = key
	released.ctrl_pressed = true
	released.shift_pressed = shift
	Input.parse_input_event(released)
