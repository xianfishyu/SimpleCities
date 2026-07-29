using System.IO;

namespace SimpleCities.Tests;

public sealed class ConstructionDockContractTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        ".."));
    private static readonly string DockScenePath = Path.Combine(ProjectRoot, "Scenes", "UI", "ConstructionDock.tscn");
    private static readonly string DockScriptPath = Path.Combine(ProjectRoot, "Scripts", "UI", "ConstructionDock.cs");

    [Fact]
    public void ConstructionDockScene_LoadsStandaloneControlWithRequiredResources()
    {
        Assert.True(File.Exists(DockScenePath), $"Missing scene: {DockScenePath}");
        Assert.True(File.Exists(DockScriptPath), $"Missing script: {DockScriptPath}");
        Assert.True(typeof(ConstructionDock).IsSubclassOf(typeof(Godot.Control)));

        string scene = File.ReadAllText(DockScenePath);
        Assert.Contains("res://Scripts/UI/ConstructionDock.cs", scene, StringComparison.Ordinal);
        Assert.Contains("res://Scenes/UI/Themes/CommandCenterTheme.tres", scene, StringComparison.Ordinal);
        Assert.Contains("res://Scenes/UI/RoadsConstructionCategory.tres", scene, StringComparison.Ordinal);
        Assert.Contains("Category = ExtResource", scene, StringComparison.Ordinal);
    }

    [Fact]
    public void ConstructionDockScene_ContainsOnlyRoadsCategoryAndRoadTools()
    {
        string scene = File.ReadAllText(DockScenePath);
        string script = File.ReadAllText(DockScriptPath);

        Assert.Contains("name=\"RoadsCategoryButton\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"CurrentToolLabel\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"ToolTray\"", scene, StringComparison.Ordinal);
        Assert.Contains("SelectToolButton", script, StringComparison.Ordinal);
        Assert.Contains("RoadToolButton", script, StringComparison.Ordinal);
        Assert.Contains("RoadRemoveToolButton", script, StringComparison.Ordinal);

        Assert.DoesNotContain("Zoning", scene, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Transit", scene, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Facilities", scene, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Landscaping", scene, StringComparison.OrdinalIgnoreCase);
    }
}
