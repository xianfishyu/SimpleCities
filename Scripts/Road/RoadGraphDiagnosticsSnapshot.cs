using System;

/// <summary>
/// RoadGraph 在提交边界发布的不可变结构诊断值。
/// </summary>
public sealed record RoadGraphDiagnosticsSnapshot
{
    public GraphStateToken StateToken { get; }
    public int NodeCount { get; }
    public int CanonicalEdgeCount { get; }
    public long GeometrySegmentCount { get; }
    public long QueryFragmentCount { get; }
    public int SelfLoopCount { get; }
    public int EdgeCount => CanonicalEdgeCount;
    public GraphLineageID LineageID => StateToken.LineageID;
    public long DomainRevisionID => StateToken.DomainRevisionID;
    public long ChangeSequence => StateToken.ChangeSequence;

    public RoadGraphDiagnosticsSnapshot(
        GraphStateToken stateToken,
        int nodeCount,
        int canonicalEdgeCount,
        long geometrySegmentCount,
        long queryFragmentCount,
        int selfLoopCount)
    {
        if (nodeCount < 0)
            throw new ArgumentOutOfRangeException(nameof(nodeCount));
        if (canonicalEdgeCount < 0)
            throw new ArgumentOutOfRangeException(nameof(canonicalEdgeCount));
        if (geometrySegmentCount < 0)
            throw new ArgumentOutOfRangeException(nameof(geometrySegmentCount));
        if (queryFragmentCount < 0)
            throw new ArgumentOutOfRangeException(nameof(queryFragmentCount));
        if (selfLoopCount < 0 || selfLoopCount > canonicalEdgeCount)
            throw new ArgumentOutOfRangeException(nameof(selfLoopCount));

        StateToken = stateToken;
        NodeCount = nodeCount;
        CanonicalEdgeCount = canonicalEdgeCount;
        GeometrySegmentCount = geometrySegmentCount;
        QueryFragmentCount = queryFragmentCount;
        SelfLoopCount = selfLoopCount;
    }
}
