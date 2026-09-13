namespace SimpleCities.RoadCore;

/// <summary>随快照建立的只读查找和弧长数据；查询不扫描路网或重算整边长度。</summary>
internal sealed class RoadSnapshotQueryData
{
    internal readonly record struct Boundary(double Parameter, RoadPoint Point);
    private readonly Dictionary<EdgeId, RoadEdge> _edges;
    private readonly Dictionary<NodeId, RoadNode> _nodes;
    private readonly Dictionary<NodeId, RoadEdge[]> _incident;
    private readonly Dictionary<NodeId, int> _degrees = [];
    private readonly Dictionary<EdgeId, Boundary[]> _vertices = [];
    private sealed record ArcData(double Length, double[] Prefix, double[] SegmentLengths);
    private readonly Dictionary<EdgeId, ArcData> _arcs = [];

    internal RoadSnapshotQueryData(RoadSnapshot snapshot)
    {
        _edges = snapshot.Edges.ToDictionary(edge => edge.Id);
        _nodes = snapshot.Nodes.ToDictionary(node => node.Id);
        var incident = new Dictionary<NodeId, List<RoadEdge>>();
        foreach (RoadEdge edge in snapshot.Edges)
        {
            AddIncident(edge.Start, edge);
            if (edge.End != edge.Start) AddIncident(edge.End, edge);
            _degrees[edge.Start] = Degree(edge.Start) + 1;
            _degrees[edge.End] = Degree(edge.End) + 1;
            var vertices = new Boundary[edge.Points.Count];
            var prefix = new double[edge.Points.Count];
            var segmentLengths = new double[edge.Points.Count - 1];
            double length = edge.Length, distance = 0;
            vertices[0] = new(0, edge.Points[0]);
            for (int i = 1; i < vertices.Length; i++)
            {
                segmentLengths[i - 1] = edge.Points[i - 1].DistanceTo(edge.Points[i]);
                distance += segmentLengths[i - 1];
                prefix[i] = distance;
                vertices[i] = new(i == vertices.Length - 1 ? 1 : distance / length, edge.Points[i]);
            }
            _vertices.Add(edge.Id, vertices);
            _arcs.Add(edge.Id, new(length, prefix, segmentLengths));
        }
        _incident = incident.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());

        void AddIncident(NodeId node, RoadEdge edge)
        {
            if (!incident.TryGetValue(node, out List<RoadEdge>? edges)) incident.Add(node, edges = []);
            edges.Add(edge);
        }
    }

    internal RoadEdge? FindEdge(EdgeId id) => _edges.GetValueOrDefault(id);
    internal RoadNode? FindNode(NodeId id) => _nodes.GetValueOrDefault(id);
    internal IReadOnlyList<RoadEdge> Incident(NodeId id) => _incident.TryGetValue(id, out RoadEdge[]? edges) ? edges : [];
    internal int Degree(NodeId id) => _degrees.GetValueOrDefault(id);
    internal Boundary[] Vertices(EdgeId id) => _vertices[id];
    internal static int Segment(Boundary[] vertices, double parameter)
    {
        int low = 0, high = vertices.Length - 1;
        while (low + 1 < high)
        {
            int middle = (low + high) / 2;
            if (vertices[middle].Parameter <= parameter) low = middle;
            else high = middle;
        }
        return Math.Min(low, vertices.Length - 2);
    }

    internal RoadPoint? Resolve(RoadLocation location)
    {
        if (!_vertices.TryGetValue(location.Edge, out Boundary[]? vertices)) return null;
        ArcData arc = _arcs[location.Edge];
        double distance = location.Parameter * arc.Length;
        int low = 0, high = vertices.Length - 1;
        while (low + 1 < high)
        {
            int middle = (low + high) / 2;
            if (arc.Prefix[middle] < distance) low = middle;
            else high = middle;
        }
        int segment = low;
        Boundary a = vertices[segment], b = vertices[segment + 1];
        // Resolve in metres, as RoadEdge.PointAt does. Dividing both prefix values into
        // normalized parameters first adds rounding even at exact positions such as (300, 50).
        double fraction = Math.Clamp((distance - arc.Prefix[segment]) / arc.SegmentLengths[segment], 0, 1);
        return new RoadPoint(a.Point.X + (b.Point.X - a.Point.X) * fraction,
            a.Point.Y + (b.Point.Y - a.Point.Y) * fraction);
    }
}
