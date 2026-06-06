using System.Collections.Generic;

public class RoadGroup
{
    public int ID { get; }
    public RoadType Type { get; internal set; }

    private readonly HashSet<int> _edgeIDs = new();
    public IReadOnlyCollection<int> EdgeIDs => _edgeIDs;
    public int EdgeCount => _edgeIDs.Count;
    public bool IsEmpty => _edgeIDs.Count == 0;

    public RoadGroup(int id, RoadType type = RoadType.Street)
    {
        ID = id;
        Type = type;
    }

    internal void AddEdge(int edgeID) => _edgeIDs.Add(edgeID);

    internal void RemoveEdge(int edgeID) => _edgeIDs.Remove(edgeID);
}
