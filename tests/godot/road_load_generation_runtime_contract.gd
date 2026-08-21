extends SceneTree

const MAP_SCENE := "res://Scenes/MapTest.tscn"
const SOURCE_SLOT_NAME := "Road load generation source"
const ACTIVE_SLOT_NAME := "Road load generation active"
const V3_SAVE_FIXTURE := preload("res://tests/godot/v3_save_fixture.gd")
const EDGE_COUNT := 10_000
const EDGE_LENGTH := 8.0
const EDGE_SPACING := 32.0
const PHASE_PREPARE := 3
const PHASE_COMMIT := 5
const RESULT_FAILED := 2
const RESULT_CANCELED := 3

var failed := false
var test_map: Node
var save_manager: Node
var source_slot_id := ""
var active_slot_id := ""
var active_load_token := ""
var invalidation_target: Node
var invalidation_parent: Node
var detached_target: Node
var invalidation_count := 0

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
	if not require(
		save_manager.has_signal("SaveOperationStateChanged"),
		"SaveManager does not expose its operation-state signal"):
		return
	save_manager.connect(
		"SaveOperationStateChanged",
		Callable(self, "_on_save_operation_state_changed"))

	if not require(
		await V3_SAVE_FIXTURE.save_as(save_manager, SOURCE_SLOT_NAME),
		"Could not create the source slot"):
		return
	source_slot_id = str(save_manager.get("CurrentSlotID"))

	var builder: Node = test_map.get_node("RoadSystem/RoadBuilder")
	var renderer: Node = test_map.get_node("RoadSystem/RoadRenderer")
	var tool_manager: Node = test_map.get_node("ToolManager")
	if not require(builder.BeginPlace(Vector2(200.0, 350.0)), "Active graph fixture did not begin"):
		return
	builder.UpdatePlace(Vector2(600.0, 350.0))
	if not require(builder.CommitPlace(Vector2(600.0, 350.0)), "Active graph fixture did not commit"):
		return
	await process_frame
	await process_frame
	if not require(renderer.GetRenderedEdgeCount() == 1, "Active graph fixture did not render"):
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

	tool_manager.set("CurrentTool", 1)
	if not require(builder.BeginPlace(Vector2(800.0, 300.0)), "Preserved placement did not begin"):
		return
	if not require(builder.AddPlacePoint(Vector2(900.0, 300.0)), "Preserved placement corner was not fixed"):
		return

	if not require(write_large_fixture(source_slot_id), "Could not publish the 10k source fixture"):
		return

	if not await run_renderer_invalidation(tool_manager, renderer, builder, active_payload_before):
		return

	if not await run_scene_invalidation(
		tool_manager,
		renderer,
		builder,
		active_payload_before,
		"Placement scene-stale Load"):
		return

	var builder_process_mode_before: int = builder.process_mode
	builder.process_mode = Node.PROCESS_MODE_DISABLED
	builder.CancelPlaceSession()
	tool_manager.set("CurrentTool", 2)
	var removal_edge_hit: Dictionary = renderer.FindRoadSurfaceHit(Vector2(400.0, 400.0), 0.0)
	if not require(
		builder.BeginRemove(Vector2(180.0, 380.0), true),
		"Removal state did not begin before scene-stale Load"):
		return
	builder.UpdateRemove(Vector2(620.0, 420.0))
	if not require(
		builder.HasActiveRemoveSession() and
		builder.GetRemovalSelectionCount() == 1 and
		renderer.GetRemovalPreviewEdgeCount() == 1 and
		renderer.HasRemovalSelectionBounds(),
		"Removal state did not retain a rectangle selection before scene-stale Load"):
		return
	if not removal_edge_hit.is_empty():
		renderer.SetHoveredEdgeID(int(removal_edge_hit.get("edgeID", -1)))
	if not await run_scene_invalidation(
		tool_manager,
		renderer,
		builder,
		active_payload_before,
		"Removal scene-stale Load"):
		return

	builder.CancelRemoveSession()
	tool_manager.set("CurrentTool", 3)
	if not require(builder.SetSelectedRoadType(2), "RoadUpgrade target type could not be selected"):
		return
	if not require(
		builder.BeginUpgrade(Vector2(180.0, 380.0), true),
		"RoadUpgrade state did not begin before scene-stale Load"):
		return
	builder.UpdateUpgrade(Vector2(620.0, 420.0))
	if not require(
		builder.HasActiveUpgradeSession() and
		builder.GetUpgradeTargetRoadType() == 2 and
		builder.GetUpgradeSelectionCount() == 1 and
		renderer.GetUpgradePreviewEdgeCount() == 1 and
		renderer.HasUpgradeSelectionBounds(),
		"RoadUpgrade state did not retain a rectangle selection before scene-stale Load"):
		return
	if not removal_edge_hit.is_empty():
		renderer.SetHoveredEdgeID(int(removal_edge_hit.get("edgeID", -1)))
	if not await run_scene_invalidation(
		tool_manager,
		renderer,
		builder,
		active_payload_before,
		"RoadUpgrade scene-stale Load"):
		return

	builder.CancelUpgradeSession()
	tool_manager.set("CurrentTool", 0)
	builder.process_mode = builder_process_mode_before

	await cleanup()
	await process_frame
	await process_frame
	if not require(save_manager.get("RegisteredSaveableCount") == 0, "Cleanup retained saveables"):
		return
	print("PASS road load generation runtime contract")
	quit(0)

func run_renderer_invalidation(
	tool_manager: Node,
	renderer: Node,
	builder: Node,
	active_payload_before: String) -> bool:
	var render_state_before: Dictionary = renderer.GetPresentationState().duplicate(true)
	var rendered_edges_before: int = renderer.GetRenderedEdgeCount()
	var mesh_vertices_before: int = renderer.GetRoadMeshVertexCount()
	var marker_count_before: int = renderer.GetNodeMarkerCount()
	var tool_state_before: Dictionary = capture_tool_state(tool_manager, renderer, builder)
	var builder_process_mode_before: int = int(builder.process_mode)
	builder.process_mode = Node.PROCESS_MODE_DISABLED

	arm_prepare_invalidation(renderer)
	active_load_token = str(save_manager.StartLoad(source_slot_id))
	var result: Dictionary = await V3_SAVE_FIXTURE.wait_for_operation(
		save_manager,
		active_load_token)
	active_load_token = ""
	if not require(await V3_SAVE_FIXTURE.wait_for_idle(save_manager), "Renderer-stale Load did not become idle"):
		return false
	if not require(invalidation_count == 1, "Renderer admission was not invalidated exactly once"):
		return false
	if not require(renderer.get_parent() == null, "Renderer remained inside the scene tree"):
		return false
	if not require_failure_before_commit(result, RESULT_FAILED, "Renderer-stale Load"):
		return false
	if not require(
		str(result.get("error", "")).contains("RoadRenderer"),
		"Renderer-stale Load did not identify the participant"):
		return false
	if not require_preserved_state(
		renderer,
		builder,
		tool_manager,
		render_state_before,
		rendered_edges_before,
		mesh_vertices_before,
		marker_count_before,
		tool_state_before,
		"Renderer-stale Load"):
		return false

	invalidation_parent.add_child(renderer)
	invalidation_parent = null
	detached_target = null
	builder.process_mode = builder_process_mode_before
	await process_frame
	await process_frame
	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, active_slot_id),
		"Could not recapture the graph after renderer invalidation"):
		return false
	return require_active_payload_unchanged(active_payload_before, "Renderer-stale Load")

func run_scene_invalidation(
	tool_manager: Node,
	renderer: Node,
	builder: Node,
	active_payload_before: String,
	label: String) -> bool:
	var render_state_before: Dictionary = renderer.GetPresentationState().duplicate(true)
	var rendered_edges_before: int = renderer.GetRenderedEdgeCount()
	var mesh_vertices_before: int = renderer.GetRoadMeshVertexCount()
	var marker_count_before: int = renderer.GetNodeMarkerCount()
	var tool_state_before: Dictionary = capture_tool_state(tool_manager, renderer, builder)
	var scene_generation_before: int = int(save_manager.get("SceneGeneration"))
	arm_prepare_invalidation(tool_manager)
	active_load_token = str(save_manager.StartLoad(source_slot_id))
	var result: Dictionary = await V3_SAVE_FIXTURE.wait_for_operation(
		save_manager,
		active_load_token)
	active_load_token = ""
	if not require(await V3_SAVE_FIXTURE.wait_for_idle(save_manager), "%s did not become idle" % label):
		return false
	if not require(invalidation_count == 1, "%s was not invalidated exactly once" % label):
		return false
	if not require(tool_manager.get_parent() == null, "%s left ToolManager inside the scene tree" % label):
		return false
	if not require_failure_before_commit(result, RESULT_CANCELED, label):
		return false
	if not require(
		int(save_manager.get("SceneGeneration")) != scene_generation_before,
		"%s did not advance scene generation" % label):
		return false
	if not require_preserved_state(
		renderer,
		builder,
		tool_manager,
		render_state_before,
		rendered_edges_before,
		mesh_vertices_before,
		marker_count_before,
		tool_state_before,
		label):
		return false

	tool_manager.request_ready()
	invalidation_parent.add_child(tool_manager)
	invalidation_parent = null
	detached_target = null
	await process_frame
	await process_frame
	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, active_slot_id),
		"Could not recapture the graph after %s" % label):
		return false
	return require_active_payload_unchanged(active_payload_before, label)

func arm_prepare_invalidation(target: Node) -> void:
	invalidation_target = target
	invalidation_parent = target.get_parent()
	invalidation_count = 0

func _on_save_operation_state_changed(
	operation_token: String,
	_operation_kind: int,
	_target_slot_id: String,
	phase: int,
	_has_crossed_commit_boundary: bool,
	_cancellation_requested: bool) -> void:
	if operation_token != active_load_token or phase != PHASE_PREPARE or invalidation_target == null:
		return
	invalidation_count += 1
	var target := invalidation_target
	invalidation_target = null
	invalidation_parent.remove_child(target)
	detached_target = target

func require_failure_before_commit(result: Dictionary, result_kind: int, label: String) -> bool:
	return require(not result.is_empty(), "%s did not publish a result" % label) and require(
		int(result.get("resultKind", -1)) == result_kind,
		"%s returned the wrong result kind" % label) and require(
		not bool(result.get("committed", true)),
		"%s crossed its commit boundary" % label) and require(
		int(result.get("finalPhase", PHASE_COMMIT)) < PHASE_COMMIT,
		"%s failed after commit began" % label) and require(
		str(save_manager.get("CurrentSlotID")) == active_slot_id,
		"%s changed the active slot" % label)

func require_preserved_state(
	renderer: Node,
	builder: Node,
	tool_manager: Node,
	render_state_before: Dictionary,
	rendered_edges_before: int,
	mesh_vertices_before: int,
	marker_count_before: int,
	tool_state_before: Dictionary,
	label: String) -> bool:
	return require(
		presentation_references_match(renderer.GetPresentationState(), render_state_before),
		"%s changed the presentation token or surface state" % label) and require(
		renderer.GetRenderedEdgeCount() == rendered_edges_before and
		renderer.GetRoadMeshVertexCount() == mesh_vertices_before and
		renderer.GetNodeMarkerCount() == marker_count_before,
		"%s changed renderer resources" % label) and require(
		capture_tool_state(tool_manager, renderer, builder) == tool_state_before,
		"%s changed tool-owned session, hover, selection, or history state" % label)

func capture_tool_state(tool_manager: Node, renderer: Node, builder: Node) -> Dictionary:
	return {
		"currentTool": int(tool_manager.get("CurrentTool")),
		"selectedRoadType": int(builder.GetSelectedRoadType()),
		"hasPlace": builder.HasActivePlaceSession(),
		"fixedCorners": builder.GetFixedCornerCount(),
		"previewPointCount": renderer.GetPreviewPointCount(),
		"hasRemove": builder.HasActiveRemoveSession(),
		"removeSelectionCount": builder.GetRemovalSelectionCount(),
		"removePreviewCount": renderer.GetRemovalPreviewEdgeCount(),
		"removeHasBounds": renderer.HasRemovalSelectionBounds(),
		"removeBounds": renderer.GetRemovalSelectionBounds() if renderer.HasRemovalSelectionBounds() else null,
		"hasUpgrade": builder.HasActiveUpgradeSession(),
		"upgradeSelectionCount": builder.GetUpgradeSelectionCount(),
		"upgradeTarget": int(builder.GetUpgradeTargetRoadType()),
		"upgradePreviewCount": renderer.GetUpgradePreviewEdgeCount(),
		"upgradeHasBounds": renderer.HasUpgradeSelectionBounds(),
		"upgradeBounds": renderer.GetUpgradeSelectionBounds() if renderer.HasUpgradeSelectionBounds() else null,
		"hoveredEdgeID": renderer.get("HoveredEdgeID"),
		"undoCount": builder.GetUndoEditCount(),
		"redoCount": builder.GetRedoEditCount(),
	}

func presentation_references_match(current: Dictionary, before: Dictionary) -> bool:
	var stable_keys: Array[String] = [
		"attemptCount",
		"desired",
		"presented",
		"isStalled",
		"stalledToken",
		"failureType",
		"failureMessage",
		"retainedSurfacePrimitiveCount",
	]
	for key: String in stable_keys:
		if current.get(key) != before.get(key):
			return false
	return true

func require_active_payload_unchanged(active_payload_before: String, label: String) -> bool:
	var active_payload_after := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	return require(active_payload_after == active_payload_before, "%s changed the active RoadGraph" % label)

func write_large_fixture(slot_id: String) -> bool:
	var path := V3_SAVE_FIXTURE.slot_path(slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME)
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return false
	var columns := ceili(sqrt(float(EDGE_COUNT) * 16.0 / 9.0))
	var rows := ceili(float(EDGE_COUNT) / float(columns))
	var width := float(columns - 1) * EDGE_SPACING
	var height := float(rows - 1) * EDGE_SPACING
	file.store_string(
		'{"formatFamily":"simple-cities-v3","payloadType":"road-network","schemaVersion":1,"nextID":%d,"nodes":[' %
		(EDGE_COUNT * 3 + 1))
	for index in range(EDGE_COUNT):
		var position := fixture_position(index, columns, width, height)
		write_item(file, {"id": index * 2 + 1, "x": position.x, "y": position.y}, index > 0)
		write_item(file, {"id": index * 2 + 2, "x": position.x + EDGE_LENGTH, "y": position.y}, true)
	file.store_string("],\"edges\":[")
	for index in range(EDGE_COUNT):
		var position := fixture_position(index, columns, width, height)
		write_item(file, {
			"id": EDGE_COUNT * 2 + index + 1,
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
	file.store_string("]}")
	file.close()
	return V3_SAVE_FIXTURE.refresh_manifest_payload(slot_id)

func fixture_position(index: int, columns: int, width: float, height: float) -> Vector2:
	var column := index % columns
	var row := index / columns
	return Vector2(
		float(column) * EDGE_SPACING - width * 0.5,
		float(row) * EDGE_SPACING - height * 0.5)

func write_item(file: FileAccess, value: Dictionary, prepend_comma: bool) -> void:
	if prepend_comma:
		file.store_8(44)
	file.store_string(JSON.stringify(value))

func cleanup() -> void:
	if save_manager != null:
		var callback := Callable(self, "_on_save_operation_state_changed")
		if save_manager.is_connected("SaveOperationStateChanged", callback):
			save_manager.disconnect("SaveOperationStateChanged", callback)
	if detached_target != null and is_instance_valid(detached_target) and invalidation_parent != null:
		if detached_target.name == "ToolManager":
			detached_target.request_ready()
		invalidation_parent.add_child(detached_target)
		detached_target = null
		invalidation_parent = null
		await process_frame
		await process_frame
	if save_manager != null and not active_slot_id.is_empty():
		await V3_SAVE_FIXTURE.delete_slot(save_manager, active_slot_id)
		active_slot_id = ""
	if save_manager != null and not source_slot_id.is_empty():
		await V3_SAVE_FIXTURE.delete_slot(save_manager, source_slot_id)
		source_slot_id = ""
	if test_map != null and is_instance_valid(test_map):
		test_map.queue_free()

func require(condition: bool, message: String) -> bool:
	if condition:
		return true
	fail(message)
	return false

func fail(message: String) -> void:
	if failed:
		return
	failed = true
	push_error("FAIL road load generation runtime contract: %s" % message)
	cleanup_after_failure.call_deferred()

func cleanup_after_failure() -> void:
	await cleanup()
	quit(1)
