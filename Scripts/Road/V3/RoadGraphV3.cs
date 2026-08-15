using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Road.V3;

public sealed record RoadGraphV3Node(int ID, Vector2 Position);

public sealed record RoadGraphV3Edge(
    int ID,
    int NodeAID,
    int NodeBID,
    IReadOnlyList<RoadGeometrySegment> Geometry,
    RoadType RoadType)
{
    public bool IsSelfLoop => NodeAID == NodeBID;
}

/// <summary>
/// V3 最小 incidence 图运行时骨架：支持 self-loop 的 A/B incidence、平行 Edge、
/// degree/distinct-edge/neighbor 查询和删除时一次性移除全部 incidence。
/// 仍为可变骨架；后续按指南升级为不可变 root 与事务摘要。
/// </summary>
public sealed class RoadGraphV3
{
    private readonly Dictionary<int, RoadGraphV3Node> _nodes = new();
    private readonly Dictionary<int, RoadGraphV3Edge> _edges = new();
    private readonly Dictionary<int, List<EdgeIncidence>> _incidences = new();
    private readonly RoadIDAllocator _nodeIDs;
    private readonly RoadIDAllocator _edgeIDs;

    public RoadGraphV3() : this(RoadGraphCapacity.Default)
    {
    }

    public RoadGraphV3(RoadGraphCapacity capacity)
    {
        capacity.Validate();
        _nodeIDs = new RoadIDAllocator(0, capacity.MaxID);
        _edgeIDs = new RoadIDAllocator(0, capacity.MaxID);
    }

    public int AddNode(Vector2 position)
    {
        if (!RoadNumericPolicy.IsWithinCoordinateRange(position))
            throw new ArgumentOutOfRangeException(nameof(position), "Node position must be finite and within the V3 numeric range.");
        if (!_nodeIDs.TryAllocate(out int id))
            throw new InvalidOperationException("Node ID capacity exceeded.");

        var node = new RoadGraphV3Node(id, RoadNumericPolicy.NormalizeVector(position));
        _nodes[id] = node;
        _incidences[id] = [];
        return id;
    }

    public int AddEdge(
        int nodeAID,
        int nodeBID,
        IReadOnlyList<RoadGeometrySegment> geometry,
        RoadType roadType)
    {
        if (!_nodes.ContainsKey(nodeAID))
            throw new ArgumentOutOfRangeException(nameof(nodeAID), "Node A does not exist.");
        if (!_nodes.ContainsKey(nodeBID))
            throw new ArgumentOutOfRangeException(nameof(nodeBID), "Node B does not exist.");
        ArgumentNullException.ThrowIfNull(geometry);
        if (geometry.Count == 0)
            throw new ArgumentException("Edge geometry must contain at least one segment.", nameof(geometry));
        if (geometry.Any(segment => segment is null))
            throw new ArgumentException("Edge geometry cannot contain null.", nameof(geometry));
        if (!_edgeIDs.TryAllocate(out int edgeID))
            throw new InvalidOperationException("Edge ID capacity exceeded.");

        var edge = new RoadGraphV3Edge(edgeID, nodeAID, nodeBID, geometry, roadType);
        _edges[edgeID] = edge;
        AddIncidence(nodeAID, edgeID, EdgeEndpoint.A, nodeBID);
        if (edge.IsSelfLoop)
        {
            AddIncidence(nodeAID, edgeID, EdgeEndpoint.B, nodeAID);
        }
        else
        {
            AddIncidence(nodeBID, edgeID, EdgeEndpoint.B, nodeAID);
        }

        return edgeID;
    }

    public bool RemoveEdge(int edgeID)
    {
        if (!_edges.Remove(edgeID, out RoadGraphV3Edge? edge))
            return false;

        RemoveIncidence(edge.NodeAID, edgeID);
        if (edge.IsSelfLoop)
        {
            RemoveIncidence(edge.NodeAID, edgeID);
        }
        else
        {
            RemoveIncidence(edge.NodeBID, edgeID);
        }

        return true;
    }

    public int GetDegree(int nodeID)
    {
        RequireNode(nodeID);
        return _incidences[nodeID].Count;
    }

    public int GetIncidentEdgeCount(int nodeID)
    {
        RequireNode(nodeID);
        return _incidences[nodeID].Select(incidence => incidence.EdgeID).Distinct().Count();
    }

    public IReadOnlyList<EdgeIncidence> GetIncidences(int nodeID)
    {
        RequireNode(nodeID);
        return _incidences[nodeID].ToArray();
    }

    public IReadOnlyList<int> GetNeighborIDs(int nodeID)
    {
        RequireNode(nodeID);
        return _incidences[nodeID]
            .Select(incidence => incidence.NeighborNodeID)
            .Distinct()
            .Order()
            .ToArray();
    }

    public RoadGraphV3Node? GetNode(int nodeID) => _nodes.GetValueOrDefault(nodeID);

    public RoadGraphV3Edge? GetEdge(int edgeID) => _edges.GetValueOrDefault(edgeID);

    public IReadOnlyList<RoadGraphV3Node> GetAllNodes() => _nodes.Values.OrderBy(node => node.ID).ToList();

    public IReadOnlyList<RoadGraphV3Edge> GetAllEdges() => _edges.Values.OrderBy(edge => edge.ID).ToList();

    private void AddIncidence(int nodeID, int edgeID, EdgeEndpoint endpoint, int neighborNodeID)
    {
        _incidences[nodeID].Add(new EdgeIncidence(edgeID, endpoint, neighborNodeID));
    }

    private void RemoveIncidence(int nodeID, int edgeID)
    {
        _incidences[nodeID].RemoveAll(incidence => incidence.EdgeID == edgeID);
    }

    private void RequireNode(int nodeID)
    {
        if (!_nodes.ContainsKey(nodeID))
            throw new ArgumentOutOfRangeException(nameof(nodeID), "Node does not exist.");
    }
}
