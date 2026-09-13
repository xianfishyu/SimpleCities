extends "res://tests/godot/v4_history_runtime_contract.gd"

const DISPLAY_PROBE = preload("res://tests/godot/V4DisplayFailureProbe.cs")
const DISPLAY_PREFLIGHT_PROBE = preload("res://tests/godot/V4DisplayPreflightProbe.cs")
const DISPLAY_CONTROLS: String = "HUD/Panel/Margin/Controls/"

func run() -> void:
	var preflight: Dictionary = DISPLAY_PREFLIGHT_PROBE.new().Run()
	for row in preflight.get("results", []):
		check("resource_" + str(row.get("name", "unknown")), row.get("passed", false), row.get("error", ""))
	var map = load("res://Scenes/V4MapTest.tscn").instantiate()
	root.add_child(map)
	current_scene = map
	await process_frame
	check("display_map_created", map.CreateMap(100))
	focus(map, Vector2(250, 0), 1.2)
	await process_frame
	check("display_baseline_road_built", await build(map, Vector2.ZERO, Vector2(500, 0)))
	var baseline: Dictionary = map.GetRoadState()
	var baseline_history: Dictionary = map.GetHistoryState()
	await RenderingServer.frame_post_draw
	var baseline_pixels: PackedByteArray = road_pixels(map)
	var probe = DISPLAY_PROBE.new()
	probe.Install(map, "preflight", false)
	await request_build(map, Vector2(0, 200), Vector2(500, 200))
	check("preflight_failure_keeps_core_history_and_current_display", probe.FaultCalls == 1 and not map.IsBuildBusy and map.IsPresentationCurrent and map.GetRoadState() == baseline and map.GetHistoryState() == baseline_history, {"phase": map.BuildPhase, "state": presentation(map)})
	await RenderingServer.frame_post_draw
	check("preflight_failure_preserves_rendered_road_pixels", not baseline_pixels.is_empty() and road_pixels(map) == baseline_pixels)
	var status: Label = map.get_node(DISPLAY_CONTROLS + "Status")
	check("preflight_failure_is_visible_and_editing_remains_available", status.text.contains("Injected V4 display preflight failure.") and map.SetToolMode(0))
	probe.Cleanup()
	probe = DISPLAY_PROBE.new()
	probe.Install(map, "publish", false)
	await request_build(map, Vector2(0, 200), Vector2(500, 200))
	var committed: Dictionary = map.GetRoadState()
	var committed_history: Dictionary = map.GetHistoryState()
	var failed: Dictionary = presentation(map)
	check("publish_failure_commits_core_and_history_once", probe.FaultCalls == 1 and not map.IsBuildBusy and not map.IsPresentationCurrent and committed.get("sourceToken") != baseline.get("sourceToken") and committed.get("edges", []).size() == 2 and history_counts(map, 2, 0), {"phase": map.BuildPhase, "state": failed})
	check("publish_failure_retains_last_complete_display_tokens", failed.get("phase") == "Failed" and failed.get("desiredToken") == map.StateToken and failed.get("presentedToken") == baseline.get("sourceToken") and failed.get("drawnToken") == baseline.get("sourceToken") and not failed.get("canEdit", true), failed)
	await RenderingServer.frame_post_draw
	check("publish_failure_preserves_rendered_road_pixels", not baseline_pixels.is_empty() and road_pixels(map) == baseline_pixels)
	check("publish_failure_has_persistent_message_and_manual_retry", recovery_visible(map))
	check("committed_display_failure_does_not_offer_road_cancellation", not status.text.contains("Esc") and not status.text.contains("取消"), status.text)
	var error_before: String = recovery_message(map)
	await rejected_inputs(map, committed, committed_history)
	check("failed_display_camera_remains_responsive", await pan_camera(map))
	var manager: Node = root.get_node("SaveManager")
	var save_button: Button = map.get_node(DISPLAY_CONTROLS + "Save")
	check("failed_display_save_button_remains_enabled", not save_button.disabled)
	await click_control(save_button)
	var saved: bool = await SAVE.operation_succeeded(manager, map.LastOperationToken)
	var slot: String = manager.CurrentSlotID if saved else ""
	await process_frame
	check("failed_display_can_save_committed_road_state", saved and map.GetRoadState() == committed and map.GetHistoryState() == committed_history and not map.IsPresentationCurrent)
	check("save_does_not_hide_display_failure", recovery_visible(map) and recovery_message(map) == error_before)
	probe.Cleanup()
	await retry_failures_and_success(map, manager, slot, committed, committed_history)
	await first_draw_failure(map)
	for replacement in ["new_map", "load"]:
		for late_failure in [false, true]:
			await late_retry_is_discarded(map, manager, slot, committed, replacement, late_failure)
	await loaded_first_draw_failure(map, manager, slot, committed)
	var deleted: bool = await SAVE.delete_slot(manager, slot) if saved else true
	check("display_save_fixture_deleted", deleted)
	await RenderingServer.frame_post_draw
	var capture_path: String = OS.get_environment("V4_QA_OUTPUT")
	var capture_dir: String = ProjectSettings.globalize_path(capture_path if not capture_path.is_empty() else "res://.scratch/v4-18-qa")
	var directory_error: int = DirAccess.make_dir_recursive_absolute(capture_dir)
	check("recovered_display_screenshot", (directory_error == OK or directory_error == ERR_ALREADY_EXISTS) and root.get_texture().get_image().save_png(capture_dir.path_join("display-recovered.png")) == OK)
	print("V4_DISPLAY_RETRY_RESULT ", JSON.stringify({"rows": _rows, "passed": _passed}))
	map.queue_free()
	await process_frame
	if _passed:
		print("PASS V4 display retry runtime contract")
		quit(0)
	else:
		push_error("FAIL V4 display retry runtime contract")
		quit(1)

func presentation(map: Node) -> Dictionary:
	return map.GetPresentationState() if map.has_method("GetPresentationState") else {}

func request_build(map: Node, start: Vector2, end: Vector2) -> bool:
	press(map, start)
	motion(map, end)
	release(map, end)
	var deadline: int = Time.get_ticks_msec() + 5000
	while Time.get_ticks_msec() < deadline:
		await process_frame
		if not map.IsBuildBusy: return true
	return false

func rejected_inputs(map: Node, road: Dictionary, history: Dictionary) -> void:
	check("failed_display_rejects_pick_and_tool_switch", map.PickRoad(Vector2(250, 0)).is_empty() and not map.SetToolMode(1) and not map.SetToolMode(2) and not map.SetToolMode(3))
	press(map, Vector2(0, -200))
	motion(map, Vector2(500, -200))
	release(map, Vector2(500, -200))
	shortcut(KEY_Z)
	shortcut(KEY_Y)
	escape()
	await process_frame
	var undo: Button = map.get_node(DISPLAY_CONTROLS + "History/Undo")
	var redo: Button = map.get_node(DISPLAY_CONTROLS + "History/Redo")
	var tools: OptionButton = map.get_node(DISPLAY_CONTROLS + "ToolMode")
	check("failed_display_rejects_edit_history_and_selection", not map.UndoRoadEdit() and not map.RedoRoadEdit() and not map.HasBuildPreview and map.GetSelectionState().get("selectedCount", -1) == 0 and map.GetRoadState() == road and map.GetHistoryState() == history and undo.disabled and redo.disabled and tools.disabled)

func recovery_message(map: Node) -> String:
	var message: Label = map.get_node_or_null(DISPLAY_CONTROLS + "DisplayRecovery/Message")
	return message.text if message != null else ""

func recovery_visible(map: Node) -> bool:
	var message: Label = map.get_node_or_null(DISPLAY_CONTROLS + "DisplayRecovery/Message")
	var retry: Button = map.get_node_or_null(DISPLAY_CONTROLS + "DisplayRecovery/Retry")
	return message != null and message.is_visible_in_tree() and not message.text.is_empty() and retry != null and retry.is_visible_in_tree() and not retry.disabled

func road_pixels(map: Node) -> PackedByteArray:
	# Include all three possible road rows while excluding the changing status panel.
	var center: Vector2 = map.get_canvas_transform() * Vector2(250, 0)
	var rect := Rect2i(Vector2i(center) - Vector2i(120, 260), Vector2i(240, 520))
	var image: Image = root.get_texture().get_image()
	if not Rect2i(Vector2i.ZERO, image.get_size()).encloses(rect): return PackedByteArray()
	return image.get_region(rect).get_data()

func pan_camera(map: Node) -> bool:
	var camera: Camera2D = map.get_node("Camera2D")
	var before: Vector2 = camera.position
	var pan := InputEventMouseMotion.new()
	pan.position = map.get_canvas_transform() * Vector2(250, 0)
	pan.relative = Vector2(20, 0)
	pan.button_mask = MOUSE_BUTTON_MASK_MIDDLE
	Input.parse_input_event(pan)
	await process_frame
	return camera.position != before

func retry_failures_and_success(map: Node, manager: Node, slot: String, road: Dictionary, history: Dictionary) -> void:
	var payload_path: String = "user://saves-v4/" + slot + "/road_network_v4.json"
	var saved_bytes: PackedByteArray = FileAccess.get_file_as_bytes(payload_path) if not slot.is_empty() else PackedByteArray()
	var retry: Button = map.get_node(DISPLAY_CONTROLS + "DisplayRecovery/Retry")
	check("retry_control_is_inside_viewport", root.get_visible_rect().encloses(retry.get_global_rect()))
	var gate = DISPLAY_PROBE.new()
	gate.InstallRetryGate(map, true)
	await click_control(retry)
	await wait_worker(gate)
	var pending: Dictionary = presentation(map)
	check("retry_button_starts_background_work_and_disables_editing", gate.Started and gate.UsedBackgroundThread and map.IsDisplayRetryBusy and pending.get("phase") == "Preparing" and not pending.get("canEdit", true) and retry.disabled and map.GetRoadState() == road and map.GetHistoryState() == history, pending)
	var wait_deadline: int = Time.get_ticks_msec() + 400
	while Time.get_ticks_msec() < wait_deadline:
		await process_frame
	var status: Label = map.get_node(DISPLAY_CONTROLS + "Status")
	escape()
	await process_frame
	check("retry_wait_has_no_cancel_hint_and_escape_does_not_undo_commit", not status.text.contains("Esc") and not status.text.contains("取消") and map.IsDisplayRetryBusy and presentation(map).get("phase") == "Preparing" and map.GetRoadState() == road and map.GetHistoryState() == history, status.text)
	for _click in range(3):
		await click_control(retry)
	check("repeated_retry_clicks_are_singleflight", gate.RetryCalls == 1 and not map.RetryRoadDisplay() and map.IsDisplayRetryBusy)
	var save_button: Button = map.get_node(DISPLAY_CONTROLS + "Save")
	check("retry_keeps_camera_and_save_available", not save_button.disabled and await pan_camera(map) and await SAVE.save(manager, slot) and FileAccess.get_file_as_bytes(payload_path) == saved_bytes and map.GetRoadState() == road and map.GetHistoryState() == history)
	gate.Release()
	await wait_retry_finished(map)
	check("retry_worker_failure_preserves_committed_state_and_allows_retry", gate.Completed and presentation(map).get("phase") == "Failed" and recovery_visible(map) and map.GetRoadState() == road and map.GetHistoryState() == history)
	await cleanup_gate(gate)
	for stage in ["preflight", "publish"]:
		var fault = DISPLAY_PROBE.new()
		fault.Install(map, stage, true)
		await click_control(retry)
		await wait_retry_finished(map)
		check("retry_" + stage + "_failure_preserves_version_ids_and_history", fault.FaultCalls == 1 and presentation(map).get("phase") == "Failed" and recovery_visible(map) and map.GetRoadState() == road and map.GetHistoryState() == history)
		fault.Cleanup()
	gate = DISPLAY_PROBE.new()
	gate.InstallRetryGate(map, false)
	await click_control(retry)
	await wait_worker(gate)
	check("successful_retry_still_uses_single_worker", gate.Started and gate.UsedBackgroundThread and gate.RetryCalls == 1 and not map.RetryRoadDisplay())
	gate.Release()
	await wait_retry_finished(map)
	await RenderingServer.frame_post_draw
	var recovered: Dictionary = presentation(map)
	check("manual_retry_publishes_and_draws_current_snapshot", gate.Completed and recovered.get("phase") == "Current" and recovered.get("desiredToken") == map.StateToken and recovered.get("presentedToken") == map.StateToken and recovered.get("drawnToken") == map.StateToken and recovered.get("canEdit", false) and map.IsPresentationCurrent and not map.IsDisplayRetryBusy and not map.get_node(DISPLAY_CONTROLS + "DisplayRecovery").visible, recovered)
	check("retry_success_does_not_keep_stale_cancel_hint", not status.text.contains("Esc") and not status.text.contains("取消"), status.text)
	check("retry_does_not_recommit_or_change_codec_bytes", map.GetRoadState() == road and map.GetHistoryState() == history and await SAVE.save(manager, slot) and not saved_bytes.is_empty() and FileAccess.get_file_as_bytes(payload_path) == saved_bytes)
	check("recovered_road_can_be_picked_and_current_retry_is_noop", map.PickRoad(Vector2(250, 200)).get("sourceToken") == map.StateToken and map.SetToolMode(1) and not map.RetryRoadDisplay() and map.GetRoadState() == road and map.GetHistoryState() == history)
	await cleanup_gate(gate)
	check("recovered_history_controls_are_enabled", not map.get_node(DISPLAY_CONTROLS + "History/Undo").disabled)

func first_draw_failure(map: Node) -> void:
	check("draw_failure_fixture_mode", map.SetToolMode(0))
	focus(map, Vector2(250, 0), 1.2)
	await process_frame
	await RenderingServer.frame_post_draw
	var old_road: Dictionary = map.GetRoadState()
	var old_history: Dictionary = map.GetHistoryState()
	var old_pixels: PackedByteArray = road_pixels(map)
	var fault = DISPLAY_PROBE.new()
	fault.Install(map, "draw", true)
	press(map, Vector2(0, -200))
	motion(map, Vector2(500, -200))
	release(map, Vector2(500, -200))
	var deadline: int = Time.get_ticks_msec() + 5000
	while fault.FaultCalls == 0 and Time.get_ticks_msec() < deadline:
		await RenderingServer.frame_post_draw
	check("first_failed_draw_retains_previous_road_in_same_frame", fault.FaultCalls == 1 and not old_pixels.is_empty() and road_pixels(map) == old_pixels)
	await process_frame
	var road: Dictionary = map.GetRoadState()
	var history: Dictionary = map.GetHistoryState()
	var state: Dictionary = presentation(map)
	check("draw_failure_keeps_one_committed_change_and_old_display", state.get("phase") == "Failed" and state.get("presentedToken") == old_road.get("sourceToken") and state.get("drawnToken") == old_road.get("sourceToken") and road.get("sourceToken") != old_road.get("sourceToken") and history.get("undoCount") == old_history.get("undoCount", 0) + 1 and not state.get("canEdit", true), state)
	await click_control(map.get_node(DISPLAY_CONTROLS + "DisplayRecovery/Retry"))
	await wait_retry_finished(map)
	await RenderingServer.frame_post_draw
	check("repeated_draw_failure_keeps_fallback_and_allows_another_retry", fault.FaultCalls == 2 and recovery_visible(map) and presentation(map).get("phase") == "Failed" and road_pixels(map) == old_pixels and map.GetRoadState() == road and map.GetHistoryState() == history)
	fault.Cleanup()
	await click_control(map.get_node(DISPLAY_CONTROLS + "DisplayRecovery/Retry"))
	await wait_retry_finished(map)
	check("draw_failure_recovers_without_second_core_commit", map.IsPresentationCurrent and presentation(map).get("phase") == "Current" and map.GetRoadState() == road and map.GetHistoryState() == history and map.PickRoad(Vector2(250, -200)).get("sourceToken") == map.StateToken)

func late_retry_is_discarded(map: Node, manager: Node, slot: String, target: Dictionary, replacement: String, late_failure: bool) -> void:
	var label: String = replacement + ("_late_failure" if late_failure else "_late_success")
	check(label + "_fixture_map_created", map.CreateMap(100) and map.SetToolMode(0))
	focus(map, Vector2(250, 0), 1.2)
	await process_frame
	check(label + "_fixture_baseline_built", await build(map, Vector2.ZERO, Vector2(500, 0)))
	var fault = DISPLAY_PROBE.new()
	fault.Install(map, "publish", false)
	await request_build(map, Vector2(0, 200), Vector2(500, 200))
	check(label + "_fixture_display_failed", fault.FaultCalls == 1 and presentation(map).get("phase") == "Failed")
	fault.Cleanup()
	var gate = DISPLAY_PROBE.new()
	gate.InstallRetryGate(map, late_failure)
	await click_control(map.get_node(DISPLAY_CONTROLS + "DisplayRecovery/Retry"))
	await wait_worker(gate)
	check(label + "_worker_is_pending", gate.Started and map.IsDisplayRetryBusy and presentation(map).get("phase") == "Preparing")
	var replaced: bool = map.CreateMap(50) if replacement == "new_map" else await SAVE.operation_succeeded(manager, map.LoadSlot(slot))
	await process_frame
	await RenderingServer.frame_post_draw
	var road: Dictionary = map.GetRoadState()
	var history: Dictionary = map.GetHistoryState()
	var token: String = map.StateToken
	check(label + "_replacement_publishes_while_old_worker_waits", replaced and map.IsPresentationCurrent and presentation(map).get("phase") == "Current" and history_counts(map, 0, 0) and (map.CellSizeMetres == 50 and road.is_empty() if replacement == "new_map" else content_matches(map, target)))
	gate.Release()
	await wait_worker_completed(gate)
	await wait_retry_finished(map)
	for _frame in range(6):
		await process_frame
	var after: Dictionary = presentation(map)
	check(label + "_cannot_publish_or_report_error_into_replacement", gate.Completed and map.StateToken == token and map.GetRoadState() == road and map.GetHistoryState() == history and after.get("phase") == "Current" and after.get("error", "missing").is_empty() and after.get("presentedToken") == token and after.get("drawnToken") == token and not map.IsDisplayRetryBusy and not map.get_node(DISPLAY_CONTROLS + "DisplayRecovery").visible, after)
	await cleanup_gate(gate)

func loaded_first_draw_failure(map: Node, manager: Node, slot: String, target: Dictionary) -> void:
	check("loaded_draw_fixture_created", map.CreateMap(50) and map.SetToolMode(0))
	focus(map, Vector2(250, 0), 1.2)
	await process_frame
	check("loaded_draw_fixture_road_built", await build(map, Vector2(0, -200), Vector2(500, -200)))
	await RenderingServer.frame_post_draw
	var old_token: String = map.StateToken
	var old_pixels: PackedByteArray = road_pixels(map)
	var fault = DISPLAY_PROBE.new()
	fault.Install(map, "draw", false)
	var operation: String = map.LoadSlot(slot)
	var deadline: int = Time.get_ticks_msec() + 5000
	while fault.FaultCalls == 0 and Time.get_ticks_msec() < deadline:
		await RenderingServer.frame_post_draw
	check("loaded_first_failed_draw_preserves_complete_previous_frame", fault.FaultCalls == 1 and not old_pixels.is_empty() and road_pixels(map) == old_pixels)
	var result: Dictionary = await SAVE.wait_for_operation(manager, operation, 5.0)
	var idle: bool = await SAVE.wait_for_idle(manager, 5.0)
	await process_frame
	var road: Dictionary = map.GetRoadState()
	var history: Dictionary = map.GetHistoryState()
	var state: Dictionary = presentation(map)
	check("loaded_draw_failure_keeps_successful_load_and_new_lineage", idle and result.get("committed", false) and manager.CurrentSlotID == slot and map.StateToken != old_token and map.StateToken != target.get("sourceToken") and road.get("edges") == target.get("edges") and history_counts(map, 0, 0) and map.CellSizeMetres == 100 and map.PresentedCellSizeMetres == 50, {"result": result, "state": state})
	check("loaded_draw_failure_exposes_retry_with_old_display", state.get("phase") == "Failed" and state.get("presentedToken") == old_token and state.get("drawnToken") == old_token and not state.get("canEdit", true) and recovery_visible(map), state)
	fault.Cleanup()
	await click_control(map.get_node(DISPLAY_CONTROLS + "DisplayRecovery/Retry"))
	await wait_retry_finished(map)
	check("loaded_draw_retry_restores_display_without_repeating_load", map.IsPresentationCurrent and presentation(map).get("phase") == "Current" and map.PresentedCellSizeMetres == 100 and manager.CurrentSlotID == slot and map.GetRoadState() == road and map.GetHistoryState() == history and map.PickRoad(Vector2(250, 200)).get("sourceToken") == map.StateToken)

func wait_worker(probe: RefCounted) -> bool:
	var deadline: int = Time.get_ticks_msec() + 5000
	while not probe.Started and Time.get_ticks_msec() < deadline:
		await process_frame
	return probe.Started

func wait_worker_completed(probe: RefCounted) -> bool:
	var deadline: int = Time.get_ticks_msec() + 5000
	while not probe.Completed and Time.get_ticks_msec() < deadline:
		await process_frame
	return probe.Completed

func cleanup_gate(probe: RefCounted) -> void:
	probe.Release()
	if probe.Started:
		await wait_worker_completed(probe)
	if not probe.Started or probe.Completed:
		probe.Cleanup()
	else:
		check("display_retry_worker_cleanup_timeout", false)

func wait_retry_finished(map: Node) -> bool:
	var deadline: int = Time.get_ticks_msec() + 5000
	while Time.get_ticks_msec() < deadline:
		await process_frame
		if not map.IsDisplayRetryBusy and presentation(map).get("phase") not in ["Preparing", "AwaitingDraw"]: return true
	return false
