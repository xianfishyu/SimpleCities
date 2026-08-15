using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3ManifestCodecTests
{
    [Fact]
    public void Serialize_ThenDeserialize_RoundTripsManifest()
    {
        V3Manifest manifest = ValidManifest();

        string json = V3ManifestCodec.Serialize(manifest);
        V3ManifestCodecResult result = V3ManifestCodec.Deserialize(json);

        Assert.True(result.Success, result.Error);
        Assert.Equal(manifest.SlotId, result.Manifest!.SlotId);
        Assert.Equal(manifest.DisplayName, result.Manifest.DisplayName);
        Assert.Equal(manifest.Files[0].Sha256, result.Manifest.Files[0].Sha256);
    }

    [Fact]
    public void Deserialize_RejectsInvalidJson()
    {
        V3ManifestCodecResult result = V3ManifestCodec.Deserialize("not json");

        Assert.False(result.Success);
        Assert.Equal("MalformedJson", result.Error);
    }

    [Fact]
    public void Deserialize_RejectsInvalidManifest()
    {
        const string json = """
            {
              "formatFamily": "wrong",
              "schemaVersion": 1,
              "slotId": "city-001",
              "displayName": "河湾城",
              "timestamp": "2026-08-12T08:00:00.0000000Z",
              "cityName": "河湾城",
              "population": 1200,
              "funds": 50000,
              "thumbnailFile": null,
              "files": []
            }
            """;

        V3ManifestCodecResult result = V3ManifestCodec.Deserialize(json);

        Assert.False(result.Success);
        Assert.Equal("InvalidFormatFamily", result.Error);
    }

    [Fact]
    public void Serialize_UsesCamelCaseFieldNames()
    {
        string json = V3ManifestCodec.Serialize(ValidManifest());

        Assert.Contains("\"formatFamily\"", json, StringComparison.Ordinal);
        Assert.Contains("\"schemaVersion\"", json, StringComparison.Ordinal);
        Assert.Contains("\"displayName\"", json, StringComparison.Ordinal);
        Assert.Contains("\"encodedLength\"", json, StringComparison.Ordinal);
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
