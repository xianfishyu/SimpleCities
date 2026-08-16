using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadToolStateTests
{
    [Fact]
    public void InitialState_IsPlaceAndStreet()
    {
        var state = new RoadToolState();

        Assert.Equal(RoadToolType.Place, state.CurrentTool);
        Assert.Equal(RoadType.Street, state.SelectedRoadType);
    }

    [Fact]
    public void SwitchTo_ChangesTool()
    {
        var state = new RoadToolState();

        state.SwitchTo(RoadToolType.Upgrade);

        Assert.Equal(RoadToolType.Upgrade, state.CurrentTool);
    }

    [Fact]
    public void TrySelectRoadType_ValidType_Succeeds()
    {
        var state = new RoadToolState();

        Assert.True(state.TrySelectRoadType(RoadType.Highway));
        Assert.Equal(RoadType.Highway, state.SelectedRoadType);
    }

    [Fact]
    public void TrySelectRoadType_InvalidType_Fails()
    {
        var state = new RoadToolState();

        Assert.False(state.TrySelectRoadType((RoadType)99));
        Assert.Equal(RoadType.Street, state.SelectedRoadType);
    }

    [Fact]
    public void CaptureRestore_RestoresPreviousState()
    {
        var state = new RoadToolState();
        state.SwitchTo(RoadToolType.Upgrade);
        state.TrySelectRoadType(RoadType.Highway);
        RoadToolStateSnapshot snapshot = state.Capture();

        state.SwitchTo(RoadToolType.Remove);
        state.TrySelectRoadType(RoadType.Dirt);
        state.Restore(snapshot);

        Assert.Equal(RoadToolType.Upgrade, state.CurrentTool);
        Assert.Equal(RoadType.Highway, state.SelectedRoadType);
    }
}
