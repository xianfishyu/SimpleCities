extends SceneTree

const CATALOG_PATH := "res://Scenes/UI/RoadsConstructionCategory.tres"
const EXPECTED_TOOLS := {
	"city-road": {"display_name": "城市道路", "shortcut_hint": "", "tool_type": 1, "sort_order": 10, "icon_path": "res://Assets/UI/Icons/construction-road.svg"},
	"road-upgrade": {"display_name": "道路改造", "shortcut_hint": "T", "tool_type": 3, "sort_order": 20, "icon_path": "res://Assets/UI/Icons/construction-road-upgrade.svg"},
}

func _initialize() -> void:
	var category := load(CATALOG_PATH)
	if category == null:
		fail("Could not load %s" % CATALOG_PATH)
		return

	var validation: Dictionary = category.GetValidationResult()
	if not validation.get("valid", false):
		fail("Production validation failed: %s" % validation.get("error", "missing error"))
		return

	if category.Id != "roads" or category.DisplayName != "道路":
		fail("Unexpected category identity: %s/%s" % [category.Id, category.DisplayName])
		return

	if category.Tools.size() != EXPECTED_TOOLS.size():
		fail("Expected city road and road upgrade tools, got %d" % category.Tools.size())
		return

	var previous_sort_order := -1
	for tool in category.Tools:
		if not EXPECTED_TOOLS.has(tool.Id):
			fail("Unexpected tool ID: %s" % tool.Id)
			return
		var expected: Dictionary = EXPECTED_TOOLS[tool.Id]
		if tool.DisplayName != expected.display_name or tool.ShortcutHint != expected.shortcut_hint:
			fail("Unexpected display data for tool %s" % tool.Id)
			return
		if tool.ToolType != expected.tool_type:
			fail("Tool %s maps to ToolType %d" % [tool.Id, tool.ToolType])
			return
		if tool.SortOrder != expected.sort_order or tool.Description.is_empty():
			fail("Unexpected ordering or description for tool %s" % tool.Id)
			return
		if tool.SortOrder <= previous_sort_order:
			fail("Catalog tools are not stored in ascending SortOrder")
			return
		previous_sort_order = tool.SortOrder
		if tool.Icon == null or tool.Icon.resource_path != expected.icon_path:
			fail("Tool %s does not use its production icon" % tool.Id)
			return

	var empty_id_category := category.duplicate(true)
	empty_id_category.Tools[0].Id = ""
	if empty_id_category.GetValidationResult().get("valid", true):
		fail("Production validation accepted an empty tool ID")
		return

	var duplicate_id_category := category.duplicate(true)
	duplicate_id_category.Tools.append(duplicate_id_category.Tools[0].duplicate(true))
	if duplicate_id_category.GetValidationResult().get("valid", true):
		fail("Production validation accepted duplicate tool IDs")
		return

	var duplicate_type_category := category.duplicate(true)
	duplicate_type_category.Tools[1].ToolType = duplicate_type_category.Tools[0].ToolType
	var duplicate_type_result: Dictionary = duplicate_type_category.GetValidationResult()
	if duplicate_type_result.get("valid", true):
		fail("Production validation accepted ToolType values %s/%s: %s" % [duplicate_type_category.Tools[0].ToolType, duplicate_type_category.Tools[1].ToolType, duplicate_type_result.get("error", "missing error")])
		return

	var duplicate_order_category := category.duplicate(true)
	duplicate_order_category.Tools[1].SortOrder = duplicate_order_category.Tools[0].SortOrder
	var duplicate_order_result: Dictionary = duplicate_order_category.GetValidationResult()
	if duplicate_order_result.get("valid", true):
		fail("Production validation accepted SortOrder values %s/%s: %s" % [duplicate_order_category.Tools[0].SortOrder, duplicate_order_category.Tools[1].SortOrder, duplicate_order_result.get("error", "missing error")])
		return

	var empty_ref_category := category.duplicate(true)
	empty_ref_category.Tools[0] = null
	if empty_ref_category.GetValidationResult().get("valid", true):
		fail("Production validation accepted an empty tool reference")
		return

	print("PASS roads construction category contract")
	quit(0)

func fail(message: String) -> void:
	push_error(message)
	quit(1)
