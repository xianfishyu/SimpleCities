extends "res://tests/godot/v4_history_runtime_contract.gd"

# Wall time between actual rendered frames; all output is deferred until sampling ends.
var _perf_frames: Array[float] = []
var _perf_frame_ends_usec: Array[int] = []
var _perf_recording: bool = false
var _perf_last_draw: int = 0
var _perf_focused_draws: int = 0
var _perf_unfocused_draws: int = 0
var _perf_minimized_draws: int = 0

func run() -> void:
	Engine.max_fps = 0
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	RenderingServer.frame_post_draw.connect(_perf_draw)
	var requested: String = OS.get_environment("V4_PERF_CELL")
	var cells: Array = [25, 50, 100, 200] if requested.is_empty() else [int(requested)]
	for cell in cells:
		if not [25, 50, 100, 200].has(cell):
			push_error("Invalid V4_PERF_CELL")
			quit(1)
			return
		var result: Dictionary = await measure_cell(cell)
		_perf_recording = false
		var output: String = OS.get_environment("V4_QA_OUTPUT")
		if output.is_empty(): output = "res://.scratch/v4-20-20260921"
		var directory: String = ProjectSettings.globalize_path(output)
		DirAccess.make_dir_recursive_absolute(directory)
		var file := FileAccess.open(directory.path_join("render-%d.json" % cell), FileAccess.WRITE)
		if file == null:
			push_error("Cannot write performance evidence: " + str(FileAccess.get_open_error()))
			quit(1)
			return
		file.store_string(JSON.stringify(result, "\t"))
		file.close()
		print("V4_PERFORMANCE_RESULT ", JSON.stringify({"cell": cell, "passed": result.get("passed", false), "idle": result.get("idle", {}).get("summary", {}), "panHover": result.get("panHover", {}).get("summary", {}), "buildUndo": result.get("buildUndoFrames", {}).get("summary", {}), "upgrade": result.get("upgradeFrames", {}).get("summary", {}), "error": result.get("error", "")}))
		if not result.get("passed", false):
			push_error("Performance contract failed: " + str(result.get("error", "unknown")))
			quit(1)
			return
	quit(0)

func _perf_draw() -> void:
	var now: int = Time.get_ticks_usec()
	if _perf_recording and _perf_last_draw > 0:
		_perf_frames.append(float(now - _perf_last_draw) / 1000.0)
		_perf_frame_ends_usec.append(now)
		if DisplayServer.window_is_focused(): _perf_focused_draws += 1
		else: _perf_unfocused_draws += 1
		if DisplayServer.window_get_mode() == DisplayServer.WINDOW_MODE_MINIMIZED: _perf_minimized_draws += 1
	_perf_last_draw = now

func begin_frames() -> int:
	_perf_frames = []
	_perf_frame_ends_usec = []
	_perf_last_draw = 0
	_perf_focused_draws = 0
	_perf_unfocused_draws = 0
	_perf_minimized_draws = 0
	_perf_recording = true
	return Time.get_ticks_usec()

func end_frames(started: int) -> Dictionary:
	_perf_recording = false
	var duration: float = float(Time.get_ticks_usec() - started) / 1000000.0
	var values: Array[float] = _perf_frames.duplicate()
	var ordered: Array[float] = values.duplicate()
	ordered.sort()
	var over_budget: int = 0
	for value in values:
		if value > 1000.0 / 144.0: over_budget += 1
	var visibility: String = "foreground" if _perf_unfocused_draws == 0 and _perf_minimized_draws == 0 else "mixed_or_background"
	var elapsed_ms: float = 0.0
	var seconds: Array = []
	for value in values:
		var bucket: int = int(elapsed_ms / 1000.0)
		while seconds.size() <= bucket: seconds.append([])
		seconds[bucket].append(value)
		elapsed_ms += value
	var per_second: Array = []
	for index in range(seconds.size()):
		var samples: Array = seconds[index]
		if samples.is_empty():
			per_second.append({"second": index, "count": 0, "p95Ms": null, "maxMs": null})
			continue
		samples.sort()
		per_second.append({"second": index, "count": samples.size(), "p95Ms": samples[maxi(0, int(ceil(samples.size() * 0.95)) - 1)], "maxMs": samples.back()})
	return {"durationSeconds": duration, "observedFrameSeconds": elapsed_ms / 1000.0, "frameEndsUsec": _perf_frame_ends_usec.duplicate(), "intervalsMs": values, "perSecond": per_second, "focusedDraws": _perf_focused_draws, "unfocusedDraws": _perf_unfocused_draws, "minimizedDraws": _perf_minimized_draws, "visibility": visibility, "summary": {"count": values.size(), "p95Ms": percentile(ordered, 0.95), "p99Ms": percentile(ordered, 0.99), "maxMs": ordered.back() if not ordered.is_empty() else 0, "over144BudgetCount": over_budget, "over144BudgetRatio": float(over_budget) / max(1, values.size())}}

func percentile(ordered: Array[float], fraction: float) -> float:
	return ordered[max(0, int(ceil(ordered.size() * fraction)) - 1)] if not ordered.is_empty() else 0.0

func measure_cell(cell: int) -> Dictionary:
	var result: Dictionary = {"cell": cell, "passed": false, "renderingMethod": RenderingServer.get_current_rendering_method(), "renderingDriver": RenderingServer.get_current_rendering_driver_name(), "engine": Engine.get_version_info(), "vsyncMode": DisplayServer.window_get_vsync_mode(), "maxFps": Engine.max_fps, "viewport": {"width": root.get_visible_rect().size.x, "height": root.get_visible_rect().size.y}, "builds": [], "undos": [], "redos": [], "upgrades": [], "deletes": [], "cancellations": []}
	var smoke: bool = OS.get_environment("V4_PERF_SMOKE") == "1"
	result.capturedUtc = Time.get_datetime_string_from_system(true)
	result.debugBuild = OS.is_debug_build()
	result.physicsTicksPerSecond = Engine.physics_ticks_per_second
	result.lowProcessorUsageMode = OS.low_processor_usage_mode
	result.windowSize = {"width": DisplayServer.window_get_size().x, "height": DisplayServer.window_get_size().y}
	result.windowFocusedAtStart = DisplayServer.window_is_focused()
	result.windowModeAtStart = DisplayServer.window_get_mode()
	result.focusSampling = "DisplayServer.window_is_focused and window_get_mode at each frame_post_draw"
	result.frameBoundary = "wall-clock interval between actual frame_post_draw callbacks; includes CPU, GPU, scheduling and instrumentation"
	result.frameClock = "frameEndsUsec uses Time.get_ticks_usec; C# phase durations use Stopwatch, with no common absolute epoch or frame-to-phase attribution"
	result.scope = "240-edge current-admission Debug baseline; not a 10K-edge or stable-144-FPS acceptance"
	result.passMeaning = "fixture, input and measurement integrity only; latency and frame targets are reported independently"
	var window_seconds: float = 0.25 if smoke else 10.0
	var repetitions: int = 2 if smoke else 30
	result.smoke = smoke
	result.minimumWindowSeconds = window_seconds
	result.warmupSeconds = 0.1 if smoke else 5.0
	result.warmupScope = "render/idle warmup; first operation includes any remaining cold planner/JIT cost"
	result.inputCadence = "wall-clock pacing at most 60 Hz, with no catch-up bursts; pan/hover pair targets 120 motion events per second"
	result.historyTrigger = "UndoRoadEdit/RedoRoadEdit API invocation to frame_post_draw"
	result.buildAndUpgradeTrigger = "mouse release handling timestamp to first matching frame_post_draw; OS event delivery before handler is excluded"
	if root.get_visible_rect().size != Vector2(1600, 900) or result.renderingMethod != "forward_plus" or result.renderingDriver != "vulkan":
		result.error = "Requires actual 1600x900 Forward+/Vulkan viewport"
		return result
	var map = load("res://Scenes/V4MapTest.tscn").instantiate()
	root.add_child(map)
	current_scene = map
	await process_frame
	map.PreparePerformanceFixture(cell)
	var deadline: int = Time.get_ticks_msec() + 30000
	while map.PerformanceFixtureBusy and Time.get_ticks_msec() < deadline:
		await process_frame
	if map.PerformanceFixtureBusy or not map.PerformanceFixtureError.is_empty():
		result.error = "Fixture failed or timed out: " + map.PerformanceFixtureError
		return result
	result.fixture = map.GetPerformanceFixture()
	if result.fixture.get("edges", 0) != 240:
		result.error = "Fixture must contain 240 edges"
		return result
	var zoom: float = minf(2.0, 600.0 / (8.0 * cell))
	focus(map, Vector2.ZERO, zoom)
	var camera: Camera2D = map.get_node("Camera2D")
	result.samplingCamera = {"x": camera.position.x, "y": camera.position.y, "zoom": zoom}
	map.SetToolMode(3)
	await create_timer(0.1 if smoke else 5.0).timeout
	var started: int = begin_frames()
	await create_timer(window_seconds).timeout
	result.idle = end_frames(started)
	result.idle.camera = result.samplingCamera
	started = begin_frames()
	var previous_offset := Vector2.ZERO
	var middle := InputEventMouseButton.new()
	middle.position = Vector2(1000, 450)
	middle.button_index = MOUSE_BUTTON_MIDDLE
	middle.pressed = true
	Input.parse_input_event(middle)
	var next_pan_at: int = started
	var pan_ticks: int = 0
	while Time.get_ticks_usec() - started < window_seconds * 1000000:
		if Time.get_ticks_usec() < next_pan_at:
			await process_frame
			continue
		var seconds: float = float(Time.get_ticks_usec() - started) / 1000000.0
		var offset := Vector2(sin(seconds * 1.7) * 30.0, sin(seconds * 1.1) * 12.0)
		var pan := InputEventMouseMotion.new()
		pan.position = Vector2(1000, 450)
		pan.relative = offset - previous_offset
		pan.button_mask = MOUSE_BUTTON_MASK_MIDDLE
		Input.parse_input_event(pan)
		previous_offset = offset
		motion(map, Vector2(sin(seconds * 2.0) * 2.5 * cell, 0), false)
		pan_ticks += 1
		next_pan_at = Time.get_ticks_usec() + 16667
		await process_frame
	middle = InputEventMouseButton.new()
	middle.position = Vector2(1000, 450)
	middle.button_index = MOUSE_BUTTON_MIDDLE
	middle.pressed = false
	Input.parse_input_event(middle)
	result.panHover = end_frames(started)
	result.panHover.cameraStart = result.samplingCamera
	result.panHover.motionEventCount = pan_ticks * 2
	result.panHover.actualMotionEventsPerSecond = float(pan_ticks * 2) / result.panHover.durationSeconds
	result.panFinalCamera = {"x": camera.position.x, "y": camera.position.y}
	# The junction view does not frame the separate L-chain region. Sample it explicitly.
	var polyline_zoom: float = minf(2.0, 1100.0 / (24.0 * cell))
	camera.zoom = Vector2(polyline_zoom, polyline_zoom)
	camera.position = Vector2(-4 * cell, 7 * cell)
	await create_timer(0.1 if smoke else 1.0).timeout
	started = begin_frames()
	await create_timer(window_seconds).timeout
	result.polylineView = end_frames(started)
	result.polylineView.camera = {"x": camera.position.x, "y": camera.position.y, "zoom": polyline_zoom}
	result.polylineView.purpose = "separate region of 11 canonical L chains; fixture is representative, not a high-chain-point worst case"
	focus(map, Vector2(0, -8 * cell), zoom)
	result.operationCamera = {"x": camera.position.x, "y": camera.position.y, "zoom": zoom}
	await process_frame
	started = begin_frames()
	var profile: OptionButton = map.get_node("HUD/Panel/Margin/Controls/Profile")
	var iteration: int = 0
	while iteration < repetitions or Time.get_ticks_usec() - started < window_seconds * 1000000:
		map.SetToolMode(0)
		profile.select(1)
		var token: String = map.StateToken
		press(map, Vector2(-2 * cell, -10 * cell))
		motion(map, Vector2(2 * cell, -10 * cell))
		release(map, Vector2(2 * cell, -10 * cell))
		if not await wait_drawn(map, token):
			result.error = "Build failed or timed out at %d: %s" % [iteration, map.BuildPhase]
			return result
		var metrics: Dictionary = map.GetOperationTimings()
		if not valid_metrics(metrics, map.StateToken):
			result.error = "Invalid build timing: " + JSON.stringify(metrics)
			return result
		result.builds.append(metrics)
		token = map.StateToken
		if not map.UndoRoadEdit() or not await wait_drawn(map, token):
			result.error = "Undo failed or timed out"
			return result
		metrics = map.GetOperationTimings()
		if not valid_metrics(metrics, map.StateToken):
			result.error = "Invalid undo timing"
			return result
		result.undos.append(metrics)
		token = map.StateToken
		if not map.RedoRoadEdit() or not await wait_drawn(map, token):
			result.error = "Redo failed or timed out"
			return result
		metrics = map.GetOperationTimings()
		if not valid_metrics(metrics, map.StateToken):
			result.error = "Invalid redo timing"
			return result
		result.redos.append(metrics)
		token = map.StateToken
		if not map.UndoRoadEdit() or not await wait_drawn(map, token):
			result.error = "Second undo after redo failed or timed out"
			return result
		metrics = map.GetOperationTimings()
		if not valid_metrics(metrics, map.StateToken):
			result.error = "Invalid second undo timing"
			return result
		result.undos.append(metrics)
		iteration += 1
	result.buildUndoFrames = end_frames(started)
	result.buildUndoFrames.camera = result.operationCamera
	started = begin_frames()
	iteration = 0
	while iteration < repetitions or Time.get_ticks_usec() - started < window_seconds * 1000000:
		map.SetToolMode(3)
		profile.select(3 if iteration % 2 == 0 else 1)
		var token: String = map.StateToken
		var hold_started: int = Time.get_ticks_usec()
		press(map, Vector2(-3.5 * cell, -8 * cell))
		for span in range(1, 8):
			await wait_input_cadence()
			motion(map, Vector2((-3.5 + span) * cell, -8 * cell))
		await wait_input_cadence()
		var selected: int = map.GetSelectionState().get("selectedCount", 0)
		if selected != 8:
			result.error = "Expected eight selected spans, got %d" % selected
			return result
		var held_ms: float = float(Time.get_ticks_usec() - hold_started) / 1000.0
		release(map, Vector2(3.5 * cell, -8 * cell))
		if not await wait_drawn(map, token):
			result.error = "Upgrade failed or timed out"
			return result
		var metrics: Dictionary = map.GetOperationTimings()
		if not valid_metrics(metrics, map.StateToken):
			result.error = "Invalid upgrade timing"
			return result
		metrics.selectedCount = selected
		metrics.holdBeforeReleaseMs = held_ms
		result.upgrades.append(metrics)
		iteration += 1
	result.upgradeFrames = end_frames(started)
	result.upgradeFrames.camera = result.operationCamera
	started = begin_frames()
	for deletion_index in range(repetitions):
		map.SetToolMode(2)
		var token: String = map.StateToken
		var hold_started: int = Time.get_ticks_usec()
		press(map, Vector2(-3.5 * cell, -8 * cell))
		for span in range(1, 8):
			await wait_input_cadence()
			motion(map, Vector2((-3.5 + span) * cell, -8 * cell))
		await wait_input_cadence()
		var selected: int = map.GetSelectionState().get("selectedCount", 0)
		if selected != 8:
			result.error = "Expected eight delete spans, got %d" % selected
			return result
		var held_ms: float = float(Time.get_ticks_usec() - hold_started) / 1000.0
		release(map, Vector2(3.5 * cell, -8 * cell))
		if not await wait_drawn(map, token):
			result.error = "Delete failed or timed out"
			return result
		var metrics: Dictionary = map.GetOperationTimings()
		if not valid_metrics(metrics, map.StateToken):
			result.error = "Invalid delete timing"
			return result
		metrics.selectedCount = selected
		metrics.holdBeforeReleaseMs = held_ms
		result.deletes.append(metrics)
		token = map.StateToken
		if not map.UndoRoadEdit() or not await wait_drawn(map, token):
			result.error = "Undo after delete failed or timed out"
			return result
		metrics = map.GetOperationTimings()
		if not valid_metrics(metrics, map.StateToken):
			result.error = "Invalid undo-after-delete timing"
			return result
		result.undos.append(metrics)
	result.deleteUndoFrames = end_frames(started)
	result.deleteUndoFrames.camera = result.operationCamera
	started = begin_frames()
	for cancellation_index in range(2 if smoke else 10):
		if not map.SetToolMode(0):
			result.error = "Cancellation build mode was not admitted"
			return result
		await process_frame
		var token: String = map.StateToken
		var history: Dictionary = map.GetHistoryState()
		press(map, Vector2(-2 * cell, -10 * cell))
		await process_frame
		motion(map, Vector2(2 * cell, -10 * cell))
		release(map, Vector2(2 * cell, -10 * cell))
		# Input may still be queued while this coroutine is resumed by process_frame.
		# Drain the gesture before measuring Esc; stale Drawn is not a cancellation.
		Input.flush_buffered_events()
		await process_frame
		var admitted_phase: String = map.BuildPhase
		var admitted_busy: bool = map.IsBuildBusy
		if not admitted_busy and map.StateToken == token:
			result.error = "Cancellation gesture was not admitted; phase=" + admitted_phase
			return result
		var cancel_started: int = Time.get_ticks_usec()
		var cancel_draw_frame: int = Engine.get_frames_drawn()
		escape()
		Input.flush_buffered_events()
		await process_frame
		deadline = Time.get_ticks_msec() + 10000
		while map.IsBuildBusy and Time.get_ticks_msec() < deadline:
			await process_frame
		var elapsed: float = float(Time.get_ticks_usec() - cancel_started) / 1000.0
		if map.IsBuildBusy:
			result.error = "Cancellation timed out"
			return result
		var cancelled: bool = map.BuildPhase == "Cancelled" and map.StateToken == token and map.GetHistoryState() == history
		var race: bool = map.StateToken != token
		result.cancellations.append({"escapeToIdleMs": elapsed, "inputDrawFrame": cancel_draw_frame, "idleDrawFrame": Engine.get_frames_drawn(), "renderFrameDelta": Engine.get_frames_drawn() - cancel_draw_frame, "sourceToken": token, "desiredToken": map.StateToken, "presentedToken": map.GetPresentationState().get("presentedToken"), "cancelled": cancelled, "lostRace": race, "phase": map.BuildPhase, "admittedPhase": admitted_phase, "admittedBusy": admitted_busy, "historyPreserved": map.GetHistoryState() == history})
		if race:
			var committed: String = map.StateToken
			if not map.UndoRoadEdit() or not await wait_drawn(map, committed):
				result.error = "Could not restore cancelled-race build"
				return result
		elif not cancelled:
			result.error = "Esc neither cancelled nor lost a commit race"
			return result
	result.cancellationFrames = end_frames(started)
	result.responseSummary = {"builds": summarize_responses(result.builds), "undos": summarize_responses(result.undos), "redos": summarize_responses(result.redos), "upgrades": summarize_responses(result.upgrades), "deletes": summarize_responses(result.deletes)}
	result.windowFocusedAtEnd = DisplayServer.window_is_focused()
	result.foregroundForAllMeasuredFrames = [result.idle, result.panHover, result.polylineView, result.buildUndoFrames, result.upgradeFrames, result.deleteUndoFrames, result.cancellationFrames].all(func(window): return window.visibility == "foreground")
	result.finalHistory = map.GetHistoryState()
	result.finalRoadState = map.GetRoadState()
	result.passed = true
	map.queue_free()
	await process_frame
	return result

func wait_drawn(map: Node, previous: String) -> bool:
	var deadline: int = Time.get_ticks_msec() + 10000
	while Time.get_ticks_msec() < deadline:
		await process_frame
		if map.StateToken != previous and map.BuildPhase == "Drawn" and map.IsPresentationCurrent:
			return true
		if not map.IsBuildBusy and map.BuildPhase in ["Failed", "Cancelled", "Rejected"]:
			return false
	return false

func wait_input_cadence() -> void:
	var until: int = Time.get_ticks_usec() + 16667
	while Time.get_ticks_usec() < until:
		await process_frame

func valid_metrics(metrics: Dictionary, current_token: String) -> bool:
	if metrics.get("phase") != "Drawn" or metrics.get("presentedToken") != current_token:
		return false
	if metrics.get("desiredToken") != current_token or not str(metrics.get("failureReason", "missing")).is_empty(): return false
	if typeof(metrics.get("renderFrameDelta")) != TYPE_INT or metrics.renderFrameDelta < 0: return false
	if metrics.resultDrawFrame - metrics.inputDrawFrame != metrics.renderFrameDelta: return false
	var total: float = metrics.get("inputToDrawMs", -1.0)
	if total <= 0 or metrics.get("sourceToken", "").is_empty(): return false
	for key in ["domainMs", "presentationPrepareMs", "referenceCommitMs", "presentationCommitMs"]:
		var value: float = metrics.get(key, -1.0)
		if not is_finite(value) or value < 0: return false
	if abs(metrics.domainMs + metrics.presentationPrepareMs - metrics.workerMs) > 0.001: return false
	if metrics.referenceCommitMs + metrics.presentationCommitMs > metrics.publicationMs + 0.001: return false
	var phase_sum: float = 0.0
	for key in ["inputPreparationMs", "workerQueueMs", "workerMs", "resumeMs", "preflightMs", "publicationMs", "publishToDrawMs"]:
		var value: float = metrics.get(key, -1.0)
		if not is_finite(value) or value < 0 or value > total + 0.1: return false
		phase_sum += value
	metrics.uninstrumentedBookkeepingMs = total - phase_sum
	return is_finite(total) and phase_sum <= total + 0.1

func summarize_responses(metrics: Array) -> Dictionary:
	var values: Array[float] = []
	var over_100: int = 0
	var over_300: int = 0
	for sample in metrics:
		var value: float = sample.inputToDrawMs
		values.append(value)
		if value > 100.0: over_100 += 1
		if value > 300.0: over_300 += 1
	values.sort()
	return {"count": values.size(), "p95Ms": percentile(values, 0.95), "p99Ms": percentile(values, 0.99), "maxMs": values.back() if not values.is_empty() else 0.0, "over100MsCount": over_100, "over300MsCount": over_300, "over300MsRatio": float(over_300) / max(1, values.size())}
