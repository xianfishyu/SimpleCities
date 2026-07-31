using Godot;

public partial class ConstructionDockButton : Button
{
    private TextureRect? _icon;
    private Label? _label;
    private ColorRect? _selectedUnderline;
    private bool _isReady;

    private Texture2D? _iconTexture;
    private string _displayText = string.Empty;
    private bool _selected;

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

    public override void _Ready()
    {
        Text = string.Empty;
        _icon = GetNodeOrNull<TextureRect>("Presentation/Icon");
        _label = GetNodeOrNull<Label>("Presentation/Label");
        _selectedUnderline = GetNodeOrNull<ColorRect>("Presentation/SelectedUnderline");
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
        _selectedUnderline = null;
    }

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

        if (_selectedUnderline != null)
        {
            _selectedUnderline.Visible = _selected;
            _selectedUnderline.Color = colors.Underline;
        }
    }

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

    private readonly record struct PresentationColors(Color Icon, Color Label, Color Underline);
}
