using System;

namespace SimpleCities.Road.V3;

/// <summary>
/// V3 mutation 与 format v1 load 共用容量门禁。
/// 超限必须在发布/提交前结构化失败，不能退化为无索引全图扫描或 ID 溢出。
/// </summary>
public readonly record struct RoadGraphCapacity
{
    public int MaxNodes { get; init; }
    public int MaxEdges { get; init; }
    public int MaxGeometrySegmentsPerEdge { get; init; }
    public int MaxTotalGeometrySegments { get; init; }
    public int MaxQueryFragments { get; init; }
    public int MaxBuckets { get; init; }
    public int MaxBucketReferences { get; init; }
    public int MaxMutationCandidates { get; init; }
    public int MaxID { get; init; }

    public static RoadGraphCapacity Default { get; } = new()
    {
        MaxNodes = 1_000_000,
        MaxEdges = 2_000_000,
        MaxGeometrySegmentsPerEdge = 100_000,
        MaxTotalGeometrySegments = 5_000_000,
        MaxQueryFragments = 10_000_000,
        MaxBuckets = 1_000_000,
        MaxBucketReferences = 20_000_000,
        MaxMutationCandidates = 1_000_000,
        MaxID = 2_000_000_000,
    };

    public void Validate()
    {
        if (MaxNodes <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxNodes), "Capacity must be positive.");
        if (MaxEdges <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxEdges), "Capacity must be positive.");
        if (MaxGeometrySegmentsPerEdge <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxGeometrySegmentsPerEdge), "Capacity must be positive.");
        if (MaxTotalGeometrySegments <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxTotalGeometrySegments), "Capacity must be positive.");
        if (MaxQueryFragments <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxQueryFragments), "Capacity must be positive.");
        if (MaxBuckets <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxBuckets), "Capacity must be positive.");
        if (MaxBucketReferences <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxBucketReferences), "Capacity must be positive.");
        if (MaxMutationCandidates <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxMutationCandidates), "Capacity must be positive.");
        if (MaxID <= 0 || MaxID >= int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(MaxID), "MaxID must be positive and below int.MaxValue.");
        if (MaxID < MaxNodes || MaxID < MaxEdges || MaxID < MaxTotalGeometrySegments)
            throw new ArgumentOutOfRangeException(nameof(MaxID), "MaxID must cover every entity count capacity.");
    }
}
