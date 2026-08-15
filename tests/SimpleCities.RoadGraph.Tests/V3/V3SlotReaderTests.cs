using SimpleCities.Core.V3;
using System.Text;

namespace SimpleCities.Tests.V3;

public sealed class V3SlotReaderTests
{
    [Fact]
    public void Read_ReturnsManifestAndPayloads()
    {
        byte[] data = "road-network"u8.ToArray();
        V3Manifest manifest = ValidManifest(data);
        var payloads = new Dictionary<string, byte[]> { [manifest.Files[0].Name] = data };
        IReadOnlyDictionary<string, byte[]> files = V3SlotWriter.BuildFiles(manifest, payloads);

        V3SlotReadResult result = V3SlotReader.Read(files);

        Assert.True(result.Success, result.Error);
        Assert.Equal(manifest.SlotId, result.Manifest!.SlotId);
        Assert.Equal(data, result.Payloads![manifest.Files[0].Name]);
    }

    [Fact]
    public void Read_MissingManifest_Fails()
    {
        var files = new Dictionary<string, byte[]> { ["road_network.json"] = "data"u8.ToArray() };

        V3SlotReadResult result = V3SlotReader.Read(files);

        Assert.False(result.Success);
        Assert.Equal("MissingManifest", result.Error);
    }

    [Fact]
    public void Read_InvalidManifest_Fails()
    {
        var files = new Dictionary<string, byte[]>
        {
            [V3SlotReader.ManifestFileName] = Encoding.UTF8.GetBytes("not json"),
        };

        V3SlotReadResult result = V3SlotReader.Read(files);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Read_DuplicateManifestKey_Fails()
    {
        const string json = """{"formatFamily":"simple-cities-v3","schemaVersion":1,"slotId":"city-001","slotId":"city-002","displayName":"n","timestamp":"2026-08-12T08:00:00.0000000Z","cityName":"n","population":null,"funds":null,"thumbnailFile":null,"files":[]}""";
        var files = new Dictionary<string, byte[]>
        {
            [V3SlotReader.ManifestFileName] = Encoding.UTF8.GetBytes(json),
        };

        V3SlotReadResult result = V3SlotReader.Read(files);

        Assert.False(result.Success);
        Assert.StartsWith("DuplicateKey:", result.Error);
    }

    private static V3Manifest ValidManifest(byte[] data) =>
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
            [new V3ManifestFile("road_network.json", data.LongLength, V3PayloadDigest.ComputeSha256(data))]);
}
