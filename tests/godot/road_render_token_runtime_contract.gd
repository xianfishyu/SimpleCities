extends SceneTree

const MAP_SCENE := "res://Scenes/MapTest.tscn"
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
		await V3_SAVE_FIXTURE.load_slot(save_manager, slot_id),
		"Render token fixture did not load"):
		return
	await process_frame
	var loaded := presentation_token(renderer, "Aggregate Load")
	if loaded.is_empty() or not require_load_change(styled, loaded):
		return
	if not require_surface_hit(renderer, Vector2(-50.0, 100.0), loaded, "Aggregate Load"):
		return
	if not require_terminal_cap_hit(renderer, Vector2(-108.0, 100.0), loaded, "Aggregate Load"):
		return
	if not require_semantic_join_hit(renderer, Vector2(5.0, 95.0), loaded, "Aggregate Load"):
		return
	if not require_junction_patch_hit(renderer, Vector2(300.0, 100.0), loaded, "Aggregate Load"):
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
	return recovered

func restore_mutated_style() -> void:
	if mutated_style == null:
		return
	mutated_style.set("Width", original_style_width)
	mutated_style = null

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
