class_name V3SaveFixture
extends RefCounted

const PAYLOAD_FILE_NAME := "road_network.json"
const RESULT_SUCCEEDED := 0
const RESULT_SUCCEEDED_WITH_WARNINGS := 1
const DEFAULT_OPERATION_TIMEOUT_SECONDS := 120.0

static func save_as(save_manager: Node, display_name: String) -> bool:
	return await operation_succeeded(save_manager, save_manager.StartSaveAs(display_name))

static func save(save_manager: Node, slot_id: String) -> bool:
	return await operation_succeeded(save_manager, save_manager.StartSave(slot_id))

static func load_slot(save_manager: Node, slot_id: String) -> bool:
	return await operation_succeeded(save_manager, save_manager.StartLoad(slot_id))

static func run_autosave(controller: Node, save_manager: Node) -> bool:
	return await operation_succeeded(save_manager, controller.RunAutosaveNow())

static func delete_slot(save_manager: Node, slot_id: String) -> bool:
	var operation_token: String = save_manager.RequestDeleteSlot(slot_id)
	if operation_token.is_empty():
		return false
	var started_token: String = save_manager.StartDeleteSlot(slot_id, operation_token)
	return await operation_succeeded(save_manager, started_token)

static func operation_succeeded(save_manager: Node, operation_token: String) -> bool:
	var result := await wait_for_operation(save_manager, operation_token)
	if not await wait_for_idle(save_manager):
		print("SAVE_OPERATION_FAILURE idle-timeout token=%s" % operation_token)
		return false
	var result_kind := int(result.get("resultKind", -1))
	var succeeded := result_kind == RESULT_SUCCEEDED or result_kind == RESULT_SUCCEEDED_WITH_WARNINGS
	if not succeeded:
		print("SAVE_OPERATION_FAILURE %s" % JSON.stringify(result))
	return succeeded

static func wait_for_idle(
	save_manager: Node,
	timeout_seconds := DEFAULT_OPERATION_TIMEOUT_SECONDS) -> bool:
	var deadline_msec := Time.get_ticks_msec() + int(timeout_seconds * 1000.0)
	var idle_frames := 0
	while Time.get_ticks_msec() < deadline_msec:
		if not is_instance_valid(save_manager):
			return false
		if not bool(save_manager.get("IsOperationBusy")):
			idle_frames += 1
			if idle_frames >= 3:
				return true
		else:
			idle_frames = 0
		await save_manager.get_tree().process_frame
	return false

static func wait_for_operation(
	save_manager: Node,
	operation_token: String,
	timeout_seconds := DEFAULT_OPERATION_TIMEOUT_SECONDS) -> Dictionary:
	if operation_token.is_empty() or not is_instance_valid(save_manager):
		return {}
	var deadline_msec := Time.get_ticks_msec() + int(timeout_seconds * 1000.0)
	while Time.get_ticks_msec() < deadline_msec:
		if not is_instance_valid(save_manager):
			return {}
		if save_manager.HasOperationResult(operation_token):
			var result: Dictionary = save_manager.GetOperationResult(operation_token)
			if not result.is_empty():
				return result
		await save_manager.get_tree().process_frame
	return {}

static func publish_payload(slot_id: String, payload: Dictionary) -> bool:
	return publish_payload_text(slot_id, JSON.stringify(payload))

static func publish_payload_text(slot_id: String, payload_text: String) -> bool:
	var payload_path := slot_path(slot_id, PAYLOAD_FILE_NAME)
	var payload_file := FileAccess.open(payload_path, FileAccess.WRITE)
	if payload_file == null:
		return false
	payload_file.store_string(payload_text)
	payload_file.close()
	return refresh_manifest_payload(slot_id)

static func refresh_manifest_payload(slot_id: String) -> bool:
	var payload_path := slot_path(slot_id, PAYLOAD_FILE_NAME)
	var payload_file := FileAccess.open(payload_path, FileAccess.READ)
	if payload_file == null:
		return false
	var payload_length := payload_file.get_length()
	var hash_context := HashingContext.new()
	if hash_context.start(HashingContext.HASH_SHA256) != OK:
		payload_file.close()
		return false
	while payload_file.get_position() < payload_length:
		var remaining := payload_length - payload_file.get_position()
		var chunk := payload_file.get_buffer(mini(64 * 1024, remaining))
		if chunk.is_empty() or hash_context.update(chunk) != OK:
			payload_file.close()
			return false
	payload_file.close()
	var payload_sha256 := hash_context.finish().hex_encode()

	var manifest_path := slot_path(slot_id, "manifest.json")
	var manifest: Variant = JSON.parse_string(FileAccess.get_file_as_string(manifest_path))
	if not manifest is Dictionary:
		return false
	manifest["schemaVersion"] = int(manifest.get("schemaVersion", -1))
	var files: Array = manifest.get("files", [])
	if files.size() != 1 or not files[0] is Dictionary:
		return false

	var file_entry: Dictionary = files[0]
	file_entry["name"] = PAYLOAD_FILE_NAME
	file_entry["encodedLength"] = payload_length
	file_entry["sha256"] = payload_sha256
	files[0] = file_entry
	manifest["files"] = files

	var manifest_file := FileAccess.open(manifest_path, FileAccess.WRITE)
	if manifest_file == null:
		return false
	manifest_file.store_string(JSON.stringify(manifest))
	manifest_file.close()
	return true

static func slot_path(slot_id: String, file_name: String) -> String:
	return "user://saves-v3/%s/%s" % [slot_id, file_name]
