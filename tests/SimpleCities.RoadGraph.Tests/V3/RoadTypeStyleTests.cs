using Godot;
using SimpleCities.Road.V3;
using System;

namespace SimpleCities.Tests.V3;

public sealed class RoadTypeStyleTests
{
    [Fact]
    public void Validate_ValidStyle_Succeeds()
    {
        RoadTypeStyle style = CreateStyle(RoadType.Street, "街道", Colors.White, 1f);

        Assert.True(style.Validate(out string? error), error);
    }

    [Fact]
    public void Validate_InvalidDisplayName_Fails()
    {
        RoadTypeStyle style = CreateStyle(RoadType.Street, " ", Colors.White, 1f);

        Assert.False(style.Validate(out string? error));
        Assert.Equal("InvalidDisplayName", error);
    }

    [Fact]
    public void Validate_NonPositiveWidth_Fails()
    {
        RoadTypeStyle style = CreateStyle(RoadType.Street, "街道", Colors.White, 0f);

        Assert.False(style.Validate(out string? error));
        Assert.Equal("InvalidWidth", error);
    }

    [Fact]
    public void Catalog_Create_AllFour_Succeeds()
    {
        RoadTypeStyleCatalogResult result = RoadTypeStyleCatalog.Create(CreateAllStyles());

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.Styles);
        Assert.Equal(4, result.Styles!.Count);
    }

    [Fact]
    public void Catalog_CreateDefault_Succeeds()
    {
        RoadTypeStyleCatalogResult result = RoadTypeStyleCatalog.CreateDefault();

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.Styles);
        Assert.Equal(4, result.Styles!.Count);
        Assert.All(Enum.GetValues<RoadType>(), type => Assert.Contains(type, result.Styles!.Keys));
    }

    [Fact]
    public void Catalog_MissingType_Fails()
    {
        RoadTypeStyleCatalogResult result = RoadTypeStyleCatalog.Create(
            [
                CreateStyle(RoadType.Dirt, "土路", Colors.Brown, 1f),
                CreateStyle(RoadType.Street, "街道", Colors.White, 1f),
                CreateStyle(RoadType.Arterial, "主干道", Colors.Yellow, 1f),
            ]);

        Assert.False(result.Success);
        Assert.Equal("MissingRoadType:Highway", result.Error);
    }

    [Fact]
    public void Catalog_DuplicateType_Fails()
    {
        RoadTypeStyleCatalogResult result = RoadTypeStyleCatalog.Create(
            [
                CreateStyle(RoadType.Dirt, "土路", Colors.Brown, 1f),
                CreateStyle(RoadType.Street, "街道", Colors.White, 1f),
                CreateStyle(RoadType.Arterial, "主干道", Colors.Yellow, 1f),
                CreateStyle(RoadType.Highway, "高速", Colors.Red, 1f),
                CreateStyle(RoadType.Highway, "高速二", Colors.Red, 1f),
            ]);

        Assert.False(result.Success);
        Assert.StartsWith("DuplicateRoadType:", result.Error);
    }

    [Fact]
    public void Catalog_InvalidStyle_Fails()
    {
        RoadTypeStyleCatalogResult result = RoadTypeStyleCatalog.Create(
            [
                CreateStyle(RoadType.Dirt, "土路", Colors.Brown, 1f),
                CreateStyle(RoadType.Street, "街道", Colors.White, 1f),
                CreateStyle(RoadType.Arterial, "主干道", Colors.Yellow, 1f),
                CreateStyle(RoadType.Highway, "高速", Colors.Red, 0f),
            ]);

        Assert.False(result.Success);
        Assert.Equal("InvalidWidth", result.Error);
    }

    private static RoadTypeStyle[] CreateAllStyles() =>
        [
            CreateStyle(RoadType.Dirt, "土路", Colors.Brown, 1f),
            CreateStyle(RoadType.Street, "街道", Colors.White, 1f),
            CreateStyle(RoadType.Arterial, "主干道", Colors.Yellow, 1f),
            CreateStyle(RoadType.Highway, "高速", Colors.Red, 1f),
        ];

    private static RoadTypeStyle CreateStyle(
        RoadType roadType,
        string displayName,
        Color color,
        float width) =>
        new()
        {
            RoadType = roadType,
            DisplayName = displayName,
            Color = color,
            Width = width,
        };
}
