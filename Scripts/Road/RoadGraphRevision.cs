using System;
using System.Collections.Generic;
using System.Collections.Immutable;

public readonly record struct GraphLineageID(long Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct GraphStateToken(
    GraphLineageID LineageID,
    long DomainRevisionID,
    long ChangeSequence);

public sealed class RoadGraphRevision : ISaveSnapshot, IPreparedSaveState
{
    private readonly ImmutableDictionary<int, GraphNode> _nodes;
    private readonly ImmutableDictionary<int, GraphEdge> _edges;

    public GraphLineageID LineageID { get; }
    public long DomainRevisionID { get; }
    public long ChangeSequence { get; }
    public int NextIDWatermark { get; }
    public GraphStateToken StateToken => new(
        LineageID,
        DomainRevisionID,
        ChangeSequence);
    public IReadOnlyDictionary<int, GraphNode> Nodes => _nodes;
    public IReadOnlyDictionary<int, GraphEdge> Edges => _edges;
    internal RoadGraphResourceCounts ResourceCounts { get; }
    internal double TotalGeometryLength { get; }

    internal ImmutableDictionary<int, GraphNode> NodeMap => _nodes;
    internal ImmutableDictionary<int, GraphEdge> EdgeMap => _edges;
    internal ImmutableDictionary<int, NodeSpatialRef> NodeReferenceMap { get; }
    internal ImmutableDictionary<int, ImmutableArray<ISpatialRef>> EdgeReferenceMap { get; }
    internal UniformGridSnapshot SpatialIndex { get; }

    internal RoadGraphRevision(
        GraphLineageID lineageID,
        long domainRevisionID,
        long changeSequence,
        int nextIDWatermark,
        double totalGeometryLength,
        ImmutableDictionary<int, GraphNode> nodes,
        ImmutableDictionary<int, GraphEdge> edges,
        ImmutableDictionary<int, NodeSpatialRef> nodeReferences,
        ImmutableDictionary<int, ImmutableArray<ISpatialRef>> edgeReferences,
        UniformGridSnapshot spatialIndex,
        RoadGraphResourceCounts resourceCounts)
    {
        if (lineageID.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(lineageID));
        if (domainRevisionID < 0)
            throw new ArgumentOutOfRangeException(nameof(domainRevisionID));
        if (changeSequence < 0)
            throw new ArgumentOutOfRangeException(nameof(changeSequence));
        if (nextIDWatermark < 0)
            throw new ArgumentOutOfRangeException(nameof(nextIDWatermark));
        if (!double.IsFinite(totalGeometryLength) || totalGeometryLength < 0d)
            throw new ArgumentOutOfRangeException(nameof(totalGeometryLength));

        LineageID = lineageID;
        DomainRevisionID = domainRevisionID;
        ChangeSequence = changeSequence;
        NextIDWatermark = nextIDWatermark;
        TotalGeometryLength = totalGeometryLength;
        _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        _edges = edges ?? throw new ArgumentNullException(nameof(edges));
        NodeReferenceMap = nodeReferences ?? throw new ArgumentNullException(nameof(nodeReferences));
        EdgeReferenceMap = edgeReferences ?? throw new ArgumentNullException(nameof(edgeReferences));
        SpatialIndex = spatialIndex ?? throw new ArgumentNullException(nameof(spatialIndex));
        ResourceCounts = resourceCounts;
    }

    internal RoadGraphRevision WithRuntimeMetadata(
        GraphLineageID lineageID,
        long domainRevisionID,
        long changeSequence,
        int nextIDWatermark) =>
        new(
            lineageID,
            domainRevisionID,
            changeSequence,
            nextIDWatermark,
            TotalGeometryLength,
            _nodes,
            _edges,
            NodeReferenceMap,
            EdgeReferenceMap,
            SpatialIndex,
            ResourceCounts);
}

public sealed record RoadGraphEntityDelta<TEntity>(
    int ID,
    TEntity? Before,
    TEntity? After)
    where TEntity : class;

public sealed class RoadGraphDelta
{
    private readonly ImmutableArray<RoadGraphEntityDelta<GraphNode>> _nodes;
    private readonly ImmutableArray<RoadGraphEntityDelta<GraphEdge>> _edges;

    public GraphLineageID LineageID { get; }
    public long BeforeRevisionID { get; }
    public long AfterRevisionID { get; }
    public bool IsFullReset { get; }
    public long EstimatedByteSize { get; }
    public IReadOnlyList<RoadGraphEntityDelta<GraphNode>> Nodes => _nodes;
    public IReadOnlyList<RoadGraphEntityDelta<GraphEdge>> Edges => _edges;

    internal RoadGraphDelta(
        RoadGraphRevision beforeRevision,
        RoadGraphRevision afterRevision,
        IEnumerable<RoadGraphEntityDelta<GraphNode>> nodes,
        IEnumerable<RoadGraphEntityDelta<GraphEdge>> edges,
        bool isFullReset = false)
    {
        ArgumentNullException.ThrowIfNull(beforeRevision);
        ArgumentNullException.ThrowIfNull(afterRevision);
        if (!isFullReset && beforeRevision.LineageID != afterRevision.LineageID)
            throw new ArgumentException("A delta cannot cross graph lineages.");

        LineageID = afterRevision.LineageID;
        BeforeRevisionID = beforeRevision.DomainRevisionID;
        AfterRevisionID = afterRevision.DomainRevisionID;
        IsFullReset = isFullReset;
        _nodes = [.. nodes];
        _edges = [.. edges];
        EstimatedByteSize = EstimateRetainedBytes(_nodes, _edges);
    }

    private static long EstimateRetainedBytes(
        IEnumerable<RoadGraphEntityDelta<GraphNode>> nodes,
        IEnumerable<RoadGraphEntityDelta<GraphEdge>> edges)
    {
        const long DeltaOverhead = 128;
        const long ChangeOverhead = 32;
        const long NodeOverhead = 48;
        const long IncidenceBytes = 16;
        const long EdgeOverhead = 80;
        const long GeometryBytes = 192;

        long total = DeltaOverhead;
        var retained = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (RoadGraphEntityDelta<GraphNode> change in nodes)
        {
            total = SaturatingAdd(total, ChangeOverhead);
            total = EstimateNode(change.Before, retained, total, NodeOverhead, IncidenceBytes);
            total = EstimateNode(change.After, retained, total, NodeOverhead, IncidenceBytes);
        }
        foreach (RoadGraphEntityDelta<GraphEdge> change in edges)
        {
            total = SaturatingAdd(total, ChangeOverhead);
            total = EstimateEdge(change.Before, retained, total, EdgeOverhead, GeometryBytes);
            total = EstimateEdge(change.After, retained, total, EdgeOverhead, GeometryBytes);
        }
        return total;
    }

    private static long EstimateNode(
        GraphNode? node,
        HashSet<object> retained,
        long total,
        long nodeOverhead,
        long incidenceBytes)
    {
        if (node is null || !retained.Add(node))
            return total;
        total = SaturatingAdd(total, nodeOverhead);
        return SaturatingAdd(total, SaturatingMultiply(node.IncidenceCount, incidenceBytes));
    }

    private static long EstimateEdge(
        GraphEdge? edge,
        HashSet<object> retained,
        long total,
        long edgeOverhead,
        long geometryBytes)
    {
        if (edge is null || !retained.Add(edge))
            return total;
        total = SaturatingAdd(total, edgeOverhead);
        foreach (RoadGeometrySegment geometry in edge.GeometrySegments)
        {
            if (retained.Add(geometry))
                total = SaturatingAdd(total, geometryBytes);
        }
        return total;
    }

    private static long SaturatingMultiply(long left, long right) =>
        left > 0 && right > long.MaxValue / left ? long.MaxValue : left * right;

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;
}

public sealed record RoadGraphChangedEvent(
    RoadGraphDelta Delta,
    RoadGraphChangeSummary Changes,
    GraphStateToken StateToken);

public enum RoadGraphDeltaDirection
{
    Reverse,
    Forward,
}

public enum RoadGraphDeltaApplyError
{
    None,
    StaleGraphState,
}

public sealed record RoadGraphDeltaApplyResult(
    RoadGraphDeltaApplyError Error,
    GraphStateToken StateToken,
    RoadGraphChangeSummary Changes)
{
    public bool Success => Error == RoadGraphDeltaApplyError.None;
}
