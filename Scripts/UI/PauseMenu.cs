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
        Settings,
        Bindings,
        Confirmation,
    }

    private enum ConfirmationAction
    {
        ReturnToMainMenu,
        QuitToDesktop,
    }

    private const float SilentVolumeDb = -60f;

    private Button _continueButton = null!;
    private Button _saveButton = null!;
    private Button _loadButton = null!;
    private Button _settingsButton = null!;
    private Button _exitGameButton = null!;
    private Button _exitDesktopButton = null!;
    private Label _statusLabel = null!;
    private Control _mainContent = null!;
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
    private int _masterBusIndex = -1;
    private string? _capturingAction;
    private readonly Dictionary<string, Button> _bindingButtons = new(StringComparer.Ordinal);
    private Control? _focusBeforeOpen;

    public event Action? ContinueRequested;
    public event Action? SaveRequested;
    public event Action? LoadRequested;
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

        if (_view == MenuView.Confirmation)
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
        _statusLabel.Text = string.Empty;
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

    /// <summary>将存取档结果显示在暂停菜单内；操作完成后菜单保持打开和暂停状态。</summary>
    public void ShowStatus(string message, bool success)
    {
        _statusLabel.Text = message;
        _statusLabel.AddThemeColorOverride("font_color", success ? new Color("#52C878") : new Color("#FF6B6B"));
    }

    private void ResolveNodes()
    {
        _mainContent = GetNode<Control>("Center/MainPanel/MainContent");
        _settingsContent = GetNode<Control>("Center/MainPanel/SettingsContent");
        _bindingsContent = GetNode<Control>("Center/MainPanel/BindingsContent");
        _confirmationContent = GetNode<Control>("Center/MainPanel/ConfirmationContent");
        _continueButton = GetNode<Button>("Center/MainPanel/MainContent/ContinueButton");
        _saveButton = GetNode<Button>("Center/MainPanel/MainContent/SaveButton");
        _loadButton = GetNode<Button>("Center/MainPanel/MainContent/LoadButton");
        _settingsButton = GetNode<Button>("Center/MainPanel/MainContent/SettingsButton");
        _exitGameButton = GetNode<Button>("Center/MainPanel/MainContent/ExitGameButton");
        _exitDesktopButton = GetNode<Button>("Center/MainPanel/MainContent/ExitDesktopButton");
        _statusLabel = GetNode<Label>("Center/MainPanel/MainContent/StatusLabel");
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
        _masterVolumeSlider.ValueChanged += OnMasterVolumeChanged;
        _muteToggle.Toggled += OnMuteToggled;
        _keyBindingsButton.Pressed += ShowBindingsView;
        _settingsBackButton.Pressed += ShowMainView;
        _resetBindingsButton.Pressed += ResetBindings;
        _bindingsBackButton.Pressed += ShowSettingsView;
        _confirmButton.Pressed += ConfirmRequestedAction;
        _cancelButton.Pressed += ShowMainView;
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
        _masterVolumeSlider.ValueChanged -= OnMasterVolumeChanged;
        _muteToggle.Toggled -= OnMuteToggled;
        _keyBindingsButton.Pressed -= ShowBindingsView;
        _settingsBackButton.Pressed -= ShowMainView;
        _resetBindingsButton.Pressed -= ResetBindings;
        _bindingsBackButton.Pressed -= ShowSettingsView;
        _confirmButton.Pressed -= ConfirmRequestedAction;
        _cancelButton.Pressed -= ShowMainView;
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
        _settingsContent.Visible = true;
        _bindingsContent.Visible = false;
        _confirmationContent.Visible = false;
        CallDeferred(MethodName.FocusSettingsControl);
    }

    private void ShowBindingsView()
    {
        _view = MenuView.Bindings;
        _mainContent.Visible = false;
        _settingsContent.Visible = false;
        _bindingsContent.Visible = true;
        _confirmationContent.Visible = false;
        _bindingStatusLabel.Text = string.Empty;
        RefreshBindingButtons();
        CallDeferred(MethodName.FocusFirstBindingButton);
    }

    private void ShowConfirmationView(string title, string message, ConfirmationAction action)
    {
        _view = MenuView.Confirmation;
        _confirmationAction = action;
        _mainContent.Visible = false;
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

    private void OnSavePressed() => SaveRequested?.Invoke();

    private void OnLoadPressed() => LoadRequested?.Invoke();

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
        if (_confirmationAction == ConfirmationAction.ReturnToMainMenu)
            ReturnToMainMenuRequested?.Invoke();
        else
            QuitToDesktopRequested?.Invoke();
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
