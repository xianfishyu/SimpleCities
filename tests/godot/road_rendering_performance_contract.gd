extends SceneTree

const MAP_SCENE := "res://Scenes/MapTest.tscn"
const TEST_SLOT_NAME := "Road rendering performance contract"
const V3_SAVE_FIXTURE := preload("res://tests/godot/v3_save_fixture.gd")
const DATASET_SIZES: Array[int] = [10_000, 100_000]
const DATASET_KINDS: Array[String] = ["grid", "junction-dense", "geometry-dense", "owner-dense"]
const EDGE_LENGTH := 8.0
const EDGE_SPACING := 32.0
const GEOMETRY_DENSE_EDGE_SPACING := 320.0
const GEOMETRY_DENSE_SEGMENT_LENGTH := 32.0
const GEOMETRY_DENSE_SEGMENT_COUNT := 8
const GEOMETRY_DENSE_WAVE_HEIGHT := 12.0
const OWNER_DENSE_EDGES_PER_CELL := 8
const OWNER_DENSE_NODES_PER_CELL := 13
const OWNER_DENSE_CELL_SPACING := 320.0
const OWNER_HIT_BATCH_COUNT := 20
const OWNER_HIT_QUERIES_PER_BATCH := 1000
const FRAME_BUDGET_MS := 16.67
# These one-shot 10k budgets are intentionally separate from the continuous
# frame budget. They leave room for cold-start variance while keeping a fixed
# regression gate for the three supported batch sizes.
const TYPE_CHANGE_10K_BUDGET_MS := {
	1: {"upgrade": 350.0, "undo": 250.0, "redo": 250.0},
	100: {"upgrade": 400.0, "undo": 300.0, "redo": 300.0},
	1000: {"upgrade": 500.0, "undo": 400.0, "redo": 400.0},
}
const CAMERA_SAMPLE_COUNT := 120
const DYNAMIC_SAMPLE_COUNT := 60

var test_map: Node
var save_manager: Node
var slot_id := ""
var enforce_budget := false
var measure_type_change_latency := false
var measure_owner_hit_latency := false
var failed_budget_scenarios: Array[String] = []
var failure_cleanup_started := false
var dataset_kind := "grid"

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	enforce_budget = OS.get_cmdline_user_args().has("--enforce-budget")
	measure_type_change_latency = OS.get_cmdline_user_args().has("--measure-type-change")
	measure_owner_hit_latency = OS.get_cmdline_user_args().has("--measure-owner-hits")
	dataset_kind = read_requested_dataset_kind()
	if not require(DATASET_KINDS.has(dataset_kind), "Unknown rendering performance dataset kind: %s" % dataset_kind):
		return
	if not require(not measure_type_change_latency or dataset_kind == "grid", "Type-change latency requires the grid dataset"):
		return
	if not require(not measure_owner_hit_latency or dataset_kind == "owner-dense", "Owner-hit latency requires the owner-dense dataset"):
		return
	var requested_dataset_size := read_requested_dataset_size()
	var dataset_sizes: Array[int] = DATASET_SIZES.duplicate()
	if requested_dataset_size > 0:
		dataset_sizes.clear()
		dataset_sizes.append(requested_dataset_size)
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
	var game_hud: CanvasLayer = test_map.get_node("GameHUD")
	game_hud.process_mode = Node.PROCESS_MODE_DISABLED
	game_hud.visible = false

	var camera: Camera2D = test_map.get_node("Camera2D")
	camera.process_mode = Node.PROCESS_MODE_DISABLED
	camera.zoom = Vector2(0.125, 0.125)
	var renderer: Node = test_map.get_node("RoadSystem/RoadRenderer")
	var builder: Node = test_map.get_node("RoadSystem/RoadBuilder")

	save_manager = root.get_node("SaveManager")
	if not require(await V3_SAVE_FIXTURE.save_as(save_manager, TEST_SLOT_NAME), "Performance fixture slot was not created"):
		return
	slot_id = save_manager.get("CurrentSlotID")
	var road_path: String = V3_SAVE_FIXTURE.slot_path(slot_id, "road_network.json")

	for edge_count: int in dataset_sizes:
		var columns: int = ceili(sqrt(float(edge_count) * 16.0 / 9.0))
		var rows: int = ceili(float(edge_count) / float(columns))
		camera.position = Vector2.ZERO if dataset_kind != "grid" else Vector2(EDGE_LENGTH * 0.5, 0.0)
		print("STAGE fixture-write-start edges=%d" % edge_count)
		if not require(write_fixture(road_path, edge_count, columns, rows), "Performance fixture could not be written"):
			return
		print("STAGE fixture-write-done edges=%d" % edge_count)

		var rebuild_start_us: int = Time.get_ticks_usec()
		print("STAGE load-start edges=%d" % edge_count)
		if not require(await V3_SAVE_FIXTURE.load_slot(save_manager, slot_id), "Performance fixture did not load"):
			return
		print("STAGE load-done edges=%d" % edge_count)
		var rebuild_ms: float = float(Time.get_ticks_usec() - rebuild_start_us) / 1000.0
		print("STAGE renderer-count-start edges=%d" % edge_count)
		if not require(renderer.GetRenderedEdgeCount() == edge_count, "Renderer did not rebuild the requested Edge count"):
			return
		print("STAGE renderer-count-done edges=%d" % edge_count)
		for _warmup in range(10):
			await wait_rendered_frame()

		var camera_samples: Array[float] = await sample_camera_frames(camera)
		var camera_metrics := capture_render_metrics(renderer)
		var preview_samples: Array[float] = await sample_preview_frames(renderer)
		var preview_metrics := capture_render_metrics(renderer)
		renderer.set("PreviewPoints", PackedVector2Array())
		renderer.queue_redraw()
		await wait_rendered_frame()
		var highlight_samples: Array[float] = await sample_highlight_frames(renderer, first_edge_id_for_dataset(edge_count))
		var highlight_metrics := capture_render_metrics(renderer)
		renderer.set("HoveredEdgeID", null)
		renderer.queue_redraw()
		await wait_rendered_frame()

		print_result(edge_count, "camera", camera_samples, rebuild_ms, camera_metrics)
		print_result(edge_count, "preview", preview_samples, rebuild_ms, preview_metrics)
		print_result(edge_count, "highlight", highlight_samples, rebuild_ms, highlight_metrics)
		if measure_type_change_latency and not await measure_type_change_latencies(
			builder,
			renderer,
			edge_count,
			columns,
			rows):
			return
		if measure_owner_hit_latency and not measure_owner_hit_latencies(
			renderer,
			edge_count):
			return

	if enforce_budget and not failed_budget_scenarios.is_empty():
		fail("10k rendering frame budget exceeded: %s" % "、".join(failed_budget_scenarios))
		return

	if not require(await V3_SAVE_FIXTURE.delete_slot(save_manager, slot_id), "Performance fixture slot cleanup failed"):
		return
	slot_id = ""
	test_map.queue_free()
	await process_frame
	await process_frame
	if not require(save_manager.get("RegisteredSaveableCount") == 0, "Performance cleanup retained saveables"):
		return

	print("PASS road rendering performance contract dataset=%s" % dataset_kind)
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

func measure_type_change_latencies(
	builder: Node,
	renderer: Node,
	edge_count: int,
	columns: int,
	rows: int) -> bool:
	if not require(edge_count >= 1000, "Type-change latency requires at least 1000 Edge"):
		return false
	if int(builder.GetSelectedRoadType()) != 3 and not require(
		builder.SetSelectedRoadType(3),
		"Type-change latency could not select Highway"):
		return false

	var cases: Array[Dictionary] = [
		{"edges": 1, "column": 0, "row": 0, "columns": 1, "rows": 1},
		{"edges": 100, "column": 20, "row": 0, "columns": 100, "rows": 1},
		{"edges": 1000, "column": 0, "row": 1, "columns": 20, "rows": 50},
	]
	for case_index in range(cases.size()):
		var test_case: Dictionary = cases[case_index]
		var selected_edges := int(test_case.edges)
		var bounds := type_change_selection_bounds(test_case, columns, rows)
		var end := bounds.position + bounds.size
		if not require(
			builder.BeginUpgrade(bounds.position, true),
			"Type-change latency could not begin the %d-Edge selection" % selected_edges):
			return false
		builder.UpdateUpgrade(end)
		if not require(
			builder.GetUpgradeSelectionCount() == selected_edges,
			"Type-change latency selected %d instead of %d Edge" % [builder.GetUpgradeSelectionCount(), selected_edges]):
			return false

		var before_upgrade_sequence := presented_change_sequence(renderer)
		var upgrade_started_us := Time.get_ticks_usec()
		if not require(
			builder.ConfirmUpgrade(end),
			"Type-change latency could not confirm the %d-Edge batch" % selected_edges):
			return false
		var upgrade_ms := await wait_for_current_presentation(
			renderer,
			upgrade_started_us,
			"%d-Edge type change" % selected_edges)
		if upgrade_ms < 0.0:
			return false
		if not require(
			presented_change_sequence(renderer) == before_upgrade_sequence + 1 and
			builder.GetUndoEditCount() == case_index + 1 and
			builder.GetRedoEditCount() == 0,
			"%d-Edge type change did not publish one history boundary" % selected_edges):
			return false

		var before_undo_sequence := presented_change_sequence(renderer)
		var undo_started_us := Time.get_ticks_usec()
		if not require(
			builder.UndoLastEdit(),
			"Type-change latency could not undo the %d-Edge batch" % selected_edges):
			return false
		var undo_ms := await wait_for_current_presentation(
			renderer,
			undo_started_us,
			"%d-Edge type-change undo" % selected_edges)
		if undo_ms < 0.0:
			return false
		if not require(
			presented_change_sequence(renderer) == before_undo_sequence + 1 and
			builder.GetUndoEditCount() == case_index and
			builder.GetRedoEditCount() == 1,
			"%d-Edge type-change undo did not restore one history boundary" % selected_edges):
			return false

		var before_redo_sequence := presented_change_sequence(renderer)
		var redo_started_us := Time.get_ticks_usec()
		if not require(
			builder.RedoLastEdit(),
			"Type-change latency could not redo the %d-Edge batch" % selected_edges):
			return false
		var redo_ms := await wait_for_current_presentation(
			renderer,
			redo_started_us,
			"%d-Edge type-change redo" % selected_edges)
		if redo_ms < 0.0:
			return false
		if not require(
			presented_change_sequence(renderer) == before_redo_sequence + 1 and
			builder.GetUndoEditCount() == case_index + 1 and
			builder.GetRedoEditCount() == 0 and
			renderer.GetRenderedEdgeCount() == edge_count,
			"%d-Edge type-change redo did not republish the exact history boundary" % selected_edges):
			return false

		var budget: Dictionary = TYPE_CHANGE_10K_BUDGET_MS.get(selected_edges, {})
		var gate_passed := budget.is_empty() or (
			upgrade_ms <= float(budget["upgrade"]) and
			undo_ms <= float(budget["undo"]) and
			redo_ms <= float(budget["redo"]))
		print("TYPE_CHANGE_RESULT %s" % JSON.stringify({
			"dataset": dataset_kind,
			"edges": edge_count,
			"changed_edges": selected_edges,
			"upgrade_ms": snappedf(upgrade_ms, 0.001),
			"undo_ms": snappedf(undo_ms, 0.001),
			"redo_ms": snappedf(redo_ms, 0.001),
			"budget_ms": budget,
			"gate": "PASS" if gate_passed or not enforce_budget or edge_count != 10_000 else "FAIL",
			"render_nodes": renderer.GetStaticRenderNodeCount(),
			"surface_primitives": int(renderer.GetPresentationState().get("surfacePrimitiveCount", -1)),
		}))
		if enforce_budget and edge_count == 10_000 and not require(
			gate_passed,
			"%d-Edge type-change latency exceeded its fixed 10k budget: upgrade=%.3f/%.3f ms, undo=%.3f/%.3f ms, redo=%.3f/%.3f ms" % [
				selected_edges,
				upgrade_ms,
				float(budget.get("upgrade", -1.0)),
				undo_ms,
				float(budget.get("undo", -1.0)),
				redo_ms,
				float(budget.get("redo", -1.0)),
			]):
			return false
	return true

func presented_change_sequence(renderer: Node) -> int:
	var presented: Dictionary = renderer.GetPresentationState().get("presented", {})
	return int(presented.get("changeSequence", -1))

func type_change_selection_bounds(test_case: Dictionary, columns: int, rows: int) -> Rect2:
	var start_index := int(test_case.row) * columns + int(test_case.column)
	var end_index := (
		(int(test_case.row) + int(test_case.rows) - 1) * columns +
		int(test_case.column) + int(test_case.columns) - 1)
	var width := float(columns - 1) * EDGE_SPACING
	var height := float(rows - 1) * EDGE_SPACING
	var first := fixture_position(start_index, columns, width, height)
	var last := fixture_position(end_index, columns, width, height)
	var minimum := first - Vector2(12.0, 12.0)
	var maximum := last + Vector2(EDGE_LENGTH + 12.0, 12.0)
	return Rect2(minimum, maximum - minimum)

func wait_for_current_presentation(
	renderer: Node,
	started_us: int,
	source: String) -> float:
	for _frame in range(600):
		var state: Dictionary = renderer.GetPresentationState()
		if bool(state.get("isReady", false)) and state.get("desired", {}) == state.get("presented", {}):
			return float(Time.get_ticks_usec() - started_us) / 1000.0
		await wait_rendered_frame()
	fail("%s did not publish a matching presentation" % source)
	return -1.0

func measure_owner_hit_latencies(renderer: Node, edge_count: int) -> bool:
	if not require(edge_count >= OWNER_DENSE_EDGES_PER_CELL, "Owner-hit latency requires one complete owner cell"):
		return false
	var cell_count := edge_count / OWNER_DENSE_EDGES_PER_CELL
	var columns := ceili(sqrt(float(cell_count) * 16.0 / 9.0))
	var rows := ceili(float(cell_count) / float(columns))
	var cell := owner_dense_cell_position(0, columns, rows)
	var probes: Array[Dictionary] = [
		{
			"kind": "EdgeRibbon",
			"hint": (owner_dense_node_position(cell, 7) + owner_dense_node_position(cell, 8)) * 0.5,
			"radius": 8,
		},
		{
			"kind": "TerminalCap",
			"hint": owner_dense_node_position(cell, 7) + Vector2(-6.0, 0.0),
			"radius": 12,
		},
		{
			"kind": "SemanticJoin",
			"hint": owner_dense_node_position(cell, 4),
			"radius": 24,
		},
		{
			"kind": "JunctionPatch",
			"hint": owner_dense_node_position(cell, 0),
			"radius": 48,
		},
	]
	var presented: Dictionary = renderer.GetPresentationState().get("presented", {})
	for probe: Dictionary in probes:
		var owner_kind := String(probe.kind)
		var position_value: Variant = find_owner_hit_position(
			renderer,
			probe.hint,
			owner_kind,
			int(probe.radius))
		if position_value == null:
			return false
		var position: Vector2 = position_value
		var initial_hit: Dictionary = renderer.FindRoadSurfaceHit(position, 0.0)
		var initial_location: Dictionary = initial_hit.get("location", {})
		if not require(
			initial_hit.get("ownerKind", "") == owner_kind and
			initial_hit.get("renderToken", {}) == presented and
			float(initial_hit.get("surfaceDistance", -1.0)) == 0.0 and
			not initial_location.is_empty(),
			"%s query did not preserve its owner, token, distance, and canonical location" % owner_kind):
			return false

		for _warmup in range(100):
			renderer.FindRoadSurfaceHit(position, 0.0)
		var samples: Array[float] = []
		for _batch in range(OWNER_HIT_BATCH_COUNT):
			var hit_count := 0
			var started_us := Time.get_ticks_usec()
			for _query in range(OWNER_HIT_QUERIES_PER_BATCH):
				var hit: Dictionary = renderer.FindRoadSurfaceHit(position, 0.0)
				if hit.get("ownerKind", "") == owner_kind:
					hit_count += 1
			var elapsed_ms := float(Time.get_ticks_usec() - started_us) / 1000.0
			if not require(
				hit_count == OWNER_HIT_QUERIES_PER_BATCH,
				"%s query batch returned %d/%d matching owner hits" % [owner_kind, hit_count, OWNER_HIT_QUERIES_PER_BATCH]):
				return false
			samples.append(elapsed_ms / float(OWNER_HIT_QUERIES_PER_BATCH))

		print("OWNER_HIT_RESULT %s" % JSON.stringify({
			"dataset": dataset_kind,
			"edges": edge_count,
			"owner_kind": owner_kind,
			"mean_ms": snappedf(mean(samples), 0.000001),
			"p95_ms": snappedf(percentile95(samples), 0.000001),
			"batches": OWNER_HIT_BATCH_COUNT,
			"queries_per_batch": OWNER_HIT_QUERIES_PER_BATCH,
			"render_nodes": renderer.GetStaticRenderNodeCount(),
			"surface_primitives": int(renderer.GetPresentationState().get("surfacePrimitiveCount", -1)),
		}))
	return true

func find_owner_hit_position(
	renderer: Node,
	hint: Vector2,
	owner_kind: String,
	radius: int) -> Variant:
	var hinted: Dictionary = renderer.FindRoadSurfaceHit(hint, 0.0)
	if hinted.get("ownerKind", "") == owner_kind:
		return hint
	for y in range(-radius, radius + 1):
		for x in range(-radius, radius + 1):
			var position := hint + Vector2(x, y)
			var hit: Dictionary = renderer.FindRoadSurfaceHit(position, 0.0)
			if hit.get("ownerKind", "") == owner_kind:
				return position
	fail("No %s hit was found near %s" % [owner_kind, hint])
	return null

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
		"dataset": dataset_kind,
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
	if dataset_kind == "junction-dense":
		return write_junction_dense_fixture(path, edge_count)
	if dataset_kind == "geometry-dense":
		return write_geometry_dense_fixture(path, edge_count)
	if dataset_kind == "owner-dense":
		return write_owner_dense_fixture(path, edge_count)
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return false
	var width := float(columns - 1) * EDGE_SPACING
	var height := float(rows - 1) * EDGE_SPACING
	file.store_string('{"formatFamily":"simple-cities-v3","payloadType":"road-network","schemaVersion":1,"nextID":%d,"nodes":[' % (edge_count * 3 + 1))
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
		write_item(file, {
			"id": edge_id,
			"nodeAID": index * 2 + 1,
			"nodeBID": index * 2 + 2,
			"roadType": "street",
			"geometry": [{
				"version": 1,
				"kind": "line",
				"start": {"x": position.x, "y": position.y},
				"end": {"x": position.x + EDGE_LENGTH, "y": position.y},
			}],
		}, index > 0)
	file.store_string(']}')
	file.close()
	print("STAGE manifest-hash-start edges=%d" % edge_count)
	var refreshed: bool = V3_SAVE_FIXTURE.refresh_manifest_payload(slot_id)
	print("STAGE manifest-hash-done edges=%d" % edge_count)
	return refreshed

func write_junction_dense_fixture(path: String, edge_count: int) -> bool:
	if edge_count <= 0 or edge_count % 4 != 0:
		return false
	var cluster_count := edge_count / 4
	var columns := ceili(sqrt(float(cluster_count) * 16.0 / 9.0))
	var rows := ceili(float(cluster_count) / float(columns))
	var node_count := cluster_count * 5
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return false
	file.store_string('{"formatFamily":"simple-cities-v3","payloadType":"road-network","schemaVersion":1,"nextID":%d,"nodes":[' % (node_count + edge_count + 1))
	for cluster in range(cluster_count):
		var center := junction_cluster_position(cluster, columns, rows)
		var center_id := cluster * 5 + 1
		write_item(file, {"id": center_id, "x": center.x, "y": center.y}, cluster > 0)
		for arm in range(4):
			var endpoint := center + junction_arm_offset(arm)
			write_item(file, {
				"id": center_id + arm + 1,
				"x": endpoint.x,
				"y": endpoint.y,
			}, true)
	file.store_string('],"edges":[')
	var road_types: Array[String] = ["dirt", "street", "arterial", "highway"]
	for cluster in range(cluster_count):
		var center := junction_cluster_position(cluster, columns, rows)
		var center_id := cluster * 5 + 1
		for arm in range(4):
			var edge_index := cluster * 4 + arm
			var edge_id := node_count + edge_index + 1
			var endpoint := center + junction_arm_offset(arm)
			write_item(file, {
				"id": edge_id,
				"nodeAID": center_id,
				"nodeBID": center_id + arm + 1,
				"roadType": road_types[arm],
				"geometry": [{
					"version": 1,
					"kind": "line",
					"start": {"x": center.x, "y": center.y},
					"end": {"x": endpoint.x, "y": endpoint.y},
				}],
			}, edge_index > 0)
	file.store_string(']}')
	file.close()
	print("STAGE manifest-hash-start edges=%d dataset=%s" % [edge_count, dataset_kind])
	var refreshed: bool = V3_SAVE_FIXTURE.refresh_manifest_payload(slot_id)
	print("STAGE manifest-hash-done edges=%d dataset=%s" % [edge_count, dataset_kind])
	return refreshed

func junction_cluster_position(cluster: int, columns: int, rows: int) -> Vector2:
	var spacing := EDGE_SPACING * 3.0
	var width := float(columns - 1) * spacing
	var height := float(rows - 1) * spacing
	return Vector2(
		float(cluster % columns) * spacing - width * 0.5,
		float(cluster / columns) * spacing - height * 0.5)

func junction_arm_offset(arm: int) -> Vector2:
	return [
		Vector2(0.0, -EDGE_LENGTH),
		Vector2(EDGE_LENGTH, 0.0),
		Vector2(0.0, EDGE_LENGTH),
		Vector2(-EDGE_LENGTH, 0.0),
	][arm]

func first_edge_id_for_dataset(edge_count: int) -> int:
	if dataset_kind == "junction-dense":
		return (edge_count / 4) * 5 + 1
	if dataset_kind == "owner-dense":
		return (edge_count / OWNER_DENSE_EDGES_PER_CELL) * OWNER_DENSE_NODES_PER_CELL + 1
	return edge_count * 2 + 1

func write_geometry_dense_fixture(path: String, edge_count: int) -> bool:
	if edge_count <= 0:
		return false
	var columns := ceili(sqrt(float(edge_count) * 16.0 / 9.0))
	var rows := ceili(float(edge_count) / float(columns))
	var node_count := edge_count * 2
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return false
	file.store_string('{"formatFamily":"simple-cities-v3","payloadType":"road-network","schemaVersion":1,"nextID":%d,"nodes":[' % (node_count + edge_count + 1))
	for index in range(edge_count):
		var start := geometry_dense_start(index, columns, rows)
		var end := start + Vector2(GEOMETRY_DENSE_SEGMENT_LENGTH * GEOMETRY_DENSE_SEGMENT_COUNT, 0.0)
		write_item(file, {"id": index * 2 + 1, "x": start.x, "y": start.y}, index > 0)
		write_item(file, {"id": index * 2 + 2, "x": end.x, "y": end.y}, true)
	file.store_string('],"edges":[')
	var road_types: Array[String] = ["dirt", "street", "arterial", "highway"]
	for index in range(edge_count):
		var start := geometry_dense_start(index, columns, rows)
		var geometry: Array[Dictionary] = []
		for segment_index in range(GEOMETRY_DENSE_SEGMENT_COUNT):
			geometry.append({
				"version": 1,
				"kind": "line",
				"start": geometry_dense_point(start, segment_index),
				"end": geometry_dense_point(start, segment_index + 1),
			})
		write_item(file, {
			"id": node_count + index + 1,
			"nodeAID": index * 2 + 1,
			"nodeBID": index * 2 + 2,
			"roadType": road_types[index % road_types.size()],
			"geometry": geometry,
		}, index > 0)
	file.store_string(']}')
	file.close()
	print("STAGE manifest-hash-start edges=%d dataset=%s geometry_segments=%d" % [edge_count, dataset_kind, GEOMETRY_DENSE_SEGMENT_COUNT])
	var refreshed: bool = V3_SAVE_FIXTURE.refresh_manifest_payload(slot_id)
	print("STAGE manifest-hash-done edges=%d dataset=%s geometry_segments=%d" % [edge_count, dataset_kind, GEOMETRY_DENSE_SEGMENT_COUNT])
	return refreshed

func geometry_dense_start(index: int, columns: int, rows: int) -> Vector2:
	var width := float(columns - 1) * GEOMETRY_DENSE_EDGE_SPACING
	var height := float(rows - 1) * GEOMETRY_DENSE_EDGE_SPACING
	return Vector2(
		float(index % columns) * GEOMETRY_DENSE_EDGE_SPACING - width * 0.5,
		float(index / columns) * GEOMETRY_DENSE_EDGE_SPACING - height * 0.5)

func geometry_dense_point(start: Vector2, point_index: int) -> Dictionary:
	var y_offset := 0.0
	if point_index > 0 and point_index < GEOMETRY_DENSE_SEGMENT_COUNT:
		y_offset = GEOMETRY_DENSE_WAVE_HEIGHT if point_index % 2 == 0 else -GEOMETRY_DENSE_WAVE_HEIGHT
	return {
		"x": start.x + float(point_index) * GEOMETRY_DENSE_SEGMENT_LENGTH,
		"y": start.y + y_offset,
	}

func write_owner_dense_fixture(path: String, edge_count: int) -> bool:
	if edge_count <= 0 or edge_count % OWNER_DENSE_EDGES_PER_CELL != 0:
		return false
	var cell_count := edge_count / OWNER_DENSE_EDGES_PER_CELL
	var columns := ceili(sqrt(float(cell_count) * 16.0 / 9.0))
	var rows := ceili(float(cell_count) / float(columns))
	var node_count := cell_count * OWNER_DENSE_NODES_PER_CELL
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return false
	file.store_string('{"formatFamily":"simple-cities-v3","payloadType":"road-network","schemaVersion":1,"nextID":%d,"nodes":[' % (node_count + edge_count + 1))
	for cell_index in range(cell_count):
		var cell := owner_dense_cell_position(cell_index, columns, rows)
		var first_node_id := cell_index * OWNER_DENSE_NODES_PER_CELL + 1
		for local_node_index in range(OWNER_DENSE_NODES_PER_CELL):
			var position := owner_dense_node_position(cell, local_node_index)
			write_item(file, {
				"id": first_node_id + local_node_index,
				"x": position.x,
				"y": position.y,
			}, cell_index > 0 or local_node_index > 0)
	file.store_string('],"edges":[')
	var endpoint_pairs: Array[Vector2i] = [
		Vector2i(0, 1),
		Vector2i(0, 2),
		Vector2i(0, 3),
		Vector2i(4, 5),
		Vector2i(4, 6),
		Vector2i(7, 8),
		Vector2i(9, 10),
		Vector2i(11, 12),
	]
	var road_types: Array[String] = [
		"dirt",
		"highway",
		"arterial",
		"dirt",
		"highway",
		"street",
		"arterial",
		"highway",
	]
	for cell_index in range(cell_count):
		var cell := owner_dense_cell_position(cell_index, columns, rows)
		var first_node_id := cell_index * OWNER_DENSE_NODES_PER_CELL + 1
		for local_edge_index in range(OWNER_DENSE_EDGES_PER_CELL):
			var endpoints := endpoint_pairs[local_edge_index]
			var start := owner_dense_node_position(cell, endpoints.x)
			var end := owner_dense_node_position(cell, endpoints.y)
			var edge_index := cell_index * OWNER_DENSE_EDGES_PER_CELL + local_edge_index
			write_item(file, {
				"id": node_count + edge_index + 1,
				"nodeAID": first_node_id + endpoints.x,
				"nodeBID": first_node_id + endpoints.y,
				"roadType": road_types[local_edge_index],
				"geometry": [{
					"version": 1,
					"kind": "line",
					"start": {"x": start.x, "y": start.y},
					"end": {"x": end.x, "y": end.y},
				}],
			}, edge_index > 0)
	file.store_string(']}')
	file.close()
	print("STAGE manifest-hash-start edges=%d dataset=%s" % [edge_count, dataset_kind])
	var refreshed: bool = V3_SAVE_FIXTURE.refresh_manifest_payload(slot_id)
	print("STAGE manifest-hash-done edges=%d dataset=%s" % [edge_count, dataset_kind])
	return refreshed

func owner_dense_cell_position(cell_index: int, columns: int, rows: int) -> Vector2:
	var width := float(columns - 1) * OWNER_DENSE_CELL_SPACING
	var height := float(rows - 1) * OWNER_DENSE_CELL_SPACING
	return Vector2(
		float(cell_index % columns) * OWNER_DENSE_CELL_SPACING - width * 0.5,
		float(cell_index / columns) * OWNER_DENSE_CELL_SPACING - height * 0.5)

func owner_dense_node_position(cell: Vector2, local_node_index: int) -> Vector2:
	var offsets: Array[Vector2] = [
		Vector2(0.0, -80.0),
		Vector2(64.0, -80.0),
		Vector2(64.0, -64.0),
		Vector2(-64.0, -80.0),
		Vector2(0.0, 0.0),
		Vector2(-32.0, 32.0),
		Vector2(32.0, 32.0),
		Vector2(-32.0, 80.0),
		Vector2(32.0, 80.0),
		Vector2(-32.0, 128.0),
		Vector2(32.0, 128.0),
		Vector2(-32.0, 176.0),
		Vector2(32.0, 176.0),
	]
	return cell + offsets[local_node_index]

func read_requested_dataset_size() -> int:
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--dataset-size="):
			return argument.trim_prefix("--dataset-size=").to_int()
	return 0

func read_requested_dataset_kind() -> String:
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--dataset-kind="):
			return argument.trim_prefix("--dataset-kind=")
	return "grid"

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
	if failure_cleanup_started:
		return
	failure_cleanup_started = true
	cleanup_after_failure.call_deferred()

func cleanup_after_failure() -> void:
	if save_manager != null and not slot_id.is_empty():
		await V3_SAVE_FIXTURE.delete_slot(save_manager, slot_id)
		slot_id = ""
	if test_map != null:
		test_map.queue_free()
	quit(1)
