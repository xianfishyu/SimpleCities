using Godot;
using System.Collections.Immutable;

namespace SimpleCities.Tests;

public sealed class RoadGraphTransactionV3Tests
{
    [Fact]
    public void ApplyDelta_RestoresDomainRevisionAndOnlyAdvancesSequence()
    {
        var graph = new RoadGraph();
        GraphStateToken initial = graph.CurrentStateToken;
        var events = new List<RoadGraphChangedEvent>();
        graph.GraphChanged += events.Add;

        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(20f, 0f),
        ]).Success);
        RoadGraphChangedEvent edit = Assert.Single(events);
        GraphStateToken edited = graph.CurrentStateToken;
        GraphEdge editedEdge = Assert.Single(graph.GetAllEdges());
        int watermark = graph.NextIDWatermark;

        Assert.Equal(initial.LineageID, edited.LineageID);
        Assert.Equal(1, edited.DomainRevisionID);
        Assert.Equal(1, edited.ChangeSequence);

        RoadGraphDeltaApplyResult undo = graph.ApplyDelta(
            edit.Delta,
            RoadGraphDeltaDirection.Reverse,
            edited);

        Assert.True(undo.Success);
        Assert.Equal(initial.LineageID, undo.StateToken.LineageID);
        Assert.Equal(0, undo.StateToken.DomainRevisionID);
        Assert.Equal(2, undo.StateToken.ChangeSequence);
        Assert.Empty(graph.GetAllEdges());
        Assert.Equal(watermark, graph.NextIDWatermark);
        Assert.False(events[^1].Changes.IsFullReset);

        RoadGraphDeltaApplyResult redo = graph.ApplyDelta(
            edit.Delta,
            RoadGraphDeltaDirection.Forward,
            undo.StateToken);

        Assert.True(redo.Success);
        Assert.Equal(1, redo.StateToken.DomainRevisionID);
        Assert.Equal(3, redo.StateToken.ChangeSequence);
        Assert.Same(editedEdge, Assert.Single(graph.GetAllEdges()));
        Assert.Equal(watermark, graph.NextIDWatermark);
        Assert.Equal([1L, 2L, 3L], events.Select(change => change.StateToken.ChangeSequence));
        graph.AssertInvariants();
    }

    [Fact]
    public void ApplyDelta_RejectsWrongDirectionStaleSequenceAndDuplicateReplay()
    {
        var graph = new RoadGraph();
        RoadGraphChangedEvent? edit = null;
        graph.GraphChanged += change => edit = change;
        GraphStateToken initial = graph.CurrentStateToken;
        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(20f, 0f),
        ]).Success);
        RoadGraphDelta delta = Assert.IsType<RoadGraphDelta>(edit?.Delta);
        GraphStateToken edited = graph.CurrentStateToken;
        RoadGraphRevision editedRoot = graph.CaptureRevision();

        AssertStale(RoadGraphDeltaDirection.Forward, edited);
        AssertStale(RoadGraphDeltaDirection.Reverse, initial);

        RoadGraphDeltaApplyResult undo = graph.ApplyDelta(
            delta,
            RoadGraphDeltaDirection.Reverse,
            edited);
        Assert.True(undo.Success);
        RoadGraphRevision undoneRoot = graph.CaptureRevision();

        AssertStale(RoadGraphDeltaDirection.Reverse, undo.StateToken);
        AssertStale(RoadGraphDeltaDirection.Forward, edited);

        RoadGraphDeltaApplyResult redo = graph.ApplyDelta(
            delta,
            RoadGraphDeltaDirection.Forward,
            undo.StateToken);
        Assert.True(redo.Success);
        RoadGraphRevision redoneRoot = graph.CaptureRevision();
        AssertStale(RoadGraphDeltaDirection.Forward, redo.StateToken);
        Assert.NotSame(editedRoot, undoneRoot);
        Assert.NotSame(undoneRoot, redoneRoot);

        void AssertStale(
            RoadGraphDeltaDirection direction,
            GraphStateToken token)
        {
            RoadGraphRevision root = graph.CaptureRevision();
            GraphStateToken before = graph.CurrentStateToken;
            RoadGraphDeltaApplyResult result = graph.ApplyDelta(delta, direction, token);
            Assert.False(result.Success);
            Assert.Equal(RoadGraphDeltaApplyError.StaleGraphState, result.Error);
            Assert.Same(root, graph.CaptureRevision());
            Assert.Equal(before, graph.CurrentStateToken);
            Assert.False(result.Changes.HasChanges);
        }
    }

    [Fact]
    public void UndoThenBranchDoesNotReuseRevisionOrEntityIDs()
    {
        var graph = new RoadGraph();
        RoadGraphChangedEvent? firstEdit = null;
        graph.GraphChanged += change => firstEdit ??= change;
        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(20f, 0f),
        ]).Success);
        RoadGraphDelta firstDelta = Assert.IsType<RoadGraphDelta>(firstEdit?.Delta);
        int watermarkAfterFirst = graph.NextIDWatermark;

        RoadGraphDeltaApplyResult undo = graph.ApplyDelta(
            firstDelta,
            RoadGraphDeltaDirection.Reverse,
            graph.CurrentStateToken);
        Assert.True(undo.Success);
        Assert.True(graph.SubmitPolyline(RoadType.Highway, [
            new Vector2(0f, 40f),
            new Vector2(20f, 40f),
        ]).Success);

        GraphStateToken branched = graph.CurrentStateToken;
        Assert.Equal(2, branched.DomainRevisionID);
        Assert.Equal(3, branched.ChangeSequence);
        Assert.All(
            graph.GetAllNodes().Select(node => node.ID)
                .Concat(graph.GetAllEdges().Select(edge => edge.ID)),
            id => Assert.True(id >= watermarkAfterFirst));
        Assert.True(graph.NextIDWatermark > watermarkAfterFirst);

        RoadGraphDeltaApplyResult staleRedo = graph.ApplyDelta(
            firstDelta,
            RoadGraphDeltaDirection.Forward,
            branched);
        Assert.Equal(RoadGraphDeltaApplyError.StaleGraphState, staleRedo.Error);
    }

    [Fact]
    public void FullResetCreatesNewLineageAndInvalidatesOldDeltaAndToken()
    {
        var graph = new RoadGraph();
        RoadGraphChangedEvent? edit = null;
        graph.GraphChanged += change => edit ??= change;
        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(20f, 0f),
        ]).Success);
        RoadGraphDelta delta = Assert.IsType<RoadGraphDelta>(edit?.Delta);
        GraphStateToken oldToken = graph.CurrentStateToken;
        string emptyPayload = RoadGraphTestCodec.CaptureJson(new RoadGraph());

        RoadGraphTestCodec.LoadJson(graph, emptyPayload);

        GraphStateToken reset = graph.CurrentStateToken;
        Assert.NotEqual(oldToken.LineageID, reset.LineageID);
        Assert.Equal(0, reset.DomainRevisionID);
        Assert.Equal(oldToken.ChangeSequence + 1, reset.ChangeSequence);
        Assert.Equal(0, graph.NextIDWatermark);
        RoadGraphRevision root = graph.CaptureRevision();

        RoadGraphDeltaApplyResult stale = graph.ApplyDelta(
            delta,
            RoadGraphDeltaDirection.Reverse,
            oldToken);
        Assert.Equal(RoadGraphDeltaApplyError.StaleGraphState, stale.Error);
        Assert.Same(root, graph.CaptureRevision());

        Assert.True(graph.SubmitPolyline(RoadType.Dirt, [
            new Vector2(0f, 40f),
            new Vector2(20f, 40f),
        ]).Success);
        Assert.Equal([0, 1], graph.GetAllNodes().Select(node => node.ID).Order());
        Assert.Equal(2, Assert.Single(graph.GetAllEdges()).ID);
    }

    [Fact]
    public void PublicationIsolatesObserverFailuresAndRejectsMutationReentrancy()
    {
        var graph = new RoadGraph();
        int laterObserverCalls = 0;
        RoadPathSubmissionResult? reentrant = null;
        graph.GraphChanged += _ => throw new InvalidOperationException("first observer failed");
        graph.GraphChanged += _ =>
        {
            reentrant = graph.SubmitPolyline(RoadType.Street, [
                new Vector2(0f, 40f),
                new Vector2(20f, 40f),
            ]);
        };
        graph.GraphChanged += _ => throw new InvalidOperationException("middle observer failed");
        graph.GraphChanged += _ => laterObserverCalls++;

        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(20f, 0f),
        ]);

        Assert.True(result.Success);
        Assert.Equal(RoadPathSubmissionError.MutationReentrant, reentrant?.Error);
        Assert.Equal(1, laterObserverCalls);
        Assert.Single(graph.GetAllEdges());
        Assert.Equal(1, graph.CurrentStateToken.ChangeSequence);
        graph.AssertInvariants();
    }

    [Fact]
    public void LocalMutationSharesUntouchedEntitiesGeometryReferencesAndBucketPages()
    {
        var graph = new RoadGraph();
        int changedEdgeID = AddLine(graph, 0f);
        int untouchedEdgeID = AddLine(graph, 10_000f);
        RoadGraphRevision before = graph.CaptureRevision();
        GraphEdge untouchedEdge = before.EdgeMap[untouchedEdgeID];
        GraphNode untouchedNode = before.NodeMap[untouchedEdge.NodeA];
        EdgeGeometryRef untouchedReference = before.EdgeReferenceMap[untouchedEdgeID]
            .Cast<EdgeGeometryRef>()
            .First();
        KeyValuePair<(int bx, int by), ImmutableArray<ISpatialRef>> untouchedBucket =
            Assert.Single(
                before.SpatialIndex.Buckets,
                pair => pair.Value.Any(reference =>
                    ReferenceEquals(reference, untouchedReference)));

        RoadTypeChangeResult result = graph.ChangeRoadType(
            [changedEdgeID],
            RoadType.Highway);

        Assert.True(result.Success);
        RoadGraphRevision after = graph.CaptureRevision();
        Assert.Same(untouchedEdge, after.EdgeMap[untouchedEdgeID]);
        Assert.Same(untouchedNode, after.NodeMap[untouchedNode.ID]);
        Assert.Same(
            untouchedEdge.GeometrySegments[0],
            after.EdgeMap[untouchedEdgeID].GeometrySegments[0]);
        Assert.Equal(
            before.EdgeReferenceMap[untouchedEdgeID],
            after.EdgeReferenceMap[untouchedEdgeID]);
        Assert.Equal(
            untouchedBucket.Value,
            after.SpatialIndex.Buckets[untouchedBucket.Key]);
        Assert.NotSame(before.EdgeMap[changedEdgeID], after.EdgeMap[changedEdgeID]);
    }

    [Fact]
    public void CaptureRevisionReturnsThePublishedRootWithoutCopying()
    {
        var graph = new RoadGraph();
        _ = AddLine(graph, 0f);

        RoadGraphRevision root = graph.CaptureRevision();

        for (int index = 0; index < 100_000; index++)
            Assert.Same(root, graph.CaptureRevision());
    }

    [Fact]
    public void EveryOrdinaryMutationKindProducesAnApplicableDelta()
    {
        VerifyRoundTrip(graph =>
        {
            Assert.True(graph.SubmitPath(new RoadBuildRequest(new RoadPath([
                new CubicBezierRoadGeometrySegment(
                    new Vector2(0f, -20f),
                    new Vector2(0f, -10f),
                    new Vector2(0f, 10f),
                    new Vector2(0f, 20f)),
            ]), RoadType.Street)).Success);
            return graph.SubmitPolyline(RoadType.Highway, [
                new Vector2(-20f, 0f),
                new Vector2(20f, 0f),
            ]).Success;
        });

        VerifyRoundTrip(graph =>
        {
            int branchID = Assert.Single(graph.SubmitPolyline(RoadType.Street, [
                new Vector2(10f, 0f),
                new Vector2(10f, 10f),
            ]).Changes.CreatedEdgeIDs);
            Assert.True(graph.SubmitPolyline(RoadType.Street, [
                Vector2.Zero,
                new Vector2(10f, 0f),
                new Vector2(20f, 0f),
            ]).Success);
            return graph.RemoveEdge(branchID);
        });

        VerifyRoundTrip(graph =>
        {
            int edgeID = Assert.Single(graph.SubmitPath(new RoadBuildRequest(new RoadPath([
                new CubicBezierRoadGeometrySegment(
                    Vector2.Zero,
                    new Vector2(0f, 10f),
                    new Vector2(20f, 10f),
                    new Vector2(20f, 0f)),
            ]), RoadType.Street)).Changes.CreatedEdgeIDs);
            return graph.SplitEdgeAtGeometryParameters(
                edgeID,
                [new EdgeGeometrySplitPoint(0, 0.5f)]);
        });

        VerifyRoundTrip(graph =>
        {
            _ = AddLine(graph, 0f);
            int arterialID = Assert.Single(graph.SubmitPolyline(RoadType.Arterial, [
                new Vector2(20f, 0f),
                new Vector2(40f, 0f),
            ]).Changes.CreatedEdgeIDs);
            return graph.ChangeRoadType([arterialID], RoadType.Street).Success;
        });
    }

    private static void VerifyRoundTrip(Func<RoadGraph, bool> mutate)
    {
        var graph = new RoadGraph();
        RoadGraphChangedEvent? mutationEvent = null;
        graph.GraphChanged += change => mutationEvent = change;
        Assert.True(mutate(graph));
        RoadGraphChangedEvent changed = Assert.IsType<RoadGraphChangedEvent>(mutationEvent);
        RoadGraphRevision after = graph.CaptureRevision();
        int watermark = graph.NextIDWatermark;

        RoadGraphDeltaApplyResult reverse = graph.ApplyDelta(
            changed.Delta,
            RoadGraphDeltaDirection.Reverse,
            changed.StateToken);
        Assert.True(reverse.Success);
        graph.AssertInvariants();

        RoadGraphDeltaApplyResult forward = graph.ApplyDelta(
            changed.Delta,
            RoadGraphDeltaDirection.Forward,
            reverse.StateToken);
        Assert.True(forward.Success);
        RoadGraphRevision replayed = graph.CaptureRevision();
        Assert.Equal(after.DomainRevisionID, replayed.DomainRevisionID);
        Assert.Equal(after.ChangeSequence + 2, replayed.ChangeSequence);
        Assert.Equal(watermark, graph.NextIDWatermark);
        Assert.Equal(after.NodeMap.Keys.Order(), replayed.NodeMap.Keys.Order());
        Assert.Equal(after.EdgeMap.Keys.Order(), replayed.EdgeMap.Keys.Order());
        foreach ((int id, GraphNode node) in after.NodeMap)
            Assert.Same(node, replayed.NodeMap[id]);
        foreach ((int id, GraphEdge edge) in after.EdgeMap)
        {
            Assert.Same(edge, replayed.EdgeMap[id]);
            Vector2 midpoint = edge.GeometrySegments[0].GetPosition(0.5f);
            Assert.Contains(id, graph.FindEdgeIDsNear(midpoint, 0.01f));
        }
        graph.AssertInvariants();
    }

    private static int AddLine(RoadGraph graph, float y)
    {
        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street, [
            new Vector2(0f, y),
            new Vector2(20f, y),
        ]);
        Assert.True(result.Success, result.Error.ToString());
        return Assert.Single(result.Changes.CreatedEdgeIDs);
    }
}
