extends SceneTree

const SAVE := preload("res://tests/godot/v3_save_fixture.gd")

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	var map = load("res://Scenes/V4MapTest.tscn").instantiate()
	root.add_child(map)
	current_scene = map
	await process_frame
	await process_frame
	var camera: Camera2D = map.get_node("Camera2D")
	camera.position = Vector2(-400, 0)
	camera.zoom = Vector2(0.5, 0.5)
	await process_frame
	var manager = root.get_node("SaveManager")
	var passed := true
	var rows: Array = []
	for index in range(4):
		map.CreateMap(100)
		var profiles: OptionButton = map.get_node_or_null("HUD/Panel/Margin/Controls/Profile")
		if profiles == null:
			push_error("FAIL V4 road profile selector missing")
			map.queue_free()
			await process_frame
			quit(1)
			return
		profiles.select(index)
		var a := Vector2(-300, -200)
		var b := Vector2(300, 400)
		var transform: Transform2D = map.get_canvas_transform()
		mouse_button(transform * a, true)
		await process_frame
		mouse_motion(transform * b)
		await process_frame
		var preview: bool = map.HasBuildPreview and map.RoadCount == 0
		mouse_button(transform * b, false)
		var ready := await wait_build(map)
		await RenderingServer.frame_post_draw
		var hit: Dictionary = map.PickRoad(Vector2(0, 100))
		var built: bool = ready and map.RoadCount == 1 and not map.HasBuildPreview and map.IsPresentationCurrent
		var profile: String = ["dirt", "street", "arterial", "highway"][index]
		var hit_ok: bool = hit.get("profile", "") == profile and absf(hit.get("parameter", -1.0) - 0.5) < 0.00001
		var before: Dictionary = map.GetRoadState()
		hit_ok = hit_ok and before.get("meshSurfaces", 0) == 1 and hit.get("sourceToken") == before.get("sourceToken")
		var normal := Vector2(-1, 1).normalized()
		var half_width: float = [4.0, 6.0, 12.0, 16.0][index]
		hit_ok = hit_ok and not map.PickRoad(Vector2(0, 100) + normal * (half_width - 0.5)).is_empty() and map.PickRoad(Vector2(0, 100) + normal * (half_width + 0.5)).is_empty()
		var saved := await SAVE.save_as(manager, "V4 independent road QA")
		var slot: String = manager.CurrentSlotID
		map.CreateMap(200)
		var loaded := await SAVE.operation_succeeded(manager, map.LoadSlot(slot))
		await process_frame
		await RenderingServer.frame_post_draw
		var after: Dictionary = map.GetRoadState()
		var round_trip: bool = after.get("start") == before.get("start") and after.get("end") == before.get("end") and after.get("profile") == profile and map.CellSizeMetres == 100 and map.IsPresentationCurrent
		var reloaded_hit: Dictionary = map.PickRoad(Vector2(0, 100))
		round_trip = round_trip and reloaded_hit.get("profile", "") == profile and reloaded_hit.get("sourceToken") != hit.get("sourceToken")
		rows.append({"profile":profile,"preview":preview,"built":built,"hit":hit_ok,"saved":saved,"loaded":loaded,"round_trip":round_trip})
		passed = passed and preview and built and hit_ok and saved and loaded and round_trip
		passed = await SAVE.delete_slot(manager, slot) and passed
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(ProjectSettings.globalize_path("res://.scratch/v4-05-qa/independent-road.png"))
	var directions_ok := true
	for end in [Vector2(300, 0), Vector2(300, 300), Vector2(0, 300), Vector2(-300, 300), Vector2(-300, 0), Vector2(-300, -300), Vector2(0, -300), Vector2(300, -300)]:
		map.CreateMap(100)
		var transform: Transform2D = map.get_canvas_transform()
		mouse_button(transform * Vector2.ZERO, true)
		mouse_motion(transform * end)
		mouse_button(transform * end, false)
		var built := await wait_build(map)
		directions_ok = directions_ok and built and map.GetRoadState().get("end") == end
	passed = passed and directions_ok
	print("V4_EIGHT_DIRECTIONS ", directions_ok)
	# A zero-length press/release and Esc leave no multi-click draft behind.
	map.CreateMap(100)
	var center: Vector2 = map.get_canvas_transform() * Vector2.ZERO
	mouse_button(center, true)
	mouse_button(center, false)
	await wait_idle(map)
	passed = passed and map.RoadCount == 0 and not map.HasBuildPreview
	# Cancel after release but before the main-thread continuation can publish.
	var esc := InputEventKey.new()
	esc.keycode = KEY_ESCAPE
	esc.pressed = true
	mouse_button(center, true)
	mouse_motion(center + Vector2(150, 0))
	mouse_button(center + Vector2(150, 0), false)
	Input.parse_input_event(esc)
	await wait_idle(map)
	var pending_cancel: bool = map.RoadCount == 0 and not map.HasBuildPreview and not map.IsBuildBusy
	passed = passed and pending_cancel
	print("V4_PENDING_CANCEL ", pending_cancel)
	mouse_button(center, true)
	Input.parse_input_event(esc)
	mouse_button(center + Vector2(100, 0), false)
	await process_frame
	passed = passed and map.RoadCount == 0 and not map.HasBuildPreview
	print("V4_INDEPENDENT_ROAD_RESULT ", JSON.stringify({"rows":rows,"passed":passed}))
	map.queue_free()
	await process_frame
	if passed:
		print("PASS V4 independent road runtime contract")
		quit(0)
	else:
		push_error("FAIL V4 independent road runtime contract")
		quit(1)

func mouse_button(position: Vector2, pressed: bool) -> void:
	var event := InputEventMouseButton.new()
	event.position = position
	event.button_index = MOUSE_BUTTON_LEFT
	event.pressed = pressed
	Input.parse_input_event(event)

func mouse_motion(position: Vector2) -> void:
	var event := InputEventMouseMotion.new()
	event.position = position
	event.button_mask = MOUSE_BUTTON_MASK_LEFT
	Input.parse_input_event(event)

func wait_build(map: Node) -> bool:
	return await wait_idle(map) and map.RoadCount == 1

func wait_idle(map: Node) -> bool:
	var deadline := Time.get_ticks_msec() + 5000
	while Time.get_ticks_msec() < deadline:
		await process_frame
		if not map.IsBuildBusy:
			return true
	return false
