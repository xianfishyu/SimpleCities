using System.IO;

namespace SimpleCities.Tests;

public sealed class AutosaveContractTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        ".."));

    [Fact]
    public void MapScene_ComposesConfigurableAutosaveController()
    {
        string scene = File.ReadAllText(Path.Combine(ProjectRoot, "Scenes", "MapTest.tscn"));
        string controller = File.ReadAllText(Path.Combine(ProjectRoot, "Scripts", "Core", "AutosaveController.cs"));

        Assert.Contains("name=\"AutosaveController\"", scene, StringComparison.Ordinal);
        Assert.Contains("res://Scripts/Core/AutosaveController.cs", scene, StringComparison.Ordinal);
        Assert.Contains("[Export(PropertyHint.Range", controller, StringComparison.Ordinal);
        Assert.Contains("IntervalSeconds { get; set; } = 300d", controller, StringComparison.Ordinal);
        Assert.Contains("new Timer", controller, StringComparison.Ordinal);
        Assert.Contains("_timer.Timeout += OnAutosaveTimeout", controller, StringComparison.Ordinal);
        Assert.Contains("saveManager?.SaveAutosave()", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void AutosaveProfile_UsesReservedSlotWithoutSelectingItAndLabelsListRows()
    {
        string manager = File.ReadAllText(Path.Combine(ProjectRoot, "Scripts", "Core", "SaveManager.cs"));
        string summary = File.ReadAllText(Path.Combine(ProjectRoot, "Scripts", "Core", "SaveData.cs"));
        string pauseMenu = File.ReadAllText(Path.Combine(ProjectRoot, "Scripts", "UI", "PauseMenu.cs"));

        Assert.Contains("public bool SaveAutosave()", manager, StringComparison.Ordinal);
        Assert.Contains("AutosaveSlotID,\n                AutosaveDisplayName", manager, StringComparison.Ordinal);
        Assert.Contains("public bool IsAutosave", summary, StringComparison.Ordinal);
        Assert.Contains("summary.IsAutosave ? \"自动\" : \"手动\"", pauseMenu, StringComparison.Ordinal);
    }
}
