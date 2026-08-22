extends SceneTree

const MAP_SCENE := "res://Scenes/MapTest.tscn"
const UPDATE_TOKEN_FAILURE_PROBE_PATH := \
	"res://tests/godot/RoadRendererUpdateTokenFailureProbe.cs"
const TEST_SLOT_NAME := "Road render token runtime contract"
const V3_SAVE_FIXTURE := preload("res://tests/godot/v3_save_fixture.gd")
const TOKEN_KEYS := [
	"sceneGeneration",
	"graphFacadeID",
	"graphFacadeGeneration",
	"changeSequence",
	"roadStyleRevision",
	"renderRequestID",
]

var test_map: Node
var save_manager: Node
var slot_id := ""
var failure_cleanup_started := false
var mutated_style: Resource
var original_style_width := 0.0

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	var packed_map: PackedScene = load(MAP_SCENE)
	if not require(packed_map != null, "MapTest scene did not load"):
		return
	test_map = packed_map.instantiate()
	var autosave_controller: Node = test_map.get_node("AutosaveController")
	autosave_controller.set("AutosaveEnabled", false)
	root.add_child(test_map)
	current_scene = test_map
	await process_frame
	await process_frame
	autosave_controller.SetAutosaveEnabled(false)

	save_manager = root.get_node("SaveManager")
	var renderer: Node = test_map.get_node("RoadSystem/RoadRenderer")
	var initial := presentation_token(renderer, "Initial presentation")
	if initial.is_empty():
		return
	if not require(
		int(initial.sceneGeneration) == int(save_manager.get("SceneGeneration")),
		"Renderer did not consume SaveManager scene generation"):
		return

	var builder: Node = test_map.get_node("RoadSystem/RoadBuilder")
	if not require(builder.BeginPlace(Vector2(0.0, 0.0)), "Normal mutation did not begin"):
		return
	builder.UpdatePlace(Vector2(100.0, 0.0))
	if not require(builder.CommitPlace(Vector2(100.0, 0.0)), "Normal mutation did not commit"):
		return
	var pending_undo_count: int = builder.GetUndoEditCount()
	var pending_redo_count: int = builder.GetRedoEditCount()
	if not require(
		not builder.CanUndoLastEdit() and
		not builder.UndoLastEdit() and
		builder.GetUndoEditCount() == pending_undo_count and
		builder.GetRedoEditCount() == pending_redo_count,
		"Pending presentation admitted an undo command"):
		return
	if not require(
		not builder.BeginPlace(Vector2(0.0, 100.0)) and
		not builder.HasActivePlaceSession() and
		renderer.GetPreviewPointCount() == 0,
		"Pending presentation admitted a new placement session"):
		return
	await process_frame
	await process_frame
	var mutated := presentation_token(renderer, "Normal mutation")
	if mutated.is_empty() or not require_ordinary_change(initial, mutated):
		return
	if not require_surface_hit(renderer, Vector2(50.0, 0.0), mutated, "Normal mutation"):
		return
	if not require_terminal_cap_hit(renderer, Vector2(-5.0, 0.0), mutated, "Normal mutation"):
		return
	var recovered: Dictionary = await require_stalled_retry(renderer, builder, mutated)
	if recovered.is_empty():
		return
	var resource_recovered: Dictionary = await require_update_token_resource_failure(
		renderer,
		builder,
		recovered)
	if resource_recovered.is_empty():
		return
	recovered = resource_recovered
	var node_batch_recovered: Dictionary = await require_update_token_node_batch_factory_failure(
		renderer,
		builder,
		recovered)
	if node_batch_recovered.is_empty():
		return
	recovered = node_batch_recovered
	var road_mesh_recovered: Dictionary = await require_update_token_road_mesh_factory_failure(
		renderer,
		builder,
		recovered)
	if road_mesh_recovered.is_empty():
		return
	recovered = road_mesh_recovered
	var surface_snapshot_recovered: Dictionary = \
		await require_update_token_surface_snapshot_failure(renderer, builder, recovered)
	if surface_snapshot_recovered.is_empty():
		return
	recovered = surface_snapshot_recovered

	var removal_edge_count: int = renderer.GetRenderedEdgeCount()
	var removal_history_count: int = builder.GetUndoEditCount()
	if not require(
		builder.BeginRemove(Vector2(50.0, 0.0), false) and
		builder.GetRemovalSelectionCount() == 1,
		"Current surface did not admit a removal selection"):
		return
	if not require(renderer.RefreshRoadStyles(), "Explicit road style refresh did not present"):
		return
	var styled := presentation_token(renderer, "Style refresh")
	if styled.is_empty() or not require_style_change(recovered, styled):
		return
	if not require(
		not builder.ConfirmRemove(Vector2(50.0, 0.0)) and
		not builder.HasActiveRemoveSession() and
		renderer.GetRemovalPreviewEdgeCount() == 0 and
		renderer.GetRenderedEdgeCount() == removal_edge_count and
		builder.GetUndoEditCount() == removal_history_count,
		"A removal selection captured from the previous render token mutated the graph"):
		return
	if not require_surface_hit(renderer, Vector2(50.0, 0.0), styled, "Style refresh"):
		return
	if not require_terminal_cap_hit(renderer, Vector2(-5.0, 0.0), styled, "Style refresh"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save_as(save_manager, TEST_SLOT_NAME),
		"Render token fixture slot was not created"):
		return
	slot_id = str(save_manager.get("CurrentSlotID"))
	if not require(
		V3_SAVE_FIXTURE.publish_payload(slot_id, build_fixture()),
		"Render token fixture payload and manifest could not be published"):
		return
	if not require(
		builder.BeginPlace(Vector2(0.0, -200.0)),
		"Queued-continuation mutation did not begin"):
		return
	builder.UpdatePlace(Vector2(100.0, -200.0))
	if not require(
		builder.CommitPlace(Vector2(100.0, -200.0)),
		"Queued-continuation mutation did not commit"):
		return
	var queued_state: Dictionary = renderer.GetPresentationState()
	var queued_desired: Dictionary = queued_state.get("desired", {})
	if not require(
		queued_state.get("phase", "") == "pending" and
		not bool(queued_state.get("isReady", true)) and
		queued_state.get("presented", {}) == styled and
		queued_desired != styled,
		"Ordinary mutation did not leave a deferred presentation continuation"):
		return
	if not require_ordinary_change(styled, queued_desired):
		return

	var load_operation_token := str(save_manager.StartLoad(slot_id))
	if not require(
		not load_operation_token.is_empty(),
		"Queued-continuation Load did not return an operation token"):
		return
	var admitted := presentation_token(renderer, "Queued-continuation Load admission")
	if admitted.is_empty() or not require(
		admitted == queued_desired,
		"Load admission did not synchronously consume the queued current presentation"):
		return
	if not require(
		await V3_SAVE_FIXTURE.operation_succeeded(save_manager, load_operation_token),
		"Render token fixture did not load"):
		return
	await process_frame
	await process_frame
	var loaded := presentation_token(renderer, "Aggregate Load")
	if loaded.is_empty() or not require_load_change(admitted, loaded):
		return
	if not require_surface_hit(renderer, Vector2(-50.0, 100.0), loaded, "Aggregate Load"):
		return
	if not require_terminal_cap_hit(renderer, Vector2(-108.0, 100.0), loaded, "Aggregate Load"):
		return
	if not require_semantic_join_hit(renderer, Vector2(5.0, 95.0), loaded, "Aggregate Load"):
		return
	if not require_junction_patch_hit(renderer, Vector2(300.0, 100.0), loaded, "Aggregate Load"):
		return

	var consecutive_load_token := str(save_manager.StartLoad(slot_id))
	if not require(
		not consecutive_load_token.is_empty() and consecutive_load_token != load_operation_token,
		"Consecutive Load did not return a distinct operation token"):
		return
	if not require(
		await V3_SAVE_FIXTURE.operation_succeeded(save_manager, consecutive_load_token),
		"Consecutive render token fixture Load did not complete"):
		return
	await process_frame
	await process_frame
	var consecutively_loaded := presentation_token(renderer, "Consecutive aggregate Load")
	if consecutively_loaded.is_empty() or not require_load_change(loaded, consecutively_loaded):
		return
	if not require_surface_hit(
		renderer,
		Vector2(-50.0, 100.0),
		consecutively_loaded,
		"Consecutive aggregate Load"):
		return
	if not require_terminal_cap_hit(
		renderer,
		Vector2(-108.0, 100.0),
		consecutively_loaded,
		"Consecutive aggregate Load"):
		return
	if not require_semantic_join_hit(
		renderer,
		Vector2(5.0, 95.0),
		consecutively_loaded,
		"Consecutive aggregate Load"):
		return
	if not require_junction_patch_hit(
		renderer,
		Vector2(300.0, 100.0),
		consecutively_loaded,
		"Consecutive aggregate Load"):
		return

	if not require(
		await V3_SAVE_FIXTURE.delete_slot(save_manager, slot_id),
		"Render token fixture slot cleanup failed"):
		return
	slot_id = ""
	test_map.queue_free()
	await process_frame
	await process_frame
	if not require(
		save_manager.get("RegisteredSaveableCount") == 0,
		"Render token cleanup retained saveables"):
		return

	print("PASS road render token runtime contract")
	quit(0)

func presentation_token(renderer: Node, source: String) -> Dictionary:
	var state: Dictionary = renderer.GetPresentationState()
	if not require(bool(state.get("isReady", false)), "%s is not presentation-ready" % source):
		return {}
	if not require(
		state.get("phase", "") == "ready" and not bool(state.get("isStalled", true)),
		"%s did not report the ready presentation phase" % source):
		return {}
	var desired: Dictionary = state.get("desired", {})
	var presented: Dictionary = state.get("presented", {})
	if not require(desired == presented, "%s published mismatched desired/presented tokens" % source):
		return {}
	for key: String in TOKEN_KEYS:
		if not require(desired.has(key), "%s token is missing %s" % [source, key]):
			return {}
	if not require(
		int(desired.sceneGeneration) > 0 and
		int(desired.graphFacadeID) > 0 and
		int(desired.graphFacadeGeneration) > 0 and
		int(desired.changeSequence) >= 0 and
		int(desired.roadStyleRevision) > 0 and
		int(desired.renderRequestID) > 0,
		"%s token contains an invalid identity component" % source):
		return {}
	return desired

func require_stalled_retry(
	renderer: Node,
	builder: Node,
	before: Dictionary
) -> Dictionary:
	var retained_edge_count: int = renderer.GetRenderedEdgeCount()
	var retained_vertex_count: int = renderer.GetRoadMeshVertexCount()
	var retained_state: Dictionary = renderer.GetPresentationState()
	var retained_primitive_count := int(retained_state.get("surfacePrimitiveCount", 0))
	var config: Resource = renderer.get("Config")
	var styles: Array = config.get("RoadTypeStyles")
	if not require(not styles.is_empty(), "Road style array is empty"):
		return {}
	mutated_style = styles[0]
	original_style_width = float(mutated_style.get("Width"))
	mutated_style.set("Width", 0.0)

	if not require(builder.BeginPlace(Vector2(0.0, 200.0)), "Stalled mutation did not begin"):
		return {}
	builder.UpdatePlace(Vector2(100.0, 200.0))
	if not require(builder.CommitPlace(Vector2(100.0, 200.0)), "Stalled mutation did not commit"):
		return {}
	await process_frame
	await process_frame

	var stalled: Dictionary = renderer.GetPresentationState()
	var desired: Dictionary = stalled.get("desired", {})
	if not require(
		stalled.get("phase", "") == "stalled" and
		bool(stalled.get("isStalled", false)) and
		not bool(stalled.get("isReady", true)),
		"Failed ordinary presentation did not enter the stalled phase"):
		return {}
	if not require(
		desired != before and stalled.get("presented", {}) == before,
		"Stalled presentation did not retain the previous presented token"):
		return {}
	if not require_ordinary_change(before, desired):
		return {}
	if not require(
		stalled.get("stalledToken", {}) == desired and
		int(stalled.get("attemptCount", 0)) == 1 and
		not str(stalled.get("failureType", "")).is_empty() and
		not str(stalled.get("failureMessage", "")).is_empty(),
		"Stalled presentation did not expose its target and first failure"):
		return {}
	if not require(
		int(stalled.get("surfacePrimitiveCount", -1)) == 0 and
		int(stalled.get("retainedSurfacePrimitiveCount", -1)) == retained_primitive_count and
		renderer.GetRenderedEdgeCount() == retained_edge_count and
		renderer.GetRoadMeshVertexCount() == retained_vertex_count,
		"Failed presentation did not retain the previous complete render state"):
		return {}
	if not require(
		renderer.FindRoadSurfaceHit(Vector2(50.0, 0.0), 0.0).is_empty(),
		"Stalled presentation still returned a road surface hit"):
		return {}
	if not require(
		not builder.BeginRemove(Vector2(50.0, 0.0), false) and
		not builder.HasActiveRemoveSession(),
		"Stalled presentation admitted a removal session"):
		return {}
	if not require(
		not builder.BeginPlace(Vector2(0.0, 300.0)) and
		not builder.HasActivePlaceSession() and
		renderer.GetPreviewPointCount() == 0,
		"Stalled presentation admitted a placement session"):
		return {}
	var stalled_undo_count: int = builder.GetUndoEditCount()
	var stalled_redo_count: int = builder.GetRedoEditCount()
	if not require(
		not builder.CanUndoLastEdit() and
		not builder.UndoLastEdit() and
		builder.GetUndoEditCount() == stalled_undo_count and
		builder.GetRedoEditCount() == stalled_redo_count and
		renderer.GetPresentationState().get("desired", {}) == desired,
		"Stalled presentation admitted an undo command"):
		return {}

	if not require(
		not renderer.RetryRoadPresentation(),
		"Retry unexpectedly succeeded while the style remained invalid"):
		return {}
	var failed_retry: Dictionary = renderer.GetPresentationState()
	if not require(
		failed_retry.get("desired", {}) == desired and
		failed_retry.get("stalledToken", {}) == desired and
		int(failed_retry.get("attemptCount", 0)) == 2,
		"Failed retry changed identity or did not increment its attempt"):
		return {}

	restore_mutated_style()
	if not require(
		renderer.RetryRoadPresentation(),
		"Retry did not publish after the style was repaired"):
		return {}
	var recovered := presentation_token(renderer, "Recovered ordinary presentation")
	if recovered.is_empty():
		return {}
	if not require(
		recovered == desired and int(renderer.GetPresentationState().get("attemptCount", 0)) == 3,
		"Successful retry did not publish the same desired token"):
		return {}
	if not require(
		renderer.GetRenderedEdgeCount() == retained_edge_count + 1 and
		renderer.GetRoadMeshVertexCount() > retained_vertex_count,
		"Successful retry did not atomically publish the new graph presentation"):
		return {}
	if not require(
		not renderer.RetryRoadPresentation(),
		"Ready presentation accepted a redundant retry"):
		return {}

	var replay_edge_count: int = renderer.GetRenderedEdgeCount()
	var replay_undo_count: int = builder.GetUndoEditCount()
	var replay_redo_count: int = builder.GetRedoEditCount()
	if not require(
		builder.CanUndoLastEdit() and builder.UndoLastEdit(),
		"Current presentation did not admit undo"):
		return {}
	if not require(
		not builder.CanRedoLastEdit() and
		not builder.RedoLastEdit() and
		builder.GetUndoEditCount() == replay_undo_count - 1 and
		builder.GetRedoEditCount() == replay_redo_count + 1 and
		renderer.GetRenderedEdgeCount() == replay_edge_count,
		"Pending undo presentation admitted redo or changed retained rendering"):
		return {}
	await process_frame
	await process_frame
	var undone := presentation_token(renderer, "Undo presentation")
	if undone.is_empty() or not require_ordinary_change(recovered, undone):
		return {}
	if not require(
		renderer.GetRenderedEdgeCount() == replay_edge_count - 1 and
		builder.CanRedoLastEdit() and builder.RedoLastEdit(),
		"Presented undo did not admit redo"):
		return {}
	if not require(
		not builder.CanUndoLastEdit() and
		not builder.UndoLastEdit() and
		builder.GetUndoEditCount() == replay_undo_count and
		builder.GetRedoEditCount() == replay_redo_count and
		renderer.GetRenderedEdgeCount() == replay_edge_count - 1,
		"Pending redo presentation admitted undo or changed retained rendering"):
		return {}
	await process_frame
	await process_frame
	var replayed := presentation_token(renderer, "Redo presentation")
	if replayed.is_empty() or not require_ordinary_change(undone, replayed):
		return {}
	if not require(
		renderer.GetRenderedEdgeCount() == replay_edge_count,
		"Presented redo did not restore the rendered graph"):
		return {}
	recovered = replayed

	var admitted_edge_count: int = renderer.GetRenderedEdgeCount()
	var admitted_history_count: int = builder.GetUndoEditCount()
	if not require(
		builder.BeginPlace(Vector2(0.0, 400.0)),
		"Current presentation did not admit the token-supersession placement"):
		return {}
	builder.UpdatePlace(Vector2(100.0, 400.0))
	if not require(
		builder.HasActivePlaceSession() and renderer.GetPreviewPointCount() > 0,
		"Admitted placement did not retain its preview"):
		return {}
	if not require(
		renderer.RefreshRoadStyles(),
		"Valid style refresh did not publish a replacement token"):
		return {}
	if not require(
		not builder.ConfirmPlace(Vector2(100.0, 400.0)) and
		not builder.HasActivePlaceSession() and
		renderer.GetPreviewPointCount() == 0 and
		renderer.GetRenderedEdgeCount() == admitted_edge_count and
		builder.GetUndoEditCount() == admitted_history_count,
		"A placement captured from the previous render token mutated the graph"):
		return {}
	var refreshed := presentation_token(renderer, "Placement token supersession")
	if refreshed.is_empty() or not require_style_change(recovered, refreshed):
		return {}
	return refreshed

func restore_mutated_style() -> void:
	if mutated_style == null:
		return
	mutated_style.set("Width", original_style_width)
	mutated_style = null

func require_update_token_resource_failure(
	renderer: Node,
	builder: Node,
	before: Dictionary
) -> Dictionary:
	var probe_script: Script = load(UPDATE_TOKEN_FAILURE_PROBE_PATH)
	if not require(probe_script != null, "Debug update-token failure probe did not load"):
		return {}
	var probe: RefCounted = probe_script.new()
	if not require(probe != null, "Debug update-token failure probe did not instantiate"):
		return {}

	var retained_edge_count: int = renderer.GetRenderedEdgeCount()
	var retained_vertex_count: int = renderer.GetRoadMeshVertexCount()
	var retained_marker_count: int = renderer.GetNodeMarkerCount()
	var retained_state: Dictionary = renderer.GetPresentationState()
	var retained_primitive_count := int(retained_state.get("surfacePrimitiveCount", 0))
	var resource_count_before := int(probe.GetObjectResourceCount())
	var failure_message := str(probe.GetFailureMessage())
	probe.Arm(renderer)
	if not require(bool(probe.IsArmed()), "Update-token failure probe did not arm"):
		return {}

	if not require(
		builder.BeginPlace(Vector2(0.0, 500.0)),
		"Resource-preflight failure mutation did not begin"):
		return {}
	builder.UpdatePlace(Vector2(100.0, 500.0))
	if not require(
		builder.CommitPlace(Vector2(100.0, 500.0)),
		"Resource-preflight failure mutation did not commit"):
		return {}
	await process_frame
	await process_frame

	var stalled: Dictionary = renderer.GetPresentationState()
	var desired: Dictionary = stalled.get("desired", {})
	if not require(
		stalled.get("phase", "") == "stalled" and
		bool(stalled.get("isStalled", false)) and
		not bool(stalled.get("isReady", true)) and
		desired != before and
		stalled.get("presented", {}) == before and
		stalled.get("stalledToken", {}) == desired,
		"Resource-preflight failure did not stall the new desired token"):
		return {}
	if not require_ordinary_change(before, desired):
		return {}
	if not require(
		int(stalled.get("attemptCount", 0)) == 1 and
		str(stalled.get("failureType", "")) == "System.InvalidOperationException" and
		str(stalled.get("failureMessage", "")) == failure_message and
		int(probe.GetTriggerCount()) == 1 and
		not bool(probe.IsArmed()),
		"Resource-preflight failure was not reported as the one-shot first attempt"):
		return {}
	if not require(
		int(probe.GetObjectResourceCount()) == resource_count_before and
		int(stalled.get("surfacePrimitiveCount", -1)) == 0 and
		int(stalled.get("retainedSurfacePrimitiveCount", -1)) == retained_primitive_count and
		renderer.GetRenderedEdgeCount() == retained_edge_count and
		renderer.GetRoadMeshVertexCount() == retained_vertex_count and
		renderer.GetNodeMarkerCount() == retained_marker_count,
		"Resource-preflight failure leaked resources or replaced retained presentation"):
		return {}
	if not require(
		renderer.FindRoadSurfaceHit(Vector2(50.0, 0.0), 0.0).is_empty(),
		"Resource-preflight stalled token still exposed its retained surface"):
		return {}

	if not require(
		renderer.RetryRoadPresentation(),
		"Resource-preflight retry did not publish the same desired token"):
		return {}
	var recovered := presentation_token(renderer, "Resource-preflight retry")
	if recovered.is_empty():
		return {}
	if not require(
		recovered == desired and
		int(renderer.GetPresentationState().get("attemptCount", 0)) == 2 and
		int(probe.GetTriggerCount()) == 1 and
		renderer.GetRenderedEdgeCount() == retained_edge_count + 1 and
		renderer.GetRoadMeshVertexCount() > retained_vertex_count,
		"Resource-preflight retry did not atomically publish attempt two"):
		return {}
	if not require_surface_hit(
		renderer,
		Vector2(50.0, 500.0),
		recovered,
		"Resource-preflight retry"):
		return {}
	print(
		(
			"UPDATE_TOKEN_FAILURE_RESULT resource_before=%d resource_after=%d " +
			"trigger_count=%d stalled_attempt=1 recovered_attempt=2"
		) % [
			resource_count_before,
			int(probe.GetObjectResourceCount()),
			int(probe.GetTriggerCount()),
		])
	return recovered

func require_update_token_node_batch_factory_failure(
	renderer: Node,
	builder: Node,
	before: Dictionary
) -> Dictionary:
	var probe_script: Script = load(UPDATE_TOKEN_FAILURE_PROBE_PATH)
	if not require(probe_script != null, "Debug node-batch failure probe did not load"):
		return {}
	var probe: RefCounted = probe_script.new()
	if not require(probe != null, "Debug node-batch failure probe did not instantiate"):
		return {}

	var retained_edge_count: int = renderer.GetRenderedEdgeCount()
	var retained_vertex_count: int = renderer.GetRoadMeshVertexCount()
	var retained_marker_count: int = renderer.GetNodeMarkerCount()
	var retained_state: Dictionary = renderer.GetPresentationState()
	var retained_primitive_count := int(retained_state.get("surfacePrimitiveCount", 0))
	var resource_count_before := int(probe.GetObjectResourceCount())
	var failure_message := str(probe.GetNodeBatchFactoryFailureMessage())
	probe.ArmNodeBatchFactoryFailure(renderer)
	if not require(
		bool(probe.IsNodeBatchFactoryFailureArmed()),
		"Update-token node-batch failure probe did not arm"):
		return {}

	if not require(
		builder.BeginPlace(Vector2(0.0, 600.0)),
		"Node-batch factory failure mutation did not begin"):
		return {}
	builder.UpdatePlace(Vector2(100.0, 600.0))
	if not require(
		builder.CommitPlace(Vector2(100.0, 600.0)),
		"Node-batch factory failure mutation did not commit"):
		return {}
	await process_frame
	await process_frame

	var stalled: Dictionary = renderer.GetPresentationState()
	var desired: Dictionary = stalled.get("desired", {})
	if not require(
		stalled.get("phase", "") == "stalled" and
		bool(stalled.get("isStalled", false)) and
		not bool(stalled.get("isReady", true)) and
		desired != before and
		stalled.get("presented", {}) == before and
		stalled.get("stalledToken", {}) == desired,
		"Node-batch factory failure did not stall the new desired token"):
		return {}
	if not require_ordinary_change(before, desired):
		return {}
	if not require(
		int(stalled.get("attemptCount", 0)) == 1 and
		str(stalled.get("failureType", "")) == "System.InvalidOperationException" and
		str(stalled.get("failureMessage", "")) == failure_message and
		int(probe.GetNodeBatchFactoryFailureCount()) == 1 and
		int(probe.GetNodeBatchFactoryMarkerReadCount()) == 1 and
		not bool(probe.IsNodeBatchFactoryFailureArmed()),
		"Node-batch factory failure was not raised inside the one-shot first attempt"):
		return {}
	if not require(
		int(probe.GetObjectResourceCount()) == resource_count_before and
		int(stalled.get("surfacePrimitiveCount", -1)) == 0 and
		int(stalled.get("retainedSurfacePrimitiveCount", -1)) == retained_primitive_count and
		renderer.GetRenderedEdgeCount() == retained_edge_count and
		renderer.GetRoadMeshVertexCount() == retained_vertex_count and
		renderer.GetNodeMarkerCount() == retained_marker_count,
		"Node-batch factory failure leaked resources or replaced retained presentation"):
		return {}
	if not require(
		renderer.FindRoadSurfaceHit(Vector2(50.0, 0.0), 0.0).is_empty(),
		"Node-batch factory stalled token still exposed its retained surface"):
		return {}

	if not require(
		renderer.RetryRoadPresentation(),
		"Node-batch factory retry did not publish the same desired token"):
		return {}
	var recovered := presentation_token(renderer, "Node-batch factory retry")
	if recovered.is_empty():
		return {}
	if not require(
		recovered == desired and
		int(renderer.GetPresentationState().get("attemptCount", 0)) == 2 and
		int(probe.GetNodeBatchFactoryFailureCount()) == 1 and
		int(probe.GetNodeBatchFactoryMarkerReadCount()) == 1 and
		renderer.GetRenderedEdgeCount() == retained_edge_count + 1 and
		renderer.GetRoadMeshVertexCount() > retained_vertex_count and
		renderer.GetNodeMarkerCount() > retained_marker_count,
		"Node-batch factory retry did not atomically publish attempt two"):
		return {}
	if not require_surface_hit(
		renderer,
		Vector2(50.0, 600.0),
		recovered,
		"Node-batch factory retry"):
		return {}
	print(
		(
			"UPDATE_TOKEN_NODE_BATCH_FAILURE_RESULT resource_before=%d resource_after=%d " +
			"trigger_count=%d marker_read_count=%d stalled_attempt=1 recovered_attempt=2"
		) % [
			resource_count_before,
			int(probe.GetObjectResourceCount()),
			int(probe.GetNodeBatchFactoryFailureCount()),
			int(probe.GetNodeBatchFactoryMarkerReadCount()),
		])
	return recovered

func require_update_token_road_mesh_factory_failure(
	renderer: Node,
	builder: Node,
	before: Dictionary
) -> Dictionary:
	var probe_script: Script = load(UPDATE_TOKEN_FAILURE_PROBE_PATH)
	if not require(probe_script != null, "Debug road-mesh failure probe did not load"):
		return {}
	var probe: RefCounted = probe_script.new()
	if not require(probe != null, "Debug road-mesh failure probe did not instantiate"):
		return {}

	var retained_edge_count: int = renderer.GetRenderedEdgeCount()
	var retained_vertex_count: int = renderer.GetRoadMeshVertexCount()
	var retained_marker_count: int = renderer.GetNodeMarkerCount()
	var retained_state: Dictionary = renderer.GetPresentationState()
	var retained_primitive_count := int(retained_state.get("surfacePrimitiveCount", 0))
	var resource_count_before := int(probe.GetObjectResourceCount())
	var failure_message := str(probe.GetRoadMeshFactoryFailureMessage())
	probe.ArmRoadMeshFactoryFailure(renderer)
	if not require(
		bool(probe.IsRoadMeshFactoryFailureArmed()),
		"Update-token road-mesh failure probe did not arm"):
		return {}

	if not require(
		builder.BeginPlace(Vector2(0.0, 700.0)),
		"Road-mesh factory failure mutation did not begin"):
		return {}
	builder.UpdatePlace(Vector2(100.0, 700.0))
	if not require(
		builder.CommitPlace(Vector2(100.0, 700.0)),
		"Road-mesh factory failure mutation did not commit"):
		return {}
	await process_frame
	await process_frame

	var stalled: Dictionary = renderer.GetPresentationState()
	var desired: Dictionary = stalled.get("desired", {})
	if not require(
		stalled.get("phase", "") == "stalled" and
		bool(stalled.get("isStalled", false)) and
		not bool(stalled.get("isReady", true)) and
		desired != before and
		stalled.get("presented", {}) == before and
		stalled.get("stalledToken", {}) == desired,
		"Road-mesh factory failure did not stall the new desired token"):
		return {}
	if not require_ordinary_change(before, desired):
		return {}
	if not require(
		int(stalled.get("attemptCount", 0)) == 1 and
		str(stalled.get("failureType", "")) == "System.InvalidOperationException" and
		str(stalled.get("failureMessage", "")) == failure_message and
		int(probe.GetRoadMeshFactoryFailureCount()) == 1 and
		int(probe.GetRoadMeshFactoryIndexEnumerationCount()) == 1 and
		not bool(probe.IsRoadMeshFactoryFailureArmed()),
		"Road-mesh factory failure was not raised inside the one-shot first attempt"):
		return {}
	if not require(
		int(probe.GetObjectResourceCount()) == resource_count_before and
		int(stalled.get("surfacePrimitiveCount", -1)) == 0 and
		int(stalled.get("retainedSurfacePrimitiveCount", -1)) == retained_primitive_count and
		renderer.GetRenderedEdgeCount() == retained_edge_count and
		renderer.GetRoadMeshVertexCount() == retained_vertex_count and
		renderer.GetNodeMarkerCount() == retained_marker_count,
		"Road-mesh factory failure leaked resources or replaced retained presentation"):
		return {}
	if not require(
		renderer.FindRoadSurfaceHit(Vector2(50.0, 0.0), 0.0).is_empty(),
		"Road-mesh factory stalled token still exposed its retained surface"):
		return {}

	if not require(
		renderer.RetryRoadPresentation(),
		"Road-mesh factory retry did not publish the same desired token"):
		return {}
	var recovered := presentation_token(renderer, "Road-mesh factory retry")
	if recovered.is_empty():
		return {}
	if not require(
		recovered == desired and
		int(renderer.GetPresentationState().get("attemptCount", 0)) == 2 and
		int(probe.GetRoadMeshFactoryFailureCount()) == 1 and
		int(probe.GetRoadMeshFactoryIndexEnumerationCount()) == 1 and
		renderer.GetRenderedEdgeCount() == retained_edge_count + 1 and
		renderer.GetRoadMeshVertexCount() > retained_vertex_count and
		renderer.GetNodeMarkerCount() > retained_marker_count,
		"Road-mesh factory retry did not atomically publish attempt two"):
		return {}
	if not require_surface_hit(
		renderer,
		Vector2(50.0, 700.0),
		recovered,
		"Road-mesh factory retry"):
		return {}
	print(
		(
			"UPDATE_TOKEN_ROAD_MESH_FAILURE_RESULT resource_before=%d resource_after=%d " +
			"trigger_count=%d index_enumeration_count=%d " +
			"stalled_attempt=1 recovered_attempt=2"
		) % [
			resource_count_before,
			int(probe.GetObjectResourceCount()),
			int(probe.GetRoadMeshFactoryFailureCount()),
			int(probe.GetRoadMeshFactoryIndexEnumerationCount()),
		])
	return recovered

func require_update_token_surface_snapshot_failure(
	renderer: Node,
	builder: Node,
	before: Dictionary
) -> Dictionary:
	var probe_script: Script = load(UPDATE_TOKEN_FAILURE_PROBE_PATH)
	if not require(probe_script != null, "Debug surface-snapshot failure probe did not load"):
		return {}
	var probe: RefCounted = probe_script.new()
	if not require(probe != null, "Debug surface-snapshot failure probe did not instantiate"):
		return {}

	var retained_edge_count: int = renderer.GetRenderedEdgeCount()
	var retained_vertex_count: int = renderer.GetRoadMeshVertexCount()
	var retained_marker_count: int = renderer.GetNodeMarkerCount()
	var retained_state: Dictionary = renderer.GetPresentationState()
	var retained_primitive_count := int(retained_state.get("surfacePrimitiveCount", 0))
	var resource_count_before := int(probe.GetObjectResourceCount())
	var failure_message := str(probe.GetRoadSurfaceSnapshotFailureMessage())
	probe.ArmRoadSurfaceSnapshotFailure(renderer)
	if not require(
		bool(probe.IsRoadSurfaceSnapshotFailureArmed()),
		"Update-token surface-snapshot failure probe did not arm"):
		return {}

	if not require(
		builder.BeginPlace(Vector2(0.0, 800.0)),
		"Surface-snapshot failure mutation did not begin"):
		return {}
	builder.UpdatePlace(Vector2(100.0, 800.0))
	if not require(
		builder.CommitPlace(Vector2(100.0, 800.0)),
		"Surface-snapshot failure mutation did not commit"):
		return {}
	await process_frame
	await process_frame

	var stalled: Dictionary = renderer.GetPresentationState()
	var desired: Dictionary = stalled.get("desired", {})
	if not require(
		stalled.get("phase", "") == "stalled" and
		bool(stalled.get("isStalled", false)) and
		not bool(stalled.get("isReady", true)) and
		desired != before and
		stalled.get("presented", {}) == before and
		stalled.get("stalledToken", {}) == desired,
		"Surface-snapshot failure did not stall the new desired token"):
		return {}
	if not require_ordinary_change(before, desired):
		return {}
	if not require(
		int(stalled.get("attemptCount", 0)) == 1 and
		str(stalled.get("failureType", "")) == "System.ArgumentNullException" and
		str(stalled.get("failureMessage", "")) == failure_message and
		int(probe.GetRoadSurfaceSnapshotFailureCount()) == 1 and
		not bool(probe.IsRoadSurfaceSnapshotFailureArmed()),
		"Surface-snapshot constructor failure was not raised on the one-shot first attempt"):
		return {}
	if not require(
		int(probe.GetObjectResourceCount()) == resource_count_before and
		int(stalled.get("surfacePrimitiveCount", -1)) == 0 and
		int(stalled.get("retainedSurfacePrimitiveCount", -1)) == retained_primitive_count and
		renderer.GetRenderedEdgeCount() == retained_edge_count and
		renderer.GetRoadMeshVertexCount() == retained_vertex_count and
		renderer.GetNodeMarkerCount() == retained_marker_count,
		"Surface-snapshot failure leaked resources or replaced retained presentation"):
		return {}
	if not require(
		renderer.FindRoadSurfaceHit(Vector2(50.0, 0.0), 0.0).is_empty(),
		"Surface-snapshot stalled token still exposed its retained surface"):
		return {}

	if not require(
		renderer.RetryRoadPresentation(),
		"Surface-snapshot retry did not publish the same desired token"):
		return {}
	var recovered := presentation_token(renderer, "Surface-snapshot retry")
	if recovered.is_empty():
		return {}
	if not require(
		recovered == desired and
		int(renderer.GetPresentationState().get("attemptCount", 0)) == 2 and
		int(probe.GetRoadSurfaceSnapshotFailureCount()) == 1 and
		renderer.GetRenderedEdgeCount() == retained_edge_count + 1 and
		renderer.GetRoadMeshVertexCount() > retained_vertex_count and
		renderer.GetNodeMarkerCount() > retained_marker_count,
		"Surface-snapshot retry did not atomically publish attempt two"):
		return {}
	if not require_surface_hit(
		renderer,
		Vector2(50.0, 800.0),
		recovered,
		"Surface-snapshot retry"):
		return {}
	print(
		(
			"UPDATE_TOKEN_SURFACE_SNAPSHOT_FAILURE_RESULT " +
			"resource_before=%d resource_after=%d trigger_count=%d " +
			"stalled_attempt=1 recovered_attempt=2"
		) % [
			resource_count_before,
			int(probe.GetObjectResourceCount()),
			int(probe.GetRoadSurfaceSnapshotFailureCount()),
		])
	return recovered

func require_ordinary_change(before: Dictionary, after: Dictionary) -> bool:
	return (
		require_same(before, after, [
			"sceneGeneration",
			"graphFacadeID",
			"graphFacadeGeneration",
			"roadStyleRevision",
		], "Normal mutation") and
		require(
			int(after.changeSequence) == int(before.changeSequence) + 1,
			"Normal mutation did not advance ChangeSequence exactly once") and
		require(
			int(after.renderRequestID) == int(before.renderRequestID) + 1,
			"Normal mutation did not schedule exactly one render request"))

func require_style_change(before: Dictionary, after: Dictionary) -> bool:
	return (
		require_same(before, after, [
			"sceneGeneration",
			"graphFacadeID",
			"graphFacadeGeneration",
			"changeSequence",
		], "Style refresh") and
		require(
			int(after.roadStyleRevision) == int(before.roadStyleRevision) + 1,
			"Style refresh did not advance RoadStyleRevision exactly once") and
		require(
			int(after.renderRequestID) == int(before.renderRequestID) + 1,
			"Style refresh did not schedule exactly one render request"))

func require_load_change(before: Dictionary, after: Dictionary) -> bool:
	return (
		require_same(before, after, [
			"sceneGeneration",
			"graphFacadeID",
			"roadStyleRevision",
		], "Aggregate Load") and
		require(
			int(after.graphFacadeGeneration) == int(before.graphFacadeGeneration) + 1,
			"Aggregate Load did not advance GraphFacadeGeneration exactly once") and
		require(
			int(after.changeSequence) == int(before.changeSequence) + 1,
			"Aggregate Load did not advance ChangeSequence exactly once") and
		require(
			int(after.renderRequestID) == int(before.renderRequestID) + 1,
			"Aggregate Load did not reserve exactly one render request"))

func require_surface_hit(
	renderer: Node,
	position: Vector2,
	expected_token: Dictionary,
	source: String) -> bool:
	var state: Dictionary = renderer.GetPresentationState()
	if not require(
		int(state.get("surfacePrimitiveCount", 0)) > 0,
		"%s did not publish road surface primitives" % source):
		return false
	var hit: Dictionary = renderer.FindRoadSurfaceHit(position, 0.0)
	var location: Dictionary = hit.get("location", {})
	return (
		require(not hit.is_empty(), "%s did not return a visible surface hit" % source) and
		require(
			hit.get("ownerKind", "") == "EdgeRibbon",
			"%s returned a non-ribbon owner for the basic surface" % source) and
		require(
			float(hit.get("surfaceDistance", -1.0)) == 0.0,
			"%s returned a non-zero distance inside the visible surface" % source) and
		require(
			hit.get("renderToken", {}) == expected_token,
			"%s surface hit token did not match the presented token" % source) and
		require(not location.is_empty(), "%s surface hit did not return a canonical location" % source) and
		require(
			int(location.get("edgeID", -1)) == int(hit.get("edgeID", -2)),
			"%s surface location did not preserve its owner Edge" % source) and
		require(
			int(location.get("geometryIndex", -1)) >= 0,
			"%s surface location returned an invalid geometry index" % source) and
		require(
			float(location.get("parameter", -1.0)) >= 0.0 and
			float(location.get("parameter", 2.0)) <= 1.0,
			"%s surface location returned an invalid parameter" % source))

func require_terminal_cap_hit(
	renderer: Node,
	position: Vector2,
	expected_token: Dictionary,
	source: String) -> bool:
	var hit: Dictionary = renderer.FindRoadSurfaceHit(position, 0.0)
	var location: Dictionary = hit.get("location", {})
	return (
		require(not hit.is_empty(), "%s did not return a terminal cap hit" % source) and
		require(
			hit.get("ownerKind", "") == "TerminalCap",
			"%s returned the wrong terminal cap owner kind" % source) and
		require(
			hit.get("endpoint", "") == "A" and int(hit.get("nodeID", -1)) >= 0,
			"%s terminal cap did not preserve its Node incidence" % source) and
		require(
			float(hit.get("surfaceDistance", -1.0)) == 0.0,
			"%s returned a non-zero distance inside the terminal cap" % source) and
		require(
			hit.get("renderToken", {}) == expected_token,
			"%s terminal cap token did not match the presented token" % source) and
		require(not location.is_empty(), "%s terminal cap omitted its canonical location" % source) and
		require(
			int(location.get("edgeID", -1)) == int(hit.get("edgeID", -2)) and
			int(location.get("geometryIndex", -1)) == 0 and
			is_zero_approx(float(location.get("parameter", -1.0))),
			"%s terminal cap did not return the canonical Edge start" % source))

func require_semantic_join_hit(
	renderer: Node,
	position: Vector2,
	expected_token: Dictionary,
	source: String) -> bool:
	var state: Dictionary = renderer.GetPresentationState()
	var hit: Dictionary = renderer.FindRoadSurfaceHit(position, 0.0)
	var location: Dictionary = hit.get("location", {})
	return (
		require(
			int(state.get("surfacePrimitiveCount", 0)) == 21,
			"%s did not publish ribbon, cap, join, and junction primitives together" % source) and
		require(
			renderer.GetNodeMarkerCount() == 5,
			"%s rendered a semantic boundary or junction as a node marker" % source) and
		require(not hit.is_empty(), "%s did not return a semantic join hit" % source) and
		require(
			hit.get("ownerKind", "") == "SemanticJoin",
			"%s returned the wrong semantic join owner kind" % source) and
		require(
			hit.get("endpoint", "") == "A" and int(hit.get("nodeID", -1)) == 1,
			"%s semantic join did not preserve its Node incidence" % source) and
		require(
			hit.get("renderToken", {}) == expected_token,
			"%s semantic join token did not match the presented token" % source) and
		require(not location.is_empty(), "%s semantic join omitted its canonical location" % source) and
		require(
			int(location.get("edgeID", -1)) == int(hit.get("edgeID", -2)) and
			int(location.get("geometryIndex", -1)) == 0 and
			is_zero_approx(float(location.get("parameter", -1.0))),
			"%s semantic join did not return the canonical Edge endpoint" % source))

func require_junction_patch_hit(
	renderer: Node,
	junction_position: Vector2,
	expected_token: Dictionary,
	source: String) -> bool:
	var state: Dictionary = renderer.GetPresentationState()
	if not require(
		int(state.get("surfacePrimitiveCount", 0)) == 21,
		"%s did not publish the complete junction surface" % source):
		return false
	if not require(
		renderer.GetRoadMeshVertexCount() == 38,
		"%s published the wrong mixed junction mesh size" % source):
		return false
	if not require(
		renderer.GetNodeMarkerCount() == 5,
		"%s rendered the degree-three junction as a node marker" % source):
		return false

	for y in range(-40, 41):
		for x in range(-40, 41):
			var hit: Dictionary = renderer.FindRoadSurfaceHit(
				junction_position + Vector2(x, y),
				0.0)
			if hit.get("ownerKind", "") != "JunctionPatch" or int(hit.get("nodeID", -1)) != 5:
				continue
			var location: Dictionary = hit.get("location", {})
			return (
				require(
					int(hit.get("edgeID", -1)) in [9, 10, 11] and
					hit.get("endpoint", "") == "A",
					"%s junction patch did not preserve its primary incidence" % source) and
				require(
					float(hit.get("surfaceDistance", -1.0)) == 0.0,
					"%s returned a non-zero distance inside the junction patch" % source) and
				require(
					hit.get("renderToken", {}) == expected_token,
					"%s junction patch token did not match the presented token" % source) and
				require(not location.is_empty(), "%s junction patch omitted its location" % source) and
				require(
					int(location.get("edgeID", -1)) == int(hit.get("edgeID", -2)) and
					int(location.get("geometryIndex", -1)) == 0 and
					is_zero_approx(float(location.get("parameter", -1.0))),
					"%s junction patch did not return its canonical A endpoint" % source))

	return require(false, "%s did not return a JunctionPatch hit near the junction" % source)

func require_same(
	before: Dictionary,
	after: Dictionary,
	keys: Array,
	source: String) -> bool:
	for key: String in keys:
		if not require(
			int(after[key]) == int(before[key]),
			"%s unexpectedly changed %s" % [source, key]):
			return false
	return true

func build_fixture() -> Dictionary:
	return {
		"formatFamily": "simple-cities-v3",
		"payloadType": "road-network",
		"schemaVersion": 1,
		"nextID": 12,
		"nodes": [
			{"id": 0, "x": -100.0, "y": 100.0},
			{"id": 1, "x": 0.0, "y": 100.0},
			{"id": 2, "x": 0.0, "y": 200.0},
			{"id": 5, "x": 300.0, "y": 100.0},
			{"id": 6, "x": 400.0, "y": 100.0},
			{"id": 7, "x": 400.0, "y": 120.0},
			{"id": 8, "x": 200.0, "y": 100.0},
		],
		"edges": [
			{
				"id": 3,
				"nodeAID": 0,
				"nodeBID": 1,
				"roadType": "highway",
				"geometry": [{
					"version": 1,
					"kind": "line",
					"start": {"x": -100.0, "y": 100.0},
					"end": {"x": 0.0, "y": 100.0},
				}],
			},
			{
				"id": 4,
				"nodeAID": 1,
				"nodeBID": 2,
				"roadType": "street",
				"geometry": [{
					"version": 1,
					"kind": "line",
					"start": {"x": 0.0, "y": 100.0},
					"end": {"x": 0.0, "y": 200.0},
				}],
			},
			{
				"id": 9,
				"nodeAID": 5,
				"nodeBID": 6,
				"roadType": "dirt",
				"geometry": [{
					"version": 1,
					"kind": "line",
					"start": {"x": 300.0, "y": 100.0},
					"end": {"x": 400.0, "y": 100.0},
				}],
			},
			{
				"id": 10,
				"nodeAID": 5,
				"nodeBID": 7,
				"roadType": "highway",
				"geometry": [{
					"version": 1,
					"kind": "line",
					"start": {"x": 300.0, "y": 100.0},
					"end": {"x": 400.0, "y": 120.0},
				}],
			},
			{
				"id": 11,
				"nodeAID": 5,
				"nodeBID": 8,
				"roadType": "street",
				"geometry": [{
					"version": 1,
					"kind": "line",
					"start": {"x": 300.0, "y": 100.0},
					"end": {"x": 200.0, "y": 100.0},
				}],
			},
		],
	}

func require(condition: bool, message: String) -> bool:
	if condition:
		return true
	push_error(message)
	if not failure_cleanup_started:
		failure_cleanup_started = true
		cleanup_after_failure.call_deferred()
	return false

func cleanup_after_failure() -> void:
	restore_mutated_style()
	if save_manager != null and not slot_id.is_empty():
		await V3_SAVE_FIXTURE.delete_slot(save_manager, slot_id)
		slot_id = ""
	if test_map != null:
		test_map.queue_free()
	quit(1)
