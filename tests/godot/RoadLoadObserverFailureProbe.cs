using System;

public partial class RoadLoadObserverFailureProbe : Godot.RefCounted
{
    private const string FailureMessage =
        "Injected RoadRenderer presentation observer failure.";

    private RoadRenderer? _renderer;
    private Action<RoadRenderToken>? _handler;
    private int _triggerCount;

    public void Arm(RoadRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        Disarm();
        _renderer = renderer;
        _handler = OnPresentationReady;
        renderer.PresentationReady += _handler;
    }

    public void Disarm()
    {
        if (_renderer is not null && _handler is not null)
            _renderer.PresentationReady -= _handler;
        _renderer = null;
        _handler = null;
    }

    public int GetTriggerCount() => _triggerCount;

    private void OnPresentationReady(RoadRenderToken _)
    {
        _triggerCount++;
        Disarm();
        throw new InvalidOperationException(FailureMessage);
    }
}
