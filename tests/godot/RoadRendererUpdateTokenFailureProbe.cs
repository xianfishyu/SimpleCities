using Godot;
using System;
using System.Collections;
using System.Collections.Generic;

public partial class RoadRendererUpdateTokenFailureProbe : RefCounted
{
    private RoadRenderer? _renderer;

    public void Arm(RoadRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.ArmNextOrdinaryPresentationResourcePreflightFailure();
        _renderer = renderer;
    }

    public void ArmNodeBatchFactoryFailure(RoadRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.ArmNextOrdinaryNodeBatchFactoryFailure();
        _renderer = renderer;
    }

    public void ArmRoadMeshFactoryFailure(RoadRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.ArmNextOrdinaryRoadMeshFactoryFailure();
        _renderer = renderer;
    }

    public void ArmRoadSurfaceSnapshotFailure(RoadRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.ArmNextOrdinaryRoadSurfaceSnapshotFailure();
        _renderer = renderer;
    }

    public void ArmPreCommitTokenSupersession(RoadRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.ArmNextOrdinaryPreCommitTokenSupersession();
        _renderer = renderer;
    }

    public void ArmPreCommitRoadStyleSupersession(RoadRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.ArmNextOrdinaryPreCommitRoadStyleSupersession();
        _renderer = renderer;
    }

    public bool IsArmed() =>
        _renderer?.IsOrdinaryPresentationResourcePreflightFailureArmed() ?? false;

    public int GetTriggerCount() =>
        _renderer?.GetOrdinaryPresentationResourcePreflightFailureCount() ?? 0;

    public string GetFailureMessage() =>
        RoadRenderer.OrdinaryPresentationResourcePreflightFailureMessage;

    public bool IsNodeBatchFactoryFailureArmed() =>
        _renderer?.IsOrdinaryNodeBatchFactoryFailureArmed() ?? false;

    public int GetNodeBatchFactoryFailureCount() =>
        _renderer?.GetOrdinaryNodeBatchFactoryFailureCount() ?? 0;

    public int GetNodeBatchFactoryMarkerReadCount() =>
        _renderer?.GetOrdinaryNodeBatchFactoryMarkerReadCount() ?? 0;

    public string GetNodeBatchFactoryFailureMessage() =>
        RoadRenderer.OrdinaryNodeBatchFactoryFailureMessage;

    public bool IsRoadMeshFactoryFailureArmed() =>
        _renderer?.IsOrdinaryRoadMeshFactoryFailureArmed() ?? false;

    public int GetRoadMeshFactoryFailureCount() =>
        _renderer?.GetOrdinaryRoadMeshFactoryFailureCount() ?? 0;

    public int GetRoadMeshFactoryIndexEnumerationCount() =>
        _renderer?.GetOrdinaryRoadMeshFactoryIndexEnumerationCount() ?? 0;

    public string GetRoadMeshFactoryFailureMessage() =>
        RoadRenderer.OrdinaryRoadMeshFactoryFailureMessage;

    public bool IsRoadSurfaceSnapshotFailureArmed() =>
        _renderer?.IsOrdinaryRoadSurfaceSnapshotFailureArmed() ?? false;

    public int GetRoadSurfaceSnapshotFailureCount() =>
        _renderer?.GetOrdinaryRoadSurfaceSnapshotFailureCount() ?? 0;

    public string GetRoadSurfaceSnapshotFailureMessage() =>
        new ArgumentNullException("prepared").Message;

    public bool IsPreCommitTokenSupersessionArmed() =>
        _renderer?.IsOrdinaryPreCommitTokenSupersessionArmed() ?? false;

    public int GetPreCommitTokenSupersessionCount() =>
        _renderer?.GetOrdinaryPreCommitTokenSupersessionCount() ?? 0;

    public int GetPreCommitSupersededAttemptNumber() =>
        _renderer?.GetOrdinaryPreCommitSupersededAttemptNumber() ?? 0;

    public Godot.Collections.Dictionary GetPreCommitSupersededToken() =>
        _renderer?.GetOrdinaryPreCommitSupersededToken() ?? new();

    public Godot.Collections.Dictionary GetPreCommitReplacementToken() =>
        _renderer?.GetOrdinaryPreCommitReplacementToken() ?? new();

    public bool CompletePreCommitTokenSupersession() =>
        _renderer?.CompleteOrdinaryPreCommitTokenSupersession() ?? false;

    public long GetObjectResourceCount() => Convert.ToInt64(
        Performance.GetMonitor(Performance.Monitor.ObjectResourceCount));
}

public partial class RoadRenderer
{
    internal const string OrdinaryPresentationResourcePreflightFailureMessage =
        "Injected ordinary road presentation resource preflight failure.";
    internal const string OrdinaryNodeBatchFactoryFailureMessage =
        "Injected ordinary CreateNodeBatch marker read failure.";
    internal const string OrdinaryRoadMeshFactoryFailureMessage =
        "Injected ordinary CreateRoadMesh index enumeration failure.";

    private bool _ordinaryPresentationResourcePreflightFailureArmed;
    private RoadRenderToken? _ordinaryPresentationResourcePreflightFailureArmToken;
    private int _ordinaryPresentationResourcePreflightFailureCount;
    private bool _ordinaryNodeBatchFactoryFailureArmed;
    private RoadRenderToken? _ordinaryNodeBatchFactoryFailureArmToken;
    private int _ordinaryNodeBatchFactoryFailureCount;
    private int _ordinaryNodeBatchFactoryFailureMarkerReadCount;
    private bool _ordinaryRoadMeshFactoryFailureArmed;
    private RoadRenderToken? _ordinaryRoadMeshFactoryFailureArmToken;
    private int _ordinaryRoadMeshFactoryFailureCount;
    private int _ordinaryRoadMeshFactoryFailureIndexEnumerationCount;
    private bool _ordinaryRoadSurfaceSnapshotFailureArmed;
    private RoadRenderToken? _ordinaryRoadSurfaceSnapshotFailureArmToken;
    private int _ordinaryRoadSurfaceSnapshotFailureCount;
    private bool _ordinaryPreCommitTokenSupersessionArmed;
    private RoadRenderToken? _ordinaryPreCommitTokenSupersessionArmToken;
    private int _ordinaryPreCommitTokenSupersessionCount;
    private int _ordinaryPreCommitSupersededAttemptNumber;
    private RoadRenderToken? _ordinaryPreCommitSupersededToken;
    private RoadRenderToken? _ordinaryPreCommitReplacementToken;
    private bool _ordinaryPreCommitAdvancesRoadStyleRevision;

    partial void ProbeOrdinaryPresentationResourcePreflightFailure(
        RoadRenderToken targetToken)
    {
        if (!_ordinaryPresentationResourcePreflightFailureArmed ||
            _ordinaryPresentationResourcePreflightFailureArmToken is not
                RoadRenderToken armedToken ||
            targetToken == armedToken)
        {
            return;
        }

        _ordinaryPresentationResourcePreflightFailureArmed = false;
        _ordinaryPresentationResourcePreflightFailureArmToken = null;
        _ordinaryPresentationResourcePreflightFailureCount++;
        throw new InvalidOperationException(
            OrdinaryPresentationResourcePreflightFailureMessage);
    }

    partial void ProbeOrdinaryRoadMeshFactoryFailure(
        RoadRenderToken targetToken,
        ref IReadOnlyCollection<int> roadIndices)
    {
        if (!_ordinaryRoadMeshFactoryFailureArmed ||
            _ordinaryRoadMeshFactoryFailureArmToken is not RoadRenderToken armedToken ||
            targetToken == armedToken)
        {
            return;
        }

        _ordinaryRoadMeshFactoryFailureArmed = false;
        _ordinaryRoadMeshFactoryFailureArmToken = null;
        _ordinaryRoadMeshFactoryFailureCount++;
        roadIndices = new OrdinaryRoadMeshFactoryFailureIndices(this);
    }

    partial void ProbeOrdinaryNodeBatchFactoryFailure(
        RoadRenderToken targetToken,
        ref IReadOnlyList<RoadRendererNodeMarker> nodeMarkers)
    {
        if (!_ordinaryNodeBatchFactoryFailureArmed ||
            _ordinaryNodeBatchFactoryFailureArmToken is not RoadRenderToken armedToken ||
            targetToken == armedToken)
        {
            return;
        }

        _ordinaryNodeBatchFactoryFailureArmed = false;
        _ordinaryNodeBatchFactoryFailureArmToken = null;
        _ordinaryNodeBatchFactoryFailureCount++;
        nodeMarkers = new OrdinaryNodeBatchFactoryFailureMarkers(this);
    }

    partial void ProbeOrdinaryRoadSurfaceSnapshotFailure(
        RoadRenderToken targetToken,
        ref RoadSurfaceSnapshot.PreparedData roadSurface)
    {
        if (!_ordinaryRoadSurfaceSnapshotFailureArmed ||
            _ordinaryRoadSurfaceSnapshotFailureArmToken is not RoadRenderToken armedToken ||
            targetToken == armedToken)
        {
            return;
        }

        _ordinaryRoadSurfaceSnapshotFailureArmed = false;
        _ordinaryRoadSurfaceSnapshotFailureArmToken = null;
        _ordinaryRoadSurfaceSnapshotFailureCount++;
        roadSurface = null!;
    }

    partial void ProbeOrdinaryPreCommitTokenSupersession(
        RoadRenderToken targetToken)
    {
        if (!_ordinaryPreCommitTokenSupersessionArmed ||
            _ordinaryPreCommitTokenSupersessionArmToken is not RoadRenderToken armedToken ||
            targetToken == armedToken)
        {
            return;
        }

        _ordinaryPreCommitTokenSupersessionArmed = false;
        _ordinaryPreCommitTokenSupersessionArmToken = null;
        _ordinaryPreCommitTokenSupersessionCount++;
        _ordinaryPreCommitSupersededAttemptNumber = _presentationTokens.AttemptCount;
        _ordinaryPreCommitSupersededToken = targetToken;
        _ordinaryPreCommitReplacementToken =
            _ordinaryPreCommitAdvancesRoadStyleRevision
                ? _presentationTokens.RequestStyleRefresh(targetToken.ChangeSequence)
                : _presentationTokens.RequestRebuild(targetToken.ChangeSequence);
        _ordinaryPreCommitAdvancesRoadStyleRevision = false;
    }

    internal void ArmNextOrdinaryPresentationResourcePreflightFailure()
    {
        if (!IsPresentationReady() ||
            _presentationTokens.PresentedToken is not RoadRenderToken currentToken)
        {
            throw new InvalidOperationException(
                "Road presentation must be ready before arming its failure probe.");
        }
        if (_ordinaryPresentationResourcePreflightFailureArmed)
        {
            throw new InvalidOperationException(
                "Road presentation resource preflight failure probe is already armed.");
        }
        if (_ordinaryNodeBatchFactoryFailureArmed)
        {
            throw new InvalidOperationException(
                "Road presentation node-batch factory failure probe is already armed.");
        }
        if (_ordinaryRoadMeshFactoryFailureArmed)
        {
            throw new InvalidOperationException(
                "Road presentation road-mesh factory failure probe is already armed.");
        }
        if (_ordinaryRoadSurfaceSnapshotFailureArmed)
        {
            throw new InvalidOperationException(
                "Road presentation surface-snapshot failure probe is already armed.");
        }
        if (_ordinaryPreCommitTokenSupersessionArmed)
        {
            throw new InvalidOperationException(
                "Road presentation pre-commit supersession probe is already armed.");
        }

        _ordinaryPresentationResourcePreflightFailureArmed = true;
        _ordinaryPresentationResourcePreflightFailureArmToken = currentToken;
    }

    internal bool IsOrdinaryPresentationResourcePreflightFailureArmed() =>
        _ordinaryPresentationResourcePreflightFailureArmed;

    internal int GetOrdinaryPresentationResourcePreflightFailureCount() =>
        _ordinaryPresentationResourcePreflightFailureCount;

    internal void ArmNextOrdinaryNodeBatchFactoryFailure()
    {
        if (!IsPresentationReady() ||
            _presentationTokens.PresentedToken is not RoadRenderToken currentToken)
        {
            throw new InvalidOperationException(
                "Road presentation must be ready before arming its failure probe.");
        }
        if (_ordinaryNodeBatchFactoryFailureArmed)
        {
            throw new InvalidOperationException(
                "Road presentation node-batch factory failure probe is already armed.");
        }
        if (_ordinaryPresentationResourcePreflightFailureArmed)
        {
            throw new InvalidOperationException(
                "Road presentation resource preflight failure probe is already armed.");
        }
        if (_ordinaryRoadMeshFactoryFailureArmed)
        {
            throw new InvalidOperationException(
                "Road presentation road-mesh factory failure probe is already armed.");
        }
        if (_ordinaryRoadSurfaceSnapshotFailureArmed)
        {
            throw new InvalidOperationException(
                "Road presentation surface-snapshot failure probe is already armed.");
        }
        if (_ordinaryPreCommitTokenSupersessionArmed)
        {
            throw new InvalidOperationException(
                "Road presentation pre-commit supersession probe is already armed.");
        }

        _ordinaryNodeBatchFactoryFailureArmed = true;
        _ordinaryNodeBatchFactoryFailureArmToken = currentToken;
    }

    internal bool IsOrdinaryNodeBatchFactoryFailureArmed() =>
        _ordinaryNodeBatchFactoryFailureArmed;

    internal int GetOrdinaryNodeBatchFactoryFailureCount() =>
        _ordinaryNodeBatchFactoryFailureCount;

    internal int GetOrdinaryNodeBatchFactoryMarkerReadCount() =>
        _ordinaryNodeBatchFactoryFailureMarkerReadCount;

    internal void ArmNextOrdinaryRoadMeshFactoryFailure()
    {
        if (!IsPresentationReady() ||
            _presentationTokens.PresentedToken is not RoadRenderToken currentToken)
        {
            throw new InvalidOperationException(
                "Road presentation must be ready before arming its failure probe.");
        }
        if (_ordinaryRoadMeshFactoryFailureArmed)
        {
            throw new InvalidOperationException(
                "Road presentation road-mesh factory failure probe is already armed.");
        }
        if (_ordinaryPresentationResourcePreflightFailureArmed)
        {
            throw new InvalidOperationException(
                "Road presentation resource preflight failure probe is already armed.");
        }
        if (_ordinaryNodeBatchFactoryFailureArmed)
        {
            throw new InvalidOperationException(
                "Road presentation node-batch factory failure probe is already armed.");
        }
        if (_ordinaryRoadSurfaceSnapshotFailureArmed)
        {
            throw new InvalidOperationException(
                "Road presentation surface-snapshot failure probe is already armed.");
        }
        if (_ordinaryPreCommitTokenSupersessionArmed)
        {
            throw new InvalidOperationException(
                "Road presentation pre-commit supersession probe is already armed.");
        }

        _ordinaryRoadMeshFactoryFailureArmed = true;
        _ordinaryRoadMeshFactoryFailureArmToken = currentToken;
    }

    internal bool IsOrdinaryRoadMeshFactoryFailureArmed() =>
        _ordinaryRoadMeshFactoryFailureArmed;

    internal int GetOrdinaryRoadMeshFactoryFailureCount() =>
        _ordinaryRoadMeshFactoryFailureCount;

    internal int GetOrdinaryRoadMeshFactoryIndexEnumerationCount() =>
        _ordinaryRoadMeshFactoryFailureIndexEnumerationCount;

    internal void ArmNextOrdinaryRoadSurfaceSnapshotFailure()
    {
        if (!IsPresentationReady() ||
            _presentationTokens.PresentedToken is not RoadRenderToken currentToken)
        {
            throw new InvalidOperationException(
                "Road presentation must be ready before arming its failure probe.");
        }
        if (_ordinaryRoadSurfaceSnapshotFailureArmed)
        {
            throw new InvalidOperationException(
                "Road presentation surface-snapshot failure probe is already armed.");
        }
        if (_ordinaryPresentationResourcePreflightFailureArmed)
        {
            throw new InvalidOperationException(
                "Road presentation resource preflight failure probe is already armed.");
        }
        if (_ordinaryRoadMeshFactoryFailureArmed)
        {
            throw new InvalidOperationException(
                "Road presentation road-mesh factory failure probe is already armed.");
        }
        if (_ordinaryNodeBatchFactoryFailureArmed)
        {
            throw new InvalidOperationException(
                "Road presentation node-batch factory failure probe is already armed.");
        }
        if (_ordinaryPreCommitTokenSupersessionArmed)
        {
            throw new InvalidOperationException(
                "Road presentation pre-commit supersession probe is already armed.");
        }

        _ordinaryRoadSurfaceSnapshotFailureArmed = true;
        _ordinaryRoadSurfaceSnapshotFailureArmToken = currentToken;
    }

    internal bool IsOrdinaryRoadSurfaceSnapshotFailureArmed() =>
        _ordinaryRoadSurfaceSnapshotFailureArmed;

    internal int GetOrdinaryRoadSurfaceSnapshotFailureCount() =>
        _ordinaryRoadSurfaceSnapshotFailureCount;

    internal void ArmNextOrdinaryPreCommitTokenSupersession() =>
        ArmNextOrdinaryPreCommitTokenSupersession(
            advanceRoadStyleRevision: false);

    internal void ArmNextOrdinaryPreCommitRoadStyleSupersession() =>
        ArmNextOrdinaryPreCommitTokenSupersession(
            advanceRoadStyleRevision: true);

    private void ArmNextOrdinaryPreCommitTokenSupersession(
        bool advanceRoadStyleRevision)
    {
        if (!IsPresentationReady() ||
            _presentationTokens.PresentedToken is not RoadRenderToken currentToken)
        {
            throw new InvalidOperationException(
                "Road presentation must be ready before arming its failure probe.");
        }
        if (_ordinaryPreCommitTokenSupersessionArmed)
        {
            throw new InvalidOperationException(
                "Road presentation pre-commit supersession probe is already armed.");
        }
        if (_ordinaryPresentationResourcePreflightFailureArmed)
        {
            throw new InvalidOperationException(
                "Road presentation resource preflight failure probe is already armed.");
        }
        if (_ordinaryRoadMeshFactoryFailureArmed)
        {
            throw new InvalidOperationException(
                "Road presentation road-mesh factory failure probe is already armed.");
        }
        if (_ordinaryNodeBatchFactoryFailureArmed)
        {
            throw new InvalidOperationException(
                "Road presentation node-batch factory failure probe is already armed.");
        }
        if (_ordinaryRoadSurfaceSnapshotFailureArmed)
        {
            throw new InvalidOperationException(
                "Road presentation surface-snapshot failure probe is already armed.");
        }

        _ordinaryPreCommitTokenSupersessionArmed = true;
        _ordinaryPreCommitTokenSupersessionArmToken = currentToken;
        _ordinaryPreCommitSupersededAttemptNumber = 0;
        _ordinaryPreCommitSupersededToken = null;
        _ordinaryPreCommitReplacementToken = null;
        _ordinaryPreCommitAdvancesRoadStyleRevision = advanceRoadStyleRevision;
    }

    internal bool IsOrdinaryPreCommitTokenSupersessionArmed() =>
        _ordinaryPreCommitTokenSupersessionArmed;

    internal int GetOrdinaryPreCommitTokenSupersessionCount() =>
        _ordinaryPreCommitTokenSupersessionCount;

    internal int GetOrdinaryPreCommitSupersededAttemptNumber() =>
        _ordinaryPreCommitSupersededAttemptNumber;

    internal Godot.Collections.Dictionary GetOrdinaryPreCommitSupersededToken() =>
        ToTokenDictionary(_ordinaryPreCommitSupersededToken);

    internal Godot.Collections.Dictionary GetOrdinaryPreCommitReplacementToken() =>
        ToTokenDictionary(_ordinaryPreCommitReplacementToken);

    internal bool CompleteOrdinaryPreCommitTokenSupersession()
    {
        if (_ordinaryPreCommitTokenSupersessionArmed ||
            _ordinaryPreCommitReplacementToken is not RoadRenderToken replacementToken ||
            _presentationTokens.DesiredToken != replacementToken ||
            _presentationTokens.PresentedToken == replacementToken)
        {
            return false;
        }

        return TryRebuildStaticBatches() &&
               _presentationTokens.PresentedToken == replacementToken;
    }

    private sealed class OrdinaryRoadMeshFactoryFailureIndices(RoadRenderer owner)
        : IReadOnlyCollection<int>
    {
        public int Count => 1;

        public IEnumerator<int> GetEnumerator()
        {
            owner._ordinaryRoadMeshFactoryFailureIndexEnumerationCount++;
            throw new InvalidOperationException(
                OrdinaryRoadMeshFactoryFailureMessage);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class OrdinaryNodeBatchFactoryFailureMarkers(RoadRenderer owner)
        : IReadOnlyList<RoadRendererNodeMarker>
    {
        public int Count => 1;

        public RoadRendererNodeMarker this[int index]
        {
            get
            {
                owner._ordinaryNodeBatchFactoryFailureMarkerReadCount++;
                throw new InvalidOperationException(
                    OrdinaryNodeBatchFactoryFailureMessage);
            }
        }

        public IEnumerator<RoadRendererNodeMarker> GetEnumerator() =>
            ((IEnumerable<RoadRendererNodeMarker>)Array.Empty<RoadRendererNodeMarker>())
                .GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
