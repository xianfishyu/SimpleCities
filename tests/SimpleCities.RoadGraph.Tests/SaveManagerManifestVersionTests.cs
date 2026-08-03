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
}
