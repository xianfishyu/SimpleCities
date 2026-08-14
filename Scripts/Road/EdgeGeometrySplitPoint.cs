using Godot;

internal readonly record struct EdgeGeometrySplitPoint(
    int GeometrySegmentIndex,
    float SegmentParameter,
    Vector2? CanonicalPosition = null);
