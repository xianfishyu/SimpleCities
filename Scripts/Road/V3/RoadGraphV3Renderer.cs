using Godot;
using SimpleCities.Road.V3;
using System.Linq;

/// <summary>
/// V3 最小道路渲染器：从 RoadGraphV3System 的 controller 读取权威几何，
/// 按 RoadTypeStyle 绘制折线预览，不修改图数据。
/// </summary>
public partial class RoadGraphV3Renderer : Node2D
{
    [Export] public float DisplayTolerance { get; set; } = RoadGeometryDisplaySampler.DefaultTolerance;

    private GraphStateToken? _lastToken;

    public override void _Process(double delta)
    {
        RoadGraphV3System? system = RoadGraphV3System.Instance;
        if (system is null)
            return;

        GraphStateToken current = system.Controller.Facade.CurrentToken;
        if (_lastToken == current)
            return;

        _lastToken = current;
        QueueRedraw();
    }

    public override void _Draw()
    {
        RoadGraphV3System? system = RoadGraphV3System.Instance;
        if (system is null)
            return;

        var revision = system.Controller.Facade.Revision;
        var styles = system.Application.DefaultStyles;
        foreach (RoadGraphV3Edge edge in revision.Edges.Values)
        {
            if (!styles.TryGet(edge.RoadType, out RoadTypeStyle? style))
                continue;

            Vector2[] points = RoadGeometryDisplaySampler.SampleSegments(edge.Geometry, DisplayTolerance);
            if (points.Length < 2)
                continue;

            DrawPolyline(points, style.Color, style.Width, true);

            if (RoadRibbonBuilder.TryBuild(edge, style, DisplayTolerance, out RoadRibbonMeshData ribbon))
            {
                Vector2[] outline = ribbon.ToOutlineVertices().ToArray();
                if (outline.Length >= 3)
                {
                    var colors = new Color[outline.Length];
                    for (int index = 0; index < colors.Length; index++)
                        colors[index] = style.Color;
                    DrawPolygon(outline, colors);
                }
            }
        }
    }
}
