extends SceneTree

const MAP_SCENE := "res://Scenes/MapTest.tscn"

var failed := false


func _initialize() -> void:
	run.call_deferred()


func run() -> void:
	root.size = Vector2i(1600, 900)
	await process_frame
	var packed_scene: PackedScene = load(MAP_SCENE)
	require(packed_scene != null, "MapTest scene did not load")
	if packed_scene == null:
		finish()
		return

	var map: Node = packed_scene.instantiate()
	map.get_node("AutosaveController").set("AutosaveEnabled", false)
	root.add_child(map)
	current_scene = map
	for frame in range(6):
		await process_frame

	var camera: Camera2D = map.get_node("Camera2D")
	var background: ColorRect = map.get_node("MapBackground/ColorRect")
	var pause_menu: Control = map.get_node("GameHUD/PauseMenu")
	var debug_button: Button = map.get_node("GameHUD/DebugPanel/PanelMargin/Rows/DebugToggleButton")
	require(not pause_menu.is_visible_in_tree(),
		"PauseMenu must be hidden during the drag scenario")
	require(background.mouse_filter == Control.MOUSE_FILTER_IGNORE,
		"the visual-only map background must ignore mouse input")

	var map_drag_distance := await drag_camera(
		camera,
		Vector2(800.0, 450.0),
		Vector2(720.0, 400.0))
	var ui_start := debug_button.get_global_rect().get_center()
	var ui_drag_distance := await drag_camera(
		camera,
		ui_start,
		ui_start + Vector2(20.0, 0.0))

	print("Camera middle drag routing: map distance=%.3f, UI distance=%.3f, background filter=%s" % [
		map_drag_distance,
		ui_drag_distance,
		background.mouse_filter,
	])
	require(map_drag_distance > 1.0,
		"middle drag did not move the camera from the map area")
	require(ui_drag_distance <= 0.001,
		"middle drag moved the camera after starting over a UI button")

	map.queue_free()
	await process_frame
	await process_frame
	finish()


func drag_camera(camera: Camera2D, start: Vector2, finish_position: Vector2) -> float:
	Input.parse_input_event(mouse_motion_event(start, 0))
	await process_frame
	camera.position = Vector2(100.0, 100.0)
	var position_before_drag := camera.position
	Input.parse_input_event(mouse_button_event(true, start))
	await process_frame
	Input.parse_input_event(mouse_motion_event(finish_position, MOUSE_BUTTON_MASK_MIDDLE))
	await process_frame
	Input.parse_input_event(mouse_button_event(false, finish_position))
	await process_frame
	return camera.position.distance_to(position_before_drag)


func mouse_button_event(pressed: bool, position: Vector2) -> InputEventMouseButton:
	var event := InputEventMouseButton.new()
	event.button_index = MOUSE_BUTTON_MIDDLE
	event.pressed = pressed
	event.button_mask = MOUSE_BUTTON_MASK_MIDDLE if pressed else 0
	event.position = position
	event.global_position = position
	return event


func mouse_motion_event(position: Vector2, button_mask: int) -> InputEventMouseMotion:
	var event := InputEventMouseMotion.new()
	event.position = position
	event.global_position = position
	event.button_mask = button_mask
	return event


func require(condition: bool, message: String) -> void:
	if condition:
		return
	failed = true
	push_error("FAIL camera middle drag input contract: %s" % message)


func finish() -> void:
	if failed:
		quit(1)
		return
	print("PASS camera middle drag input contract")
	quit(0)
