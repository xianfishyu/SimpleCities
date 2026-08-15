using System.IO;
using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3ManifestStrictFileReaderTests
{
    [Fact]
    public void Read_ValidFile_Succeeds()
    {
        string path = GetTempFile();
        try
        {
            V3Manifest manifest = ValidManifest();
            File.WriteAllText(path, V3ManifestCodec.Serialize(manifest));

            V3ManifestCodecResult result = V3ManifestStrictFileReader.Read(path);

            Assert.True(result.Success, result.Error);
            Assert.Equal(manifest.SlotId, result.Manifest!.SlotId);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void Read_MissingFile_Fails()
    {
        V3ManifestCodecResult result = V3ManifestStrictFileReader.Read(GetTempFile());

        Assert.False(result.Success);
        Assert.Equal("FileMissing", result.Error);
    }

    [Fact]
    public void Read_DuplicateKeyFile_Fails()
    {
        string path = GetTempFile();
        try
        {
            const string json = """{"formatFamily":"simple-cities-v3","schemaVersion":1,"slotId":"city-001","slotId":"city-002","displayName":"n","timestamp":"2026-08-12T08:00:00.0000000Z","cityName":"n","population":null,"funds":null,"thumbnailFile":null,"files":[]}""";
            File.WriteAllText(path, json);

            V3ManifestCodecResult result = V3ManifestStrictFileReader.Read(path);

            Assert.False(result.Success);
            Assert.StartsWith("DuplicateKey:", result.Error);
        }
        finally
        {
            Cleanup(path);
        }
    }

    private static string GetTempFile() =>
        Path.Combine(Path.GetTempPath(), $"v3-manifest-{Guid.NewGuid():N}.json");

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
