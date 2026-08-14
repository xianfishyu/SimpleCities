using System.IO;

namespace SimpleCities.Tests;

public sealed class PauseMenuContractTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        ".."));
    private static readonly string PauseMenuScenePath = Path.Combine(ProjectRoot, "Scenes", "UI", "PauseMenu.tscn");
    private static readonly string PauseMenuScriptPath = Path.Combine(ProjectRoot, "Scripts", "UI", "PauseMenu.cs");
    private static readonly string GameHudPath = Path.Combine(ProjectRoot, "Scripts", "UI", "GameHUD.cs");
    private static readonly string GameHudScenePath = Path.Combine(ProjectRoot, "Scenes", "UI", "GameHUD.tscn");
    private static readonly string MainMenuScenePath = Path.Combine(ProjectRoot, "Scenes", "MainMenu.tscn");
    private static readonly string MainMenuScriptPath = Path.Combine(ProjectRoot, "Scripts", "UI", "MainMenu.cs");
    private static readonly string SaveManagerPath = Path.Combine(ProjectRoot, "Scripts", "Core", "SaveManager.cs");

    [Fact]
    public void PauseMenuScene_ProvidesAllRequestedActionsAndSubviews()
    {
        string scene = File.ReadAllText(PauseMenuScenePath);

        Assert.Contains("name=\"ContinueButton\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"SaveButton\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"LoadButton\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"SettingsButton\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"ExitGameButton\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"ExitDesktopButton\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"SaveManagementContent\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"SaveNameInput\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"SaveAsButton\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"SaveSlotList\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"SaveSlotSummaryLabel\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"OverwriteButton\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"DeleteButton\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"SaveStatusLabel\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"SettingsContent\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"KeyBindingsButton\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"BindingsContent\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"BindingsList\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"ResetBindingsButton\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"ConfirmationContent\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"MasterVolumeSlider\"", scene, StringComparison.Ordinal);
        Assert.Contains("name=\"MuteToggle\"", scene, StringComparison.Ordinal);
        Assert.Contains("text = \"继续游戏\"", scene, StringComparison.Ordinal);
        Assert.Contains("text = \"保存\"", scene, StringComparison.Ordinal);
        Assert.Contains("text = \"读档\"", scene, StringComparison.Ordinal);
        Assert.Contains("text = \"另存为\"", scene, StringComparison.Ordinal);
        Assert.Contains("text = \"覆盖\"", scene, StringComparison.Ordinal);
        Assert.Contains("text = \"加载\"", scene, StringComparison.Ordinal);
        Assert.Contains("text = \"删除\"", scene, StringComparison.Ordinal);
        Assert.Contains("text = \"设置\"", scene, StringComparison.Ordinal);
        Assert.Contains("text = \"退出游戏\"", scene, StringComparison.Ordinal);
        Assert.Contains("text = \"退出到桌面\"", scene, StringComparison.Ordinal);
    }

    [Fact]
    public void PauseMenuIntegration_PausesThroughHudAndKeepsToolManagerFreeOfEscape()
    {
        string pauseMenu = File.ReadAllText(PauseMenuScriptPath);
        string hud = File.ReadAllText(GameHudPath);
        string hudScene = File.ReadAllText(GameHudScenePath);

        Assert.Contains("ProcessMode = ProcessModeEnum.Always;", pauseMenu, StringComparison.Ordinal);
        Assert.Contains("SetTreePaused(true);", pauseMenu, StringComparison.Ordinal);
        Assert.Contains("SetTreePaused(false);", pauseMenu, StringComparison.Ordinal);
        Assert.Contains("Engine.GetMainLoop() is SceneTree tree", pauseMenu, StringComparison.Ordinal);
        Assert.Contains("ConfigureSaveManager", pauseMenu, StringComparison.Ordinal);
        Assert.Contains("ListSlots()", pauseMenu, StringComparison.Ordinal);
        Assert.Contains("saveManager.StartSaveAs(displayName)", pauseMenu, StringComparison.Ordinal);
        Assert.Contains("saveManager.StartSave(slotID)", pauseMenu, StringComparison.Ordinal);
        Assert.Contains("saveManager.StartLoad(slotID)", pauseMenu, StringComparison.Ordinal);
        Assert.Contains("saveManager?.ArmDeletion(summary)", pauseMenu, StringComparison.Ordinal);
        Assert.Contains("saveManager.StartDeleteSlot(", pauseMenu, StringComparison.Ordinal);
        Assert.Contains("_saveManager.OperationStateChanged += OnSaveOperationStateChanged", pauseMenu, StringComparison.Ordinal);
        Assert.Contains("_saveManager.OperationCompleted += OnSaveOperationCompleted", pauseMenu, StringComparison.Ordinal);
        Assert.Contains("_operationMenuGeneration != _menuOpenGeneration", pauseMenu, StringComparison.Ordinal);
        Assert.Contains("_operationSceneGeneration != saveManager.SceneGeneration", pauseMenu, StringComparison.Ordinal);
        Assert.Contains("_saveManager?.CancelOperation(_activeOperationToken)", pauseMenu, StringComparison.Ordinal);
        Assert.Contains("if (IsSaveOperationBusy)", pauseMenu, StringComparison.Ordinal);
        Assert.DoesNotContain("saveManager.Save(slotID)", pauseMenu, StringComparison.Ordinal);
        Assert.DoesNotContain("saveManager.Load(slotID)", pauseMenu, StringComparison.Ordinal);
        Assert.Contains("ConfirmationAction.OverwriteSave", pauseMenu, StringComparison.Ordinal);
        Assert.Contains("ConfirmationAction.LoadSave", pauseMenu, StringComparison.Ordinal);
        Assert.Contains("ConfirmationAction.DeleteSave", pauseMenu, StringComparison.Ordinal);
        Assert.Contains("_overwriteSaveButton.Disabled = !validSelection", pauseMenu, StringComparison.Ordinal);
        Assert.Contains("_loadSaveButton.Disabled = !validSelection", pauseMenu, StringComparison.Ordinal);
        Assert.Contains("RegisteredSaveableCount", File.ReadAllText(Path.Combine(ProjectRoot, "Scripts", "Core", "SaveManager.cs")), StringComparison.Ordinal);
        Assert.Contains("Unregister", File.ReadAllText(Path.Combine(ProjectRoot, "Scripts", "Core", "SaveManager.cs")), StringComparison.Ordinal);
        Assert.Contains("InputBindingManager.PauseMenuAction", hud, StringComparison.Ordinal);
        Assert.Contains("EventMatchesAction", hud, StringComparison.Ordinal);
        Assert.Contains("TryGetToolForEvent", hud, StringComparison.Ordinal);
        Assert.DoesNotContain("Key.Escape", hud, StringComparison.Ordinal);
        Assert.Contains("OpenPauseMenu();", hud, StringComparison.Ordinal);
        Assert.Contains("_toolManager?.CancelRoadSessions();", hud, StringComparison.Ordinal);
        string hudInput = hud[hud.IndexOf("public override void _Input", StringComparison.Ordinal)..
            hud.IndexOf("public override void _Process", StringComparison.Ordinal)];
        Assert.True(
            hudInput.IndexOf("_pauseMenu.IsOpen", StringComparison.Ordinal) <
            hudInput.IndexOf("EventMatchesAction(@event, InputBindingManager.PauseMenuAction)", StringComparison.Ordinal),
            "GameHUD must yield open-menu input before matching the global pause action.");
        Assert.Contains("ConfigureSaveManager", hud, StringComparison.Ordinal);
        Assert.Contains("ReturnToMainMenuRequested", hud, StringComparison.Ordinal);
        Assert.Contains("QuitToDesktopRequested", hud, StringComparison.Ordinal);
        Assert.Contains("PauseMenu", hudScene, StringComparison.Ordinal);
        Assert.True(File.Exists(MainMenuScenePath));
    }

    [Fact]
    public void ExitFlows_DrainSceneOperationsAndRouteApplicationQuitThroughSaveManager()
    {
        string pauseMenu = File.ReadAllText(PauseMenuScriptPath);
        string hud = File.ReadAllText(GameHudPath);
        string mainMenu = File.ReadAllText(MainMenuScriptPath);
        string saveManager = File.ReadAllText(SaveManagerPath);

        Assert.Contains("SetExitConvergencePending", pauseMenu, StringComparison.Ordinal);
        Assert.Contains("_exitConvergencePending", pauseMenu, StringComparison.Ordinal);
        Assert.Contains("await saveManager.DrainCurrentSceneOperationsAsync()", hud, StringComparison.Ordinal);
        Assert.True(
            hud.IndexOf("await saveManager.DrainCurrentSceneOperationsAsync()", StringComparison.Ordinal) <
            hud.IndexOf("ChangeSceneToFile(MainMenuScenePath)", StringComparison.Ordinal),
            "GameHUD must drain the originating scene before changing scenes.");
        Assert.Contains("saveManager.RequestApplicationQuit()", hud, StringComparison.Ordinal);
        Assert.Contains("SaveManager.Instance.RequestApplicationQuit()", mainMenu, StringComparison.Ordinal);

        Assert.Contains("GetTree().AutoAcceptQuit = false", saveManager, StringComparison.Ordinal);
        Assert.Contains("NotificationWMCloseRequest", saveManager, StringComparison.Ordinal);
        Assert.Contains("BeginSceneClose();", saveManager, StringComparison.Ordinal);
        Assert.Contains("WaitForTrackedOperationsAsync", saveManager, StringComparison.Ordinal);
        Assert.Contains("_coordinator.DiscardPendingAutosave()", saveManager, StringComparison.Ordinal);
        Assert.Contains("_coordinator.BeginShutdownAsync()", saveManager, StringComparison.Ordinal);
        Assert.DoesNotContain("_ = _coordinator.BeginShutdownAsync()", saveManager, StringComparison.Ordinal);
    }
}
