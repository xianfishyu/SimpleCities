using Godot;
using System.Collections.Generic;
using System.Linq;

public readonly struct EdgeRef
{
    public int EdgeID { get; }
    public int NeighborNodeID { get; }

    public EdgeRef(int edgeID, int neighborNodeID)
    {
        EdgeID = edgeID;
        NeighborNodeID = neighborNodeID;
    }
}

public class GraphNode
{
    public int ID { get; }
    public Vector2 Position { get; }

    private readonly List<EdgeRef> _edges = new();
    public IReadOnlyList<EdgeRef> Edges => _edges;
    public int EdgeCount => _edges.Count;

    public GraphNode(int id, Vector2 position)
    {
        ID = id;
        Position = position;
    }

    internal void AddEdge(int edgeID, int neighborNodeID)
    {
        _edges.Add(new EdgeRef(edgeID, neighborNodeID));
    }

    internal bool RemoveEdge(int edgeID)
    {
        int index = _edges.FindIndex(e => e.EdgeID == edgeID);
        if (index < 0) return false;
        _edges.RemoveAt(index);
        return true;
    }

    public IEnumerable<int> GetNeighborIDs()
    {
        return _edges.Select(e => e.NeighborNodeID).Distinct();
    }
}
