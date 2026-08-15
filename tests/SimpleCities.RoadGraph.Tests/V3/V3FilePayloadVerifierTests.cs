using System.IO;
using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3FilePayloadVerifierTests
{
    [Fact]
    public void Verify_ValidFile_Succeeds()
    {
        string path = GetTempFile();
        try
        {
            byte[] data = "road-network"u8.ToArray();
            File.WriteAllBytes(path, data);
            var file = new V3ManifestFile("road_network.json", data.LongLength, V3PayloadDigest.ComputeSha256(data));

            V3SameHandleVerificationResult result = V3FilePayloadVerifier.Verify(path, file);

            Assert.True(result.Success);
            Assert.True(result.EndOfFile);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void Verify_CorruptFile_Fails()
    {
        string path = GetTempFile();
        try
        {
            byte[] data = "road-network"u8.ToArray();
            File.WriteAllBytes(path, "other"u8.ToArray());
            var file = new V3ManifestFile("road_network.json", data.LongLength, V3PayloadDigest.ComputeSha256(data));

            V3SameHandleVerificationResult result = V3FilePayloadVerifier.Verify(path, file);

            Assert.False(result.Success);
            Assert.NotNull(result.Error);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void Verify_MissingFile_Fails()
    {
        string path = GetTempFile();
        var file = new V3ManifestFile("road_network.json", 1, new string('a', 64));

        V3SameHandleVerificationResult result = V3FilePayloadVerifier.Verify(path, file);

        Assert.False(result.Success);
        Assert.Equal("FileMissing", result.Error);
    }

    private static string GetTempFile() =>
        Path.Combine(Path.GetTempPath(), $"v3-payload-{Guid.NewGuid():N}.json");

    private static void Cleanup(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // best-effort cleanup
        }
    }
}
