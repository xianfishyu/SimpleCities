using Godot;
using SimpleCities.Road.V3;

/// <summary>
/// V3 最小道路渲染器：从 RoadGraphV3System 的 controller 读取权威几何，
/// 按 RoadTypeStyle 绘制折线预览，不修改图数据。
/// </summary>
public partial class RoadGraphV3Renderer : Node2D
{
    [Export] public float DisplayTolerance { get; set; } = RoadGeometryDisplaySampler.DefaultTolerance;

    public override void _Process(double delta)
    {
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
        }
    }
}
