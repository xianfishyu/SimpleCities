using System;
using Godot;

public partial class SystemControls : PanelContainer
{
    private Button _saveButton = null!;
    private Button _loadButton = null!;
    private Label _statusLabel = null!;

    public event Action? SaveRequested;
    public event Action? LoadRequested;

    public NodePath SaveFocusPath => _saveButton.GetPath();
    public NodePath LoadFocusPath => _loadButton.GetPath();

    public override void _Ready()
    {
        _saveButton = GetNode<Button>("PanelMargin/Controls/Buttons/SaveButton");
        _loadButton = GetNode<Button>("PanelMargin/Controls/Buttons/LoadButton");
        _statusLabel = GetNode<Label>("PanelMargin/Controls/StatusLabel");

        _saveButton.FocusMode = FocusModeEnum.All;
        _loadButton.FocusMode = FocusModeEnum.All;
        _saveButton.Pressed += OnSavePressed;
        _loadButton.Pressed += OnLoadPressed;
    }

    public override void _ExitTree()
    {
        if (_saveButton != null)
            _saveButton.Pressed -= OnSavePressed;
        if (_loadButton != null)
            _loadButton.Pressed -= OnLoadPressed;
    }

    public void ShowStatus(string message, bool success)
    {
        _statusLabel.Text = message;
        _statusLabel.AddThemeColorOverride("font_color", success ? new Color("#52C878") : new Color("#FF6B6B"));
    }

    public void ConfigureFocus(NodePath previousPath, NodePath nextPath)
    {
        _saveButton.FocusPrevious = previousPath;
        _saveButton.FocusNext = _loadButton.GetPath();
        _loadButton.FocusPrevious = _saveButton.GetPath();
        _loadButton.FocusNext = nextPath;
    }

    private void OnSavePressed() => SaveRequested?.Invoke();

    private void OnLoadPressed() => LoadRequested?.Invoke();
}
