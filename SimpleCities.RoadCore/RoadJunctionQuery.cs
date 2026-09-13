namespace SimpleCities.RoadCore;

public readonly record struct RoadVector(double X, double Y);
public enum RoadEndRole { Start, End }
public readonly record struct RoadIncidenceKey(EdgeId Edge, RoadEndRole Role);
public sealed record RoadIncidence(RoadIncidenceKey Key, RoadProfileId Profile, RoadVector Outward, double BearingRadians);
public sealed record RoadTurnMovement(RoadIncidenceKey From, RoadIncidenceKey To, double SignedAngleRadians);

/// <summary>带来源版本的端接和几何转向；不表达交通许可、信号或容量。</summary>
public sealed class RoadJunctionReadModel
{
    internal RoadJunctionReadModel(RoadStateToken source, RoadNode node, IEnumerable<RoadIncidence> incidences, IEnumerable<RoadTurnMovement> turns)
    {
        Source = source;
        Node = node;
        Incidences = Array.AsReadOnly(incidences.ToArray());
        Turns = Array.AsReadOnly(turns.ToArray());
    }
    public RoadStateToken Source { get; }
    public RoadNode Node { get; }
    public IReadOnlyList<RoadIncidence> Incidences { get; }
    public IReadOnlyList<RoadTurnMovement> Turns { get; }
}

public static class RoadJunctionQuery
{
    public static RoadJunctionReadModel? Read(RoadSnapshot snapshot, NodeId nodeId)
    {
        RoadNode? node = snapshot.QueryData.FindNode(nodeId);
        if (node is null) return null;
        var incidences = new List<RoadIncidence>();
        foreach (RoadEdge edge in snapshot.QueryData.Incident(nodeId))
        {
            if (edge.Start == nodeId) Add(edge, RoadEndRole.Start, edge.Points[1]);
            if (edge.End == nodeId) Add(edge, RoadEndRole.End, edge.Points[^2]);
        }
        RoadIncidence[] ordered = incidences.OrderBy(item => item.BearingRadians)
            .ThenBy(item => item.Key.Edge.Value).ThenBy(item => item.Key.Role).ToArray();
        var turns = new List<RoadTurnMovement>();
        foreach (RoadIncidence from in ordered)
            foreach (RoadIncidence to in ordered)
            {
                // Incoming travel points toward the node; outgoing points away from it.
                double x = -from.Outward.X, y = -from.Outward.Y;
                double angle = Math.Atan2(x * to.Outward.Y - y * to.Outward.X, x * to.Outward.X + y * to.Outward.Y);
                if (angle == -Math.PI) angle = Math.PI;
                turns.Add(new RoadTurnMovement(from.Key, to.Key, angle));
            }
        return new RoadJunctionReadModel(snapshot.Token, node, ordered, turns);

        void Add(RoadEdge edge, RoadEndRole role, RoadPoint next)
        {
            double length = node.Position.DistanceTo(next);
            var outward = new RoadVector((next.X - node.Position.X) / length, (next.Y - node.Position.Y) / length);
            double bearing = Math.Atan2(outward.Y, outward.X);
            if (bearing < 0) bearing += Math.Tau;
            incidences.Add(new RoadIncidence(new RoadIncidenceKey(edge.Id, role), edge.Profile, outward, bearing));
        }
    }
}
