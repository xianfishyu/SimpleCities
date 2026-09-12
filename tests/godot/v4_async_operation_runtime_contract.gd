extends SceneTree

const WORK_PROBE = preload("res://tests/godot/V4OperationWorkProbe.cs")

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	var map = load("res://Scenes/V4MapTest.tscn").instantiate()
	root.add_child(map)
	current_scene = map
	await process_frame
	var manager = root.get_node("SaveManager")
	var camera: Camera2D = map.get_node("Camera2D")
	camera.position = Vector2(-400, 0)
	camera.zoom = Vector2(0.5, 0.5)
	await process_frame
	var probe = WORK_PROBE.new()
	probe.Install(map)
	var original: String = map.StateToken
	build(map, Vector2.ZERO, Vector2(300, 0))
	var deadline := Time.get_ticks_msec() + 5000
	var frames := 0
	while Time.get_ticks_msec() < deadline and (not probe.Started or map.BuildElapsedMilliseconds <= 350):
		await process_frame
		frames += 1
	var status: Label = map.get_node("HUD/Panel/Margin/Controls/Status")
	var waiting: bool = map.IsBuildBusy and map.BuildPhase == "Preparing" and frames >= 3 and probe.UsedBackgroundThread and status.text.contains("等待") and status.text.contains("Esc")
	var camera_before := camera.position
	var pan := InputEventMouseMotion.new()
	pan.position = map.get_canvas_transform() * Vector2.ZERO
	pan.relative = Vector2(20, 0)
	pan.button_mask = MOUSE_BUTTON_MASK_MIDDLE
	Input.parse_input_event(pan)
	await process_frame
	var camera_responsive: bool = camera.position != camera_before
	var single_writer: bool = not map.CreateMap(25) and map.LoadSlot("not-queued") == ""
	build(map, Vector2(0, 100), Vector2(300, 100))
	await process_frame
	single_writer = single_writer and not map.HasBuildPreview and map.StateToken == original
	escape()
	# parse_input_event is delivered through Godot's input dispatch. Observe after that
	# dispatch, while the held worker guarantees cancellation cleanup cannot finish yet.
	await process_frame
	var cancelling: bool = map.BuildPhase == "Cancelling" and map.IsBuildBusy and map.StateToken == original and status.text.contains("取消")
	await process_frame
	cancelling = cancelling and map.IsBuildBusy and not map.CreateMap(50) and map.LoadSlot("still-not-queued") == ""
	probe.Release()
	var cleaned := await wait_idle(map)
	var late_rejected: bool = cleaned and map.BuildPhase == "Cancelled" and map.StateToken == original and map.RoadCount == 0 and map.BuildDrawnElapsedMilliseconds < 0
	probe.Remove(map)
	# Cancellation is accepted immediately after release, before the 300 ms target.
	var immediate_probe = WORK_PROBE.new()
	immediate_probe.Install(map)
	build(map, Vector2.ZERO, Vector2(300, 0))
	escape()
	await process_frame
	var immediate_cancel_elapsed: float = map.BuildElapsedMilliseconds
	var immediate_cancel: bool = map.BuildPhase == "Cancelling" and map.IsBuildBusy and map.StateToken == original and immediate_cancel_elapsed < 300
	immediate_probe.Release()
	immediate_cancel = await wait_idle(map) and immediate_cancel and map.RoadCount == 0
	immediate_probe.Remove(map)
	# Hold a successful command beyond the target: it must keep working and measure its real draw.
	var successful_probe = WORK_PROBE.new()
	successful_probe.Install(map)
	build(map, Vector2.ZERO, Vector2(300, 0))
	deadline = Time.get_ticks_msec() + 5000
	while Time.get_ticks_msec() < deadline and (not successful_probe.Started or map.BuildElapsedMilliseconds <= 350):
		await process_frame
	var before_draw: bool = map.BuildDrawnElapsedMilliseconds < 0 and map.StateToken == original
	# Keep the CanvasItem hidden while its references publish. Global post_draw signals
	# must not be mistaken for the target having reached this view's actual _Draw.
	map.visible = false
	successful_probe.Release()
	deadline = Time.get_ticks_msec() + 5000
	while map.BuildPhase == "Preparing" and Time.get_ticks_msec() < deadline:
		await process_frame
	await RenderingServer.frame_post_draw
	await RenderingServer.frame_post_draw
	var published_not_drawn: bool = map.BuildPhase == "Committed" and map.IsBuildBusy and map.IsPresentationCurrent and map.StateToken != original and map.BuildDrawnElapsedMilliseconds < 0
	escape()
	await process_frame
	published_not_drawn = published_not_drawn and map.BuildPhase == "Committed" and map.StateToken == map.BuildPresentedToken
	map.visible = true
	var completed := await wait_idle(map)
	var drawn: bool = completed and before_draw and map.RoadCount == 1 and map.BuildPhase == "Drawn" and map.BuildDrawnElapsedMilliseconds > 300 and map.BuildSourceToken == original and map.StateToken != original and map.BuildPresentedToken == map.StateToken and map.IsPresentationCurrent
	var committed_token: String = map.StateToken
	var elapsed: float = map.BuildDrawnElapsedMilliseconds
	escape()
	await process_frame
	var committed_retained: bool = map.StateToken == committed_token and map.RoadCount == 1 and map.BuildPhase == "Drawn" and not status.text.contains("Esc")
	successful_probe.Remove(map)
	# Exit while the controlled worker is still running; its late continuation cannot publish.
	map.CreateMap(100)
	var exit_probe = WORK_PROBE.new()
	exit_probe.Install(map)
	build(map, Vector2.ZERO, Vector2(300, 0))
	deadline = Time.get_ticks_msec() + 5000
	while not exit_probe.Started and Time.get_ticks_msec() < deadline:
		await process_frame
	var exit_started: bool = exit_probe.Started
	map.queue_free()
	await process_frame
	var replacement = load("res://Scenes/V4MapTest.tscn").instantiate()
	root.add_child(replacement)
	current_scene = replacement
	var replacement_token: String = replacement.StateToken
	exit_probe.Release()
	deadline = Time.get_ticks_msec() + 5000
	while not exit_probe.Completed and Time.get_ticks_msec() < deadline:
		await process_frame
	await process_frame
	await process_frame
	var exit_safe: bool = exit_started and exit_probe.Completed and not manager.IsOperationBusy and replacement.StateToken == replacement_token and replacement.RoadCount == 0 and replacement.IsPresentationCurrent
	for work_probe in [probe, immediate_probe, successful_probe, exit_probe]:
		if work_probe.Completed:
			work_probe.Cleanup()
	replacement.queue_free()
	await process_frame
	var passed: bool = waiting and camera_responsive and single_writer and cancelling and late_rejected and immediate_cancel and published_not_drawn and drawn and committed_retained and exit_safe
	print("V4_ASYNC_OPERATION_RESULT ", JSON.stringify({"waiting":waiting,"camera_responsive":camera_responsive,"single_writer":single_writer,"cancelling":cancelling,"late_rejected":late_rejected,"immediate_cancel":immediate_cancel,"immediate_cancel_elapsed_ms":immediate_cancel_elapsed,"published_not_drawn":published_not_drawn,"drawn":drawn,"drawn_elapsed_ms":elapsed,"committed_retained":committed_retained,"exit_safe":exit_safe,"passed":passed}))
	if passed:
		print("PASS V4 async operation runtime contract")
		quit(0)
	else:
		push_error("FAIL V4 async operation runtime contract")
		quit(1)

func build(map: Node, start: Vector2, end: Vector2) -> void:
	var transform: Transform2D = map.get_canvas_transform()
	button(transform * start, true)
	var motion := InputEventMouseMotion.new()
	motion.position = transform * end
	motion.button_mask = MOUSE_BUTTON_MASK_LEFT
	Input.parse_input_event(motion)
	button(transform * end, false)

func button(position: Vector2, pressed: bool) -> void:
	var event := InputEventMouseButton.new()
	event.position = position
	event.button_index = MOUSE_BUTTON_LEFT
	event.pressed = pressed
	Input.parse_input_event(event)

func escape() -> void:
	var event := InputEventKey.new()
	event.keycode = KEY_ESCAPE
	event.pressed = true
	Input.parse_input_event(event)

func wait_idle(map: Node) -> bool:
	var deadline := Time.get_ticks_msec() + 5000
	while Time.get_ticks_msec() < deadline:
		await process_frame
		if not map.IsBuildBusy:
			return true
	return false
