extends SceneTree

const SAVE := preload("res://tests/godot/v3_save_fixture.gd")
const WORK := preload("res://tests/godot/V4OperationWorkProbe.cs")

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	var map = load("res://Scenes/V4MapTest.tscn").instantiate()
	root.add_child(map)
	current_scene = map
	await process_frame
	if not map.has_method("GetBuildPreview"):
		push_error("FAIL V4 conflict preview entry is missing")
		map.queue_free()
		await process_frame
		quit(1)
		return
	var camera: Camera2D = map.get_node("Camera2D")
	camera.position = Vector2(-250, 0)
	camera.zoom = Vector2(0.7, 0.7)
	await process_frame
	var manager = root.get_node("SaveManager")
	press(map, Vector2.ZERO)
	motion(map, Vector2(400, 0))
	release(map, Vector2(400, 0))
	var passed := await wait_idle(map)
	passed = passed and map.RoadCount == 1
	var before: String = map.StateToken
	var saved := await SAVE.save_as(manager, "V4 overlap QA")
	var slot: String = manager.CurrentSlotID
	var payload_path := "user://saves-v4/" + slot + "/road_network_v4.json"
	var bytes := FileAccess.get_file_as_bytes(payload_path)
	var rows: Array = []
	for profile in range(4):
		map.get_node("HUD/Panel/Margin/Controls/Profile").select(profile)
		press(map, Vector2(-200, 0))
		motion(map, Vector2(600, 0))
		var ready := await wait_preview(map)
		var preview: Dictionary = map.GetBuildPreview()
		var segments: Array = preview.get("segments", [])
		var red: Array = segments.filter(func(segment): return segment.get("conflict", false))
		var exact: bool = ready and preview.get("phase") == "Rejected" and red.size() == 1 and red[0].get("start") == Vector2.ZERO and red[0].get("end") == Vector2(400, 0) and segments.size() == 3
		var reason: bool = map.get_node("HUD/Panel/Margin/Controls/Status").text.contains("重叠")
		var unchanged: bool = map.StateToken == before and map.PickRoad(Vector2(200, 0)).get("profile") == "street"
		if profile == 3:
			await RenderingServer.frame_post_draw
			root.get_texture().get_image().save_png(ProjectSettings.globalize_path("res://.scratch/v4-07-qa/overlap.png"))
		release(map, Vector2(600, 0))
		var idle := await wait_idle(map)
		unchanged = unchanged and idle and map.StateToken == before and map.RoadCount == 1 and map.IsPresentationCurrent
		rows.append({"profile":profile,"exact_red_span":exact,"reason":reason,"unchanged":unchanged})
		passed = passed and exact and reason and unchanged
	# Releasing before a preview result exists must still perform final validation.
	press(map, Vector2(400, 0))
	motion(map, Vector2.ZERO)
	release(map, Vector2.ZERO)
	passed = await wait_idle(map) and passed and map.StateToken == before
	passed = await SAVE.save(manager, slot) and passed and FileAccess.get_file_as_bytes(payload_path) == bytes
	# Touching the endpoint is a valid extension, rather than a red overlap.
	press(map, Vector2(400, 0))
	motion(map, Vector2(700, 0))
	var ready := await wait_preview(map)
	var contact: Dictionary = map.GetBuildPreview()
	var contact_ok: bool = ready and contact.get("phase") == "Ready" and contact.get("segments", []).all(func(segment): return not segment.get("conflict", false))
	release(map, Vector2(700, 0))
	contact_ok = await wait_idle(map) and contact_ok and map.StateToken != before
	passed = passed and contact_ok and saved
	passed = await SAVE.delete_slot(manager, slot) and passed
	var preview_lifecycle := await preview_late_results(map)
	passed = passed and preview_lifecycle
	print("V4_OVERLAP_RESULT ", JSON.stringify({"rows":rows,"endpoint_contact":contact_ok,"preview_lifecycle":preview_lifecycle,"passed":passed}))
	map.queue_free()
	await process_frame
	if passed:
		print("PASS V4 overlap runtime contract")
		quit(0)
	else:
		push_error("FAIL V4 overlap runtime contract")
		quit(1)

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

func motion(map: Node, point: Vector2) -> void:
	var event := InputEventMouseMotion.new()
	event.position = map.get_canvas_transform() * point
	event.button_mask = MOUSE_BUTTON_MASK_LEFT
	Input.parse_input_event(event)

func wait_idle(map: Node) -> bool:
	var deadline := Time.get_ticks_msec() + 5000
	while Time.get_ticks_msec() < deadline:
		await process_frame
		if not map.IsBuildBusy: return true
	return false

func wait_preview(map: Node) -> bool:
	var deadline := Time.get_ticks_msec() + 5000
	while Time.get_ticks_msec() < deadline:
		await process_frame
		if map.GetBuildPreview().get("phase") != "Pending": return true
	return false

func preview_late_results(map: Node) -> bool:
	map.CreateMap(100)
	var probe = WORK.new()
	probe.InstallPreview(map)
	press(map, Vector2.ZERO)
	motion(map, Vector2(300, 0))
	motion(map, Vector2(-300, 0))
	var deadline := Time.get_ticks_msec() + 5000
	while not probe.Started and Time.get_ticks_msec() < deadline: await process_frame
	var pending: Dictionary = map.GetBuildPreview()
	var neutral: bool = probe.Started and pending.get("phase") == "Pending" and pending.get("segments", []).all(func(segment): return not segment["conflict"] and segment["color"].is_equal_approx(Color("9aa5ad")))
	probe.Release()
	var ready := await wait_preview(map)
	var latest: Dictionary = map.GetBuildPreview()
	var coalesced: bool = ready and latest.get("phase") == "Ready" and latest["segments"][0]["end"] == Vector2(-300, 0) and map.RoadCount == 0
	probe.RemovePreview(map)
	if probe.Completed: probe.Cleanup()
	var escaped := InputEventKey.new()
	escaped.keycode = KEY_ESCAPE
	escaped.pressed = true
	Input.parse_input_event(escaped)
	await process_frame
	var stale_probe = WORK.new()
	stale_probe.InstallPreview(map)
	press(map, Vector2.ZERO)
	motion(map, Vector2(300, 0))
	deadline = Time.get_ticks_msec() + 5000
	while not stale_probe.Started and Time.get_ticks_msec() < deadline: await process_frame
	var created: bool = map.CreateMap(50)
	var token: String = map.StateToken
	stale_probe.Release()
	deadline = Time.get_ticks_msec() + 5000
	while not stale_probe.Completed and Time.get_ticks_msec() < deadline: await process_frame
	for frame in range(3): await process_frame
	var stale_rejected: bool = created and stale_probe.Completed and map.StateToken == token and map.CellSizeMetres == 50 and map.RoadCount == 0 and map.GetBuildPreview().get("phase") == "None" and map.GetBuildPreview()["segments"].is_empty()
	stale_probe.RemovePreview(map)
	if stale_probe.Completed: stale_probe.Cleanup()
	print("V4_PREVIEW_LIFECYCLE ", JSON.stringify({"pending_neutral":neutral,"latest_only":coalesced,"stale_rejected":stale_rejected}))
	return neutral and coalesced and stale_rejected
