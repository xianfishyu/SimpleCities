using System.IO;

namespace SimpleCities.Tests;

public sealed class GameHUDCompositionContractTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        ".."));
    private static readonly string HudScenePath = Path.Combine(ProjectRoot, "Scenes", "UI", "GameHUD.tscn");
    private static readonly string HudScriptPath = Path.Combine(ProjectRoot, "Scripts", "UI", "GameHUD.cs");

    [Fact]
    public void GameHUDScene_ComposesCommandCenterPanelsWithoutLegacyPanelPaths()
    {
        string scene = File.ReadAllText(HudScenePath);

        Assert.Contains("name=\"ConstructionDock\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"ToolContextPanel\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"DebugPanel\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"SystemControls\"", scene, StringComparison.Ordinal);
        Assert.Contains("res://Scenes/UI/Themes/CommandCenterTheme.tres", scene, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"Panel\"", scene, StringComparison.Ordinal);
        Assert.DoesNotContain("Panel/VBox", scene, StringComparison.Ordinal);
        Assert.DoesNotContain("ToolBar", scene, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveBar", scene, StringComparison.Ordinal);
    }

    [Fact]
    public void GameHUDScript_UsesCompositionRootPathsAndPreservesSaveLoadShortcuts()
    {
        string script = File.ReadAllText(HudScriptPath);

        Assert.Contains("ConstructionDock", script, StringComparison.Ordinal);
        Assert.Contains("ToolContextPanel", script, StringComparison.Ordinal);
        Assert.Contains("DebugPanel", script, StringComparison.Ordinal);
        Assert.Contains("SystemControls", script, StringComparison.Ordinal);
        Assert.Contains("Key.F5", script, StringComparison.Ordinal);
        Assert.Contains("Key.F9", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Panel/VBox", script, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectBtn", script, StringComparison.Ordinal);
        Assert.DoesNotContain("RoadBtn", script, StringComparison.Ordinal);
        Assert.DoesNotContain("RemoveBtn", script, StringComparison.Ordinal);
    }
}
