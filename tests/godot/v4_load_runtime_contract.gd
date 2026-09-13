extends "res://tests/godot/v4_history_runtime_contract.gd"

const LOAD_PROBE = preload("res://tests/godot/V4LoadWorkProbe.cs")

func run() -> void:
	var map = load("res://Scenes/V4MapTest.tscn").instantiate()
	root.add_child(map)
	current_scene = map
	await process_frame
	var manager: Node = root.get_node("SaveManager")
	check("target_map_created", map.CreateMap(50))
	focus(map, Vector2(250, 0), 1.2)
	await process_frame
	check("target_road_built", await build(map, Vector2(0, 100), Vector2(500, 100)))
	var target_road: Dictionary = map.GetRoadState()
	var target_saved: bool = await SAVE.save_as(manager, "V4 load target QA")
	check("target_slot_saved", target_saved)
	var target_slot: String = manager.CurrentSlotID if target_saved else ""
	check("current_map_created", map.CreateMap(100))
	var built: bool = await build(map, Vector2.ZERO, Vector2(500, 0))
	built = await build(map, Vector2(0, 200), Vector2(500, 200)) and built
	shortcut(KEY_Z)
	await settle(map)
	check("current_map_has_undo_and_redo", built and history_counts(map, 1, 1))
	var current_saved: bool = await SAVE.save_as(manager, "V4 load current QA")
	check("current_slot_saved", current_saved)
	var current_slot: String = manager.CurrentSlotID if current_saved else ""
	check("selection_mode", map.SetToolMode(1))
	press(map, Vector2(150, 0))
	await process_frame
	motion(map, Vector2(350, 0))
	await process_frame
	var road_before: Dictionary = map.GetRoadState()
	var history_before: Dictionary = map.GetHistoryState()
	var selection_before: Dictionary = map.GetSelectionState()
	check("held_selection_fixture", selection_before.get("selecting", false) and selection_before.get("selectedCount", 0) == 3)
	var probe = LOAD_PROBE.new()
	probe.Install(manager, map, "fail-preflight")
	var operation: String = map.LoadSlot(target_slot)
	var deadline: int = Time.get_ticks_msec() + 5000
	while not probe.Started and Time.get_ticks_msec() < deadline:
		await process_frame
	check("load_prepares_in_background_without_publishing", not operation.is_empty() and probe.Started and probe.UsedBackgroundThread and manager.IsOperationBusy and unchanged(map, manager, road_before, history_before, selection_before, current_slot))
	release(map, Vector2(350, 0))
	await process_frame
	motion(map, Vector2(450, 0), false)
	await process_frame
	var ui_motion := InputEventMouseMotion.new()
	ui_motion.position = map.get_node("HUD/Panel").get_global_rect().get_center()
	Input.parse_input_event(ui_motion)
	await process_frame
	escape()
	await process_frame
	check("load_freezes_held_selection_against_release_hover_and_escape", unchanged(map, manager, road_before, history_before, selection_before, current_slot), {"before": selection_before, "after": map.GetSelectionState()})
	var camera: Camera2D = map.get_node("Camera2D")
	var camera_before: Vector2 = camera.position
	var pan := InputEventMouseMotion.new()
	pan.position = map.get_canvas_transform() * Vector2(250, 0)
	pan.relative = Vector2(20, 0)
	pan.button_mask = MOUSE_BUTTON_MASK_MIDDLE
	Input.parse_input_event(pan)
	await process_frame
	check("load_wait_keeps_camera_responsive", camera.position != camera_before and manager.IsOperationBusy)
	probe.Release()
	var result: Dictionary = await SAVE.wait_for_operation(manager, operation, 5.0)
	var idle: bool = await SAVE.wait_for_idle(manager, 5.0)
	await process_frame
	check("preflight_failure_preserves_network_history_tools_and_slot", idle and probe.Completed and not result.get("committed", true) and unchanged(map, manager, road_before, history_before, selection_before, current_slot), {"result": result, "selection": map.GetSelectionState()})
	if probe.Completed: probe.Cleanup()
	await successful_load(map, manager, target_slot, target_road)
	await late_preview_after_load(map, manager, target_slot, target_road)
	await failed_save_keeps_slot(map, manager, current_slot)
	await malformed_payload_is_rejected(map, manager, target_slot)
	for preview_fails in [false, true]:
		await preview_recovers_after_failed_load(map, manager, target_slot, preview_fails)
	escape()
	await process_frame
	var target_deleted: bool = await SAVE.delete_slot(manager, target_slot) if target_saved else true
	var current_deleted: bool = await SAVE.delete_slot(manager, current_slot) if current_saved else true
	check("load_save_fixtures_deleted", target_deleted and current_deleted)
	print("V4_LOAD_RESULT ", JSON.stringify({"rows": _rows, "passed": _passed}))
	map.queue_free()
	await process_frame
	if _passed:
		print("PASS V4 load runtime contract")
		quit(0)
	else:
		push_error("FAIL V4 load runtime contract")
		quit(1)

func successful_load(map: Node, manager: Node, target_slot: String, target_road: Dictionary) -> void:
	var road_before: Dictionary = map.GetRoadState()
	var history_before: Dictionary = map.GetHistoryState()
	var selection_before: Dictionary = map.GetSelectionState()
	var slot_before: String = manager.CurrentSlotID
	var probe = LOAD_PROBE.new()
	probe.Install(manager, map, "observe-notifications")
	var operation: String = map.LoadSlot(target_slot)
	var deadline: int = Time.get_ticks_msec() + 5000
	while not probe.Started and Time.get_ticks_msec() < deadline:
		await process_frame
	check("successful_load_wait_preserves_held_selection", probe.Started and probe.UsedBackgroundThread and selection_before.get("selecting", false) and selection_before.get("selectedCount", 0) == 3 and unchanged(map, manager, road_before, history_before, selection_before, slot_before))
	probe.Release()
	var result: Dictionary = await SAVE.wait_for_operation(manager, operation, 5.0)
	var idle: bool = await SAVE.wait_for_idle(manager, 5.0)
	await process_frame
	await RenderingServer.frame_post_draw
	check("observer_failure_is_a_committed_warning", idle and probe.Completed and result.get("committed", false) and result.get("resultKind", -1) == SAVE.RESULT_SUCCEEDED_WITH_WARNINGS and str(result.get("warnings", "")).contains("Injected V4 external load observer failure."), result)
	check("first_notification_observes_already_clean_tools_and_history", probe.NotificationObserved and probe.ObservedCleanState)
	var selection: Dictionary = map.GetSelectionState()
	check("load_publishes_target_map_slot_and_empty_session_state", map.CellSizeMetres == 50 and map.PresentedCellSizeMetres == 50 and manager.CurrentSlotID == target_slot and history_counts(map, 0, 0) and selection.get("selectedCount", -1) == 0 and not selection.get("selecting", true) and not selection.get("hasHover", true) and selection.get("strokes", []).is_empty())
	var hit: Dictionary = map.PickRoad(Vector2(250, 100))
	check("loaded_geometry_and_visible_owner_match_saved_core", content_matches(map, target_road) and hit.get("sourceToken") == map.StateToken and hit.get("surfaceCenter") == Vector2(250, 100) and map.PickRoad(Vector2(250, 0)).is_empty(), hit)
	check("load_establishes_new_lineage", not lineage(map.StateToken).is_empty() and lineage(map.StateToken) != lineage(history_before.get("sourceToken", "")) and lineage(map.StateToken) != lineage(target_road.get("sourceToken", "")))
	if probe.Completed: probe.Cleanup()

func lineage(token: String) -> String:
	return token.get_slice("Lineage = ", 1).get_slice(",", 0).strip_edges()

func late_preview_after_load(map: Node, manager: Node, target_slot: String, target_road: Dictionary) -> void:
	check("late_preview_map_created", map.SetToolMode(0) and map.CreateMap(100))
	focus(map, Vector2(250, 0), 1.2)
	await process_frame
	var preview_probe = WORK_PROBE.new()
	preview_probe.InstallPreview(map)
	press(map, Vector2.ZERO)
	motion(map, Vector2(500, 0))
	var deadline: int = Time.get_ticks_msec() + 5000
	while not preview_probe.Started and Time.get_ticks_msec() < deadline:
		await process_frame
	check("old_preview_is_pending_before_load", preview_probe.Started and map.HasBuildPreview and map.GetBuildPreview().get("phase") == "Pending")
	var load_probe = LOAD_PROBE.new()
	load_probe.Install(manager, map, "normal")
	var operation: String = map.LoadSlot(target_slot)
	deadline = Time.get_ticks_msec() + 5000
	while not load_probe.Started and Time.get_ticks_msec() < deadline:
		await process_frame
	load_probe.Release()
	var loaded: bool = await SAVE.operation_succeeded(manager, operation)
	await process_frame
	check("successful_load_clears_old_pending_preview", loaded and load_probe.Started and content_matches(map, target_road) and map.CellSizeMetres == 50 and not map.HasBuildPreview and map.GetBuildPreview().get("phase") == "None" and map.GetBuildPreview().get("segments", []).is_empty())
	if load_probe.Completed: load_probe.Cleanup()
	var loaded_road: Dictionary = map.GetRoadState()
	var loaded_history: Dictionary = map.GetHistoryState()
	preview_probe.Release()
	preview_probe.RemovePreview(map)
	# A fresh preview can only complete after the single old worker has drained.
	# Its distinct geometry also exposes any old result that replaces the new draft.
	press(map, Vector2(0, 200))
	motion(map, Vector2(500, 200))
	await process_frame
	deadline = Time.get_ticks_msec() + 5000
	while map.GetBuildPreview().get("phase") == "Pending" and Time.get_ticks_msec() < deadline:
		await process_frame
	var preview: Dictionary = map.GetBuildPreview()
	var segments: Array = preview.get("segments", [])
	check("late_old_worker_cannot_replace_new_lineage_preview", preview_probe.Completed and preview.get("phase") == "Ready" and preview.get("sourceToken") == map.StateToken and segments.size() == 1 and segments[0].get("start") == Vector2(0, 200) and segments[0].get("end") == Vector2(500, 200) and map.GetRoadState() == loaded_road and map.GetHistoryState() == loaded_history, preview)
	if preview_probe.Completed: preview_probe.Cleanup()
	escape()
	await process_frame
	release(map, Vector2(500, 200))
	await settle(map)

func failed_save_keeps_slot(map: Node, manager: Node, manual_slot: String) -> void:
	var directory: String = "user://saves-v4/" + manual_slot + "/"
	var payload: PackedByteArray = FileAccess.get_file_as_bytes(directory + "road_network_v4.json")
	var manifest: PackedByteArray = FileAccess.get_file_as_bytes(directory + "manifest.json")
	var slot_before: String = manager.CurrentSlotID
	var road_before: Dictionary = map.GetRoadState()
	var history_before: Dictionary = map.GetHistoryState()
	var lock_probe = LOAD_PROBE.new()
	lock_probe.LockSaveManifest(manual_slot)
	var operation: String = manager.StartSave(manual_slot)
	var result: Dictionary = await SAVE.wait_for_operation(manager, operation, 5.0)
	var idle: bool = await SAVE.wait_for_idle(manager, 5.0)
	# Release the external resource before assertions and reads, even on a failed wait.
	lock_probe.ReleaseSaveLock()
	check("locked_save_fails_before_publication", idle and result.get("resultKind", -1) == 2 and not result.get("committed", true), result)
	check("failed_save_keeps_current_slot_and_original_files", not payload.is_empty() and not manifest.is_empty() and manager.CurrentSlotID == slot_before and map.GetRoadState() == road_before and map.GetHistoryState() == history_before and FileAccess.get_file_as_bytes(directory + "road_network_v4.json") == payload and FileAccess.get_file_as_bytes(directory + "manifest.json") == manifest)
	check("save_can_retry_after_external_lock_is_released", await SAVE.save(manager, manual_slot) and manager.CurrentSlotID == manual_slot and map.GetRoadState() == road_before and map.GetHistoryState() == history_before)

func malformed_payload_is_rejected(map: Node, manager: Node, slot: String) -> void:
	var directory: String = "user://saves-v4/" + slot + "/"
	var payload: PackedByteArray = FileAccess.get_file_as_bytes(directory + "road_network_v4.json")
	var manifest: PackedByteArray = FileAccess.get_file_as_bytes(directory + "manifest.json")
	var text: String = payload.get_string_from_utf8()
	var changed: String = text.replace('"profile":"street"', '"profile":"unknown-v4-profile"')
	var road_before: Dictionary = map.GetRoadState()
	var history_before: Dictionary = map.GetHistoryState()
	var selection_before: Dictionary = map.GetSelectionState()
	var slot_before: String = manager.CurrentSlotID
	check("invalid_profile_fixture_reaches_reader_integrity_gate", changed != text and publish_fixture(slot, changed))
	var result: Dictionary = await SAVE.wait_for_operation(manager, map.LoadSlot(slot), 5.0)
	var idle: bool = await SAVE.wait_for_idle(manager, 5.0)
	await process_frame
	check("unknown_profile_load_preserves_current_state", idle and result.get("resultKind", -1) == 2 and not result.get("committed", true) and str(result.get("error", "")).contains("known profile") and map.GetRoadState() == road_before and map.GetHistoryState() == history_before and map.GetSelectionState() == selection_before and manager.CurrentSlotID == slot_before and map.IsPresentationCurrent, result)
	check("invalid_profile_fixture_restored", write_bytes(directory + "road_network_v4.json", payload) and write_bytes(directory + "manifest.json", manifest))

func publish_fixture(slot: String, text: String) -> bool:
	# Same integrity-refresh fixture as v4_empty_map_runtime_contract: only this
	# test's new V4 slot is edited, so rejection exercises RoadCodec.Read.
	var directory: String = "user://saves-v4/" + slot + "/"
	if not write_bytes(directory + "road_network_v4.json", text.to_utf8_buffer()): return false
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(directory + "manifest.json"))
	manifest["schemaVersion"] = int(manifest["schemaVersion"])
	manifest["files"][0]["encodedLength"] = text.to_utf8_buffer().size()
	manifest["files"][0]["sha256"] = text.sha256_text()
	return write_bytes(directory + "manifest.json", JSON.stringify(manifest).to_utf8_buffer())

func preview_recovers_after_failed_load(map: Node, manager: Node, target_slot: String, preview_fails: bool) -> void:
	var label: String = "rejected" if preview_fails else "ready"
	check(label + "_preview_recovery_map_created", map.SetToolMode(0) and map.CreateMap(100))
	focus(map, Vector2(250, 0), 1.2)
	await process_frame
	var preview_probe = WORK_PROBE.new()
	if preview_fails: preview_probe.InstallPreviewFailure(map)
	else: preview_probe.InstallPreview(map)
	press(map, Vector2.ZERO)
	motion(map, Vector2(500, 0))
	var deadline: int = Time.get_ticks_msec() + 5000
	while not preview_probe.Started and Time.get_ticks_msec() < deadline:
		await process_frame
	var pending: Dictionary = map.GetBuildPreview()
	var road_before: Dictionary = map.GetRoadState()
	var history_before: Dictionary = map.GetHistoryState()
	var slot_before: String = manager.CurrentSlotID
	check(label + "_preview_pending_fixture", preview_probe.Started and pending.get("phase") == "Pending" and map.HasBuildPreview)
	var load_probe = LOAD_PROBE.new()
	load_probe.Install(manager, map, "fail-preflight")
	var operation: String = map.LoadSlot(target_slot)
	deadline = Time.get_ticks_msec() + 5000
	while not load_probe.Started and Time.get_ticks_msec() < deadline:
		await process_frame
	preview_probe.Release()
	deadline = Time.get_ticks_msec() + 5000
	while not preview_probe.Completed and Time.get_ticks_msec() < deadline:
		await process_frame
	var frozen: bool = load_probe.Started and preview_probe.Completed
	# The load worker stays gated while the released preview can complete and its
	# continuation can run. Both success and failure must leave the held draft intact.
	for _frame in range(12):
		await process_frame
		frozen = frozen and manager.IsOperationBusy and map.GetBuildPreview() == pending and map.GetRoadState() == road_before and map.GetHistoryState() == history_before and manager.CurrentSlotID == slot_before
	check(label + "_preview_completion_cannot_change_tools_during_load", frozen, {"before": pending, "after": map.GetBuildPreview()})
	load_probe.Release()
	var result: Dictionary = await SAVE.wait_for_operation(manager, operation, 5.0)
	var idle: bool = await SAVE.wait_for_idle(manager, 5.0)
	await process_frame
	check(label + "_load_failure_keeps_active_network_and_slot", idle and result.get("resultKind", -1) == 2 and not result.get("committed", true) and map.GetRoadState() == road_before and map.GetHistoryState() == history_before and manager.CurrentSlotID == slot_before and map.CellSizeMetres == 100 and map.PresentedCellSizeMetres == 100 and map.IsPresentationCurrent)
	if load_probe.Completed: load_probe.Cleanup()
	# No pointer event occurs after admission: the same held draft must resume itself.
	deadline = Time.get_ticks_msec() + 5000
	while map.GetBuildPreview().get("phase") == "Pending" and Time.get_ticks_msec() < deadline:
		await process_frame
	var resumed: Dictionary = map.GetBuildPreview()
	var segments: Array = resumed.get("segments", [])
	check(label + "_preview_resumes_without_pointer_motion", resumed.get("phase") == ("Rejected" if preview_fails else "Ready") and resumed.get("sourceToken") == history_before.get("sourceToken") and map.HasBuildPreview and segments.size() == 1 and segments[0].get("start") == Vector2.ZERO and segments[0].get("end") == Vector2(500, 0), resumed)
	preview_probe.RemovePreview(map)
	if preview_probe.Completed: preview_probe.Cleanup()
	escape()
	await process_frame
	release(map, Vector2(500, 0))
	await settle(map)

func write_bytes(path: String, bytes: PackedByteArray) -> bool:
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null: return false
	file.store_buffer(bytes)
	file.close()
	return true

func unchanged(map: Node, manager: Node, road: Dictionary, history: Dictionary, selection: Dictionary, slot: String) -> bool:
	return map.GetRoadState() == road and map.GetHistoryState() == history and map.GetSelectionState() == selection and manager.CurrentSlotID == slot and map.CellSizeMetres == 100 and map.PresentedCellSizeMetres == 100 and map.IsPresentationCurrent
