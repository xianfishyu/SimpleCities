using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadPlacementSessionV3Tests
{
    [Fact]
    public void Update_ProducesPreviewPath()
    {
        var session = new RoadPlacementSessionV3(RoadType.Street, Vector2.Zero);

        RoadPathDraft draft = session.Update(new Vector2(1f, 0f));

        Assert.True(draft.CanCommit);
        Assert.Equal(2, draft.PreviewPoints.Count);
    }

    [Fact]
    public void TryAddPoint_AddsCorner()
    {
        var session = new RoadPlacementSessionV3(RoadType.Street, Vector2.Zero);
        session.Update(new Vector2(1f, 0f));

        Assert.True(session.TryAddPoint(new Vector2(1f, 0f)));
        Assert.Equal(1, session.FixedCornerCount);
        Assert.Equal(new Vector2(1f, 0f), session.CurrentAnchor);
    }

    [Fact]
    public void TryAddPoint_ZeroLength_Fails()
    {
        var session = new RoadPlacementSessionV3(RoadType.Street, Vector2.Zero);

        Assert.False(session.TryAddPoint(Vector2.Zero));
        Assert.Equal(0, session.FixedCornerCount);
    }

    [Fact]
    public void TryRemoveLastPoint_RemovesCorner()
    {
        var session = new RoadPlacementSessionV3(RoadType.Street, Vector2.Zero);
        session.TryAddPoint(new Vector2(1f, 0f));
        session.TryAddPoint(new Vector2(2f, 0f));
        Assert.Equal(2, session.FixedCornerCount);

        Assert.True(session.TryRemoveLastPoint());
        Assert.Equal(1, session.FixedCornerCount);
        Assert.Equal(new Vector2(1f, 0f), session.CurrentAnchor);
    }

    [Fact]
    public void HasSelfIntersection_StraightPath_ReturnsFalse()
    {
        var session = new RoadPlacementSessionV3(RoadType.Street, Vector2.Zero);
        session.TryAddPoint(new Vector2(1f, 0f));
        session.TryAddPoint(new Vector2(2f, 0f));

        Assert.False(session.HasSelfIntersection);
    }

    [Fact]
    public void HasSelfIntersection_CrossingPath_ReturnsTrue()
    {
        var session = new RoadPlacementSessionV3(RoadType.Street, Vector2.Zero);
        session.TryAddPoint(new Vector2(2f, 2f));
        session.TryAddPoint(new Vector2(0f, 2f));
        session.TryAddPoint(new Vector2(2f, 0f));
        // Last segment from (0,2) to (2,0) crosses first segment (0,0)-(2,2).

        Assert.True(session.HasSelfIntersection);
    }

    [Fact]
    public void TryClose_ClosesPath()
    {
        var session = new RoadPlacementSessionV3(RoadType.Street, Vector2.Zero);
        session.TryAddPoint(new Vector2(1f, 0f));
        session.TryAddPoint(new Vector2(1f, 1f));

        Assert.True(session.TryClose());
        Assert.True(session.IsClosed);
        Assert.True(session.CurrentDraft.CanCommit);
        Assert.Equal(Vector2.Zero, session.CurrentDraft.PreviewTo);
    }

    [Fact]
    public void TryClose_NoFixedPoints_Fails()
    {
        var session = new RoadPlacementSessionV3(RoadType.Street, Vector2.Zero);

        Assert.False(session.TryClose());
        Assert.False(session.IsClosed);
    }

    [Fact]
    public void TryCommit_ClosedPath_ReturnsTypedRequest()
    {
        var session = new RoadPlacementSessionV3(RoadType.Street, Vector2.Zero);
        session.TryAddPoint(new Vector2(1f, 0f));
        session.TryAddPoint(new Vector2(1f, 1f));
        session.TryClose();

        Assert.True(session.TryCommit(out RoadBuildRequest? request));
        Assert.Equal(RoadType.Street, request.RoadType);
        Assert.NotNull(request.Path);
        Assert.Equal(3, request.Path.Segments.Count);
    }

    [Fact]
    public void TryCommit_ReturnsTypedRequest()
    {
        var session = new RoadPlacementSessionV3(RoadType.Highway, Vector2.Zero);
        session.TryAddPoint(new Vector2(1f, 0f));

        Assert.True(session.TryCommit(out RoadBuildRequest? request));
        Assert.Equal(RoadType.Highway, request.RoadType);
        Assert.NotNull(request.Path);
    }

    [Fact]
    public void TryCommit_EmptyPath_Fails()
    {
        var session = new RoadPlacementSessionV3(RoadType.Street, Vector2.Zero);

        Assert.False(session.TryCommit(out _));
    }

    [Fact]
    public void Constructor_InvalidRoadType_Throws()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => new RoadPlacementSessionV3((RoadType)99, Vector2.Zero));
    }

    [Fact]
    public void TryClose_WithinCloseRadius_ClosesPath()
    {
        var session = new RoadPlacementSessionV3(RoadType.Street, Vector2.Zero);
        session.TryAddPoint(new Vector2(1f, 0f));
        session.TryAddPoint(new Vector2(1f, 1f));

        Assert.True(session.TryClose(new Vector2(0.1f, 0.1f), 0.2f));
        Assert.True(session.IsClosed);
        Assert.Equal(Vector2.Zero, session.CurrentDraft.PreviewTo);
    }

    [Fact]
    public void TryClose_OutsideCloseRadius_Fails()
    {
        var session = new RoadPlacementSessionV3(RoadType.Street, Vector2.Zero);
        session.TryAddPoint(new Vector2(1f, 0f));

        Assert.False(session.TryClose(new Vector2(2f, 0f), 0.2f));
        Assert.False(session.IsClosed);
    }

    [Fact]
    public void TryClose_InvalidCloseRadius_Throws()
    {
        var session = new RoadPlacementSessionV3(RoadType.Street, Vector2.Zero);
        session.TryAddPoint(new Vector2(1f, 0f));

        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => session.TryClose(new Vector2(0.1f, 0f), 0f));
    }

    [Fact]
    public void IsWithinCloseRadius_Boundary_ReturnsTrue()
    {
        var session = new RoadPlacementSessionV3(RoadType.Street, Vector2.Zero);
        session.TryAddPoint(new Vector2(1f, 0f));

        Assert.True(session.IsWithinCloseRadius(new Vector2(0.2f, 0f), 0.2f));
        Assert.False(session.IsWithinCloseRadius(new Vector2(0.21f, 0f), 0.2f));
    }

    [Fact]
    public void IsWithinCloseRadius_NoFixedPoints_Fails()
    {
        var session = new RoadPlacementSessionV3(RoadType.Street, Vector2.Zero);

        Assert.False(session.IsWithinCloseRadius(Vector2.Zero, 1f));
    }

    [Fact]
    public void IsWithinCloseRadius_NonFinitePointer_Throws()
    {
        var session = new RoadPlacementSessionV3(RoadType.Street, Vector2.Zero);

        Assert.Throws<System.ArgumentException>(
            () => session.IsWithinCloseRadius(new Vector2(float.NaN, 0f), 1f));
    }

    [Fact]
    public void TryGetClosedDraft_WithinRadius_ReturnsClosedPath()
    {
        var session = new RoadPlacementSessionV3(RoadType.Street, Vector2.Zero);
        session.TryAddPoint(new Vector2(1f, 0f));
        session.TryAddPoint(new Vector2(1f, 1f));

        Assert.True(session.TryGetClosedDraft(new Vector2(0.1f, 0.1f), 0.2f, out RoadPathDraft draft));
        Assert.Equal(4, draft.PreviewPoints.Count);
        Assert.Equal(Vector2.Zero, draft.PreviewTo);
        Assert.NotNull(draft.Path);
        Assert.Equal(3, draft.Path.Segments.Count);
    }

    [Fact]
    public void TryGetClosedDraft_OutsideRadius_Fails()
    {
        var session = new RoadPlacementSessionV3(RoadType.Street, Vector2.Zero);
        session.TryAddPoint(new Vector2(1f, 0f));

        Assert.False(session.TryGetClosedDraft(new Vector2(2f, 0f), 0.2f, out _));
    }

    [Fact]
    public void TryGetClosedDraft_NoFixedPoints_Fails()
    {
        var session = new RoadPlacementSessionV3(RoadType.Street, Vector2.Zero);

        Assert.False(session.TryGetClosedDraft(new Vector2(0.1f, 0f), 1f, out _));
    }

    [Fact]
    public void TryGetClosedDraft_InvalidCloseRadius_Throws()
    {
        var session = new RoadPlacementSessionV3(RoadType.Street, Vector2.Zero);

        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => session.TryGetClosedDraft(new Vector2(0.1f, 0f), -1f, out _));
    }
}
