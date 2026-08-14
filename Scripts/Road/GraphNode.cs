using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

public enum EdgeEndpoint
{
    A,
    B,
}

public readonly record struct EdgeIncidence(
    int EdgeID,
    EdgeEndpoint Endpoint,
    int NeighborNodeID);

public class GraphNode
{
    public int ID { get; }
    public Vector2 Position { get; }

    private readonly ImmutableArray<EdgeIncidence> _incidences;
    public IReadOnlyList<EdgeIncidence> Incidences => _incidences;
    public int IncidenceCount => _incidences.Length;
    public int Degree => IncidenceCount;
    public int IncidentEdgeCount => _incidences.Select(incidence => incidence.EdgeID).Distinct().Count();

    public GraphNode(int id, Vector2 position) : this(id, position, []) { }

    private GraphNode(
        int id,
        Vector2 position,
        IEnumerable<EdgeIncidence> incidences)
    {
        if (id < 0)
            throw new ArgumentOutOfRangeException(nameof(id));

        ID = id;
        Position = position;
        _incidences = [.. incidences.OrderBy(incidence => incidence.EdgeID)
            .ThenBy(incidence => incidence.Endpoint)];
    }

    internal GraphNode WithAddedIncidence(
        int edgeID,
        EdgeEndpoint endpoint,
        int neighborNodeID)
    {
        if (edgeID < 0)
            throw new ArgumentOutOfRangeException(nameof(edgeID));
        if (!Enum.IsDefined(endpoint))
            throw new ArgumentOutOfRangeException(nameof(endpoint));
        if (neighborNodeID < 0)
            throw new ArgumentOutOfRangeException(nameof(neighborNodeID));
        if (_incidences.Any(incidence =>
                incidence.EdgeID == edgeID && incidence.Endpoint == endpoint))
        {
            throw new InvalidOperationException(
                $"Node {ID} already contains the {endpoint} incidence for edge {edgeID}.");
        }

        return new GraphNode(
            ID,
            Position,
            _incidences.Add(new EdgeIncidence(edgeID, endpoint, neighborNodeID)));
    }

    internal GraphNode WithRemovedIncidence(
        int edgeID,
        EdgeEndpoint endpoint,
        out bool removed)
    {
        int index = -1;
        for (int candidateIndex = 0; candidateIndex < _incidences.Length; candidateIndex++)
        {
            EdgeIncidence incidence = _incidences[candidateIndex];
            if (incidence.EdgeID == edgeID && incidence.Endpoint == endpoint)
            {
                index = candidateIndex;
                break;
            }
        }
        removed = index >= 0;
        return removed
            ? new GraphNode(ID, Position, _incidences.RemoveAt(index))
            : this;
    }

    internal GraphNode WithoutIncidences() =>
        _incidences.IsEmpty ? this : new GraphNode(ID, Position);

    public IEnumerable<int> GetNeighborIDs()
    {
        return _incidences
            .Select(incidence => incidence.NeighborNodeID)
            .Distinct()
            .Order();
    }
}
