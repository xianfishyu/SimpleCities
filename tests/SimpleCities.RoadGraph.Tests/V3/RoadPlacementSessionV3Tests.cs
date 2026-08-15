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
}
