using Godot;
using System;

public partial class RoadRendererUpdateTokenFailureProbe : RefCounted
{
    private RoadRenderer? _renderer;

    public void Arm(RoadRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.ArmNextOrdinaryPresentationResourcePreflightFailure();
        _renderer = renderer;
    }

    public bool IsArmed() =>
        _renderer?.IsOrdinaryPresentationResourcePreflightFailureArmed() ?? false;

    public int GetTriggerCount() =>
        _renderer?.GetOrdinaryPresentationResourcePreflightFailureCount() ?? 0;

    public string GetFailureMessage() =>
        RoadRenderer.OrdinaryPresentationResourcePreflightFailureMessage;

    public long GetObjectResourceCount() => Convert.ToInt64(
        Performance.GetMonitor(Performance.Monitor.ObjectResourceCount));
}

public partial class RoadRenderer
{
    internal const string OrdinaryPresentationResourcePreflightFailureMessage =
        "Injected ordinary road presentation resource preflight failure.";

    private bool _ordinaryPresentationResourcePreflightFailureArmed;
    private RoadRenderToken? _ordinaryPresentationResourcePreflightFailureArmToken;
    private int _ordinaryPresentationResourcePreflightFailureCount;

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

        _ordinaryPresentationResourcePreflightFailureArmed = true;
        _ordinaryPresentationResourcePreflightFailureArmToken = currentToken;
    }

    internal bool IsOrdinaryPresentationResourcePreflightFailureArmed() =>
        _ordinaryPresentationResourcePreflightFailureArmed;

    internal int GetOrdinaryPresentationResourcePreflightFailureCount() =>
        _ordinaryPresentationResourcePreflightFailureCount;
}
