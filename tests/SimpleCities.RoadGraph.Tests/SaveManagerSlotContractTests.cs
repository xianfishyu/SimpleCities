using System.Text.Json;

namespace SimpleCities.Tests;

public sealed class SaveManagerSlotContractTests : IDisposable
{
    private readonly string _saveRoot = Path.Combine(
        Path.GetTempPath(),
        $"simple-cities-save-tests-{Guid.NewGuid():N}");

    [Fact]
    public void SaveAndLoad_RoundTripsRegisteredStateAndManifest()
    {
        var store = CreateStore();
        var saveable = new TestSaveable("road_network", 42);

        Assert.Equal(1, store.Save("manual-1", [saveable]));

        string slotDir = Path.Combine(_saveRoot, "manual-1");
        Assert.True(File.Exists(Path.Combine(slotDir, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(slotDir, "road_network.json")));
        ManifestData manifest = SaveManager.ParseAndValidateManifest(
            File.ReadAllText(Path.Combine(slotDir, "manifest.json")));
        Assert.Equal(["road_network.json"], manifest.Files);

        saveable.Value = 7;
        Assert.Equal(1, store.Load("manual-1", [saveable]));

        Assert.Equal(42, saveable.Value);
        Assert.Equal(1, saveable.RestoreCount);
    }

    [Theory]
    [InlineData(MissingSlotPart.Manifest)]
    [InlineData(MissingSlotPart.DataFile)]
    public void Load_MissingRequiredFileDoesNotRestore(MissingSlotPart missingPart)
    {
        var store = CreateStore();
        var saveable = new TestSaveable("road_network", 42);
        Assert.Equal(1, store.Save("broken", [saveable]));
        saveable.Value = 7;

        string slotDir = Path.Combine(_saveRoot, "broken");
        string fileName = missingPart == MissingSlotPart.Manifest
            ? "manifest.json"
            : "road_network.json";
        File.Delete(Path.Combine(slotDir, fileName));

        Assert.ThrowsAny<IOException>(() => store.Load("broken", [saveable]));

        Assert.Equal(7, saveable.Value);
        Assert.Equal(0, saveable.RestoreCount);
    }

    [Fact]
    public void Load_UnsupportedManifestDoesNotRestore()
    {
        var store = CreateStore();
        var saveable = new TestSaveable("road_network", 42);
        Assert.Equal(1, store.Save("broken", [saveable]));
        saveable.Value = 7;
        File.WriteAllText(
            Path.Combine(_saveRoot, "broken", "manifest.json"),
            "{\"schemaVersion\":2,\"files\":[\"road_network.json\"]}");

        Assert.Throws<JsonException>(() => store.Load("broken", [saveable]));

        Assert.Equal(7, saveable.Value);
        Assert.Equal(0, saveable.RestoreCount);
    }

    [Fact]
    public void DeleteSlot_RemovesNonEmptySlotRecursively()
    {
        var store = CreateStore();
        Assert.Equal(1, store.Save("manual-1", [new TestSaveable("road_network", 42)]));

        Assert.True(store.Delete("manual-1"));

        Assert.False(Directory.Exists(Path.Combine(_saveRoot, "manual-1")));
        Assert.False(store.Exists("manual-1"));
        Assert.False(store.Delete("manual-1"));
    }

    [Fact]
    public void Save_CaptureFailureDoesNotPublishSlot()
    {
        var store = CreateStore();

        Assert.Throws<InvalidOperationException>(() =>
            store.Save("broken", [new ThrowingSaveable()]));

        string slotDir = Path.Combine(_saveRoot, "broken");
        Assert.False(store.Exists("broken"));
        Assert.Empty(Directory.GetFiles(slotDir));
    }

    public void Dispose()
    {
        if (Directory.Exists(_saveRoot))
            Directory.Delete(_saveRoot, recursive: true);
    }

    private SaveSlotStore CreateStore() => new(_saveRoot);

    public enum MissingSlotPart
    {
        Manifest,
        DataFile,
    }

    private sealed class TestSaveable(string saveFileName, int value) : ISaveable
    {
        public string SaveFileName { get; } = saveFileName;
        public int Value { get; set; } = value;
        public int RestoreCount { get; private set; }

        public object CaptureState() => new TestState { Value = Value };

        public void RestoreState(string json)
        {
            TestState state = JsonSerializer.Deserialize<TestState>(json)
                ?? throw new JsonException("Test state must be an object.");
            Value = state.Value;
            RestoreCount++;
        }
    }

    private sealed class TestState
    {
        public int Value { get; set; }
    }

    private sealed class ThrowingSaveable : ISaveable
    {
        public string SaveFileName => "broken";

        public object CaptureState() => throw new InvalidOperationException("Capture failed.");

        public void RestoreState(string json) => throw new NotSupportedException();
    }
}
