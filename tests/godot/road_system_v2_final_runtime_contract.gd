extends SceneTree

const MAP_SCENE := "res://Scenes/MapTest.tscn"
const PRIMARY_DISPLAY_NAME := "V3 最终验收 / 城市: 2026"
const DAMAGED_DISPLAY_NAME := "V3 最终验收损坏槽"
const AUTOSAVE_SLOT_ID := "autosave"
const AUTOSAVE_PATH := "user://saves-v3/autosave"
const AUTOSAVE_BACKUP_PATH := "user://.qa-road-system-v3-final-autosave"
const V3_SAVE_FIXTURE := preload("res://tests/godot/v3_save_fixture.gd")
const CURVE_KINDS := [
	"line",
	"cubicBezier",
	"cubicHermite",
	"circularArc",
	"clothoid",
	"rationalQuadratic",
]

var test_map: Node
var save_manager: Node
var autosave_controller: Node
var primary_slot_id := ""
var curve_slot_id := ""
var damaged_slot_id := ""
var autosave_was_backed_up := false
var failure_cleanup_started := false

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	if not require(prepare_autosave_backup(), "Existing autosave could not be isolated"):
		return
	var packed_map: PackedScene = load(MAP_SCENE)
	if not require(packed_map != null, "MapTest scene did not load"):
		return
	test_map = packed_map.instantiate()
	autosave_controller = test_map.get_node("AutosaveController")
	autosave_controller.set("AutosaveEnabled", false)
	root.add_child(test_map)
	current_scene = test_map
	await process_frame
	await process_frame
	autosave_controller.SetAutosaveEnabled(false)

	save_manager = root.get_node("SaveManager")
	var road_builder: Node = test_map.get_node("RoadSystem/RoadBuilder")
	var road_renderer: Node = test_map.get_node("RoadSystem/RoadRenderer")
	if not require(save_manager.get("RegisteredSaveableCount") == 1, "MapTest did not register exactly the RoadGraph"):
		return

	if not await prepare_primary_line_fixture(road_renderer):
		return
	if not await verify_placement_crossing_and_history(road_builder, road_renderer):
		return
	if not await verify_primary_slot(road_renderer):
		return
	if not await verify_curve_slot(road_renderer):
		return
	if not await verify_autosave_and_damaged_load(road_renderer):
		return
	if not await cleanup_slots():
		return

	test_map.queue_free()
	await process_frame
	await process_frame
	if not require(save_manager.get("RegisteredSaveableCount") == 0, "Final runtime cleanup retained saveables"):
		return
	if not require(restore_autosave_backup(), "Existing autosave backup could not be restored"):
		return

	print("PASS road system v3 final runtime contract")
	quit(0)

func prepare_primary_line_fixture(road_renderer: Node) -> bool:
	if not require(await V3_SAVE_FIXTURE.save_as(save_manager, PRIMARY_DISPLAY_NAME), "Primary named slot was not created"):
		return false
	primary_slot_id = save_manager.get("CurrentSlotID")
	if not require(primary_slot_id.begins_with("manual-"), "Primary slot did not use a safe internal ID"):
		return false
	var fixture := {
		"formatFamily": "simple-cities-v3",
		"payloadType": "road-network",
		"schemaVersion": 1,
		"nextID": 4,
		"nodes": [
			{"id": 1, "x": 200.0, "y": 350.0},
			{"id": 2, "x": 600.0, "y": 350.0},
		],
		"edges": [{
			"id": 3,
			"nodeAID": 1,
			"nodeBID": 2,
			"roadType": "street",
			"geometry": [{
				"version": 1,
				"kind": "line",
				"start": point(200, 350),
				"end": point(600, 350),
			}],
		}],
	}
	if not require(
		V3_SAVE_FIXTURE.publish_payload(primary_slot_id, fixture),
		"Primary line fixture and manifest could not be published"):
		return false
	if not require(await V3_SAVE_FIXTURE.load_slot(save_manager, primary_slot_id), "Primary line fixture did not load"):
		return false
	await process_frame
	await process_frame
	return require(road_renderer.GetRenderedEdgeCount() == 1, "Primary line fixture did not render exactly one edge")

func verify_placement_crossing_and_history(road_builder: Node, road_renderer: Node) -> bool:
	if not require(road_builder.BeginPlace(Vector2(400, 200)), "Crossing placement did not begin"):
		return false
	road_builder.UpdatePlace(Vector2(400, 600))
	if not require(road_builder.CommitPlace(Vector2(400, 600)), "Crossing placement did not commit"):
		return false
	await process_frame
	var crossing_edge_count: int = road_renderer.GetRenderedEdgeCount()
	if not require(
		crossing_edge_count == 4,
		"Interior planar crossing produced %d edges instead of four" % crossing_edge_count):
		return false

	if not require(road_builder.BeginPlace(Vector2(800, 300)), "Continuous placement did not begin"):
		return false
	if not require(road_builder.AddPlacePoint(Vector2(900, 300)), "Continuous placement missed the first corner"):
		return false
	if not require(road_builder.AddPlacePoint(Vector2(900, 400)), "Continuous placement missed the second corner"):
		return false
	road_builder.UpdatePlace(Vector2(800, 400))
	if not require(road_renderer.GetPreviewPointCount() >= 4, "Continuous placement preview is incomplete"):
		return false
	if not require(road_builder.ConfirmPlace(Vector2(800, 400)), "Continuous placement did not commit"):
		return false
	await process_frame
	var continuous_edge_count: int = road_renderer.GetRenderedEdgeCount()
	if not require(continuous_edge_count == 5, "Continuous placement produced %d total edges instead of five" % continuous_edge_count):
		return false

	if not require(road_builder.BeginPlace(Vector2(1100, 300)), "Cancellation placement did not begin"):
		return false
	road_builder.UpdatePlace(Vector2(1200, 300))
	road_builder.CancelPlaceSession()
	await process_frame
	if not require(
		road_renderer.GetRenderedEdgeCount() == 5 and road_renderer.GetPreviewPointCount() == 0,
		"Cancelled placement changed the graph or retained its preview"):
		return false

	if not require(road_builder.BeginRemove(Vector2(300, 350), false), "Single-edge removal did not begin"):
		return false
	var single_selection_count: int = road_builder.GetRemovalSelectionCount()
	if not require(single_selection_count == 1, "Single-edge removal selected %d edges instead of one" % single_selection_count):
		return false
	if not require(road_builder.ConfirmRemove(Vector2(300, 350)), "Single-edge removal did not commit"):
		return false
	await process_frame
	if not require(road_renderer.GetRenderedEdgeCount() == 4, "Single-edge removal produced the wrong graph"):
		return false
	if not require(road_builder.UndoLastEdit(), "Single-edge removal could not be undone"):
		return false
	await process_frame
	if not require(road_renderer.GetRenderedEdgeCount() == 5, "Undo did not restore the crossing graph"):
		return false
	if not require(road_builder.RedoLastEdit(), "Single-edge removal could not be redone"):
		return false
	await process_frame
	if not require(road_renderer.GetRenderedEdgeCount() == 4, "Redo did not reproduce the single-edge removal"):
		return false
	if not require(road_builder.UndoLastEdit(), "Crossing graph could not be restored after redo"):
		return false
	await process_frame

	if not require(road_builder.BeginRemove(Vector2(780, 350), false), "Continuous removal cancellation did not begin"):
		return false
	road_builder.UpdateRemove(Vector2(920, 350))
	if not require(road_builder.GetRemovalSelectionCount() == 1, "Continuous removal did not select the maximal three-segment Edge"):
		return false
	road_builder.CancelRemoveSession()
	if not require(road_renderer.GetRenderedEdgeCount() == 5, "Cancelled continuous removal changed the graph"):
		return false
	if not require(road_builder.BeginRemove(Vector2(780, 350), false), "Continuous removal did not begin"):
		return false
	road_builder.UpdateRemove(Vector2(920, 350))
	if not require(road_builder.ConfirmRemove(Vector2(920, 350)), "Continuous removal did not commit"):
		return false
	await process_frame
	if not require(road_renderer.GetRenderedEdgeCount() == 4, "Continuous removal did not remove the maximal path Edge"):
		return false
	if not require(road_builder.UndoLastEdit(), "Continuous removal could not be undone"):
		return false
	await process_frame

	if not require(road_builder.BeginRemove(Vector2(450, 330), true), "Rectangle removal did not begin"):
		return false
	road_builder.UpdateRemove(Vector2(580, 370))
	if not require(road_builder.GetRemovalSelectionCount() == 1, "Rectangle removal selected an unexpected edge set"):
		return false
	if not require(road_builder.ConfirmRemove(Vector2(580, 370)), "Rectangle removal did not commit"):
		return false
	await process_frame
	if not require(road_renderer.GetRenderedEdgeCount() == 4, "Rectangle removal did not remove its selected edge"):
		return false
	if not require(road_builder.UndoLastEdit(), "Rectangle removal could not be undone"):
		return false
	await process_frame
	if not require(road_renderer.GetRenderedEdgeCount() == 5, "Rectangle undo did not restore the complete graph"):
		return false
	if not require(road_builder.RedoLastEdit(), "Rectangle removal could not be redone"):
		return false
	await process_frame
	if not require(road_renderer.GetRenderedEdgeCount() == 4, "Rectangle redo did not reproduce the deletion"):
		return false
	if not require(road_builder.UndoLastEdit(), "Final graph restoration could not be undone"):
		return false
	await process_frame
	return require(road_renderer.GetRenderedEdgeCount() == 5, "Final edit history state is not the complete crossing graph")

func verify_primary_slot(road_renderer: Node) -> bool:
	if not require(await V3_SAVE_FIXTURE.save(save_manager, primary_slot_id), "Primary slot overwrite failed"):
		return false
	var manifest: Variant = read_json(slot_path(primary_slot_id, "manifest.json"))
	if not require(manifest is Dictionary, "Primary manifest is not a JSON object"):
		return false
	var manifest_files: Array = manifest.get("files", [])
	if not require(
		manifest.get("formatFamily") == "simple-cities-v3" and
		manifest.get("schemaVersion") == 1 and
		manifest.get("displayName") == PRIMARY_DISPLAY_NAME and
		manifest_files.size() == 1 and
		manifest_files[0] is Dictionary and
		manifest_files[0].get("name") == "road_network.json" and
		int(manifest_files[0].get("encodedLength", -1)) > 0 and
		str(manifest_files[0].get("sha256", "")).length() == 64 and
		manifest.get("cityName") == "Unknown City" and
		manifest.get("population") == null and
		manifest.get("funds") == null and
		manifest.get("thumbnailFile") == null,
		"Primary manifest metadata or RoadGraph-only file list is wrong"):
		return false
	if not require_saved_counts(primary_slot_id, 7, 5, "Primary crossing slot"):
		return false
	return require(road_renderer.GetRenderedEdgeCount() == 5, "Saving the primary slot changed the rendered graph")

func verify_curve_slot(road_renderer: Node) -> bool:
	if not require(await V3_SAVE_FIXTURE.save_as(save_manager, PRIMARY_DISPLAY_NAME), "Duplicate named curve slot was not created"):
		return false
	curve_slot_id = save_manager.get("CurrentSlotID")
	if not require(curve_slot_id.begins_with("manual-") and curve_slot_id != primary_slot_id, "Duplicate display name reused an internal slot ID"):
		return false
	var curve_fixture := build_curve_fixture()
	if not require(
		V3_SAVE_FIXTURE.publish_payload(curve_slot_id, curve_fixture),
		"Curve fixture payload and manifest could not be published"):
		return false
	if not require(await V3_SAVE_FIXTURE.load_slot(save_manager, curve_slot_id), "Curve fixture slot did not load"):
		return false
	await process_frame
	await process_frame
	if not require(road_renderer.GetRenderedEdgeCount() == 6, "Renderer did not rebuild all six native geometry edges"):
		return false
	for edge_index in range(CURVE_KINDS.size()):
		var edge_id := 13 + edge_index
		var point_count: int = road_renderer.GetRenderedPointCount(edge_id)
		if not require(point_count == 2 if edge_index == 0 else point_count > 2, "Native geometry %s has an invalid display sample count" % CURVE_KINDS[edge_index]):
			return false
	if not require(await V3_SAVE_FIXTURE.save(save_manager, curve_slot_id), "Curve slot overwrite failed"):
		return false
	var saved_curve_payload: Variant = read_json(slot_path(curve_slot_id, "road_network.json"))
	if not require(saved_curve_payload is Dictionary, "Saved curve payload is not a JSON object"):
		return false
	var saved_kinds: Array[String] = []
	for edge: Variant in saved_curve_payload.get("edges", []):
		if edge is Dictionary and edge.get("geometry", []).size() == 1:
			saved_kinds.append(str(edge.geometry[0].get("kind", "")))
	if not require(saved_kinds == CURVE_KINDS, "Curve save did not preserve all native geometry kinds"):
		return false
	if not require(await V3_SAVE_FIXTURE.load_slot(save_manager, primary_slot_id), "Primary crossing slot did not reload after the curve slot"):
		return false
	await process_frame
	await process_frame
	if not require(road_renderer.GetRenderedEdgeCount() == 5, "Primary crossing graph did not survive switching named slots"):
		return false
	var deleted_curve_slot_id := curve_slot_id
	if not require(await V3_SAVE_FIXTURE.delete_slot(save_manager, deleted_curve_slot_id), "Curve slot could not be deleted"):
		return false
	curve_slot_id = ""
	return require(not save_manager.SaveSlotExists(deleted_curve_slot_id), "Deleted curve slot is still visible")

func verify_autosave_and_damaged_load(road_renderer: Node) -> bool:
	if not require(await V3_SAVE_FIXTURE.run_autosave(autosave_controller, save_manager), "Immediate autosave failed"):
		return false
	if not require(save_manager.get("CurrentSlotID") == primary_slot_id, "Autosave replaced the selected manual slot"):
		return false
	if not require(save_manager.SaveSlotExists(AUTOSAVE_SLOT_ID), "Autosave slot was not published"):
		return false
	if not require_saved_counts(AUTOSAVE_SLOT_ID, 7, 5, "Autosave crossing slot"):
		return false
	if not require(await V3_SAVE_FIXTURE.load_slot(save_manager, AUTOSAVE_SLOT_ID), "Autosave slot did not load"):
		return false
	await process_frame
	if not require(road_renderer.GetRenderedEdgeCount() == 5, "Autosave load did not restore the complete graph"):
		return false
	if not require(await V3_SAVE_FIXTURE.load_slot(save_manager, primary_slot_id), "Primary slot did not reload after autosave"):
		return false
	await process_frame

	if not require(await V3_SAVE_FIXTURE.save_as(save_manager, DAMAGED_DISPLAY_NAME), "Damaged-load fixture slot was not created"):
		return false
	damaged_slot_id = save_manager.get("CurrentSlotID")
	if not require(await V3_SAVE_FIXTURE.load_slot(save_manager, primary_slot_id), "Primary slot could not be selected before the damaged load"):
		return false
	if not require(
		V3_SAVE_FIXTURE.publish_payload_text(
			damaged_slot_id,
			'{"formatFamily":"simple-cities-v3","payloadType":"road-network","schemaVersion":1,"nextID":1,"nodes":[{"id":0,"x":0,"y":0}],"edges":[]}'),
		"Damaged RoadGraph fixture and manifest could not be published"):
		return false
	if not require(not await V3_SAVE_FIXTURE.load_slot(save_manager, damaged_slot_id), "Damaged RoadGraph payload was accepted"):
		return false
	if not require(save_manager.get("CurrentSlotID") == primary_slot_id, "Failed damaged load changed CurrentSlotID"):
		return false
	if not require(road_renderer.GetRenderedEdgeCount() == 5, "Failed damaged load changed the active graph"):
		return false
	if not require(await V3_SAVE_FIXTURE.delete_slot(save_manager, damaged_slot_id), "Damaged fixture slot could not be deleted"):
		return false
	damaged_slot_id = ""
	return true

func require_saved_counts(slot_id: String, node_count: int, edge_count: int, context: String) -> bool:
	var payload: Variant = read_json(slot_path(slot_id, "road_network.json"))
	if not require(payload is Dictionary, "%s payload is not a JSON object" % context):
		return false
	for edge: Variant in payload.get("edges", []):
		if not require(edge is Dictionary and edge.get("roadType", "") == "street", "%s contains a non-street Edge" % context):
			return false
	return require(
		payload.get("formatFamily", "") == "simple-cities-v3" and
		payload.get("payloadType", "") == "road-network" and
		payload.get("schemaVersion", -1) == 1 and
		payload.get("nodes", []).size() == node_count and
		payload.get("edges", []).size() == edge_count,
		"%s topology counts are wrong" % context)

func build_curve_fixture() -> Dictionary:
	var curvature := 0.005
	var arc_length := 250.0
	var heading_delta := curvature * arc_length
	var clothoid_start := Vector2(50.0, 50.0)
	var clothoid_end := clothoid_start + Vector2(
		sin(heading_delta) / curvature,
		(1.0 - cos(heading_delta)) / curvature)
	var definitions := [
		{"start": Vector2(-400, -250), "end": Vector2(-100, -250), "geometry": {"version": 1, "kind": "line", "start": point(-400, -250), "end": point(-100, -250)}},
		{"start": Vector2(-400, -150), "end": Vector2(-100, -150), "geometry": {"version": 1, "kind": "cubicBezier", "start": point(-400, -150), "control1": point(-400, -250), "control2": point(-100, -50), "end": point(-100, -150)}},
		{"start": Vector2(50, -250), "end": Vector2(350, -250), "geometry": {"version": 1, "kind": "cubicHermite", "start": point(50, -250), "startTangent": point(300, 200), "end": point(350, -250), "endTangent": point(300, -200)}},
		{"start": Vector2(-400, 50), "end": Vector2(-100, 50), "geometry": {"version": 1, "kind": "circularArc", "start": point(-400, 50), "end": point(-100, 50), "center": point(-250, 50), "radius": 150.0, "startAngle": PI, "endAngle": 0.0, "sweepAngle": PI}},
		{"start": clothoid_start, "end": clothoid_end, "geometry": {"version": 1, "kind": "clothoid", "start": point(clothoid_start.x, clothoid_start.y), "end": point(clothoid_end.x, clothoid_end.y), "startHeading": 0.0, "reverseStartHeading": fposmod(heading_delta + PI, TAU), "startCurvature": curvature, "endCurvature": curvature, "arcLength": arc_length}},
		{"start": Vector2(-50, 250), "end": Vector2(350, 250), "geometry": {"version": 1, "kind": "rationalQuadratic", "start": point(-50, 250), "startWeight": 1.0, "control1": point(150, 80), "controlWeight": 0.6, "end": point(350, 250), "endWeight": 1.0}},
	]
	var nodes := []
	var edges := []
	for index in range(definitions.size()):
		var definition: Dictionary = definitions[index]
		var node_a_id := index * 2 + 1
		var node_b_id := node_a_id + 1
		var edge_id := 13 + index
		var start: Vector2 = definition.start
		var end: Vector2 = definition.end
		nodes.append({"id": node_a_id, "x": start.x, "y": start.y})
		nodes.append({"id": node_b_id, "x": end.x, "y": end.y})
		edges.append({"id": edge_id, "nodeAID": node_a_id, "nodeBID": node_b_id, "roadType": "street", "geometry": [definition.geometry]})
	return {
		"formatFamily": "simple-cities-v3",
		"payloadType": "road-network",
		"schemaVersion": 1,
		"nextID": 19,
		"nodes": nodes,
		"edges": edges,
	}

func prepare_autosave_backup() -> bool:
	var autosave_absolute := ProjectSettings.globalize_path(AUTOSAVE_PATH)
	var backup_absolute := ProjectSettings.globalize_path(AUTOSAVE_BACKUP_PATH)
	if DirAccess.dir_exists_absolute(backup_absolute):
		if DirAccess.dir_exists_absolute(autosave_absolute):
			return false
		if DirAccess.rename_absolute(backup_absolute, autosave_absolute) != OK:
			return false
	if not DirAccess.dir_exists_absolute(autosave_absolute):
		return true
	if DirAccess.rename_absolute(autosave_absolute, backup_absolute) != OK:
		return false
	autosave_was_backed_up = true
	return true

func restore_autosave_backup() -> bool:
	var autosave_absolute := ProjectSettings.globalize_path(AUTOSAVE_PATH)
	var backup_absolute := ProjectSettings.globalize_path(AUTOSAVE_BACKUP_PATH)
	if not autosave_was_backed_up:
		return not DirAccess.dir_exists_absolute(backup_absolute)
	if DirAccess.dir_exists_absolute(autosave_absolute):
		return false
	if DirAccess.rename_absolute(backup_absolute, autosave_absolute) != OK:
		return false
	autosave_was_backed_up = false
	return true

func cleanup_slots() -> bool:
	for slot_id in [damaged_slot_id, curve_slot_id, primary_slot_id, AUTOSAVE_SLOT_ID]:
		if slot_id.is_empty() or not save_manager.SaveSlotExists(slot_id):
			continue
		if not require(await V3_SAVE_FIXTURE.delete_slot(save_manager, slot_id), "Final contract could not delete slot %s" % slot_id):
			return false
	damaged_slot_id = ""
	curve_slot_id = ""
	primary_slot_id = ""
	return true

func cleanup_after_failure() -> void:
	if save_manager != null:
		for slot_id in [damaged_slot_id, curve_slot_id, primary_slot_id, AUTOSAVE_SLOT_ID]:
			if not slot_id.is_empty() and save_manager.SaveSlotExists(slot_id):
				await V3_SAVE_FIXTURE.delete_slot(save_manager, slot_id)
	if test_map != null:
		test_map.queue_free()
	restore_autosave_backup()
	quit(1)

func slot_path(slot_id: String, file_name: String) -> String:
	return "user://saves-v3/%s/%s" % [slot_id, file_name]

func read_json(path: String) -> Variant:
	if not FileAccess.file_exists(path):
		return null
	return JSON.parse_string(FileAccess.get_file_as_string(path))

func point(x: float, y: float) -> Dictionary:
	return {"x": x, "y": y}

func require(condition: bool, message: String) -> bool:
	if condition:
		return true
	push_error(message)
	if not failure_cleanup_started:
		failure_cleanup_started = true
		cleanup_after_failure.call_deferred()
	return false
