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
    public void Input_OnlyEscapeSwitchesToolsAndRoadInputForwardingRemains()
    {
        string source = File.ReadAllText(ToolManagerPath);

        Assert.DoesNotContain("case Key.R:", source, StringComparison.Ordinal);
        Assert.DoesNotContain("case Key.E:", source, StringComparison.Ordinal);
        Assert.Contains("case Key.Escape:", source, StringComparison.Ordinal);
        Assert.Contains("CurrentTool = ToolType.Select;", source, StringComparison.Ordinal);
        Assert.Contains("_roadBuilder.HandlePlaceInput(@event);", source, StringComparison.Ordinal);
        Assert.Contains("_roadBuilder.HandleRemoveInput(@event);", source, StringComparison.Ordinal);
    }
}
