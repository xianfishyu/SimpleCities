using System;
using System.IO;

namespace SimpleCities.Tests;

public sealed class RoadTypeSelectorContractTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        ".."));

    private static readonly string ScenePath = Path.Combine(ProjectRoot, "Scenes", "UI", "GameHUD.tscn");
    private static readonly string PanelPath = Path.Combine(ProjectRoot, "Scripts", "UI", "ToolContextPanel.cs");
    private static readonly string HudPath = Path.Combine(ProjectRoot, "Scripts", "UI", "GameHUD.cs");
    private static readonly string ToolManagerPath = Path.Combine(ProjectRoot, "Scripts", "Tools", "ToolManager.cs");
    private static readonly string BuilderPath = Path.Combine(ProjectRoot, "Scripts", "Road", "RoadBuilder.cs");

    [Fact]
    public void GameHudScene_DeclaresFourRoadTypeSegmentsWithSwatches()
    {
        string scene = File.ReadAllText(ScenePath);

        Assert.Contains("name=\"RoadTypeRow\" type=\"VBoxContainer\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"RoadTypeSelector\" type=\"HBoxContainer\"", scene, StringComparison.Ordinal);

        foreach (string buttonName in new[] { "DirtButton", "StreetButton", "ArterialButton", "HighwayButton" })
        {
            string block = ExtractNodeBlock(scene, buttonName);
            Assert.Contains("type=\"Button\"", block, StringComparison.Ordinal);
            Assert.Contains("toggle_mode = true", block, StringComparison.Ordinal);
            Assert.Contains("focus_mode = 2", block, StringComparison.Ordinal);
            Assert.Contains("name=\"Swatch\" type=\"ColorRect\"", scene, StringComparison.Ordinal);
        }

        string streetBlock = ExtractNodeBlock(scene, "StreetButton");
        Assert.Contains("button_pressed = true", streetBlock, StringComparison.Ordinal);
        Assert.Contains("name=\"RoadTypeStatus\" type=\"Label\"", scene, StringComparison.Ordinal);
    }

    [Fact]
    public void ToolContextPanel_UsesValidatedStylesAndOnlySharedRoadTypeState()
    {
        string panel = File.ReadAllText(PanelPath);

        Assert.Contains("ConfigureRoadTypeState", panel, StringComparison.Ordinal);
        Assert.Contains("RoadTypeOrder", panel, StringComparison.Ordinal);
        Assert.Contains("ButtonGroup", panel, StringComparison.Ordinal);
        Assert.Contains("ToolType.RoadUpgrade", panel, StringComparison.Ordinal);
        Assert.Contains("TryValidateRoadTypeStyles", panel, StringComparison.Ordinal);
        Assert.Contains("TrySetSelectedRoadType", panel, StringComparison.Ordinal);
        Assert.Contains("button.Disabled = !_roadTypeSelectorAvailable", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("SubmitPath", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("ChangeRoadType", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAllEdges", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("RemoveEdges", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void GameHudAndToolManager_InjectRoadBuilderStateWithoutGraphCommands()
    {
        string hud = File.ReadAllText(HudPath);
        string toolManager = File.ReadAllText(ToolManagerPath);
        string builder = File.ReadAllText(BuilderPath);

        Assert.Contains("_toolContextPanel.ConfigureRoadTypeState", hud, StringComparison.Ordinal);
        Assert.Contains("_toolManager.GetSelectedRoadType", hud, StringComparison.Ordinal);
        Assert.Contains("_toolManager.SetSelectedRoadType", hud, StringComparison.Ordinal);
        Assert.Contains("public RoadType GetSelectedRoadType()", toolManager, StringComparison.Ordinal);
        Assert.Contains("_roadBuilder?.SetSelectedRoadType(roadType)", toolManager, StringComparison.Ordinal);
        Assert.DoesNotContain("SubmitPath", toolManager, StringComparison.Ordinal);
        Assert.DoesNotContain("ChangeRoadType", toolManager, StringComparison.Ordinal);
        Assert.Contains("public RoadType SelectedRoadType", builder, StringComparison.Ordinal);
        Assert.Contains("= RoadType.Street", builder, StringComparison.Ordinal);
    }

    private static string ExtractNodeBlock(string scene, string nodeName)
    {
        string header = $"[node name=\"{nodeName}\"";
        int start = scene.IndexOf(header, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing node block for {nodeName}");

        int next = scene.IndexOf("\n[node ", start + header.Length, StringComparison.Ordinal);
        return next < 0 ? scene[start..] : scene[start..next];
    }
}
