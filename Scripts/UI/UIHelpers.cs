using Godot;

/// <summary>
/// 共享 UI 工厂方法 — 确保 HUD / 面板 / 弹窗的控件外观一致。
/// 避免各处硬编码 font_size / font_color / 尺寸。
/// </summary>
public static class UIHelpers
{
    private static readonly Color DefaultTextColor = new(0.9f, 0.9f, 0.9f);

    /// <summary>创建带统一样式的 Label。</summary>
    public static Label CreateLabel(string text, int fontSize = 13)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", DefaultTextColor);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        return label;
    }

    /// <summary>创建工具箱按钮（触发工具切换）。</summary>
    public static Button CreateToolButton(string text, ToolType tool, System.Action<ToolType> onPressed)
    {
        var btn = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(64, 28),
        };
        btn.AddThemeFontSizeOverride("font_size", 12);
        btn.Pressed += () => onPressed(tool);
        return btn;
    }

    /// <summary>创建半透明深色背景 Panel（HUD / 弹出面板通用）。</summary>
    public static Panel CreateDarkPanel(Vector2 position, Vector2 size, float alpha = 0.88f)
    {
        return new Panel
        {
            Position = position,
            Size = size,
            SelfModulate = new Color(0.08f, 0.08f, 0.08f, alpha)
        };
    }
}
