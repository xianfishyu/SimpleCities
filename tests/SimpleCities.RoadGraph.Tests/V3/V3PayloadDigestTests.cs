using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3PayloadDigestTests
{
    [Fact]
    public void ComputeSha256_Returns64LowercaseHex()
    {
        string hash = V3PayloadDigest.ComputeSha256("hello"u8.ToArray());

        Assert.Equal(64, hash.Length);
        foreach (char c in hash)
            Assert.True((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
    }

    [Fact]
    public void Matches_ReturnsTrueForMatchingData()
    {
        byte[] data = "road-network"u8.ToArray();
        var file = new V3ManifestFile("road_network.json", data.Length, V3PayloadDigest.ComputeSha256(data));

        Assert.True(V3PayloadDigest.Matches(file, data));
    }

    [Fact]
    public void Matches_RejectsWrongLength()
    {
        byte[] data = "road-network"u8.ToArray();
        var file = new V3ManifestFile("road_network.json", data.Length + 1, V3PayloadDigest.ComputeSha256(data));

        Assert.False(V3PayloadDigest.Matches(file, data));
    }

    [Fact]
    public void Matches_RejectsWrongHash()
    {
        byte[] data = "road-network"u8.ToArray();
        var file = new V3ManifestFile("road_network.json", data.Length, new string('0', 64));

        Assert.False(V3PayloadDigest.Matches(file, data));
    }
}
