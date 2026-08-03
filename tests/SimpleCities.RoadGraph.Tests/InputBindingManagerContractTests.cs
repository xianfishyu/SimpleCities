using System.IO;

namespace SimpleCities.Tests;

public sealed class InputBindingManagerContractTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        ".."));
    private static readonly string ProjectSettingsPath = Path.Combine(ProjectRoot, "project.godot");
    private static readonly string ManagerPath = Path.Combine(ProjectRoot, "Scripts", "Core", "InputBindingManager.cs");
    private static readonly string CameraPath = Path.Combine(ProjectRoot, "Scripts", "MainCamera.cs");
    private static readonly string HudPath = Path.Combine(ProjectRoot, "Scripts", "UI", "GameHUD.cs");
    private static readonly string ToolManagerPath = Path.Combine(ProjectRoot, "Scripts", "Tools", "ToolManager.cs");

    [Fact]
    public void Catalog_DefinesTenUniqueSingleKeyActions()
    {
        InputBindingManager.BindingDefinition[] definitions = InputBindingManager.Definitions.ToArray();

        Assert.Equal(10, definitions.Length);
        Assert.Equal(definitions.Length, definitions.Select(definition => definition.ActionName).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(definitions.Length, definitions.Select(definition => definition.DefaultKey).Distinct().Count());
        Assert.All(definitions, definition => Assert.True(InputBindingManager.IsBindableKey(definition.DefaultKey)));
        Assert.Equal(3, definitions.Count(definition => definition.Tool != null));
    }

    [Fact]
    public void ProjectInputMap_RegistersAutoloadAndEveryCatalogAction()
    {
        string project = File.ReadAllText(ProjectSettingsPath);

        Assert.Contains("InputBindingManager=\"*res://Scripts/Core/InputBindingManager.cs\"", project, StringComparison.Ordinal);
        foreach (InputBindingManager.BindingDefinition definition in InputBindingManager.Definitions)
            Assert.Contains($"{definition.ActionName}={{", project, StringComparison.Ordinal);
    }

    [Fact]
    public void Manager_OwnsConflictValidationAndUserConfigPersistence()
    {
        string source = File.ReadAllText(ManagerPath);

        Assert.Contains("user://input_bindings.cfg", source, StringComparison.Ordinal);
        Assert.Contains("ConfigFile", source, StringComparison.Ordinal);
        Assert.Contains("GetBoundKey(candidate.ActionName) != key", source, StringComparison.Ordinal);
        Assert.Contains("ResetToDefaults", source, StringComparison.Ordinal);
        Assert.Contains("InputMap.ActionEraseEvents", source, StringComparison.Ordinal);
        Assert.Contains("InputMap.ActionAddEvent", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Consumers_UseActionCatalogWhileToolManagerKeepsKeyboardOut()
    {
        string camera = File.ReadAllText(CameraPath);
        string hud = File.ReadAllText(HudPath);
        string toolManager = File.ReadAllText(ToolManagerPath);

        Assert.Contains("InputBindingManager.CameraMoveLeftAction", camera, StringComparison.Ordinal);
        Assert.Contains("InputBindingManager.CameraMoveRightAction", camera, StringComparison.Ordinal);
        Assert.Contains("InputBindingManager.CameraMoveUpAction", camera, StringComparison.Ordinal);
        Assert.Contains("InputBindingManager.CameraMoveDownAction", camera, StringComparison.Ordinal);
        Assert.Contains("InputBindingManager.PauseMenuAction", hud, StringComparison.Ordinal);
        Assert.Contains("InputBindingManager.EditUndoAction", hud, StringComparison.Ordinal);
        Assert.Contains("InputBindingManager.EditRedoAction", hud, StringComparison.Ordinal);
        Assert.Contains("TryGetToolForEvent", hud, StringComparison.Ordinal);
        Assert.DoesNotContain("Key.", hud, StringComparison.Ordinal);
        Assert.DoesNotContain("InputBindingManager", toolManager, StringComparison.Ordinal);
    }
}
