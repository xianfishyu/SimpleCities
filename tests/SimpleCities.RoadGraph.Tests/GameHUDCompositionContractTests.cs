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
    private static readonly string DebugPanelScriptPath = Path.Combine(ProjectRoot, "Scripts", "UI", "DebugPanel.cs");

    [Fact]
    public void GameHUDScene_ComposesRemainingPanelsAndPauseMenuWithoutSystemControls()
    {
        string scene = File.ReadAllText(HudScenePath);

        Assert.Contains("name=\"ConstructionDock\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"ToolContextPanel\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"DebugPanel\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"PauseMenu\"", scene, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"SystemControls\"", scene, StringComparison.Ordinal);
        Assert.Contains("res://Scenes/UI/Themes/CommandCenterTheme.tres", scene, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"Panel\"", scene, StringComparison.Ordinal);
        Assert.DoesNotContain("Panel/VBox", scene, StringComparison.Ordinal);
        Assert.DoesNotContain("ToolBar", scene, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveBar", scene, StringComparison.Ordinal);
    }

    [Fact]
    public void GameHUDScene_HidesPauseMenuInstanceWhileAuthoringHud()
    {
        string pauseMenu = ExtractNodeBlock(File.ReadAllText(HudScenePath), "PauseMenu");

        Assert.Contains("visible = false", pauseMenu, StringComparison.Ordinal);
    }

    [Fact]
    public void GameHUDScript_UsesCompositionRootPathsAndInjectsPauseMenuDependencies()
    {
        string script = File.ReadAllText(HudScriptPath);

        Assert.Contains("ConstructionDock", script, StringComparison.Ordinal);
        Assert.Contains("ToolContextPanel", script, StringComparison.Ordinal);
        Assert.Contains("DebugPanel", script, StringComparison.Ordinal);
        Assert.Contains("PauseMenu", script, StringComparison.Ordinal);
        Assert.Contains("ConfigureSaveManager", script, StringComparison.Ordinal);
        Assert.DoesNotContain("OnPauseSave", script, StringComparison.Ordinal);
        Assert.DoesNotContain("OnPauseLoad", script, StringComparison.Ordinal);
        Assert.DoesNotContain("SystemControls", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Key.F5", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Key.F9", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Panel/VBox", script, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectBtn", script, StringComparison.Ordinal);
        Assert.DoesNotContain("RoadBtn", script, StringComparison.Ordinal);
        Assert.DoesNotContain("RemoveBtn", script, StringComparison.Ordinal);
    }

    [Fact]
    public void GameHUDScene_KeepsDebugPanelAtDesignedTopLeftMargin()
    {
        string debugPanel = ExtractNodeBlock(File.ReadAllText(HudScenePath), "DebugPanel");

        Assert.Contains("offset_left = 16.0", debugPanel, StringComparison.Ordinal);
        Assert.Contains("offset_top = 16.0", debugPanel, StringComparison.Ordinal);
        Assert.Contains("offset_right = 316.0", debugPanel, StringComparison.Ordinal);
    }

    [Fact]
    public void GameHUDScript_PreservesDebugPanelTopLeftOutsideRightSidePlacement()
    {
        string script = File.ReadAllText(HudScriptPath);

        Assert.Contains("PlaceTopLeftDebugPanel", script, StringComparison.Ordinal);
        Assert.Contains("new Vector2(PanelMargin, PanelMargin)", script, StringComparison.Ordinal);
        Assert.DoesNotContain("PlaceRightAligned(_debugPanel", script, StringComparison.Ordinal);
        Assert.DoesNotContain("_debugPanel.Position = new Vector2(_toolContextPanel.Position.X", script, StringComparison.Ordinal);
    }

    [Fact]
    public void DebugPanelUsesCanonicalSnapshotMetricsWithoutPerFrameGraphCopies()
    {
        string scene = File.ReadAllText(HudScenePath);
        string script = File.ReadAllText(DebugPanelScriptPath);

        Assert.Contains("name=\"NodeRow\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"CanonicalEdgeRow\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"GeometrySegmentRow\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"QueryFragmentRow\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"SelfLoopRow\"", scene, StringComparison.Ordinal);
        Assert.Contains("Node（拓扑）", scene, StringComparison.Ordinal);
        Assert.Contains("Edge（规范）", scene, StringComparison.Ordinal);
        Assert.Contains("Geometry（原生）", scene, StringComparison.Ordinal);
        Assert.Contains("Query（派生）", scene, StringComparison.Ordinal);
        Assert.Contains("Self-loop（拓扑）", scene, StringComparison.Ordinal);

        Assert.Contains("if (!_debugContent.Visible)", script, StringComparison.Ordinal);
        Assert.Contains("CaptureDiagnosticsSnapshot()", script, StringComparison.Ordinal);
        Assert.Contains("snapshot.ChangeSequence == _lastDiagnosticsSequence", script, StringComparison.Ordinal);
        Assert.DoesNotContain("CaptureRevision", script, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAllNodes", script, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAllEdges", script, StringComparison.Ordinal);
        Assert.DoesNotContain("RoadGroup", scene, StringComparison.Ordinal);
        Assert.DoesNotContain("RoadGroup", script, StringComparison.Ordinal);
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
