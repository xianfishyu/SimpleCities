using Godot;

/// <summary>区分分类按钮和工具按钮，决定选中指示器的呈现方式。</summary>
public enum ConstructionDockButtonVisualRole
{
    PrimaryCategory,
    SecondaryTool,
}

/// <summary>
/// 建造栏按钮的通用呈现组件。场景中预置的分类按钮和运行时生成的工具按钮都复用它。
/// </summary>
public partial class ConstructionDockButton : Button
{
    private TextureRect? _icon;
    private Label? _label;
    private ColorRect? _primarySelectionIndicator;
    private bool _isReady;

    private Texture2D? _iconTexture;
    private string _displayText = string.Empty;
    private bool _selected;
    private ConstructionDockButtonVisualRole _visualRole = ConstructionDockButtonVisualRole.PrimaryCategory;

    [Export]
    public ConstructionDockButtonVisualRole VisualRole
    {
        get => _visualRole;
        set
        {
            _visualRole = value;
            SynchronizePresentation();
        }
    }

    [Export]
    public Texture2D? IconTexture
    {
        get => _iconTexture;
        set
        {
            _iconTexture = value;
            SynchronizePresentation();
        }
    }

    [Export]
    public string DisplayText
    {
        get => _displayText;
        set
        {
            _displayText = value ?? string.Empty;
            SynchronizePresentation();
        }
    }

    [Export]
    public bool Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            SynchronizePresentation();
        }
    }

    /// <summary>取得由场景或 ConstructionDock 动态创建的视觉子节点后完成首次同步。</summary>
    public override void _Ready()
    {
        Text = string.Empty;
        _icon = GetNodeOrNull<TextureRect>("Presentation/Icon");
        _label = GetNodeOrNull<Label>("Presentation/Label");
        _primarySelectionIndicator = GetNodeOrNull<ColorRect>("PrimarySelectionIndicator");
        _isReady = true;

        SynchronizePresentation();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged)
            SynchronizePresentation();
    }

    public override void _Draw() => SynchronizePresentation();

    public override void _ExitTree()
    {
        _isReady = false;
        _icon = null;
        _label = null;
        _primarySelectionIndicator = null;
    }

    /// <summary>将导出的图标、文字、选中态和主题色同步到内部视觉节点。</summary>
    private void SynchronizePresentation()
    {
        Text = string.Empty;
        if (!_isReady)
            return;

        PresentationColors colors = ResolvePresentationColors();

        if (_icon != null)
        {
            _icon.Texture = _iconTexture;
            _icon.Modulate = colors.Icon;
        }

        if (_label != null)
        {
            _label.Text = _displayText;
            _label.AddThemeColorOverride("font_color", colors.Label);
        }

        if (_primarySelectionIndicator != null)
        {
            _primarySelectionIndicator.Visible = _selected
                && _visualRole == ConstructionDockButtonVisualRole.PrimaryCategory;
            _primarySelectionIndicator.Color = colors.Indicator;
        }
    }

    /// <summary>依次按禁用、选中、默认状态解析语义主题色，并保留 Godot 默认主题作为后备。</summary>
    private PresentationColors ResolvePresentationColors()
    {
        if (Disabled)
        {
            Color disabledColor = ResolveThemeColor("disabled_color", "font_disabled_color");
            return new PresentationColors(disabledColor, disabledColor, disabledColor);
        }

        if (_selected)
        {
            Color selectedIconColor = ResolveThemeColor("selected_color", "font_pressed_color");
            Color selectedLabelColor = ResolveThemeColor("selected_label_color", "font_pressed_color");
            return new PresentationColors(selectedIconColor, selectedLabelColor, selectedIconColor);
        }

        Color primaryColor = ResolveThemeColor("primary_color", "font_color");
        return new PresentationColors(primaryColor, primaryColor, primaryColor);
    }

    private Color ResolveThemeColor(string semanticName, string fallbackName)
    {
        string themeType = ThemeTypeVariation.ToString();
        if (Theme != null && themeType.Length > 0 && Theme.HasColor(semanticName, themeType))
            return Theme.GetColor(semanticName, themeType);

        if (themeType.Length > 0 && HasThemeColor(semanticName, themeType))
            return GetThemeColor(semanticName, themeType);

        if (HasThemeColor(semanticName))
            return GetThemeColor(semanticName);

        if (Theme != null && themeType.Length > 0 && Theme.HasColor(fallbackName, themeType))
            return Theme.GetColor(fallbackName, themeType);

        if (themeType.Length > 0 && HasThemeColor(fallbackName, themeType))
            return GetThemeColor(fallbackName, themeType);

        if (HasThemeColor(fallbackName))
            return GetThemeColor(fallbackName);

        return Colors.White;
    }

    private readonly record struct PresentationColors(Color Icon, Color Label, Color Indicator);
}
