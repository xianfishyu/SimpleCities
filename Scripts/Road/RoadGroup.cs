using System.Collections.Generic;

public class RoadGroup
{
    public int ID { get; }

    private readonly HashSet<int> _edgeIDs = new();
    public IReadOnlyCollection<int> EdgeIDs => _edgeIDs;
    public int EdgeCount => _edgeIDs.Count;
    public bool IsEmpty => _edgeIDs.Count == 0;

    public RoadGroup(int id)
    {
        ID = id;
    }

    internal void AddEdge(int edgeID) => _edgeIDs.Add(edgeID);

    internal void RemoveEdge(int edgeID) => _edgeIDs.Remove(edgeID);
}
