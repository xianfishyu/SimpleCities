using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadToolFullResetTests
{
    [Fact]
    public void Prepare_CapturesToolAndType()
    {
        var state = new RoadToolState();
        state.SwitchTo(RoadToolType.Upgrade);
        state.TrySelectRoadType(RoadType.Highway);

        RoadToolFullReset plan = RoadToolFullReset.Prepare(state);

        Assert.Equal(RoadToolType.Upgrade, plan.CurrentTool);
        Assert.Equal(RoadType.Highway, plan.SelectedRoadType);
        Assert.True(plan.IsValid);
    }

    [Fact]
    public void IsValid_FalseForInvalidRoadType()
    {
        var plan = new RoadToolFullReset(RoadToolType.Place, (RoadType)99);

        Assert.False(plan.IsValid);
    }

    [Fact]
    public void TryApplyTo_AppliesPreservedState()
    {
        var source = new RoadToolState();
        source.SwitchTo(RoadToolType.Remove);
        source.TrySelectRoadType(RoadType.Arterial);
        RoadToolFullReset plan = RoadToolFullReset.Prepare(source);

        var target = new RoadToolState();
        Assert.True(plan.TryApplyTo(target));
        Assert.Equal(RoadToolType.Remove, target.CurrentTool);
        Assert.Equal(RoadType.Arterial, target.SelectedRoadType);
    }

    [Fact]
    public void TryApplyTo_InvalidType_FailsWithoutChanging()
    {
        var plan = new RoadToolFullReset(RoadToolType.Place, (RoadType)99);
        var target = new RoadToolState();
        target.SwitchTo(RoadToolType.Upgrade);
        target.TrySelectRoadType(RoadType.Street);

        Assert.False(plan.TryApplyTo(target));
        Assert.Equal(RoadToolType.Upgrade, target.CurrentTool);
        Assert.Equal(RoadType.Street, target.SelectedRoadType);
    }

    [Fact]
    public void TryApplyTo_NullTarget_Throws()
    {
        var plan = RoadToolFullReset.Prepare(new RoadToolState());

        Assert.Throws<System.ArgumentNullException>(() => plan.TryApplyTo(null!));
    }
}
