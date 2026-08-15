using Godot;
using System;
using System.Collections.Generic;

namespace SimpleCities.Road.V3;

/// <summary>
/// self-loop 的 rooted chain 规范化：seam 固定为数组首尾公共点，数组内部可合并，
/// 但禁止跨 seam 合并或循环移位；最后用 typed direction key 选择规范存储方向。
/// </summary>
public static class RoadSelfLoopChain
{
    public static IReadOnlyList<RoadGeometrySegment> Canonicalize(
        IReadOnlyList<RoadGeometrySegment> chain)
    {
        ArgumentNullException.ThrowIfNull(chain);
        if (chain.Count == 0)
            throw new ArgumentException("A self-loop chain requires at least one geometry segment.", nameof(chain));

        Vector2 seamStart = RoadNumericPolicy.NormalizeVector(chain[0].Start);
        Vector2 seamEnd = RoadNumericPolicy.NormalizeVector(chain[^1].End);
        if (seamStart != seamEnd)
            throw new ArgumentException("Self-loop chain must be closed at the seam.", nameof(chain));

        IReadOnlyList<RoadGeometrySegment> canonical = RoadGeometryCanonicalizer.Canonicalize(chain);
        return RoadDirectionKey.SelectCanonicalDirection(canonical);
    }
}
