using Godot;

/// <summary>
/// 网格背景绘制器
/// 显示可缩放的网格线作为游戏背景
/// </summary>
public partial class Background : Node2D
{
    /// <summary>
    /// 网格大小（像素）
    /// </summary>
    [Export] public float GridSize = 50f;

    /// <summary>
    /// 网格线颜色
    /// </summary>
    [Export] public Color GridColor = Colors.Gray;

    /// <summary>
    /// 主网格线颜色（每10格显示一条粗线）
    /// </summary>
    [Export] public Color MainGridColor = Colors.Black;

    /// <summary>
    /// 网格线宽度
    /// </summary>
    [Export] public float LineWidth = 1f;

    /// <summary>
    /// 主网格线宽度
    /// </summary>
    [Export] public float MainLineWidth = 2f;

    /// <summary>
    /// 是否显示网格
    /// </summary>
    [Export] public bool ShowGrid = true;

    /// <summary>
    /// 背景颜色
    /// </summary>
    [Export] public Color BackgroundColor = Colors.White;

    private Camera2D camera;

    public override void _Ready()
    {
        // 设置 Z 轴在后面
        ZIndex = -100;

        // 获取相机引用
        camera = GetViewport().GetCamera2D();
    }

    public override void _Process(double delta)
    {
        if (ShowGrid)
        {
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (!ShowGrid || camera == null)
            return;

        // 绘制背景
        DrawBackground();

        // 绘制网格
        DrawGrid();
    }

    private void DrawBackground()
    {
        Camera2D cam = camera;
        Vector2 camPos = cam.GlobalPosition;
        float zoom = cam.Zoom.X;

        // 计算可见区域
        Rect2 viewport = GetViewportRect();
        Vector2 screenSize = viewport.Size;
        Vector2 topLeft = camPos - screenSize / (2 * zoom);
        Vector2 bottomRight = camPos + screenSize / (2 * zoom);

        // 绘制填充矩形作为背景
        Rect2 bgRect = new Rect2(topLeft, bottomRight - topLeft);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        DrawRect(bgRect, BackgroundColor);
    }

    private void DrawGrid()
    {
        Camera2D cam = camera;
        Vector2 camPos = cam.GlobalPosition;
        float zoom = cam.Zoom.X;

        // 计算可见区域
        Rect2 viewport = GetViewportRect();
        Vector2 screenSize = viewport.Size;
        Vector2 topLeft = camPos - screenSize / (2 * zoom);
        Vector2 bottomRight = camPos + screenSize / (2 * zoom);

        // 计算起始和结束的网格坐标
        int startX = (int)(topLeft.X / GridSize);
        int endX = (int)(bottomRight.X / GridSize) + 1;
        int startY = (int)(topLeft.Y / GridSize);
        int endY = (int)(bottomRight.Y / GridSize) + 1;

        // 绘制竖线
        for (int x = startX; x <= endX; x++)
        {
            float worldX = x * GridSize;

            // 判断是否是主网格线（每10格）
            bool isMainLine = x % 10 == 0;
            Color color = isMainLine ? MainGridColor : GridColor;
            float width = isMainLine ? MainLineWidth : LineWidth;

            Vector2 p1 = new Vector2(worldX, topLeft.Y);
            Vector2 p2 = new Vector2(worldX, bottomRight.Y);

            DrawLine(p1, p2, color, width);
        }

        // 绘制横线
        for (int y = startY; y <= endY; y++)
        {
            float worldY = y * GridSize;

            // 判断是否是主网格线（每10格）
            bool isMainLine = y % 10 == 0;
            Color color = isMainLine ? MainGridColor : GridColor;
            float width = isMainLine ? MainLineWidth : LineWidth;

            Vector2 p1 = new Vector2(topLeft.X, worldY);
            Vector2 p2 = new Vector2(bottomRight.X, worldY);

            DrawLine(p1, p2, color, width);
        }

        // 可选：绘制原点标记
        DrawOriginMarker();
    }

    private void DrawOriginMarker()
    {
        // 在原点(0,0)绘制一个十字标记
        Vector2 origin = Vector2.Zero;
        float markerSize = 20f;
        Color markerColor = new Color(1f, 0f, 0f, 0.8f);

        // 绘制十字
        DrawLine(origin - Vector2.Right * markerSize, origin + Vector2.Right * markerSize, markerColor, 2f);
        DrawLine(origin - Vector2.Down * markerSize, origin + Vector2.Down * markerSize, markerColor, 2f);
    }

    /// <summary>
    /// 切换网格显示
    /// </summary>
    public void ToggleGrid()
    {
        ShowGrid = !ShowGrid;
    }
}
