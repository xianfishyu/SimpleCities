using Godot;
using SimpleCities.Road.V3;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// V3 最小道路渲染器：仅在 GraphStateToken 变化时从 RoadGraphV3System 重建 ribbon 网格缓存，
/// 绘制填充路面；不修改图数据。
/// </summary>
public partial class RoadGraphV3Renderer : Node2D
{
    [Export] public float DisplayTolerance { get; set; } = RoadGeometryDisplaySampler.DefaultTolerance;

    private GraphStateToken? _lastToken;
    private IReadOnlyList<RoadRibbonMeshData> _cachedMeshes = [];

    public override void _Process(double delta)
    {
        RoadGraphV3System? system = RoadGraphV3System.Instance;
        if (system is null)
            return;

        GraphStateToken current = system.Controller.Facade.CurrentToken;
        if (_lastToken == current)
            return;

        _lastToken = current;
        _cachedMeshes = system.Application.BuildDefaultRibbonMeshes(DisplayTolerance);
        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (RoadRibbonMeshData ribbon in _cachedMeshes)
        {
            Vector2[] outline = ribbon.ToOutlineVertices().ToArray();
            if (outline.Length < 3)
                continue;

            Color color = ribbon.Colors.Count > 0 ? ribbon.Colors[0] : Colors.White;
            var colors = new Color[outline.Length];
            for (int index = 0; index < colors.Length; index++)
                colors[index] = color;

            DrawPolygon(outline, colors);
        }
    }
}
