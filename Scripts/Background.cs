using Godot;

/// <summary>
/// 网格背景渲染器（使用Shader）
/// 性能优化版本，所有网格通过GPU渲染
/// </summary>
public partial class Background : CanvasLayer
{
    public static Background Instance { get; private set; }

    /// <summary>
    /// 主网格大小（像素）
    /// </summary>
    [Export] public float MajorGridSize = 500f;

    /// <summary>
    /// 次网格大小（像素）
    /// </summary>
    [Export] public float MinorGridSize = 100f;

    /// <summary>
    /// 主网格线宽度
    /// </summary>
    [Export] public float MainLineWidth = 2f;

    /// <summary>
    /// 次网格线宽度
    /// </summary>
    [Export] public float LineWidth = 1f;

    /// <summary>
    /// 点网格大小（像素）
    /// </summary>
    [Export] public float DotGridSize = 10f;

    /// <summary>
    /// 点的半径
    /// </summary>
    [Export] public float DotRadius = 0.5f;

    /// <summary>
    /// 点网格跳过因子（性能优化）
    /// </summary>
    [Export] public int DotSkipFactor = 1;

    /// <summary>
    /// 是否显示背景颜色
    /// </summary>
    [Export] public bool ShowBackground = true;

    /// <summary>
    /// 是否显示网格
    /// </summary>
    [Export] public bool ShowGrid = true;

    /// <summary>
    /// 是否显示主网格
    /// </summary>
    [Export] public bool ShowMainGrid = true;

    /// <summary>
    /// 是否显示次级网格
    /// </summary>
    [Export] public bool ShowMinorGrid = true;

    /// <summary>
    /// 是否显示点网格
    /// </summary>
    [Export] public bool ShowDotGrid = true;

    /// <summary>
    /// 背景颜色
    /// </summary>
    [Export] public Color BackgroundColor = new Color(128f/255f, 128f/255f, 128f/255f);

    private ColorRect gridDisplay;
    private ShaderMaterial shaderMaterial;

    public override void _Ready()
    {
        Instance = this;

        // 创建 ColorRect 来显示 Shader
        gridDisplay = new ColorRect();
        gridDisplay.AnchorLeft = 0;
        gridDisplay.AnchorTop = 0;
        gridDisplay.AnchorRight = 1;
        gridDisplay.AnchorBottom = 1;
        gridDisplay.OffsetLeft = 0;
        gridDisplay.OffsetTop = 0;
        gridDisplay.OffsetRight = 0;
        gridDisplay.OffsetBottom = 0;

        // 加载并应用 Shader
        var shader = GD.Load<Shader>("res://Shaders/Grid.gdshader");
        shaderMaterial = new ShaderMaterial();
        shaderMaterial.Shader = shader;
        gridDisplay.Material = shaderMaterial;

        AddChild(gridDisplay);
    }

    public override void _Process(double delta)
    {
        if (shaderMaterial == null)
            return;

        // 获取相机信息
        Camera2D camera = GetViewport().GetCamera2D();
        Vector2 cameraPos = camera?.GlobalPosition ?? Vector2.Zero;
        float cameraZoom = camera?.Zoom.X ?? 1.0f;
        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;

        // 更新 Shader 参数
        shaderMaterial.SetShaderParameter("major_grid_size", MajorGridSize);
        shaderMaterial.SetShaderParameter("major_line_width", MainLineWidth);
        shaderMaterial.SetShaderParameter("minor_grid_size", MinorGridSize);
        shaderMaterial.SetShaderParameter("minor_line_width", LineWidth);
        shaderMaterial.SetShaderParameter("dot_grid_size", DotGridSize);
        shaderMaterial.SetShaderParameter("dot_radius", DotRadius);
        shaderMaterial.SetShaderParameter("dot_skip_factor", DotSkipFactor);

        shaderMaterial.SetShaderParameter("show_background", ShowBackground);
        shaderMaterial.SetShaderParameter("show_major_grid", ShowMainGrid && ShowGrid);
        shaderMaterial.SetShaderParameter("show_minor_grid", ShowMinorGrid && ShowGrid);
        shaderMaterial.SetShaderParameter("show_dot_grid", ShowDotGrid && ShowGrid);
        shaderMaterial.SetShaderParameter("background_color", BackgroundColor);
        
        // 传递相机和视口参数
        shaderMaterial.SetShaderParameter("camera_pos", cameraPos);
        shaderMaterial.SetShaderParameter("camera_zoom", cameraZoom);
        shaderMaterial.SetShaderParameter("viewport_size", viewportSize);
    }

    /// <summary>
    /// 切换网格显示
    /// </summary>
    public void ToggleGrid()
    {
        ShowGrid = !ShowGrid;
    }
}
