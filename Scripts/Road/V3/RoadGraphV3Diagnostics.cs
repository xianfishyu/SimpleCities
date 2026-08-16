namespace SimpleCities.Road.V3;

/// <summary>
/// V3 道路图诊断快照：随事务维护的不可变拓扑/几何计数，供 DebugPanel 等 UI O(1) 读取。
/// </summary>
public sealed record RoadGraphV3Diagnostics(
    int NodeCount,
    int EdgeCount,
    int GeometrySegmentCount,
    int SelfLoopCount,
    int ParallelEdgeCount,
    long ChangeSequence,
    int QueryFragmentCount = 0)
{
    public bool IsValid =>
        NodeCount >= 0 &&
        EdgeCount >= 0 &&
        GeometrySegmentCount >= 0 &&
        SelfLoopCount >= 0 &&
        SelfLoopCount <= EdgeCount &&
        ParallelEdgeCount >= 0 &&
        ParallelEdgeCount <= EdgeCount &&
        ChangeSequence >= 0;
}
