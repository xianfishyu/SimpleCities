using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3SameHandleVerifierTests
{
    [Fact]
    public void Verify_ReturnsSuccessWithEofWhenMatching()
    {
        byte[] data = "road-network"u8.ToArray();
        var file = new V3ManifestFile("road_network.json", data.LongLength, V3PayloadDigest.ComputeSha256(data));

        V3SameHandleVerificationResult result = V3SameHandleVerifier.Verify(file, data);

        Assert.True(result.Success);
        Assert.True(result.EndOfFile);
        Assert.Equal(data.LongLength, result.ConsumedBytes);
    }

    [Fact]
    public void Verify_ReturnsLengthMismatch()
    {
        byte[] data = "road-network"u8.ToArray();
        var file = new V3ManifestFile("road_network.json", data.LongLength + 1, V3PayloadDigest.ComputeSha256(data));

        V3SameHandleVerificationResult result = V3SameHandleVerifier.Verify(file, data);

        Assert.False(result.Success);
        Assert.False(result.EndOfFile);
        Assert.Equal("LengthMismatch", result.Error);
    }

    [Fact]
    public void Verify_ReturnsDigestMismatch()
    {
        byte[] data = "road-network"u8.ToArray();
        var file = new V3ManifestFile("road_network.json", data.LongLength, new string('0', 64));

        V3SameHandleVerificationResult result = V3SameHandleVerifier.Verify(file, data);

        Assert.False(result.Success);
        Assert.False(result.EndOfFile);
        Assert.Equal("DigestMismatch", result.Error);
    }
}
