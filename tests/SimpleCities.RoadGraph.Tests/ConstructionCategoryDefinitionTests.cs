namespace SimpleCities.Tests;

public sealed class ConstructionCategoryDefinitionTests
{
    [Fact]
    public void ToolType_StillContainsKeyboardOnlySelectAndRoadRemove()
    {
        Assert.Equal(
            [ToolType.Select, ToolType.Road, ToolType.RoadRemove],
            Enum.GetValues<ToolType>());
    }

    [Fact]
    public void TryValidate_CityRoadOnlyCatalog_ReturnsTrue()
    {
        Assert.True(ConstructionCategoryDefinition.TryValidate(
            "roads",
            "道路",
            ["city-road"],
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
            ["city-road"],
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

    [Fact]
    public void ConstructionToolDefinition_Icon_IsNullableExportedTextureResource()
    {
        System.Reflection.PropertyInfo? iconProperty = typeof(ConstructionToolDefinition).GetProperty("Icon");
        Assert.NotNull(iconProperty);

        Assert.Equal(typeof(Godot.Texture2D), iconProperty.PropertyType);
        Assert.True(iconProperty.CanRead);
        Assert.True(iconProperty.CanWrite);
        Assert.NotNull(iconProperty.GetCustomAttributes(typeof(Godot.ExportAttribute), inherit: true).SingleOrDefault());
        Assert.Equal(
            System.Reflection.NullabilityState.Nullable,
            new System.Reflection.NullabilityInfoContext().Create(iconProperty).ReadState);
    }

}
