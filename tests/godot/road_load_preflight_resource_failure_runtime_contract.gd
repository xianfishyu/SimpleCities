extends SceneTree

const MAP_SCENE := "res://Scenes/MapTest.tscn"
const PROBE_PATH := "res://tests/godot/RoadLoadPreflightResourceFailureProbe.cs"
const SOURCE_SLOT_NAME := "Road preflight resource source"
const ACTIVE_SLOT_NAME := "Road preflight resource active"
const V3_SAVE_FIXTURE := preload("res://tests/godot/v3_save_fixture.gd")
const RESULT_SUCCEEDED := 0
const RESULT_FAILED := 2
const PHASE_PREPARE := 3
const TOOL_ROAD := 1
const ROAD_TYPE_ARTERIAL := 2

var failed := false
var test_map: Node
var save_manager: Node
var probe: RefCounted
var source_slot_id := ""
var active_slot_id := ""

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	var packed_map: PackedScene = load(MAP_SCENE)
	if not require(packed_map != null, "MapTest scene did not load"):
		return
	test_map = packed_map.instantiate()
	test_map.get_node("AutosaveController").set("AutosaveEnabled", false)
	root.add_child(test_map)
	current_scene = test_map
	await process_frame
	await process_frame

	save_manager = root.get_node("SaveManager")
	var builder: Node = test_map.get_node("RoadSystem/RoadBuilder")
	var renderer: Node = test_map.get_node("RoadSystem/RoadRenderer")
	var tool_manager: Node = test_map.get_node("ToolManager")

	if not require(
		await V3_SAVE_FIXTURE.save_as(save_manager, SOURCE_SLOT_NAME),
		"Could not create the empty source slot"):
		return
	source_slot_id = str(save_manager.get("CurrentSlotID"))

	if not require(builder.BeginPlace(Vector2(200.0, 300.0)), "Active road did not begin"):
		return
	builder.UpdatePlace(Vector2(600.0, 300.0))
	if not require(builder.CommitPlace(Vector2(600.0, 300.0)), "Active road did not commit"):
		return
	await process_frame
	await process_frame
	if not require(
		renderer.GetRenderedEdgeCount() == 1 and
		renderer.GetRoadMeshVertexCount() > 0 and
		renderer.GetNodeMarkerCount() > 0,
		"Active road did not publish all retained presentation resources"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save_as(save_manager, ACTIVE_SLOT_NAME),
		"Could not create the active slot"):
		return
	active_slot_id = str(save_manager.get("CurrentSlotID"))
	var active_payload_before := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	if not require(not active_payload_before.is_empty(), "Active payload was not readable"):
		return

	tool_manager.set("CurrentTool", TOOL_ROAD)
	if not require(
		tool_manager.SetSelectedRoadType(ROAD_TYPE_ARTERIAL),
		"Active RoadType did not change before the preflight probes"):
		return
	if not require(builder.BeginPlace(Vector2(700.0, 400.0)), "Transient placement did not begin"):
		return
	if not require(
		builder.AddPlacePoint(Vector2(800.0, 400.0)),
		"Transient placement point was not added"):
		return

	var presentation_before: Dictionary = renderer.GetPresentationState().duplicate(true)
	var edge_count_before: int = renderer.GetRenderedEdgeCount()
	var vertex_count_before: int = renderer.GetRoadMeshVertexCount()
	var marker_count_before: int = renderer.GetNodeMarkerCount()
	var hit_before: Dictionary = renderer.FindRoadSurfaceHit(Vector2(400.0, 300.0), 0.0)
	var undo_count_before: int = builder.GetUndoEditCount()
	var redo_count_before: int = builder.GetRedoEditCount()
	var selected_road_type_before: int = int(tool_manager.GetSelectedRoadType())

	var probe_script: Script = load(PROBE_PATH)
	if not require(probe_script != null, "Debug resource failure probe did not load"):
		return
	probe = probe_script.new()
	if not require(probe != null, "Debug resource failure probe did not instantiate"):
		return
	var probe_result: Dictionary = probe.Run(renderer)

	if not require(
		bool(probe_result.get("failedAtSnapshot", false)) and
		str(probe_result.get("exceptionType", "")) == "ArgumentNullException" and
		int(probe_result.get("roadVertexCount", 0)) > 0 and
		int(probe_result.get("nodeMarkerCount", 0)) > 0,
		"RoadSurfaceSnapshot failure was not injected after both resource factories: %s" %
		JSON.stringify(probe_result)):
		return
	if not require(
		int(probe_result.get("resourceCountAfter", -1)) ==
		int(probe_result.get("resourceCountBefore", -2)) and
		bool(probe_result.get("roadMeshPreserved", false)) and
		bool(probe_result.get("nodeBatchPreserved", false)) and
		bool(probe_result.get("surfacePreserved", false)),
		"Failed preflight leaked resources or replaced retained presentation references: %s" %
		JSON.stringify(probe_result)):
		return

	var plan_disposal_result: Dictionary = probe.RunUncommittedPlanDisposal(renderer)
	if not require(
		bool(plan_disposal_result.get("planWasCurrent", false)) and
		bool(plan_disposal_result.get("planBecameStale", false)) and
		int(plan_disposal_result.get("roadVertexCount", 0)) > 0 and
		int(plan_disposal_result.get("nodeMarkerCount", 0)) > 0,
		"Uncommitted load plan was not created with both presentation resources: %s" %
		JSON.stringify(plan_disposal_result)):
		return
	if not require(
		int(plan_disposal_result.get("resourceCountAfter", -1)) ==
		int(plan_disposal_result.get("resourceCountBefore", -2)) and
		bool(plan_disposal_result.get("roadMeshPreserved", false)) and
		bool(plan_disposal_result.get("nodeBatchPreserved", false)) and
		bool(plan_disposal_result.get("surfacePreserved", false)),
		"Uncommitted load plan disposal leaked resources or replaced retained references: %s" %
		JSON.stringify(plan_disposal_result)):
		return

	var renderer_boundary_result: Dictionary = (
		probe.RunRendererCommitBoundaryGenerationMismatch(renderer))
	if not require(
		bool(renderer_boundary_result.get("failedWhileEnteringCommit", false)) and
		str(renderer_boundary_result.get("exceptionType", "")) ==
			"LoadPreflightInvalidException" and
		str(renderer_boundary_result.get("exceptionMessage", "")) ==
			"A load participant generation changed while entering commit." and
		bool(renderer_boundary_result.get("planWasCurrent", false)) and
		bool(renderer_boundary_result.get("planBecameStale", false)) and
		int(renderer_boundary_result.get("roadVertexCount", 0)) > 0 and
		int(renderer_boundary_result.get("nodeMarkerCount", 0)) > 0 and
		int(renderer_boundary_result.get("commitLeaseCount", -1)) == 1 and
		int(renderer_boundary_result.get("boundaryCount", -1)) == 1 and
		int(renderer_boundary_result.get("markCommittedCount", -1)) == 0,
		"Real renderer plan did not fail at the aggregate commit boundary: %s" %
		JSON.stringify(renderer_boundary_result)):
		return
	if not require(
		int(renderer_boundary_result.get("graphCommitCount", -1)) == 0 and
		int(renderer_boundary_result.get("toolCommitCount", -1)) == 0 and
		int(renderer_boundary_result.get("slotCommitCount", -1)) == 0 and
		int(renderer_boundary_result.get("graphDisposeCount", -1)) == 1 and
		int(renderer_boundary_result.get("toolDisposeCount", -1)) == 1 and
		int(renderer_boundary_result.get("slotDisposeCount", -1)) == 1 and
		bool(renderer_boundary_result.get("admissionReacquired", false)),
		"Renderer generation mismatch swapped or retained an aggregate companion plan: %s" %
		JSON.stringify(renderer_boundary_result)):
		return
	if not require(
		int(renderer_boundary_result.get("resourceCountAfter", -1)) ==
		int(renderer_boundary_result.get("resourceCountBefore", -2)) and
		bool(renderer_boundary_result.get("roadMeshPreserved", false)) and
		bool(renderer_boundary_result.get("nodeBatchPreserved", false)) and
		bool(renderer_boundary_result.get("surfacePreserved", false)) and
		bool(renderer_boundary_result.get("tokensPreserved", false)),
		"Renderer commit-boundary mismatch leaked resources or replaced presentation state: %s" %
		JSON.stringify(renderer_boundary_result)):
		return

	var tool_boundary_result: Dictionary = (
		probe.RunToolCommitBoundaryGenerationMismatch(tool_manager))
	if not require(
		bool(tool_boundary_result.get("failedWhileEnteringCommit", false)) and
		str(tool_boundary_result.get("exceptionType", "")) ==
			"LoadPreflightInvalidException" and
		str(tool_boundary_result.get("exceptionMessage", "")) ==
			"A load participant generation changed while entering commit." and
		bool(tool_boundary_result.get("planWasCurrent", false)) and
		bool(tool_boundary_result.get("planBecameStale", false)) and
		int(tool_boundary_result.get("commitLeaseCount", -1)) == 1 and
		int(tool_boundary_result.get("boundaryCount", -1)) == 1 and
		int(tool_boundary_result.get("markCommittedCount", -1)) == 0,
		"Real tool plan did not fail at the aggregate commit boundary: %s" %
		JSON.stringify(tool_boundary_result)):
		return
	if not require(
		int(tool_boundary_result.get("graphCommitCount", -1)) == 0 and
		int(tool_boundary_result.get("rendererCommitCount", -1)) == 0 and
		int(tool_boundary_result.get("slotCommitCount", -1)) == 0 and
		int(tool_boundary_result.get("graphDisposeCount", -1)) == 1 and
		int(tool_boundary_result.get("rendererDisposeCount", -1)) == 1 and
		int(tool_boundary_result.get("slotDisposeCount", -1)) == 1 and
		bool(tool_boundary_result.get("toolAdmissionReacquired", false)) and
		bool(tool_boundary_result.get("builderAdmissionReacquired", false)),
		"Tool generation mismatch swapped or retained an aggregate companion plan: %s" %
		JSON.stringify(tool_boundary_result)):
		return
	if not require(
		bool(tool_boundary_result.get("currentToolPreserved", false)) and
		bool(tool_boundary_result.get("selectedRoadTypePreserved", false)) and
		bool(tool_boundary_result.get("placementPreserved", false)) and
		bool(tool_boundary_result.get("fixedCornersPreserved", false)) and
		bool(tool_boundary_result.get("historyPreserved", false)) and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		int(tool_manager.GetSelectedRoadType()) == selected_road_type_before and
		builder.HasActivePlaceSession() and
		builder.GetFixedCornerCount() == 1 and
		builder.GetUndoEditCount() == undo_count_before and
		builder.GetRedoEditCount() == redo_count_before,
		"Tool commit-boundary mismatch changed active tool or RoadBuilder state: %s" %
		JSON.stringify(tool_boundary_result)):
		return

	var node_batch_failure_result: Dictionary = probe.RunNodeBatchFactoryFailure(renderer)
	if not require(
		bool(node_batch_failure_result.get("failedInsideFactory", false)) and
		bool(node_batch_failure_result.get("markerIndexerRead", false)) and
		str(node_batch_failure_result.get("exceptionType", "")) ==
			"InvalidOperationException",
		"CreateNodeBatch did not fail from its marker loop: %s" %
		JSON.stringify(node_batch_failure_result)):
		return
	if not require(
		int(node_batch_failure_result.get("resourceCountAfter", -1)) ==
		int(node_batch_failure_result.get("resourceCountBefore", -2)) and
		bool(node_batch_failure_result.get("roadMeshPreserved", false)) and
		bool(node_batch_failure_result.get("nodeBatchPreserved", false)) and
		bool(node_batch_failure_result.get("surfacePreserved", false)),
		"CreateNodeBatch failure leaked its MultiMesh or replaced retained references: %s" %
		JSON.stringify(node_batch_failure_result)):
		return

	var road_mesh_failure_result: Dictionary = probe.RunRoadMeshOwnershipFailure(renderer)
	if not require(
		bool(road_mesh_failure_result.get("failedInsideOwnershipBoundary", false)) and
		bool(road_mesh_failure_result.get("initializerEntered", false)) and
		str(road_mesh_failure_result.get("exceptionType", "")) ==
			"InvalidOperationException",
		"Road mesh ownership boundary did not receive the injected initializer failure: %s" %
		JSON.stringify(road_mesh_failure_result)):
		return
	if not require(
		int(road_mesh_failure_result.get("resourceCountAfter", -1)) ==
		int(road_mesh_failure_result.get("resourceCountBefore", -2)) and
		bool(road_mesh_failure_result.get("roadMeshPreserved", false)) and
		bool(road_mesh_failure_result.get("nodeBatchPreserved", false)) and
		bool(road_mesh_failure_result.get("surfacePreserved", false)),
		"Road mesh initialization failure leaked its ArrayMesh or replaced retained references: %s" %
		JSON.stringify(road_mesh_failure_result)):
		return

	var aggregate_road_mesh_resource_count_before := int(probe.GetObjectResourceCount())
	var aggregate_road_mesh_failure_message := str(
		probe.GetAggregateLoadRoadMeshFactoryFailureMessage())
	probe.ArmAggregateLoadRoadMeshFactoryFailure(renderer)
	if not require(
		bool(probe.IsAggregateLoadRoadMeshFactoryFailureArmed()),
		"Aggregate Load road-mesh factory failure probe did not arm"):
		return
	var aggregate_road_mesh_failed_load_result := await run_load(active_slot_id)
	if not require(
		int(aggregate_road_mesh_failed_load_result.get("resultKind", -1)) == RESULT_FAILED and
		not bool(aggregate_road_mesh_failed_load_result.get("committed", true)) and
		str(aggregate_road_mesh_failed_load_result.get("warnings", "")).is_empty() and
		str(aggregate_road_mesh_failed_load_result.get("error", "")) ==
			aggregate_road_mesh_failure_message,
		"Aggregate Load did not fail inside CreateRoadMesh before commit: %s" %
		JSON.stringify(aggregate_road_mesh_failed_load_result)):
		return
	var aggregate_road_mesh_resource_count_after := int(probe.GetObjectResourceCount())
	if not require(
		not bool(probe.IsAggregateLoadRoadMeshFactoryFailureArmed()) and
		int(probe.GetAggregateLoadRoadMeshFactoryFailureCount()) == 1 and
		int(probe.GetAggregateLoadRoadMeshFactoryIndexEnumerationCount()) == 1 and
		aggregate_road_mesh_resource_count_after ==
			aggregate_road_mesh_resource_count_before,
		"Aggregate road-mesh factory failure did not release its preflight resource: %s" %
		JSON.stringify({
			"armed": bool(probe.IsAggregateLoadRoadMeshFactoryFailureArmed()),
			"triggerCount": int(probe.GetAggregateLoadRoadMeshFactoryFailureCount()),
			"indexEnumerationCount": int(
				probe.GetAggregateLoadRoadMeshFactoryIndexEnumerationCount()),
			"resourceCountBefore": aggregate_road_mesh_resource_count_before,
			"resourceCountAfter": aggregate_road_mesh_resource_count_after,
		})):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		builder.HasActivePlaceSession() and
		builder.GetFixedCornerCount() == 1 and
		builder.GetUndoEditCount() == undo_count_before and
		builder.GetRedoEditCount() == redo_count_before and
		renderer.GetRenderedEdgeCount() == edge_count_before and
		renderer.GetRoadMeshVertexCount() == vertex_count_before and
		renderer.GetNodeMarkerCount() == marker_count_before and
		renderer.GetPresentationState() == presentation_before and
		renderer.FindRoadSurfaceHit(Vector2(400.0, 300.0), 0.0) == hit_before,
		"Aggregate road-mesh factory failure changed graph, tool, presentation, surface, token, or slot state"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, active_slot_id),
		"Could not recapture the active graph after aggregate road-mesh factory failure"):
		return
	var aggregate_road_mesh_payload_after := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	if not require(
		aggregate_road_mesh_payload_after == active_payload_before,
		"Aggregate road-mesh factory failure changed RoadGraph"):
		return

	var aggregate_node_batch_resource_count_before := int(probe.GetObjectResourceCount())
	var aggregate_node_batch_failure_message := str(
		probe.GetAggregateLoadNodeBatchFactoryFailureMessage())
	probe.ArmAggregateLoadNodeBatchFactoryFailure(renderer)
	if not require(
		bool(probe.IsAggregateLoadNodeBatchFactoryFailureArmed()),
		"Aggregate Load node-batch factory failure probe did not arm"):
		return
	var aggregate_node_batch_failed_load_result := await run_load(active_slot_id)
	if not require(
		int(aggregate_node_batch_failed_load_result.get("resultKind", -1)) == RESULT_FAILED and
		not bool(aggregate_node_batch_failed_load_result.get("committed", true)) and
		str(aggregate_node_batch_failed_load_result.get("warnings", "")).is_empty() and
		str(aggregate_node_batch_failed_load_result.get("error", "")) ==
			aggregate_node_batch_failure_message,
		"Aggregate Load did not fail inside CreateNodeBatch before commit: %s" %
		JSON.stringify(aggregate_node_batch_failed_load_result)):
		return
	var aggregate_node_batch_resource_count_after := int(probe.GetObjectResourceCount())
	if not require(
		not bool(probe.IsAggregateLoadNodeBatchFactoryFailureArmed()) and
		int(probe.GetAggregateLoadNodeBatchFactoryFailureCount()) == 1 and
		int(probe.GetAggregateLoadNodeBatchFactoryMarkerReadCount()) == 1 and
		aggregate_node_batch_resource_count_after ==
			aggregate_node_batch_resource_count_before,
		"Aggregate node-batch factory failure did not release both preflight resources: %s" %
		JSON.stringify({
			"armed": bool(probe.IsAggregateLoadNodeBatchFactoryFailureArmed()),
			"triggerCount": int(probe.GetAggregateLoadNodeBatchFactoryFailureCount()),
			"markerReadCount": int(probe.GetAggregateLoadNodeBatchFactoryMarkerReadCount()),
			"resourceCountBefore": aggregate_node_batch_resource_count_before,
			"resourceCountAfter": aggregate_node_batch_resource_count_after,
		})):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		builder.HasActivePlaceSession() and
		builder.GetFixedCornerCount() == 1 and
		builder.GetUndoEditCount() == undo_count_before and
		builder.GetRedoEditCount() == redo_count_before and
		renderer.GetRenderedEdgeCount() == edge_count_before and
		renderer.GetRoadMeshVertexCount() == vertex_count_before and
		renderer.GetNodeMarkerCount() == marker_count_before and
		renderer.GetPresentationState() == presentation_before and
		renderer.FindRoadSurfaceHit(Vector2(400.0, 300.0), 0.0) == hit_before,
		"Aggregate node-batch factory failure changed graph, tool, presentation, surface, token, or slot state"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, active_slot_id),
		"Could not recapture the active graph after aggregate node-batch factory failure"):
		return
	var aggregate_node_batch_payload_after := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	if not require(
		aggregate_node_batch_payload_after == active_payload_before,
		"Aggregate node-batch factory failure changed RoadGraph"):
		return

	var aggregate_reserved_token_resource_count_before := int(probe.GetObjectResourceCount())
	var aggregate_reserved_token_failure_message := str(
		probe.GetAggregateLoadReservedRenderTokenFailureMessage())
	probe.ArmAggregateLoadReservedRenderTokenFailure(renderer)
	if not require(
		bool(probe.IsAggregateLoadReservedRenderTokenFailureArmed()),
		"Aggregate Load reserved render-token failure probe did not arm"):
		return
	var aggregate_reserved_token_failed_load_result := await run_load(active_slot_id)
	if not require(
		int(aggregate_reserved_token_failed_load_result.get("resultKind", -1)) ==
			RESULT_FAILED and
		not bool(aggregate_reserved_token_failed_load_result.get("committed", true)) and
		str(aggregate_reserved_token_failed_load_result.get("warnings", "")).is_empty() and
		str(aggregate_reserved_token_failed_load_result.get("error", "")) ==
			aggregate_reserved_token_failure_message,
		"Aggregate Load did not fail inside reserved render-token creation: %s" %
		JSON.stringify(aggregate_reserved_token_failed_load_result)):
		return
	var aggregate_reserved_token_resource_count_after := int(probe.GetObjectResourceCount())
	if not require(
		not bool(probe.IsAggregateLoadReservedRenderTokenFailureArmed()) and
		int(probe.GetAggregateLoadReservedRenderTokenFailureCount()) == 1 and
		aggregate_reserved_token_resource_count_after ==
			aggregate_reserved_token_resource_count_before,
		"Aggregate reserved render-token failure did not release both preflight resources: %s" %
		JSON.stringify({
			"armed": bool(probe.IsAggregateLoadReservedRenderTokenFailureArmed()),
			"triggerCount": int(probe.GetAggregateLoadReservedRenderTokenFailureCount()),
			"resourceCountBefore": aggregate_reserved_token_resource_count_before,
			"resourceCountAfter": aggregate_reserved_token_resource_count_after,
		})):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		builder.HasActivePlaceSession() and
		builder.GetFixedCornerCount() == 1 and
		builder.GetUndoEditCount() == undo_count_before and
		builder.GetRedoEditCount() == redo_count_before and
		renderer.GetRenderedEdgeCount() == edge_count_before and
		renderer.GetRoadMeshVertexCount() == vertex_count_before and
		renderer.GetNodeMarkerCount() == marker_count_before and
		renderer.GetPresentationState() == presentation_before and
		renderer.FindRoadSurfaceHit(Vector2(400.0, 300.0), 0.0) == hit_before,
		"Aggregate reserved render-token failure changed graph, tool, presentation, surface, token, or slot state"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, active_slot_id),
		"Could not recapture the active graph after aggregate reserved render-token failure"):
		return
	var aggregate_reserved_token_payload_after := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	if not require(
		aggregate_reserved_token_payload_after == active_payload_before,
		"Aggregate reserved render-token failure changed RoadGraph"):
		return

	var aggregate_snapshot_resource_count_before := int(probe.GetObjectResourceCount())
	var aggregate_snapshot_failure_message := str(
		probe.GetAggregateLoadRoadSurfaceSnapshotFailureMessage())
	probe.ArmAggregateLoadRoadSurfaceSnapshotFailure(renderer)
	if not require(
		bool(probe.IsAggregateLoadRoadSurfaceSnapshotFailureArmed()),
		"Aggregate Load road-surface snapshot failure probe did not arm"):
		return
	var aggregate_snapshot_failed_load_result := await run_load(active_slot_id)
	if not require(
		int(aggregate_snapshot_failed_load_result.get("resultKind", -1)) == RESULT_FAILED and
		not bool(aggregate_snapshot_failed_load_result.get("committed", true)) and
		str(aggregate_snapshot_failed_load_result.get("warnings", "")).is_empty() and
		str(aggregate_snapshot_failed_load_result.get("error", "")) ==
			aggregate_snapshot_failure_message,
		"Aggregate Load did not fail inside RoadSurfaceSnapshot construction: %s" %
		JSON.stringify(aggregate_snapshot_failed_load_result)):
		return
	var aggregate_snapshot_resource_count_after := int(probe.GetObjectResourceCount())
	if not require(
		not bool(probe.IsAggregateLoadRoadSurfaceSnapshotFailureArmed()) and
		int(probe.GetAggregateLoadRoadSurfaceSnapshotFailureCount()) == 1 and
		aggregate_snapshot_resource_count_after == aggregate_snapshot_resource_count_before,
		"Aggregate snapshot construction failure did not release both preflight resources: %s" %
		JSON.stringify({
			"armed": bool(probe.IsAggregateLoadRoadSurfaceSnapshotFailureArmed()),
			"triggerCount": int(probe.GetAggregateLoadRoadSurfaceSnapshotFailureCount()),
			"resourceCountBefore": aggregate_snapshot_resource_count_before,
			"resourceCountAfter": aggregate_snapshot_resource_count_after,
		})):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		builder.HasActivePlaceSession() and
		builder.GetFixedCornerCount() == 1 and
		builder.GetUndoEditCount() == undo_count_before and
		builder.GetRedoEditCount() == redo_count_before and
		renderer.GetRenderedEdgeCount() == edge_count_before and
		renderer.GetRoadMeshVertexCount() == vertex_count_before and
		renderer.GetNodeMarkerCount() == marker_count_before and
		renderer.GetPresentationState() == presentation_before and
		renderer.FindRoadSurfaceHit(Vector2(400.0, 300.0), 0.0) == hit_before,
		"Aggregate snapshot construction failure changed graph, tool, presentation, surface, token, or slot state"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, active_slot_id),
		"Could not recapture the active graph after aggregate snapshot construction failure"):
		return
	var aggregate_snapshot_payload_after := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	if not require(
		aggregate_snapshot_payload_after == active_payload_before,
		"Aggregate snapshot construction failure changed RoadGraph"):
		return

	var aggregate_commit_plan_resource_count_before := int(probe.GetObjectResourceCount())
	var aggregate_commit_plan_failure_message := str(
		probe.GetAggregateLoadCommitPlanConstructionFailureMessage())
	probe.ArmAggregateLoadCommitPlanConstructionFailure(renderer)
	if not require(
		bool(probe.IsAggregateLoadCommitPlanConstructionFailureArmed()),
		"Aggregate Load commit-plan construction failure probe did not arm"):
		return
	var aggregate_commit_plan_failed_load_result := await run_load(active_slot_id)
	if not require(
		int(aggregate_commit_plan_failed_load_result.get("resultKind", -1)) == RESULT_FAILED and
		not bool(aggregate_commit_plan_failed_load_result.get("committed", true)) and
		str(aggregate_commit_plan_failed_load_result.get("warnings", "")).is_empty() and
		str(aggregate_commit_plan_failed_load_result.get("error", "")) ==
			aggregate_commit_plan_failure_message,
		"Aggregate Load did not fail inside renderer commit-plan construction: %s" %
		JSON.stringify(aggregate_commit_plan_failed_load_result)):
		return
	var aggregate_commit_plan_resource_count_after := int(probe.GetObjectResourceCount())
	if not require(
		not bool(probe.IsAggregateLoadCommitPlanConstructionFailureArmed()) and
		int(probe.GetAggregateLoadCommitPlanConstructionFailureCount()) == 1 and
		aggregate_commit_plan_resource_count_after ==
			aggregate_commit_plan_resource_count_before,
		"Aggregate commit-plan construction failure did not release both preflight resources: %s" %
		JSON.stringify({
			"armed": bool(probe.IsAggregateLoadCommitPlanConstructionFailureArmed()),
			"triggerCount": int(probe.GetAggregateLoadCommitPlanConstructionFailureCount()),
			"resourceCountBefore": aggregate_commit_plan_resource_count_before,
			"resourceCountAfter": aggregate_commit_plan_resource_count_after,
		})):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		builder.HasActivePlaceSession() and
		builder.GetFixedCornerCount() == 1 and
		builder.GetUndoEditCount() == undo_count_before and
		builder.GetRedoEditCount() == redo_count_before and
		renderer.GetRenderedEdgeCount() == edge_count_before and
		renderer.GetRoadMeshVertexCount() == vertex_count_before and
		renderer.GetNodeMarkerCount() == marker_count_before and
		renderer.GetPresentationState() == presentation_before and
		renderer.FindRoadSurfaceHit(Vector2(400.0, 300.0), 0.0) == hit_before,
		"Aggregate commit-plan construction failure changed graph, tool, presentation, surface, token, or slot state"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, active_slot_id),
		"Could not recapture the active graph after aggregate commit-plan construction failure"):
		return
	var aggregate_commit_plan_payload_after := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	if not require(
		aggregate_commit_plan_payload_after == active_payload_before,
		"Aggregate commit-plan construction failure changed RoadGraph"):
		return

	var aggregate_resource_count_before := int(probe.GetObjectResourceCount())
	var aggregate_failure_message := str(
		probe.GetAggregateLoadResourcePreflightFailureMessage())
	probe.ArmAggregateLoadResourcePreflightFailure(renderer)
	if not require(
		bool(probe.IsAggregateLoadResourcePreflightFailureArmed()),
		"Aggregate Load resource preflight failure probe did not arm"):
		return
	var failed_load_result := await run_load(source_slot_id)
	if not require(
		int(failed_load_result.get("resultKind", -1)) == RESULT_FAILED and
		not bool(failed_load_result.get("committed", true)) and
		str(failed_load_result.get("warnings", "")).is_empty() and
		str(failed_load_result.get("error", "")) == aggregate_failure_message,
		"Aggregate Load did not fail before commit at renderer resource preflight: %s" %
		JSON.stringify(failed_load_result)):
		return
	if not require(
		not bool(probe.IsAggregateLoadResourcePreflightFailureArmed()) and
		int(probe.GetAggregateLoadResourcePreflightFailureCount()) == 1 and
		int(probe.GetObjectResourceCount()) == aggregate_resource_count_before,
		"Failed aggregate Load did not release its resources and one-shot probe: %s" %
		JSON.stringify({
			"armed": bool(probe.IsAggregateLoadResourcePreflightFailureArmed()),
			"triggerCount": int(probe.GetAggregateLoadResourcePreflightFailureCount()),
			"resourceCountBefore": aggregate_resource_count_before,
			"resourceCountAfter": int(probe.GetObjectResourceCount()),
		})):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		builder.HasActivePlaceSession() and
		builder.GetFixedCornerCount() == 1 and
		builder.GetUndoEditCount() == undo_count_before and
		builder.GetRedoEditCount() == redo_count_before and
		renderer.GetRenderedEdgeCount() == edge_count_before and
		renderer.GetRoadMeshVertexCount() == vertex_count_before and
		renderer.GetNodeMarkerCount() == marker_count_before and
		renderer.GetPresentationState() == presentation_before and
		renderer.FindRoadSurfaceHit(Vector2(400.0, 300.0), 0.0) == hit_before,
		"Pre-commit resource failure changed graph, tool, presentation, surface, token, or slot state"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, active_slot_id),
		"Could not recapture the active graph after failed preflight"):
		return
	var active_payload_after := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	if not require(active_payload_after == active_payload_before, "Failed preflight changed RoadGraph"):
		return

	var post_renderer_resource_count_before := int(probe.GetObjectResourceCount())
	var post_renderer_failure_message := str(
		probe.GetAggregateLoadPostRendererPreflightFailureMessage())
	probe.ArmAggregateLoadPostRendererPreflightFailure(save_manager)
	if not require(
		bool(probe.IsAggregateLoadPostRendererPreflightFailureArmed()),
		"Post-renderer aggregate Load failure probe did not arm"):
		return
	var post_renderer_failed_load_result := await run_load(source_slot_id)
	if not require(
		int(post_renderer_failed_load_result.get("resultKind", -1)) == RESULT_FAILED and
		not bool(post_renderer_failed_load_result.get("committed", true)) and
		str(post_renderer_failed_load_result.get("warnings", "")).is_empty() and
		str(post_renderer_failed_load_result.get("error", "")) ==
			post_renderer_failure_message,
		"Aggregate Load did not fail after renderer plan creation: %s" %
		JSON.stringify(post_renderer_failed_load_result)):
		return
	var post_renderer_resource_count_after := int(probe.GetObjectResourceCount())
	if not require(
		not bool(probe.IsAggregateLoadPostRendererPreflightFailureArmed()) and
		int(probe.GetAggregateLoadPostRendererPreflightFailureCount()) == 1 and
		post_renderer_resource_count_after == post_renderer_resource_count_before,
		"Post-renderer aggregate failure did not dispose the prepared plan and resources: %s" %
		JSON.stringify({
			"armed": bool(probe.IsAggregateLoadPostRendererPreflightFailureArmed()),
			"triggerCount": int(probe.GetAggregateLoadPostRendererPreflightFailureCount()),
			"resourceCountBefore": post_renderer_resource_count_before,
			"resourceCountAfter": post_renderer_resource_count_after,
		})):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		builder.HasActivePlaceSession() and
		builder.GetFixedCornerCount() == 1 and
		builder.GetUndoEditCount() == undo_count_before and
		builder.GetRedoEditCount() == redo_count_before and
		renderer.GetRenderedEdgeCount() == edge_count_before and
		renderer.GetRoadMeshVertexCount() == vertex_count_before and
		renderer.GetNodeMarkerCount() == marker_count_before and
		renderer.GetPresentationState() == presentation_before and
		renderer.FindRoadSurfaceHit(Vector2(400.0, 300.0), 0.0) == hit_before,
		"Post-renderer aggregate failure changed graph, tool, presentation, surface, token, or slot state"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, active_slot_id),
		"Could not recapture the active graph after post-renderer aggregate failure"):
		return
	var post_renderer_payload_after := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	if not require(
		post_renderer_payload_after == active_payload_before,
		"Post-renderer aggregate failure changed RoadGraph"):
		return

	var post_slot_resource_count_before := int(probe.GetObjectResourceCount())
	var post_slot_failure_message := str(
		probe.GetAggregateLoadPostSlotPreflightFailureMessage())
	probe.ArmAggregateLoadPostSlotPreflightFailure(save_manager)
	if not require(
		bool(probe.IsAggregateLoadPostSlotPreflightFailureArmed()),
		"Post-slot aggregate Load failure probe did not arm"):
		return
	var post_slot_failed_load_result := await run_load(source_slot_id)
	if not require(
		int(post_slot_failed_load_result.get("resultKind", -1)) == RESULT_FAILED and
		not bool(post_slot_failed_load_result.get("committed", true)) and
		str(post_slot_failed_load_result.get("warnings", "")).is_empty() and
		str(post_slot_failed_load_result.get("error", "")) == post_slot_failure_message,
		"Aggregate Load did not fail after slot-target plan creation: %s" %
		JSON.stringify(post_slot_failed_load_result)):
		return
	var post_slot_resource_count_after := int(probe.GetObjectResourceCount())
	if not require(
		not bool(probe.IsAggregateLoadPostSlotPreflightFailureArmed()) and
		int(probe.GetAggregateLoadPostSlotPreflightFailureCount()) == 1 and
		post_slot_resource_count_after == post_slot_resource_count_before,
		"Post-slot aggregate failure did not dispose every prepared plan and resource: %s" %
		JSON.stringify({
			"armed": bool(probe.IsAggregateLoadPostSlotPreflightFailureArmed()),
			"triggerCount": int(probe.GetAggregateLoadPostSlotPreflightFailureCount()),
			"resourceCountBefore": post_slot_resource_count_before,
			"resourceCountAfter": post_slot_resource_count_after,
		})):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		builder.HasActivePlaceSession() and
		builder.GetFixedCornerCount() == 1 and
		builder.GetUndoEditCount() == undo_count_before and
		builder.GetRedoEditCount() == redo_count_before and
		renderer.GetRenderedEdgeCount() == edge_count_before and
		renderer.GetRoadMeshVertexCount() == vertex_count_before and
		renderer.GetNodeMarkerCount() == marker_count_before and
		renderer.GetPresentationState() == presentation_before and
		renderer.FindRoadSurfaceHit(Vector2(400.0, 300.0), 0.0) == hit_before,
		"Post-slot aggregate failure changed graph, tool, presentation, surface, token, or slot state"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, active_slot_id),
		"Could not recapture the active graph after post-slot aggregate failure"):
		return
	var post_slot_payload_after := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	if not require(
		post_slot_payload_after == active_payload_before,
		"Post-slot aggregate failure changed RoadGraph"):
		return

	var post_ownership_resource_count_before := int(probe.GetObjectResourceCount())
	var post_ownership_failure_message := str(
		probe.GetAggregateLoadPostOwnershipPreCommitFailureMessage())
	probe.ArmAggregateLoadPostOwnershipPreCommitFailure(save_manager)
	if not require(
		bool(probe.IsAggregateLoadPostOwnershipPreCommitFailureArmed()),
		"Post-ownership aggregate Load failure probe did not arm"):
		return
	var post_ownership_failed_load_result := await run_load(source_slot_id)
	if not require(
		int(post_ownership_failed_load_result.get("resultKind", -1)) == RESULT_FAILED and
		not bool(post_ownership_failed_load_result.get("committed", true)) and
		str(post_ownership_failed_load_result.get("warnings", "")).is_empty() and
		str(post_ownership_failed_load_result.get("error", "")) ==
			post_ownership_failure_message,
		"Aggregate Load did not fail after ownership transfer and before commit: %s" %
		JSON.stringify(post_ownership_failed_load_result)):
		return
	var post_ownership_resource_count_after := int(probe.GetObjectResourceCount())
	if not require(
		not bool(probe.IsAggregateLoadPostOwnershipPreCommitFailureArmed()) and
		int(probe.GetAggregateLoadPostOwnershipPreCommitFailureCount()) == 1 and
		post_ownership_resource_count_after == post_ownership_resource_count_before,
		"Post-ownership aggregate failure did not dispose every owned plan and resource: %s" %
		JSON.stringify({
			"armed": bool(probe.IsAggregateLoadPostOwnershipPreCommitFailureArmed()),
			"triggerCount": int(
				probe.GetAggregateLoadPostOwnershipPreCommitFailureCount()),
			"resourceCountBefore": post_ownership_resource_count_before,
			"resourceCountAfter": post_ownership_resource_count_after,
		})):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		builder.HasActivePlaceSession() and
		builder.GetFixedCornerCount() == 1 and
		builder.GetUndoEditCount() == undo_count_before and
		builder.GetRedoEditCount() == redo_count_before and
		renderer.GetRenderedEdgeCount() == edge_count_before and
		renderer.GetRoadMeshVertexCount() == vertex_count_before and
		renderer.GetNodeMarkerCount() == marker_count_before and
		renderer.GetPresentationState() == presentation_before and
		renderer.FindRoadSurfaceHit(Vector2(400.0, 300.0), 0.0) == hit_before,
		"Post-ownership aggregate failure changed graph, tool, presentation, surface, token, or slot state"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, active_slot_id),
		"Could not recapture the active graph after post-ownership aggregate failure"):
		return
	var post_ownership_payload_after := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	if not require(
		post_ownership_payload_after == active_payload_before,
		"Post-ownership aggregate failure changed RoadGraph"):
		return

	var graph_boundary_operation_resource_count_before := int(probe.GetObjectResourceCount())
	var graph_boundary_operation_failure_message := str(
		probe.GetAggregateLoadGraphCommitBoundaryGenerationMismatchMessage())
	probe.ArmAggregateLoadGraphCommitBoundaryGenerationMismatch(save_manager)
	if not require(
		bool(probe.IsAggregateLoadGraphCommitBoundaryGenerationMismatchArmed()),
		"Real StartLoad graph commit-boundary generation mismatch probe did not arm"):
		return
	var graph_boundary_operation_failed_load_result := await run_load(active_slot_id)
	if not require(
		int(graph_boundary_operation_failed_load_result.get("resultKind", -1)) ==
			RESULT_FAILED and
		not bool(graph_boundary_operation_failed_load_result.get("committed", true)) and
		str(graph_boundary_operation_failed_load_result.get("warnings", "")).is_empty() and
		str(graph_boundary_operation_failed_load_result.get("error", "")) ==
			graph_boundary_operation_failure_message,
		"Real StartLoad did not reject the stale graph inside the commit boundary: %s" %
		JSON.stringify(graph_boundary_operation_failed_load_result)):
		return
	var graph_boundary_operation_resource_count_after := int(probe.GetObjectResourceCount())
	if not require(
		not bool(probe.IsAggregateLoadGraphCommitBoundaryGenerationMismatchArmed()) and
		int(probe.GetAggregateLoadGraphCommitBoundaryGenerationMismatchCount()) == 1 and
		int(probe.GetAggregateLoadGraphCommitBoundaryCount()) == 1 and
		int(probe.GetAggregateLoadGraphMarkCommittedCount()) == 0 and
		graph_boundary_operation_resource_count_after ==
			graph_boundary_operation_resource_count_before,
		"Real StartLoad graph boundary rejection did not release owned resources exactly once: %s" %
		JSON.stringify({
			"armed": bool(
				probe.IsAggregateLoadGraphCommitBoundaryGenerationMismatchArmed()),
			"triggerCount": int(
				probe.GetAggregateLoadGraphCommitBoundaryGenerationMismatchCount()),
			"boundaryCount": int(probe.GetAggregateLoadGraphCommitBoundaryCount()),
			"markCommittedCount": int(probe.GetAggregateLoadGraphMarkCommittedCount()),
			"resourceCountBefore": graph_boundary_operation_resource_count_before,
			"resourceCountAfter": graph_boundary_operation_resource_count_after,
		})):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		int(tool_manager.GetSelectedRoadType()) == selected_road_type_before and
		builder.HasActivePlaceSession() and
		builder.GetFixedCornerCount() == 1 and
		builder.GetUndoEditCount() == undo_count_before and
		builder.GetRedoEditCount() == redo_count_before and
		renderer.GetRenderedEdgeCount() == edge_count_before and
		renderer.GetRoadMeshVertexCount() == vertex_count_before and
		renderer.GetNodeMarkerCount() == marker_count_before and
		renderer.GetPresentationState() == presentation_before and
		renderer.FindRoadSurfaceHit(Vector2(400.0, 300.0), 0.0) == hit_before,
		"Real StartLoad graph boundary rejection changed graph, tool, placement, history, presentation, surface, token, or slot state"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, active_slot_id),
		"Could not recapture the active graph after graph boundary rejection"):
		return
	var graph_boundary_operation_payload_after := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	if not require(
		graph_boundary_operation_payload_after == active_payload_before,
		"Real StartLoad graph boundary rejection changed RoadGraph"):
		return

	var renderer_boundary_operation_resource_count_before := int(probe.GetObjectResourceCount())
	var renderer_boundary_operation_failure_message := str(
		probe.GetAggregateLoadRendererCommitBoundaryGenerationMismatchMessage())
	probe.ArmAggregateLoadRendererCommitBoundaryGenerationMismatch(save_manager, renderer)
	if not require(
		bool(probe.IsAggregateLoadRendererCommitBoundaryGenerationMismatchArmed()),
		"Real StartLoad renderer commit-boundary generation mismatch probe did not arm"):
		return
	var renderer_boundary_operation_failed_load_result := await run_load(active_slot_id)
	if not require(
		int(renderer_boundary_operation_failed_load_result.get("resultKind", -1)) ==
			RESULT_FAILED and
		not bool(renderer_boundary_operation_failed_load_result.get("committed", true)) and
		str(renderer_boundary_operation_failed_load_result.get("warnings", "")).is_empty() and
		str(renderer_boundary_operation_failed_load_result.get("error", "")) ==
			renderer_boundary_operation_failure_message,
		"Real StartLoad did not reject the stale renderer inside the commit boundary: %s" %
		JSON.stringify(renderer_boundary_operation_failed_load_result)):
		return
	var renderer_boundary_operation_resource_count_after := int(probe.GetObjectResourceCount())
	if not require(
		not bool(probe.IsAggregateLoadRendererCommitBoundaryGenerationMismatchArmed()) and
		int(probe.GetAggregateLoadRendererCommitBoundaryGenerationMismatchCount()) == 1 and
		int(probe.GetAggregateLoadRendererCommitBoundaryCount()) == 1 and
		int(probe.GetAggregateLoadRendererMarkCommittedCount()) == 0 and
		renderer_boundary_operation_resource_count_after ==
			renderer_boundary_operation_resource_count_before,
		"Real StartLoad renderer boundary rejection did not release owned resources exactly once: %s" %
		JSON.stringify({
			"armed": bool(
				probe.IsAggregateLoadRendererCommitBoundaryGenerationMismatchArmed()),
			"triggerCount": int(
				probe.GetAggregateLoadRendererCommitBoundaryGenerationMismatchCount()),
			"boundaryCount": int(probe.GetAggregateLoadRendererCommitBoundaryCount()),
			"markCommittedCount": int(
				probe.GetAggregateLoadRendererMarkCommittedCount()),
			"resourceCountBefore": renderer_boundary_operation_resource_count_before,
			"resourceCountAfter": renderer_boundary_operation_resource_count_after,
		})):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		builder.HasActivePlaceSession() and
		builder.GetFixedCornerCount() == 1 and
		builder.GetUndoEditCount() == undo_count_before and
		builder.GetRedoEditCount() == redo_count_before and
		renderer.GetRenderedEdgeCount() == edge_count_before and
		renderer.GetRoadMeshVertexCount() == vertex_count_before and
		renderer.GetNodeMarkerCount() == marker_count_before and
		renderer.GetPresentationState() == presentation_before and
		renderer.FindRoadSurfaceHit(Vector2(400.0, 300.0), 0.0) == hit_before,
		"Real StartLoad renderer boundary rejection changed graph, tool, presentation, surface, token, or slot state"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, active_slot_id),
		"Could not recapture the active graph after renderer boundary rejection"):
		return
	var renderer_boundary_operation_payload_after := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	if not require(
		renderer_boundary_operation_payload_after == active_payload_before,
		"Real StartLoad renderer boundary rejection changed RoadGraph"):
		return

	var tool_boundary_operation_resource_count_before := int(probe.GetObjectResourceCount())
	var tool_boundary_operation_failure_message := str(
		probe.GetAggregateLoadToolCommitBoundaryGenerationMismatchMessage())
	probe.ArmAggregateLoadToolCommitBoundaryGenerationMismatch(save_manager, tool_manager)
	if not require(
		bool(probe.IsAggregateLoadToolCommitBoundaryGenerationMismatchArmed()),
		"Real StartLoad tool commit-boundary generation mismatch probe did not arm"):
		return
	var tool_boundary_operation_failed_load_result := await run_load(active_slot_id)
	if not require(
		int(tool_boundary_operation_failed_load_result.get("resultKind", -1)) ==
			RESULT_FAILED and
		not bool(tool_boundary_operation_failed_load_result.get("committed", true)) and
		str(tool_boundary_operation_failed_load_result.get("warnings", "")).is_empty() and
		str(tool_boundary_operation_failed_load_result.get("error", "")) ==
			tool_boundary_operation_failure_message,
		"Real StartLoad did not reject the stale tool inside the commit boundary: %s" %
		JSON.stringify(tool_boundary_operation_failed_load_result)):
		return
	var tool_boundary_operation_resource_count_after := int(probe.GetObjectResourceCount())
	if not require(
		not bool(probe.IsAggregateLoadToolCommitBoundaryGenerationMismatchArmed()) and
		int(probe.GetAggregateLoadToolCommitBoundaryGenerationMismatchCount()) == 1 and
		int(probe.GetAggregateLoadToolCommitBoundaryCount()) == 1 and
		int(probe.GetAggregateLoadToolMarkCommittedCount()) == 0 and
		tool_boundary_operation_resource_count_after ==
			tool_boundary_operation_resource_count_before,
		"Real StartLoad tool boundary rejection did not release owned resources exactly once: %s" %
		JSON.stringify({
			"armed": bool(
				probe.IsAggregateLoadToolCommitBoundaryGenerationMismatchArmed()),
			"triggerCount": int(
				probe.GetAggregateLoadToolCommitBoundaryGenerationMismatchCount()),
			"boundaryCount": int(probe.GetAggregateLoadToolCommitBoundaryCount()),
			"markCommittedCount": int(probe.GetAggregateLoadToolMarkCommittedCount()),
			"resourceCountBefore": tool_boundary_operation_resource_count_before,
			"resourceCountAfter": tool_boundary_operation_resource_count_after,
		})):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		int(tool_manager.GetSelectedRoadType()) == selected_road_type_before and
		builder.HasActivePlaceSession() and
		builder.GetFixedCornerCount() == 1 and
		builder.GetUndoEditCount() == undo_count_before and
		builder.GetRedoEditCount() == redo_count_before and
		renderer.GetRenderedEdgeCount() == edge_count_before and
		renderer.GetRoadMeshVertexCount() == vertex_count_before and
		renderer.GetNodeMarkerCount() == marker_count_before and
		renderer.GetPresentationState() == presentation_before and
		renderer.FindRoadSurfaceHit(Vector2(400.0, 300.0), 0.0) == hit_before,
		"Real StartLoad tool boundary rejection changed graph, tool, placement, history, presentation, surface, token, or slot state"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, active_slot_id),
		"Could not recapture the active graph after tool boundary rejection"):
		return
	var tool_boundary_operation_payload_after := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	if not require(
		tool_boundary_operation_payload_after == active_payload_before,
		"Real StartLoad tool boundary rejection changed RoadGraph"):
		return

	var slot_target_boundary_operation_resource_count_before := int(
		probe.GetObjectResourceCount())
	var slot_target_boundary_operation_failure_message := str(
		probe.GetAggregateLoadSlotTargetCommitBoundaryGenerationMismatchMessage())
	probe.ArmAggregateLoadSlotTargetCommitBoundaryGenerationMismatch(save_manager)
	if not require(
		bool(probe.IsAggregateLoadSlotTargetCommitBoundaryGenerationMismatchArmed()),
		"Real StartLoad slot-target commit-boundary generation mismatch probe did not arm"):
		return
	var slot_target_boundary_operation_failed_load_result := await run_load(active_slot_id)
	if not require(
		int(slot_target_boundary_operation_failed_load_result.get("resultKind", -1)) ==
			RESULT_FAILED and
		not bool(slot_target_boundary_operation_failed_load_result.get("committed", true)) and
		str(slot_target_boundary_operation_failed_load_result.get("warnings", "")).is_empty() and
		str(slot_target_boundary_operation_failed_load_result.get("error", "")) ==
			slot_target_boundary_operation_failure_message,
		"Real StartLoad did not reject the stale slot target inside the commit boundary: %s" %
		JSON.stringify(slot_target_boundary_operation_failed_load_result)):
		return
	var slot_target_boundary_operation_resource_count_after := int(
		probe.GetObjectResourceCount())
	if not require(
		not bool(probe.IsAggregateLoadSlotTargetCommitBoundaryGenerationMismatchArmed()) and
		int(probe.GetAggregateLoadSlotTargetCommitBoundaryGenerationMismatchCount()) == 1 and
		int(probe.GetAggregateLoadSlotTargetCommitBoundaryCount()) == 1 and
		int(probe.GetAggregateLoadSlotTargetMarkCommittedCount()) == 0 and
		slot_target_boundary_operation_resource_count_after ==
			slot_target_boundary_operation_resource_count_before,
		"Real StartLoad slot-target boundary rejection did not release owned resources exactly once: %s" %
		JSON.stringify({
			"armed": bool(
				probe.IsAggregateLoadSlotTargetCommitBoundaryGenerationMismatchArmed()),
			"triggerCount": int(
				probe.GetAggregateLoadSlotTargetCommitBoundaryGenerationMismatchCount()),
			"boundaryCount": int(probe.GetAggregateLoadSlotTargetCommitBoundaryCount()),
			"markCommittedCount": int(
				probe.GetAggregateLoadSlotTargetMarkCommittedCount()),
			"resourceCountBefore": slot_target_boundary_operation_resource_count_before,
			"resourceCountAfter": slot_target_boundary_operation_resource_count_after,
		})):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		int(tool_manager.GetSelectedRoadType()) == selected_road_type_before and
		builder.HasActivePlaceSession() and
		builder.GetFixedCornerCount() == 1 and
		builder.GetUndoEditCount() == undo_count_before and
		builder.GetRedoEditCount() == redo_count_before and
		renderer.GetRenderedEdgeCount() == edge_count_before and
		renderer.GetRoadMeshVertexCount() == vertex_count_before and
		renderer.GetNodeMarkerCount() == marker_count_before and
		renderer.GetPresentationState() == presentation_before and
		renderer.FindRoadSurfaceHit(Vector2(400.0, 300.0), 0.0) == hit_before,
		"Real StartLoad slot-target boundary rejection changed graph, tool, placement, history, presentation, surface, token, or slot state"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, active_slot_id),
		"Could not recapture the active graph after slot-target boundary rejection"):
		return
	var slot_target_boundary_operation_payload_after := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	if not require(
		slot_target_boundary_operation_payload_after == active_payload_before,
		"Real StartLoad slot-target boundary rejection changed RoadGraph"):
		return

	var renderer_config: Resource = renderer.get("Config")
	var road_type_styles: Array = renderer_config.get("RoadTypeStyles")
	if not require(
		road_type_styles.size() == 4,
		"Renderer admission style-snapshot fixture did not begin with four styles"):
		return
	var renderer_admission_style_snapshot_resource_count_before := int(
		probe.GetObjectResourceCount())
	var removed_road_type_style: Resource = road_type_styles[road_type_styles.size() - 1]
	road_type_styles.remove_at(road_type_styles.size() - 1)
	var invalid_style_validation: Dictionary = (
		renderer_config.GetRoadTypeStylesValidationResult())
	var renderer_admission_style_snapshot_failure_message := (
		"RoadConfig RoadTypeStyles are invalid: %s" %
		str(invalid_style_validation.get("error", "")))
	var renderer_admission_style_snapshot_failed_load_result := await run_load(source_slot_id)
	road_type_styles.push_back(removed_road_type_style)
	var restored_style_validation: Dictionary = (
		renderer_config.GetRoadTypeStylesValidationResult())
	var renderer_admission_style_snapshot_resource_count_after := int(
		probe.GetObjectResourceCount())
	if not require(
		not bool(invalid_style_validation.get("valid", true)) and
		bool(restored_style_validation.get("valid", false)) and
		int(renderer_admission_style_snapshot_failed_load_result.get(
			"resultKind", -1)) == RESULT_FAILED and
		not bool(renderer_admission_style_snapshot_failed_load_result.get(
			"committed", true)) and
		str(renderer_admission_style_snapshot_failed_load_result.get(
			"warnings", "")).is_empty() and
		str(renderer_admission_style_snapshot_failed_load_result.get("error", "")) ==
			renderer_admission_style_snapshot_failure_message,
		"Real StartLoad did not fail while capturing the renderer admission style snapshot: %s" %
		JSON.stringify(renderer_admission_style_snapshot_failed_load_result)):
		return
	if not require(
		renderer_admission_style_snapshot_resource_count_after ==
			renderer_admission_style_snapshot_resource_count_before and
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		int(tool_manager.GetSelectedRoadType()) == selected_road_type_before and
		builder.HasActivePlaceSession() and
		builder.GetFixedCornerCount() == 1 and
		builder.GetUndoEditCount() == undo_count_before and
		builder.GetRedoEditCount() == redo_count_before and
		renderer.GetRenderedEdgeCount() == edge_count_before and
		renderer.GetRoadMeshVertexCount() == vertex_count_before and
		renderer.GetNodeMarkerCount() == marker_count_before and
		renderer.GetPresentationState() == presentation_before and
		renderer.FindRoadSurfaceHit(Vector2(400.0, 300.0), 0.0) == hit_before,
		"Renderer admission style-snapshot failure changed resources, graph, tool, placement, history, presentation, surface, token, or slot state"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, active_slot_id),
		"Could not recapture the active graph after renderer admission style-snapshot failure"):
		return
	var renderer_admission_style_snapshot_payload_after := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	if not require(
		renderer_admission_style_snapshot_payload_after == active_payload_before,
		"Renderer admission style-snapshot failure changed RoadGraph"):
		return

	var renderer_admission_settings_resource_count_before := int(
		probe.GetObjectResourceCount())
	var original_curve_display_tolerance := float(
		renderer_config.get("CurveDisplayTolerance"))
	if not require(
		original_curve_display_tolerance > 0.0,
		"Renderer admission settings fixture began with an invalid curve tolerance"):
		return
	renderer_config.set("CurveDisplayTolerance", 0.0)
	var renderer_admission_settings_failed_load_result := await run_load(source_slot_id)
	renderer_config.set("CurveDisplayTolerance", original_curve_display_tolerance)
	var renderer_admission_settings_resource_count_after := int(
		probe.GetObjectResourceCount())
	if not require(
		int(renderer_admission_settings_failed_load_result.get("resultKind", -1)) ==
			RESULT_FAILED and
		not bool(renderer_admission_settings_failed_load_result.get("committed", true)) and
		str(renderer_admission_settings_failed_load_result.get("warnings", "")).is_empty() and
		str(renderer_admission_settings_failed_load_result.get("error", "")) ==
			"RoadRenderer curve tolerance is invalid.",
		"Real StartLoad did not fail while validating renderer admission settings: %s" %
		JSON.stringify(renderer_admission_settings_failed_load_result)):
		return
	if not require(
		float(renderer_config.get("CurveDisplayTolerance")) ==
			original_curve_display_tolerance and
		renderer_admission_settings_resource_count_after ==
			renderer_admission_settings_resource_count_before and
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		int(tool_manager.GetSelectedRoadType()) == selected_road_type_before and
		builder.HasActivePlaceSession() and
		builder.GetFixedCornerCount() == 1 and
		builder.GetUndoEditCount() == undo_count_before and
		builder.GetRedoEditCount() == redo_count_before and
		renderer.GetRenderedEdgeCount() == edge_count_before and
		renderer.GetRoadMeshVertexCount() == vertex_count_before and
		renderer.GetNodeMarkerCount() == marker_count_before and
		renderer.GetPresentationState() == presentation_before and
		renderer.FindRoadSurfaceHit(Vector2(400.0, 300.0), 0.0) == hit_before,
		"Renderer admission settings failure changed resources, graph, tool, placement, history, presentation, surface, token, or slot state"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, active_slot_id),
		"Could not recapture the active graph after renderer admission settings failure"):
		return
	var renderer_admission_settings_payload_after := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	if not require(
		renderer_admission_settings_payload_after == active_payload_before,
		"Renderer admission settings failure changed RoadGraph"):
		return

	var renderer_admission_construction_resource_count_before := int(
		probe.GetObjectResourceCount())
	var renderer_admission_construction_failure_message := str(
		probe.GetAggregateLoadRendererAdmissionConstructionFailureMessage())
	probe.ArmAggregateLoadRendererAdmissionConstructionFailure(renderer)
	if not require(
		bool(probe.IsAggregateLoadRendererAdmissionConstructionFailureArmed()),
		"Aggregate Load renderer admission construction failure probe did not arm"):
		return
	var renderer_admission_construction_failed_load_result := await run_load(source_slot_id)
	var renderer_admission_construction_resource_count_after := int(
		probe.GetObjectResourceCount())
	if not require(
		int(renderer_admission_construction_failed_load_result.get("resultKind", -1)) ==
			RESULT_FAILED and
		not bool(renderer_admission_construction_failed_load_result.get("committed", true)) and
		str(renderer_admission_construction_failed_load_result.get("warnings", "")).is_empty() and
		str(renderer_admission_construction_failed_load_result.get("error", "")) ==
			renderer_admission_construction_failure_message,
		"Real StartLoad did not fail after reserving renderer presentation and before admission construction: %s" %
		JSON.stringify(renderer_admission_construction_failed_load_result)):
		return
	if not require(
		not bool(probe.IsAggregateLoadRendererAdmissionConstructionFailureArmed()) and
		int(probe.GetAggregateLoadRendererAdmissionConstructionFailureCount()) == 1 and
		renderer_admission_construction_resource_count_after ==
			renderer_admission_construction_resource_count_before and
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		int(tool_manager.GetSelectedRoadType()) == selected_road_type_before and
		builder.HasActivePlaceSession() and
		builder.GetFixedCornerCount() == 1 and
		builder.GetUndoEditCount() == undo_count_before and
		builder.GetRedoEditCount() == redo_count_before and
		renderer.GetRenderedEdgeCount() == edge_count_before and
		renderer.GetRoadMeshVertexCount() == vertex_count_before and
		renderer.GetNodeMarkerCount() == marker_count_before and
		renderer.GetPresentationState() == presentation_before and
		renderer.FindRoadSurfaceHit(Vector2(400.0, 300.0), 0.0) == hit_before,
		"Renderer admission construction failure changed resources, graph, tool, placement, history, presentation, surface, token, or slot state"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, active_slot_id),
		"Could not recapture the active graph after renderer admission construction failure"):
		return
	var renderer_admission_construction_payload_after := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	if not require(
		renderer_admission_construction_payload_after == active_payload_before,
		"Renderer admission construction failure changed RoadGraph"):
		return

	var renderer_admission_publication_resource_count_before := int(
		probe.GetObjectResourceCount())
	var renderer_admission_publication_failure_message := str(
		probe.GetAggregateLoadRendererAdmissionPublicationFailureMessage())
	probe.ArmAggregateLoadRendererAdmissionPublicationFailure(renderer)
	if not require(
		bool(probe.IsAggregateLoadRendererAdmissionPublicationFailureArmed()),
		"Aggregate Load renderer admission publication failure probe did not arm"):
		return
	var renderer_admission_publication_failed_load_result := await run_load(source_slot_id)
	var renderer_admission_publication_resource_count_after := int(
		probe.GetObjectResourceCount())
	if not require(
		int(renderer_admission_publication_failed_load_result.get("resultKind", -1)) ==
			RESULT_FAILED and
		not bool(renderer_admission_publication_failed_load_result.get("committed", true)) and
		str(renderer_admission_publication_failed_load_result.get("warnings", "")).is_empty() and
		str(renderer_admission_publication_failed_load_result.get("error", "")) ==
			renderer_admission_publication_failure_message,
		"Real StartLoad did not fail after renderer admission construction and before publication: %s" %
		JSON.stringify(renderer_admission_publication_failed_load_result)):
		return
	if not require(
		not bool(probe.IsAggregateLoadRendererAdmissionPublicationFailureArmed()) and
		int(probe.GetAggregateLoadRendererAdmissionPublicationFailureCount()) == 1 and
		renderer_admission_publication_resource_count_after ==
			renderer_admission_publication_resource_count_before and
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		int(tool_manager.GetSelectedRoadType()) == selected_road_type_before and
		builder.HasActivePlaceSession() and
		builder.GetFixedCornerCount() == 1 and
		builder.GetUndoEditCount() == undo_count_before and
		builder.GetRedoEditCount() == redo_count_before and
		renderer.GetRenderedEdgeCount() == edge_count_before and
		renderer.GetRoadMeshVertexCount() == vertex_count_before and
		renderer.GetNodeMarkerCount() == marker_count_before and
		renderer.GetPresentationState() == presentation_before and
		renderer.FindRoadSurfaceHit(Vector2(400.0, 300.0), 0.0) == hit_before,
		"Renderer admission publication failure changed resources, graph, tool, placement, history, presentation, surface, token, or slot state"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, active_slot_id),
		"Could not recapture the active graph after renderer admission publication failure"):
		return
	var renderer_admission_publication_payload_after := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	if not require(
		renderer_admission_publication_payload_after == active_payload_before,
		"Renderer admission publication failure changed RoadGraph"):
		return

	var post_renderer_admission_resource_count_before := int(probe.GetObjectResourceCount())
	var post_renderer_admission_failure_message := str(
		probe.GetAggregateLoadPostRendererAdmissionFailureMessage())
	probe.ArmAggregateLoadPostRendererAdmissionFailure(save_manager)
	if not require(
		bool(probe.IsAggregateLoadPostRendererAdmissionFailureArmed()),
		"Aggregate Load post-renderer admission failure probe did not arm"):
		return
	var post_renderer_admission_failed_load_result := await run_load(source_slot_id)
	var post_renderer_admission_resource_count_after := int(probe.GetObjectResourceCount())
	if not require(
		int(post_renderer_admission_failed_load_result.get("resultKind", -1)) ==
			RESULT_FAILED and
		not bool(post_renderer_admission_failed_load_result.get("committed", true)) and
		str(post_renderer_admission_failed_load_result.get("warnings", "")).is_empty() and
		str(post_renderer_admission_failed_load_result.get("error", "")) ==
			post_renderer_admission_failure_message,
		"Real StartLoad did not fail after renderer admission publication and before worker start: %s" %
		JSON.stringify(post_renderer_admission_failed_load_result)):
		return
	if not require(
		not bool(probe.IsAggregateLoadPostRendererAdmissionFailureArmed()) and
		int(probe.GetAggregateLoadPostRendererAdmissionFailureCount()) == 1 and
		post_renderer_admission_resource_count_after ==
			post_renderer_admission_resource_count_before and
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		int(tool_manager.GetSelectedRoadType()) == selected_road_type_before and
		builder.HasActivePlaceSession() and
		builder.GetFixedCornerCount() == 1 and
		builder.GetUndoEditCount() == undo_count_before and
		builder.GetRedoEditCount() == redo_count_before and
		renderer.GetRenderedEdgeCount() == edge_count_before and
		renderer.GetRoadMeshVertexCount() == vertex_count_before and
		renderer.GetNodeMarkerCount() == marker_count_before and
		renderer.GetPresentationState() == presentation_before and
		renderer.FindRoadSurfaceHit(Vector2(400.0, 300.0), 0.0) == hit_before,
		"Post-renderer admission failure changed resources, graph, tool, placement, history, presentation, surface, token, or slot state"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, active_slot_id),
		"Could not recapture the active graph after post-renderer admission failure"):
		return
	var post_renderer_admission_payload_after := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	if not require(
		post_renderer_admission_payload_after == active_payload_before,
		"Post-renderer admission failure changed RoadGraph"):
		return

	var post_participant_capture_resource_count_before := int(probe.GetObjectResourceCount())
	var post_participant_capture_failure_message := str(
		probe.GetAggregateLoadPostParticipantCaptureFailureMessage())
	probe.ArmAggregateLoadPostParticipantCaptureFailure(save_manager)
	if not require(
		bool(probe.IsAggregateLoadPostParticipantCaptureFailureArmed()),
		"Aggregate Load post-participant capture failure probe did not arm"):
		return
	var post_participant_capture_failed_load_result := await run_load(source_slot_id)
	var post_participant_capture_resource_count_after := int(probe.GetObjectResourceCount())
	if not require(
		int(post_participant_capture_failed_load_result.get("resultKind", -1)) ==
			RESULT_FAILED and
		not bool(post_participant_capture_failed_load_result.get("committed", true)) and
		str(post_participant_capture_failed_load_result.get("warnings", "")).is_empty() and
		str(post_participant_capture_failed_load_result.get("error", "")) ==
			post_participant_capture_failure_message,
		"Real StartLoad did not fail after participant capture and before Prepare phase: %s" %
		JSON.stringify(post_participant_capture_failed_load_result)):
		return
	if not require(
		not bool(probe.IsAggregateLoadPostParticipantCaptureFailureArmed()) and
		int(probe.GetAggregateLoadPostParticipantCaptureFailureCount()) == 1 and
		int(probe.GetAggregateLoadPostParticipantCaptureParticipantCount()) == 1 and
		post_participant_capture_resource_count_after ==
			post_participant_capture_resource_count_before and
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		int(tool_manager.GetSelectedRoadType()) == selected_road_type_before and
		builder.HasActivePlaceSession() and
		builder.GetFixedCornerCount() == 1 and
		builder.GetUndoEditCount() == undo_count_before and
		builder.GetRedoEditCount() == redo_count_before and
		renderer.GetRenderedEdgeCount() == edge_count_before and
		renderer.GetRoadMeshVertexCount() == vertex_count_before and
		renderer.GetNodeMarkerCount() == marker_count_before and
		renderer.GetPresentationState() == presentation_before and
		renderer.FindRoadSurfaceHit(Vector2(400.0, 300.0), 0.0) == hit_before,
		"Post-participant capture failure changed resources, graph, tool, placement, history, presentation, surface, token, or slot state"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, active_slot_id),
		"Could not recapture the active graph after post-participant capture failure"):
		return
	var post_participant_capture_payload_after := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	if not require(
		post_participant_capture_payload_after == active_payload_before,
		"Post-participant capture failure changed RoadGraph"):
		return

	var post_prepare_phase_resource_count_before := int(probe.GetObjectResourceCount())
	var post_prepare_phase_failure_message := str(
		probe.GetAggregateLoadPostPreparePhaseFailureMessage())
	probe.ArmAggregateLoadPostPreparePhaseFailure(save_manager)
	if not require(
		bool(probe.IsAggregateLoadPostPreparePhaseFailureArmed()),
		"Aggregate Load post-Prepare phase failure probe did not arm"):
		return
	var post_prepare_phase_failed_load_result := await run_load(source_slot_id)
	var post_prepare_phase_resource_count_after := int(probe.GetObjectResourceCount())
	if not require(
		int(post_prepare_phase_failed_load_result.get("resultKind", -1)) ==
			RESULT_FAILED and
		int(post_prepare_phase_failed_load_result.get("finalPhase", -1)) ==
			PHASE_PREPARE and
		not bool(post_prepare_phase_failed_load_result.get("committed", true)) and
		str(post_prepare_phase_failed_load_result.get("warnings", "")).is_empty() and
		str(post_prepare_phase_failed_load_result.get("error", "")) ==
			post_prepare_phase_failure_message,
		"Real StartLoad did not fail after entering Prepare phase and before worker start: %s" %
		JSON.stringify(post_prepare_phase_failed_load_result)):
		return
	if not require(
		not bool(probe.IsAggregateLoadPostPreparePhaseFailureArmed()) and
		int(probe.GetAggregateLoadPostPreparePhaseFailureCount()) == 1 and
		int(probe.GetAggregateLoadPostPreparePhaseObservedPhase()) == PHASE_PREPARE and
		post_prepare_phase_resource_count_after ==
			post_prepare_phase_resource_count_before and
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		int(tool_manager.GetSelectedRoadType()) == selected_road_type_before and
		builder.HasActivePlaceSession() and
		builder.GetFixedCornerCount() == 1 and
		builder.GetUndoEditCount() == undo_count_before and
		builder.GetRedoEditCount() == redo_count_before and
		renderer.GetRenderedEdgeCount() == edge_count_before and
		renderer.GetRoadMeshVertexCount() == vertex_count_before and
		renderer.GetNodeMarkerCount() == marker_count_before and
		renderer.GetPresentationState() == presentation_before and
		renderer.FindRoadSurfaceHit(Vector2(400.0, 300.0), 0.0) == hit_before,
		"Post-Prepare phase failure changed resources, graph, tool, placement, history, presentation, surface, token, or slot state"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, active_slot_id),
		"Could not recapture the active graph after post-Prepare phase failure"):
		return
	var post_prepare_phase_payload_after := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	if not require(
		post_prepare_phase_payload_after == active_payload_before,
		"Post-Prepare phase failure changed RoadGraph"):
		return

	var worker_entry_resource_count_before := int(probe.GetObjectResourceCount())
	var worker_entry_failure_message := str(
		probe.GetAggregateLoadWorkerEntryFailureMessage())
	probe.ArmAggregateLoadWorkerEntryFailure(save_manager)
	if not require(
		bool(probe.IsAggregateLoadWorkerEntryFailureArmed()),
		"Aggregate Load worker-entry failure probe did not arm"):
		return
	var worker_entry_failed_load_result := await run_load(source_slot_id)
	var worker_entry_resource_count_after := int(probe.GetObjectResourceCount())
	if not require(
		int(worker_entry_failed_load_result.get("resultKind", -1)) == RESULT_FAILED and
		int(worker_entry_failed_load_result.get("finalPhase", -1)) == PHASE_PREPARE and
		not bool(worker_entry_failed_load_result.get("committed", true)) and
		str(worker_entry_failed_load_result.get("warnings", "")).is_empty() and
		str(worker_entry_failed_load_result.get("error", "")) ==
			worker_entry_failure_message,
		"Real StartLoad did not fail inside the worker before slot preparation: %s" %
		JSON.stringify(worker_entry_failed_load_result)):
		return
	if not require(
		not bool(probe.IsAggregateLoadWorkerEntryFailureArmed()) and
		int(probe.GetAggregateLoadWorkerEntryFailureCount()) == 1 and
		bool(probe.DidAggregateLoadWorkerEntryFailureRunOffMainThread()) and
		worker_entry_resource_count_after == worker_entry_resource_count_before and
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		int(tool_manager.GetSelectedRoadType()) == selected_road_type_before and
		builder.HasActivePlaceSession() and
		builder.GetFixedCornerCount() == 1 and
		builder.GetUndoEditCount() == undo_count_before and
		builder.GetRedoEditCount() == redo_count_before and
		renderer.GetRenderedEdgeCount() == edge_count_before and
		renderer.GetRoadMeshVertexCount() == vertex_count_before and
		renderer.GetNodeMarkerCount() == marker_count_before and
		renderer.GetPresentationState() == presentation_before and
		renderer.FindRoadSurfaceHit(Vector2(400.0, 300.0), 0.0) == hit_before,
		"Worker-entry failure changed resources, graph, tool, placement, history, presentation, surface, token, or slot state"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, active_slot_id),
		"Could not recapture the active graph after worker-entry failure"):
		return
	var worker_entry_payload_after := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	if not require(
		worker_entry_payload_after == active_payload_before,
		"Worker-entry failure changed RoadGraph"):
		return

	var post_slot_preparation_resource_count_before := int(probe.GetObjectResourceCount())
	var post_slot_preparation_failure_message := str(
		probe.GetAggregateLoadPostSlotPreparationFailureMessage())
	probe.ArmAggregateLoadPostSlotPreparationFailure(save_manager)
	if not require(
		bool(probe.IsAggregateLoadPostSlotPreparationFailureArmed()),
		"Aggregate Load post-slot preparation failure probe did not arm"):
		return
	var post_slot_preparation_failed_load_result := await run_load(source_slot_id)
	var post_slot_preparation_resource_count_after := int(probe.GetObjectResourceCount())
	if not require(
		int(post_slot_preparation_failed_load_result.get("resultKind", -1)) ==
			RESULT_FAILED and
		int(post_slot_preparation_failed_load_result.get("finalPhase", -1)) ==
			PHASE_PREPARE and
		not bool(post_slot_preparation_failed_load_result.get("committed", true)) and
		str(post_slot_preparation_failed_load_result.get("warnings", "")).is_empty() and
		str(post_slot_preparation_failed_load_result.get("error", "")) ==
			post_slot_preparation_failure_message,
		"Real StartLoad did not fail after slot preparation and before graph-state lookup: %s" %
		JSON.stringify(post_slot_preparation_failed_load_result)):
		return
	if not require(
		not bool(probe.IsAggregateLoadPostSlotPreparationFailureArmed()) and
		int(probe.GetAggregateLoadPostSlotPreparationFailureCount()) == 1 and
		bool(probe.DidAggregateLoadPostSlotPreparationFailureRunOffMainThread()) and
		str(probe.GetAggregateLoadPostSlotPreparationObservedSlotID()) == source_slot_id and
		int(probe.GetAggregateLoadPostSlotPreparationParticipantCount()) == 1 and
		post_slot_preparation_resource_count_after ==
			post_slot_preparation_resource_count_before and
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		int(tool_manager.GetSelectedRoadType()) == selected_road_type_before and
		builder.HasActivePlaceSession() and
		builder.GetFixedCornerCount() == 1 and
		builder.GetUndoEditCount() == undo_count_before and
		builder.GetRedoEditCount() == redo_count_before and
		renderer.GetRenderedEdgeCount() == edge_count_before and
		renderer.GetRoadMeshVertexCount() == vertex_count_before and
		renderer.GetNodeMarkerCount() == marker_count_before and
		renderer.GetPresentationState() == presentation_before and
		renderer.FindRoadSurfaceHit(Vector2(400.0, 300.0), 0.0) == hit_before,
		"Post-slot preparation failure changed resources, graph, tool, placement, history, presentation, surface, token, or slot state"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, active_slot_id),
		"Could not recapture the active graph after post-slot preparation failure"):
		return
	var post_slot_preparation_payload_after := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	if not require(
		post_slot_preparation_payload_after == active_payload_before,
		"Post-slot preparation failure changed RoadGraph"):
		return

	var post_graph_state_lookup_resource_count_before := int(probe.GetObjectResourceCount())
	var post_graph_state_lookup_failure_message := str(
		probe.GetAggregateLoadPostGraphStateLookupFailureMessage())
	probe.ArmAggregateLoadPostGraphStateLookupFailure(save_manager)
	if not require(
		bool(probe.IsAggregateLoadPostGraphStateLookupFailureArmed()),
		"Aggregate Load post-graph-state lookup failure probe did not arm"):
		return
	var post_graph_state_lookup_failed_load_result := await run_load(source_slot_id)
	var post_graph_state_lookup_resource_count_after := int(probe.GetObjectResourceCount())
	if not require(
		int(post_graph_state_lookup_failed_load_result.get("resultKind", -1)) ==
			RESULT_FAILED and
		int(post_graph_state_lookup_failed_load_result.get("finalPhase", -1)) ==
			PHASE_PREPARE and
		not bool(post_graph_state_lookup_failed_load_result.get("committed", true)) and
		str(post_graph_state_lookup_failed_load_result.get("warnings", "")).is_empty() and
		str(post_graph_state_lookup_failed_load_result.get("error", "")) ==
			post_graph_state_lookup_failure_message,
		"Real StartLoad did not fail after graph-state lookup and before revision validation: %s" %
		JSON.stringify(post_graph_state_lookup_failed_load_result)):
		return
	if not require(
		not bool(probe.IsAggregateLoadPostGraphStateLookupFailureArmed()) and
		int(probe.GetAggregateLoadPostGraphStateLookupFailureCount()) == 1 and
		bool(probe.DidAggregateLoadPostGraphStateLookupFailureRunOffMainThread()) and
		str(probe.GetAggregateLoadPostGraphStateLookupObservedStateTypeName()) ==
			"RoadGraphRevision" and
		post_graph_state_lookup_resource_count_after ==
			post_graph_state_lookup_resource_count_before and
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		int(tool_manager.GetSelectedRoadType()) == selected_road_type_before and
		builder.HasActivePlaceSession() and
		builder.GetFixedCornerCount() == 1 and
		builder.GetUndoEditCount() == undo_count_before and
		builder.GetRedoEditCount() == redo_count_before and
		renderer.GetRenderedEdgeCount() == edge_count_before and
		renderer.GetRoadMeshVertexCount() == vertex_count_before and
		renderer.GetNodeMarkerCount() == marker_count_before and
		renderer.GetPresentationState() == presentation_before and
		renderer.FindRoadSurfaceHit(Vector2(400.0, 300.0), 0.0) == hit_before,
		"Post-graph-state lookup failure changed resources, graph, tool, placement, history, presentation, surface, token, or slot state"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, active_slot_id),
		"Could not recapture the active graph after post-graph-state lookup failure"):
		return
	var post_graph_state_lookup_payload_after := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	if not require(
		post_graph_state_lookup_payload_after == active_payload_before,
		"Post-graph-state lookup failure changed RoadGraph"):
		return

	var renderer_worker_prepare_resource_count_before := int(probe.GetObjectResourceCount())
	var renderer_worker_prepare_failure_message := str(
		probe.GetAggregateLoadRendererWorkerPrepareFailureMessage())
	probe.ArmAggregateLoadRendererWorkerPrepareFailure(save_manager)
	if not require(
		bool(probe.IsAggregateLoadRendererWorkerPrepareFailureArmed()),
		"Aggregate Load renderer worker-prepare failure probe did not arm"):
		return
	var renderer_worker_prepare_failed_load_result := await run_load(source_slot_id)
	if not require(
		int(renderer_worker_prepare_failed_load_result.get("resultKind", -1)) ==
			RESULT_FAILED and
		not bool(renderer_worker_prepare_failed_load_result.get("committed", true)) and
		str(renderer_worker_prepare_failed_load_result.get("warnings", "")).is_empty() and
		str(renderer_worker_prepare_failed_load_result.get("error", "")) ==
			renderer_worker_prepare_failure_message,
		"Real StartLoad did not fail before renderer worker preparation: %s" %
		JSON.stringify(renderer_worker_prepare_failed_load_result)):
		return
	var renderer_worker_prepare_resource_count_after := int(probe.GetObjectResourceCount())
	if not require(
		not bool(probe.IsAggregateLoadRendererWorkerPrepareFailureArmed()) and
		int(probe.GetAggregateLoadRendererWorkerPrepareFailureCount()) == 1 and
		renderer_worker_prepare_resource_count_after ==
			renderer_worker_prepare_resource_count_before,
		"Renderer worker-prepare failure allocated resources or kept its probe armed: %s" %
		JSON.stringify({
			"armed": bool(probe.IsAggregateLoadRendererWorkerPrepareFailureArmed()),
			"triggerCount": int(
				probe.GetAggregateLoadRendererWorkerPrepareFailureCount()),
			"resourceCountBefore": renderer_worker_prepare_resource_count_before,
			"resourceCountAfter": renderer_worker_prepare_resource_count_after,
		})):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		int(tool_manager.GetSelectedRoadType()) == selected_road_type_before and
		builder.HasActivePlaceSession() and
		builder.GetFixedCornerCount() == 1 and
		builder.GetUndoEditCount() == undo_count_before and
		builder.GetRedoEditCount() == redo_count_before and
		renderer.GetRenderedEdgeCount() == edge_count_before and
		renderer.GetRoadMeshVertexCount() == vertex_count_before and
		renderer.GetNodeMarkerCount() == marker_count_before and
		renderer.GetPresentationState() == presentation_before and
		renderer.FindRoadSurfaceHit(Vector2(400.0, 300.0), 0.0) == hit_before,
		"Renderer worker-prepare failure changed graph, tool, placement, history, presentation, surface, token, or slot state"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, active_slot_id),
		"Could not recapture the active graph after renderer worker-prepare failure"):
		return
	var renderer_worker_prepare_payload_after := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	if not require(
		renderer_worker_prepare_payload_after == active_payload_before,
		"Renderer worker-prepare failure changed RoadGraph"):
		return

	var load_result := await run_load(source_slot_id)
	if not require(
		int(load_result.get("resultKind", -1)) == RESULT_SUCCEEDED and
		bool(load_result.get("committed", false)) and
		str(load_result.get("warnings", "")).is_empty(),
		"Load after failed preflight did not reacquire every admission: %s" %
		JSON.stringify(load_result)):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == source_slot_id and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		not builder.HasActivePlaceSession() and
		builder.GetUndoEditCount() == 0 and
		builder.GetRedoEditCount() == 0 and
		renderer.GetRenderedEdgeCount() == 0 and
		matching_presentation_is_ready(renderer),
		"Load after failed preflight did not jointly commit empty graph/tool/presentation/slot state: %s" %
		JSON.stringify({
			"slot": str(save_manager.get("CurrentSlotID")),
			"tool": int(tool_manager.get("CurrentTool")),
			"hasPlacement": builder.HasActivePlaceSession(),
			"undoCount": builder.GetUndoEditCount(),
			"redoCount": builder.GetRedoEditCount(),
			"renderedEdges": renderer.GetRenderedEdgeCount(),
			"presentation": renderer.GetPresentationState(),
		})):
		return

	print("ROAD_LOAD_PREFLIGHT_RESOURCE_FAILURE_RESULT %s" % JSON.stringify({
		"exception_type": str(probe_result.get("exceptionType", "")),
		"resource_count_before": int(probe_result.get("resourceCountBefore", -1)),
		"resource_count_after": int(probe_result.get("resourceCountAfter", -1)),
		"road_vertices": int(probe_result.get("roadVertexCount", -1)),
		"node_markers": int(probe_result.get("nodeMarkerCount", -1)),
		"plan_resource_count_before": int(
			plan_disposal_result.get("resourceCountBefore", -1)),
		"plan_resource_count_after": int(
			plan_disposal_result.get("resourceCountAfter", -1)),
		"plan_became_stale": bool(plan_disposal_result.get("planBecameStale", false)),
		"renderer_boundary_exception_type": str(
			renderer_boundary_result.get("exceptionType", "")),
		"renderer_boundary_count": int(renderer_boundary_result.get("boundaryCount", -1)),
		"renderer_boundary_mark_committed_count": int(
			renderer_boundary_result.get("markCommittedCount", -1)),
		"renderer_boundary_resource_count_before": int(
			renderer_boundary_result.get("resourceCountBefore", -1)),
		"renderer_boundary_resource_count_after": int(
			renderer_boundary_result.get("resourceCountAfter", -1)),
		"tool_boundary_exception_type": str(
			tool_boundary_result.get("exceptionType", "")),
		"tool_boundary_count": int(tool_boundary_result.get("boundaryCount", -1)),
		"tool_boundary_mark_committed_count": int(
			tool_boundary_result.get("markCommittedCount", -1)),
		"tool_boundary_tool_admission_reacquired": bool(
			tool_boundary_result.get("toolAdmissionReacquired", false)),
		"tool_boundary_builder_admission_reacquired": bool(
			tool_boundary_result.get("builderAdmissionReacquired", false)),
		"node_batch_exception_type": str(
			node_batch_failure_result.get("exceptionType", "")),
		"node_batch_resource_count_before": int(
			node_batch_failure_result.get("resourceCountBefore", -1)),
		"node_batch_resource_count_after": int(
			node_batch_failure_result.get("resourceCountAfter", -1)),
		"road_mesh_exception_type": str(
			road_mesh_failure_result.get("exceptionType", "")),
		"road_mesh_resource_count_before": int(
			road_mesh_failure_result.get("resourceCountBefore", -1)),
		"road_mesh_resource_count_after": int(
			road_mesh_failure_result.get("resourceCountAfter", -1)),
		"aggregate_road_mesh_failure_result_kind": int(
			aggregate_road_mesh_failed_load_result.get("resultKind", -1)),
		"aggregate_road_mesh_failure_committed": bool(
			aggregate_road_mesh_failed_load_result.get("committed", true)),
		"aggregate_road_mesh_failure_trigger_count": int(
			probe.GetAggregateLoadRoadMeshFactoryFailureCount()),
		"aggregate_road_mesh_index_enumeration_count": int(
			probe.GetAggregateLoadRoadMeshFactoryIndexEnumerationCount()),
		"aggregate_road_mesh_resource_count_before":
			aggregate_road_mesh_resource_count_before,
		"aggregate_road_mesh_resource_count_after":
			aggregate_road_mesh_resource_count_after,
		"aggregate_node_batch_failure_result_kind": int(
			aggregate_node_batch_failed_load_result.get("resultKind", -1)),
		"aggregate_node_batch_failure_committed": bool(
			aggregate_node_batch_failed_load_result.get("committed", true)),
		"aggregate_node_batch_failure_trigger_count": int(
			probe.GetAggregateLoadNodeBatchFactoryFailureCount()),
		"aggregate_node_batch_marker_read_count": int(
			probe.GetAggregateLoadNodeBatchFactoryMarkerReadCount()),
		"aggregate_node_batch_resource_count_before":
			aggregate_node_batch_resource_count_before,
		"aggregate_node_batch_resource_count_after":
			aggregate_node_batch_resource_count_after,
		"aggregate_reserved_token_failure_result_kind": int(
			aggregate_reserved_token_failed_load_result.get("resultKind", -1)),
		"aggregate_reserved_token_failure_committed": bool(
			aggregate_reserved_token_failed_load_result.get("committed", true)),
		"aggregate_reserved_token_failure_trigger_count": int(
			probe.GetAggregateLoadReservedRenderTokenFailureCount()),
		"aggregate_reserved_token_resource_count_before":
			aggregate_reserved_token_resource_count_before,
		"aggregate_reserved_token_resource_count_after":
			aggregate_reserved_token_resource_count_after,
		"aggregate_snapshot_failure_result_kind": int(
			aggregate_snapshot_failed_load_result.get("resultKind", -1)),
		"aggregate_snapshot_failure_committed": bool(
			aggregate_snapshot_failed_load_result.get("committed", true)),
		"aggregate_snapshot_failure_trigger_count": int(
			probe.GetAggregateLoadRoadSurfaceSnapshotFailureCount()),
		"aggregate_snapshot_resource_count_before":
			aggregate_snapshot_resource_count_before,
		"aggregate_snapshot_resource_count_after":
			aggregate_snapshot_resource_count_after,
		"aggregate_commit_plan_failure_result_kind": int(
			aggregate_commit_plan_failed_load_result.get("resultKind", -1)),
		"aggregate_commit_plan_failure_committed": bool(
			aggregate_commit_plan_failed_load_result.get("committed", true)),
		"aggregate_commit_plan_failure_trigger_count": int(
			probe.GetAggregateLoadCommitPlanConstructionFailureCount()),
		"aggregate_commit_plan_resource_count_before":
			aggregate_commit_plan_resource_count_before,
		"aggregate_commit_plan_resource_count_after":
			aggregate_commit_plan_resource_count_after,
		"renderer_admission_style_snapshot_failure_result_kind": int(
			renderer_admission_style_snapshot_failed_load_result.get("resultKind", -1)),
		"renderer_admission_style_snapshot_failure_committed": bool(
			renderer_admission_style_snapshot_failed_load_result.get("committed", true)),
		"renderer_admission_style_snapshot_resource_count_before":
			renderer_admission_style_snapshot_resource_count_before,
		"renderer_admission_style_snapshot_resource_count_after":
			renderer_admission_style_snapshot_resource_count_after,
		"renderer_admission_settings_failure_result_kind": int(
			renderer_admission_settings_failed_load_result.get("resultKind", -1)),
		"renderer_admission_settings_failure_committed": bool(
			renderer_admission_settings_failed_load_result.get("committed", true)),
		"renderer_admission_settings_resource_count_before":
			renderer_admission_settings_resource_count_before,
		"renderer_admission_settings_resource_count_after":
			renderer_admission_settings_resource_count_after,
		"renderer_admission_construction_failure_result_kind": int(
			renderer_admission_construction_failed_load_result.get("resultKind", -1)),
		"renderer_admission_construction_failure_committed": bool(
			renderer_admission_construction_failed_load_result.get("committed", true)),
		"renderer_admission_construction_failure_trigger_count": int(
			probe.GetAggregateLoadRendererAdmissionConstructionFailureCount()),
		"renderer_admission_construction_resource_count_before":
			renderer_admission_construction_resource_count_before,
		"renderer_admission_construction_resource_count_after":
			renderer_admission_construction_resource_count_after,
		"renderer_admission_publication_failure_result_kind": int(
			renderer_admission_publication_failed_load_result.get("resultKind", -1)),
		"renderer_admission_publication_failure_committed": bool(
			renderer_admission_publication_failed_load_result.get("committed", true)),
		"renderer_admission_publication_failure_trigger_count": int(
			probe.GetAggregateLoadRendererAdmissionPublicationFailureCount()),
		"renderer_admission_publication_resource_count_before":
			renderer_admission_publication_resource_count_before,
		"renderer_admission_publication_resource_count_after":
			renderer_admission_publication_resource_count_after,
		"post_renderer_admission_failure_result_kind": int(
			post_renderer_admission_failed_load_result.get("resultKind", -1)),
		"post_renderer_admission_failure_committed": bool(
			post_renderer_admission_failed_load_result.get("committed", true)),
		"post_renderer_admission_failure_trigger_count": int(
			probe.GetAggregateLoadPostRendererAdmissionFailureCount()),
		"post_renderer_admission_resource_count_before":
			post_renderer_admission_resource_count_before,
		"post_renderer_admission_resource_count_after":
			post_renderer_admission_resource_count_after,
		"post_participant_capture_failure_result_kind": int(
			post_participant_capture_failed_load_result.get("resultKind", -1)),
		"post_participant_capture_failure_committed": bool(
			post_participant_capture_failed_load_result.get("committed", true)),
		"post_participant_capture_failure_trigger_count": int(
			probe.GetAggregateLoadPostParticipantCaptureFailureCount()),
		"post_participant_capture_participant_count": int(
			probe.GetAggregateLoadPostParticipantCaptureParticipantCount()),
		"post_participant_capture_resource_count_before":
			post_participant_capture_resource_count_before,
		"post_participant_capture_resource_count_after":
			post_participant_capture_resource_count_after,
		"post_prepare_phase_failure_result_kind": int(
			post_prepare_phase_failed_load_result.get("resultKind", -1)),
		"post_prepare_phase_failure_final_phase": int(
			post_prepare_phase_failed_load_result.get("finalPhase", -1)),
		"post_prepare_phase_failure_committed": bool(
			post_prepare_phase_failed_load_result.get("committed", true)),
		"post_prepare_phase_failure_trigger_count": int(
			probe.GetAggregateLoadPostPreparePhaseFailureCount()),
		"post_prepare_phase_observed_phase": int(
			probe.GetAggregateLoadPostPreparePhaseObservedPhase()),
		"post_prepare_phase_resource_count_before":
			post_prepare_phase_resource_count_before,
		"post_prepare_phase_resource_count_after":
			post_prepare_phase_resource_count_after,
		"worker_entry_failure_result_kind": int(
			worker_entry_failed_load_result.get("resultKind", -1)),
		"worker_entry_failure_final_phase": int(
			worker_entry_failed_load_result.get("finalPhase", -1)),
		"worker_entry_failure_committed": bool(
			worker_entry_failed_load_result.get("committed", true)),
		"worker_entry_failure_trigger_count": int(
			probe.GetAggregateLoadWorkerEntryFailureCount()),
		"worker_entry_failure_off_main_thread": bool(
			probe.DidAggregateLoadWorkerEntryFailureRunOffMainThread()),
		"worker_entry_resource_count_before": worker_entry_resource_count_before,
		"worker_entry_resource_count_after": worker_entry_resource_count_after,
		"post_slot_preparation_failure_result_kind": int(
			post_slot_preparation_failed_load_result.get("resultKind", -1)),
		"post_slot_preparation_failure_final_phase": int(
			post_slot_preparation_failed_load_result.get("finalPhase", -1)),
		"post_slot_preparation_failure_committed": bool(
			post_slot_preparation_failed_load_result.get("committed", true)),
		"post_slot_preparation_failure_trigger_count": int(
			probe.GetAggregateLoadPostSlotPreparationFailureCount()),
		"post_slot_preparation_failure_off_main_thread": bool(
			probe.DidAggregateLoadPostSlotPreparationFailureRunOffMainThread()),
		"post_slot_preparation_observed_slot_id": str(
			probe.GetAggregateLoadPostSlotPreparationObservedSlotID()),
		"post_slot_preparation_participant_count": int(
			probe.GetAggregateLoadPostSlotPreparationParticipantCount()),
		"post_slot_preparation_resource_count_before":
			post_slot_preparation_resource_count_before,
		"post_slot_preparation_resource_count_after":
			post_slot_preparation_resource_count_after,
		"post_graph_state_lookup_failure_result_kind": int(
			post_graph_state_lookup_failed_load_result.get("resultKind", -1)),
		"post_graph_state_lookup_failure_final_phase": int(
			post_graph_state_lookup_failed_load_result.get("finalPhase", -1)),
		"post_graph_state_lookup_failure_committed": bool(
			post_graph_state_lookup_failed_load_result.get("committed", true)),
		"post_graph_state_lookup_failure_trigger_count": int(
			probe.GetAggregateLoadPostGraphStateLookupFailureCount()),
		"post_graph_state_lookup_failure_off_main_thread": bool(
			probe.DidAggregateLoadPostGraphStateLookupFailureRunOffMainThread()),
		"post_graph_state_lookup_observed_state_type": str(
			probe.GetAggregateLoadPostGraphStateLookupObservedStateTypeName()),
		"post_graph_state_lookup_resource_count_before":
			post_graph_state_lookup_resource_count_before,
		"post_graph_state_lookup_resource_count_after":
			post_graph_state_lookup_resource_count_after,
		"renderer_worker_prepare_failure_result_kind": int(
			renderer_worker_prepare_failed_load_result.get("resultKind", -1)),
		"renderer_worker_prepare_failure_committed": bool(
			renderer_worker_prepare_failed_load_result.get("committed", true)),
		"renderer_worker_prepare_failure_trigger_count": int(
			probe.GetAggregateLoadRendererWorkerPrepareFailureCount()),
		"renderer_worker_prepare_resource_count_before":
			renderer_worker_prepare_resource_count_before,
		"renderer_worker_prepare_resource_count_after":
			renderer_worker_prepare_resource_count_after,
		"aggregate_failure_result_kind": int(failed_load_result.get("resultKind", -1)),
		"aggregate_failure_committed": bool(failed_load_result.get("committed", true)),
		"aggregate_failure_trigger_count": int(
			probe.GetAggregateLoadResourcePreflightFailureCount()),
		"aggregate_resource_count_before": aggregate_resource_count_before,
		"aggregate_resource_count_after": int(probe.GetObjectResourceCount()),
		"post_renderer_failure_result_kind": int(
			post_renderer_failed_load_result.get("resultKind", -1)),
		"post_renderer_failure_committed": bool(
			post_renderer_failed_load_result.get("committed", true)),
		"post_renderer_failure_trigger_count": int(
			probe.GetAggregateLoadPostRendererPreflightFailureCount()),
		"post_renderer_resource_count_before": post_renderer_resource_count_before,
		"post_renderer_resource_count_after": post_renderer_resource_count_after,
		"post_slot_failure_result_kind": int(
			post_slot_failed_load_result.get("resultKind", -1)),
		"post_slot_failure_committed": bool(
			post_slot_failed_load_result.get("committed", true)),
		"post_slot_failure_trigger_count": int(
			probe.GetAggregateLoadPostSlotPreflightFailureCount()),
		"post_slot_resource_count_before": post_slot_resource_count_before,
		"post_slot_resource_count_after": post_slot_resource_count_after,
		"post_ownership_failure_result_kind": int(
			post_ownership_failed_load_result.get("resultKind", -1)),
		"post_ownership_failure_committed": bool(
			post_ownership_failed_load_result.get("committed", true)),
		"post_ownership_failure_trigger_count": int(
			probe.GetAggregateLoadPostOwnershipPreCommitFailureCount()),
		"post_ownership_resource_count_before": post_ownership_resource_count_before,
		"post_ownership_resource_count_after": post_ownership_resource_count_after,
		"graph_boundary_operation_failure_result_kind": int(
			graph_boundary_operation_failed_load_result.get("resultKind", -1)),
		"graph_boundary_operation_failure_committed": bool(
			graph_boundary_operation_failed_load_result.get("committed", true)),
		"graph_boundary_operation_failure_trigger_count": int(
			probe.GetAggregateLoadGraphCommitBoundaryGenerationMismatchCount()),
		"graph_boundary_operation_count": int(
			probe.GetAggregateLoadGraphCommitBoundaryCount()),
		"graph_boundary_operation_mark_committed_count": int(
			probe.GetAggregateLoadGraphMarkCommittedCount()),
		"graph_boundary_operation_resource_count_before":
			graph_boundary_operation_resource_count_before,
		"graph_boundary_operation_resource_count_after":
			graph_boundary_operation_resource_count_after,
		"renderer_boundary_operation_failure_result_kind": int(
			renderer_boundary_operation_failed_load_result.get("resultKind", -1)),
		"renderer_boundary_operation_failure_committed": bool(
			renderer_boundary_operation_failed_load_result.get("committed", true)),
		"renderer_boundary_operation_failure_trigger_count": int(
			probe.GetAggregateLoadRendererCommitBoundaryGenerationMismatchCount()),
		"renderer_boundary_operation_count": int(
			probe.GetAggregateLoadRendererCommitBoundaryCount()),
		"renderer_boundary_operation_mark_committed_count": int(
			probe.GetAggregateLoadRendererMarkCommittedCount()),
		"renderer_boundary_operation_resource_count_before":
			renderer_boundary_operation_resource_count_before,
		"renderer_boundary_operation_resource_count_after":
			renderer_boundary_operation_resource_count_after,
		"tool_boundary_operation_failure_result_kind": int(
			tool_boundary_operation_failed_load_result.get("resultKind", -1)),
		"tool_boundary_operation_failure_committed": bool(
			tool_boundary_operation_failed_load_result.get("committed", true)),
		"tool_boundary_operation_failure_trigger_count": int(
			probe.GetAggregateLoadToolCommitBoundaryGenerationMismatchCount()),
		"tool_boundary_operation_count": int(
			probe.GetAggregateLoadToolCommitBoundaryCount()),
		"tool_boundary_operation_mark_committed_count": int(
			probe.GetAggregateLoadToolMarkCommittedCount()),
		"tool_boundary_operation_resource_count_before":
			tool_boundary_operation_resource_count_before,
		"tool_boundary_operation_resource_count_after":
			tool_boundary_operation_resource_count_after,
		"slot_target_boundary_operation_failure_result_kind": int(
			slot_target_boundary_operation_failed_load_result.get("resultKind", -1)),
		"slot_target_boundary_operation_failure_committed": bool(
			slot_target_boundary_operation_failed_load_result.get("committed", true)),
		"slot_target_boundary_operation_failure_trigger_count": int(
			probe.GetAggregateLoadSlotTargetCommitBoundaryGenerationMismatchCount()),
		"slot_target_boundary_operation_count": int(
			probe.GetAggregateLoadSlotTargetCommitBoundaryCount()),
		"slot_target_boundary_operation_mark_committed_count": int(
			probe.GetAggregateLoadSlotTargetMarkCommittedCount()),
		"slot_target_boundary_operation_resource_count_before":
			slot_target_boundary_operation_resource_count_before,
		"slot_target_boundary_operation_resource_count_after":
			slot_target_boundary_operation_resource_count_after,
		"load_result_kind": int(load_result.get("resultKind", -1)),
	}))
	await cleanup()
	print("PASS road load preflight resource failure runtime contract")
	quit(0)

func run_load(slot_id: String) -> Dictionary:
	var operation_token: String = save_manager.StartLoad(slot_id)
	var result: Dictionary = await V3_SAVE_FIXTURE.wait_for_operation(
		save_manager,
		operation_token)
	if not await V3_SAVE_FIXTURE.wait_for_idle(save_manager):
		fail("Load did not become idle for slot %s" % slot_id)
		return {}
	return result

func matching_presentation_is_ready(renderer: Node) -> bool:
	var state: Dictionary = renderer.GetPresentationState()
	return (
		bool(state.get("isReady", false)) and
		not bool(state.get("isStalled", true)) and
		state.get("desired", {}) == state.get("presented", {}))

func cleanup() -> void:
	if save_manager != null and not source_slot_id.is_empty():
		await V3_SAVE_FIXTURE.delete_slot(save_manager, source_slot_id)
		source_slot_id = ""
	if save_manager != null and not active_slot_id.is_empty():
		await V3_SAVE_FIXTURE.delete_slot(save_manager, active_slot_id)
		active_slot_id = ""
	if test_map != null and is_instance_valid(test_map):
		test_map.queue_free()
		await process_frame
	if probe != null:
		probe.FlushPendingManagedFinalizers()
		probe = null

func require(condition: bool, message: String) -> bool:
	if condition:
		return true
	fail(message)
	return false

func fail(message: String) -> void:
	if failed:
		return
	failed = true
	push_error("FAIL road load preflight resource failure runtime contract: %s" % message)
	cleanup_after_failure.call_deferred()

func cleanup_after_failure() -> void:
	await cleanup()
	quit(1)
