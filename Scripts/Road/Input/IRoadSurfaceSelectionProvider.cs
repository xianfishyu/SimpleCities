using Godot;

internal interface IRoadSurfaceSelectionProvider
{
    bool TryCaptureCurrentToken(out RoadRenderToken renderToken);

    bool IsCurrent(RoadRenderToken expectedToken);

    bool TryFindClosest(
        RoadRenderToken expectedToken,
        Vector2 position,
        float maxSurfaceDistance,
        out RoadSurfaceHit? hit);

    bool TryFindEdgeIDsIntersecting(
        RoadRenderToken expectedToken,
        Rect2 bounds,
        out int[] edgeIDs);
}
