using Godot;

/// <summary>
/// 路网系统根节点 — 持有 RoadNetwork 实例并注入给子节点
/// </summary>
public partial class RoadSystem : Node2D
{
    public RoadNetwork Network { get; private set; } = null!;
    public static RoadSystem Instance { get; private set; } = null!;

    public override void _Ready()
    {
        Instance = this;
        Network = new RoadNetwork();

        var renderer = GetNode<RoadRenderer>("RoadRenderer");
        var builder = GetNode<RoadBuilder>("RoadBuilder");
        renderer.SetNetwork(Network);
        builder.SetNetwork(Network);
    }
}
