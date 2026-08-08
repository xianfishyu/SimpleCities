extends SceneTree

const MAP_SCENE := "res://Scenes/MapTest.tscn"
const ISOLATED_CAMERA_SCENE := "res://tests/godot/camera_zoom_test_scene.tscn"
const LOW_FRAME_RATE := 30
const HIGH_FRAME_RATE := 120
const MAX_ONE_SECOND_ZOOM_DIFFERENCE := 0.01
const MAX_ONE_SECOND_PAN_DIFFERENCE := 0.01
const MAX_NORMALIZED_PAN_DIFFERENCE_ACROSS_ZOOM := 0.01

var failed := false


class WheelConsumer extends Control:
	func _gui_input(event: InputEvent) -> void:
		if event is InputEventMouseButton and event.button_index in [MOUSE_BUTTON_WHEEL_UP, MOUSE_BUTTON_WHEEL_DOWN]:
			accept_event()


func _initialize() -> void:
	run.call_deferred()


func run() -> void:
	var map: Node = await instantiate_scene(MAP_SCENE)
	if map != null:
		var autosave_controller: Node = map.get_node("AutosaveController")
		autosave_controller.set("AutosaveEnabled", false)
		var map_camera: Camera2D = map.get_node("Camera2D")
		verify_zoom_settings_are_editor_visible(map_camera, "MapTest")
		require(is_equal_approx(float(map_camera.get("minScale")), 0.125),
			"MapTest does not use MainCamera's default 0.125 minimum zoom")
		await verify_wheel_event_reaches_camera(map_camera, "MapTest")
		verify_zoom_is_frame_rate_independent(map_camera, "MapTest")
		await free_scene(map)
	await verify_keyboard_pan_is_frame_rate_independent()
	await verify_keyboard_pan_follows_zoom_influence()
	await verify_keyboard_pan_uses_rendered_zoom_during_transition()
	await verify_keyboard_pan_response_times_are_configurable()
	await verify_middle_drag_stops_keyboard_pan()
	await verify_zoom_bounds_apply_to_restore_and_wheel_input()
	await verify_sub_milliscale_survives_ready_normalization()
	await verify_consumed_ui_wheel_does_not_zoom_camera()
	await verify_wheel_zoom_preserves_mouse_anchor()

	var isolated_scene: Node = await instantiate_scene(ISOLATED_CAMERA_SCENE)
	if isolated_scene != null:
		var isolated_camera: Camera2D = isolated_scene.get_node("Camera2D")
		verify_zoom_settings_are_editor_visible(isolated_camera, "isolated scene")
		await verify_wheel_event_reaches_camera(isolated_camera, "isolated scene")
		verify_zoom_is_frame_rate_independent(isolated_camera, "isolated scene")
		await free_scene(isolated_scene)

	finish()


func instantiate_scene(scene_path: String) -> Node:
	var packed_scene: PackedScene = load(scene_path)
	if packed_scene == null:
		require(false, "%s did not load" % scene_path)
		return null

	var scene: Node = packed_scene.instantiate()
	root.add_child(scene)
	current_scene = scene
	await process_frame
	await process_frame
	return scene


func free_scene(scene: Node) -> void:
	scene.queue_free()
	await process_frame
	await process_frame


func verify_zoom_settings_are_editor_visible(camera: Camera2D, scene_name: String) -> void:
	var editor_property_names: Array[String] = []
	var editor_property_hints: Dictionary = {}
	for property: Dictionary in camera.get_property_list():
		if int(property.usage) & PROPERTY_USAGE_EDITOR:
			var property_name := str(property.name)
			editor_property_names.append(property_name)
			editor_property_hints[property_name] = str(property.hint_string)

	for property_name in [
		"smoothing",
		"referenceFps",
		"panSpeed",
		"zoomInfluence",
		"accelerationTime",
		"decelerationTime",
	]:
		require(editor_property_names.has(property_name),
			"%s does not expose %s in the Godot Inspector" % [scene_name, property_name])

	require(str(editor_property_hints.get("minScale", "")).begins_with("0.000001,"),
		"%s minScale Inspector range truncates sub-milliscale values" % scene_name)


func verify_wheel_event_reaches_camera(camera: Camera2D, scene_name: String) -> void:
	camera.zoom = Vector2.ONE
	camera.set("defaultScale", 1.0)

	Input.parse_input_event(wheel_event(MOUSE_BUTTON_WHEEL_UP))
	await process_frame
	await process_frame

	var target_zoom := float(camera.get("defaultScale"))
	require(is_equal_approx(target_zoom, 1.125), "%s wheel-up did not update MainCamera's zoom target" % scene_name)
	require(camera.zoom.x > 1.0 and camera.zoom.x < target_zoom,
		"%s wheel-up did not begin smooth zoom convergence within two frames" % scene_name)


func verify_zoom_is_frame_rate_independent(camera: Camera2D, scene_name: String) -> void:
	var low_rate_zoom := zoom_after_one_second(camera, LOW_FRAME_RATE)
	var high_rate_zoom := zoom_after_one_second(camera, HIGH_FRAME_RATE)
	var difference := absf(high_rate_zoom - low_rate_zoom)

	print("Camera zoom one-second convergence (%s): %d FPS=%.6f, %d FPS=%.6f, difference=%.6f" % [
		scene_name,
		LOW_FRAME_RATE,
		low_rate_zoom,
		HIGH_FRAME_RATE,
		high_rate_zoom,
		difference,
	])
	require(difference <= MAX_ONE_SECOND_ZOOM_DIFFERENCE,
		"%s zoom smoothing depends on frame rate; delta=%.6f exceeds %.6f" % [
			scene_name,
			difference,
			MAX_ONE_SECOND_ZOOM_DIFFERENCE,
		])


func verify_keyboard_pan_is_frame_rate_independent() -> void:
	var low_rate_position := await pan_after_one_second(LOW_FRAME_RATE)
	var high_rate_position := await pan_after_one_second(HIGH_FRAME_RATE)
	var difference := absf(high_rate_position - low_rate_position)

	print("Camera pan one-second convergence: %d FPS=%.6f, %d FPS=%.6f, difference=%.6f" % [
		LOW_FRAME_RATE,
		low_rate_position,
		HIGH_FRAME_RATE,
		high_rate_position,
		difference,
	])
	require(difference <= MAX_ONE_SECOND_PAN_DIFFERENCE,
		"keyboard pan depends on frame rate; delta=%.6f exceeds %.6f" % [
			difference,
			MAX_ONE_SECOND_PAN_DIFFERENCE,
		])


func verify_keyboard_pan_follows_zoom_influence() -> void:
	const INFLUENCE := 0.75
	var zoomed_out_screen_distance := await screen_pan_after_one_second(0.125, INFLUENCE)
	var zoomed_in_screen_distance := await screen_pan_after_one_second(4.0, INFLUENCE)
	var zoomed_out_normalized := zoomed_out_screen_distance / pow(0.125, 1.0 - INFLUENCE)
	var zoomed_in_normalized := zoomed_in_screen_distance / pow(4.0, 1.0 - INFLUENCE)
	var difference := absf(zoomed_out_normalized - zoomed_in_normalized)

	print("Camera pan zoom influence: zoom 0.125=%.6f, zoom 4.0=%.6f, normalized difference=%.6f" % [
		zoomed_out_screen_distance,
		zoomed_in_screen_distance,
		difference,
	])
	require(zoomed_in_screen_distance > zoomed_out_screen_distance,
		"zoomInfluence did not produce the expected hybrid pan-speed curve")
	require(difference <= MAX_NORMALIZED_PAN_DIFFERENCE_ACROSS_ZOOM,
		"keyboard pan does not follow zoomInfluence; delta=%.6f exceeds %.6f" % [
			difference,
			MAX_NORMALIZED_PAN_DIFFERENCE_ACROSS_ZOOM,
		])


func verify_keyboard_pan_uses_rendered_zoom_during_transition() -> void:
	var zooming_sample := await screen_pan_sample_after_one_frame(0.125, 4.0, 0.08)
	var steady_sample := await screen_pan_sample_after_one_frame(zooming_sample.y, zooming_sample.y, 0.08)
	var difference := absf(steady_sample.x - zooming_sample.x)

	print("Camera pan one-frame screen distance: steady=%.6f, zooming=%.6f, difference=%.6f" % [
		steady_sample.x,
		zooming_sample.x,
		difference,
	])
	require(difference <= MAX_NORMALIZED_PAN_DIFFERENCE_ACROSS_ZOOM,
		"keyboard pan screen speed changes while zoom is converging; delta=%.6f exceeds %.6f" % [
			difference,
			MAX_NORMALIZED_PAN_DIFFERENCE_ACROSS_ZOOM,
		])


func verify_keyboard_pan_response_times_are_configurable() -> void:
	var fast_response_distance := await screen_pan_sample_after_one_frame(1.0, 1.0, 0.03)
	var slow_response_distance := await screen_pan_sample_after_one_frame(1.0, 1.0, 0.3)

	print("Camera pan one-frame acceleration response: 0.03s=%.6f, 0.3s=%.6f" % [
		fast_response_distance.x,
		slow_response_distance.x,
	])
	require(fast_response_distance.x > slow_response_distance.x * 2.0,
		"accelerationTime does not materially change keyboard movement response")


func verify_middle_drag_stops_keyboard_pan() -> void:
	var scene: Node = await instantiate_scene(ISOLATED_CAMERA_SCENE)
	if scene == null:
		return

	var camera: Camera2D = scene.get_node("Camera2D")
	Input.action_press("KeyBoard_MoveRight")
	camera._UnhandledInput(InputEventKey.new())
	var middle_press := wheel_event(MOUSE_BUTTON_MIDDLE)
	camera._UnhandledInput(middle_press)
	var position_before_process := camera.position
	camera._Process(1.0 / 60.0)
	require(camera.position.is_equal_approx(position_before_process),
		"middle drag allowed stale keyboard input to move the camera")

	var middle_release := wheel_event(MOUSE_BUTTON_MIDDLE)
	middle_release.pressed = false
	Input.action_release("KeyBoard_MoveRight")
	camera._UnhandledInput(middle_release)
	camera._Process(1.0 / 60.0)
	require(camera.position.is_equal_approx(position_before_process),
		"middle drag left stale keyboard velocity after release")
	await free_scene(scene)


func verify_zoom_bounds_apply_to_restore_and_wheel_input() -> void:
	var scene: Node = await instantiate_scene(ISOLATED_CAMERA_SCENE)
	if scene == null:
		return

	var camera: Camera2D = scene.get_node("Camera2D")
	camera.set("minScale", 0.009)
	camera.set("maxScale", 2.0)
	camera.call("RestoreState", "{\"PositionX\":0,\"PositionY\":0,\"Zoom\":4}")
	require(is_equal_approx(float(camera.get("defaultScale")), 2.0),
		"restore allowed zoom above maxScale")
	camera.call("RestoreState", "{\"PositionX\":0,\"PositionY\":0,\"Zoom\":0.001}")
	require(is_equal_approx(float(camera.get("defaultScale")), 0.009),
		"restore allowed zoom below minScale")

	camera.set("defaultScale", 1.99)
	camera._UnhandledInput(wheel_event(MOUSE_BUTTON_WHEEL_UP))
	require(is_equal_approx(float(camera.get("defaultScale")), 2.0),
		"wheel input exceeded maxScale")
	await free_scene(scene)


func verify_sub_milliscale_survives_ready_normalization() -> void:
	var packed_scene: PackedScene = load(ISOLATED_CAMERA_SCENE)
	var scene: Node = packed_scene.instantiate()
	var camera: Camera2D = scene.get_node("Camera2D")
	camera.set("minScale", 0.0001)
	camera.set("defaultScale", 0.0001)
	root.add_child(scene)
	current_scene = scene
	await process_frame
	await process_frame

	print("Camera sub-milliscale normalization: minScale=%.6f, defaultScale=%.6f" % [
		float(camera.get("minScale")),
		float(camera.get("defaultScale")),
	])
	require(is_equal_approx(float(camera.get("minScale")), 0.0001),
		"camera normalization truncated minScale below 0.001")
	require(is_equal_approx(float(camera.get("defaultScale")), 0.0001),
		"camera normalization truncated defaultScale below 0.001")
	await free_scene(scene)


func verify_consumed_ui_wheel_does_not_zoom_camera() -> void:
	var scene: Node = await instantiate_scene(ISOLATED_CAMERA_SCENE)
	if scene == null:
		return

	var camera: Camera2D = scene.get_node("Camera2D")
	var consumer := WheelConsumer.new()
	consumer.position = Vector2.ZERO
	consumer.size = Vector2(1600.0, 900.0)
	consumer.mouse_filter = Control.MOUSE_FILTER_STOP
	scene.add_child(consumer)
	var target_before_input := float(camera.get("defaultScale"))
	Input.parse_input_event(wheel_event(MOUSE_BUTTON_WHEEL_UP))
	await process_frame
	require(is_equal_approx(float(camera.get("defaultScale")), target_before_input),
		"a UI-consumed wheel event still changed the camera zoom target")
	await free_scene(scene)


func verify_wheel_zoom_preserves_mouse_anchor() -> void:
	var scene: Node = await instantiate_scene(ISOLATED_CAMERA_SCENE)
	if scene == null:
		return

	var camera: Camera2D = scene.get_node("Camera2D")
	camera.position = Vector2(100.0, 200.0)
	camera.zoom = Vector2.ONE
	camera.set("defaultScale", 1.0)
	var viewport_rect := camera.get_viewport().get_visible_rect()
	var viewport_center := viewport_rect.position + viewport_rect.size * 0.5
	var anchor_position := viewport_rect.position + viewport_rect.size * Vector2(0.75, 0.35)
	var world_before := camera.position + (anchor_position - viewport_center) / camera.zoom
	var event := wheel_event(MOUSE_BUTTON_WHEEL_UP)
	event.position = anchor_position
	camera._UnhandledInput(event)
	camera._Process(1.0 / 60.0)
	var world_after := camera.position + (anchor_position - viewport_center) / camera.zoom

	require(world_before.distance_to(world_after) <= 0.001,
		"wheel zoom moved the world point under the mouse anchor")
	await free_scene(scene)


func pan_after_one_second(frame_rate: int) -> float:
	var scene: Node = await instantiate_scene(ISOLATED_CAMERA_SCENE)
	if scene == null:
		return 0.0

	var camera: Camera2D = scene.get_node("Camera2D")
	camera.position = Vector2.ZERO
	camera.set("defaultScale", 1.0)
	Input.action_press("KeyBoard_MoveRight")
	camera._UnhandledInput(InputEventKey.new())
	var delta := 1.0 / float(frame_rate)
	for frame_index in range(frame_rate):
		camera._Process(delta)
	Input.action_release("KeyBoard_MoveRight")
	camera._UnhandledInput(InputEventKey.new())
	var position_x := camera.position.x
	await free_scene(scene)
	return position_x


func screen_pan_after_one_second(zoom_scale: float, zoom_influence: float = 0.75) -> float:
	var scene: Node = await instantiate_scene(ISOLATED_CAMERA_SCENE)
	if scene == null:
		return 0.0

	var camera: Camera2D = scene.get_node("Camera2D")
	camera.position = Vector2.ZERO
	camera.zoom = Vector2.ONE * zoom_scale
	camera.set("defaultScale", zoom_scale)
	camera.set("zoomInfluence", zoom_influence)
	Input.action_press("KeyBoard_MoveRight")
	camera._UnhandledInput(InputEventKey.new())
	for frame_index in range(60):
		camera._Process(1.0 / 60.0)
	Input.action_release("KeyBoard_MoveRight")
	camera._UnhandledInput(InputEventKey.new())
	var screen_distance := camera.position.x * zoom_scale
	await free_scene(scene)
	return screen_distance


func screen_pan_sample_after_one_frame(
	start_zoom: float,
	target_zoom: float,
	acceleration_time: float,
) -> Vector2:
	var scene: Node = await instantiate_scene(ISOLATED_CAMERA_SCENE)
	if scene == null:
		return Vector2.ZERO

	var camera: Camera2D = scene.get_node("Camera2D")
	camera.position = Vector2.ZERO
	camera.zoom = Vector2.ONE * start_zoom
	camera.set("defaultScale", target_zoom)
	camera.set("accelerationTime", acceleration_time)
	Input.action_press("KeyBoard_MoveRight")
	camera._UnhandledInput(InputEventKey.new())
	camera._Process(1.0 / 60.0)
	Input.action_release("KeyBoard_MoveRight")
	camera._UnhandledInput(InputEventKey.new())
	var screen_distance := camera.position.x * camera.zoom.x
	var rendered_zoom := camera.zoom.x
	await free_scene(scene)
	return Vector2(screen_distance, rendered_zoom)


func zoom_after_one_second(camera: Camera2D, frame_rate: int) -> float:
	camera.zoom = Vector2.ONE
	camera.set("defaultScale", 2.0)
	var delta := 1.0 / float(frame_rate)
	for frame_index in range(frame_rate):
		camera._Process(delta)
	return camera.zoom.x


func wheel_event(button: MouseButton) -> InputEventMouseButton:
	var event := InputEventMouseButton.new()
	event.button_index = button
	event.pressed = true
	event.position = Vector2(800.0, 450.0)
	return event


func require(condition: bool, message: String) -> void:
	if condition:
		return
	failed = true
	push_error("FAIL camera zoom runtime contract: %s" % message)


func finish() -> void:
	if failed:
		quit(1)
		return
	print("PASS camera zoom runtime contract")
	quit(0)
