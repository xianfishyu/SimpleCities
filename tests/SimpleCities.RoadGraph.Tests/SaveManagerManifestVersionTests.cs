using System.Text.Json;

namespace SimpleCities.Tests;

public sealed class SaveManagerManifestVersionTests
{
    [Fact]
    public void ParseAndValidateManifest_CurrentVersionIsAccepted()
    {
        ManifestData manifest = SaveManager.ParseAndValidateManifest("""
            {
              "schemaVersion": 1,
              "slotId": "autosave",
              "displayName": "Autosave",
              "timestamp": "2026-08-04T00:00:00Z",
              "cityName": "My City",
              "files": ["road_network.json"]
            }
            """);

        Assert.Equal(1, manifest.SchemaVersion);
        Assert.Equal("autosave", manifest.SlotID);
        Assert.Equal("Autosave", manifest.DisplayName);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":0}")]
    [InlineData("{\"schemaVersion\":2}")]
    [InlineData("{\"schemaVersion\":\"1\"}")]
    [InlineData("{\"SchemaVersion\":1}")]
    public void ParseAndValidateManifest_UnsupportedVersionIsRejected(string json)
    {
        Assert.Throws<JsonException>(() => SaveManager.ParseAndValidateManifest(json));
    }

    [Theory]
    [InlineData("2026-08-04T08:00:00+08:00", "road_network.json")]
    [InlineData("not-a-time", "road_network.json")]
    [InlineData("2026-08-04T00:00:00Z", "../road_network.json")]
    [InlineData("2026-08-04T00:00:00Z", "road_network.txt")]
    [InlineData("2026-08-04T00:00:00Z", "Manifest.json")]
    public void ParseAndValidateManifest_InvalidMetadataIsRejected(string timestamp, string fileName)
    {
        string json = $$"""
            {
              "schemaVersion": 1,
              "slotId": "autosave",
              "displayName": "Autosave",
              "timestamp": "{{timestamp}}",
              "cityName": "Unknown City",
              "files": ["{{fileName}}"]
            }
            """;

        Assert.Throws<JsonException>(() => SaveManager.ParseAndValidateManifest(json));
    }

    [Fact]
    public void ParseAndValidateManifest_DuplicateFilesAreRejected()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "slotId": "autosave",
              "displayName": "Autosave",
              "timestamp": "2026-08-04T00:00:00Z",
              "cityName": "Unknown City",
              "files": ["road_network.json", "road_network.json"]
            }
            """;

        Assert.Throws<JsonException>(() => SaveManager.ParseAndValidateManifest(json));
    }

    [Fact]
    public void ParseAndValidateManifest_CaseInsensitiveDuplicateFilesAreRejected()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "slotId": "autosave",
              "displayName": "Autosave",
              "timestamp": "2026-08-04T00:00:00Z",
              "cityName": "Unknown City",
              "files": ["Economy.json", "economy.json"]
            }
            """;

        Assert.Throws<JsonException>(() => SaveManager.ParseAndValidateManifest(json));
    }
}
