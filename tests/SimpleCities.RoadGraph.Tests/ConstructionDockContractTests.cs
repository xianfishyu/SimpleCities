using System.IO;

namespace SimpleCities.Tests;

public sealed class ConstructionDockContractTests
{
    private static readonly (string NodeName, string Text, string IconPath)[] CategoryButtons =
    [
        ("RoadsCategoryButton", "道路", "res://Assets/UI/Icons/construction-road.svg"),
        ("ZoningCategoryButton", "区域", "res://Assets/UI/Icons/construction-zoning.svg"),
        ("FacilitiesCategoryButton", "公共设施", "res://Assets/UI/Icons/construction-facilities.svg"),
        ("TransitCategoryButton", "交通", "res://Assets/UI/Icons/construction-transit.svg"),
        ("LandscapingCategoryButton", "景观", "res://Assets/UI/Icons/construction-landscaping.svg"),
    ];

    private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        ".."));
    private static readonly string DockScenePath = Path.Combine(ProjectRoot, "Scenes", "UI", "ConstructionDock.tscn");
    private static readonly string DockScriptPath = Path.Combine(ProjectRoot, "Scripts", "UI", "ConstructionDock.cs");
    private static readonly string RoadsCategoryPath = Path.Combine(ProjectRoot, "Scenes", "UI", "RoadsConstructionCategory.tres");
    private static readonly string DockButtonScenePath = Path.Combine(ProjectRoot, "Scenes", "UI", "ConstructionDockButton.tscn");

    [Fact]
    public void ConstructionDockScene_LoadsStandaloneControlWithRequiredResources()
    {
        Assert.True(File.Exists(DockScenePath), $"Missing scene: {DockScenePath}");
        Assert.True(File.Exists(DockScriptPath), $"Missing script: {DockScriptPath}");
        Assert.True(typeof(ConstructionDock).IsSubclassOf(typeof(Godot.Control)));

        string scene = File.ReadAllText(DockScenePath);
        Assert.Contains("res://Scripts/UI/ConstructionDock.cs", scene, StringComparison.Ordinal);
        Assert.Contains("res://Scenes/UI/Themes/ConstructionDockTheme.tres", scene, StringComparison.Ordinal);
        Assert.DoesNotContain("res://Scenes/UI/Themes/CommandCenterTheme.tres", scene, StringComparison.Ordinal);
        Assert.Contains("res://Scenes/UI/RoadsConstructionCategory.tres", scene, StringComparison.Ordinal);
        Assert.Contains("res://Scenes/UI/ConstructionDockButton.tscn", scene, StringComparison.Ordinal);
        Assert.Contains("Category = ExtResource", scene, StringComparison.Ordinal);
    }

    [Fact]
    public void ConstructionDockScene_ContainsFiveEnabledFocusableCategoriesAndPreservedCatalogBehavior()
    {
        string scene = File.ReadAllText(DockScenePath);
        string script = File.ReadAllText(DockScriptPath);

        Assert.Contains("name=\"ToolTray\"", scene, StringComparison.Ordinal);
        Assert.Contains("RoadToolButton", script, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectToolButton", script, StringComparison.Ordinal);
        Assert.DoesNotContain("RoadRemoveToolButton", script, StringComparison.Ordinal);
        Assert.Contains("[ToolType.Select] = new(\"选择\", \"查看当前状态。\", string.Empty)", script, StringComparison.Ordinal);
        Assert.Contains("[ToolType.RoadRemove] = new(\"拆路\", \"点击已有道路进行拆除。\", string.Empty)", script, StringComparison.Ordinal);
        Assert.Contains("_shortcutRow.Visible = !string.IsNullOrWhiteSpace", File.ReadAllText(Path.Combine(ProjectRoot, "Scripts", "UI", "ToolContextPanel.cs")), StringComparison.Ordinal);

        foreach ((string nodeName, string text, _) in CategoryButtons)
        {
            string block = ExtractNodeBlock(scene, nodeName);
            Assert.DoesNotContain("disabled = true", block, StringComparison.Ordinal);
            Assert.DoesNotContain("focus_mode = 0", block, StringComparison.Ordinal);
            Assert.True(
                block.Contains($"text = \"{text}\"", StringComparison.Ordinal) ||
                block.Contains($"DisplayText = \"{text}\"", StringComparison.Ordinal),
                $"Category {nodeName} must preserve the exact label '{text}'.");
        }

        Assert.Contains("ResidentialZonePlaceholder", script, StringComparison.Ordinal);
        Assert.Contains("CommercialZonePlaceholder", script, StringComparison.Ordinal);
        Assert.Contains("SchoolPlaceholder", script, StringComparison.Ordinal);
        Assert.Contains("ClinicPlaceholder", script, StringComparison.Ordinal);
        Assert.Contains("BusStopPlaceholder", script, StringComparison.Ordinal);
        Assert.Contains("MetroStationPlaceholder", script, StringComparison.Ordinal);
        Assert.Contains("ParkPlaceholder", script, StringComparison.Ordinal);
        Assert.Contains("PlazaPlaceholder", script, StringComparison.Ordinal);
        Assert.Contains("Disabled = true", script, StringComparison.Ordinal);
        Assert.Contains("FocusMode = FocusModeEnum.None", script, StringComparison.Ordinal);
        Assert.Contains("TooltipText = \"尚未开放\"", script, StringComparison.Ordinal);

        Assert.DoesNotContain("ZoningConstructionCategory", scene, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FacilitiesConstructionCategory", scene, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TransitConstructionCategory", scene, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LandscapingConstructionCategory", scene, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConstructionDockButtonScene_DefaultsToEnabledAndFocusable()
    {
        Assert.True(File.Exists(DockButtonScenePath), $"Missing reusable category button scene: {DockButtonScenePath}");
        string button = ExtractNodeBlock(File.ReadAllText(DockButtonScenePath), "ConstructionDockButton");

        Assert.DoesNotContain("disabled = true", button, StringComparison.Ordinal);
        Assert.Contains("focus_mode = 2", button, StringComparison.Ordinal);
    }

    [Fact]
    public void ConstructionDockScene_UsesKResourcesWithoutRuntimeConceptCoupling()
    {
        string scene = File.ReadAllText(DockScenePath);
        string script = File.ReadAllText(DockScriptPath);
        string category = File.ReadAllText(RoadsCategoryPath);

        foreach ((_, _, string iconPath) in CategoryButtons)
        {
            string diskPath = Path.Combine(ProjectRoot, iconPath[6..].Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(diskPath), $"Missing K icon resource: {iconPath}");
            Assert.Contains($"path=\"{iconPath}\"", scene, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("res://docs/ui/concepts/", scene, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("res://docs/ui/concepts/", category, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("res://docs/ui/concepts/", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("res://Assets/UI/Icons/", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ConstructionDockScene_IsFullWidthBottomFlushWithExactKGeometry()
    {
        string scene = File.ReadAllText(DockScenePath);
        string script = File.ReadAllText(DockScriptPath);
        string dock = ExtractNodeBlock(scene, "ConstructionDock");

        Assert.Contains("anchor_left = 0.0", dock, StringComparison.Ordinal);
        Assert.Contains("anchor_right = 1.0", dock, StringComparison.Ordinal);
        Assert.Contains("anchor_bottom = 1.0", dock, StringComparison.Ordinal);
        Assert.Contains("offset_left = 0.0", dock, StringComparison.Ordinal);
        Assert.Contains("offset_right = 0.0", dock, StringComparison.Ordinal);
        Assert.Contains("offset_bottom = 0.0", dock, StringComparison.Ordinal);
        Assert.DoesNotContain("MaximumWidth", script, StringComparison.Ordinal);
        Assert.Contains("CollapsedHeight = 76", script, StringComparison.Ordinal);
        Assert.Contains("ExpandedHeight = 140", script, StringComparison.Ordinal);

        string categoryScroll = ExtractNodeBlock(scene, "CategoryScroll");
        string categoryBar = ExtractNodeBlock(scene, "CategoryBar");
        Assert.Contains("custom_minimum_size = Vector2(0, 76)", categoryScroll, StringComparison.Ordinal);
        Assert.Contains("horizontal_scroll_mode = 3", categoryScroll, StringComparison.Ordinal);
        Assert.Contains("vertical_scroll_mode = 0", categoryScroll, StringComparison.Ordinal);
        Assert.Contains("custom_minimum_size = Vector2(552, 76)", categoryBar, StringComparison.Ordinal);
        Assert.Contains("custom_minimum_size = Vector2(0, 64)", ExtractNodeBlock(scene, "ToolTray"), StringComparison.Ordinal);
        Assert.Contains("custom_minimum_size = Vector2(0, 64)", ExtractNodeBlock(scene, "ToolScroll"), StringComparison.Ordinal);
    }

    [Fact]
    public void ConstructionDockScene_UsesHorizontalToolTrayAndReusableTexturedCategoryButtons()
    {
        string scene = File.ReadAllText(DockScenePath);

        string scroll = ExtractNodeBlock(scene, "ToolScroll");
        Assert.Contains(ReadScrollMode(scroll, "horizontal_scroll_mode"), new[] { 1, 2, 3, 4 });
        Assert.Equal(0, ReadScrollMode(scroll, "vertical_scroll_mode"));
        Assert.Contains("name=\"ToolList\" type=\"HBoxContainer\"", scene, StringComparison.Ordinal);
        Assert.Contains("alignment = 1", ExtractNodeBlock(scene, "ToolList"), StringComparison.Ordinal);
        Assert.True(File.Exists(DockButtonScenePath), $"Missing reusable category button scene: {DockButtonScenePath}");

        string buttonSceneId = ExtractUniqueExtResourceId(scene, "res://Scenes/UI/ConstructionDockButton.tscn");

        foreach ((string nodeName, string text, string iconPath) in CategoryButtons)
        {
            string block = ExtractNodeBlock(scene, nodeName);
            string iconId = ExtractUniqueExtResourceId(scene, iconPath);
            Assert.Contains($"instance=ExtResource(\"{buttonSceneId}\")", block, StringComparison.Ordinal);
            Assert.DoesNotContain($"text = \"{text}\"", block, StringComparison.Ordinal);
            Assert.Contains($"DisplayText = \"{text}\"", block, StringComparison.Ordinal);
            Assert.Contains($"IconTexture = ExtResource(\"{iconId}\")", block, StringComparison.Ordinal);
            Assert.DoesNotContain("disabled = true", block, StringComparison.Ordinal);
            Assert.DoesNotContain("focus_mode = 0", block, StringComparison.Ordinal);
            Assert.DoesNotContain("focus_mode = 1", block, StringComparison.Ordinal);
            string[] focusOverrides = ReadPropertyLines(block, "focus_mode");
            if (focusOverrides.Length == 1)
                Assert.Equal("focus_mode = 2", focusOverrides[0]);
            else
                Assert.Empty(focusOverrides);
        }

        Assert.DoesNotContain("name=\"CurrentToolLabel\"", scene, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"SelectToolButton\"", scene, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"RoadRemoveToolButton\"", scene, StringComparison.Ordinal);
    }

    [Fact]
    public void ConstructionDockButtonScene_AnchorsPrimaryIndicatorAtDockBottom()
    {
        string scene = File.ReadAllText(DockButtonScenePath);
        string indicator = ExtractNodeBlock(scene, "PrimarySelectionIndicator");

        Assert.Contains("parent=\".\"", indicator, StringComparison.Ordinal);
        Assert.Contains("anchor_top = 1.0", indicator, StringComparison.Ordinal);
        Assert.Contains("anchor_right = 1.0", indicator, StringComparison.Ordinal);
        Assert.Contains("anchor_bottom = 1.0", indicator, StringComparison.Ordinal);
        Assert.Contains("offset_top = -4.0", indicator, StringComparison.Ordinal);
        Assert.Contains("offset_bottom = 0.0", indicator, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedUnderline", scene, StringComparison.Ordinal);
    }

    [Fact]
    public void ConstructionDockScript_KeepsNativeButtonTextEmptyForCustomDockButtons()
    {
        string script = File.ReadAllText(DockScriptPath);

        Assert.DoesNotContain("button.Text = category.DisplayName", script, StringComparison.Ordinal);
        Assert.DoesNotContain("\n                Text = placeholder.DisplayName", script, StringComparison.Ordinal);
        Assert.DoesNotContain("\n                Text = ToolButtonText(tool)", script, StringComparison.Ordinal);
        Assert.Contains("dockButton.DisplayText = category.DisplayName", script, StringComparison.Ordinal);
        Assert.Contains("DisplayText = placeholder.DisplayName", script, StringComparison.Ordinal);
        Assert.Contains("DisplayText = tool.DisplayName", script, StringComparison.Ordinal);
    }

    [Fact]
    public void RoadsCatalog_ContainsOnlyCityRoadMappedToRoadTool()
    {
        string category = File.ReadAllText(RoadsCategoryPath);
        string toolScriptId = ExtractUniqueExtResourceId(category, "res://Scripts/UI/ConstructionToolDefinition.cs");
        string[] toolBlocks = ExtractSubResourceBlocks(category)
            .Where(block => block.Contains($"script = ExtResource(\"{toolScriptId}\")", StringComparison.Ordinal))
            .ToArray();
        string toolBlock = Assert.Single(toolBlocks);
        string toolResourceId = ExtractHeaderAttribute(toolBlock, "id");
        string toolsLine = Assert.Single(ReadPropertyLines(category, "Tools"));
        string roadIconId = ExtractUniqueExtResourceId(category, "res://Assets/UI/Icons/construction-road.svg");

        Assert.Contains("Id = \"city-road\"", toolBlock, StringComparison.Ordinal);
        Assert.Contains("DisplayName = \"城市道路\"", toolBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("ShortcutHint =", toolBlock, StringComparison.Ordinal);
        Assert.Contains("ToolType = 1", toolBlock, StringComparison.Ordinal);
        Assert.Contains($"Icon = ExtResource(\"{roadIconId}\")", toolBlock, StringComparison.Ordinal);
        Assert.Equal($"Tools = Array[ExtResource(\"{toolScriptId}\")]([SubResource(\"{toolResourceId}\")])", toolsLine);
        Assert.Single(ExtractSubResourceReferences(toolsLine));
        Assert.DoesNotContain("Id = \"select\"", category, StringComparison.Ordinal);
        Assert.DoesNotContain("Id = \"road-remove\"", category, StringComparison.Ordinal);
        Assert.DoesNotContain("ToolType = 0", category, StringComparison.Ordinal);
        Assert.DoesNotContain("ToolType = 2", category, StringComparison.Ordinal);
    }

    private static string ExtractNodeBlock(string scene, string nodeName)
    {
        string header = $"[node name=\"{nodeName}\"";
        int start = scene.IndexOf(header, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing node block for {nodeName}");

        int next = scene.IndexOf("\n[node ", start + header.Length, StringComparison.Ordinal);
        return next < 0 ? scene[start..] : scene[start..next];
    }

    private static string ExtractUniqueExtResourceId(string source, string path)
    {
        string marker = $"path=\"{path}\"";
        string declaration = Assert.Single(
            source.Split('\n'),
            line => line.StartsWith("[ext_resource ", StringComparison.Ordinal) && line.Contains(marker, StringComparison.Ordinal));
        return ExtractHeaderAttribute(declaration, "id");
    }

    private static string ExtractHeaderAttribute(string header, string attribute)
    {
        string marker = $" {attribute}=\"";
        int valueStart = header.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(valueStart >= 0, $"Missing {attribute} attribute in: {header}");
        valueStart += marker.Length;
        int valueEnd = header.IndexOf('"', valueStart);
        Assert.True(valueEnd > valueStart, $"Malformed {attribute} attribute in: {header}");
        return header[valueStart..valueEnd];
    }

    private static string[] ExtractSubResourceBlocks(string source)
    {
        return source.Split("\n[sub_resource ", StringSplitOptions.None)
            .Skip(1)
            .Select(block => "[sub_resource " + block.Split("\n[", 2, StringSplitOptions.None)[0])
            .ToArray();
    }

    private static string[] ReadPropertyLines(string source, string propertyName)
    {
        string prefix = propertyName + " = ";
        return source.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
    }

    private static string[] ExtractSubResourceReferences(string propertyLine)
    {
        return propertyLine.Split("SubResource(\"", StringSplitOptions.None).Skip(1).Select(part => part.Split('\"')[0]).ToArray();
    }

    private static int ReadScrollMode(string nodeBlock, string propertyName)
    {
        string line = Assert.Single(ReadPropertyLines(nodeBlock, propertyName));
        string serializedValue = line[(propertyName.Length + 3)..];
        Assert.True(int.TryParse(serializedValue, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int value),
            $"{propertyName} must be one exact serialized Godot ScrollMode integer, but was '{serializedValue}'.");
        return value;
    }

}
