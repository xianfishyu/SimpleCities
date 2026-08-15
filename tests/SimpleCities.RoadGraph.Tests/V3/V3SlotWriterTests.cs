using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3SlotWriterTests
{
    [Fact]
    public void BuildFiles_IncludesManifestAndPayload()
    {
        byte[] data = "road-network"u8.ToArray();
        V3Manifest manifest = ValidManifest(data);
        var payloads = new Dictionary<string, byte[]> { [manifest.Files[0].Name] = data };

        IReadOnlyDictionary<string, byte[]> files = V3SlotWriter.BuildFiles(manifest, payloads);

        Assert.True(files.ContainsKey(V3SlotWriter.ManifestFileName));
        Assert.Equal(data, files[manifest.Files[0].Name]);
        Assert.Equal(2, files.Count);
    }

    [Fact]
    public void BuildFiles_ThrowsOnMissingPayload()
    {
        byte[] data = "road-network"u8.ToArray();
        V3Manifest manifest = ValidManifest(data);

        Assert.Throws<ArgumentException>(() => V3SlotWriter.BuildFiles(manifest, new Dictionary<string, byte[]>()));
    }

    [Fact]
    public void BuildFiles_ThrowsOnUndeclaredPayload()
    {
        byte[] data = "road-network"u8.ToArray();
        V3Manifest manifest = ValidManifest(data);
        var payloads = new Dictionary<string, byte[]>
        {
            [manifest.Files[0].Name] = data,
            ["extra.json"] = data,
        };

        Assert.Throws<ArgumentException>(() => V3SlotWriter.BuildFiles(manifest, payloads));
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
