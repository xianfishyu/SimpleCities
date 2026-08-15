using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Road.V3;

/// <summary>
/// V3 不可变 root 骨架：所有变更返回新 revision，旧 revision 永不被修改。
/// 当前使用全量字典复制，后续按指南改为持久化页/copy-on-write 结构共享。
/// </summary>
public sealed class RoadGraphV3Revision
{
    private readonly RoadGraphCapacity _capacity;
    private readonly IReadOnlyDictionary<int, RoadGraphV3Node> _nodes;
    private readonly IReadOnlyDictionary<int, RoadGraphV3Edge> _edges;

    public IReadOnlyDictionary<int, RoadGraphV3Node> Nodes => _nodes;
    public IReadOnlyDictionary<int, RoadGraphV3Edge> Edges => _edges;
    public RoadGraphCapacity Capacity => _capacity;
    public int NextNodeID { get; }
    public int NextEdgeID { get; }

    internal RoadGraphV3Revision(
        RoadGraphCapacity capacity,
        IReadOnlyDictionary<int, RoadGraphV3Node> nodes,
        IReadOnlyDictionary<int, RoadGraphV3Edge> edges,
        int nextNodeID,
        int nextEdgeID)
    {
        _capacity = capacity;
        _nodes = nodes;
        _edges = edges;
        NextNodeID = nextNodeID;
        NextEdgeID = nextEdgeID;
    }

    public static RoadGraphV3Revision Empty(RoadGraphCapacity capacity)
    {
        capacity.Validate();
        return new RoadGraphV3Revision(capacity, new Dictionary<int, RoadGraphV3Node>(), new Dictionary<int, RoadGraphV3Edge>(), 0, 0);
    }

    public bool TryAddNode(Vector2 position, out RoadGraphV3Revision revision, out int nodeID)
    {
        if (!RoadNumericPolicy.IsWithinCoordinateRange(position))
            throw new ArgumentOutOfRangeException(nameof(position), "Node position must be finite and within the V3 numeric range.");
        if (NextNodeID >= _capacity.MaxID)
        {
            revision = this;
            nodeID = 0;
            return false;
        }

        nodeID = NextNodeID;
        var node = new RoadGraphV3Node(nodeID, RoadNumericPolicy.NormalizeVector(position));
        var nodes = new Dictionary<int, RoadGraphV3Node>(_nodes) { [nodeID] = node };
        revision = new RoadGraphV3Revision(_capacity, nodes, _edges, NextNodeID + 1, NextEdgeID);
        return true;
    }

    public bool TryAddEdge(
        int nodeAID,
        int nodeBID,
        IReadOnlyList<RoadGeometrySegment> geometry,
        RoadType roadType,
        out RoadGraphV3Revision revision,
        out int edgeID)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        if (!_nodes.ContainsKey(nodeAID) || !_nodes.ContainsKey(nodeBID))
            throw new ArgumentOutOfRangeException(nameof(nodeAID), "Edge endpoint does not exist.");
        if (geometry.Count == 0 || geometry.Any(segment => segment is null))
            throw new ArgumentException("Edge geometry must be non-empty and contain no null.", nameof(geometry));
        if (NextEdgeID >= _capacity.MaxID)
        {
            revision = this;
            edgeID = 0;
            return false;
        }

        edgeID = NextEdgeID;
        var edge = new RoadGraphV3Edge(edgeID, nodeAID, nodeBID, geometry, roadType);
        var edges = new Dictionary<int, RoadGraphV3Edge>(_edges) { [edgeID] = edge };
        revision = new RoadGraphV3Revision(_capacity, _nodes, edges, NextNodeID, NextEdgeID + 1);
        return true;
    }

    public bool TryRemoveEdge(int edgeID, out RoadGraphV3Revision revision)
    {
        if (!_edges.ContainsKey(edgeID))
        {
            revision = this;
            return false;
        }

        var edges = new Dictionary<int, RoadGraphV3Edge>(_edges);
        edges.Remove(edgeID);
        revision = new RoadGraphV3Revision(_capacity, _nodes, edges, NextNodeID, NextEdgeID);
        return true;
    }

    public bool TryChangeRoadType(int edgeID, RoadType roadType, out RoadGraphV3Revision revision)
    {
        if (!_edges.TryGetValue(edgeID, out RoadGraphV3Edge? edge))
        {
            revision = this;
            return false;
        }

        if (edge.RoadType == roadType)
        {
            revision = this;
            return false;
        }

        var edges = new Dictionary<int, RoadGraphV3Edge>(_edges)
        {
            [edgeID] = new RoadGraphV3Edge(edge.ID, edge.NodeAID, edge.NodeBID, edge.Geometry, roadType),
        };
        revision = new RoadGraphV3Revision(_capacity, _nodes, edges, NextNodeID, NextEdgeID);
        return true;
    }

    public IReadOnlyList<RoadGraphV3Node> GetAllNodes() => _nodes.Values.OrderBy(node => node.ID).ToList();

    public IReadOnlyList<RoadGraphV3Edge> GetAllEdges() => _edges.Values.OrderBy(edge => edge.ID).ToList();
}
