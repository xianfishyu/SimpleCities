using Godot;
using SimpleCities.Core.V3;
using SimpleCities.Road.V3;

/// <summary>
/// V3 道路系统根节点：在真实 Godot 场景中持有 RoadGraphV3Application。
/// </summary>
public partial class RoadGraphV3System : Node2D
{
    [Export] public RoadConfigV3 Config { get; set; } = null!;

    public static RoadGraphV3System Instance { get; private set; } = null!;
    public RoadGraphV3Application Application { get; private set; } = null!;
    public RoadGraphV3Controller Controller => Application.Controller;
    public RoadToolState ToolState => Application.ToolState;

    public bool TryBuildFromPolyline(
        System.Collections.Generic.IReadOnlyList<Vector2> points,
        RoadType roadType,
        out RoadGraphV3ChangeSummary summary) =>
        Application.TryBuildFromPolyline(points, roadType, out summary);

    public bool SaveCurrent(
        string displayName,
        string cityName,
        string timestamp,
        long? population,
        decimal? funds,
        string? thumbnailFile) =>
        Application.SaveCurrent(displayName, cityName, timestamp, population, funds, thumbnailFile);

    public bool Load(string slotId, long lineageID = 1) =>
        Application.Load(slotId, lineageID);

    public bool DeleteCurrentSlot() =>
        Application.DeleteCurrentSlot();

    public override void _Ready()
    {
        Instance = this;
        string root = ProjectSettings.GlobalizePath(V3SaveRoot.EditorRoot);
        Application = Config is not null
            ? new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default, Config)
            : new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
    }

    public override void _ExitTree()
    {
        if (ReferenceEquals(Instance, this))
            Instance = null!;
    }
}
