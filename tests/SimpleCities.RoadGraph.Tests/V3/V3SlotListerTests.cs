using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3SlotListerTests
{
    [Fact]
    public void List_ReturnsSortedSummaries()
    {
        var children = new Dictionary<string, V3SlotOccupant>
        {
            ["b"] = V3SlotOccupant.CorruptV3,
            ["a"] = V3SlotOccupant.CompleteV3,
        };

        IReadOnlyList<V3SlotSummary> result = V3SlotLister.List(children);

        Assert.Equal(new[] { "a", "b" }, result.Select(summary => summary.SlotId).ToArray());
        Assert.Equal(V3SlotOccupant.CompleteV3, result[0].Occupant);
    }

    [Fact]
    public void List_IncludesAllChildren()
    {
        var children = new Dictionary<string, V3SlotOccupant>
        {
            ["city-001"] = V3SlotOccupant.CompleteV3,
            ["foreign"] = V3SlotOccupant.Foreign,
            ["unsafe"] = V3SlotOccupant.Unsafe,
        };

        IReadOnlyList<V3SlotSummary> result = V3SlotLister.List(children);

        Assert.Equal(3, result.Count);
    }
}
