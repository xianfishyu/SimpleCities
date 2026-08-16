using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using SimpleCities.Core.V3;

/// <summary>
/// 全屏暂停菜单。它管理菜单视图、焦点、会话内音频和持久化按键设置，游戏流程操作通过事件交给 GameHUD。
/// </summary>
public partial class PauseMenu : Control
{
    private enum MenuView
    {
        Main,
        SaveManagement,
        Settings,
        Bindings,
        Confirmation,
    }

    private enum ConfirmationAction
    {
        ReturnToMainMenu,
        QuitToDesktop,
        OverwriteSave,
        LoadSave,
        DeleteSave,
    }

    private const float SilentVolumeDb = -60f;

    private Button _continueButton = null!;
    private Button _saveButton = null!;
    private Button _loadButton = null!;
    private Button _settingsButton = null!;
    private Button _exitGameButton = null!;
    private Button _exitDesktopButton = null!;
    private Control _mainContent = null!;
    private Control _saveManagementContent = null!;
    private LineEdit _saveNameInput = null!;
    private Button _saveAsButton = null!;
    private ItemList _saveSlotList = null!;
    private Label _saveSlotSummaryLabel = null!;
    private Button _overwriteSaveButton = null!;
    private Button _loadSaveButton = null!;
    private Button _deleteSaveButton = null!;
    private Label _saveStatusLabel = null!;
    private Button _saveManagementBackButton = null!;
    private Control _settingsContent = null!;
    private Control _confirmationContent = null!;
    private HSlider _masterVolumeSlider = null!;
    private Label _masterVolumeValue = null!;
    private CheckButton _muteToggle = null!;
    private Button _keyBindingsButton = null!;
    private Button _settingsBackButton = null!;
    private Control _bindingsContent = null!;
    private VBoxContainer _bindingsList = null!;
    private Label _bindingStatusLabel = null!;
    private Button _resetBindingsButton = null!;
    private Button _bindingsBackButton = null!;
    private Label _confirmationTitle = null!;
    private Label _confirmationMessage = null!;
    private Button _confirmButton = null!;
    private Button _cancelButton = null!;

    private MenuView _view;
    private ConfirmationAction _confirmationAction;
    private MenuView _confirmationReturnView;
    private string? _confirmationSlotID;
    private string _confirmationDisplayName = string.Empty;
    private int _masterBusIndex = -1;
    private string? _capturingAction;
    private readonly Dictionary<string, Button> _bindingButtons = new(StringComparer.Ordinal);
    private readonly List<SaveSlotSummary> _saveSlots = new();
    private readonly List<V3SaveSlotUiSummary> _v3SaveSlots = new();
    private readonly V3SaveOperationController _operationController = new();
    private IV3SaveOperationBackend? _v3Backend;
    private V3SaveOperationUiCoordinator? _v3Coordinator;
    private long _sceneGeneration = 1;
    private int _selectedSaveSlotIndex = -1;
    private bool _focusSaveNameOnViewOpen;
    private SaveManager? _saveManager;
    private Control? _focusBeforeOpen;

    public event Action? ContinueRequested;
    public event Action? ReturnToMainMenuRequested;
    public event Action? QuitToDesktopRequested;

    /// <summary>菜单是否正在显示并持有场景树暂停状态。</summary>
    public bool IsOpen => Visible;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        ResolveNodes();
        BuildBindingControls();
        WireEvents();
        InitializeAudioControls();
        ShowMainView();
        Visible = false;
    }

    public override void _ExitTree()
    {
        CancelBindingCapture(showStatus: false);
        UnwireEvents();
        if (IsOpen)
            SetTreePaused(false);
        _focusBeforeOpen = null;
    }

    public override void _Input(InputEvent @event)
    {
        if (!IsOpen || @event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return;

        if (_capturingAction != null)
        {
            HandleBindingInput(keyEvent);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!GodotObject.IsInstanceValid(InputBindingManager.Instance) ||
            !InputBindingManager.Instance.EventMatchesAction(@event, InputBindingManager.PauseMenuAction))
            return;

        if (_operationController.IsBusy)
        {
            _operationController.RequestCancel();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_view == MenuView.Confirmation)
            CancelConfirmation();
        else if (_view == MenuView.SaveManagement)
            ShowMainView();
        else if (_view == MenuView.Bindings)
            ShowSettingsView();
        else if (_view == MenuView.Settings)
            ShowMainView();
        else
            ContinueRequested?.Invoke();

        GetViewport().SetInputAsHandled();
    }

    /// <summary>显示主菜单并暂停场景树；本节点使用 Always 模式，因此仍能响应继续操作。</summary>
    public void Open()
    {
        _focusBeforeOpen = GetViewport().GuiGetFocusOwner();
        ShowMainView();
        Visible = true;
        SetTreePaused(true);
        CallDeferred(MethodName.FocusContinueButton);
    }

    /// <summary>关闭菜单并恢复场景树，保留当前地图、相机和工具状态。</summary>
    public void Close()
    {
        CancelBindingCapture(showStatus: false);
        Visible = false;
        SetTreePaused(false);
        CallDeferred(MethodName.RestorePreviousFocus);
    }

    /// <summary>由 HUD 组合根提供存档后端；传入 null 时界面仍可打开并显示不可用状态。</summary>
    public void ConfigureSaveManager(SaveManager? saveManager) => _saveManager = saveManager;

    /// <summary>由 HUD 组合根提供 V3 存档后端；传入 null 时回退到 V2 SaveManager。</summary>
    public void ConfigureV3Backend(IV3SaveOperationBackend? backend)
    {
        _sceneGeneration++;
        _v3Backend = backend;
        _v3Coordinator = backend != null
            ? new V3SaveOperationUiCoordinator(backend, _operationController, _sceneGeneration)
            : null;
        _operationController.Reset();
    }

    private void ResolveNodes()
    {
        _mainContent = GetNode<Control>("Center/MainPanel/MainContent");
        _saveManagementContent = GetNode<Control>("Center/MainPanel/SaveManagementContent");
        _settingsContent = GetNode<Control>("Center/MainPanel/SettingsContent");
        _bindingsContent = GetNode<Control>("Center/MainPanel/BindingsContent");
        _confirmationContent = GetNode<Control>("Center/MainPanel/ConfirmationContent");
        _continueButton = GetNode<Button>("Center/MainPanel/MainContent/ContinueButton");
        _saveButton = GetNode<Button>("Center/MainPanel/MainContent/SaveButton");
        _loadButton = GetNode<Button>("Center/MainPanel/MainContent/LoadButton");
        _settingsButton = GetNode<Button>("Center/MainPanel/MainContent/SettingsButton");
        _exitGameButton = GetNode<Button>("Center/MainPanel/MainContent/ExitGameButton");
        _exitDesktopButton = GetNode<Button>("Center/MainPanel/MainContent/ExitDesktopButton");
        _saveNameInput = GetNode<LineEdit>("Center/MainPanel/SaveManagementContent/SaveNameRow/SaveNameInput");
        _saveAsButton = GetNode<Button>("Center/MainPanel/SaveManagementContent/SaveNameRow/SaveAsButton");
        _saveSlotList = GetNode<ItemList>("Center/MainPanel/SaveManagementContent/SaveSlotList");
        _saveSlotSummaryLabel = GetNode<Label>("Center/MainPanel/SaveManagementContent/SaveSlotSummaryLabel");
        _overwriteSaveButton = GetNode<Button>("Center/MainPanel/SaveManagementContent/SaveActions/OverwriteButton");
        _loadSaveButton = GetNode<Button>("Center/MainPanel/SaveManagementContent/SaveActions/LoadButton");
        _deleteSaveButton = GetNode<Button>("Center/MainPanel/SaveManagementContent/SaveActions/DeleteButton");
        _saveStatusLabel = GetNode<Label>("Center/MainPanel/SaveManagementContent/SaveStatusLabel");
        _saveManagementBackButton = GetNode<Button>("Center/MainPanel/SaveManagementContent/BackButton");
        _masterVolumeSlider = GetNode<HSlider>("Center/MainPanel/SettingsContent/MasterVolumeSlider");
        _masterVolumeValue = GetNode<Label>("Center/MainPanel/SettingsContent/MasterVolumeValue");
        _muteToggle = GetNode<CheckButton>("Center/MainPanel/SettingsContent/MuteToggle");
        _keyBindingsButton = GetNode<Button>("Center/MainPanel/SettingsContent/KeyBindingsButton");
        _settingsBackButton = GetNode<Button>("Center/MainPanel/SettingsContent/BackButton");
        _bindingsList = GetNode<VBoxContainer>("Center/MainPanel/BindingsContent/BindingsScroll/BindingsList");
        _bindingStatusLabel = GetNode<Label>("Center/MainPanel/BindingsContent/BindingStatusLabel");
        _resetBindingsButton = GetNode<Button>("Center/MainPanel/BindingsContent/BindingActions/ResetBindingsButton");
        _bindingsBackButton = GetNode<Button>("Center/MainPanel/BindingsContent/BindingActions/BackButton");
        _confirmationTitle = GetNode<Label>("Center/MainPanel/ConfirmationContent/ConfirmationTitle");
        _confirmationMessage = GetNode<Label>("Center/MainPanel/ConfirmationContent/ConfirmationMessage");
        _confirmButton = GetNode<Button>("Center/MainPanel/ConfirmationContent/ConfirmationButtons/ConfirmButton");
        _cancelButton = GetNode<Button>("Center/MainPanel/ConfirmationContent/ConfirmationButtons/CancelButton");
    }

    private void WireEvents()
    {
        _continueButton.Pressed += OnContinuePressed;
        _saveButton.Pressed += OnSavePressed;
        _loadButton.Pressed += OnLoadPressed;
        _settingsButton.Pressed += ShowSettingsView;
        _exitGameButton.Pressed += RequestReturnToMainMenu;
        _exitDesktopButton.Pressed += RequestQuitToDesktop;
        _saveAsButton.Pressed += CreateNamedSave;
        _saveNameInput.TextSubmitted += OnSaveNameSubmitted;
        _saveSlotList.ItemSelected += OnSaveSlotSelected;
        _overwriteSaveButton.Pressed += RequestOverwriteSave;
        _loadSaveButton.Pressed += RequestLoadSave;
        _deleteSaveButton.Pressed += RequestDeleteSave;
        _saveManagementBackButton.Pressed += ShowMainView;
        _masterVolumeSlider.ValueChanged += OnMasterVolumeChanged;
        _muteToggle.Toggled += OnMuteToggled;
        _keyBindingsButton.Pressed += ShowBindingsView;
        _settingsBackButton.Pressed += ShowMainView;
        _resetBindingsButton.Pressed += ResetBindings;
        _bindingsBackButton.Pressed += ShowSettingsView;
        _confirmButton.Pressed += ConfirmRequestedAction;
        _cancelButton.Pressed += CancelConfirmation;
        if (GodotObject.IsInstanceValid(InputBindingManager.Instance))
            InputBindingManager.Instance.BindingChanged += OnBindingChanged;
    }

    private void UnwireEvents()
    {
        if (_continueButton == null)
            return;

        _continueButton.Pressed -= OnContinuePressed;
        _saveButton.Pressed -= OnSavePressed;
        _loadButton.Pressed -= OnLoadPressed;
        _settingsButton.Pressed -= ShowSettingsView;
        _exitGameButton.Pressed -= RequestReturnToMainMenu;
        _exitDesktopButton.Pressed -= RequestQuitToDesktop;
        _saveAsButton.Pressed -= CreateNamedSave;
        _saveNameInput.TextSubmitted -= OnSaveNameSubmitted;
        _saveSlotList.ItemSelected -= OnSaveSlotSelected;
        _overwriteSaveButton.Pressed -= RequestOverwriteSave;
        _loadSaveButton.Pressed -= RequestLoadSave;
        _deleteSaveButton.Pressed -= RequestDeleteSave;
        _saveManagementBackButton.Pressed -= ShowMainView;
        _masterVolumeSlider.ValueChanged -= OnMasterVolumeChanged;
        _muteToggle.Toggled -= OnMuteToggled;
        _keyBindingsButton.Pressed -= ShowBindingsView;
        _settingsBackButton.Pressed -= ShowMainView;
        _resetBindingsButton.Pressed -= ResetBindings;
        _bindingsBackButton.Pressed -= ShowSettingsView;
        _confirmButton.Pressed -= ConfirmRequestedAction;
        _cancelButton.Pressed -= CancelConfirmation;
        if (GodotObject.IsInstanceValid(InputBindingManager.Instance))
            InputBindingManager.Instance.BindingChanged -= OnBindingChanged;
    }

    private void InitializeAudioControls()
    {
        _masterBusIndex = AudioServer.GetBusIndex("Master");
        bool hasMasterBus = _masterBusIndex >= 0;
        _masterVolumeSlider.Editable = hasMasterBus;
        _muteToggle.Disabled = !hasMasterBus;
        if (!hasMasterBus)
        {
            _masterVolumeValue.Text = "不可用";
            return;
        }

        _muteToggle.ButtonPressed = AudioServer.IsBusMute(_masterBusIndex);
        _masterVolumeSlider.Value = VolumePercent(AudioServer.GetBusVolumeDb(_masterBusIndex));
        UpdateVolumeLabel(_masterVolumeSlider.Value);
    }

    private void ShowMainView()
    {
        CancelBindingCapture(showStatus: false);
        _view = MenuView.Main;
        _mainContent.Visible = true;
        _saveManagementContent.Visible = false;
        _settingsContent.Visible = false;
        _bindingsContent.Visible = false;
        _confirmationContent.Visible = false;
        if (IsOpen)
            CallDeferred(MethodName.FocusContinueButton);
    }

    private void ShowSettingsView()
    {
        CancelBindingCapture(showStatus: false);
        _view = MenuView.Settings;
        _mainContent.Visible = false;
        _saveManagementContent.Visible = false;
        _settingsContent.Visible = true;
        _bindingsContent.Visible = false;
        _confirmationContent.Visible = false;
        CallDeferred(MethodName.FocusSettingsControl);
    }

    private void ShowBindingsView()
    {
        _view = MenuView.Bindings;
        _mainContent.Visible = false;
        _saveManagementContent.Visible = false;
        _settingsContent.Visible = false;
        _bindingsContent.Visible = true;
        _confirmationContent.Visible = false;
        _bindingStatusLabel.Text = string.Empty;
        RefreshBindingButtons();
        CallDeferred(MethodName.FocusFirstBindingButton);
    }

    private void ShowSaveManagementView(bool focusName)
    {
        CancelBindingCapture(showStatus: false);
        _view = MenuView.SaveManagement;
        _mainContent.Visible = false;
        _saveManagementContent.Visible = true;
        _settingsContent.Visible = false;
        _bindingsContent.Visible = false;
        _confirmationContent.Visible = false;
        _focusSaveNameOnViewOpen = focusName;
        _saveStatusLabel.Text = string.Empty;
        RefreshSaveSlots(PreferredSaveSlotID());
        CallDeferred(MethodName.FocusSaveManagementControl);
    }

    private void ShowConfirmationView(
        string title,
        string message,
        ConfirmationAction action,
        MenuView returnView = MenuView.Main,
        string? slotID = null,
        string displayName = "")
    {
        _view = MenuView.Confirmation;
        _confirmationAction = action;
        _confirmationReturnView = returnView;
        _confirmationSlotID = slotID;
        _confirmationDisplayName = displayName;
        _mainContent.Visible = false;
        _saveManagementContent.Visible = false;
        _settingsContent.Visible = false;
        _bindingsContent.Visible = false;
        _confirmationContent.Visible = true;
        _confirmationTitle.Text = title;
        _confirmationMessage.Text = message;
        CallDeferred(MethodName.FocusCancelButton);
    }

    private void FocusContinueButton() => _continueButton.GrabFocus();

    private void FocusSettingsControl()
    {
        if (_masterVolumeSlider.Editable)
            _masterVolumeSlider.GrabFocus();
        else
            _settingsBackButton.GrabFocus();
    }

    private void FocusSaveManagementControl()
    {
        if (_focusSaveNameOnViewOpen || _saveSlotList.ItemCount == 0)
            _saveNameInput.GrabFocus();
        else
            _saveSlotList.GrabFocus();
    }

    private void FocusFirstBindingButton()
    {
        foreach (InputBindingManager.BindingDefinition definition in InputBindingManager.Definitions)
        {
            if (_bindingButtons.TryGetValue(definition.ActionName, out Button? button))
            {
                button.GrabFocus();
                return;
            }
        }

        _bindingsBackButton.GrabFocus();
    }

    private void FocusCancelButton() => _cancelButton.GrabFocus();

    private void RestorePreviousFocus()
    {
        Control? previousFocus = _focusBeforeOpen;
        _focusBeforeOpen = null;
        if (previousFocus == null
            || !GodotObject.IsInstanceValid(previousFocus)
            || !previousFocus.IsInsideTree()
            || !previousFocus.IsVisibleInTree()
            || previousFocus.FocusMode == FocusModeEnum.None)
        {
            return;
        }

        previousFocus.GrabFocus();
    }

    private static void SetTreePaused(bool paused)
    {
        if (Engine.GetMainLoop() is SceneTree tree)
            tree.Paused = paused;
    }

    private void OnContinuePressed() => ContinueRequested?.Invoke();

    private void OnSavePressed() => ShowSaveManagementView(focusName: true);

    private void OnLoadPressed() => ShowSaveManagementView(focusName: false);

    private void RequestReturnToMainMenu()
    {
        ShowConfirmationView("结束当前城市？", "未保存的变更将丢失。", ConfirmationAction.ReturnToMainMenu);
    }

    private void RequestQuitToDesktop()
    {
        ShowConfirmationView("退出到桌面？", "未保存的变更将丢失。", ConfirmationAction.QuitToDesktop);
    }

    private void ConfirmRequestedAction()
    {
        switch (_confirmationAction)
        {
            case ConfirmationAction.ReturnToMainMenu:
                ReturnToMainMenuRequested?.Invoke();
                break;
            case ConfirmationAction.QuitToDesktop:
                QuitToDesktopRequested?.Invoke();
                break;
            case ConfirmationAction.OverwriteSave:
                OverwriteConfirmedSave();
                break;
            case ConfirmationAction.LoadSave:
                LoadConfirmedSave();
                break;
            case ConfirmationAction.DeleteSave:
                DeleteConfirmedSave();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void CancelConfirmation()
    {
        if (_confirmationReturnView == MenuView.SaveManagement)
            ShowSaveManagementView(focusName: false);
        else
            ShowMainView();
    }

    private void CreateNamedSave()
    {
        string displayName = _saveNameInput.Text.Trim();
        if (displayName.Length == 0)
        {
            ShowSaveStatus("请输入存档名称", success: false);
            _saveNameInput.GrabFocus();
            return;
        }

        V3SaveOperationUiCoordinator? v3Coordinator = ActiveV3Coordinator();
        if (v3Coordinator != null)
        {
            V3SaveOperationUiState state = v3Coordinator.SaveAs(
                displayName,
                displayName,
                DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                null,
                null,
                null);
            if (state.IsBusy || state.Phase == V3SaveOperationUiPhase.Cancelling)
            {
                ShowSaveStatus("存档操作进行中", success: false);
                return;
            }

            if (state.IsComplete)
            {
                _saveNameInput.Text = string.Empty;
                RefreshSaveSlots(ActiveV3Backend()?.CurrentSlotID);
                ShowSaveStatus($"已创建“{displayName}”", success: true);
            }
            else
            {
                ShowSaveStatus(state.Error ?? "新建存档失败", success: false);
            }

            return;
        }

        SaveManager? saveManager = ActiveSaveManager();
        if (saveManager == null)
        {
            ShowSaveStatus("存档管理不可用", success: false);
            return;
        }

        if (!saveManager.SaveAs(displayName))
        {
            ShowSaveStatus("新建存档失败", success: false);
            return;
        }

        _saveNameInput.Text = string.Empty;
        RefreshSaveSlots(saveManager.CurrentSlotID);
        ShowSaveStatus($"已创建“{displayName}”", success: true);
    }

    private void OnSaveNameSubmitted(string submittedText) => CreateNamedSave();

    private void RequestOverwriteSave()
    {
        if (ActiveV3Backend() != null)
        {
            V3SaveSlotUiSummary? v3Summary = SelectedV3SaveSlot();
            if (v3Summary?.CanLoadOrOverwrite != true)
                return;

            ShowConfirmationView(
                "覆盖存档？",
                $"{ConfirmationSummary(v3Summary)}\n现有内容将被当前城市替换。",
                ConfirmationAction.OverwriteSave,
                MenuView.SaveManagement,
                v3Summary.SlotId,
                v3Summary.DisplayName);
            return;
        }

        SaveSlotSummary? summary = SelectedSaveSlot();
        if (summary?.IsValid != true)
            return;

        ShowConfirmationView(
            "覆盖存档？",
            $"{ConfirmationSummary(summary)}\n现有内容将被当前城市替换。",
            ConfirmationAction.OverwriteSave,
            MenuView.SaveManagement,
            summary.SlotID,
            summary.DisplayName);
    }

    private void RequestLoadSave()
    {
        if (ActiveV3Backend() != null)
        {
            V3SaveSlotUiSummary? v3Summary = SelectedV3SaveSlot();
            if (v3Summary?.CanLoadOrOverwrite != true)
                return;

            ShowConfirmationView(
                "加载存档？",
                $"{ConfirmationSummary(v3Summary)}\n未保存的当前变更将丢失。",
                ConfirmationAction.LoadSave,
                MenuView.SaveManagement,
                v3Summary.SlotId,
                v3Summary.DisplayName);
            return;
        }

        SaveSlotSummary? summary = SelectedSaveSlot();
        if (summary?.IsValid != true)
            return;

        ShowConfirmationView(
            "加载存档？",
            $"{ConfirmationSummary(summary)}\n未保存的当前变更将丢失。",
            ConfirmationAction.LoadSave,
            MenuView.SaveManagement,
            summary.SlotID,
            summary.DisplayName);
    }

    private void RequestDeleteSave()
    {
        if (ActiveV3Backend() != null)
        {
            V3SaveSlotUiSummary? v3Summary = SelectedV3SaveSlot();
            if (v3Summary?.CanDelete != true)
                return;

            ShowConfirmationView(
                "删除存档？",
                $"{ConfirmationSummary(v3Summary)}\n删除后无法恢复。",
                ConfirmationAction.DeleteSave,
                MenuView.SaveManagement,
                v3Summary.SlotId,
                v3Summary.DisplayName);
            return;
        }

        SaveSlotSummary? summary = SelectedSaveSlot();
        if (summary == null)
            return;

        string displayName = summary.IsValid ? summary.DisplayName : summary.SlotID;
        ShowConfirmationView(
            "删除存档？",
            $"{ConfirmationSummary(summary)}\n删除后无法恢复。",
            ConfirmationAction.DeleteSave,
            MenuView.SaveManagement,
            summary.SlotID,
            displayName);
    }

    private void OverwriteConfirmedSave()
    {
        V3SaveOperationUiCoordinator? v3Coordinator = ActiveV3Coordinator();
        string? slotID = _confirmationSlotID;
        string displayName = _confirmationDisplayName;
        if (v3Coordinator != null)
        {
            if (slotID == null)
            {
                ShowSaveManagementView(focusName: false);
                ShowSaveStatus("覆盖存档失败", success: false);
                return;
            }

            V3SaveOperationUiState state = v3Coordinator.Save(
                slotID,
                displayName,
                displayName,
                DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                null,
                null,
                null);
            ShowSaveManagementView(focusName: false);
            ShowSaveStatus(
                state.IsComplete ? $"已覆盖“{displayName}”" : state.Error ?? "覆盖存档失败",
                state.IsComplete);
            return;
        }

        SaveManager? saveManager = ActiveSaveManager();
        bool success = saveManager != null && slotID != null && saveManager.Save(slotID);
        ShowSaveManagementView(focusName: false);
        ShowSaveStatus(success ? $"已覆盖“{displayName}”" : "覆盖存档失败", success);
    }

    private void LoadConfirmedSave()
    {
        V3SaveOperationUiCoordinator? v3Coordinator = ActiveV3Coordinator();
        string? slotID = _confirmationSlotID;
        string displayName = _confirmationDisplayName;
        if (v3Coordinator != null)
        {
            if (slotID == null)
            {
                ShowSaveManagementView(focusName: false);
                ShowSaveStatus("加载存档失败", success: false);
                return;
            }

            V3SaveOperationUiState state = v3Coordinator.Load(slotID, lineageID: 1);
            ShowSaveManagementView(focusName: false);
            ShowSaveStatus(
                state.IsComplete ? $"已加载“{displayName}”" : state.Error ?? "加载存档失败",
                state.IsComplete);
            return;
        }

        SaveManager? saveManager = ActiveSaveManager();
        bool success = saveManager != null && slotID != null && saveManager.Load(slotID);
        ShowSaveManagementView(focusName: false);
        ShowSaveStatus(success ? $"已加载“{displayName}”" : "加载存档失败", success);
    }

    private void DeleteConfirmedSave()
    {
        V3SaveOperationUiCoordinator? v3Coordinator = ActiveV3Coordinator();
        string? slotID = _confirmationSlotID;
        string displayName = _confirmationDisplayName;
        if (v3Coordinator != null)
        {
            if (slotID == null)
            {
                ShowSaveManagementView(focusName: false);
                ShowSaveStatus("删除存档失败", success: false);
                return;
            }

            V3SaveOperationUiState state = v3Coordinator.Delete(slotID);
            ShowSaveManagementView(focusName: false);
            ShowSaveStatus(
                state.IsComplete ? $"已删除“{displayName}”" : state.Error ?? "删除存档失败",
                state.IsComplete);
            return;
        }

        SaveManager? saveManager = ActiveSaveManager();
        bool success = saveManager != null && slotID != null && saveManager.DeleteSlot(slotID);
        ShowSaveManagementView(focusName: false);
        ShowSaveStatus(success ? $"已删除“{displayName}”" : "删除存档失败", success);
    }

    private void RefreshSaveSlots(string? preferredSlotID)
    {
        _saveSlots.Clear();
        _v3SaveSlots.Clear();
        _saveSlotList.Clear();
        _selectedSaveSlotIndex = -1;

        IV3SaveOperationBackend? v3Backend = ActiveV3Backend();
        if (v3Backend != null)
        {
            RefreshV3SaveSlots(v3Backend, preferredSlotID);
            return;
        }

        SaveManager? saveManager = ActiveSaveManager();
        if (saveManager == null)
        {
            _saveSlotSummaryLabel.Text = "存档管理不可用";
            _saveSlotSummaryLabel.TooltipText = string.Empty;
            UpdateSaveActionAvailability();
            return;
        }

        _saveSlots.AddRange(saveManager.ListSlots());
        int preferredIndex = -1;
        for (int index = 0; index < _saveSlots.Count; index++)
        {
            SaveSlotSummary summary = _saveSlots[index];
            string slotKind = summary.IsAutosave ? "自动" : "手动";
            string itemText = summary.IsValid
                ? $"{slotKind}  ·  {summary.DisplayName}  ·  {FormatSaveTime(summary.SavedAtUtc)}"
                : $"损坏{slotKind}存档  ·  {summary.SlotID}";
            _saveSlotList.AddItem(itemText);
            _saveSlotList.SetItemMetadata(index, summary.SlotID);
            if (!summary.IsValid)
                _saveSlotList.SetItemCustomFgColor(index, new Color("#FF6B6B"));
            if (string.Equals(summary.SlotID, preferredSlotID, StringComparison.Ordinal))
                preferredIndex = index;
        }

        if (_saveSlots.Count == 0)
        {
            _saveSlotSummaryLabel.Text = "暂无存档";
            _saveSlotSummaryLabel.TooltipText = string.Empty;
            UpdateSaveActionAvailability();
            return;
        }

        _selectedSaveSlotIndex = preferredIndex >= 0 ? preferredIndex : 0;
        _saveSlotList.Select(_selectedSaveSlotIndex);
        UpdateSelectedSaveSummary();
    }

    private void RefreshV3SaveSlots(IV3SaveOperationBackend backend, string? preferredSlotID)
    {
        int preferredIndex = -1;
        foreach (V3SlotSummary slot in backend.ListSlots())
        {
            V3SaveSlotUiSummary summary = V3SaveSlotUiSummary.FromSlot(slot);
            if (!summary.IsListable)
                continue;

            _v3SaveSlots.Add(summary);
            string slotKind = string.Equals(summary.SlotId, SaveManager.AutosaveSlotID, StringComparison.OrdinalIgnoreCase)
                ? "自动"
                : "手动";
            string itemText = summary.Occupant == V3SlotOccupant.CompleteV3
                ? $"{slotKind}  ·  {summary.DisplayName}  ·  {FormatSaveTime(ParseTimestamp(summary.Timestamp))}"
                : $"损坏{slotKind}存档  ·  {summary.SlotId}";
            int itemIndex = _saveSlotList.ItemCount;
            _saveSlotList.AddItem(itemText);
            _saveSlotList.SetItemMetadata(itemIndex, summary.SlotId);
            if (summary.Occupant == V3SlotOccupant.CorruptV3)
                _saveSlotList.SetItemCustomFgColor(itemIndex, new Color("#FF6B6B"));
            if (string.Equals(summary.SlotId, preferredSlotID, StringComparison.Ordinal))
                preferredIndex = itemIndex;
        }

        if (_v3SaveSlots.Count == 0)
        {
            _saveSlotSummaryLabel.Text = "暂无存档";
            _saveSlotSummaryLabel.TooltipText = string.Empty;
            UpdateSaveActionAvailability();
            return;
        }

        _selectedSaveSlotIndex = preferredIndex >= 0 ? preferredIndex : 0;
        _saveSlotList.Select(_selectedSaveSlotIndex);
        UpdateSelectedSaveSummary();
    }

    private void OnSaveSlotSelected(long index)
    {
        if (ActiveV3Backend() != null)
            _selectedSaveSlotIndex = index >= 0 && index < _v3SaveSlots.Count ? (int)index : -1;
        else
            _selectedSaveSlotIndex = index >= 0 && index < _saveSlots.Count ? (int)index : -1;
        UpdateSelectedSaveSummary();
    }

    private void UpdateSelectedSaveSummary()
    {
        if (ActiveV3Backend() != null)
        {
            UpdateSelectedV3SaveSummary();
            return;
        }

        SaveSlotSummary? summary = SelectedSaveSlot();
        if (summary == null)
        {
            _saveSlotSummaryLabel.Text = "暂无存档";
            _saveSlotSummaryLabel.TooltipText = string.Empty;
            UpdateSaveActionAvailability();
            return;
        }

        if (!summary.IsValid)
        {
            string error = summary.Error ?? "清单无法读取";
            _saveSlotSummaryLabel.Text = $"损坏存档：{summary.SlotID}\n{error}";
            _saveSlotSummaryLabel.TooltipText = error;
            UpdateSaveActionAvailability();
            return;
        }

        string population = summary.Population?.ToString("N0") ?? "暂无";
        string funds = summary.Funds?.ToString("N0") ?? "暂无";
        string thumbnail = summary.ThumbnailPath == null ? "暂无" : "已有";
        string slotKind = summary.IsAutosave ? "自动存档" : "手动存档";
        _saveSlotSummaryLabel.Text =
            $"{slotKind}  ·  {summary.DisplayName}  ·  {FormatSaveTime(summary.SavedAtUtc)}\n" +
            $"城市：{summary.CityName}  人口：{population}  资金：{funds}  缩略图：{thumbnail}";
        _saveSlotSummaryLabel.TooltipText = string.Empty;
        UpdateSaveActionAvailability();
    }

    private void UpdateSelectedV3SaveSummary()
    {
        V3SaveSlotUiSummary? summary = SelectedV3SaveSlot();
        if (summary == null)
        {
            _saveSlotSummaryLabel.Text = "暂无存档";
            _saveSlotSummaryLabel.TooltipText = string.Empty;
            UpdateSaveActionAvailability();
            return;
        }

        if (summary.Occupant == V3SlotOccupant.CorruptV3)
        {
            string error = summary.Error ?? "清单无法读取";
            _saveSlotSummaryLabel.Text = $"损坏存档：{summary.SlotId}\n{error}";
            _saveSlotSummaryLabel.TooltipText = error;
            UpdateSaveActionAvailability();
            return;
        }

        string slotKind = string.Equals(summary.SlotId, SaveManager.AutosaveSlotID, StringComparison.OrdinalIgnoreCase)
            ? "自动存档"
            : "手动存档";
        _saveSlotSummaryLabel.Text =
            $"{slotKind}  ·  {summary.DisplayName}  ·  {FormatSaveTime(ParseTimestamp(summary.Timestamp))}\n" +
            $"槽 ID：{summary.SlotId}";
        _saveSlotSummaryLabel.TooltipText = string.Empty;
        UpdateSaveActionAvailability();
    }

    private void UpdateSaveActionAvailability()
    {
        if (_operationController.IsBusy)
        {
            _saveAsButton.Disabled = true;
            _overwriteSaveButton.Disabled = true;
            _loadSaveButton.Disabled = true;
            _deleteSaveButton.Disabled = true;
            return;
        }

        _saveAsButton.Disabled = ActiveV3Backend() == null && ActiveSaveManager() == null;

        if (ActiveV3Backend() != null)
        {
            V3SaveSlotUiSummary? v3Summary = SelectedV3SaveSlot();
            _overwriteSaveButton.Disabled = v3Summary?.CanLoadOrOverwrite != true;
            _loadSaveButton.Disabled = v3Summary?.CanLoadOrOverwrite != true;
            _deleteSaveButton.Disabled = v3Summary?.CanDelete != true;
            return;
        }

        SaveSlotSummary? summary = SelectedSaveSlot();
        bool validSelection = summary?.IsValid == true;
        _overwriteSaveButton.Disabled = !validSelection;
        _loadSaveButton.Disabled = !validSelection;
        _deleteSaveButton.Disabled = summary == null;
    }

    private SaveSlotSummary? SelectedSaveSlot()
    {
        return _selectedSaveSlotIndex >= 0 && _selectedSaveSlotIndex < _saveSlots.Count
            ? _saveSlots[_selectedSaveSlotIndex]
            : null;
    }

    private V3SaveSlotUiSummary? SelectedV3SaveSlot()
    {
        return _selectedSaveSlotIndex >= 0 && _selectedSaveSlotIndex < _v3SaveSlots.Count
            ? _v3SaveSlots[_selectedSaveSlotIndex]
            : null;
    }

    private string? PreferredSaveSlotID()
    {
        if (ActiveV3Backend() != null)
        {
            V3SaveSlotUiSummary? v3Selected = SelectedV3SaveSlot();
            if (v3Selected != null)
                return v3Selected.SlotId;
            return ActiveV3Backend()?.CurrentSlotID;
        }

        SaveSlotSummary? selected = SelectedSaveSlot();
        if (selected != null)
            return selected.SlotID;

        return ActiveSaveManager()?.CurrentSlotID;
    }

    private IV3SaveOperationBackend? ActiveV3Backend() => _v3Backend;

    private V3SaveOperationUiCoordinator? ActiveV3Coordinator() => _v3Coordinator;

    private SaveManager? ActiveSaveManager()
    {
        return _saveManager != null && GodotObject.IsInstanceValid(_saveManager)
            ? _saveManager
            : null;
    }

    private static string ConfirmationSummary(SaveSlotSummary summary)
    {
        return summary.IsValid
            ? $"{(summary.IsAutosave ? "自动" : "手动")} · “{summary.DisplayName}” · {FormatSaveTime(summary.SavedAtUtc)}"
            : $"损坏{(summary.IsAutosave ? "自动" : "手动")}存档 · {summary.SlotID}";
    }

    private static string ConfirmationSummary(V3SaveSlotUiSummary summary)
    {
        string slotKind = string.Equals(summary.SlotId, SaveManager.AutosaveSlotID, StringComparison.OrdinalIgnoreCase)
            ? "自动"
            : "手动";
        return summary.Occupant == V3SlotOccupant.CompleteV3
            ? $"{slotKind} · “{summary.DisplayName}” · {FormatSaveTime(ParseTimestamp(summary.Timestamp))}"
            : $"损坏{slotKind}存档 · {summary.SlotId}";
    }

    private static DateTimeOffset? ParseTimestamp(string? timestamp)
    {
        if (string.IsNullOrWhiteSpace(timestamp))
            return null;

        return DateTimeOffset.TryParse(
            timestamp,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateTimeOffset value)
            ? value
            : null;
    }

    private static string FormatSaveTime(DateTimeOffset? savedAtUtc)
    {
        return savedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "时间未知";
    }

    private void ShowSaveStatus(string message, bool success)
    {
        _saveStatusLabel.Text = message;
        _saveStatusLabel.AddThemeColorOverride("font_color", success ? new Color("#52C878") : new Color("#FF6B6B"));
    }

    private void OnMasterVolumeChanged(double value)
    {
        UpdateVolumeLabel(value);
        if (_masterBusIndex < 0)
            return;

        AudioServer.SetBusVolumeDb(_masterBusIndex, VolumeDecibels(value));
    }

    private void OnMuteToggled(bool muted)
    {
        if (_masterBusIndex >= 0)
            AudioServer.SetBusMute(_masterBusIndex, muted);
    }

    private void BuildBindingControls()
    {
        foreach (Node child in _bindingsList.GetChildren())
        {
            _bindingsList.RemoveChild(child);
            child.Free();
        }
        _bindingButtons.Clear();

        if (!GodotObject.IsInstanceValid(InputBindingManager.Instance))
        {
            _bindingStatusLabel.Text = "输入设置不可用";
            _resetBindingsButton.Disabled = true;
            return;
        }

        _resetBindingsButton.Disabled = false;
        string? currentGroup = null;
        foreach (InputBindingManager.BindingDefinition definition in InputBindingManager.Definitions)
        {
            if (currentGroup != definition.GroupName)
            {
                currentGroup = definition.GroupName;
                var groupLabel = new Label
                {
                    Text = currentGroup,
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                groupLabel.AddThemeColorOverride("font_color", new Color("#A7AFBA"));
                groupLabel.AddThemeFontSizeOverride("font_size", 13);
                _bindingsList.AddChild(groupLabel);
            }

            var row = new HBoxContainer
            {
                Name = $"{definition.ActionName}_BindingRow",
            };
            row.AddThemeConstantOverride("separation", 12);

            var actionLabel = new Label
            {
                Text = definition.DisplayName,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            var bindingButton = new Button
            {
                Name = $"{definition.ActionName}_BindingButton",
                CustomMinimumSize = new Vector2(132f, 36f),
                FocusMode = FocusModeEnum.All,
            };

            string actionName = definition.ActionName;
            bindingButton.Pressed += () => BeginBindingCapture(actionName);
            row.AddChild(actionLabel);
            row.AddChild(bindingButton);
            _bindingsList.AddChild(row);
            _bindingButtons[actionName] = bindingButton;
        }

        RefreshBindingButtons();
    }

    private void BeginBindingCapture(string actionName)
    {
        if (_capturingAction == actionName)
        {
            CancelBindingCapture(showStatus: true);
            return;
        }

        _capturingAction = actionName;
        _bindingStatusLabel.Text = string.Empty;
        RefreshBindingButtons();
        _bindingButtons[actionName].Text = "等待输入...";
    }

    private void HandleBindingInput(InputEventKey keyEvent)
    {
        if (_capturingAction == null)
            return;

        if (keyEvent.CtrlPressed || keyEvent.AltPressed || keyEvent.ShiftPressed || keyEvent.MetaPressed)
        {
            ShowBindingStatus("暂不支持组合键", success: false);
            return;
        }

        if (!GodotObject.IsInstanceValid(InputBindingManager.Instance))
        {
            CancelBindingCapture(showStatus: false);
            ShowBindingStatus("输入设置不可用", success: false);
            return;
        }

        string actionName = _capturingAction;
        Key key = InputBindingManager.NormalizeKey(keyEvent);
        if (!InputBindingManager.Instance.TryRebind(actionName, key, out string error))
        {
            ShowBindingStatus(error, success: false);
            return;
        }

        _capturingAction = null;
        RefreshBindingButtons();
        ShowBindingStatus(
            $"“{InputBindingManager.Instance.GetDisplayName(actionName)}”已绑定为 {InputBindingManager.Instance.GetBindingText(actionName)}",
            success: true);
    }

    private void CancelBindingCapture(bool showStatus)
    {
        if (_capturingAction == null)
            return;

        _capturingAction = null;
        RefreshBindingButtons();
        if (showStatus)
            ShowBindingStatus("已取消按键绑定", success: true);
    }

    private void ResetBindings()
    {
        CancelBindingCapture(showStatus: false);
        if (!GodotObject.IsInstanceValid(InputBindingManager.Instance))
        {
            ShowBindingStatus("输入设置不可用", success: false);
            return;
        }

        if (!InputBindingManager.Instance.ResetToDefaults(out string error))
        {
            ShowBindingStatus(error, success: false);
            return;
        }

        RefreshBindingButtons();
        ShowBindingStatus("已恢复默认按键", success: true);
    }

    private void OnBindingChanged(string actionName) => RefreshBindingButtons();

    private void RefreshBindingButtons()
    {
        if (!GodotObject.IsInstanceValid(InputBindingManager.Instance))
            return;

        foreach ((string actionName, Button button) in _bindingButtons)
            button.Text = InputBindingManager.Instance.GetBindingText(actionName);

        if (_capturingAction != null && _bindingButtons.TryGetValue(_capturingAction, out Button? captureButton))
            captureButton.Text = "等待输入...";
    }

    private void ShowBindingStatus(string message, bool success)
    {
        _bindingStatusLabel.Text = message;
        _bindingStatusLabel.AddThemeColorOverride("font_color", success ? new Color("#52C878") : new Color("#FF6B6B"));
    }

    private void UpdateVolumeLabel(double value) => _masterVolumeValue.Text = $"{Mathf.RoundToInt((float)value)}%";

    private static double VolumePercent(float decibels)
    {
        return Mathf.Clamp(Mathf.Remap(decibels, SilentVolumeDb, 0f, 0f, 100f), 0f, 100f);
    }

    private static float VolumeDecibels(double percent)
    {
        return Mathf.Lerp(SilentVolumeDb, 0f, Mathf.Clamp((float)percent / 100f, 0f, 1f));
    }
}
