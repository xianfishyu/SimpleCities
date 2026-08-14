using System;
using System.Collections.Generic;
using Godot;

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

    private enum MenuOperationAction
    {
        None,
        SaveAs,
        Overwrite,
        Load,
        Delete,
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
    private string _confirmationOperationToken = string.Empty;
    private int _masterBusIndex = -1;
    private string? _capturingAction;
    private readonly Dictionary<string, Button> _bindingButtons = new(StringComparer.Ordinal);
    private readonly List<SaveSlotSummary> _saveSlots = new();
    private int _selectedSaveSlotIndex = -1;
    private bool _focusSaveNameOnViewOpen;
    private SaveManager? _saveManager;
    private Control? _focusBeforeOpen;
    private long _menuOpenGeneration;
    private long _operationMenuGeneration;
    private long _operationSceneGeneration;
    private string _activeOperationToken = string.Empty;
    private string _activeOperationSlotID = string.Empty;
    private string _activeOperationDisplayName = string.Empty;
    private SaveOperationKind _activeOperationKind;
    private SaveOperationPhase _activeOperationPhase;
    private MenuOperationAction _activeMenuOperation;
    private bool _activeOperationCrossedBoundary;
    private bool _activeOperationCancelRequested;
    private bool _exitConvergencePending;
    private bool _refreshSaveSlotsWhenIdle;
    private int _saveManagerIdleFrames;

    public event Action? ContinueRequested;
    public event Action? ReturnToMainMenuRequested;
    public event Action? QuitToDesktopRequested;

    /// <summary>菜单是否正在显示并持有场景树暂停状态。</summary>
    public bool IsOpen => Visible;
    public bool IsSaveOperationBusy => _activeOperationToken.Length != 0;
    public string ActiveSaveOperationToken => _activeOperationToken;
    public int ActiveSaveOperationPhase => (int)_activeOperationPhase;
    public bool ActiveSaveOperationCrossedBoundary => _activeOperationCrossedBoundary;
    public bool ActiveSaveOperationCancelRequested => _activeOperationCancelRequested;
    public bool IsExitConvergencePending => _exitConvergencePending;
    public long MenuOpenGeneration => _menuOpenGeneration;

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
        ConfigureSaveManager(null);
        CancelBindingCapture(showStatus: false);
        UnwireEvents();
        if (IsOpen)
            SetTreePaused(false);
        _focusBeforeOpen = null;
    }

    public override void _Process(double delta)
    {
        if (!_refreshSaveSlotsWhenIdle || !IsOpen || _view != MenuView.SaveManagement ||
            IsSaveOperationBusy)
        {
            return;
        }

        SaveManager? saveManager = ActiveSaveManager();
        if (saveManager?.IsOperationBusy != false)
        {
            _saveManagerIdleFrames = 0;
            return;
        }
        if (++_saveManagerIdleFrames < 3)
            return;

        _refreshSaveSlotsWhenIdle = false;
        _saveManagerIdleFrames = 0;
        SetSaveOperationControlsDisabled(false);
        RefreshSaveSlots(PreferredSaveSlotID());
    }

    public override void _Input(InputEvent @event)
    {
        if (!IsOpen || @event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return;

        if (_exitConvergencePending)
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_capturingAction != null)
        {
            HandleBindingInput(keyEvent);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!GodotObject.IsInstanceValid(InputBindingManager.Instance) ||
            !InputBindingManager.Instance.EventMatchesAction(@event, InputBindingManager.PauseMenuAction))
            return;

        if (IsSaveOperationBusy)
        {
            if (!_activeOperationCrossedBoundary && !_activeOperationCancelRequested)
            {
                _activeOperationCancelRequested = true;
                _saveManager?.CancelOperation(_activeOperationToken);
                ShowSaveStatus("正在取消操作...", success: false);
            }
        }
        else if (_view == MenuView.Confirmation)
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
        _exitConvergencePending = false;
        _menuOpenGeneration = NextGeneration(_menuOpenGeneration);
        _focusBeforeOpen = GetViewport().GuiGetFocusOwner();
        ShowMainView();
        Visible = true;
        SetTreePaused(true);
        CallDeferred(MethodName.FocusContinueButton);
    }

    /// <summary>关闭菜单并恢复场景树，保留当前地图、相机和工具状态。</summary>
    public void Close()
    {
        if (IsSaveOperationBusy || _exitConvergencePending)
            return;
        CancelBindingCapture(showStatus: false);
        Visible = false;
        SetTreePaused(false);
        CallDeferred(MethodName.RestorePreviousFocus);
    }

    public void SetExitConvergencePending(bool pending)
    {
        _exitConvergencePending = pending;
        SetSaveOperationControlsDisabled(pending || IsSaveOperationBusy);
    }

    /// <summary>由 HUD 组合根提供存档后端；传入 null 时界面仍可打开并显示不可用状态。</summary>
    public void ConfigureSaveManager(SaveManager? saveManager)
    {
        if (ReferenceEquals(_saveManager, saveManager))
            return;
        if (_saveManager is not null && GodotObject.IsInstanceValid(_saveManager))
        {
            _saveManager.OperationStateChanged -= OnSaveOperationStateChanged;
            _saveManager.OperationCompleted -= OnSaveOperationCompleted;
        }
        _saveManager = saveManager;
        if (_saveManager is not null && GodotObject.IsInstanceValid(_saveManager))
        {
            _saveManager.OperationStateChanged += OnSaveOperationStateChanged;
            _saveManager.OperationCompleted += OnSaveOperationCompleted;
        }
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
        if (IsSaveOperationBusy)
            return;
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
        if (IsSaveOperationBusy)
            return;
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
        if (IsSaveOperationBusy)
            return;
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
        if (IsSaveOperationBusy)
            return;
        CancelBindingCapture(showStatus: false);
        _view = MenuView.SaveManagement;
        _mainContent.Visible = false;
        _saveManagementContent.Visible = true;
        _settingsContent.Visible = false;
        _bindingsContent.Visible = false;
        _confirmationContent.Visible = false;
        _focusSaveNameOnViewOpen = focusName;
        _saveStatusLabel.Text = string.Empty;
        SaveManager? saveManager = ActiveSaveManager();
        if (saveManager?.IsOperationBusy == true)
        {
            _refreshSaveSlotsWhenIdle = true;
            _saveManagerIdleFrames = 0;
            SetSaveOperationControlsDisabled(true);
            ShowSaveStatus("正在等待后台存档操作完成...", success: true);
            return;
        }
        RefreshSaveSlots(PreferredSaveSlotID());
        CallDeferred(MethodName.FocusSaveManagementControl);
    }

    private void ShowConfirmationView(
        string title,
        string message,
        ConfirmationAction action,
        MenuView returnView = MenuView.Main,
        string? slotID = null,
        string displayName = "",
        string operationToken = "")
    {
        if (IsSaveOperationBusy)
            return;
        _view = MenuView.Confirmation;
        _confirmationAction = action;
        _confirmationReturnView = returnView;
        _confirmationSlotID = slotID;
        _confirmationDisplayName = displayName;
        _confirmationOperationToken = operationToken;
        _mainContent.Visible = false;
        _saveManagementContent.Visible = false;
        _settingsContent.Visible = false;
        _bindingsContent.Visible = false;
        _confirmationContent.Visible = true;
        _confirmationTitle.Text = title;
        _confirmationMessage.Text = message;
        CallDeferred(MethodName.FocusCancelButton);
    }

    private void FocusContinueButton() => TryGrabDeferredFocus(_continueButton);

    private void FocusSettingsControl()
    {
        if (!IsInsideTree() || !Visible)
            return;
        if (_masterVolumeSlider.Editable)
            TryGrabDeferredFocus(_masterVolumeSlider);
        else
            TryGrabDeferredFocus(_settingsBackButton);
    }

    private void FocusSaveManagementControl()
    {
        if (!IsInsideTree() || !Visible)
            return;
        if (_focusSaveNameOnViewOpen || _saveSlotList.ItemCount == 0)
            TryGrabDeferredFocus(_saveNameInput);
        else
            TryGrabDeferredFocus(_saveSlotList);
    }

    private void FocusFirstBindingButton()
    {
        if (!IsInsideTree() || !Visible)
            return;
        foreach (InputBindingManager.BindingDefinition definition in InputBindingManager.Definitions)
        {
            if (_bindingButtons.TryGetValue(definition.ActionName, out Button? button))
            {
                TryGrabDeferredFocus(button);
                return;
            }
        }

        TryGrabDeferredFocus(_bindingsBackButton);
    }

    private void FocusCancelButton() => TryGrabDeferredFocus(_cancelButton);

    private void TryGrabDeferredFocus(Control control)
    {
        if (!IsInsideTree() || !Visible || !GodotObject.IsInstanceValid(control) ||
            !control.IsInsideTree() || !control.IsVisibleInTree() ||
            control.FocusMode == FocusModeEnum.None ||
            control is BaseButton { Disabled: true })
        {
            return;
        }
        control.GrabFocus();
    }

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

    private void OnContinuePressed()
    {
        if (!IsSaveOperationBusy && !_exitConvergencePending)
            ContinueRequested?.Invoke();
    }

    private void OnSavePressed() => ShowSaveManagementView(focusName: true);

    private void OnLoadPressed() => ShowSaveManagementView(focusName: false);

    private void RequestReturnToMainMenu()
    {
        if (IsSaveOperationBusy || _exitConvergencePending)
            return;
        ShowConfirmationView("结束当前城市？", "未保存的变更将丢失。", ConfirmationAction.ReturnToMainMenu);
    }

    private void RequestQuitToDesktop()
    {
        if (IsSaveOperationBusy || _exitConvergencePending)
            return;
        ShowConfirmationView("退出到桌面？", "未保存的变更将丢失。", ConfirmationAction.QuitToDesktop);
    }

    private void ConfirmRequestedAction()
    {
        if (IsSaveOperationBusy || _exitConvergencePending)
            return;
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
        if (IsSaveOperationBusy || _exitConvergencePending)
            return;
        if (_confirmationReturnView == MenuView.SaveManagement)
            ShowSaveManagementView(focusName: false);
        else
            ShowMainView();
    }

    private void CreateNamedSave()
    {
        if (IsSaveOperationBusy)
            return;
        string displayName = _saveNameInput.Text.Trim();
        if (displayName.Length == 0)
        {
            ShowSaveStatus("请输入存档名称", success: false);
            _saveNameInput.GrabFocus();
            return;
        }

        SaveManager? saveManager = ActiveSaveManager();
        if (saveManager == null)
        {
            ShowSaveStatus("存档管理不可用", success: false);
            return;
        }

        BeginSaveOperation(
            saveManager,
            saveManager.StartSaveAs(displayName),
            SaveOperationKind.Publish,
            MenuOperationAction.SaveAs,
            string.Empty,
            displayName);
    }

    private void OnSaveNameSubmitted(string submittedText) => CreateNamedSave();

    private void RequestOverwriteSave()
    {
        if (IsSaveOperationBusy)
            return;
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
        if (IsSaveOperationBusy)
            return;
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
        if (IsSaveOperationBusy)
            return;
        SaveSlotSummary? summary = SelectedSaveSlot();
        if (summary == null)
            return;
        SaveManager? saveManager = ActiveSaveManager();
        string operationToken = saveManager?.ArmDeletion(summary) ?? string.Empty;
        if (operationToken.Length == 0)
        {
            RefreshSaveSlots(summary.SlotID);
            ShowSaveStatus("存档列表已变化，请重新确认", success: false);
            return;
        }

        string displayName = summary.IsValid ? summary.DisplayName : summary.SlotID;
        ShowConfirmationView(
            "删除存档？",
            $"{ConfirmationSummary(summary)}\n删除后无法恢复。",
            ConfirmationAction.DeleteSave,
            MenuView.SaveManagement,
            summary.SlotID,
            displayName,
            operationToken);
    }

    private void OverwriteConfirmedSave()
    {
        SaveManager? saveManager = ActiveSaveManager();
        string? slotID = _confirmationSlotID;
        string displayName = _confirmationDisplayName;
        if (saveManager is null || slotID is null)
        {
            ShowSaveManagementView(focusName: false);
            ShowSaveStatus("覆盖存档失败", success: false);
            return;
        }
        BeginSaveOperation(
            saveManager,
            saveManager.StartSave(slotID),
            SaveOperationKind.Publish,
            MenuOperationAction.Overwrite,
            slotID,
            displayName);
    }

    private void LoadConfirmedSave()
    {
        SaveManager? saveManager = ActiveSaveManager();
        string? slotID = _confirmationSlotID;
        string displayName = _confirmationDisplayName;
        if (saveManager is null || slotID is null)
        {
            ShowSaveManagementView(focusName: false);
            ShowSaveStatus("加载存档失败", success: false);
            return;
        }
        BeginSaveOperation(
            saveManager,
            saveManager.StartLoad(slotID),
            SaveOperationKind.Load,
            MenuOperationAction.Load,
            slotID,
            displayName);
    }

    private void DeleteConfirmedSave()
    {
        SaveManager? saveManager = ActiveSaveManager();
        string? slotID = _confirmationSlotID;
        string displayName = _confirmationDisplayName;
        if (saveManager is null || slotID is null)
        {
            ShowSaveManagementView(focusName: false);
            ShowSaveStatus("删除存档失败", success: false);
            return;
        }
        string operationToken = saveManager.StartDeleteSlot(
            slotID,
            _confirmationOperationToken);
        if (operationToken.Length == 0)
        {
            ShowSaveManagementView(focusName: false);
            ShowSaveStatus("存档列表已变化，请重新确认", success: false);
            return;
        }
        BeginSaveOperation(
            saveManager,
            operationToken,
            SaveOperationKind.Delete,
            MenuOperationAction.Delete,
            slotID,
            displayName);
    }

    private void BeginSaveOperation(
        SaveManager saveManager,
        string operationToken,
        SaveOperationKind operationKind,
        MenuOperationAction menuOperation,
        string slotID,
        string displayName)
    {
        if (operationToken.Length == 0 || IsSaveOperationBusy)
        {
            ShowSaveManagementView(focusName: false);
            ShowSaveStatus("无法启动存档操作", success: false);
            return;
        }

        _activeOperationToken = operationToken;
        _activeOperationSlotID = slotID;
        _activeOperationDisplayName = displayName;
        _activeOperationKind = operationKind;
        _activeOperationPhase = SaveOperationPhase.Admission;
        _activeMenuOperation = menuOperation;
        _activeOperationCrossedBoundary = false;
        _activeOperationCancelRequested = false;
        _refreshSaveSlotsWhenIdle = false;
        _saveManagerIdleFrames = 0;
        _operationMenuGeneration = _menuOpenGeneration;
        _operationSceneGeneration = saveManager.SceneGeneration;

        _view = MenuView.SaveManagement;
        _mainContent.Visible = false;
        _saveManagementContent.Visible = true;
        _settingsContent.Visible = false;
        _bindingsContent.Visible = false;
        _confirmationContent.Visible = false;
        SetSaveOperationControlsDisabled(true);
        ShowSaveStatus(FormatOperationProgress(_activeOperationPhase), success: true);
    }

    private void OnSaveOperationStateChanged(SaveOperationState state)
    {
        if (!IsMatchingOperation(state.OperationToken, state.Kind))
            return;
        _activeOperationPhase = state.Phase;
        _activeOperationCrossedBoundary = state.HasCrossedCommitBoundary;
        _activeOperationCancelRequested |= state.CancellationRequested;
        ShowSaveStatus(FormatOperationProgress(state.Phase), success: true);
    }

    private void OnSaveOperationCompleted(SaveOperationResult result)
    {
        SaveManager? saveManager = ActiveSaveManager();
        if (saveManager is null || !IsOpen ||
            !IsMatchingOperation(result.OperationToken, result.Kind) ||
            _operationMenuGeneration != _menuOpenGeneration ||
            _operationSceneGeneration != saveManager.SceneGeneration)
        {
            return;
        }

        MenuOperationAction menuOperation = _activeMenuOperation;
        string displayName = _activeOperationDisplayName;
        string preferredSlotID = result.TargetSlotID;
        ClearSaveOperation();

        if (!result.IsSuccess)
        {
            RefreshSaveSlots(PreferredSaveSlotID());
            string message = result.ResultKind == SaveOperationResultKind.Canceled
                ? "操作已取消"
                : result.Error?.Length > 0
                    ? $"{OperationFailureLabel(menuOperation)}：{result.Error}"
                    : OperationFailureLabel(menuOperation);
            ShowSaveStatus(message, success: false);
            return;
        }

        string warningSuffix = result.Warnings.Count == 0
            ? string.Empty
            : $"（{string.Join("；", result.Warnings)}）";
        switch (menuOperation)
        {
            case MenuOperationAction.SaveAs:
                _saveNameInput.Text = string.Empty;
                RefreshSaveSlots(preferredSlotID);
                ShowSaveStatus($"已创建“{displayName}”{warningSuffix}", success: true);
                break;
            case MenuOperationAction.Overwrite:
                RefreshSaveSlots(preferredSlotID);
                ShowSaveStatus($"已覆盖“{displayName}”{warningSuffix}", success: true);
                break;
            case MenuOperationAction.Delete:
                RefreshSaveSlots(saveManager.CurrentSlotID);
                ShowSaveStatus($"已删除“{displayName}”{warningSuffix}", success: true);
                break;
            case MenuOperationAction.Load:
                if (result.Warnings.Count != 0)
                    GD.PushWarning($"Load completed with observer warnings: {string.Join("; ", result.Warnings)}");
                ShowSaveStatus($"已加载“{displayName}”{warningSuffix}", success: true);
                ContinueRequested?.Invoke();
                break;
        }
    }

    private bool IsMatchingOperation(string operationToken, SaveOperationKind operationKind) =>
        _activeOperationToken.Length != 0 &&
        string.Equals(_activeOperationToken, operationToken, StringComparison.Ordinal) &&
        _activeOperationKind == operationKind;

    private void ClearSaveOperation()
    {
        _activeOperationToken = string.Empty;
        _activeOperationSlotID = string.Empty;
        _activeOperationDisplayName = string.Empty;
        _activeOperationKind = default;
        _activeOperationPhase = default;
        _activeMenuOperation = MenuOperationAction.None;
        _activeOperationCrossedBoundary = false;
        _activeOperationCancelRequested = false;
        _operationMenuGeneration = 0;
        _operationSceneGeneration = 0;
        SetSaveOperationControlsDisabled(false);
    }

    private void SetSaveOperationControlsDisabled(bool disabled)
    {
        _continueButton.Disabled = disabled;
        _saveButton.Disabled = disabled;
        _loadButton.Disabled = disabled;
        _settingsButton.Disabled = disabled;
        _exitGameButton.Disabled = disabled;
        _exitDesktopButton.Disabled = disabled;
        _saveNameInput.Editable = !disabled;
        _saveAsButton.Disabled = disabled;
        _overwriteSaveButton.Disabled = disabled;
        _loadSaveButton.Disabled = disabled;
        _deleteSaveButton.Disabled = disabled;
        _saveManagementBackButton.Disabled = disabled;
        _confirmButton.Disabled = disabled;
        _cancelButton.Disabled = disabled;
        if (!disabled)
            UpdateSaveActionAvailability();
    }

    private string FormatOperationProgress(SaveOperationPhase phase)
    {
        string operation = _activeMenuOperation switch
        {
            MenuOperationAction.SaveAs => "创建",
            MenuOperationAction.Overwrite => "覆盖",
            MenuOperationAction.Load => "加载",
            MenuOperationAction.Delete => "删除",
            _ => "处理",
        };
        string phaseLabel = phase switch
        {
            SaveOperationPhase.Admission => "等待操作权限",
            SaveOperationPhase.Capture => "捕获快照",
            SaveOperationPhase.Recover => "恢复事务",
            SaveOperationPhase.Prepare => "准备数据",
            SaveOperationPhase.Preflight => "预检资源",
            SaveOperationPhase.Commit => "提交",
            SaveOperationPhase.Publish => "发布存档",
            SaveOperationPhase.Cleanup => "清理事务",
            SaveOperationPhase.Completed => "完成",
            _ => phase.ToString(),
        };
        return $"正在{operation}“{_activeOperationDisplayName}”：{phaseLabel}";
    }

    private static string OperationFailureLabel(MenuOperationAction operation) => operation switch
    {
        MenuOperationAction.SaveAs => "新建存档失败",
        MenuOperationAction.Overwrite => "覆盖存档失败",
        MenuOperationAction.Load => "加载存档失败",
        MenuOperationAction.Delete => "删除存档失败",
        _ => "存档操作失败",
    };

    private static long NextGeneration(long generation) =>
        generation == long.MaxValue ? 1 : generation + 1;

    private void RefreshSaveSlots(string? preferredSlotID)
    {
        _saveSlots.Clear();
        _saveSlotList.Clear();
        _selectedSaveSlotIndex = -1;

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

    private void OnSaveSlotSelected(long index)
    {
        _selectedSaveSlotIndex = index >= 0 && index < _saveSlots.Count ? (int)index : -1;
        UpdateSelectedSaveSummary();
    }

    private void UpdateSelectedSaveSummary()
    {
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
        _saveSlotSummaryLabel.TooltipText = summary.Warning ?? string.Empty;
        UpdateSaveActionAvailability();
    }

    private void UpdateSaveActionAvailability()
    {
        if (IsSaveOperationBusy)
        {
            _overwriteSaveButton.Disabled = true;
            _loadSaveButton.Disabled = true;
            _deleteSaveButton.Disabled = true;
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

    private string? PreferredSaveSlotID()
    {
        SaveSlotSummary? selected = SelectedSaveSlot();
        if (selected != null)
            return selected.SlotID;

        return ActiveSaveManager()?.CurrentSlotID;
    }

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
