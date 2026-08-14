using Godot;

namespace SimpleCities.Tests;

public sealed class RoadTypeStyleTests
{
    private static readonly RoadTypeStyleDefinition[] ValidStyles =
    [
        Style(RoadType.Dirt, "Dirt", "#8A6652", 14f),
        Style(RoadType.Street, "Street", "#60727C", 20f),
        Style(RoadType.Arterial, "Arterial", "#D7A928", 26f),
        Style(RoadType.Highway, "Highway", "#C84B3A", 32f),
    ];

    public static TheoryData<float> InvalidWidths => new()
    {
        0f,
        RoadNumericPolicy.MinimumDisplayRoadWidth * 0.5f,
        -1f,
        float.NaN,
        float.PositiveInfinity,
        float.NegativeInfinity,
        RoadNumericPolicy.MaximumDisplayRoadWidth + 1f,
    };

    public static TheoryData<Color> InvalidColors => new()
    {
        new Color(float.NaN, 0f, 0f, 1f),
        new Color(0f, float.PositiveInfinity, 0f, 1f),
        new Color(0f, 0f, float.NegativeInfinity, 1f),
        new Color(1f, 1f, 1f, float.NaN),
        new Color(1f, 1f, 1f, 0f),
        new Color(1f, 1f, 1f, -0.1f),
    };

    [Fact]
    public void ResourceShape_ExportsOnlyTheV3FirstVersionStyleFields()
    {
        Assert.True(typeof(RoadTypeStyle).IsSubclassOf(typeof(Resource)));
        Assert.NotNull(typeof(RoadTypeStyle).GetCustomAttributes(typeof(GlobalClassAttribute), true).SingleOrDefault());

        AssertExportedProperty(nameof(RoadTypeStyle.RoadType), typeof(RoadType));
        AssertExportedProperty(nameof(RoadTypeStyle.DisplayName), typeof(string));
        AssertExportedProperty(nameof(RoadTypeStyle.Color), typeof(Color));
        AssertExportedProperty(nameof(RoadTypeStyle.Width), typeof(float));

        System.Reflection.PropertyInfo? stylesProperty = typeof(RoadConfig).GetProperty(nameof(RoadConfig.RoadTypeStyles));
        Assert.NotNull(stylesProperty);
        Assert.Equal(typeof(Godot.Collections.Array<RoadTypeStyle>), stylesProperty.PropertyType);
        Assert.NotNull(stylesProperty.GetCustomAttributes(typeof(ExportAttribute), true).SingleOrDefault());
    }

    [Fact]
    public void TryValidateRoadTypeStyles_ExactUniqueCoverage_ReturnsTrue()
    {
        Assert.True(RoadConfig.TryValidateRoadTypeStyles(ValidStyles, out string error), error);
    }

    [Fact]
    public void TryValidateRoadTypeStyles_NullCollection_ReturnsFalse()
    {
        Assert.False(RoadConfig.TryValidateRoadTypeStyles(null, out string error));
        Assert.Contains("cannot be null", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidateRoadTypeStyles_MissingEntry_ReturnsFalse()
    {
        Assert.False(RoadConfig.TryValidateRoadTypeStyles(ValidStyles[..^1], out string error));
        Assert.Contains("exactly 4", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryValidateRoadTypeStyles_DuplicateEntry_ReturnsFalse()
    {
        RoadTypeStyleDefinition[] styles = (RoadTypeStyleDefinition[])ValidStyles.Clone();
        styles[^1] = styles[0] with { DisplayName = "Duplicate dirt" };

        Assert.False(RoadConfig.TryValidateRoadTypeStyles(styles, out string error));
        Assert.Contains("duplicate", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(nameof(RoadType.Dirt), error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryValidateRoadTypeStyles_UndefinedEnum_ReturnsFalse()
    {
        RoadTypeStyleDefinition[] styles = (RoadTypeStyleDefinition[])ValidStyles.Clone();
        styles[^1] = styles[^1] with { RoadType = (RoadType)99 };

        Assert.False(RoadConfig.TryValidateRoadTypeStyles(styles, out string error));
        Assert.Contains("99", error, StringComparison.Ordinal);
        Assert.Contains("not defined", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryValidateRoadTypeStyles_EmptyDisplayName_ReturnsFalse(string displayName)
    {
        RoadTypeStyleDefinition[] styles = (RoadTypeStyleDefinition[])ValidStyles.Clone();
        styles[1] = styles[1] with { DisplayName = displayName };

        Assert.False(RoadConfig.TryValidateRoadTypeStyles(styles, out string error));
        Assert.Contains("display name", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(InvalidColors))]
    public void TryValidateRoadTypeStyles_NonFiniteOrTransparentColor_ReturnsFalse(Color color)
    {
        RoadTypeStyleDefinition[] styles = (RoadTypeStyleDefinition[])ValidStyles.Clone();
        styles[2] = styles[2] with { Color = color };

        Assert.False(RoadConfig.TryValidateRoadTypeStyles(styles, out string error));
        Assert.Contains("color", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(InvalidWidths))]
    public void TryValidateRoadTypeStyles_OutOfRangeOrNonFiniteWidth_ReturnsFalse(float width)
    {
        RoadTypeStyleDefinition[] styles = (RoadTypeStyleDefinition[])ValidStyles.Clone();
        styles[3] = styles[3] with { Width = width };

        Assert.False(RoadConfig.TryValidateRoadTypeStyles(styles, out string error));
        Assert.Contains("width", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveRoadTypeStyle_ShuffledInput_ReturnsTheUniqueRequestedStyle()
    {
        RoadTypeStyleDefinition[] shuffled =
        [
            ValidStyles[2],
            ValidStyles[0],
            ValidStyles[3],
            ValidStyles[1],
        ];

        foreach (RoadTypeStyleDefinition expected in ValidStyles)
        {
            Assert.Equal(
                expected,
                RoadConfig.ResolveRoadTypeStyle(shuffled, expected.RoadType));
        }
    }

    [Fact]
    public void ResolveRoadTypeStyle_InvalidConfigOrTarget_ThrowsInsteadOfFallingBack()
    {
        Assert.Throws<InvalidOperationException>(() =>
            RoadConfig.ResolveRoadTypeStyle(ValidStyles[..^1], RoadType.Street));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RoadConfig.ResolveRoadTypeStyle(ValidStyles, (RoadType)99));
    }

    [Fact]
    public void RoadRenderer_ReportsInvalidRoadTypeStylesDuringStartup()
    {
        string rendererSource = File.ReadAllText(Path.Combine(
            FindProjectRoot(),
            "Scripts",
            "Road",
            "RoadRenderer.cs"));

        Assert.Contains("Config.TryValidateRoadTypeStyles", rendererSource, StringComparison.Ordinal);
        Assert.Contains(
            "RoadRenderer: RoadTypeStyles resource is invalid:",
            rendererSource,
            StringComparison.Ordinal);
        Assert.Contains("GD.PushError", rendererSource, StringComparison.Ordinal);
    }

    private static RoadTypeStyleDefinition Style(
        RoadType roadType,
        string displayName,
        string color,
        float width) =>
        new(roadType, displayName, new Color(color), width);

    private static void AssertExportedProperty(string name, Type expectedType)
    {
        System.Reflection.PropertyInfo? property = typeof(RoadTypeStyle).GetProperty(name);
        Assert.NotNull(property);
        Assert.Equal(expectedType, property.PropertyType);
        Assert.NotNull(property.GetCustomAttributes(typeof(ExportAttribute), true).SingleOrDefault());
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "SimpleCities.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName
            ?? throw new DirectoryNotFoundException("SimpleCities project root was not found.");
    }
}
