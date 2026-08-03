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

        Assert.Equal(1, store.Save("manual-1", "第一座城市", [saveable]));

        string slotDir = Path.Combine(_saveRoot, "manual-1");
        Assert.True(File.Exists(Path.Combine(slotDir, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(slotDir, "road_network.json")));
        ManifestData manifest = SaveManager.ParseAndValidateManifest(
            File.ReadAllText(Path.Combine(slotDir, "manifest.json")));
        Assert.Equal("manual-1", manifest.SlotID);
        Assert.Equal("第一座城市", manifest.DisplayName);
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
        Assert.Equal(1, store.Save("broken", "Broken", [saveable]));
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
        Assert.Equal(1, store.Save("broken", "Broken", [saveable]));
        saveable.Value = 7;
        File.WriteAllText(
            Path.Combine(_saveRoot, "broken", "manifest.json"),
            "{\"schemaVersion\":2,\"slotId\":\"broken\",\"displayName\":\"Broken\",\"files\":[\"road_network.json\"]}");

        Assert.Throws<JsonException>(() => store.Load("broken", [saveable]));

        Assert.Equal(7, saveable.Value);
        Assert.Equal(0, saveable.RestoreCount);
    }

    [Fact]
    public void DeleteSlot_RemovesNonEmptySlotRecursively()
    {
        var store = CreateStore();
        Assert.Equal(1, store.Save("manual-1", "Manual 1", [new TestSaveable("road_network", 42)]));

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
            store.Save("broken", "Broken", [new ThrowingSaveable()]));

        string slotDir = Path.Combine(_saveRoot, "broken");
        Assert.False(store.Exists("broken"));
        Assert.Empty(Directory.GetFiles(slotDir));
    }

    [Fact]
    public void Create_StoresFreeFormDisplayNamesUnderDistinctSafeIDs()
    {
        var store = CreateStore();
        var saveable = new TestSaveable("road_network", 42);
        const string displayName = "同名城市 / 夏季: 2026";

        string firstID = store.Create(displayName, [saveable]);
        string secondID = store.Create(displayName, [saveable]);

        Assert.NotEqual(firstID, secondID);
        Assert.Matches("^manual-[0-9a-f]{32}$", firstID);
        Assert.Matches("^manual-[0-9a-f]{32}$", secondID);
        Assert.Equal(displayName, store.ReadManifest(firstID).DisplayName);
        Assert.Equal(displayName, store.ReadManifest(secondID).DisplayName);
        Assert.Equal(
            Path.GetFullPath(_saveRoot),
            Directory.GetParent(Path.Combine(_saveRoot, firstID))!.FullName);
    }

    [Fact]
    public void Create_AcceptsMaximumDisplayNameLength()
    {
        var store = CreateStore();
        string displayName = new('城', SaveSlotStore.MaxDisplayNameLength);

        string slotID = store.Create(displayName, []);

        Assert.Equal(displayName, store.ReadManifest(slotID).DisplayName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsEmptyDisplayNameBeforeCreatingDirectory(string displayName)
    {
        var store = CreateStore();

        Assert.Throws<ArgumentException>(() => store.Create(displayName, []));

        Assert.False(Directory.Exists(_saveRoot));
    }

    [Fact]
    public void Create_RejectsOverlongDisplayNameBeforeCreatingDirectory()
    {
        var store = CreateStore();
        string displayName = new('a', SaveSlotStore.MaxDisplayNameLength + 1);

        Assert.Throws<ArgumentException>(() => store.Create(displayName, []));

        Assert.False(Directory.Exists(_saveRoot));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("..\\escape")]
    [InlineData("C:\\escape")]
    [InlineData("中文槽位")]
    [InlineData("slot name")]
    public void DirectoryOperations_RejectUnsafeInternalIDs(string slotID)
    {
        var store = CreateStore();

        Assert.Throws<ArgumentException>(() => store.Save(slotID, "Safe display name", []));
        Assert.Throws<ArgumentException>(() => store.Load(slotID, []));
        Assert.Throws<ArgumentException>(() => store.Exists(slotID));
        Assert.Throws<ArgumentException>(() => store.Delete(slotID));
    }

    [Fact]
    public void ReadManifest_RejectsSlotIDThatDoesNotMatchDirectory()
    {
        var store = CreateStore();
        store.Save("manual-1", "Manual 1", []);
        string manifestPath = Path.Combine(_saveRoot, "manual-1", "manifest.json");
        File.WriteAllText(
            manifestPath,
            File.ReadAllText(manifestPath).Replace("manual-1", "manual-2", StringComparison.Ordinal));

        Assert.Throws<InvalidDataException>(() => store.ReadManifest("manual-1"));
    }

    [Fact]
    public void Save_UnwritableBasePathFailsWithoutPublishingManifest()
    {
        Directory.CreateDirectory(_saveRoot);
        string basePath = Path.Combine(_saveRoot, "not-a-directory");
        File.WriteAllText(basePath, "occupied");
        var store = new SaveSlotStore(basePath);

        Assert.ThrowsAny<IOException>(() => store.Save("manual-1", "Manual 1", []));

        Assert.False(File.Exists(Path.Combine(basePath, "manual-1", "manifest.json")));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("..\\outside")]
    [InlineData("nested/file")]
    [InlineData("nested\\file")]
    public void Save_RejectsUnsafeSystemFileName(string saveFileName)
    {
        var store = CreateStore();

        Assert.Throws<ArgumentException>(() =>
            store.Save("manual-1", "Manual 1", [new TestSaveable(saveFileName, 42)]));

        Assert.False(File.Exists(Path.Combine(_saveRoot, "outside.json")));
        Assert.False(File.Exists(Path.Combine(_saveRoot, "manual-1", "manifest.json")));
    }

    [Fact]
    public void ListSlots_MissingRootReturnsEmptyList()
    {
        var store = CreateStore();

        Assert.Empty(store.ListSlots());
    }

    [Fact]
    public void ListSlots_ReturnsMetadataWithoutLoadingSystemState()
    {
        var store = CreateStore();
        var saveable = new TestSaveable("road_network", 42);
        store.Save("manual-1", "第一座城市", [saveable]);

        SaveSlotSummary summary = Assert.Single(store.ListSlots());

        Assert.True(summary.IsValid);
        Assert.Equal("manual-1", summary.SlotID);
        Assert.Equal("第一座城市", summary.DisplayName);
        Assert.Equal("Unknown City", summary.CityName);
        Assert.Null(summary.Population);
        Assert.Null(summary.Funds);
        Assert.Null(summary.ThumbnailPath);
        Assert.Equal(["road_network.json"], summary.Files);
        Assert.NotNull(summary.SavedAtUtc);
        Assert.Equal(TimeSpan.Zero, summary.SavedAtUtc.Value.Offset);
        Assert.Equal(0, saveable.RestoreCount);
    }

    [Fact]
    public void ListSlots_SortsNewestFirstAndUsesSlotIDAsStableTieBreaker()
    {
        var store = CreateStore();
        store.Save("manual-b", "同名", []);
        store.Save("manual-a", "同名", []);
        store.Save("manual-new", "最新", []);
        RewriteManifest("manual-b", manifest => manifest.Timestamp = "2026-08-03T00:00:00Z");
        RewriteManifest("manual-a", manifest => manifest.Timestamp = "2026-08-03T00:00:00Z");
        RewriteManifest("manual-new", manifest => manifest.Timestamp = "2026-08-04T00:00:00Z");

        IReadOnlyList<SaveSlotSummary> summaries = store.ListSlots();

        Assert.Equal(["manual-new", "manual-a", "manual-b"], summaries.Select(item => item.SlotID));
        Assert.Equal(["最新", "同名", "同名"], summaries.Select(item => item.DisplayName));
    }

    [Fact]
    public void ListSlots_CorruptSlotDoesNotBlockValidSlots()
    {
        var store = CreateStore();
        store.Save("valid", "有效存档", []);
        Directory.CreateDirectory(Path.Combine(_saveRoot, "broken"));
        File.WriteAllText(Path.Combine(_saveRoot, "broken", "manifest.json"), "not json");

        IReadOnlyList<SaveSlotSummary> summaries = store.ListSlots();

        Assert.Collection(
            summaries,
            valid =>
            {
                Assert.True(valid.IsValid);
                Assert.Equal("valid", valid.SlotID);
            },
            broken =>
            {
                Assert.False(broken.IsValid);
                Assert.Equal("broken", broken.SlotID);
                Assert.False(string.IsNullOrWhiteSpace(broken.Error));
            });
    }

    [Fact]
    public void ListSlots_MissingThumbnailUsesPlaceholderUntilFileExists()
    {
        var store = CreateStore();
        store.Save("manual-1", "Manual 1", []);
        RewriteManifest("manual-1", manifest =>
        {
            manifest.CityName = "Harbor City";
            manifest.Population = 12345;
            manifest.Funds = 67890.50m;
            manifest.ThumbnailFile = "thumbnail.png";
        });

        SaveSlotSummary missingThumbnail = Assert.Single(store.ListSlots());
        Assert.Equal("Harbor City", missingThumbnail.CityName);
        Assert.Equal(12345, missingThumbnail.Population);
        Assert.Equal(67890.50m, missingThumbnail.Funds);
        Assert.Null(missingThumbnail.ThumbnailPath);

        string thumbnailPath = Path.Combine(_saveRoot, "manual-1", "thumbnail.png");
        File.WriteAllBytes(thumbnailPath, [0x89, 0x50, 0x4E, 0x47]);
        Assert.Equal(thumbnailPath, Assert.Single(store.ListSlots()).ThumbnailPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_saveRoot))
            Directory.Delete(_saveRoot, recursive: true);
    }

    private SaveSlotStore CreateStore() => new(_saveRoot);

    private void RewriteManifest(string slotID, Action<ManifestData> update)
    {
        string manifestPath = Path.Combine(_saveRoot, slotID, "manifest.json");
        ManifestData manifest = SaveManager.ParseAndValidateManifest(File.ReadAllText(manifestPath));
        update(manifest);
        File.WriteAllText(manifestPath, SaveJson.Serialize(manifest));
    }

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
