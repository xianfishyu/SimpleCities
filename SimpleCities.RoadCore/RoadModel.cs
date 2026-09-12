namespace SimpleCities.RoadCore;

public readonly record struct NodeId
{
    public NodeId(long value)
    {
        if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }
    public long Value { get; }
}

public readonly record struct EdgeId
{
    public EdgeId(long value)
    {
        if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }
    public long Value { get; }
}

public readonly record struct RoadProfileId
{
    public RoadProfileId(string value)
    {
        if (value is not ("dirt" or "street" or "arterial" or "highway"))
            throw new ArgumentException("Unknown road profile.", nameof(value));
        Value = value;
    }
    public string Value { get; }
    public bool IsValid => Value is "dirt" or "street" or "arterial" or "highway";
    public static RoadProfileId Dirt => new("dirt");
    public static RoadProfileId Street => new("street");
    public static RoadProfileId Arterial => new("arterial");
    public static RoadProfileId Highway => new("highway");
}

public readonly record struct RoadPoint(double X, double Y)
{
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y);
    public double DistanceTo(RoadPoint other) => Math.Sqrt((X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y));
}

public sealed record RoadNode(NodeId Id, RoadPoint Position);
public sealed record RoadEdge
{
    public RoadEdge(EdgeId id, NodeId start, NodeId end, RoadProfileId profile, IEnumerable<RoadPoint> points)
    {
        Id = id;
        Start = start;
        End = end;
        Profile = profile;
        Points = Array.AsReadOnly(points.ToArray());
    }
    public EdgeId Id { get; }
    public NodeId Start { get; }
    public NodeId End { get; }
    public RoadProfileId Profile { get; }
    public IReadOnlyList<RoadPoint> Points { get; }
    public double Length => Points.Zip(Points.Skip(1), (a, b) => a.DistanceTo(b)).Sum();

    public RoadPoint PointAt(double parameter)
    {
        double distance = parameter * Length;
        for (int i = 1; i < Points.Count; i++)
        {
            RoadPoint a = Points[i - 1], b = Points[i];
            double length = a.DistanceTo(b);
            if (distance <= length || i == Points.Count - 1)
            {
                double t = Math.Clamp(distance / length, 0, 1);
                return new RoadPoint(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
            }
            distance -= length;
        }
        return Points[^1];
    }
}
public readonly record struct RoadLocation(RoadStateToken Source, EdgeId Edge, double Parameter);
public sealed record RoadBuildRequest(RoadStateToken Source, RoadPoint Start, RoadPoint End, RoadProfileId Profile);
public enum RoadBuildStatus { Ready, NoChange, Rejected }
public sealed record RoadBuildResult(RoadBuildStatus Status, RoadPlan? Plan, string Reason);
