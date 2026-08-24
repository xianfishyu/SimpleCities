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
    private static readonly string SaveSlotStorePath = Path.Combine(ProjectRoot, "Scripts", "Core", "SaveSlotStore.cs");
    private static readonly string DeleteCleanupProbePath = Path.Combine(
        ProjectRoot,
        "tests",
        "godot",
        "SaveDeleteCleanupFailureProbe.cs");
    private static readonly string DeleteOperationProbePath = Path.Combine(
        ProjectRoot,
        "tests",
        "godot",
        "SaveDeleteOperationProbe.cs");
    private static readonly string PublishCleanupProbePath = Path.Combine(
        ProjectRoot,
        "tests",
        "godot",
        "SavePublishCleanupFailureProbe.cs");
    private static readonly string PublishOperationProbePath = Path.Combine(
        ProjectRoot,
        "tests",
        "godot",
        "SavePublishOperationProbe.cs");

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

    [Fact]
    public void DeleteCleanupProbe_IsAfterTheTombstoneAndDebugOnly()
    {
        string saveManager = File.ReadAllText(SaveManagerPath);
        string probe = File.ReadAllText(DeleteCleanupProbePath);
        string project = File.ReadAllText(Path.Combine(ProjectRoot, "SimpleCities.csproj"));

        int operationStart = saveManager.IndexOf(
            "private async Task<SaveOperationResult> RunDeleteAsync",
            StringComparison.Ordinal);
        int operationEnd = saveManager.IndexOf(
            "private void StartTrackedOperation",
            operationStart,
            StringComparison.Ordinal);
        Assert.True(operationStart >= 0 && operationEnd > operationStart);
        string operation = saveManager[operationStart..operationEnd];
        int storeCreation = operation.IndexOf(
            "SaveSlotStore store = CreateSlotStore();",
            StringComparison.Ordinal);
        int probeConfiguration = operation.IndexOf(
            "ProbeConfigureDeleteCleanupFailure(ref store);",
            StringComparison.Ordinal);
        int delete = operation.IndexOf(
            "return store.Delete(authorization, lease);",
            StringComparison.Ordinal);
        Assert.True(storeCreation >= 0 && storeCreation < probeConfiguration);
        Assert.True(probeConfiguration < delete);
        Assert.Contains(
            "partial void ProbeConfigureDeleteCleanupFailure(ref SaveSlotStore store);",
            saveManager,
            StringComparison.Ordinal);
        Assert.Contains(
            "phase != SavePublicationPhase.DeletionTombstoned",
            probe,
            StringComparison.Ordinal);
        Assert.Contains(
            "throw new IOException(DeleteTombstoneCleanupFailureMessage);",
            probe,
            StringComparison.Ordinal);

        const string debugGroupMarker = "<ItemGroup Condition=\"'$(Configuration)' == 'Debug'\">";
        int debugGroupStart = project.IndexOf(debugGroupMarker, StringComparison.Ordinal);
        int debugGroupEnd = project.IndexOf("</ItemGroup>", debugGroupStart, StringComparison.Ordinal);
        Assert.True(debugGroupStart >= 0 && debugGroupEnd > debugGroupStart);
        string debugGroup = project[debugGroupStart..debugGroupEnd];
        Assert.Contains(
            "<Compile Include=\"tests/godot/SaveDeleteCleanupFailureProbe.cs\" />",
            debugGroup,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DeleteRecoverGate_IsBeforeDeletionAndDebugOnly()
    {
        string saveManager = File.ReadAllText(SaveManagerPath);
        string probe = File.ReadAllText(DeleteOperationProbePath);
        string project = File.ReadAllText(Path.Combine(ProjectRoot, "SimpleCities.csproj"));

        int operationStart = saveManager.IndexOf(
            "private async Task<SaveOperationResult> RunDeleteAsync",
            StringComparison.Ordinal);
        int operationEnd = saveManager.IndexOf(
            "private void StartTrackedOperation",
            operationStart,
            StringComparison.Ordinal);
        Assert.True(operationStart >= 0 && operationEnd > operationStart);
        string operation = saveManager[operationStart..operationEnd];
        int storeCreation = operation.IndexOf(
            "SaveSlotStore store = CreateSlotStore();",
            StringComparison.Ordinal);
        int recoverGate = operation.IndexOf(
            "ProbeWaitAtDeleteRecover(ref store);",
            StringComparison.Ordinal);
        int delete = operation.IndexOf(
            "return store.Delete(authorization, lease);",
            StringComparison.Ordinal);
        Assert.True(storeCreation >= 0 && storeCreation < recoverGate);
        Assert.True(recoverGate < delete);
        Assert.Contains(
            "partial void ProbeWaitAtDeleteRecover(ref SaveSlotStore store);",
            saveManager,
            StringComparison.Ordinal);
        Assert.Contains(
            "partial void ProbeObserveCancelOperation(ref string operationToken);",
            saveManager,
            StringComparison.Ordinal);
        Assert.Contains(
            "ProbeObserveCancelOperation(ref operationToken);",
            saveManager,
            StringComparison.Ordinal);
        Assert.Contains("ManualResetEventSlim", probe, StringComparison.Ordinal);
        Assert.Contains(
            "Interlocked.Exchange(ref _deleteRecoverGateArmed, 0)",
            probe,
            StringComparison.Ordinal);
        Assert.Contains(
            "throw new TimeoutException(DeleteRecoverGateTimeoutMessage);",
            probe,
            StringComparison.Ordinal);
        Assert.Contains(
            "Interlocked.Increment(ref _deleteRecoverGateCancelRequestCount);",
            probe,
            StringComparison.Ordinal);

        const string debugGroupMarker = "<ItemGroup Condition=\"'$(Configuration)' == 'Debug'\">";
        int debugGroupStart = project.IndexOf(debugGroupMarker, StringComparison.Ordinal);
        int debugGroupEnd = project.IndexOf("</ItemGroup>", debugGroupStart, StringComparison.Ordinal);
        Assert.True(debugGroupStart >= 0 && debugGroupEnd > debugGroupStart);
        string debugGroup = project[debugGroupStart..debugGroupEnd];
        Assert.Contains(
            "<Compile Include=\"tests/godot/SaveDeleteOperationProbe.cs\" />",
            debugGroup,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DeletePostCommitGate_IsAfterTheTombstoneBoundary()
    {
        string slotStore = File.ReadAllText(SaveSlotStorePath);
        string probe = File.ReadAllText(DeleteOperationProbePath);

        int deleteStart = slotStore.IndexOf(
            "internal SaveDeleteResult Delete(",
            StringComparison.Ordinal);
        int deleteEnd = slotStore.IndexOf(
            "private static IReadOnlyList<SaveParticipantDefinition>",
            deleteStart,
            StringComparison.Ordinal);
        Assert.True(deleteStart >= 0 && deleteEnd > deleteStart);
        string delete = slotStore[deleteStart..deleteEnd];
        int boundary = delete.IndexOf(
            "operationLease.CrossCommitBoundary(() => Directory.Move(slotDir, tombstoneDir));",
            StringComparison.Ordinal);
        int committed = delete.IndexOf(
            "operationLease.MarkCommitted();",
            StringComparison.Ordinal);
        int tombstoneObserver = delete.IndexOf(
            "_publicationObserver?.Invoke(SavePublicationPhase.DeletionTombstoned);",
            StringComparison.Ordinal);
        Assert.True(boundary >= 0 && boundary < committed);
        Assert.True(committed < tombstoneObserver);

        Assert.Contains(
            "store = new SaveSlotStore(_resolvedSaveBaseDir, WaitAtDeletePostCommit);",
            probe,
            StringComparison.Ordinal);
        Assert.Contains(
            "phase != SavePublicationPhase.DeletionTombstoned",
            probe,
            StringComparison.Ordinal);
        Assert.Contains(
            "Volatile.Write(ref _deletePostCommitGateEntered, 1);",
            probe,
            StringComparison.Ordinal);
        Assert.Contains(
            "Interlocked.Increment(ref _deletePostCommitGateCancelRequestCount);",
            probe,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PublishCleanupProbe_IsAfterCanonicalPublicationAndDebugOnly()
    {
        string saveManager = File.ReadAllText(SaveManagerPath);
        string probe = File.ReadAllText(PublishCleanupProbePath);
        string project = File.ReadAllText(Path.Combine(ProjectRoot, "SimpleCities.csproj"));

        int operationStart = saveManager.IndexOf(
            "private async Task<SaveOperationResult> RunAdmittedPublishAsync",
            StringComparison.Ordinal);
        int operationEnd = saveManager.IndexOf(
            "private async Task<SaveOperationResult> RunLoadAsync",
            operationStart,
            StringComparison.Ordinal);
        Assert.True(operationStart >= 0 && operationEnd > operationStart);
        string operation = saveManager[operationStart..operationEnd];
        int storeCreation = operation.IndexOf(
            "SaveSlotStore store = CreateSlotStore();",
            StringComparison.Ordinal);
        int probeConfiguration = operation.IndexOf(
            "ProbeConfigurePublishCleanupFailure(ref store);",
            StringComparison.Ordinal);
        int publish = operation.IndexOf("store.SaveCaptured", StringComparison.Ordinal);
        Assert.True(storeCreation >= 0 && storeCreation < probeConfiguration);
        Assert.True(probeConfiguration < publish);
        Assert.Contains(
            "partial void ProbeConfigurePublishCleanupFailure(ref SaveSlotStore store);",
            saveManager,
            StringComparison.Ordinal);
        Assert.Contains(
            "phase != SavePublicationPhase.CanonicalPublished",
            probe,
            StringComparison.Ordinal);
        Assert.Contains(
            "throw new IOException(PublishCleanupFailureMessage);",
            probe,
            StringComparison.Ordinal);

        const string debugGroupMarker = "<ItemGroup Condition=\"'$(Configuration)' == 'Debug'\">";
        int debugGroupStart = project.IndexOf(debugGroupMarker, StringComparison.Ordinal);
        int debugGroupEnd = project.IndexOf("</ItemGroup>", debugGroupStart, StringComparison.Ordinal);
        Assert.True(debugGroupStart >= 0 && debugGroupEnd > debugGroupStart);
        string debugGroup = project[debugGroupStart..debugGroupEnd];
        Assert.Contains(
            "<Compile Include=\"tests/godot/SavePublishCleanupFailureProbe.cs\" />",
            debugGroup,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PublishPrepareGate_IsBeforePublicationAndDebugOnly()
    {
        string saveManager = File.ReadAllText(SaveManagerPath);
        string probe = File.ReadAllText(PublishOperationProbePath);
        string project = File.ReadAllText(Path.Combine(ProjectRoot, "SimpleCities.csproj"));

        int operationStart = saveManager.IndexOf(
            "private async Task<SaveOperationResult> RunAdmittedPublishAsync",
            StringComparison.Ordinal);
        int operationEnd = saveManager.IndexOf(
            "private async Task<SaveOperationResult> RunLoadAsync",
            operationStart,
            StringComparison.Ordinal);
        Assert.True(operationStart >= 0 && operationEnd > operationStart);
        string operation = saveManager[operationStart..operationEnd];
        int storeCreation = operation.IndexOf(
            "SaveSlotStore store = CreateSlotStore();",
            StringComparison.Ordinal);
        int prepareGate = operation.IndexOf(
            "ProbeWaitAtPublishPrepare(ref store);",
            StringComparison.Ordinal);
        int publish = operation.IndexOf("store.SaveCaptured", StringComparison.Ordinal);
        Assert.True(storeCreation >= 0 && storeCreation < prepareGate);
        Assert.True(prepareGate < publish);
        Assert.Contains(
            "partial void ProbeWaitAtPublishPrepare(ref SaveSlotStore store);",
            saveManager,
            StringComparison.Ordinal);
        Assert.Contains(
            "partial void ProbeObservePublishCancelOperation(ref string operationToken);",
            saveManager,
            StringComparison.Ordinal);
        Assert.Contains(
            "ProbeObservePublishCancelOperation(ref operationToken);",
            saveManager,
            StringComparison.Ordinal);
        Assert.Contains("ManualResetEventSlim", probe, StringComparison.Ordinal);
        Assert.Contains(
            "Interlocked.Exchange(ref _publishPrepareGateArmed, 0)",
            probe,
            StringComparison.Ordinal);
        Assert.Contains(
            "throw new TimeoutException(PublishPrepareGateTimeoutMessage);",
            probe,
            StringComparison.Ordinal);
        Assert.Contains(
            "Interlocked.Increment(ref _publishPrepareGateCancelRequestCount);",
            probe,
            StringComparison.Ordinal);

        const string debugGroupMarker = "<ItemGroup Condition=\"'$(Configuration)' == 'Debug'\">";
        int debugGroupStart = project.IndexOf(debugGroupMarker, StringComparison.Ordinal);
        int debugGroupEnd = project.IndexOf("</ItemGroup>", debugGroupStart, StringComparison.Ordinal);
        Assert.True(debugGroupStart >= 0 && debugGroupEnd > debugGroupStart);
        string debugGroup = project[debugGroupStart..debugGroupEnd];
        Assert.Contains(
            "<Compile Include=\"tests/godot/SavePublishOperationProbe.cs\" />",
            debugGroup,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PublishStagedGate_IsBeforeCancellationCheckAndCommitLease()
    {
        string slotStore = File.ReadAllText(SaveSlotStorePath);
        string probe = File.ReadAllText(PublishOperationProbePath);

        int publishStart = slotStore.IndexOf(
            "private SavePublishResult SaveCore(",
            StringComparison.Ordinal);
        int publishEnd = slotStore.IndexOf(
            "public int Load(",
            publishStart,
            StringComparison.Ordinal);
        Assert.True(publishStart >= 0 && publishEnd > publishStart);
        string publish = slotStore[publishStart..publishEnd];
        int stagedObserver = publish.IndexOf(
            "_publicationObserver?.Invoke(SavePublicationPhase.Staged);",
            StringComparison.Ordinal);
        int cancellationCheck = publish.IndexOf(
            "operationLease.ThrowIfCancellationRequested();",
            stagedObserver,
            StringComparison.Ordinal);
        int commitLease = publish.IndexOf(
            "operationLease.AcquireCommitLease();",
            stagedObserver,
            StringComparison.Ordinal);
        Assert.True(stagedObserver >= 0 && stagedObserver < cancellationCheck);
        Assert.True(cancellationCheck < commitLease);

        Assert.Contains(
            "store = new SaveSlotStore(_resolvedSaveBaseDir, WaitAtPublishStaged);",
            probe,
            StringComparison.Ordinal);
        Assert.Contains(
            "phase != SavePublicationPhase.Staged",
            probe,
            StringComparison.Ordinal);
        Assert.Contains(
            "Volatile.Write(ref _publishStagedGateEntered, 1);",
            probe,
            StringComparison.Ordinal);
        Assert.Contains(
            "Interlocked.Increment(ref _publishStagedGateCancelRequestCount);",
            probe,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PublishPostCommitGate_IsAfterTheCanonicalBoundary()
    {
        string slotStore = File.ReadAllText(SaveSlotStorePath);
        string probe = File.ReadAllText(PublishOperationProbePath);

        int publishStart = slotStore.IndexOf(
            "private SavePublishResult SaveCore(",
            StringComparison.Ordinal);
        int publishEnd = slotStore.IndexOf(
            "public int Load(",
            publishStart,
            StringComparison.Ordinal);
        Assert.True(publishStart >= 0 && publishEnd > publishStart);
        string publish = slotStore[publishStart..publishEnd];
        int canonicalObserver = publish.IndexOf(
            "_publicationObserver?.Invoke(SavePublicationPhase.CanonicalPublished);",
            StringComparison.Ordinal);
        Assert.True(canonicalObserver >= 0);
        int boundary = publish.LastIndexOf(
            "operationLease.CrossCommitBoundary(",
            canonicalObserver,
            StringComparison.Ordinal);
        int committed = publish.LastIndexOf(
            "operationLease.MarkCommitted();",
            canonicalObserver,
            StringComparison.Ordinal);
        Assert.True(boundary >= 0 && boundary < committed);
        Assert.True(committed < canonicalObserver);

        Assert.Contains(
            "store = new SaveSlotStore(_resolvedSaveBaseDir, WaitAtPublishPostCommit);",
            probe,
            StringComparison.Ordinal);
        Assert.Contains(
            "phase != SavePublicationPhase.CanonicalPublished",
            probe,
            StringComparison.Ordinal);
        Assert.Contains(
            "Volatile.Write(ref _publishPostCommitGateEntered, 1);",
            probe,
            StringComparison.Ordinal);
        Assert.Contains(
            "Interlocked.Increment(ref _publishPostCommitGateCancelRequestCount);",
            probe,
            StringComparison.Ordinal);
    }
}
