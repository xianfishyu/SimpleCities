using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3ManifestStrictReaderTests
{
    [Fact]
    public void Read_ValidManifest_Succeeds()
    {
        V3Manifest manifest = ValidManifest();
        string json = V3ManifestCodec.Serialize(manifest);

        V3ManifestCodecResult result = V3ManifestStrictReader.Read(json);

        Assert.True(result.Success, result.Error);
        Assert.Equal(manifest.SlotId, result.Manifest!.SlotId);
    }

    [Fact]
    public void Read_DuplicateKey_Fails()
    {
        const string json = """
            {
              "formatFamily": "simple-cities-v3",
              "schemaVersion": 1,
              "slotId": "city-001",
              "slotId": "city-002",
              "displayName": "n",
              "timestamp": "2026-08-12T08:00:00.0000000Z",
              "cityName": "n",
              "population": null,
              "funds": null,
              "thumbnailFile": null,
              "files": []
            }
            """;

        V3ManifestCodecResult result = V3ManifestStrictReader.Read(json);

        Assert.False(result.Success);
        Assert.StartsWith("DuplicateKey:", result.Error);
    }

    [Fact]
    public void Read_InvalidManifest_Fails()
    {
        const string json = """{"formatFamily":"wrong","schemaVersion":1,"slotId":"city-001","displayName":"n","timestamp":"2026-08-12T08:00:00.0000000Z","cityName":"n","population":null,"funds":null,"thumbnailFile":null,"files":[]}""";

        V3ManifestCodecResult result = V3ManifestStrictReader.Read(json);

        Assert.False(result.Success);
        Assert.Equal("InvalidFormatFamily", result.Error);
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
            [new V3ManifestFile("road_network.json", 1, new string('a', 64))]);
}
