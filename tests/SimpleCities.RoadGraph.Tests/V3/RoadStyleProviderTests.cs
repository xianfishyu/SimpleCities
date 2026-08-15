using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadStyleProviderTests
{
    [Fact]
    public void Get_ReturnsRegisteredStyle()
    {
        RoadStyleProvider provider = CreateProvider();

        RoadTypeStyle style = provider.Get(RoadType.Highway);

        Assert.Equal("高速", style.DisplayName);
    }

    [Fact]
    public void TryGet_ExistingType_ReturnsTrue()
    {
        RoadStyleProvider provider = CreateProvider();

        Assert.True(provider.TryGet(RoadType.Street, out RoadTypeStyle? style));
        Assert.Equal("街道", style.DisplayName);
    }

    [Fact]
    public void TryGet_MissingType_ReturnsFalse()
    {
        RoadStyleProvider provider = CreateProvider();

        Assert.False(provider.TryGet((RoadType)99, out _));
    }

    private static RoadStyleProvider CreateProvider()
    {
        RoadTypeStyleCatalogResult catalog = RoadTypeStyleCatalog.Create(
            [
                new RoadTypeStyle { RoadType = RoadType.Dirt, DisplayName = "土路", Color = Colors.Brown, Width = 1f },
                new RoadTypeStyle { RoadType = RoadType.Street, DisplayName = "街道", Color = Colors.White, Width = 1f },
                new RoadTypeStyle { RoadType = RoadType.Arterial, DisplayName = "主干道", Color = Colors.Yellow, Width = 1.5f },
                new RoadTypeStyle { RoadType = RoadType.Highway, DisplayName = "高速", Color = Colors.Red, Width = 2f },
            ]);
        return new RoadStyleProvider(catalog);
    }
}
