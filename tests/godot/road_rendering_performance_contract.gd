extends SceneTree

const MAP_SCENE := "res://Scenes/MapTest.tscn"
const TEST_SLOT_NAME := "Road rendering performance contract"
const DATASET_SIZES: Array[int] = [10_000, 100_000]
const EDGE_LENGTH := 8.0
const EDGE_SPACING := 32.0
const FRAME_BUDGET_MS := 16.67
const CAMERA_SAMPLE_COUNT := 120
const DYNAMIC_SAMPLE_COUNT := 60

var test_map: Node
var save_manager: Node
var slot_id := ""
var enforce_budget := false
var failed_budget_scenarios: Array[String] = []

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	enforce_budget = OS.get_cmdline_user_args().has("--enforce-budget")
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	Engine.max_fps = 0
	OS.low_processor_usage_mode = false
	var packed_map: PackedScene = load(MAP_SCENE)
	if not require(packed_map != null, "MapTest scene did not load"):
		return
	test_map = packed_map.instantiate()
	var autosave_controller: Node = test_map.get_node("AutosaveController")
	autosave_controller.set("AutosaveEnabled", false)
	root.add_child(test_map)
	current_scene = test_map
	await wait_rendered_frame()
	autosave_controller.SetAutosaveEnabled(false)
	test_map.get_node("GameHUD").visible = false

	var camera: Camera2D = test_map.get_node("Camera2D")
	camera.process_mode = Node.PROCESS_MODE_DISABLED
	camera.zoom = Vector2(0.125, 0.125)
	var renderer: Node = test_map.get_node("RoadSystem/RoadRenderer")

	save_manager = root.get_node("SaveManager")
	if not require(save_manager.SaveAs(TEST_SLOT_NAME), "Performance fixture slot was not created"):
		return
	slot_id = save_manager.get("CurrentSlotID")
	var road_path := "res://saves/%s/road_network.json" % slot_id

	for edge_count: int in DATASET_SIZES:
		var columns: int = ceili(sqrt(float(edge_count) * 16.0 / 9.0))
		var rows: int = ceili(float(edge_count) / float(columns))
		camera.position = Vector2(EDGE_LENGTH * 0.5, 0.0)
		if not require(write_fixture(road_path, edge_count, columns, rows), "Performance fixture could not be written"):
			return

		var rebuild_start_us: int = Time.get_ticks_usec()
		if not require(save_manager.Load(slot_id), "Performance fixture did not load"):
			return
		var rebuild_ms: float = float(Time.get_ticks_usec() - rebuild_start_us) / 1000.0
		if not require(renderer.GetRenderedEdgeCount() == edge_count, "Renderer did not rebuild the requested Edge count"):
			return
		for _warmup in range(10):
			await wait_rendered_frame()

		var camera_samples: Array[float] = await sample_camera_frames(camera)
		var camera_metrics := capture_render_metrics(renderer)
		var preview_samples: Array[float] = await sample_preview_frames(renderer)
		var preview_metrics := capture_render_metrics(renderer)
		renderer.set("PreviewPoints", PackedVector2Array())
		renderer.queue_redraw()
		await wait_rendered_frame()
		var highlight_samples: Array[float] = await sample_highlight_frames(renderer, edge_count * 2 + 1)
		var highlight_metrics := capture_render_metrics(renderer)
		renderer.set("HoveredEdgeID", null)
		renderer.queue_redraw()
		await wait_rendered_frame()

		print_result(edge_count, "camera", camera_samples, rebuild_ms, camera_metrics)
		print_result(edge_count, "preview", preview_samples, rebuild_ms, preview_metrics)
		print_result(edge_count, "highlight", highlight_samples, rebuild_ms, highlight_metrics)

	if enforce_budget and not failed_budget_scenarios.is_empty():
		fail("10k rendering frame budget exceeded: %s" % "、".join(failed_budget_scenarios))
		return

	if not require(save_manager.DeleteSlot(slot_id), "Performance fixture slot cleanup failed"):
		return
	slot_id = ""
	test_map.queue_free()
	await process_frame
	await process_frame
	if not require(save_manager.get("RegisteredSaveableCount") == 0, "Performance cleanup retained saveables"):
		return

	print("PASS road rendering performance contract")
	quit(0)

func sample_camera_frames(camera: Camera2D) -> Array[float]:
	var samples: Array[float] = []
	for index in range(CAMERA_SAMPLE_COUNT):
		var start_us: int = Time.get_ticks_usec()
		camera.position = Vector2(EDGE_LENGTH * 0.5 + float(index % 30) * 8.0, float(index % 20) * 4.0)
		await wait_rendered_frame()
		samples.append(float(Time.get_ticks_usec() - start_us) / 1000.0)
	return samples

func sample_preview_frames(renderer: Node) -> Array[float]:
	var samples: Array[float] = []
	for index in range(DYNAMIC_SAMPLE_COUNT):
		var offset := float(index % 20) * 2.0
		var start_us: int = Time.get_ticks_usec()
		renderer.set("PreviewPoints", PackedVector2Array([
			Vector2(-120.0, -80.0),
			Vector2(offset, -20.0),
			Vector2(120.0, 80.0),
		]))
		renderer.queue_redraw()
		await wait_rendered_frame()
		samples.append(float(Time.get_ticks_usec() - start_us) / 1000.0)
	return samples

func sample_highlight_frames(renderer: Node, first_edge_id: int) -> Array[float]:
	var samples: Array[float] = []
	for index in range(DYNAMIC_SAMPLE_COUNT):
		var start_us: int = Time.get_ticks_usec()
		renderer.set("HoveredEdgeID", first_edge_id + index)
		renderer.queue_redraw()
		await wait_rendered_frame()
		samples.append(float(Time.get_ticks_usec() - start_us) / 1000.0)
	return samples

func capture_render_metrics(renderer: Node) -> Dictionary:
	return {
		"render_nodes": renderer.get_child_count(),
		"draw_calls": int(Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME)),
		"objects": int(Performance.get_monitor(Performance.RENDER_TOTAL_OBJECTS_IN_FRAME)),
		"primitives": int(Performance.get_monitor(Performance.RENDER_TOTAL_PRIMITIVES_IN_FRAME)),
	}

func print_result(edge_count: int, scenario: String, samples: Array[float], rebuild_ms: float, metrics: Dictionary) -> void:
	var mean_ms := mean(samples)
	var p95_ms := percentile95(samples)
	var result := {
		"edges": edge_count,
		"scenario": scenario,
		"mean_ms": snappedf(mean_ms, 0.001),
		"p95_ms": snappedf(p95_ms, 0.001),
		"rebuild_ms": snappedf(rebuild_ms, 0.001),
		"render_nodes": metrics.render_nodes,
		"draw_calls": metrics.draw_calls,
		"objects": metrics.objects,
		"primitives": metrics.primitives,
	}
	print("RESULT %s" % JSON.stringify(result))
	if edge_count == 10_000 and p95_ms > FRAME_BUDGET_MS:
		failed_budget_scenarios.append(scenario)

func mean(samples: Array[float]) -> float:
	var total := 0.0
	for sample: float in samples:
		total += sample
	return total / float(samples.size())

func percentile95(samples: Array[float]) -> float:
	var sorted: Array[float] = samples.duplicate()
	sorted.sort()
	var index: int = ceili(float(sorted.size()) * 0.95) - 1
	return sorted[clampi(index, 0, sorted.size() - 1)]

func wait_rendered_frame() -> void:
	await process_frame
	await RenderingServer.frame_post_draw

func write_fixture(path: String, edge_count: int, columns: int, rows: int) -> bool:
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return false
	var width := float(columns - 1) * EDGE_SPACING
	var height := float(rows - 1) * EDGE_SPACING
	file.store_string('{"schemaVersion":1,"nextID":%d,"nodes":[' % (edge_count * 4 + 1))
	for index in range(edge_count):
		var position := fixture_position(index, columns, width, height)
		write_item(file, {
			"id": index * 2 + 1,
			"x": position.x,
			"y": position.y,
		}, index > 0)
		write_item(file, {
			"id": index * 2 + 2,
			"x": position.x + EDGE_LENGTH,
			"y": position.y,
		}, true)
	file.store_string('],"edges":[')
	for index in range(edge_count):
		var position := fixture_position(index, columns, width, height)
		var edge_id := edge_count * 2 + index + 1
		var group_id := edge_count * 3 + index + 1
		write_item(file, {
			"id": edge_id,
			"nodeAID": index * 2 + 1,
			"nodeBID": index * 2 + 2,
			"groupID": group_id,
			"geometry": [{
				"version": 1,
				"kind": "line",
				"start": {"x": position.x, "y": position.y},
				"end": {"x": position.x + EDGE_LENGTH, "y": position.y},
			}],
		}, index > 0)
	file.store_string('],"groups":[')
	for index in range(edge_count):
		var edge_id := edge_count * 2 + index + 1
		var group_id := edge_count * 3 + index + 1
		write_item(file, {"id": group_id, "edgeIDs": [edge_id]}, index > 0)
	file.store_string(']}')
	file.close()
	return true

func fixture_position(index: int, columns: int, width: float, height: float) -> Vector2:
	var column := index % columns
	var row := index / columns
	return Vector2(float(column) * EDGE_SPACING - width * 0.5, float(row) * EDGE_SPACING - height * 0.5)

func write_item(file: FileAccess, value: Dictionary, prepend_comma: bool) -> void:
	if prepend_comma:
		file.store_8(44)
	file.store_string(JSON.stringify(value))

func require(condition: bool, message: String) -> bool:
	if condition:
		return true
	fail(message)
	return false

func fail(message: String) -> void:
	push_error(message)
	if save_manager != null and not slot_id.is_empty():
		save_manager.DeleteSlot(slot_id)
		slot_id = ""
	if test_map != null:
		test_map.queue_free()
	quit(1)
