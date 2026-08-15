using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3ManifestTests
{
    [Fact]
    public void Validate_AcceptsValidManifest()
    {
        V3ManifestValidationResult result = V3ManifestValidator.Validate(ValidManifest());

        Assert.True(result.Success, result.Error);
    }

    [Fact]
    public void Validate_RejectsWrongFamily()
    {
        V3Manifest manifest = ValidManifest() with { FormatFamily = "wrong" };

        Assert.Equal("InvalidFormatFamily", V3ManifestValidator.Validate(manifest).Error);
    }

    [Fact]
    public void Validate_RejectsInvalidSlotId()
    {
        V3Manifest manifest = ValidManifest() with { SlotId = "bad/slot" };

        Assert.Equal("InvalidSlotId", V3ManifestValidator.Validate(manifest).Error);
    }

    [Fact]
    public void Validate_RejectsInvalidTimestamp()
    {
        V3Manifest manifest = ValidManifest() with { Timestamp = "2026-08-12 08:00:00Z" };

        Assert.Equal("InvalidTimestamp", V3ManifestValidator.Validate(manifest).Error);
    }

    [Fact]
    public void Validate_RejectsInvalidSha256()
    {
        V3Manifest manifest = ValidManifest() with
        {
            Files = [new V3ManifestFile("road_network.json", 12345, "xyz")],
        };

        Assert.Equal("InvalidSha256", V3ManifestValidator.Validate(manifest).Error);
    }

    [Fact]
    public void Validate_RejectsDuplicateFileNames()
    {
        V3Manifest manifest = ValidManifest() with
        {
            Files =
            [
                new V3ManifestFile("road_network.json", 1, new string('a', 64)),
                new V3ManifestFile("road_network.json", 2, new string('b', 64)),
            ],
        };

        Assert.Equal("InvalidFileName", V3ManifestValidator.Validate(manifest).Error);
    }

    [Fact]
    public void Validate_RejectsNegativePopulation()
    {
        V3Manifest manifest = ValidManifest() with { Population = -1 };

        Assert.Equal("InvalidPopulation", V3ManifestValidator.Validate(manifest).Error);
    }

    [Fact]
    public void Validate_RejectsFundsWithTooManyDecimals()
    {
        V3Manifest manifest = ValidManifest() with { Funds = 1.234m };

        Assert.Equal("InvalidFunds", V3ManifestValidator.Validate(manifest).Error);
    }

    private static V3Manifest ValidManifest() =>
        new(
            V3SaveRoot.FormatFamily,
            V3SaveRoot.SchemaVersion,
            "city-001",
            "河湾城",
            "2026-08-12T08:00:00.0000000Z",
            "河湾城",
            1200,
            50000m,
            null,
            [new V3ManifestFile("road_network.json", 12345, new string('a', 64))]);
}
