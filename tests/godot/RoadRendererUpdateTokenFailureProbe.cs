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

        _ordinaryRoadSurfaceSnapshotFailureArmed = true;
        _ordinaryRoadSurfaceSnapshotFailureArmToken = currentToken;
    }

    internal bool IsOrdinaryRoadSurfaceSnapshotFailureArmed() =>
        _ordinaryRoadSurfaceSnapshotFailureArmed;

    internal int GetOrdinaryRoadSurfaceSnapshotFailureCount() =>
        _ordinaryRoadSurfaceSnapshotFailureCount;

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
