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

    public long GetObjectResourceCount() => Convert.ToInt64(
        Performance.GetMonitor(Performance.Monitor.ObjectResourceCount));
}

public partial class RoadRenderer
{
    internal const string OrdinaryPresentationResourcePreflightFailureMessage =
        "Injected ordinary road presentation resource preflight failure.";
    internal const string OrdinaryNodeBatchFactoryFailureMessage =
        "Injected ordinary CreateNodeBatch marker read failure.";

    private bool _ordinaryPresentationResourcePreflightFailureArmed;
    private RoadRenderToken? _ordinaryPresentationResourcePreflightFailureArmToken;
    private int _ordinaryPresentationResourcePreflightFailureCount;
    private bool _ordinaryNodeBatchFactoryFailureArmed;
    private RoadRenderToken? _ordinaryNodeBatchFactoryFailureArmToken;
    private int _ordinaryNodeBatchFactoryFailureCount;
    private int _ordinaryNodeBatchFactoryFailureMarkerReadCount;

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

        _ordinaryNodeBatchFactoryFailureArmed = true;
        _ordinaryNodeBatchFactoryFailureArmToken = currentToken;
    }

    internal bool IsOrdinaryNodeBatchFactoryFailureArmed() =>
        _ordinaryNodeBatchFactoryFailureArmed;

    internal int GetOrdinaryNodeBatchFactoryFailureCount() =>
        _ordinaryNodeBatchFactoryFailureCount;

    internal int GetOrdinaryNodeBatchFactoryMarkerReadCount() =>
        _ordinaryNodeBatchFactoryFailureMarkerReadCount;

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
