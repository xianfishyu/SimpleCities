using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3ManifestBuilderTests
{
    [Fact]
    public void Create_BuildsValidManifestWithComputedDigests()
    {
        byte[] data = "road-network"u8.ToArray();
        var payloads = new[] { new KeyValuePair<string, byte[]>("road_network.json", data) };

        V3Manifest manifest = V3ManifestBuilder.Create(
            "city-001",
            "河湾城",
            "河湾城",
            "2026-08-12T08:00:00.0000000Z",
            1200,
            50000m,
            null,
            payloads);

        Assert.True(V3ManifestValidator.Validate(manifest).Success);
        Assert.Equal(data.LongLength, manifest.Files[0].EncodedLength);
        Assert.Equal(V3PayloadDigest.ComputeSha256(data), manifest.Files[0].Sha256);
    }

    [Fact]
    public void Create_SortsFilesByName()
    {
        var payloads = new[]
        {
            new KeyValuePair<string, byte[]>("b.json", "b"u8.ToArray()),
            new KeyValuePair<string, byte[]>("a.json", "a"u8.ToArray()),
        };

        V3Manifest manifest = V3ManifestBuilder.Create("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null, payloads);

        Assert.Equal(new[] { "a.json", "b.json" }, manifest.Files.Select(file => file.Name).ToArray());
    }

    [Fact]
    public void Create_ThrowsOnNullPayloads()
    {
        Assert.Throws<ArgumentNullException>(() =>
            V3ManifestBuilder.Create("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null, null!));
    }
}
