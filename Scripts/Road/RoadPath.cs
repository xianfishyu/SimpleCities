using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

public sealed class RoadPath
{
    private readonly ReadOnlyCollection<RoadGeometrySegment?> _segments;

    public IReadOnlyList<RoadGeometrySegment?> Segments => _segments;

    public RoadPath(IReadOnlyList<RoadGeometrySegment?> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        _segments = Array.AsReadOnly(segments.ToArray());
    }
}
