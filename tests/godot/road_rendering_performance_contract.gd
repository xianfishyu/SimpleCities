extends SceneTree

const MAP_SCENE := "res://Scenes/MapTest.tscn"
const TEST_SLOT_NAME := "Road rendering performance contract"
const V3_SAVE_FIXTURE := preload("res://tests/godot/v3_save_fixture.gd")
const FULL_RESET_PROBE_PATH := "res://tests/godot/RoadFullResetPerformanceProbe.cs"
const UPDATE_TOKEN_FAILURE_PROBE_PATH := "res://tests/godot/RoadRendererUpdateTokenFailureProbe.cs"
const DATASET_SIZES: Array[int] = [10_000, 100_000]
const DATASET_KINDS: Array[String] = ["grid", "junction-dense", "geometry-dense", "owner-dense"]
const TOKEN_PERTURBATION_KINDS: Array[String] = [
	"render-request",
	"road-style",
	"scene-generation",
	"graph-facade-id",
	"graph-facade-generation",
]
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
var measure_full_reset_barrier := false
var failed_budget_scenarios: Array[String] = []
var failure_cleanup_started := false
var dataset_kind := "grid"
var token_perturbation_kind := ""

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	enforce_budget = OS.get_cmdline_user_args().has("--enforce-budget")
	measure_type_change_latency = OS.get_cmdline_user_args().has("--measure-type-change")
	measure_owner_hit_latency = OS.get_cmdline_user_args().has("--measure-owner-hits")
	measure_full_reset_barrier = OS.get_cmdline_user_args().has("--measure-full-reset")
	dataset_kind = read_requested_dataset_kind()
	token_perturbation_kind = read_requested_token_perturbation_kind()
	if not require(DATASET_KINDS.has(dataset_kind), "Unknown rendering performance dataset kind: %s" % dataset_kind):
		return
	if not require(
		token_perturbation_kind.is_empty() or TOKEN_PERTURBATION_KINDS.has(token_perturbation_kind),
		"Unknown rendering token perturbation kind: %s" % token_perturbation_kind):
		return
	if not require(not measure_type_change_latency or dataset_kind == "grid", "Type-change latency requires the grid dataset"):
		return
	if not require(not measure_owner_hit_latency or dataset_kind == "owner-dense", "Owner-hit latency requires the owner-dense dataset"):
		return
	if not require(token_perturbation_kind.is_empty() or dataset_kind == "grid", "Token perturbation requires the grid dataset"):
		return
	var requested_dataset_size := read_requested_dataset_size()
	if not require(
		token_perturbation_kind != "graph-facade-id" or requested_dataset_size > 0,
		"Graph-facade-ID perturbation requires one explicit dataset size"):
		return
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
	var road_system: Node = test_map.get_node("RoadSystem")

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
		var load_operation_token := str(save_manager.StartLoad(slot_id))
		var load_result := await V3_SAVE_FIXTURE.wait_for_operation(save_manager, load_operation_token)
		if not require(await V3_SAVE_FIXTURE.wait_for_idle(save_manager), "Performance fixture Load did not become idle"):
			return
		var load_result_kind := int(load_result.get("resultKind", -1))
		if not require(
			load_result_kind == V3_SAVE_FIXTURE.RESULT_SUCCEEDED or \
			load_result_kind == V3_SAVE_FIXTURE.RESULT_SUCCEEDED_WITH_WARNINGS,
			"Performance fixture did not load: %s" % JSON.stringify(load_result)):
			return
		print("STAGE load-done edges=%d" % edge_count)
		var rebuild_ms: float = float(Time.get_ticks_usec() - rebuild_start_us) / 1000.0
		var load_phase_metrics: Dictionary = save_manager.GetLastLoadPerformanceMetrics()
		if not validate_load_phase_metrics(
			load_phase_metrics,
			load_operation_token,
			slot_id,
			edge_count,
			rebuild_ms):
			return
		print("STAGE renderer-count-start edges=%d" % edge_count)
		if not require(renderer.GetRenderedEdgeCount() == edge_count, "Renderer did not rebuild the requested Edge count"):
			return
		print("STAGE renderer-count-done edges=%d" % edge_count)
		if measure_full_reset_barrier and not measure_non_aggregate_full_reset(
			road_system,
			renderer,
			edge_count):
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
		if not token_perturbation_kind.is_empty() and not await measure_token_perturbation(
			builder,
			renderer,
			edge_count,
			columns,
			rows):
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

func measure_token_perturbation(
	builder: Node,
	renderer: Node,
	edge_count: int,
	columns: int,
	rows: int
) -> bool:
	var perturbation_label := token_perturbation_label()
	var probe_script: Script = load(UPDATE_TOKEN_FAILURE_PROBE_PATH)
	if not require(probe_script != null, "Debug %s perturbation probe did not load" % perturbation_label):
		return false
	var probe: RefCounted = probe_script.new()
	if not require(probe != null, "Debug %s perturbation probe did not instantiate" % perturbation_label):
		return false

	var retained_state: Dictionary = renderer.GetPresentationState()
	var retained_token: Dictionary = retained_state.get("presented", {})
	var retained_edge_count: int = renderer.GetRenderedEdgeCount()
	var retained_vertex_count: int = renderer.GetRoadMeshVertexCount()
	var retained_marker_count: int = renderer.GetNodeMarkerCount()
	var retained_primitive_count := int(retained_state.get("surfacePrimitiveCount", 0))
	var resource_count_before := int(probe.GetObjectResourceCount())
	if not require(
		bool(retained_state.get("isReady", false)) and
		retained_token == retained_state.get("desired", {}) and
		retained_edge_count == edge_count,
		"%s perturbation did not start from the loaded current presentation" % perturbation_label):
		return false

	match token_perturbation_kind:
		"render-request":
			probe.ArmPreCommitTokenSupersession(renderer)
		"road-style":
			probe.ArmPreCommitRoadStyleSupersession(renderer)
		"scene-generation":
			probe.ArmPreCommitSceneGenerationSupersession(renderer)
		"graph-facade-id":
			probe.ArmPreCommitGraphFacadeIDSupersession(renderer)
		"graph-facade-generation":
			probe.ArmPreCommitGraphFacadeGenerationSupersession(renderer)
	var trigger_count_before := int(probe.GetPreCommitTokenSupersessionCount())
	if not require(
		bool(probe.IsPreCommitTokenSupersessionArmed()),
		"%s perturbation probe did not arm" % perturbation_label):
		return false

	var fixture_width := float(columns - 1) * EDGE_SPACING
	var fixture_height := float(rows - 1) * EDGE_SPACING
	var mutation_start := Vector2(
		fixture_width * 0.5 + EDGE_SPACING * 4.0,
		fixture_height * 0.5 + EDGE_SPACING * 4.0)
	var mutation_end := mutation_start + Vector2(100.0, 0.0)
	var perturbation_started_us := Time.get_ticks_usec()
	if not require(
		builder.BeginPlace(mutation_start),
		"%s perturbation mutation did not begin" % perturbation_label):
		return false
	builder.UpdatePlace(mutation_end)
	if not require(
		builder.CommitPlace(mutation_end),
		"%s perturbation mutation did not commit" % perturbation_label):
		return false

	var rejection_observed := false
	for _frame in range(600):
		await process_frame
		if int(probe.GetPreCommitTokenSupersessionCount()) == trigger_count_before + 1:
			rejection_observed = true
			break
	if not require(
		rejection_observed,
		"%s perturbation did not reach the pre-commit rejection boundary" % perturbation_label):
		return false
	var rejection_ms := float(Time.get_ticks_usec() - perturbation_started_us) / 1000.0

	var post_supersession: Dictionary = renderer.GetPresentationState()
	var superseded: Dictionary = probe.GetPreCommitSupersededToken()
	var replacement: Dictionary = probe.GetPreCommitReplacementToken()
	if not require(
		token_perturbation_tokens_are_sequential(
			token_perturbation_kind,
			retained_token,
			superseded,
			replacement),
		"%s perturbation changed an unexpected token dimension" % perturbation_label):
		return false
	if token_perturbation_kind == "graph-facade-id":
		var synchronous_hit: Dictionary = renderer.FindRoadSurfaceHit(
			(mutation_start + mutation_end) * 0.5,
			EDGE_SPACING * 0.5)
		var synchronous_evidence := {
			"phase": post_supersession.get("phase", ""),
			"is_ready": bool(post_supersession.get("isReady", false)),
			"desired_matches": post_supersession.get("desired", {}) == replacement,
			"presented_matches": post_supersession.get("presented", {}) == replacement,
			"attempt_count": int(post_supersession.get("attemptCount", 0)),
			"edge_count": renderer.GetRenderedEdgeCount(),
			"retained_edge_count": retained_edge_count,
			"resource_count": int(probe.GetObjectResourceCount()),
			"resource_count_before": resource_count_before,
			"hit_found": not synchronous_hit.is_empty(),
		}
		if not require(
			post_supersession.get("phase", "") == "ready" and
			bool(post_supersession.get("isReady", false)) and
			not bool(post_supersession.get("isStalled", true)) and
			post_supersession.get("desired", {}) == replacement and
			post_supersession.get("presented", {}) == replacement and
			post_supersession.get("stalledToken", {}).is_empty() and
			str(post_supersession.get("failureType", "")).is_empty() and
			str(post_supersession.get("failureMessage", "")).is_empty() and
			int(post_supersession.get("attemptCount", 0)) == 1 and
			int(probe.GetPreCommitSupersededAttemptNumber()) == 1 and
			not bool(probe.IsPreCommitTokenSupersessionArmed()) and
			int(probe.GetObjectResourceCount()) == resource_count_before and
			renderer.GetRenderedEdgeCount() == retained_edge_count + 1 and
			renderer.GetRoadMeshVertexCount() > retained_vertex_count and
			renderer.GetNodeMarkerCount() > retained_marker_count and
			int(post_supersession.get("surfacePrimitiveCount", 0)) > retained_primitive_count and
			not synchronous_hit.is_empty() and
			synchronous_hit.get("renderToken", {}) == replacement and
			not probe.CompletePreCommitTokenSupersession(),
			"%s perturbation did not synchronously publish one complete replacement: %s" % [
				perturbation_label,
				JSON.stringify(synchronous_evidence),
			]):
			return false
		print_token_perturbation_result(
			renderer,
			probe,
			edge_count,
			resource_count_before,
			superseded,
			replacement,
			rejection_ms,
			0.0,
			"synchronous")
		return true

	if not require(
		post_supersession.get("phase", "") == "pending" and
		not bool(post_supersession.get("isReady", true)) and
		not bool(post_supersession.get("isStalled", true)) and
		post_supersession.get("presented", {}) == retained_token and
		post_supersession.get("desired", {}) == replacement and
		int(post_supersession.get("attemptCount", -1)) == 0 and
		int(probe.GetPreCommitSupersededAttemptNumber()) == 1 and
		not bool(probe.IsPreCommitTokenSupersessionArmed()),
		"%s perturbation did not retain the old presentation behind the replacement token" % perturbation_label):
		return false
	if not require(
		int(probe.GetObjectResourceCount()) == resource_count_before and
		int(post_supersession.get("surfacePrimitiveCount", -1)) == 0 and
		int(post_supersession.get("retainedSurfacePrimitiveCount", -1)) == retained_primitive_count and
		renderer.GetRenderedEdgeCount() == retained_edge_count and
		renderer.GetRoadMeshVertexCount() == retained_vertex_count and
		renderer.GetNodeMarkerCount() == retained_marker_count and
		renderer.FindRoadSurfaceHit(
			(mutation_start + mutation_end) * 0.5,
			EDGE_SPACING * 0.5).is_empty(),
		"%s perturbation leaked resources, swapped presentation, or exposed a mixed-token surface" % perturbation_label):
		return false

	var recovery_started_us := Time.get_ticks_usec()
	if not require(
		probe.CompletePreCommitTokenSupersession(),
		"%s perturbation replacement did not publish" % perturbation_label):
		return false
	var recovery_ms := float(Time.get_ticks_usec() - recovery_started_us) / 1000.0
	var recovered: Dictionary = renderer.GetPresentationState()
	var recovered_hit: Dictionary = renderer.FindRoadSurfaceHit(
		(mutation_start + mutation_end) * 0.5,
		EDGE_SPACING * 0.5)
	var recovered_evidence := {
		"is_ready": bool(recovered.get("isReady", false)),
		"desired_matches": recovered.get("desired", {}) == replacement,
		"presented_matches": recovered.get("presented", {}) == replacement,
		"attempt_count": int(recovered.get("attemptCount", 0)),
		"edge_count": renderer.GetRenderedEdgeCount(),
		"retained_edge_count": retained_edge_count,
		"vertex_count": renderer.GetRoadMeshVertexCount(),
		"retained_vertex_count": retained_vertex_count,
		"marker_count": renderer.GetNodeMarkerCount(),
		"retained_marker_count": retained_marker_count,
		"primitive_count": int(recovered.get("surfacePrimitiveCount", 0)),
		"retained_primitive_count": retained_primitive_count,
		"resource_count": int(probe.GetObjectResourceCount()),
		"resource_count_before": resource_count_before,
		"hit_found": not recovered_hit.is_empty(),
	}
	if not require(
		bool(recovered.get("isReady", false)) and
		recovered.get("desired", {}) == replacement and
		recovered.get("presented", {}) == replacement and
		int(recovered.get("attemptCount", 0)) == 1 and
		renderer.GetRenderedEdgeCount() == retained_edge_count + 1 and
		renderer.GetRoadMeshVertexCount() > retained_vertex_count and
		renderer.GetNodeMarkerCount() > retained_marker_count and
		int(recovered.get("surfacePrimitiveCount", 0)) > retained_primitive_count and
		int(probe.GetObjectResourceCount()) == resource_count_before and
		not recovered_hit.is_empty(),
		"%s perturbation replacement did not publish one complete current presentation: %s" % [
			perturbation_label,
			JSON.stringify(recovered_evidence),
		]):
		return false

	print_token_perturbation_result(
		renderer,
		probe,
		edge_count,
		resource_count_before,
		superseded,
		replacement,
		rejection_ms,
		recovery_ms,
		"deferred")
	return true

func print_token_perturbation_result(
	renderer: Node,
	probe: RefCounted,
	edge_count: int,
	resource_count_before: int,
	superseded: Dictionary,
	replacement: Dictionary,
	rejection_ms: float,
	recovery_ms: float,
	replacement_mode: String
) -> void:
	print("TOKEN_PERTURBATION_RESULT %s" % JSON.stringify({
		"dataset": dataset_kind,
		"edges_before": edge_count,
		"edges_after": renderer.GetRenderedEdgeCount(),
		"dimension": token_perturbation_dimension(),
		"replacement_mode": replacement_mode,
		"rejection_ms": snappedf(rejection_ms, 0.001),
		"recovery_ms": snappedf(recovery_ms, 0.001),
		"total_ms": snappedf(rejection_ms + recovery_ms, 0.001),
		"resource_count_before": resource_count_before,
		"resource_count_after": int(probe.GetObjectResourceCount()),
		"superseded_attempt": int(probe.GetPreCommitSupersededAttemptNumber()),
		"replacement_attempt": int(renderer.GetPresentationState().get("attemptCount", 0)),
		"superseded_token": superseded,
		"replacement_token": replacement,
	}))

func token_perturbation_tokens_are_sequential(
	perturbation_kind: String,
	retained: Dictionary,
	superseded: Dictionary,
	replacement: Dictionary
) -> bool:
	var token_dimensions: Array[String] = [
		"sceneGeneration",
		"graphFacadeID",
		"graphFacadeGeneration",
		"changeSequence",
		"roadStyleRevision",
		"renderRequestID",
	]
	for dimension: String in token_dimensions:
		if dimension == "changeSequence" or dimension == "renderRequestID":
			continue
		if retained.get(dimension) != superseded.get(dimension):
			return false
	var perturbation_dimension := token_perturbation_dimension()
	var replacement_changed_dimensions: Array[String] = ["renderRequestID"]
	match perturbation_kind:
		"road-style", "scene-generation", "graph-facade-generation":
			replacement_changed_dimensions.append(perturbation_dimension)
		"graph-facade-id":
			replacement_changed_dimensions.append("graphFacadeID")
			replacement_changed_dimensions.append("graphFacadeGeneration")
	for dimension: String in token_dimensions:
		if replacement_changed_dimensions.has(dimension):
			continue
		if superseded.get(dimension) != replacement.get(dimension):
			return false
	if (
		int(superseded.get("changeSequence", -1)) != int(retained.get("changeSequence", -2)) + 1 or
		int(superseded.get("renderRequestID", -1)) != int(retained.get("renderRequestID", -2)) + 1 or
		int(replacement.get("renderRequestID", -1)) != int(superseded.get("renderRequestID", -2)) + 1
	):
		return false
	match perturbation_kind:
		"render-request":
			return true
		"road-style", "scene-generation", "graph-facade-generation":
			return int(replacement.get(perturbation_dimension, -1)) == int(superseded.get(perturbation_dimension, -2)) + 1
		"graph-facade-id":
			return (
				int(replacement.get("graphFacadeID", 0)) > 0 and
				replacement.get("graphFacadeID") != superseded.get("graphFacadeID") and
				int(replacement.get("graphFacadeGeneration", -1)) == int(superseded.get("graphFacadeGeneration", -2)) + 1
			)
	return false

func token_perturbation_label() -> String:
	match token_perturbation_kind:
		"render-request":
			return "Render-request"
		"road-style":
			return "Road-style"
		"scene-generation":
			return "Scene-generation"
		"graph-facade-id":
			return "Graph-facade-ID"
		"graph-facade-generation":
			return "Graph-facade-generation"
	return "Unknown token"

func token_perturbation_dimension() -> String:
	match token_perturbation_kind:
		"render-request":
			return "renderRequestID"
		"road-style":
			return "roadStyleRevision"
		"scene-generation":
			return "sceneGeneration"
		"graph-facade-id":
			return "graphFacadeID"
		"graph-facade-generation":
			return "graphFacadeGeneration"
	return "unknown"

func validate_load_phase_metrics(
	metrics: Dictionary,
	operation_token: String,
	target_slot_id: String,
	edge_count: int,
	observed_load_ms: float) -> bool:
	if not require(not metrics.is_empty(), "Load performance metrics were not recorded"):
		return false
	if not require(str(metrics.get("operationToken", "")) == operation_token, "Load performance metrics used a stale operation token"):
		return false
	if not require(str(metrics.get("targetSlotID", "")) == target_slot_id, "Load performance metrics used a stale target slot"):
		return false

	var worker_prepare_ms := float(metrics.get("workerPrepareMs", -1.0))
	var preflight_ms := float(metrics.get("preflightMs", -1.0))
	var reference_commit_ms := float(metrics.get("referenceCommitMs", -1.0))
	var aggregate_commit_ms := float(metrics.get("aggregateCommitMs", -1.0))
	var total_ms := float(metrics.get("totalMs", -1.0))
	if not require(
		worker_prepare_ms >= 0.0 and preflight_ms >= 0.0 and reference_commit_ms >= 0.0 \
		and aggregate_commit_ms >= 0.0 and total_ms >= 0.0,
		"Load performance metrics contained a negative duration"):
		return false
	if not require(reference_commit_ms <= aggregate_commit_ms, "Reference commit exceeded aggregate commit duration"):
		return false
	if not require(aggregate_commit_ms <= total_ms, "Aggregate commit exceeded total Load duration"):
		return false

	var phase_result := metrics.duplicate()
	phase_result["dataset"] = dataset_kind
	phase_result["edges"] = edge_count
	phase_result["observedLoadMs"] = observed_load_ms
	print("LOAD_PHASE_RESULT %s" % JSON.stringify(phase_result))
	return true

func measure_non_aggregate_full_reset(
	road_system: Node,
	renderer: Node,
	edge_count: int) -> bool:
	var before_state: Dictionary = renderer.GetPresentationState()
	var before_token: Dictionary = before_state.get("presented", {})
	var before_surface_primitives := int(before_state.get("surfacePrimitiveCount", -1))
	var before_mesh_vertices := int(renderer.GetRoadMeshVertexCount())
	var before_markers := int(renderer.GetNodeMarkerCount())
	if not require(
		bool(before_state.get("isReady", false)) and
		before_token == before_state.get("desired", {}),
		"Non-aggregate full reset requires a ready matching presentation"):
		return false

	var probe_script: Script = load(FULL_RESET_PROBE_PATH)
	if not require(probe_script != null, "Debug full-reset performance probe did not load"):
		return false
	var probe = probe_script.new()
	if not require(probe != null, "Debug full-reset performance probe did not instantiate"):
		return false
	var barrier_result: Dictionary = probe.CommitCurrentSnapshot(road_system)
	var barrier_ms := float(barrier_result.get("barrierMs", -1.0))
	var after_state: Dictionary = renderer.GetPresentationState()
	var after_token: Dictionary = after_state.get("presented", {})
	var metrics: Dictionary = renderer.GetLastPresentationPerformanceMetrics()
	var metrics_token: Dictionary = metrics.get("renderToken", {})

	if not require(
		bool(after_state.get("isReady", false)) and
		not bool(after_state.get("isStalled", true)) and
		after_token == after_state.get("desired", {}) and
		metrics_token == after_token,
		"Non-aggregate full reset did not synchronously publish one matching presentation"):
		return false
	if not require(
		int(barrier_result.get("graphFacadeID", -1)) == int(before_token.get("graphFacadeID", -2)) and
		int(barrier_result.get("beforeChangeSequence", -1)) == int(before_token.get("changeSequence", -2)) and
		int(barrier_result.get("afterChangeSequence", -1)) == int(after_token.get("changeSequence", -2)) and
		int(barrier_result.get("afterChangeSequence", -1)) == int(barrier_result.get("beforeChangeSequence", -2)) + 1 and
		int(barrier_result.get("beforeLineageID", -1)) != int(barrier_result.get("afterLineageID", -1)) and
		int(barrier_result.get("afterDomainRevisionID", -1)) == 0,
		"Non-aggregate full reset did not advance graph lineage and sequence exactly once"):
		return false
	if not require(
		int(after_token.get("sceneGeneration", -1)) == int(before_token.get("sceneGeneration", -2)) and
		int(after_token.get("graphFacadeID", -1)) == int(before_token.get("graphFacadeID", -2)) and
		int(after_token.get("graphFacadeGeneration", -1)) == int(before_token.get("graphFacadeGeneration", -2)) + 1 and
		int(after_token.get("changeSequence", -1)) == int(before_token.get("changeSequence", -2)) + 1 and
		int(after_token.get("roadStyleRevision", -1)) == int(before_token.get("roadStyleRevision", -2)) and
		int(after_token.get("renderRequestID", -1)) == int(before_token.get("renderRequestID", -2)) + 1,
		"Non-aggregate full reset did not advance the expected render-token dimensions"):
		return false
	if not require(
		bool(metrics.get("isFullReset", false)) and
		int(metrics.get("attemptNumber", 0)) > 0,
		"Non-aggregate full reset metrics were not classified as a build attempt"):
		return false

	var snapshot_capture_ms := float(metrics.get("snapshotCaptureMs", -1.0))
	var prepare_ms := float(metrics.get("prepareMs", -1.0))
	var resource_preflight_ms := float(metrics.get("resourcePreflightMs", -1.0))
	var presentation_commit_ms := float(metrics.get("presentationCommitMs", -1.0))
	var rebuild_total_ms := float(metrics.get("rebuildTotalMs", -1.0))
	var request_to_ready_ms := float(metrics.get("requestToReadyMs", -1.0))
	var phase_sum_ms := (
		snapshot_capture_ms +
		prepare_ms +
		resource_preflight_ms +
		presentation_commit_ms)
	if not require(
		barrier_ms >= 0.0 and
		snapshot_capture_ms >= 0.0 and
		prepare_ms >= 0.0 and
		resource_preflight_ms >= 0.0 and
		presentation_commit_ms >= 0.0 and
		phase_sum_ms <= rebuild_total_ms and
		rebuild_total_ms <= request_to_ready_ms and
		request_to_ready_ms <= barrier_ms,
		"Non-aggregate full-reset barrier timing was inconsistent: barrier=%s metrics=%s" % [
			barrier_ms,
			JSON.stringify(metrics),
		]):
		return false
	if not require(
		renderer.GetRenderedEdgeCount() == edge_count and
		int(after_state.get("surfacePrimitiveCount", -1)) == before_surface_primitives and
		int(renderer.GetRoadMeshVertexCount()) == before_mesh_vertices and
		int(renderer.GetNodeMarkerCount()) == before_markers,
		"Non-aggregate full reset changed the rendered graph payload"):
		return false

	print("FULL_RESET_BARRIER_RESULT %s" % JSON.stringify({
		"dataset": dataset_kind,
		"edges": edge_count,
		"barrier_ms": snappedf(barrier_ms, 0.001),
		"snapshot_capture_ms": snappedf(snapshot_capture_ms, 0.001),
		"prepare_ms": snappedf(prepare_ms, 0.001),
		"resource_preflight_ms": snappedf(resource_preflight_ms, 0.001),
		"presentation_commit_ms": snappedf(presentation_commit_ms, 0.001),
		"rebuild_total_ms": snappedf(rebuild_total_ms, 0.001),
		"request_to_ready_ms": snappedf(request_to_ready_ms, 0.001),
		"attempt_number": int(metrics.get("attemptNumber", 0)),
		"before_render_token": before_token,
		"render_token": after_token,
		"surface_primitives": int(after_state.get("surfacePrimitiveCount", -1)),
		"render_nodes": renderer.GetStaticRenderNodeCount(),
	}))
	return true

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
		if not validate_presentation_phase_metrics(
			renderer,
			before_upgrade_sequence + 1,
			edge_count,
			selected_edges,
			"upgrade",
			upgrade_ms):
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
		if not validate_presentation_phase_metrics(
			renderer,
			before_undo_sequence + 1,
			edge_count,
			selected_edges,
			"undo",
			undo_ms):
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
		if not validate_presentation_phase_metrics(
			renderer,
			before_redo_sequence + 1,
			edge_count,
			selected_edges,
			"redo",
			redo_ms):
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

func validate_presentation_phase_metrics(
	renderer: Node,
	expected_change_sequence: int,
	edge_count: int,
	changed_edges: int,
	operation: String,
	observed_ms: float) -> bool:
	var metrics: Dictionary = renderer.GetLastPresentationPerformanceMetrics()
	if not require(not metrics.is_empty(), "%s presentation phase metrics were not recorded" % operation):
		return false
	var render_token: Dictionary = metrics.get("renderToken", {})
	var presented_token: Dictionary = renderer.GetPresentationState().get("presented", {})
	if not require(
		render_token == presented_token and
		int(render_token.get("changeSequence", -1)) == expected_change_sequence,
		"%s presentation phase metrics used a stale render token" % operation):
		return false
	if not require(not bool(metrics.get("isFullReset", true)), "%s was misclassified as a full reset" % operation):
		return false
	if not require(int(metrics.get("attemptNumber", 0)) > 0, "%s presentation attempt number was not recorded" % operation):
		return false

	var snapshot_capture_ms := float(metrics.get("snapshotCaptureMs", -1.0))
	var prepare_ms := float(metrics.get("prepareMs", -1.0))
	var resource_preflight_ms := float(metrics.get("resourcePreflightMs", -1.0))
	var presentation_commit_ms := float(metrics.get("presentationCommitMs", -1.0))
	var rebuild_total_ms := float(metrics.get("rebuildTotalMs", -1.0))
	var request_to_ready_ms := float(metrics.get("requestToReadyMs", -1.0))
	var phase_sum_ms := (
		snapshot_capture_ms +
		prepare_ms +
		resource_preflight_ms +
		presentation_commit_ms)
	if not require(
		snapshot_capture_ms >= 0.0 and
		prepare_ms >= 0.0 and
		resource_preflight_ms >= 0.0 and
		presentation_commit_ms >= 0.0 and
		phase_sum_ms <= rebuild_total_ms and
		rebuild_total_ms <= request_to_ready_ms,
		"%s presentation phase timing was inconsistent: %s" % [operation, JSON.stringify(metrics)]):
		return false

	print("PRESENTATION_PHASE_RESULT %s" % JSON.stringify({
		"dataset": dataset_kind,
		"edges": edge_count,
		"changed_edges": changed_edges,
		"operation": operation,
		"snapshot_capture_ms": snappedf(snapshot_capture_ms, 0.001),
		"prepare_ms": snappedf(prepare_ms, 0.001),
		"resource_preflight_ms": snappedf(resource_preflight_ms, 0.001),
		"presentation_commit_ms": snappedf(presentation_commit_ms, 0.001),
		"rebuild_total_ms": snappedf(rebuild_total_ms, 0.001),
		"request_to_ready_ms": snappedf(request_to_ready_ms, 0.001),
		"observed_ms": snappedf(observed_ms, 0.001),
		"attempt_number": int(metrics.get("attemptNumber", 0)),
		"render_token": render_token,
	}))
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

func read_requested_token_perturbation_kind() -> String:
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--measure-token-perturbation="):
			return argument.trim_prefix("--measure-token-perturbation=")
	return ""

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
