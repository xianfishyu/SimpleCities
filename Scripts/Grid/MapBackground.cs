using Godot;

/// <summary>
/// 地图背景渲染器 — 纯白底色 + 网格线
/// 地形后续在编辑器中手动绘制
/// </summary>
public partial class MapBackground : CanvasLayer
{
    public static MapBackground Instance { get; private set; }

    // ═══════════════════════════════════════════
    // 背景设置
    // ═══════════════════════════════════════════

    [ExportGroup("背景设置")]
    [Export] public Color BackgroundColor = Colors.White;

    // ═══════════════════════════════════════════
    // 网格设置
    // ═══════════════════════════════════════════

    [ExportGroup("网格设置")]
    [Export] public float MajorGridSize = 500f;
    [Export] public float MainLineWidth = 2f;
    [Export] public Color MajorGridColor = new(0.85f, 0.85f, 0.85f);

    [Export] public float MinorGridSize = 100f;
    [Export] public float LineWidth = 0.5f;
    [Export] public Color MinorGridColor = new(0.92f, 0.92f, 0.92f);

    [Export] public float DotGridSize = 10f;
    [Export] public float DotRadius = 0.5f;
    [Export] public Color DotColor = new(0.75f, 0.75f, 0.75f);

    // ═══════════════════════════════════════════
    // 显示设置
    // ═══════════════════════════════════════════

    [ExportGroup("显示设置")]
    [Export] public bool ShowGrid = true;
    [Export] public bool ShowMainGrid = true;
    [Export] public bool ShowMinorGrid = true;
    [Export] public bool ShowDotGrid = true;

    // ═══════════════════════════════════════════
    // 节点引用
    // ═══════════════════════════════════════════

    [ExportGroup("节点引用")]
    [Export] public ColorRect Display;

    private ShaderMaterial _shaderMaterial;

    public override void _Ready()
    {
        Instance ??= this;

        Display.Visible = true;
        Display.AnchorLeft = 0;
        Display.AnchorTop = 0;
        Display.AnchorRight = 1;
        Display.AnchorBottom = 1;
        Display.OffsetLeft = 0;
        Display.OffsetTop = 0;
        Display.OffsetRight = 0;
        Display.OffsetBottom = 0;

        _shaderMaterial = Display.Material as ShaderMaterial;
    }

    public override void _Process(double delta)
    {
        if (_shaderMaterial == null)
            return;

        MainCamera camera = MainCamera.Instance;
        Vector2 cameraPos = camera?.GlobalPosition ?? Vector2.Zero;
        float cameraZoom = camera?.Zoom.X ?? 1.0f;
        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;

        // 背景
        _shaderMaterial.SetShaderParameter("background_color",
            new Vector3(BackgroundColor.R, BackgroundColor.G, BackgroundColor.B));

        // 网格
        _shaderMaterial.SetShaderParameter("major_grid_size", MajorGridSize);
        _shaderMaterial.SetShaderParameter("major_line_width", MainLineWidth);
        _shaderMaterial.SetShaderParameter("major_grid_color",
            new Vector3(MajorGridColor.R, MajorGridColor.G, MajorGridColor.B));
        _shaderMaterial.SetShaderParameter("minor_grid_size", MinorGridSize);
        _shaderMaterial.SetShaderParameter("minor_line_width", LineWidth);
        _shaderMaterial.SetShaderParameter("minor_grid_color",
            new Vector3(MinorGridColor.R, MinorGridColor.G, MinorGridColor.B));
        _shaderMaterial.SetShaderParameter("show_major_grid", ShowMainGrid && ShowGrid);
        _shaderMaterial.SetShaderParameter("show_minor_grid", ShowMinorGrid && ShowGrid);
        _shaderMaterial.SetShaderParameter("show_dot_grid", ShowDotGrid && ShowGrid);
        _shaderMaterial.SetShaderParameter("dot_grid_size", DotGridSize);
        _shaderMaterial.SetShaderParameter("dot_radius", DotRadius);
        _shaderMaterial.SetShaderParameter("dot_color",
            new Vector3(DotColor.R, DotColor.G, DotColor.B));

        // 相机
        _shaderMaterial.SetShaderParameter("camera_pos", cameraPos);
        _shaderMaterial.SetShaderParameter("camera_zoom", cameraZoom);
        _shaderMaterial.SetShaderParameter("viewport_size", viewportSize);
    }

    public void ToggleGrid()
    {
        ShowGrid = !ShowGrid;
    }
}
