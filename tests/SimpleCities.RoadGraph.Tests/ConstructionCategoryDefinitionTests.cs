namespace SimpleCities.Tests;

public sealed class ConstructionCategoryDefinitionTests
{
    [Fact]
    public void ToolType_ContainsExactRoadsCatalogTools()
    {
        Assert.Equal(
            [ToolType.Select, ToolType.Road, ToolType.RoadRemove],
            Enum.GetValues<ToolType>());
    }

    [Fact]
    public void TryValidate_ExactRoadToolIds_ReturnsTrue()
    {
        Assert.True(ConstructionCategoryDefinition.TryValidate(
            "roads",
            "Roads",
            ["select", "road", "road-remove"],
            out string error), error);
    }

    [Theory]
    [InlineData("", "Roads")]
    [InlineData("roads", "")]
    public void TryValidate_EmptyCategoryIdentity_ReturnsFalse(string id, string displayName)
    {
        Assert.False(ConstructionCategoryDefinition.TryValidate(
            id,
            displayName,
            ["select", "road", "road-remove"],
            out string error));
        Assert.NotEmpty(error);
    }

    [Theory]
    [InlineData("", "Select")]
    [InlineData("select", "")]
    public void TryValidate_EmptyToolIdentity_ReturnsFalse(string id, string displayName)
    {
        Assert.False(ConstructionToolDefinition.TryValidate(id, displayName, out string error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void TryValidate_DuplicateToolIds_ReturnsFalse()
    {
        Assert.False(ConstructionCategoryDefinition.TryValidate(
            "roads",
            "Roads",
            ["select", "road", "road"],
            out string error));
        Assert.Contains("road", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryValidate_NullToolCollection_ReturnsFalse()
    {
        Assert.False(ConstructionCategoryDefinition.TryValidate(
            "roads",
            "道路",
            null,
            out string error));
        Assert.Contains("tools array", error, StringComparison.OrdinalIgnoreCase);
    }

}
