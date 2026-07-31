using Godot;

/// <summary>
/// 地图背景渲染器 — 暗色底 + 网格线（对齐原点）
/// </summary>
public partial class MapBackground : CanvasLayer
{
    public static MapBackground Instance { get; private set; } = null!;

    // ═══════════════════════════════════════════
    // RoadConfig 引用
    // ═══════════════════════════════════════════

    [ExportGroup("配置引用")]
    /// <summary>道路配置资源，网格大小和偏移从 CellSize 自动推导</summary>
    [Export] public RoadConfig Config { get; set; } = null!;

    // ═══════════════════════════════════════════
    // 背景设置
    // ═══════════════════════════════════════════

    [ExportGroup("背景设置")]
    [Export] public Color BackgroundColor = new(0.118f, 0.118f, 0.118f);

    // ═══════════════════════════════════════════
    // 网格设置
    // ═══════════════════════════════════════════

    [ExportGroup("网格设置")]
    /// <summary>主网格 = CellSize × 此倍数</summary>
    [Export(PropertyHint.Range, "1,20,1")] public int MajorGridCells = 5;
    [Export] public float MainLineWidth = 1.5f;
    [Export] public Color MajorGridColor = new(0.25f, 0.25f, 0.25f);

    /// <summary>次网格 = CellSize × 此倍数</summary>
    [Export(PropertyHint.Range, "1,10,1")] public int MinorGridCells = 1;
    [Export] public float LineWidth = 0.5f;
    [Export] public Color MinorGridColor = new(0.18f, 0.18f, 0.18f);

    [Export] public float DotGridSize = 10f;
    [Export] public float DotRadius = 0.5f;
    [Export] public Color DotColor = new(0.20f, 0.20f, 0.20f);

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
    [Export] public ColorRect Display = null!;

    private ShaderMaterial? _shaderMaterial;
    private Vector2 _gridOffset;

    public override void _Ready()
    {
		Instance = this;

        // 回退到默认 RoadConfig
        if (Config == null)
        {
            GD.PushError("MapBackground: Config (RoadConfig resource) is not assigned in the scene.");
            Config = new RoadConfig();
        }

        _gridOffset = Vector2.Zero;

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

	public override void _ExitTree()
	{
		if (ReferenceEquals(Instance, this))
			Instance = null!;
		_shaderMaterial = null;
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

        // 网格偏移（从 Config.CellSize 推导）
        _shaderMaterial.SetShaderParameter("grid_offset", _gridOffset);

        // 网格（尺寸 = CellSize × 倍数，Inspector 设倍数不会被覆盖）
        float cellSize = Config.CellSize;
        _shaderMaterial.SetShaderParameter("major_grid_size", cellSize * MajorGridCells);
        _shaderMaterial.SetShaderParameter("major_line_width", MainLineWidth);
        _shaderMaterial.SetShaderParameter("major_grid_color",
            new Vector3(MajorGridColor.R, MajorGridColor.G, MajorGridColor.B));
        _shaderMaterial.SetShaderParameter("minor_grid_size", cellSize * MinorGridCells);
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
