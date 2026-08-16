using SimpleCities.Core.V3;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3ToolLoadParticipantTests
{
    [Fact]
    public void Prepare_WithValidPlan_CanCommit()
    {
        var plan = new RoadToolFullReset(RoadToolType.Upgrade, RoadType.Highway);

        V3ToolLoadParticipant participant = V3ToolLoadParticipant.Prepare(plan);

        Assert.True(participant.IsPrepared);
        Assert.True(participant.CanCommit);
        Assert.Equal(V3ToolLoadParticipant.ParticipantName, V3ToolLoadParticipant.ParticipantName);
    }

    [Fact]
    public void Unprepared_NotPrepared()
    {
        V3ToolLoadParticipant participant = V3ToolLoadParticipant.Unprepared;

        Assert.False(participant.IsPrepared);
        Assert.False(participant.CanCommit);
    }

    [Fact]
    public void Prepare_WithInvalidPlan_NotCanCommit()
    {
        var plan = new RoadToolFullReset(RoadToolType.Place, (RoadType)99);

        V3ToolLoadParticipant participant = V3ToolLoadParticipant.Prepare(plan);

        Assert.True(participant.IsPrepared);
        Assert.False(participant.CanCommit);
    }
}
