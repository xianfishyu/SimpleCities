using Godot;
using SimpleCities.Core.V3;
using SimpleCities.Road.V3;

/// <summary>
/// V3 道路系统根节点：在真实 Godot 场景中持有 RoadGraphV3Application。
/// </summary>
public partial class RoadGraphV3System : Node2D
{
    [Export] public RoadConfigV3 Config { get; set; } = null!;

    public RoadGraphV3Application Application { get; private set; } = null!;

    public override void _Ready()
    {
        string root = ProjectSettings.GlobalizePath(V3SaveRoot.EditorRoot);
        Application = Config is not null
            ? new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default, Config)
            : new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
    }
}
