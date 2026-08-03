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

    [Fact]
    public void V2Profile_SelectsOnlyRoadGraphFromRegisteredSystems()
    {
        ISaveable roadGraph = new TestSaveable("road_network", 42);
        ISaveable camera = new TestSaveable("camera", 7);
        ISaveable future = new TestSaveable("economy", 99);

        IReadOnlyList<ISaveable> selected = SaveManager.SelectSaveables(
            [camera, future, roadGraph],
            [SaveManager.RoadGraphSaveFileName]);

        Assert.Same(roadGraph, Assert.Single(selected));
    }

    [Fact]
    public void V2Profile_MissingRoadGraphIsRejectedBeforeSaving()
    {
        ISaveable camera = new TestSaveable("camera", 7);

        Assert.Throws<InvalidOperationException>(() => SaveManager.SelectSaveables(
            [camera],
            [SaveManager.RoadGraphSaveFileName]));
    }

    [Fact]
    public void FutureProfile_AddsIndependentFileWithoutChangingRoadGraphPayload()
    {
        var store = CreateStore();
        var roadGraph = new RoadGraph();
        Assert.True(roadGraph.AddRoad(Godot.Vector2.Zero, new Godot.Vector2(8f, 2f), []) >= 0);
        string roadBefore = SaveJson.Serialize(roadGraph.CaptureState());
        var economy = new TestSaveable("economy", 99);
        IReadOnlyList<ISaveable> selected = SaveManager.SelectSaveables(
            [roadGraph, economy],
            [SaveManager.RoadGraphSaveFileName, "economy"]);

        Assert.Equal(2, store.Save("future", "Future", selected));

        ManifestData manifest = store.ReadManifest("future");
        Assert.Equal(["road_network.json", "economy.json"], manifest.Files);
        Assert.Equal(roadBefore, File.ReadAllText(Path.Combine(_saveRoot, "future", "road_network.json")));
        Assert.True(File.Exists(Path.Combine(_saveRoot, "future", "economy.json")));
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
    public void Load_ManifestMissingRegisteredFileDoesNotRestore()
    {
        var store = CreateStore();
        var saveable = new TestSaveable("road_network", 42);
        store.Save("broken", "Broken", [saveable]);
        RewriteManifest("broken", manifest => manifest.Files.Clear());
        saveable.Value = 7;

        Assert.Throws<InvalidDataException>(() => store.Load("broken", [saveable]));

        Assert.Equal(7, saveable.Value);
        Assert.Equal(0, saveable.RestoreCount);
    }

    [Fact]
    public void Load_InvalidJsonInLaterFileDoesNotRestoreEarlierSystem()
    {
        var store = CreateStore();
        var first = new TestSaveable("first", 42);
        var second = new TestSaveable("second", 84);
        store.Save("broken", "Broken", [first, second]);
        File.WriteAllText(Path.Combine(_saveRoot, "broken", "second.json"), "not json");
        first.Value = 7;

        Assert.ThrowsAny<JsonException>(() => store.Load("broken", [first, second]));

        Assert.Equal(7, first.Value);
        Assert.Equal(0, first.RestoreCount);
        Assert.Equal(0, second.RestoreCount);
    }

    [Fact]
    public void Load_PreparationFailureDoesNotCommitAnyPreparedSystem()
    {
        var store = CreateStore();
        var first = new PreparedTestSaveable("first");
        var second = new PreparedTestSaveable("second") { ThrowDuringPrepare = true };
        store.Save("broken", "Broken", [first, second]);

        Assert.Throws<InvalidDataException>(() => store.Load("broken", [first, second]));

        Assert.Equal(1, first.PrepareCount);
        Assert.Equal(0, first.CommitCount);
        Assert.Equal(1, second.PrepareCount);
        Assert.Equal(0, second.CommitCount);
    }

    [Fact]
    public void Load_CorruptRoadGraphDoesNotChangeActiveGraph()
    {
        var store = CreateStore();
        var saved = new RoadGraph();
        Assert.True(saved.AddRoad(Godot.Vector2.Zero, new Godot.Vector2(8f, 2f), []) >= 0);
        store.Save("broken", "Broken", [saved]);

        var active = new RoadGraph();
        Assert.True(active.AddRoad(Godot.Vector2.Zero, new Godot.Vector2(4f, 6f), []) >= 0);
        string stateBefore = SaveJson.Serialize(active.CaptureState());
        File.WriteAllText(
            Path.Combine(_saveRoot, "broken", "road_network.json"),
            "{\"schemaVersion\":1,\"nextID\":1,\"nodes\":[{\"id\":0,\"x\":0,\"y\":0}],\"edges\":[],\"groups\":[]}");
        string manifestBefore = File.ReadAllText(Path.Combine(_saveRoot, "broken", "manifest.json"));
        string roadFileBefore = File.ReadAllText(Path.Combine(_saveRoot, "broken", "road_network.json"));

        Assert.Throws<JsonException>(() => store.Load("broken", [active]));

        Assert.Equal(stateBefore, SaveJson.Serialize(active.CaptureState()));
        Assert.Equal(manifestBefore, File.ReadAllText(Path.Combine(_saveRoot, "broken", "manifest.json")));
        Assert.Equal(roadFileBefore, File.ReadAllText(Path.Combine(_saveRoot, "broken", "road_network.json")));
    }

    [Fact]
    public void Load_MissingOptionalThumbnailStillRestoresState()
    {
        var store = CreateStore();
        var saveable = new TestSaveable("road_network", 42);
        store.Save("manual-1", "Manual 1", [saveable]);
        RewriteManifest("manual-1", manifest => manifest.ThumbnailFile = "missing.png");
        saveable.Value = 7;

        Assert.Equal(1, store.Load("manual-1", [saveable]));

        Assert.Equal(42, saveable.Value);
        Assert.Equal(1, saveable.RestoreCount);
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
    public void DeleteSlot_TransactionPathFailurePreservesExistingSlot()
    {
        var store = CreateStore();
        store.Save("manual-1", "Manual 1", [new TestSaveable("road_network", 42)]);
        File.WriteAllText(Path.Combine(_saveRoot, ".manual-1.backup"), "occupied");

        Assert.Throws<IOException>(() => store.Delete("manual-1"));

        Assert.True(Directory.Exists(Path.Combine(_saveRoot, "manual-1")));
        Assert.True(File.Exists(Path.Combine(_saveRoot, "manual-1", "manifest.json")));
    }

    [Fact]
    public void Save_CaptureFailureDoesNotPublishSlot()
    {
        var store = CreateStore();

        Assert.Throws<InvalidOperationException>(() =>
            store.Save("broken", "Broken", [new ThrowingSaveable()]));

        string slotDir = Path.Combine(_saveRoot, "broken");
        Assert.False(store.Exists("broken"));
        Assert.False(Directory.Exists(slotDir));
    }

    [Fact]
    public void Save_SerializationFailurePreservesExistingSlot()
    {
        var store = CreateStore();
        store.Save("manual-1", "Original", [new TestSaveable("road_network", 42)]);
        IReadOnlyDictionary<string, string> before = SnapshotSlot("manual-1");

        Assert.Throws<JsonException>(() => store.Save(
            "manual-1",
            "Replacement",
            [new TestSaveable("road_network", 99), new CyclicSaveable()]));

        Assert.Equal(before, SnapshotSlot("manual-1"));
        Assert.Empty(TransactionDirectories());
    }

    [Fact]
    public void Save_PublishFailureRestoresExistingSlotAndCleansTransactions()
    {
        var store = CreateStore();
        store.Save("manual-1", "Original", [new TestSaveable("road_network", 42)]);
        IReadOnlyDictionary<string, string> before = SnapshotSlot("manual-1");
        var failingStore = new SaveSlotStore(_saveRoot, phase =>
        {
            if (phase == SavePublicationPhase.PreviousSlotMoved)
                throw new IOException("Injected publish failure.");
        });

        Assert.Throws<IOException>(() => failingStore.Save(
            "manual-1",
            "Replacement",
            [new TestSaveable("road_network", 99)]));

        Assert.Equal(before, SnapshotSlot("manual-1"));
        Assert.Empty(TransactionDirectories());
    }

    [Fact]
    public void Save_RecoversInterruptedBackupAndDiscardsStaleStaging()
    {
        var store = CreateStore();
        store.Save("manual-1", "Original", [new TestSaveable("road_network", 42)]);
        string slotDir = Path.Combine(_saveRoot, "manual-1");
        string backupDir = Path.Combine(_saveRoot, ".manual-1.backup");
        string stagingDir = Path.Combine(_saveRoot, ".manual-1.staging");
        Directory.Move(slotDir, backupDir);
        Directory.CreateDirectory(stagingDir);
        File.WriteAllText(Path.Combine(stagingDir, "partial.json"), "partial");

        var loaded = new TestSaveable("road_network", 0);
        Assert.Equal(1, store.Load("manual-1", [loaded]));
        Assert.Equal(42, loaded.Value);
        Assert.Empty(TransactionDirectories());

        store.Save("manual-1", "Recovered", [new TestSaveable("road_network", 99)]);
        loaded.Value = 0;
        Assert.Equal(1, store.Load("manual-1", [loaded]));
        Assert.Equal(99, loaded.Value);
        Assert.Equal("Recovered", store.ReadManifest("manual-1").DisplayName);
        Assert.Empty(TransactionDirectories());
    }

    [Fact]
    public void ListSlots_IgnoresPublicationTransactionDirectories()
    {
        var store = CreateStore();
        store.Save("manual-1", "Manual 1", []);
        Directory.CreateDirectory(Path.Combine(_saveRoot, ".manual-1.staging"));
        Directory.CreateDirectory(Path.Combine(_saveRoot, ".manual-1.backup"));

        SaveSlotSummary summary = Assert.Single(store.ListSlots());

        Assert.Equal("manual-1", summary.SlotID);
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

    private IReadOnlyDictionary<string, string> SnapshotSlot(string slotID) =>
        Directory.GetFiles(Path.Combine(_saveRoot, slotID))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToDictionary(path => Path.GetFileName(path), File.ReadAllText, StringComparer.Ordinal);

    private string[] TransactionDirectories() => Directory.Exists(_saveRoot)
        ? Directory.GetDirectories(_saveRoot, ".*.*", SearchOption.TopDirectoryOnly)
        : [];

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

    private sealed class CyclicSaveable : ISaveable
    {
        public string SaveFileName => "cyclic";

        public object CaptureState()
        {
            var state = new CyclicState();
            state.Self = state;
            return state;
        }

        public void RestoreState(string json) => throw new NotSupportedException();

        private sealed class CyclicState
        {
            public CyclicState? Self { get; set; }
        }
    }

    private sealed class PreparedTestSaveable(string saveFileName) : IPreparedSaveable
    {
        public string SaveFileName { get; } = saveFileName;
        public bool ThrowDuringPrepare { get; init; }
        public int PrepareCount { get; private set; }
        public int CommitCount { get; private set; }

        public object CaptureState() => new TestState { Value = 1 };

        public void RestoreState(string json) => RestorePreparedState(PrepareRestoreState(json));

        public object PrepareRestoreState(string json)
        {
            PrepareCount++;
            if (ThrowDuringPrepare)
                throw new InvalidDataException("Preparation failed.");
            return JsonSerializer.Deserialize<TestState>(json)
                ?? throw new JsonException("Prepared test state must be an object.");
        }

        public void RestorePreparedState(object preparedState)
        {
            Assert.IsType<TestState>(preparedState);
            CommitCount++;
        }
    }
}
