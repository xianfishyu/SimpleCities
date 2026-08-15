using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3PayloadBudgetTests
{
    [Fact]
    public void Default_AllowsReasonableCountsAndBytes()
    {
        V3PayloadBudget budget = V3PayloadBudget.Default;

        Assert.True(budget.AllowsCounts(1000, 2000, 5000));
        Assert.True(budget.AllowsBytes(1024, 1024 * 1024, 2 * 1024 * 1024));
    }

    [Fact]
    public void AllowsCounts_RejectsNegativeOrExceeded()
    {
        V3PayloadBudget budget = V3PayloadBudget.Default;

        Assert.False(budget.AllowsCounts(-1, 0, 0));
        Assert.False(budget.AllowsCounts(budget.MaxNodes + 1, 0, 0));
        Assert.False(budget.AllowsCounts(0, budget.MaxEdges + 1, 0));
        Assert.False(budget.AllowsCounts(0, 0, budget.MaxGeometrySegments + 1));
    }

    [Fact]
    public void AllowsBytes_RejectsNegativeOrExceeded()
    {
        V3PayloadBudget budget = V3PayloadBudget.Default;

        Assert.False(budget.AllowsBytes(-1, 0, 0));
        Assert.False(budget.AllowsBytes(budget.MaxManifestBytes + 1, 0, 0));
        Assert.False(budget.AllowsBytes(0, budget.MaxPayloadBytes + 1, 0));
        Assert.False(budget.AllowsBytes(0, 0, budget.MaxSlotTotalBytes + 1));
    }
}
