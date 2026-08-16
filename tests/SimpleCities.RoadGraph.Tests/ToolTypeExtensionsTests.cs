using SimpleCities.Road.V3;

namespace SimpleCities.Tests;

public sealed class ToolTypeExtensionsTests
{
    [Theory]
    [InlineData(ToolType.Select, RoadToolType.Select)]
    [InlineData(ToolType.Road, RoadToolType.Place)]
    [InlineData(ToolType.RoadRemove, RoadToolType.Remove)]
    [InlineData(ToolType.RoadUpgrade, RoadToolType.Upgrade)]
    public void ToRoadToolType_MapsStableToolTypes(ToolType tool, RoadToolType expected)
    {
        Assert.Equal(expected, tool.ToRoadToolType());
    }
}
