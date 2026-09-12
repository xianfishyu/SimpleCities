extends SceneTree

const SAVE := preload("res://tests/godot/v3_save_fixture.gd")

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	var scene_resource = load("res://Scenes/V4MapTest.tscn")
	if scene_resource == null:
		push_error("FAIL V4 isolated map scene is missing")
		quit(1)
		return
	var map = scene_resource.instantiate()
	root.add_child(map)
	current_scene = map
	await process_frame
	await process_frame
	var manager = root.get_node("SaveManager")
	var passed: bool = map.CellSizeMetres == 100 and map.PresentedCellSizeMetres == 100
	var rows: Array = []
	for cell in [25, 50, 100, 200]:
		var selected: OptionButton = map.get_node("HUD/Panel/Margin/Controls/CellSize")
		selected.select([25, 50, 100, 200].find(cell))
		map.get_node("HUD/Panel/Margin/Controls/Create").emit_signal("pressed")
		passed = passed and map.CellSizeMetres == cell
		map.get_node("HUD/Panel/Margin/Controls/Save").emit_signal("pressed")
		var operation: String = map.LastOperationToken
		var saved := await SAVE.operation_succeeded(manager, operation)
		await process_frame
		var slot: String = manager.CurrentSlotID
		var different := 200 if cell != 200 else 25
		selected.select([25, 50, 100, 200].find(different))
		map.get_node("HUD/Panel/Margin/Controls/Create").emit_signal("pressed")
		passed = passed and map.CellSizeMetres == different
		map.get_node("HUD/Panel/Margin/Controls/Load").emit_signal("pressed")
		var loaded := await SAVE.operation_succeeded(manager, map.LastOperationToken)
		await process_frame
		await RenderingServer.frame_post_draw
		var correct: bool = map.CellSizeMetres == cell and map.PresentedCellSizeMetres == cell and map.IsPresentationCurrent
		rows.append({"cell": cell, "saved": saved, "loaded": loaded, "correct": correct})
		passed = passed and saved and loaded and correct
		if cell == 50:
			passed = await rejects_invalid_payload(map, manager, slot) and passed
		passed = await SAVE.delete_slot(manager, slot) and passed
	var camera: Camera2D = map.get_node("Camera2D")
	var original_zoom := camera.zoom
	var wheel := InputEventMouseButton.new()
	wheel.position = Vector2(950, 450)
	wheel.button_index = MOUSE_BUTTON_WHEEL_UP
	wheel.pressed = true
	Input.parse_input_event(wheel)
	await process_frame
	var zoom_works: bool = camera.zoom.x > original_zoom.x and map.CellSizeMetres == 200
	var original_position := camera.position
	var drag := InputEventMouseMotion.new()
	drag.position = Vector2(950, 450)
	drag.relative = Vector2(25, 0)
	drag.button_mask = MOUSE_BUTTON_MASK_MIDDLE
	Input.parse_input_event(drag)
	await process_frame
	var pan_works := camera.position.x < original_position.x
	passed = passed and zoom_works and pan_works
	camera.zoom = original_zoom
	camera.position = original_position
	print("V4_CAMERA_RESULT ", JSON.stringify({"zoom": zoom_works, "pan": pan_works}))
	print("V4_EMPTY_MAP_RESULT ", JSON.stringify({"rows": rows, "passed": passed}))
	await RenderingServer.frame_post_draw
	var capture_path := "res://.scratch/v4-04-qa/empty-map.png"
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture="):
			capture_path = argument.trim_prefix("--capture=")
	var capture := ProjectSettings.globalize_path(capture_path)
	root.get_texture().get_image().save_png(capture)
	map.queue_free()
	await process_frame
	if passed:
		print("PASS V4 empty map runtime contract")
		quit(0)
	else:
		push_error("FAIL V4 empty map runtime contract")
		quit(1)

func rejects_invalid_payload(map: Node, manager: Node, slot: String) -> bool:
	var payload_path := "user://saves-v4/" + slot + "/road_network_v4.json"
	var original := FileAccess.get_file_as_string(payload_path)
	var passed := true
	for replacement in [["\"schemaVersion\":6", "\"schemaVersion\":1"], ["\"cellSizeMetres\":50", "\"cellSizeMetres\":75"]]:
		var before: String = map.StateToken
		var changed := original.replace(replacement[0], replacement[1])
		passed = publish_fixture(slot, changed) and passed
		var result := await SAVE.wait_for_operation(manager, map.LoadSlot(slot))
		await SAVE.wait_for_idle(manager)
		await process_frame
		var rejected: bool = not result.get("committed", true) and result.get("resultKind", -1) == 2
		var intact: bool = map.StateToken == before and map.CellSizeMetres == 50 and map.IsPresentationCurrent
		print("V4_INVALID_LOAD_RESULT ", JSON.stringify({"field": replacement[0], "rejected": rejected, "intact": intact, "error": result.get("error", "")}))
		passed = passed and rejected and intact
	passed = publish_fixture(slot, original) and passed
	return passed

func publish_fixture(slot: String, text: String) -> bool:
	# Only the fresh slot created in this test is edited. Refresh integrity metadata
	# so malformed content reaches the actual V4 reader instead of failing the hash gate.
	var directory := "user://saves-v4/" + slot + "/"
	var file := FileAccess.open(directory + "road_network_v4.json", FileAccess.WRITE)
	if file == null:
		return false
	file.store_string(text)
	file.close()
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(directory + "manifest.json"))
	manifest["schemaVersion"] = int(manifest["schemaVersion"])
	manifest["files"][0]["encodedLength"] = text.to_utf8_buffer().size()
	manifest["files"][0]["sha256"] = text.sha256_text()
	var output := FileAccess.open(directory + "manifest.json", FileAccess.WRITE)
	if output == null:
		return false
	output.store_string(JSON.stringify(manifest))
	output.close()
	return true
