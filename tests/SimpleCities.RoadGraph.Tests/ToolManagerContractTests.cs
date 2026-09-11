using System.IO;

namespace SimpleCities.Tests;

public sealed class ToolManagerContractTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        ".."));
    private static readonly string ToolManagerPath = Path.Combine(ProjectRoot, "Scripts", "Tools", "ToolManager.cs");

    [Fact]
    public void Input_DoesNotOwnKeyboardToolSwitchingAndRoadInputForwardingRemains()
    {
        string source = File.ReadAllText(ToolManagerPath);

        Assert.DoesNotContain("case Key.R:", source, StringComparison.Ordinal);
        Assert.DoesNotContain("case Key.E:", source, StringComparison.Ordinal);
        Assert.DoesNotContain("case Key.Escape:", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CurrentTool = ToolType.Select;", source, StringComparison.Ordinal);
        Assert.Contains("_roadBuilder.HandlePlaceInput(@event);", source, StringComparison.Ordinal);
        Assert.Contains("_roadBuilder.HandleRemoveInput(@event);", source, StringComparison.Ordinal);
        Assert.Contains("_roadBuilder.HandleUpgradeInput(@event);", source, StringComparison.Ordinal);
        Assert.Contains("_roadBuilder?.UndoLastEdit()", source, StringComparison.Ordinal);
        Assert.Contains("_roadBuilder?.RedoLastEdit()", source, StringComparison.Ordinal);
        Assert.Contains("if (_currentTool == ToolType.Road)", source, StringComparison.Ordinal);
        Assert.Contains("_roadBuilder?.CancelPlaceSession();", source, StringComparison.Ordinal);
        Assert.Contains("_roadBuilder?.CancelUpgradeSession();", source, StringComparison.Ordinal);
        Assert.Contains("_roadBuilder?.SetUpgradeHoverActive(false);", source, StringComparison.Ordinal);
        Assert.Contains("_roadBuilder?.SetUpgradeHoverActive(true);", source, StringComparison.Ordinal);
        Assert.Contains("public void CancelRoadSessions()", source, StringComparison.Ordinal);
        Assert.Contains("RegisterSceneLoad(new SceneLoadParticipants(roadSystem.Graph, this, renderer))", source, StringComparison.Ordinal);
        Assert.Contains("_registeredSaveManager.UnregisterSceneLoad(this)", source, StringComparison.Ordinal);
    }
}
