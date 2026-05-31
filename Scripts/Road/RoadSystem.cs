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
        var config = builder.Config; // 从 RoadBuilder 的 Export 中取 RoadConfig

        // 注入网格系统（所有模块共用一份 CellSize）
        GridSystem.Config = config;

        renderer.SetNetwork(Network);
        builder.SetNetwork(Network);

        SaveManager.Instance.Register(Network);
    }
}
