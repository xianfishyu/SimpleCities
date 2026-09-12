extends SceneTree

const SAVE := preload("res://tests/godot/v3_save_fixture.gd")

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	var map = load("res://Scenes/V4MapTest.tscn").instantiate()
	root.add_child(map)
	current_scene = map
	await process_frame
	var manager = root.get_node("SaveManager")
	var camera: Camera2D = map.get_node("Camera2D")
	camera.position = Vector2(-400, -150)
	camera.zoom = Vector2(0.55, 0.55)
	await process_frame
	var passed := await build(map, Vector2.ZERO, Vector2(400, 0))
	passed = await build(map, Vector2(400, 0), Vector2(400, 300)) and passed
	var turn: Dictionary = map.GetRoadState()
	var merged: bool = turn.get("nodeCount", 0) == 2 and turn.get("edges", []).size() == 1 and turn["edges"][0]["points"].size() == 3
	var horizontal: Dictionary = map.PickRoad(Vector2(200, 0))
	var vertical: Dictionary = map.PickRoad(Vector2(400, 150))
	var joined: bool = not map.PickRoad(Vector2(402, -2)).is_empty()
	var positions: bool = horizontal.get("profile", "") == "street" and vertical.get("profile", "") == "street" and absf(horizontal.get("parameter", -1.0) - 2.0 / 7.0) < 0.00001 and absf(vertical.get("parameter", -1.0) - 5.5 / 7.0) < 0.00001
	passed = passed and merged and joined and positions
	passed = await build(map, Vector2(-300, -300), Vector2.ZERO) and passed
	var reverse: Dictionary = map.GetRoadState()
	var reverse_ok: bool = reverse.get("nodeCount", 0) == 2 and reverse["edges"][0]["points"].size() == 4
	map.get_node("HUD/Panel/Margin/Controls/Profile").select(3)
	passed = await build(map, Vector2(-300, -300), Vector2(-300, -600)) and passed
	var before: Dictionary = map.GetRoadState()
	var type_boundary: bool = before.get("nodeCount", 0) == 3 and before["edges"].size() == 2 and map.PickRoad(Vector2(-300, -450)).get("profile", "") == "highway"
	passed = passed and reverse_ok and type_boundary
	var saved := await SAVE.save_as(manager, "V4 turning road QA")
	var slot: String = manager.CurrentSlotID
	map.CreateMap(25)
	var loaded := await SAVE.operation_succeeded(manager, map.LoadSlot(slot))
	await process_frame
	await RenderingServer.frame_post_draw
	var after: Dictionary = map.GetRoadState()
	var round_trip: bool = loaded and map.CellSizeMetres == 100 and after.get("edges") == before.get("edges") and after.get("sourceToken") != before.get("sourceToken") and map.IsPresentationCurrent
	passed = passed and saved and round_trip
	root.get_texture().get_image().save_png(ProjectSettings.globalize_path("res://.scratch/v4-06-16-qa/turning-road.png"))
	passed = await SAVE.delete_slot(manager, slot) and passed
	# Clamping a diagonal drag keeps its direction and stops at the boundary.
	map.CreateMap(25)
	camera.position = Vector2(3500, 0)
	camera.zoom = Vector2(0.6, 0.6)
	await process_frame
	var boundary_built := await build(map, Vector2(3950, 0), Vector2(4500, 550))
	var state: Dictionary = map.GetRoadState()
	var boundary: bool = boundary_built and state.get("end") == Vector2(4000, 50)
	var cap: bool = not map.PickRoad(Vector2(4001, 51)).is_empty()
	passed = passed and boundary and cap
	print("V4_ENDPOINT_CONTINUATION_RESULT ", JSON.stringify({"merged":merged,"turn_join":joined,"positions":positions,"reverse":reverse_ok,"type_boundary":type_boundary,"round_trip":round_trip,"boundary":boundary,"outside_cap":cap,"passed":passed}))
	map.queue_free()
	await process_frame
	if passed:
		print("PASS V4 endpoint continuation runtime contract")
		quit(0)
	else:
		push_error("FAIL V4 endpoint continuation runtime contract")
		quit(1)

func build(map: Node, start: Vector2, end: Vector2) -> bool:
	var before: String = map.StateToken
	var transform: Transform2D = map.get_canvas_transform()
	button(transform * start, true)
	var motion := InputEventMouseMotion.new()
	motion.position = transform * end
	motion.button_mask = MOUSE_BUTTON_MASK_LEFT
	Input.parse_input_event(motion)
	button(transform * end, false)
	var deadline := Time.get_ticks_msec() + 5000
	while Time.get_ticks_msec() < deadline:
		await process_frame
		if not map.IsBuildBusy:
			return map.StateToken != before and map.IsPresentationCurrent
	return false

func button(position: Vector2, pressed: bool) -> void:
	var event := InputEventMouseButton.new()
	event.position = position
	event.button_index = MOUSE_BUTTON_LEFT
	event.pressed = pressed
	Input.parse_input_event(event)
