using Godot;

/// <summary>
/// 路网系统根节点 — 持有 RoadGraph 实例并注入给子节点
/// </summary>
public partial class RoadSystem : Node2D
{
    public RoadGraph Graph { get; private set; } = null!;
    public static RoadSystem Instance { get; private set; } = null!;

    public override void _Ready()
    {
        Instance = this;
        Graph = new RoadGraph();

        var renderer = GetNode<RoadRenderer>("RoadRenderer");
        var builder = GetNode<RoadBuilder>("RoadBuilder");
        var config = builder.Config;

        GridSystem.Config = config;

        renderer.SetGraph(Graph);
        builder.SetGraph(Graph);

        SaveManager.Instance.Register(Graph);
    }
}
