namespace SimpleCities.Tests;

using Godot;

public sealed class RoadRendererLifecycleContractTests
{
    private static readonly RoadTypeStyleSnapshot RoadTypeStyles =
        RoadTypeStyleSnapshot.Create([
            new RoadTypeStyleDefinition(RoadType.Dirt, "Dirt", Colors.White, 4f),
            new RoadTypeStyleDefinition(RoadType.Street, "Street", Colors.White, 6f),
            new RoadTypeStyleDefinition(RoadType.Arterial, "Arterial", Colors.White, 8f),
            new RoadTypeStyleDefinition(RoadType.Highway, "Highway", Colors.White, 10f),
        ]);

    private static readonly string ProjectRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void SetGraphSynchronizesExistingEdgesAndExitTreeUnsubscribes()
    {
        string source = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Road", "RoadRenderer.cs"));
        string setGraph = ExtractMethod(source, "public void SetGraph", "private void OnGraphChanged");

        Assert.Contains("RebuildStaticBatches()", setGraph, StringComparison.Ordinal);
        Assert.DoesNotContain("_edgePoints.Clear()", setGraph, StringComparison.Ordinal);
        Assert.DoesNotContain("_network.GetAllEdges()", setGraph, StringComparison.Ordinal);
        Assert.Contains("public override void _ExitTree", source, StringComparison.Ordinal);
        Assert.Contains("GraphChanged -= OnGraphChanged", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EdgeAdded", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EdgeRemoved", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GraphCleared", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NodeMarkersUseTopologyClassificationAndTypeSpecificRadius()
    {
        string source = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Road", "RoadRenderer.cs"));

        Assert.Contains("IsJunctionNode", source, StringComparison.Ordinal);
        Assert.Contains("GetNodeMarkerRadius", source, StringComparison.Ordinal);
        Assert.DoesNotContain("bool junction = node.EdgeCount >= 2", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Config.JunctionRadius * 1.3f", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PrimitiveJoinsAreNotNodesAndBranchJunctionUsesItsOwnRadius()
    {
        var straightGraph = new RoadGraph();
        Assert.True(straightGraph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(5f, 0f)),
            new LineRoadGeometrySegment(new Vector2(5f, 0f), new Vector2(10f, 0f)),
        ]), RoadType.Street)).Success);
        global::GraphNode endpoint = Assert.Single(
            straightGraph.GetAllNodes(),
            node => node.Position == Vector2.Zero);

        Assert.DoesNotContain(straightGraph.GetAllNodes(), node => node.Position == new Vector2(5f, 0f));
        Assert.Equal(
            3f,
            RoadRenderer.GetNodeMarkerRadius(
                straightGraph,
                endpoint,
                RoadTypeStyles,
                junctionRadius: 10f));

        var turnGraph = new RoadGraph();
        Assert.True(turnGraph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(5f, 0f)),
            new LineRoadGeometrySegment(new Vector2(5f, 0f), new Vector2(5f, 5f)),
        ]), RoadType.Street)).Success);
        Assert.DoesNotContain(turnGraph.GetAllNodes(), node => node.Position == new Vector2(5f, 0f));
        Assert.Single(turnGraph.GetAllEdges());
        Assert.Equal(2, Assert.Single(turnGraph.GetAllEdges()).GeometrySegments.Count);

        var branchGraph = new RoadGraph();
        Assert.True(branchGraph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(10f, 0f),
        ]).Success);
        Assert.True(branchGraph.SubmitPolyline(RoadType.Street, [
            new Vector2(5f, 0f),
            new Vector2(5f, 5f),
        ]).Success);
        global::GraphNode junction = Assert.Single(
            branchGraph.GetAllNodes(),
            node => node.Position == new Vector2(5f, 0f));

        Assert.True(RoadRenderer.IsJunctionNode(branchGraph, junction));
        Assert.Equal(
            10f,
            RoadRenderer.GetNodeMarkerRadius(
                branchGraph,
                junction,
                RoadTypeStyles,
                junctionRadius: 10f));
    }

    [Fact]
    public void SemanticBoundaryIsNotClassifiedAsJunctionMarker()
    {
        var graph = RoadGraph.FromPreparedTopology(new PreparedRoadGraphTopology(
            5,
            [
                new PreparedRoadNode(0, Vector2.Zero),
                new PreparedRoadNode(1, new Vector2(10f, 0f)),
                new PreparedRoadNode(2, new Vector2(0f, 10f)),
            ],
            [
                new PreparedRoadEdge(
                    RoadType.Street,
                    3,
                    0,
                    1,
                    [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f))]),
                new PreparedRoadEdge(
                    RoadType.Highway,
                    4,
                    0,
                    2,
                    [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(0f, 10f))]),
            ]));
        global::GraphNode boundary = Assert.IsType<global::GraphNode>(graph.GetNode(0));

        Assert.False(RoadRenderer.IsJunctionNode(graph, boundary));
        Assert.Equal(
            0f,
            RoadRenderer.GetNodeMarkerRadius(
                graph,
                boundary,
                RoadTypeStyles,
            junctionRadius: 10f));
    }

    [Fact]
    public void OrdinaryRebuildReusesLoadPreparerWithoutStaticJunctionMarkers()
    {
        string rendererSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Road", "RoadRenderer.cs"));
        string loadSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Road", "RoadRenderer.LoadCommit.cs"));
        string ordinaryBuild = ExtractMethod(
            rendererSource,
            "private void RebuildStaticBatches",
            "private bool PresentationResourcesAreReady");
        string loadBuild = ExtractMethod(
            loadSource,
            "internal RoadRendererPreparedLoad Prepare",
            "private sealed class RoadRendererLoadCommitPlan");
        string ordinaryNodeSurface = ExtractMethod(
            rendererSource,
            "private static RoadRendererNodeSurface? CreateNodeSurface",
            "private static RoadRendererNodeSurface CreateTerminalCapSurface");
        string loadNodeSurface = ExtractMethod(
            loadSource,
            "private static RoadRendererNodeSurface? CreateNodeSurface",
            "private static bool TryGetOutgoingDirection");

        Assert.Contains("new RoadRendererLoadPreparer(settings)", ordinaryBuild, StringComparison.Ordinal);
        Assert.Contains("AppendJunctionPatch(", loadBuild, StringComparison.Ordinal);
        Assert.DoesNotContain("JunctionRadius", ordinaryNodeSurface, StringComparison.Ordinal);
        Assert.DoesNotContain("JunctionRadius", loadNodeSurface, StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryFailureKeepsPreparedStateUnpublishedAndExposesRetry()
    {
        string source = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Road", "RoadRenderer.cs"));
        string rebuild = ExtractMethod(
            source,
            "private bool TryRebuildStaticBatches",
            "private void PublishPresentationStalled");

        int prepare = rebuild.IndexOf("preparer.Prepare(revision)", StringComparison.Ordinal);
        int currentCheck = rebuild.IndexOf(
            "_presentationTokens.DesiredToken != targetToken",
            StringComparison.Ordinal);
        int cacheSwap = rebuild.IndexOf("_edgePoints = prepared.EdgePoints", StringComparison.Ordinal);
        int tokenCommit = rebuild.IndexOf(
            "_presentationTokens.CommitDesired(targetToken)",
            StringComparison.Ordinal);

        Assert.True(prepare >= 0 && prepare < currentCheck);
        Assert.True(currentCheck < cacheSwap && cacheSwap < tokenCommit);
        Assert.Contains("ReportBuildFailure(", rebuild, StringComparison.Ordinal);
        Assert.Contains("public bool RetryRoadPresentation()", source, StringComparison.Ordinal);
        Assert.Contains("_presentationTokens.IsPresentationStalled", source, StringComparison.Ordinal);
        Assert.Contains("GetPresentedRoadSurface()?.FindClosest", source, StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryPresentationStalledObserversAreIsolatedInSubscriptionOrder()
    {
        string rendererSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Road", "RoadRenderer.cs"));
        string probeSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "tests", "godot", "RoadRendererUpdateTokenFailureProbe.cs"));
        string publication = ExtractMethod(
            rendererSource,
            "private void PublishPresentationStalled",
            "private bool PresentationResourcesAreReady");
        string arming = ExtractMethod(
            probeSource,
            "internal void ArmNextOrdinaryPresentationStalledObserverFailure()",
            "private void ThrowOrdinaryPresentationStalledObserverFailure(");
        string throwingObserver = ExtractMethod(
            probeSource,
            "private void ThrowOrdinaryPresentationStalledObserverFailure(",
            "private void RecordOrdinaryPresentationStalledObserverContinuation(");
        string continuingObserver = ExtractMethod(
            probeSource,
            "private void RecordOrdinaryPresentationStalledObserverContinuation(",
            "internal bool IsOrdinaryPresentationStalledObserverFailureArmed()");

        int invocationList = publication.IndexOf("GetInvocationList()", StringComparison.Ordinal);
        int handlerCall = publication.IndexOf("handler(failure);", StringComparison.Ordinal);
        int warning = publication.IndexOf(
            "GD.PushWarning(",
            StringComparison.Ordinal);
        int throwingSubscription = arming.IndexOf(
            "PresentationStalled += ThrowOrdinaryPresentationStalledObserverFailure;",
            StringComparison.Ordinal);
        int continuingSubscription = arming.IndexOf(
            "PresentationStalled += RecordOrdinaryPresentationStalledObserverContinuation;",
            StringComparison.Ordinal);

        Assert.True(invocationList >= 0 && invocationList < handlerCall);
        Assert.True(handlerCall < warning);
        Assert.Contains("catch (Exception exception)", publication, StringComparison.Ordinal);
        Assert.Contains(
            "$\"Road presentation stalled observer failed: {exception.Message}\"",
            publication,
            StringComparison.Ordinal);
        Assert.True(
            throwingSubscription >= 0 && throwingSubscription < continuingSubscription);
        Assert.Contains(
            "PresentationStalled -= ThrowOrdinaryPresentationStalledObserverFailure;",
            throwingObserver,
            StringComparison.Ordinal);
        Assert.Contains(
            "OrdinaryPresentationStalledObserverFailureMessage",
            throwingObserver,
            StringComparison.Ordinal);
        Assert.Contains(
            "PresentationStalled -= RecordOrdinaryPresentationStalledObserverContinuation;",
            continuingObserver,
            StringComparison.Ordinal);
        Assert.Contains(
            "_ordinaryPresentationStalledObserverContinuationCount++;",
            continuingObserver,
            StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryRebuildPublishesTokenBoundPhaseMetricsOnlyAfterCommit()
    {
        string source = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Road", "RoadRenderer.cs"));
        string graphChanged = ExtractMethod(
            source,
            "private void OnGraphChanged",
            "private void ScheduleStaticBatchRebuild");
        string rebuild = ExtractMethod(
            source,
            "private bool TryRebuildStaticBatches",
            "private void PublishPresentationStalled");

        int requestStarted = graphChanged.IndexOf(
            "long requestStarted = Stopwatch.GetTimestamp()",
            StringComparison.Ordinal);
        int tokenRequest = graphChanged.LastIndexOf(
            "RoadRenderToken mutationRequest = _presentationTokens.RequestGraphChange(",
            StringComparison.Ordinal);
        int requestMetrics = graphChanged.LastIndexOf(
            "_pendingPresentationPerformanceRequest = new(",
            StringComparison.Ordinal);
        int schedule = graphChanged.IndexOf(
            "ScheduleStaticBatchRebuild()",
            StringComparison.Ordinal);

        int snapshot = rebuild.IndexOf(
            "Config.CaptureRoadTypeStyleSnapshot()",
            StringComparison.Ordinal);
        int revision = rebuild.IndexOf("graph.CaptureRevision()", StringComparison.Ordinal);
        int prepare = rebuild.IndexOf("preparer.Prepare(revision)", StringComparison.Ordinal);
        int resourcePreflight = rebuild.IndexOf("CreateRoadMesh(", StringComparison.Ordinal);
        int currentCheck = rebuild.IndexOf(
            "_presentationTokens.DesiredToken != targetToken",
            StringComparison.Ordinal);
        int tokenCommit = rebuild.IndexOf(
            "_presentationTokens.CommitDesired(targetToken)",
            StringComparison.Ordinal);
        int metricsCommit = rebuild.IndexOf(
            "_lastPresentationPerformanceMetrics = new(",
            StringComparison.Ordinal);

        Assert.True(requestStarted >= 0 && requestStarted < tokenRequest);
        Assert.True(tokenRequest < requestMetrics && requestMetrics < schedule);
        Assert.True(snapshot >= 0 && snapshot < revision);
        Assert.True(revision < prepare && prepare < resourcePreflight);
        Assert.True(resourcePreflight < currentCheck && currentCheck < tokenCommit);
        Assert.True(tokenCommit < metricsCommit);
        Assert.Contains("request.RenderToken == targetToken", rebuild, StringComparison.Ordinal);
        Assert.Contains("Stopwatch.GetElapsedTime(request.StartTimestamp)", rebuild, StringComparison.Ordinal);
        Assert.Contains("GetLastPresentationPerformanceMetrics()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NonAggregateFullResetUsesTheSynchronousMeasuredRebuildPath()
    {
        string source = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Road", "RoadRenderer.cs"));
        string graphChanged = ExtractMethod(
            source,
            "private void OnGraphChanged",
            "private void ScheduleStaticBatchRebuild");

        int fullResetBranch = graphChanged.IndexOf(
            "if (change.Changes.IsFullReset)",
            StringComparison.Ordinal);
        int requestStarted = graphChanged.IndexOf(
            "long requestStarted = Stopwatch.GetTimestamp()",
            StringComparison.Ordinal);
        int tokenRequest = graphChanged.IndexOf(
            "RoadRenderToken resetRequest = _presentationTokens.RequestGraphChange(",
            StringComparison.Ordinal);
        int requestMetrics = graphChanged.IndexOf(
            "_pendingPresentationPerformanceRequest = new(",
            StringComparison.Ordinal);
        int synchronousRebuild = graphChanged.IndexOf(
            "RebuildStaticBatches();",
            StringComparison.Ordinal);

        Assert.True(requestStarted >= 0 && requestStarted < fullResetBranch);
        Assert.True(fullResetBranch < tokenRequest && tokenRequest < requestMetrics);
        Assert.True(requestMetrics < synchronousRebuild);
        Assert.Contains("IsFullReset: true", graphChanged, StringComparison.Ordinal);
        Assert.DoesNotContain("ScheduleStaticBatchRebuild();", graphChanged[..synchronousRebuild], StringComparison.Ordinal);
    }

    [Fact]
    public void DeferredOrdinaryRebuildIsGenerationGuardedAcrossSynchronousReset()
    {
        string source = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Road", "RoadRenderer.cs"));
        string loadSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Road", "RoadRenderer.LoadCommit.cs"));
        string scheduling = ExtractMethod(
            source,
            "private void ScheduleStaticBatchRebuild",
            "private void RebuildStaticBatches");

        Assert.Contains(
            "long continuationGeneration = _staticBatchRebuildContinuationGeneration;",
            scheduling,
            StringComparison.Ordinal);
        Assert.Contains(
            "FlushScheduledStaticBatchRebuild(continuationGeneration)",
            scheduling,
            StringComparison.Ordinal);
        Assert.Contains(
            "continuationGeneration != _staticBatchRebuildContinuationGeneration",
            scheduling,
            StringComparison.Ordinal);
        Assert.Contains(
            "InvalidateScheduledStaticBatchRebuildContinuation();",
            scheduling,
            StringComparison.Ordinal);
        Assert.Contains(
            "_owner.InvalidateScheduledStaticBatchRebuildContinuation();",
            loadSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "_owner._staticBatchRebuildScheduled = false;",
            loadSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PreparedPresentationResourcesAreReleasedBeforeOwnershipTransfer()
    {
        string rendererSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Road", "RoadRenderer.cs"));
        string loadSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Road", "RoadRenderer.LoadCommit.cs"));
        string saveManagerSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Core", "SaveManager.cs"));
        string ordinaryBuild = ExtractMethod(
            rendererSource,
            "private bool TryRebuildStaticBatches",
            "private void PublishPresentationStalled");
        string loadPreflight = ExtractMethod(
            loadSource,
            "internal INonThrowingLoadCommitPlan PreflightPreparedLoad",
            "private bool IsLoadAdmissionCurrent");
        string loadOrchestration = ExtractMethod(
            saveManagerSource,
            "private async Task<SaveOperationResult> RunLoadAsync",
            "private async Task<SaveOperationResult> RunDeleteAsync");

        Assert.Contains("bool presentationResourcesTransferred = false;", ordinaryBuild);
        Assert.Contains("finally", ordinaryBuild);
        Assert.Contains("if (!presentationResourcesTransferred)", ordinaryBuild);
        Assert.Contains(
            "DisposePreparedPresentationResources(roadMesh, nodeBatch);",
            ordinaryBuild);

        int ordinaryRoadMeshCreation = ordinaryBuild.IndexOf(
            "roadMesh = CreateRoadMesh(",
            StringComparison.Ordinal);
        int ordinaryNodeBatchCreation = ordinaryBuild.IndexOf(
            "nodeBatch = CreateNodeBatch(",
            StringComparison.Ordinal);
        int ordinaryFailureProbe = ordinaryBuild.IndexOf(
            "ProbeOrdinaryPresentationResourcePreflightFailure(targetToken);",
            StringComparison.Ordinal);
        int ordinarySurfaceCreation = ordinaryBuild.IndexOf(
            "var surfaceSnapshot = new RoadSurfaceSnapshot(",
            StringComparison.Ordinal);
        Assert.True(
            ordinaryRoadMeshCreation >= 0 &&
            ordinaryRoadMeshCreation < ordinaryNodeBatchCreation &&
            ordinaryNodeBatchCreation < ordinaryFailureProbe &&
            ordinaryFailureProbe < ordinarySurfaceCreation);

        Assert.Contains("ArrayMesh? roadMesh = null;", loadPreflight);
        Assert.Contains("MultiMesh? nodeBatch = null;", loadPreflight);
        Assert.Contains("catch", loadPreflight);
        Assert.Contains(
            "DisposePreparedPresentationResources(roadMesh, nodeBatch);",
            loadPreflight);

        int roadMeshCreation = loadPreflight.IndexOf("roadMesh = CreateRoadMesh(", StringComparison.Ordinal);
        int nodeBatchCreation = loadPreflight.IndexOf("nodeBatch = CreateNodeBatch(", StringComparison.Ordinal);
        int aggregateLoadFailureProbe = loadPreflight.IndexOf(
            "ProbeAggregateLoadResourcePreflightFailure();",
            StringComparison.Ordinal);
        int surfaceCreation = loadPreflight.IndexOf("var surfaceSnapshot = new RoadSurfaceSnapshot(", StringComparison.Ordinal);
        int planCreation = loadPreflight.IndexOf("return new RoadRendererLoadCommitPlan(", StringComparison.Ordinal);
        int cleanup = loadPreflight.LastIndexOf(
            "DisposePreparedPresentationResources(roadMesh, nodeBatch);",
            StringComparison.Ordinal);

        Assert.True(roadMeshCreation >= 0 && roadMeshCreation < nodeBatchCreation);
        Assert.True(
            nodeBatchCreation < aggregateLoadFailureProbe &&
            aggregateLoadFailureProbe < surfaceCreation &&
            surfaceCreation < planCreation);
        Assert.True(planCreation < cleanup);

        int rendererPlan = loadOrchestration.IndexOf(
            "preflightPlans.Add(context.Renderer.PreflightPreparedLoad(",
            StringComparison.Ordinal);
        int postRendererFailureProbe = loadOrchestration.IndexOf(
            "ProbeAggregateLoadPostRendererPreflightFailure();",
            StringComparison.Ordinal);
        int slotPlan = loadOrchestration.IndexOf(
            "preflightPlans.Add(new SlotTargetLoadCommitPlan(",
            StringComparison.Ordinal);
        int postSlotFailureProbe = loadOrchestration.IndexOf(
            "ProbeAggregateLoadPostSlotPreflightFailure();",
            StringComparison.Ordinal);
        int aggregateCreation = loadOrchestration.IndexOf(
            "using var aggregate = new PreparedAggregateLoad(preflightPlans);",
            StringComparison.Ordinal);
        int aggregateOwnership = loadOrchestration.IndexOf(
            "aggregateOwnsPlans = true;",
            StringComparison.Ordinal);
        int postOwnershipFailureProbe = loadOrchestration.IndexOf(
            "ProbeAggregateLoadPostOwnershipPreCommitFailure();",
            StringComparison.Ordinal);
        int aggregateCommit = loadOrchestration.IndexOf(
            "IReadOnlyList<string> warnings = aggregate.Commit(lease);",
            StringComparison.Ordinal);
        int fallbackPlanDisposal = loadOrchestration.LastIndexOf(
            "foreach (INonThrowingLoadCommitPlan plan in preflightPlans)",
            StringComparison.Ordinal);

        Assert.True(rendererPlan >= 0 && rendererPlan < postRendererFailureProbe);
        Assert.True(postRendererFailureProbe < slotPlan && slotPlan < postSlotFailureProbe);
        Assert.True(postSlotFailureProbe < aggregateCreation);
        Assert.True(aggregateCreation < aggregateOwnership);
        Assert.True(aggregateOwnership < postOwnershipFailureProbe);
        Assert.True(postOwnershipFailureProbe < aggregateCommit);
        Assert.True(aggregateCommit < fallbackPlanDisposal);
        Assert.Contains("plan.Dispose();", loadOrchestration[fallbackPlanDisposal..]);
    }

    [Fact]
    public void PresentationResourceFactoriesReleasePartiallyCreatedResources()
    {
        string rendererSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Road", "RoadRenderer.cs"));
        string loadSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Road", "RoadRenderer.LoadCommit.cs"));
        string roadMeshFactory = ExtractMethod(
            rendererSource,
            "private static ArrayMesh? CreateRoadMesh",
            "private static TResource InitializeOwnedResource");
        string resourceInitialization = ExtractMethod(
            rendererSource,
            "private static TResource InitializeOwnedResource",
            "private static void DisposePreparedPresentationResources");
        string resourceDisposal = ExtractMethod(
            rendererSource,
            "private static void DisposePreparedPresentationResources",
            "private static MultiMeshInstance2D CreateBatchLayer");
        string nodeBatchFactory = ExtractMethod(
            loadSource,
            "private static MultiMesh CreateNodeBatch",
            "internal sealed class RoadRendererLoadAdmission");

        Assert.Contains("return InitializeOwnedResource(", roadMeshFactory);
        Assert.Contains("new ArrayMesh()", roadMeshFactory);
        Assert.Contains("static (mesh, state)", roadMeshFactory);
        Assert.Contains(
            "state.Arrays[(int)Mesh.ArrayType.Index] = state.Indices.ToArray();",
            roadMeshFactory);
        Assert.Contains("return InitializeOwnedResource(", nodeBatchFactory);
        Assert.Contains("new MultiMesh()", nodeBatchFactory);
        Assert.Contains("static (batch, nodeMarkers)", nodeBatchFactory);
        Assert.Contains("using (var markerMesh", nodeBatchFactory);
        Assert.Contains("catch", resourceInitialization);
        Assert.Contains("resource.Dispose();", resourceInitialization);
        Assert.Contains("finally", resourceDisposal);
        Assert.Contains("nodeBatch?.Dispose();", resourceDisposal);
        Assert.Contains("roadMesh?.Dispose();", resourceDisposal);
    }

    [Fact]
    public void AggregateNodeBatchFactoryFailureRunsInsideOwnedLoadPreflight()
    {
        string loadSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Road", "RoadRenderer.LoadCommit.cs"));
        string probeSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "tests", "godot", "RoadLoadPreflightResourceFailureProbe.cs"));
        string loadPreflight = ExtractMethod(
            loadSource,
            "internal INonThrowingLoadCommitPlan PreflightPreparedLoad",
            "private bool IsLoadAdmissionCurrent");
        string factoryProbe = ExtractMethod(
            probeSource,
            "partial void ProbeAggregateLoadNodeBatchFactoryFailure(",
            "internal void ArmNextAggregateLoadNodeBatchFactoryFailure()");

        int roadMeshCreation = loadPreflight.IndexOf(
            "roadMesh = CreateRoadMesh(",
            StringComparison.Ordinal);
        int nodeMarkerCapture = loadPreflight.IndexOf(
            "IReadOnlyList<RoadRendererNodeMarker> nodeMarkers = prepared.NodeMarkers;",
            StringComparison.Ordinal);
        int nodeBatchFailureProbe = loadPreflight.IndexOf(
            "ProbeAggregateLoadNodeBatchFactoryFailure(ref nodeMarkers);",
            StringComparison.Ordinal);
        int nodeBatchCreation = loadPreflight.IndexOf(
            "nodeBatch = CreateNodeBatch(nodeMarkers);",
            StringComparison.Ordinal);
        int resourcePreflightFailureProbe = loadPreflight.IndexOf(
            "ProbeAggregateLoadResourcePreflightFailure();",
            StringComparison.Ordinal);

        Assert.True(roadMeshCreation >= 0 && roadMeshCreation < nodeMarkerCapture);
        Assert.True(nodeMarkerCapture < nodeBatchFailureProbe);
        Assert.True(nodeBatchFailureProbe < nodeBatchCreation);
        Assert.True(nodeBatchCreation < resourcePreflightFailureProbe);
        Assert.Contains(
            "ref IReadOnlyList<RoadRendererNodeMarker> nodeMarkers",
            loadSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "_aggregateLoadNodeBatchFactoryFailureArmed = false;",
            factoryProbe,
            StringComparison.Ordinal);
        Assert.Contains(
            "nodeMarkers = new AggregateLoadNodeBatchFactoryFailureMarkers(this);",
            factoryProbe,
            StringComparison.Ordinal);
        Assert.Contains(
            "owner._aggregateLoadNodeBatchFactoryMarkerReadCount++;",
            probeSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "AggregateLoadNodeBatchFactoryFailureMessage);",
            probeSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryRoadMeshFactoryFailureRunsInsideTheOwnedUpdateAttempt()
    {
        string rendererSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Road", "RoadRenderer.cs"));
        string probeSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "tests", "godot", "RoadRendererUpdateTokenFailureProbe.cs"));
        string ordinaryBuild = ExtractMethod(
            rendererSource,
            "private bool TryRebuildStaticBatches",
            "private void PublishPresentationStalled");
        string roadMeshFactory = ExtractMethod(
            rendererSource,
            "private static ArrayMesh? CreateRoadMesh",
            "private static TResource InitializeOwnedResource");
        string factoryProbe = ExtractMethod(
            probeSource,
            "partial void ProbeOrdinaryRoadMeshFactoryFailure(",
            "partial void ProbeOrdinaryNodeBatchFactoryFailure(");

        int roadIndexCapture = ordinaryBuild.IndexOf(
            "IReadOnlyCollection<int> roadIndices = prepared.RoadIndices;",
            StringComparison.Ordinal);
        int roadMeshFailureProbe = ordinaryBuild.IndexOf(
            "ProbeOrdinaryRoadMeshFactoryFailure(targetToken, ref roadIndices);",
            StringComparison.Ordinal);
        int roadMeshCreation = ordinaryBuild.IndexOf(
            "roadMesh = CreateRoadMesh(",
            StringComparison.Ordinal);
        int nodeMarkerCapture = ordinaryBuild.IndexOf(
            "IReadOnlyList<RoadRendererNodeMarker> nodeMarkers = prepared.NodeMarkers;",
            StringComparison.Ordinal);
        int resourceCreation = roadMeshFactory.IndexOf(
            "new ArrayMesh()",
            StringComparison.Ordinal);
        int indexEnumeration = roadMeshFactory.IndexOf(
            "state.Indices.ToArray()",
            StringComparison.Ordinal);
        int surfaceInitialization = roadMeshFactory.IndexOf(
            "mesh.AddSurfaceFromArrays(",
            StringComparison.Ordinal);

        Assert.True(roadIndexCapture >= 0 && roadIndexCapture < roadMeshFailureProbe);
        Assert.True(roadMeshFailureProbe < roadMeshCreation);
        Assert.True(roadMeshCreation < nodeMarkerCapture);
        Assert.True(resourceCreation >= 0 && resourceCreation < indexEnumeration);
        Assert.True(indexEnumeration < surfaceInitialization);
        Assert.Contains(
            "ref IReadOnlyCollection<int> roadIndices",
            rendererSource,
            StringComparison.Ordinal);
        Assert.Contains("targetToken == armedToken", factoryProbe, StringComparison.Ordinal);
        Assert.Contains(
            "_ordinaryRoadMeshFactoryFailureArmed = false;",
            factoryProbe,
            StringComparison.Ordinal);
        Assert.Contains(
            "roadIndices = new OrdinaryRoadMeshFactoryFailureIndices(this);",
            factoryProbe,
            StringComparison.Ordinal);
        Assert.Contains(
            "_ordinaryRoadMeshFactoryFailureIndexEnumerationCount++;",
            probeSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "OrdinaryRoadMeshFactoryFailureMessage);",
            probeSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryRoadSurfaceSnapshotFailureRunsAfterBothResourcesAreCreated()
    {
        string rendererSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Road", "RoadRenderer.cs"));
        string surfaceSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Road", "RoadSurfaceSnapshot.cs"));
        string probeSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "tests", "godot", "RoadRendererUpdateTokenFailureProbe.cs"));
        string ordinaryBuild = ExtractMethod(
            rendererSource,
            "private bool TryRebuildStaticBatches",
            "private void PublishPresentationStalled");
        string snapshotProbe = ExtractMethod(
            probeSource,
            "partial void ProbeOrdinaryRoadSurfaceSnapshotFailure(",
            "internal void ArmNextOrdinaryPresentationResourcePreflightFailure()");

        int roadMeshCreation = ordinaryBuild.IndexOf(
            "roadMesh = CreateRoadMesh(",
            StringComparison.Ordinal);
        int nodeBatchCreation = ordinaryBuild.IndexOf(
            "nodeBatch = CreateNodeBatch(nodeMarkers);",
            StringComparison.Ordinal);
        int resourcePreflightFailureProbe = ordinaryBuild.IndexOf(
            "ProbeOrdinaryPresentationResourcePreflightFailure(targetToken);",
            StringComparison.Ordinal);
        int roadSurfaceCapture = ordinaryBuild.IndexOf(
            "RoadSurfaceSnapshot.PreparedData roadSurface = prepared.RoadSurface;",
            StringComparison.Ordinal);
        int snapshotFailureProbe = ordinaryBuild.IndexOf(
            "ProbeOrdinaryRoadSurfaceSnapshotFailure(targetToken, ref roadSurface);",
            StringComparison.Ordinal);
        int snapshotCreation = ordinaryBuild.IndexOf(
            "var surfaceSnapshot = new RoadSurfaceSnapshot(",
            StringComparison.Ordinal);
        int resourceTransfer = ordinaryBuild.IndexOf(
            "_roadBatchLayer.Mesh = roadMesh;",
            StringComparison.Ordinal);

        Assert.True(roadMeshCreation >= 0 && roadMeshCreation < nodeBatchCreation);
        Assert.True(nodeBatchCreation < resourcePreflightFailureProbe);
        Assert.True(resourcePreflightFailureProbe < roadSurfaceCapture);
        Assert.True(roadSurfaceCapture < snapshotFailureProbe);
        Assert.True(snapshotFailureProbe < snapshotCreation);
        Assert.True(snapshotCreation < resourceTransfer);
        Assert.Contains(
            "ref RoadSurfaceSnapshot.PreparedData roadSurface",
            rendererSource,
            StringComparison.Ordinal);
        Assert.Contains("targetToken == armedToken", snapshotProbe, StringComparison.Ordinal);
        Assert.Contains(
            "_ordinaryRoadSurfaceSnapshotFailureArmed = false;",
            snapshotProbe,
            StringComparison.Ordinal);
        Assert.Contains(
            "_ordinaryRoadSurfaceSnapshotFailureCount++;",
            snapshotProbe,
            StringComparison.Ordinal);
        Assert.Contains("roadSurface = null!;", snapshotProbe, StringComparison.Ordinal);
        Assert.Contains(
            "ArgumentNullException.ThrowIfNull(prepared);",
            surfaceSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "new ArgumentNullException(\"prepared\").Message;",
            probeSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryPreCommitSupersessionDiscardsPreparedResourcesBeforeTransfer()
    {
        string rendererSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Road", "RoadRenderer.cs"));
        string probeSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "tests", "godot", "RoadRendererUpdateTokenFailureProbe.cs"));
        string ordinaryBuild = ExtractMethod(
            rendererSource,
            "private bool TryRebuildStaticBatches",
            "private void PublishPresentationStalled");
        string supersessionProbe = ExtractMethod(
            probeSource,
            "partial void ProbeOrdinaryPreCommitTokenSupersession(",
            "internal void ArmNextOrdinaryPresentationResourcePreflightFailure()");
        string completion = ExtractMethod(
            probeSource,
            "internal bool CompleteOrdinaryPreCommitTokenSupersession()",
            "private sealed class OrdinaryRoadMeshFactoryFailureIndices");

        int snapshotCreation = ordinaryBuild.IndexOf(
            "var surfaceSnapshot = new RoadSurfaceSnapshot(",
            StringComparison.Ordinal);
        int supersession = ordinaryBuild.IndexOf(
            "ProbeOrdinaryPreCommitTokenSupersession(targetToken);",
            StringComparison.Ordinal);
        int desiredValidation = ordinaryBuild.IndexOf(
            "_presentationTokens.DesiredToken != targetToken",
            StringComparison.Ordinal);
        int resourceTransfer = ordinaryBuild.IndexOf(
            "_roadBatchLayer.Mesh = roadMesh;",
            StringComparison.Ordinal);
        int fallbackDisposal = ordinaryBuild.IndexOf(
            "DisposePreparedPresentationResources(roadMesh, nodeBatch);",
            StringComparison.Ordinal);

        Assert.True(snapshotCreation >= 0 && snapshotCreation < supersession);
        Assert.True(supersession < desiredValidation);
        Assert.True(desiredValidation < resourceTransfer);
        Assert.True(resourceTransfer < fallbackDisposal);
        Assert.Contains("targetToken == armedToken", supersessionProbe, StringComparison.Ordinal);
        Assert.Contains(
            "_ordinaryPreCommitSupersededAttemptNumber = _presentationTokens.AttemptCount;",
            supersessionProbe,
            StringComparison.Ordinal);
        Assert.Contains(
            "_ordinaryPreCommitSupersededToken = targetToken;",
            supersessionProbe,
            StringComparison.Ordinal);
        Assert.Contains(
            "_presentationTokens.RequestRebuild(targetToken.ChangeSequence)",
            supersessionProbe,
            StringComparison.Ordinal);
        Assert.Contains("TryRebuildStaticBatches()", completion, StringComparison.Ordinal);
        Assert.Contains(
            "_presentationTokens.PresentedToken == replacementToken",
            completion,
            StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryPreCommitRoadStyleSupersessionUsesStyleRefreshRequest()
    {
        string probeSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "tests", "godot", "RoadRendererUpdateTokenFailureProbe.cs"));
        string supersessionProbe = ExtractMethod(
            probeSource,
            "partial void ProbeOrdinaryPreCommitTokenSupersession(",
            "internal void ArmNextOrdinaryPresentationResourcePreflightFailure()");
        string styleArm = ExtractMethod(
            probeSource,
            "internal void ArmNextOrdinaryPreCommitRoadStyleSupersession()",
            "private void ArmNextOrdinaryPreCommitTokenSupersession(");

        Assert.Contains(
            "OrdinaryPreCommitSupersessionKind.RoadStyleRevision",
            styleArm,
            StringComparison.Ordinal);
        Assert.Contains(
            "_ordinaryPreCommitSupersessionKind",
            supersessionProbe,
            StringComparison.Ordinal);
        Assert.Contains(
            "_presentationTokens.RequestStyleRefresh(targetToken.ChangeSequence)",
            supersessionProbe,
            StringComparison.Ordinal);
        Assert.Contains(
            "_presentationTokens.RequestRebuild(targetToken.ChangeSequence)",
            supersessionProbe,
            StringComparison.Ordinal);
        Assert.Contains(
            "OrdinaryPreCommitSupersessionKind.RenderRequest;",
            supersessionProbe,
            StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryPreCommitSceneGenerationSupersessionUsesSceneGenerationRequest()
    {
        string probeSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "tests", "godot", "RoadRendererUpdateTokenFailureProbe.cs"));
        string supersessionProbe = ExtractMethod(
            probeSource,
            "partial void ProbeOrdinaryPreCommitTokenSupersession(",
            "internal void ArmNextOrdinaryPresentationResourcePreflightFailure()");
        string sceneGenerationArm = ExtractMethod(
            probeSource,
            "internal void ArmNextOrdinaryPreCommitSceneGenerationSupersession()",
            "private void ArmNextOrdinaryPreCommitTokenSupersession(");
        string sceneGenerationRequest = ExtractMethod(
            probeSource,
            "private RoadRenderToken RequestOrdinaryPreCommitSceneGenerationSupersession(",
            "private RoadRenderToken RequestOrdinaryPreCommitGraphFacadeGenerationSupersession(");

        Assert.Contains(
            "OrdinaryPreCommitSupersessionKind.SceneGeneration",
            sceneGenerationArm,
            StringComparison.Ordinal);
        Assert.Contains(
            "RequestOrdinaryPreCommitSceneGenerationSupersession(targetToken)",
            supersessionProbe,
            StringComparison.Ordinal);
        Assert.Contains(
            "checked(targetToken.SceneGeneration + 1)",
            sceneGenerationRequest,
            StringComparison.Ordinal);
        Assert.Contains(
            "_presentationTokens.SetSceneGeneration(",
            sceneGenerationRequest,
            StringComparison.Ordinal);
        Assert.Contains(
            "out RoadRenderToken replacementToken",
            sceneGenerationRequest,
            StringComparison.Ordinal);
        Assert.Contains("return replacementToken;", sceneGenerationRequest, StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryPreCommitGraphFacadeIDSupersessionUsesRealRendererRebind()
    {
        string probeSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "tests", "godot", "RoadRendererUpdateTokenFailureProbe.cs"));
        string supersessionProbe = ExtractMethod(
            probeSource,
            "partial void ProbeOrdinaryPreCommitTokenSupersession(",
            "internal void ArmNextOrdinaryPresentationResourcePreflightFailure()");
        string graphFacadeIDArm = ExtractMethod(
            probeSource,
            "internal void ArmNextOrdinaryPreCommitGraphFacadeIDSupersession()",
            "internal void ArmNextOrdinaryPreCommitGraphFacadeGenerationSupersession()");
        string graphFacadeIDRequest = ExtractMethod(
            probeSource,
            "private RoadRenderToken RequestOrdinaryPreCommitGraphFacadeIDSupersession(",
            "private RoadRenderToken RequestOrdinaryPreCommitGraphFacadeGenerationSupersession(");

        Assert.Contains(
            "OrdinaryPreCommitSupersessionKind.GraphFacadeID",
            graphFacadeIDArm,
            StringComparison.Ordinal);
        Assert.Contains(
            "RequestOrdinaryPreCommitGraphFacadeIDSupersession(targetToken)",
            supersessionProbe,
            StringComparison.Ordinal);
        Assert.Contains(
            "graph.CaptureSnapshot() is not IPreparedSaveState preparedState",
            graphFacadeIDRequest,
            StringComparison.Ordinal);
        Assert.Contains("var replacementGraph = new RoadGraph();", graphFacadeIDRequest, StringComparison.Ordinal);
        Assert.Contains(
            "replacementGraph.CurrentStateToken.ChangeSequence < targetToken.ChangeSequence",
            graphFacadeIDRequest,
            StringComparison.Ordinal);
        Assert.Contains(
            "replacementGraph.CommitPreparedLoad(preparedState);",
            graphFacadeIDRequest,
            StringComparison.Ordinal);
        Assert.Contains("SetGraph(replacementGraph);", graphFacadeIDRequest, StringComparison.Ordinal);
        Assert.Contains(
            "_presentationTokens.PresentedToken != replacementToken",
            graphFacadeIDRequest,
            StringComparison.Ordinal);
        Assert.Contains("!IsPresentationReady()", graphFacadeIDRequest, StringComparison.Ordinal);
        Assert.DoesNotContain("_presentationTokens.BindGraph(", graphFacadeIDRequest, StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryPreCommitGraphFacadeGenerationSupersessionUsesCurrentFullResetRequest()
    {
        string probeSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "tests", "godot", "RoadRendererUpdateTokenFailureProbe.cs"));
        string supersessionProbe = ExtractMethod(
            probeSource,
            "partial void ProbeOrdinaryPreCommitTokenSupersession(",
            "internal void ArmNextOrdinaryPresentationResourcePreflightFailure()");
        string graphFacadeGenerationArm = ExtractMethod(
            probeSource,
            "internal void ArmNextOrdinaryPreCommitGraphFacadeGenerationSupersession()",
            "private void ArmNextOrdinaryPreCommitTokenSupersession(");
        string graphFacadeGenerationRequest = ExtractMethod(
            probeSource,
            "private RoadRenderToken RequestOrdinaryPreCommitGraphFacadeGenerationSupersession(",
            "private RoadRenderToken RequestOrdinaryPreCommitChangeSequenceSupersession(");

        Assert.Contains(
            "OrdinaryPreCommitSupersessionKind.GraphFacadeGeneration",
            graphFacadeGenerationArm,
            StringComparison.Ordinal);
        Assert.Contains(
            "RequestOrdinaryPreCommitGraphFacadeGenerationSupersession(targetToken)",
            supersessionProbe,
            StringComparison.Ordinal);
        Assert.Contains(
            "graph.FacadeID != targetToken.GraphFacadeID",
            graphFacadeGenerationRequest,
            StringComparison.Ordinal);
        Assert.Contains(
            "graph.CurrentStateToken.ChangeSequence != targetToken.ChangeSequence",
            graphFacadeGenerationRequest,
            StringComparison.Ordinal);
        Assert.Contains(
            "_presentationTokens.RequestGraphChange(",
            graphFacadeGenerationRequest,
            StringComparison.Ordinal);
        Assert.Contains("targetToken.ChangeSequence", graphFacadeGenerationRequest, StringComparison.Ordinal);
        Assert.Contains("isFullReset: true", graphFacadeGenerationRequest, StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryPreCommitChangeSequenceSupersessionUsesSecondRealGraphMutation()
    {
        string probeSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "tests", "godot", "RoadRendererUpdateTokenFailureProbe.cs"));
        string supersessionProbe = ExtractMethod(
            probeSource,
            "partial void ProbeOrdinaryPreCommitTokenSupersession(",
            "internal void ArmNextOrdinaryPresentationResourcePreflightFailure()");
        string changeSequenceArm = ExtractMethod(
            probeSource,
            "internal void ArmNextOrdinaryPreCommitChangeSequenceSupersession(",
            "private void ArmNextOrdinaryPreCommitTokenSupersession(");
        string changeSequenceRequest = ExtractMethod(
            probeSource,
            "private RoadRenderToken RequestOrdinaryPreCommitChangeSequenceSupersession(",
            "internal void ArmNextOrdinaryPresentationResourcePreflightFailure()");

        Assert.Contains(
            "OrdinaryPreCommitSupersessionKind.ChangeSequence",
            changeSequenceArm,
            StringComparison.Ordinal);
        Assert.Contains(
            "RequestOrdinaryPreCommitChangeSequenceSupersession(targetToken)",
            supersessionProbe,
            StringComparison.Ordinal);
        Assert.Contains("graph.SubmitPolyline(", changeSequenceRequest, StringComparison.Ordinal);
        Assert.Contains("RoadType.Street", changeSequenceRequest, StringComparison.Ordinal);
        Assert.Contains("result.Success", changeSequenceRequest, StringComparison.Ordinal);
        Assert.Contains("result.Changes.IsFullReset", changeSequenceRequest, StringComparison.Ordinal);
        Assert.Contains(
            "result.Changes.ChangeSequence != replacementChangeSequence",
            changeSequenceRequest,
            StringComparison.Ordinal);
        Assert.Contains(
            "_presentationTokens.DesiredToken is not RoadRenderToken replacementToken",
            changeSequenceRequest,
            StringComparison.Ordinal);
        Assert.Contains(
            "InvalidateScheduledStaticBatchRebuildContinuation();",
            changeSequenceRequest,
            StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryNodeBatchFactoryFailureRunsInsideTheOwnedUpdateAttempt()
    {
        string rendererSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Road", "RoadRenderer.cs"));
        string probeSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "tests", "godot", "RoadRendererUpdateTokenFailureProbe.cs"));
        string ordinaryBuild = ExtractMethod(
            rendererSource,
            "private bool TryRebuildStaticBatches",
            "private void PublishPresentationStalled");
        string factoryProbe = ExtractMethod(
            probeSource,
            "partial void ProbeOrdinaryNodeBatchFactoryFailure(",
            "internal void ArmNextOrdinaryPresentationResourcePreflightFailure()");

        int roadMeshCreation = ordinaryBuild.IndexOf(
            "roadMesh = CreateRoadMesh(",
            StringComparison.Ordinal);
        int nodeMarkerCapture = ordinaryBuild.IndexOf(
            "IReadOnlyList<RoadRendererNodeMarker> nodeMarkers = prepared.NodeMarkers;",
            StringComparison.Ordinal);
        int nodeBatchFailureProbe = ordinaryBuild.IndexOf(
            "ProbeOrdinaryNodeBatchFactoryFailure(targetToken, ref nodeMarkers);",
            StringComparison.Ordinal);
        int nodeBatchCreation = ordinaryBuild.IndexOf(
            "nodeBatch = CreateNodeBatch(nodeMarkers);",
            StringComparison.Ordinal);
        int resourcePreflightFailureProbe = ordinaryBuild.IndexOf(
            "ProbeOrdinaryPresentationResourcePreflightFailure(targetToken);",
            StringComparison.Ordinal);

        Assert.True(roadMeshCreation >= 0 && roadMeshCreation < nodeMarkerCapture);
        Assert.True(nodeMarkerCapture < nodeBatchFailureProbe);
        Assert.True(nodeBatchFailureProbe < nodeBatchCreation);
        Assert.True(nodeBatchCreation < resourcePreflightFailureProbe);
        Assert.Contains(
            "ref IReadOnlyList<RoadRendererNodeMarker> nodeMarkers",
            rendererSource,
            StringComparison.Ordinal);
        Assert.Contains("targetToken == armedToken", factoryProbe, StringComparison.Ordinal);
        Assert.Contains(
            "_ordinaryNodeBatchFactoryFailureArmed = false;",
            factoryProbe,
            StringComparison.Ordinal);
        Assert.Contains(
            "nodeMarkers = new OrdinaryNodeBatchFactoryFailureMarkers(this);",
            factoryProbe,
            StringComparison.Ordinal);
        Assert.Contains("public int Count => 1;", probeSource, StringComparison.Ordinal);
        Assert.Contains(
            "_ordinaryNodeBatchFactoryFailureMarkerReadCount++;",
            probeSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "throw new InvalidOperationException(",
            probeSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "OrdinaryNodeBatchFactoryFailureMessage);",
            probeSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LoadCommitPlanReleasesOnlyResourcesThatWereNotCommitted()
    {
        string loadSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Road", "RoadRenderer.LoadCommit.cs"));
        string plan = ExtractMethod(
            loadSource,
            "private sealed class RoadRendererLoadCommitPlan",
            "private static RoadRendererNodeSurface? CreateNodeSurface");

        int roadTransfer = plan.IndexOf("_owner._roadBatchLayer.Mesh = _roadMesh;", StringComparison.Ordinal);
        int nodeTransfer = plan.IndexOf(
            "_owner._nodeBatchLayer.Multimesh = _nodeBatch;",
            StringComparison.Ordinal);
        int commit = plan.IndexOf("_committed = true;", StringComparison.Ordinal);
        int disposedGuard = plan.LastIndexOf("if (_disposed)", StringComparison.Ordinal);
        int committedGuard = plan.LastIndexOf("if (_completed || _committed)", StringComparison.Ordinal);
        int release = plan.LastIndexOf(
            "DisposePreparedPresentationResources(_roadMesh, _nodeBatch);",
            StringComparison.Ordinal);

        Assert.True(roadTransfer >= 0 && roadTransfer < nodeTransfer);
        Assert.True(nodeTransfer < commit);
        Assert.True(commit < disposedGuard);
        Assert.True(disposedGuard < committedGuard && committedGuard < release);
        Assert.Contains("_disposed = true;", plan);
        Assert.Contains("finally", plan[release..]);
        Assert.Contains("_admission.Dispose();", plan[release..]);
    }

    [Fact]
    public void RendererCommitBoundaryProbeInvalidatesTheRealPlanBeforeReferenceSwap()
    {
        string probeSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "tests", "godot", "RoadLoadPreflightResourceFailureProbe.cs"));
        string probe = ExtractMethod(
            probeSource,
            "internal Godot.Collections.Dictionary ProbeLoadCommitBoundaryGenerationMismatch()",
            "private sealed class BoundaryInvalidatingLease");

        int rendererPlan = probe.IndexOf(
            "INonThrowingLoadCommitPlan rendererPlan = PreflightPreparedLoad(",
            StringComparison.Ordinal);
        int invalidatingLease = probe.IndexOf(
            "new BoundaryInvalidatingLease(admission.Dispose)",
            StringComparison.Ordinal);
        int aggregate = probe.IndexOf(
            "new PreparedAggregateLoad([",
            StringComparison.Ordinal);
        int commit = probe.IndexOf("aggregate.Commit(operation);", StringComparison.Ordinal);
        int staleCheck = probe.IndexOf(
            "planBecameStale = !rendererPlan.IsGenerationCurrent;",
            StringComparison.Ordinal);
        int reacquire = probe.IndexOf(
            "using (RoadRendererLoadAdmission reacquired = BeginLoadAdmission())",
            StringComparison.Ordinal);

        Assert.True(rendererPlan >= 0 && rendererPlan < invalidatingLease);
        Assert.True(invalidatingLease < aggregate && aggregate < commit);
        Assert.True(commit < staleCheck && staleCheck < reacquire);
        Assert.Contains("graphPlan,", probe, StringComparison.Ordinal);
        Assert.Contains("toolPlan,", probe, StringComparison.Ordinal);
        Assert.Contains("rendererPlan,", probe, StringComparison.Ordinal);
        Assert.Contains("slotPlan])", probe, StringComparison.Ordinal);
        Assert.Contains("exception is LoadPreflightInvalidException", probe, StringComparison.Ordinal);
        Assert.Contains("ExpectedFailureMessage", probe, StringComparison.Ordinal);
        Assert.Contains("graphPlan.CommitCount", probe, StringComparison.Ordinal);
        Assert.Contains("toolPlan.CommitCount", probe, StringComparison.Ordinal);
        Assert.Contains("slotPlan.CommitCount", probe, StringComparison.Ordinal);
        Assert.Contains("retainedDesiredToken == _presentationTokens.DesiredToken", probe);
        Assert.Contains("retainedPresentedToken == _presentationTokens.PresentedToken", probe);
    }

    [Fact]
    public void ToolCommitBoundaryProbeInvalidatesTheRealPlanBeforeReferenceSwap()
    {
        string probeSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "tests", "godot", "RoadLoadPreflightResourceFailureProbe.cs"));
        string probe = ExtractMethod(
            probeSource,
            "internal Godot.Collections.Dictionary ProbeToolLoadCommitBoundaryGenerationMismatch()",
            "private sealed class ToolBoundaryInvalidatingLease");

        int toolPlan = probe.IndexOf(
            "INonThrowingLoadCommitPlan toolPlan = PreflightFullReset(admission);",
            StringComparison.Ordinal);
        int invalidatingLease = probe.IndexOf(
            "new ToolBoundaryInvalidatingLease(admission.Dispose)",
            StringComparison.Ordinal);
        int aggregate = probe.IndexOf(
            "new PreparedAggregateLoad([",
            StringComparison.Ordinal);
        int commit = probe.IndexOf("aggregate.Commit(operation);", StringComparison.Ordinal);
        int staleCheck = probe.IndexOf(
            "planBecameStale = !toolPlan.IsGenerationCurrent;",
            StringComparison.Ordinal);
        int toolReacquire = probe.IndexOf(
            "using (ToolLoadAdmission reacquired = BeginLoadAdmission())",
            StringComparison.Ordinal);
        int builderReacquire = probe.IndexOf(
            "using (RoadBuilder.RoadBuilderLoadAdmission reacquired =",
            StringComparison.Ordinal);

        Assert.True(toolPlan >= 0 && toolPlan < invalidatingLease);
        Assert.True(invalidatingLease < aggregate && aggregate < commit);
        Assert.True(commit < staleCheck && staleCheck < toolReacquire);
        Assert.True(toolReacquire < builderReacquire);
        Assert.Contains("graphPlan,", probe, StringComparison.Ordinal);
        Assert.Contains("toolPlan,", probe, StringComparison.Ordinal);
        Assert.Contains("rendererPlan,", probe, StringComparison.Ordinal);
        Assert.Contains("slotPlan])", probe, StringComparison.Ordinal);
        Assert.Contains("exception is LoadPreflightInvalidException", probe, StringComparison.Ordinal);
        Assert.Contains("ExpectedFailureMessage", probe, StringComparison.Ordinal);
        Assert.Contains("graphPlan.CommitCount", probe, StringComparison.Ordinal);
        Assert.Contains("rendererPlan.CommitCount", probe, StringComparison.Ordinal);
        Assert.Contains("slotPlan.CommitCount", probe, StringComparison.Ordinal);
        Assert.Contains("retainedCurrentTool == _currentTool", probe, StringComparison.Ordinal);
        Assert.Contains(
            "retainedSelectedRoadType == builder.SelectedRoadType",
            probe,
            StringComparison.Ordinal);
        Assert.Contains("ReferenceEquals(retainedDraft, builder.CurrentDraft)", probe);
        Assert.Contains("retainedFixedCornerCount == builder.FixedCornerCount", probe);
        Assert.Contains("retainedUndoCount == builder.GetUndoEditCount()", probe);
        Assert.Contains("retainedRedoCount == builder.GetRedoEditCount()", probe);
    }

    [Fact]
    public void RealLoadParticipantsIsolateRendererObserversAndExposeCleanupBoundaries()
    {
        string loadSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Road", "RoadRenderer.LoadCommit.cs"));
        string toolSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Tools", "ToolManager.LoadCommit.cs"));
        string saveManagerSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Core", "SaveManager.cs"));
        string plan = ExtractMethod(
            loadSource,
            "private sealed class RoadRendererLoadCommitPlan",
            "private static RoadRendererNodeSurface? CreateNodeSurface");
        string notifications = ExtractMethod(
            plan,
            "public IReadOnlyList<string> PublishNotifications()",
            "public void CompleteCommit()");
        string completion = ExtractMethod(
            plan,
            "public void CompleteCommit()",
            "public void Dispose()");

        int invocationList = notifications.IndexOf("GetInvocationList()", StringComparison.Ordinal);
        int handlerCall = notifications.IndexOf("handler(_targetRenderToken);", StringComparison.Ordinal);
        int warning = notifications.IndexOf(
            "warnings.Add($\"Road presentation observer failed: {exception.Message}\")",
            StringComparison.Ordinal);
        int abandonAdmission = completion.IndexOf(
            "_owner.AbandonLoadAdmission(_admission);",
            StringComparison.Ordinal);
        int markCompleted = completion.IndexOf("_completed = true;", StringComparison.Ordinal);
        int redraw = completion.IndexOf("_owner.QueueRedraw();", StringComparison.Ordinal);
        int cleanupFailureProbe = completion.IndexOf(
            "_owner.ProbeLoadCompleteCommitFailure();",
            StringComparison.Ordinal);

        Assert.True(invocationList >= 0 && invocationList < handlerCall);
        Assert.True(handlerCall < warning);
        Assert.Contains("catch (Exception exception)", notifications, StringComparison.Ordinal);
        Assert.Contains("return warnings;", notifications, StringComparison.Ordinal);
        Assert.True(abandonAdmission >= 0 && abandonAdmission < markCompleted);
        Assert.True(markCompleted < redraw);
        Assert.True(redraw < cleanupFailureProbe);
        Assert.Contains(
            "partial void ProbeLoadCompleteCommitFailure();",
            loadSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "public IReadOnlyList<string> PublishNotifications() => [];",
            toolSource,
            StringComparison.Ordinal);
        Assert.Contains("_builderPlan.CompleteCommit();", toolSource, StringComparison.Ordinal);
        Assert.Contains(
            "_owner.AbandonLoadAdmission(_admission);",
            toolSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "public string ParticipantID => \"slot-target\";",
            saveManagerSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "public IReadOnlyList<string> PublishNotifications() => [];",
            saveManagerSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "public void CompleteCommit() => _completeCommit();",
            saveManagerSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ToolCleanupFailureProbeRunsAfterRealAdmissionRelease()
    {
        string toolSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Tools", "ToolManager.LoadCommit.cs"));
        string probeSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "tests", "godot", "RoadLoadObserverFailureProbe.cs"));
        string completion = ExtractMethod(
            toolSource,
            "public void CompleteCommit()",
            "public void Dispose()");

        int builderCompletion = completion.IndexOf(
            "_builderPlan.CompleteCommit();",
            StringComparison.Ordinal);
        int toolAdmissionRelease = completion.IndexOf(
            "_owner.AbandonLoadAdmission(_admission);",
            StringComparison.Ordinal);
        int markCompleted = completion.IndexOf("_completed = true;", StringComparison.Ordinal);
        int cleanupFailureProbe = completion.IndexOf(
            "_owner.ProbeLoadCompleteCommitFailure();",
            StringComparison.Ordinal);

        Assert.True(builderCompletion >= 0 && builderCompletion < toolAdmissionRelease);
        Assert.True(toolAdmissionRelease < markCompleted);
        Assert.True(markCompleted < cleanupFailureProbe);
        Assert.Contains(
            "partial void ProbeLoadCompleteCommitFailure();",
            toolSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "partial void ProbeLoadCompleteCommitFailure()",
            probeSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "_loadCompleteCommitFailureArmed = false;",
            probeSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "_loadCompleteCommitFailureCount++;",
            probeSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "throw new InvalidOperationException(LoadCompleteCommitFailureMessage);",
            probeSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RendererCleanupFailureProbeRunsAfterRealAdmissionRelease()
    {
        string rendererSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Road", "RoadRenderer.LoadCommit.cs"));
        string probeSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "tests", "godot", "RoadLoadObserverFailureProbe.cs"));
        string completion = ExtractMethod(
            rendererSource,
            "public void CompleteCommit()",
            "public void Dispose()");

        int rendererAdmissionRelease = completion.IndexOf(
            "_owner.AbandonLoadAdmission(_admission);",
            StringComparison.Ordinal);
        int markCompleted = completion.IndexOf("_completed = true;", StringComparison.Ordinal);
        int redraw = completion.IndexOf("_owner.QueueRedraw();", StringComparison.Ordinal);
        int cleanupFailureProbe = completion.IndexOf(
            "_owner.ProbeLoadCompleteCommitFailure();",
            StringComparison.Ordinal);

        Assert.True(rendererAdmissionRelease >= 0 && rendererAdmissionRelease < markCompleted);
        Assert.True(markCompleted < redraw);
        Assert.True(redraw < cleanupFailureProbe);
        Assert.Contains(
            "partial void ProbeLoadCompleteCommitFailure();",
            rendererSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "public partial class RoadRenderer",
            probeSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "partial void ProbeLoadCompleteCommitFailure()",
            probeSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "_loadCompleteCommitFailureArmed = false;",
            probeSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "_loadCompleteCommitFailureCount++;",
            probeSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "throw new InvalidOperationException(LoadCompleteCommitFailureMessage);",
            probeSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RoadGraphCleanupFailureProbeRunsAfterRealAdmissionRelease()
    {
        string graphSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Road", "RoadGraph.LoadCommit.cs"));
        string probeSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "tests", "godot", "RoadLoadObserverFailureProbe.cs"));
        string completion = ExtractMethod(
            graphSource,
            "public void CompleteCommit()",
            "public void Dispose()");

        int committedGuard = completion.IndexOf(
            "if (!_referencesCommitted)",
            StringComparison.Ordinal);
        int graphAdmissionRelease = completion.IndexOf(
            "_owner.AbandonLoadAdmission(_admission);",
            StringComparison.Ordinal);
        int markCompleted = completion.IndexOf("_completed = true;", StringComparison.Ordinal);
        int cleanupFailureProbe = completion.IndexOf(
            "_owner.ProbeLoadCompleteCommitFailure();",
            StringComparison.Ordinal);

        Assert.True(committedGuard >= 0 && committedGuard < graphAdmissionRelease);
        Assert.True(graphAdmissionRelease < markCompleted);
        Assert.True(markCompleted < cleanupFailureProbe);
        Assert.Contains(
            "partial void ProbeLoadCompleteCommitFailure();",
            graphSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "public partial class RoadGraph",
            probeSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "public void ArmGraphCleanupFailure(RoadSystem roadSystem)",
            probeSource,
            StringComparison.Ordinal);
        Assert.Contains("RoadGraph graph = roadSystem.Graph;", probeSource, StringComparison.Ordinal);
        Assert.Contains(
            "partial void ProbeLoadCompleteCommitFailure()",
            probeSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "_loadCompleteCommitFailureArmed = false;",
            probeSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "_loadCompleteCommitFailureCount++;",
            probeSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "throw new InvalidOperationException(LoadCompleteCommitFailureMessage);",
            probeSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SlotTargetCleanupFailureProbeRunsAfterCurrentSlotCommit()
    {
        string saveManagerSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Core", "SaveManager.cs"));
        string probeSource = File.ReadAllText(
            Path.Combine(ProjectRoot, "tests", "godot", "RoadLoadObserverFailureProbe.cs"));
        string referenceCommit = ExtractMethod(
            saveManagerSource,
            "public void CommitReferences()",
            "public IReadOnlyList<string> PublishNotifications()");
        string completion = ExtractMethod(
            saveManagerSource,
            "public void CompleteCommit()",
            "public void Dispose()");
        string completionBridge = ExtractMethod(
            saveManagerSource,
            "private void CompleteSlotTargetLoadCommit()",
            "private sealed record SceneRequest");

        int setCurrentSlot = referenceCommit.IndexOf(
            "_setCurrentSlot(_slotID);",
            StringComparison.Ordinal);
        int markCommitted = referenceCommit.IndexOf("_committed = true;", StringComparison.Ordinal);

        Assert.True(setCurrentSlot >= 0 && setCurrentSlot < markCommitted);
        Assert.Contains("_completeCommit();", completion, StringComparison.Ordinal);
        Assert.Contains(
            "ProbeSlotTargetLoadCompleteCommitFailure();",
            completionBridge,
            StringComparison.Ordinal);
        Assert.Contains(
            "partial void ProbeSlotTargetLoadCompleteCommitFailure();",
            saveManagerSource,
            StringComparison.Ordinal);
        Assert.Contains("public partial class SaveManager", probeSource, StringComparison.Ordinal);
        Assert.Contains(
            "public void ArmSlotCleanupFailure(SaveManager saveManager)",
            probeSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "partial void ProbeSlotTargetLoadCompleteCommitFailure()",
            probeSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "_slotTargetLoadCompleteCommitFailureArmed = false;",
            probeSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "_slotTargetLoadCompleteCommitFailureCount++;",
            probeSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "throw new InvalidOperationException(SlotTargetLoadCompleteCommitFailureMessage);",
            probeSource,
            StringComparison.Ordinal);
    }

    private static string ExtractMethod(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"Could not extract {startMarker}.");
        return source[start..end];
    }
}
