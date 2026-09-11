using Godot;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SimpleCities.Tests;

public sealed class SaveManagerSlotContractTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        $"simple-cities-v3-save-tests-{Guid.NewGuid():N}");

    private string V3Root => Path.Combine(_testRoot, "v3");
    private string V2ResourceRoot => Path.Combine(_testRoot, "legacy-res-saves");
    private string V2UserRoot => Path.Combine(_testRoot, "legacy-user-saves");

    [Fact]
    public void ProductionRoot_AlwaysUsesUserV3Root()
    {
        var requested = new List<string>();
        string Globalize(string path)
        {
            requested.Add(path);
            return "C:/profile/SimpleCities/saves-v3";
        }

        Assert.Equal(
            "C:/profile/SimpleCities/saves-v3",
            V3RoadStorage.Policy.ResolveSaveBaseDir(Globalize));
        Assert.Equal(["user://saves-v3"], requested);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsStreamStateAndWritesLengthHashManifest()
    {
        var store = CreateStore();
        var saveable = new TestSaveable("road_network", 42);

        SavePublishResult publish = store.Save("manual-1", "第一座城市", [saveable]);
        Assert.Equal(SavePublishResultKind.Published, publish.Kind);
        Assert.Equal(1, publish.SavedFileCount);
        V3Manifest manifest = store.ReadManifest("manual-1");
        V3ManifestFile file = Assert.Single(manifest.Files);
        string payloadPath = Path.Combine(V3Root, "manual-1", file.Name);
        byte[] payload = File.ReadAllBytes(payloadPath);
        Assert.Equal("manual-1", manifest.SlotID);
        Assert.Equal("第一座城市", manifest.DisplayName);
        Assert.Equal(payload.LongLength, file.EncodedLength);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant(),
            file.Sha256);

        saveable.Value = 7;
        Assert.Equal(1, store.Load("manual-1", [saveable]));
        Assert.Equal(42, saveable.Value);
        Assert.Equal(1, saveable.PrepareCount);
        Assert.Equal(1, saveable.CommitCount);
    }

    [Fact]
    public void Profile_SelectsOnlyExactRoadGraphParticipant()
    {
        IStreamingSaveable graph = new TestSaveable("road_network", 1);
        IStreamingSaveable economy = new TestSaveable("economy", 2);

        IReadOnlyList<IStreamingSaveable> selected = V3RoadStorage.Policy.SelectParticipants(
            [economy, graph]);

        Assert.Same(graph, Assert.Single(selected));
        Assert.Throws<InvalidOperationException>(() => V3RoadStorage.Policy.SelectParticipants([economy]));
    }

    [Fact]
    public void ScenePolicy_UsesCapturedRootAndPayloadNamesForRoundTrip()
    {
        string[] fileNames = ["economy"];
        var policy = new SceneStoragePolicy("user://isolated-scene", fileNames);
        fileNames[0] = "road_network";
        var requestedRoots = new List<string>();
        string root = policy.ResolveSaveBaseDir(path =>
        {
            requestedRoots.Add(path);
            return Path.Combine(_testRoot, "isolated-scene");
        });
        var store = new SaveSlotStore(root);
        var road = new TestSaveable("road_network", 13);
        var economy = new TestSaveable("economy", 42);
        IReadOnlyList<IStreamingSaveable> participants = policy.SelectParticipants([road, economy]);

        store.Save("manual-policy", "Policy round-trip", participants);
        economy.Value = 0;
        store.Load("manual-policy", participants);

        Assert.Equal(["user://isolated-scene"], requestedRoots);
        Assert.Equal("economy.json", Assert.Single(store.ReadManifest("manual-policy").Files).Name);
        Assert.Equal(42, economy.Value);
        Assert.Equal(13, road.Value);
        Assert.Equal(0, road.CommitCount);
        Assert.False(Directory.Exists(V3Root));
    }

    [Fact]
    public void Load_TamperedPayloadMayPrepareButNeverCommits()
    {
        var store = CreateStore();
        var saved = new TestSaveable("road_network", 42);
        store.Save("broken", "Broken", [saved]);
        string payloadPath = Path.Combine(V3Root, "broken", "road_network.json");
        File.WriteAllText(payloadPath, "{\"value\":99}", new UTF8Encoding(false));
        var active = new TestSaveable("road_network", 7);

        Assert.Throws<InvalidDataException>(() => store.Load("broken", [active]));

        Assert.Equal(7, active.Value);
        Assert.Equal(1, active.PrepareCount);
        Assert.Equal(0, active.CommitCount);
    }

    [Fact]
    public void Load_LaterPreparationFailureDoesNotCommitEarlierParticipant()
    {
        var store = CreateStore();
        store.Save("broken", "Broken", [
            new TestSaveable("first", 1),
            new TestSaveable("second", 2),
        ]);
        var first = new TestSaveable("first", 10);
        var second = new TestSaveable("second", 20) { ThrowDuringPrepare = true };

        Assert.Throws<InvalidDataException>(() => store.Load("broken", [first, second]));

        Assert.Equal(1, first.PrepareCount);
        Assert.Equal(0, first.CommitCount);
        Assert.Equal(1, second.PrepareCount);
        Assert.Equal(0, second.CommitCount);
        Assert.Equal(10, first.Value);
        Assert.Equal(20, second.Value);
    }

    [Fact]
    public void Load_DetectsDirectorySetChangeDuringPrepareBeforeAnyCommit()
    {
        var store = CreateStore();
        store.Save("manual-1", "Manual", [new TestSaveable("road_network", 42)]);
        string injectedPath = Path.Combine(V3Root, "manual-1", "injected.json");
        var active = new TestSaveable("road_network", 7)
        {
            DuringPrepare = () => File.WriteAllText(injectedPath, "{}", new UTF8Encoding(false)),
        };

        Assert.Throws<InvalidDataException>(() => store.Load("manual-1", [active]));

        Assert.Equal(1, active.PrepareCount);
        Assert.Equal(0, active.CommitCount);
        Assert.Equal(7, active.Value);
    }

    [Fact]
    public void Load_HoldsPayloadHandleAgainstReplacementDuringPrepare()
    {
        var store = CreateStore();
        store.Save("manual-1", "Manual", [new TestSaveable("road_network", 42)]);
        string payloadPath = Path.Combine(V3Root, "manual-1", "road_network.json");
        Exception? replacementError = null;
        var active = new TestSaveable("road_network", 7)
        {
            DuringPrepare = () =>
            {
                try
                {
                    File.Move(payloadPath, payloadPath + ".moved");
                }
                catch (Exception exception)
                {
                    replacementError = exception;
                }
            },
        };

        Assert.Equal(1, store.Load("manual-1", [active]));

        Assert.IsType<IOException>(replacementError);
        Assert.Equal(42, active.Value);
        Assert.False(File.Exists(payloadPath + ".moved"));
    }

    [Fact]
    public void Load_CorruptRoadGraphDoesNotChangeActiveGraphOrEmitEvent()
    {
        var store = CreateStore();
        var saved = new RoadGraph();
        Assert.True(saved.SubmitPolyline(
            RoadType.Street,
            [Vector2.Zero, new Vector2(8f, 2f)]).Success);
        store.Save("broken", "Broken", [saved]);

        var active = new RoadGraph();
        Assert.True(active.SubmitPolyline(
            RoadType.Highway,
            [Vector2.Zero, new Vector2(4f, 6f)]).Success);
        string before = RoadGraphTestCodec.CaptureJson(active);
        GraphStateToken tokenBefore = active.CurrentStateToken;
        int eventCount = 0;
        active.GraphChanged += _ => eventCount++;
        RewritePayloadAndManifest("broken", "{\"formatFamily\":\"simple-cities-v3\"}");

        Assert.Throws<JsonException>(() => store.Load("broken", [active]));

        Assert.Equal(before, RoadGraphTestCodec.CaptureJson(active));
        Assert.Equal(tokenBefore, active.CurrentStateToken);
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void Load_RoadGraphCreatesNewLineageAndInvalidatesOldHistory()
    {
        var store = CreateStore();
        var saved = new RoadGraph();
        Assert.True(saved.SubmitPolyline(
            RoadType.Dirt,
            [Vector2.Zero, new Vector2(9f, 0f)]).Success);
        store.Save("manual-1", "Manual", [saved]);

        var active = new RoadGraph();
        var history = new RoadEditHistory(active);
        Assert.True(history.Execute(() => active.SubmitPolyline(
            RoadType.Street,
            [Vector2.Zero, new Vector2(2f, 1f)]).Success));
        Assert.True(history.CanUndo);
        GraphLineageID oldLineage = active.CaptureRevision().LineageID;

        Assert.Equal(1, store.Load("manual-1", [active]));

        Assert.NotEqual(oldLineage, active.CaptureRevision().LineageID);
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Save_CaptureOrWriteFailureDoesNotPublishAndPreservesExistingSlot()
    {
        var store = CreateStore();
        Assert.Throws<InvalidOperationException>(() => store.Save(
            "new-slot", "New", [new ThrowingCaptureSaveable()]));
        Assert.Equal(SaveSlotOccupantKind.Absent, store.ClassifySlot("new-slot").Kind);

        store.Save("manual-1", "Original", [new TestSaveable("road_network", 42)]);
        IReadOnlyDictionary<string, string> before = SnapshotSlot("manual-1");
        Assert.Throws<JsonException>(() => store.Save(
            "manual-1", "Replacement", [new ThrowingWriteSaveable()]));
        Assert.Equal(before, SnapshotSlot("manual-1"));
        Assert.Empty(TransactionDirectories());
    }

    [Theory]
    [InlineData(unchecked((int)0x80070070))]
    [InlineData(28)]
    public void Save_DiskFullAfterPartialPayloadWriteNeverPublishesAndPreservesExistingSlot(
        int diskFullHResult)
    {
        var store = CreateStore();
        var newSlotSaveable = new DiskFullAfterPartialWriteSaveable(diskFullHResult);

        IOException newSlotFailure = Assert.Throws<IOException>(() => store.Save(
            "new-slot", "New", [newSlotSaveable]));

        Assert.Equal(diskFullHResult, newSlotFailure.HResult);
        Assert.Equal(1, newSlotSaveable.CaptureCount);
        Assert.False(Directory.Exists(Path.Combine(V3Root, "new-slot")));
        Assert.Empty(TransactionDirectories());

        store.Save("manual-1", "Original", [new TestSaveable("road_network", 42)]);
        IReadOnlyDictionary<string, string> before = SnapshotSlot("manual-1");
        var replacement = new DiskFullAfterPartialWriteSaveable(diskFullHResult);

        IOException replacementFailure = Assert.Throws<IOException>(() => store.Save(
            "manual-1", "Replacement", [replacement]));

        Assert.Equal(diskFullHResult, replacementFailure.HResult);
        Assert.Equal(1, replacement.CaptureCount);
        Assert.Equal(before, SnapshotSlot("manual-1"));
        Assert.Empty(TransactionDirectories());
    }

    [Fact]
    public void Save_TransactionRootOccupiedByFileFailsBeforeCaptureAndPreservesOccupant()
    {
        Directory.CreateDirectory(V3Root);
        string transactionRoot = Path.Combine(V3Root, ".save-transactions");
        byte[] occupant = Encoding.UTF8.GetBytes("transaction-root-canary");
        File.WriteAllBytes(transactionRoot, occupant);
        var saveable = new TestSaveable("road_network", 42);

        SavePublicationRecoveryException exception = Assert.Throws<SavePublicationRecoveryException>(
            () => CreateStore().Save("new-slot", "New", [saveable]));

        Assert.Contains("occupied by a file", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, saveable.CaptureCount);
        Assert.Equal(occupant, File.ReadAllBytes(transactionRoot));
        Assert.False(Directory.Exists(Path.Combine(V3Root, "new-slot")));
        Assert.Empty(Directory.GetDirectories(V3Root));
    }

    [Fact]
    public void Save_PublishesDescriptorInsideUniqueOperationTransactionBeforeCanonicalMove()
    {
        string? observedOperationDirectory = null;
        var store = new SaveSlotStore(V3Root, phase =>
        {
            if (phase != SavePublicationPhase.Staged)
                return;

            string slotTransactions = Path.Combine(V3Root, ".save-transactions", "manual-1");
            observedOperationDirectory = Assert.Single(Directory.GetDirectories(slotTransactions));
            Assert.Matches("^[0-9a-f]{32}$", Path.GetFileName(observedOperationDirectory));
            Assert.True(Directory.Exists(Path.Combine(observedOperationDirectory, "staging")));
            Assert.True(File.Exists(Path.Combine(observedOperationDirectory, "publish.json")));
            Assert.False(Directory.Exists(Path.Combine(V3Root, "manual-1")));
        });

        SavePublishResult result = store.Save(
            "manual-1", "Manual", [new TestSaveable("road_network", 42)]);
        Assert.Equal(SavePublishResultKind.Published, result.Kind);
        Assert.Equal(1, result.SavedFileCount);

        Assert.NotNull(observedOperationDirectory);
        Assert.False(Directory.Exists(observedOperationDirectory));
        Assert.Empty(TransactionDirectories());
    }

    [Fact]
    public void Save_CleanupFailureAfterCanonicalPublishReturnsPendingWithoutRollback()
    {
        var store = new SaveSlotStore(V3Root, phase =>
        {
            if (phase == SavePublicationPhase.CanonicalPublished)
                throw new IOException("Injected cleanup failure.");
        });

        SavePublishResult result = store.Save(
            "manual-1", "Manual", [new TestSaveable("road_network", 42)]);

        Assert.Equal(SavePublishResultKind.PublishedWithCleanupPending, result.Kind);
        Assert.Contains("cleanup failure", result.Warning, StringComparison.OrdinalIgnoreCase);
        var loaded = new TestSaveable("road_network", 0);
        Assert.Equal(1, CreateStore().Load("manual-1", [loaded]));
        Assert.Equal(42, loaded.Value);
    }

    [Fact]
    public void Save_PublishFailureRestoresExistingSlotAndCleansTransactions()
    {
        var store = CreateStore();
        store.Save("manual-1", "Original", [new TestSaveable("road_network", 42)]);
        IReadOnlyDictionary<string, string> before = SnapshotSlot("manual-1");
        var failingStore = new SaveSlotStore(V3Root, phase =>
        {
            if (phase == SavePublicationPhase.PreviousSlotMoved)
                throw new IOException("Injected publish failure.");
        });

        Assert.Throws<IOException>(() => failingStore.Save(
            "manual-1", "Replacement", [new TestSaveable("road_network", 99)]));

        Assert.Equal(before, SnapshotSlot("manual-1"));
        Assert.Empty(TransactionDirectories());
    }

    [Fact]
    public void Recovery_AfterPreviousSlotMoveCompletesDescriptorBoundPublication()
    {
        var store = CreateStore();
        store.Save("manual-1", "Original", [new TestSaveable("road_network", 42)]);
        PublicationFixture fixture = CreatePublicationFixture("manual-1", 99);
        Directory.Move(fixture.SlotDirectory, fixture.BackupDirectory);

        Assert.True(store.Exists("manual-1"));

        var loaded = new TestSaveable("road_network", 0);
        Assert.Equal(1, store.Load("manual-1", [loaded]));
        Assert.Equal(99, loaded.Value);
        Assert.False(Directory.Exists(fixture.OperationDirectory));
        Assert.Empty(TransactionDirectories());
    }

    [Fact]
    public void Recovery_OldCanonicalWithStagedReplacementQuarantinesUncommittedOperation()
    {
        var store = CreateStore();
        store.Save("manual-1", "Original", [new TestSaveable("road_network", 42)]);
        PublicationFixture fixture = CreatePublicationFixture("manual-1", 99);
        string operationToken = Path.GetFileName(fixture.OperationDirectory);

        Assert.True(store.Exists("manual-1"));

        var loaded = new TestSaveable("road_network", 0);
        Assert.Equal(1, store.Load("manual-1", [loaded]));
        Assert.Equal(42, loaded.Value);
        Assert.False(Directory.Exists(fixture.OperationDirectory));
        Assert.True(Directory.Exists(Path.Combine(
            V3Root, ".save-quarantine", "manual-1", operationToken)));
        Assert.False(Directory.Exists(Path.Combine(V3Root, ".save-transactions")));
    }

    [Fact]
    public void Recovery_BackupWithoutCanonicalOrStagingRestoresOldSlot()
    {
        var store = CreateStore();
        store.Save("manual-1", "Original", [new TestSaveable("road_network", 42)]);
        PublicationFixture fixture = CreatePublicationFixture("manual-1", 99);
        Directory.Move(fixture.SlotDirectory, fixture.BackupDirectory);
        Directory.Delete(fixture.StagingDirectory, recursive: true);

        Assert.True(store.Exists("manual-1"));

        var loaded = new TestSaveable("road_network", 0);
        Assert.Equal(1, store.Load("manual-1", [loaded]));
        Assert.Equal(42, loaded.Value);
        Assert.False(Directory.Exists(fixture.OperationDirectory));
        Assert.False(Directory.Exists(Path.Combine(V3Root, ".save-transactions")));
    }

    [Fact]
    public void Recovery_FirstSaveStagingWithoutCanonicalIsQuarantinedAndNeverPublished()
    {
        PublicationFixture fixture = CreateFirstSavePublicationFixture("manual-1", 42);
        string operationToken = Path.GetFileName(fixture.OperationDirectory);

        Assert.False(CreateStore().Exists("manual-1"));

        Assert.False(Directory.Exists(fixture.SlotDirectory));
        Assert.False(Directory.Exists(fixture.OperationDirectory));
        Assert.True(Directory.Exists(Path.Combine(
            V3Root, ".save-quarantine", "manual-1", operationToken)));
        Assert.False(Directory.Exists(Path.Combine(V3Root, ".save-transactions")));
    }

    [Fact]
    public void Recovery_CanonicalNewWithUnexpectedBackupBlocksAndPreservesEvidence()
    {
        var store = CreateStore();
        store.Save("manual-1", "Original", [new TestSaveable("road_network", 42)]);
        PublicationFixture fixture = CreatePublicationFixture("manual-1", 99);
        Directory.Move(fixture.SlotDirectory, fixture.BackupDirectory);
        Directory.Move(fixture.StagingDirectory, fixture.SlotDirectory);
        File.AppendAllText(
            Path.Combine(fixture.BackupDirectory, "road_network.json"),
            " ",
            new UTF8Encoding(false));
        string[] pathsBefore = Directory.GetFileSystemEntries(fixture.OperationDirectory, "*", SearchOption.AllDirectories);

        SavePublicationRecoveryException exception = Assert.Throws<SavePublicationRecoveryException>(
            () => store.Exists("manual-1"));

        Assert.Contains("ambiguous", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(Directory.Exists(fixture.SlotDirectory));
        Assert.True(Directory.Exists(fixture.BackupDirectory));
        Assert.True(File.Exists(fixture.DescriptorPath));
        Assert.Equal(
            pathsBefore.Order(StringComparer.Ordinal),
            Directory.GetFileSystemEntries(fixture.OperationDirectory, "*", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Recovery_DescriptorlessPartialTransactionIsQuarantinedAndNeverPromoted()
    {
        const string operationToken = "0123456789abcdef0123456789abcdef";
        string operationDirectory = Path.Combine(
            V3Root, ".save-transactions", "manual-1", operationToken);
        string stagingDirectory = Path.Combine(operationDirectory, "staging");
        Directory.CreateDirectory(stagingDirectory);
        File.WriteAllText(Path.Combine(stagingDirectory, "partial.json"), "{}", new UTF8Encoding(false));

        Assert.False(CreateStore().Exists("manual-1"));

        Assert.False(Directory.Exists(Path.Combine(V3Root, "manual-1")));
        Assert.False(Directory.Exists(operationDirectory));
        Assert.True(Directory.Exists(Path.Combine(
            V3Root, ".save-quarantine", "manual-1", operationToken)));
    }

    [Fact]
    public void Operations_RejectConcurrentRootOwnerBeforeInspectingOrMutatingSlots()
    {
        Directory.CreateDirectory(V3Root);
        string lockPath = Path.Combine(V3Root, ".save-root.lock");
        using var externalOwner = new FileStream(
            lockPath,
            FileMode.OpenOrCreate,
            System.IO.FileAccess.ReadWrite,
            FileShare.None);

        SaveRootBusyException exception = Assert.Throws<SaveRootBusyException>(
            () => CreateStore().ListSlots());

        Assert.Contains("locked", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.GetDirectories(V3Root));
    }

    [Fact]
    public async Task Operations_RejectIndependentProcessRootOwnerAfterDeterministicHandshake()
    {
        Directory.CreateDirectory(V3Root);
        string lockPath = Path.Combine(V3Root, ".save-root.lock");
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        string probeDll = Path.Combine(
            ProjectRoot(),
            "tests",
            "SimpleCities.SaveLockProbe",
            "bin",
            configuration,
            "net10.0",
            "SimpleCities.SaveLockProbe.dll");
        Assert.True(File.Exists(probeDll), $"Lock probe was not built: {probeDll}");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(probeDll);
        startInfo.ArgumentList.Add(lockPath);
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the save lock probe.");
        try
        {
            string? ready = await process.StandardOutput.ReadLineAsync()
                .WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal("READY", ready);

            SaveRootBusyException exception = Assert.Throws<SaveRootBusyException>(
                () => CreateStore().ListSlots());
            Assert.Contains("another operation or process", exception.Message, StringComparison.Ordinal);

            await process.StandardInput.WriteLineAsync("RELEASE");
            process.StandardInput.Close();
            Assert.True(process.WaitForExit(10_000), "Save lock probe did not exit after release.");
            Assert.Equal(0, process.ExitCode);
            Assert.Empty(CreateStore().ListSlots());
        }
        finally
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
    }

    [Fact]
    public void CreateListAndDelete_PreserveNamedSlotBehavior()
    {
        var store = CreateStore();
        const string displayName = "同名城市 / 夏季: 2026";
        string first = store.Create(displayName, [new TestSaveable("road_network", 1)]);
        string second = store.Create(displayName, [new TestSaveable("road_network", 2)]);

        Assert.NotEqual(first, second);
        Assert.Matches("^manual-[0-9a-f]{32}$", first);
        Assert.Equal(2, store.ListSlots().Count);
        SaveDeleteResult deleted = store.Delete(AuthorizeDelete(store, first));
        Assert.Equal(SaveDeleteResultKind.Deleted, deleted.Kind);
        Assert.False(store.Exists(first));
        Assert.Single(store.ListSlots());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" name")]
    public void Create_RejectsInvalidDisplayNameBeforeCreatingRoot(string displayName)
    {
        var store = CreateStore();
        Assert.Throws<ArgumentException>(() => store.Create(displayName, []));
        Assert.False(Directory.Exists(V3Root));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("..\\escape")]
    [InlineData("C:\\escape")]
    [InlineData("中文槽位")]
    [InlineData("slot name")]
    public void Operations_RejectUnsafeSlotID(string slotID)
    {
        var store = CreateStore();
        Assert.Throws<ArgumentException>(() => store.Save(slotID, "Safe", []));
        Assert.Throws<ArgumentException>(() => store.Load(slotID, []));
        Assert.Throws<ArgumentException>(() => store.Exists(slotID));
        Assert.Throws<ArgumentException>(() => store.Delete(new SaveDeletionAuthorization(
            slotID,
            1,
            "0123456789abcdef0123456789abcdef",
            SaveSlotOccupantKind.CompleteV3,
            new string('0', 64),
            "Safe")));
    }

    [Fact]
    public void List_ReturnsMetadataWithoutPreparingBusinessPayload()
    {
        var store = CreateStore();
        var saveable = new TestSaveable("road_network", 42);
        store.Save("manual-1", "第一座城市", [saveable]);

        SaveSlotSummary summary = Assert.Single(store.ListSlots());

        Assert.True(summary.IsValid);
        Assert.Equal("manual-1", summary.SlotID);
        Assert.Equal("第一座城市", summary.DisplayName);
        Assert.Equal(["road_network.json"], summary.Files);
        Assert.Equal(0, saveable.PrepareCount);
        Assert.Equal(0, saveable.CommitCount);
    }

    [Fact]
    public void MissingOptionalThumbnailKeepsBusinessSlotComplete()
    {
        var store = CreateStore();
        store.Save("manual-1", "Manual", [new TestSaveable("road_network", 42)]);
        RewriteManifest("manual-1", manifest => manifest with { ThumbnailFile = "thumbnail.png" });

        SaveSlotSummary summary = Assert.Single(store.ListSlots());

        Assert.True(summary.IsValid);
        Assert.Null(summary.ThumbnailPath);
        Assert.NotNull(summary.Warning);
        Assert.Equal(1, store.Load("manual-1", [new TestSaveable("road_network", 0)]));
    }

    [Fact]
    public void ValidThumbnailIsExposedButExcludedFromBusinessAggregateDigest()
    {
        var store = CreateStore();
        store.Save("manual-1", "Manual", [new TestSaveable("road_network", 42)]);
        RewriteManifest("manual-1", manifest => manifest with { ThumbnailFile = "thumbnail.png" });
        string slot = Path.Combine(V3Root, "manual-1");
        string thumbnail = Path.Combine(slot, "thumbnail.png");
        File.WriteAllBytes(thumbnail, PngTestData.CreateRgba(2, 2, 0x22));
        V3Manifest manifest = store.ReadManifest("manual-1");
        string before = SaveSlotStore.ComputeAggregateDigest(slot, manifest);

        SaveSlotSummary summary = Assert.Single(store.ListSlots());
        File.WriteAllBytes(thumbnail, PngTestData.CreateRgba(2, 2, 0x99));
        string after = SaveSlotStore.ComputeAggregateDigest(slot, manifest);

        Assert.True(summary.IsValid);
        Assert.Equal(thumbnail, summary.ThumbnailPath);
        Assert.Null(summary.Warning);
        Assert.Equal(before, after);
    }

    [Fact]
    public void InvalidThumbnailProducesWarningWithoutInvalidatingBusinessSlot()
    {
        var store = CreateStore();
        store.Save("manual-1", "Manual", [new TestSaveable("road_network", 42)]);
        RewriteManifest("manual-1", manifest => manifest with { ThumbnailFile = "thumbnail.png" });
        File.WriteAllText(
            Path.Combine(V3Root, "manual-1", "thumbnail.png"),
            "not-a-png",
            new UTF8Encoding(false));

        SaveSlotSummary summary = Assert.Single(store.ListSlots());

        Assert.True(summary.IsValid);
        Assert.Null(summary.ThumbnailPath);
        Assert.Contains("PNG signature", summary.Warning, StringComparison.Ordinal);
        Assert.Equal(1, store.Load("manual-1", [new TestSaveable("road_network", 0)]));
    }

    [Fact]
    public void CopiedV2OrUnknownSlotIsForeignInvisibleAndImmutable()
    {
        Directory.CreateDirectory(Path.Combine(V3Root, "copied-v2"));
        string manifestPath = Path.Combine(V3Root, "copied-v2", "manifest.json");
        string payloadPath = Path.Combine(V3Root, "copied-v2", "road_network.json");
        File.WriteAllText(manifestPath, "{\"schemaVersion\":1,\"slotId\":\"copied-v2\",\"files\":[\"road_network.json\"]}");
        File.WriteAllText(payloadPath, "{\"schemaVersion\":3,\"nextID\":0,\"nodes\":[],\"edges\":[]}");
        IReadOnlyDictionary<string, string> before = SnapshotSlot("copied-v2");
        var saveable = new TestSaveable("road_network", 42);

        Assert.Equal(SaveSlotOccupantKind.Foreign, CreateStore().ClassifySlot("copied-v2").Kind);
        Assert.Empty(CreateStore().ListSlots());
        Assert.Throws<InvalidDataException>(() => CreateStore().Delete(new SaveDeletionAuthorization(
            "copied-v2",
            1,
            "0123456789abcdef0123456789abcdef",
            SaveSlotOccupantKind.CompleteV3,
            new string('0', 64),
            "copied-v2")));
        Assert.Throws<InvalidDataException>(() => CreateStore().Load("copied-v2", [saveable]));
        Assert.Throws<InvalidDataException>(() => CreateStore().Save("copied-v2", "No", [saveable]));
        Assert.Equal(0, saveable.CaptureCount);
        Assert.Equal(before, SnapshotSlot("copied-v2"));
    }

    [Fact]
    public void SlotPathOccupiedByFileIsUnsafeInvisibleAndImmutable()
    {
        Directory.CreateDirectory(V3Root);
        string slotPath = Path.Combine(V3Root, "occupied");
        File.WriteAllText(slotPath, "do not replace", new UTF8Encoding(false));
        var saveable = new TestSaveable("road_network", 42);

        Assert.Equal(SaveSlotOccupantKind.Unsafe, CreateStore().ClassifySlot("occupied").Kind);
        Assert.Empty(CreateStore().ListSlots());
        Assert.Throws<InvalidDataException>(() => CreateStore().Delete(new SaveDeletionAuthorization(
            "occupied",
            1,
            "0123456789abcdef0123456789abcdef",
            SaveSlotOccupantKind.CompleteV3,
            new string('0', 64),
            "occupied")));
        Assert.Throws<InvalidDataException>(() =>
            CreateStore().Save("occupied", "No", [saveable]));
        Assert.Equal(0, saveable.CaptureCount);
        Assert.Equal("do not replace", File.ReadAllText(slotPath));
    }

    [Fact]
    public void DeclaredV3CorruptionIsListedAndCanBeExplicitlyDeleted()
    {
        string slotDir = Path.Combine(V3Root, "broken");
        Directory.CreateDirectory(slotDir);
        File.WriteAllText(
            Path.Combine(slotDir, "manifest.json"),
            "{\"formatFamily\":\"simple-cities-v3\",\"schemaVersion\":99}");

        SaveSlotSummary summary = Assert.Single(CreateStore().ListSlots());

        Assert.False(summary.IsValid);
        Assert.Equal("broken", summary.SlotID);
        SaveSlotStore store = CreateStore();
        SaveDeleteResult deleted = store.Delete(AuthorizeDelete(store, "broken"));
        Assert.Equal(SaveDeleteResultKind.Deleted, deleted.Kind);
        Assert.False(Directory.Exists(slotDir));
    }

    [Fact]
    public void Delete_StaleOccupantDigestCannotDeleteReplacementSlot()
    {
        var store = CreateStore();
        store.Save("manual-1", "Original", [new TestSaveable("road_network", 42)]);
        SaveDeletionAuthorization stale = AuthorizeDelete(store, "manual-1");
        store.Save("manual-1", "Replacement", [new TestSaveable("road_network", 99)]);

        Assert.Throws<InvalidDataException>(() => store.Delete(stale));

        var loaded = new TestSaveable("road_network", 0);
        Assert.Equal(1, store.Load("manual-1", [loaded]));
        Assert.Equal(99, loaded.Value);
    }

    [Fact]
    public void Delete_CleanupFailureAfterTombstoneReturnsPendingAndNeverRestoresSlot()
    {
        var normalStore = CreateStore();
        normalStore.Save("manual-1", "Manual", [new TestSaveable("road_network", 42)]);
        SaveDeletionAuthorization authorization = AuthorizeDelete(normalStore, "manual-1");
        var failingStore = new SaveSlotStore(V3Root, phase =>
        {
            if (phase == SavePublicationPhase.DeletionTombstoned)
                throw new IOException("Injected tombstone cleanup failure.");
        });

        SaveDeleteResult result = failingStore.Delete(authorization);

        Assert.Equal(SaveDeleteResultKind.DeletedWithCleanupPending, result.Kind);
        Assert.False(Directory.Exists(Path.Combine(V3Root, "manual-1")));
        Assert.False(normalStore.Exists("manual-1"));
        Assert.Empty(normalStore.ListSlots());
    }

    [Fact]
    public void Delete_DescriptorPublishedBeforeMoveIsRecoveredAsDeletion()
    {
        var normalStore = CreateStore();
        normalStore.Save("manual-1", "Manual", [new TestSaveable("road_network", 42)]);
        SaveDeletionAuthorization authorization = AuthorizeDelete(normalStore, "manual-1");
        var interruptedStore = new SaveSlotStore(V3Root, phase =>
        {
            if (phase == SavePublicationPhase.DeletionDescriptorPublished)
                throw new IOException("Simulated process interruption before tombstone move.");
        });

        Assert.Throws<IOException>(() => interruptedStore.Delete(authorization));
        Assert.True(Directory.Exists(Path.Combine(V3Root, "manual-1")));

        Assert.False(normalStore.Exists("manual-1"));
        Assert.False(Directory.Exists(Path.Combine(V3Root, "manual-1")));
        Assert.Empty(normalStore.ListSlots());
    }

    [Fact]
    public void Delete_RecoveryWithReplacementCanonicalBlocksAndPreservesEvidence()
    {
        var normalStore = CreateStore();
        normalStore.Save("manual-1", "Original", [new TestSaveable("road_network", 42)]);
        SaveDeletionAuthorization authorization = AuthorizeDelete(normalStore, "manual-1");
        var interruptedStore = new SaveSlotStore(V3Root, phase =>
        {
            if (phase == SavePublicationPhase.DeletionTombstoned)
                throw new IOException("Simulated interruption after tombstone move.");
        });
        SaveDeleteResult interrupted = interruptedStore.Delete(authorization);
        string operationDirectory = Path.Combine(
            V3Root, ".save-transactions", "manual-1", interrupted.OperationToken);
        string tombstoneDirectory = Path.Combine(operationDirectory, "tombstone");

        string replacementRoot = Path.Combine(_testRoot, "deletion-replacement-source");
        var replacementStore = new SaveSlotStore(replacementRoot);
        replacementStore.Save(
            "manual-1", "Replacement", [new TestSaveable("road_network", 99)]);
        Directory.Move(
            Path.Combine(replacementRoot, "manual-1"),
            Path.Combine(V3Root, "manual-1"));
        IReadOnlyDictionary<string, string> replacementBefore = SnapshotSlot("manual-1");
        string[] evidenceBefore = Directory.GetFileSystemEntries(
            operationDirectory, "*", SearchOption.AllDirectories);

        SaveDeletionRecoveryException exception = Assert.Throws<SaveDeletionRecoveryException>(
            () => normalStore.Exists("manual-1"));

        Assert.Contains("ambiguous", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(replacementBefore, SnapshotSlot("manual-1"));
        Assert.True(Directory.Exists(tombstoneDirectory));
        Assert.True(File.Exists(Path.Combine(operationDirectory, "delete.json")));
        Assert.Equal(
            evidenceBefore.Order(StringComparer.Ordinal),
            Directory.GetFileSystemEntries(operationDirectory, "*", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AllV3OperationsLeaveBothV2RootsByteAndTimestampIdentical()
    {
        CreateCanary(V2ResourceRoot, "resource-canary");
        CreateCanary(V2UserRoot, "user-canary");
        LegacyRootSnapshot resourceBefore = CaptureLegacyRoot(V2ResourceRoot);
        LegacyRootSnapshot userBefore = CaptureLegacyRoot(V2UserRoot);
        var store = CreateStore();
        var saveable = new TestSaveable("road_network", 42);

        Assert.Empty(store.ListSlots());
        store.Save("manual-1", "Manual", [saveable]);
        store.Load("manual-1", [saveable]);
        store.Delete(AuthorizeDelete(store, "manual-1"));
        store.Save(SaveManager.AutosaveSlotID, SaveManager.AutosaveDisplayName, [saveable]);
        store.ListSlots();

        Assert.Equal(resourceBefore, CaptureLegacyRoot(V2ResourceRoot));
        Assert.Equal(userBefore, CaptureLegacyRoot(V2UserRoot));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }

    private SaveSlotStore CreateStore() => new(V3Root);

    private static string ProjectRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SimpleCities.sln")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new DirectoryNotFoundException("SimpleCities project root was not found.");
    }

    private static SaveDeletionAuthorization AuthorizeDelete(SaveSlotStore store, string slotID)
    {
        SaveSlotSummary summary = Assert.Single(
            store.ListSlots(), candidate => candidate.SlotID == slotID);
        Assert.NotNull(summary.OccupantDigest);
        return new SaveDeletionAuthorization(
            slotID,
            1,
            Guid.NewGuid().ToString("N"),
            summary.OccupantKind,
            summary.OccupantDigest!,
            summary.IsValid ? summary.DisplayName : summary.SlotID);
    }

    private IReadOnlyDictionary<string, string> SnapshotSlot(string slotID) =>
        Directory.GetFiles(Path.Combine(V3Root, slotID))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToDictionary(
                path => Path.GetFileName(path)!,
                path => Convert.ToBase64String(File.ReadAllBytes(path)),
                StringComparer.Ordinal)!;

    private string[] TransactionDirectories() => Directory.Exists(V3Root)
        ? Directory.GetDirectories(V3Root, ".*", SearchOption.TopDirectoryOnly)
        : [];

    private PublicationFixture CreatePublicationFixture(string slotID, int replacementValue)
    {
        const string operationToken = "0123456789abcdef0123456789abcdef";
        string sourceRoot = Path.Combine(_testRoot, "replacement-source");
        var sourceStore = new SaveSlotStore(sourceRoot);
        sourceStore.Save(slotID, "Replacement", [new TestSaveable("road_network", replacementValue)]);

        string slotDirectory = Path.Combine(V3Root, slotID);
        V3Manifest oldManifest = CreateStore().ReadManifest(slotID);
        string oldDigest = SaveSlotStore.ComputeAggregateDigest(slotDirectory, oldManifest);
        string operationDirectory = Path.Combine(
            V3Root, ".save-transactions", slotID, operationToken);
        string stagingDirectory = Path.Combine(operationDirectory, "staging");
        string backupDirectory = Path.Combine(operationDirectory, "backup");
        Directory.CreateDirectory(operationDirectory);
        Directory.Move(Path.Combine(sourceRoot, slotID), stagingDirectory);
        using (var manifestStream = File.OpenRead(Path.Combine(stagingDirectory, "manifest.json")))
        {
            V3Manifest newManifest = V3ManifestCodec.Read(manifestStream);
            string newDigest = SaveSlotStore.ComputeAggregateDigest(stagingDirectory, newManifest);
            using var descriptor = new FileStream(
                Path.Combine(operationDirectory, "publish.json"),
                FileMode.CreateNew,
                System.IO.FileAccess.Write,
                FileShare.None);
            V3PublicationDescriptorCodec.Write(descriptor, new V3PublicationDescriptor(
                slotID,
                operationToken,
                oldDigest,
                newDigest,
                "staging",
                "backup"));
        }
        return new PublicationFixture(
            operationDirectory,
            slotDirectory,
            stagingDirectory,
            backupDirectory,
            Path.Combine(operationDirectory, "publish.json"));
    }

    private PublicationFixture CreateFirstSavePublicationFixture(string slotID, int value)
    {
        const string operationToken = "0123456789abcdef0123456789abcdef";
        string sourceRoot = Path.Combine(_testRoot, "first-save-source");
        var sourceStore = new SaveSlotStore(sourceRoot);
        sourceStore.Save(slotID, "First", [new TestSaveable("road_network", value)]);

        string slotDirectory = Path.Combine(V3Root, slotID);
        string operationDirectory = Path.Combine(
            V3Root, ".save-transactions", slotID, operationToken);
        string stagingDirectory = Path.Combine(operationDirectory, "staging");
        string backupDirectory = Path.Combine(operationDirectory, "backup");
        Directory.CreateDirectory(operationDirectory);
        Directory.Move(Path.Combine(sourceRoot, slotID), stagingDirectory);
        using (var manifestStream = File.OpenRead(Path.Combine(stagingDirectory, "manifest.json")))
        {
            V3Manifest manifest = V3ManifestCodec.Read(manifestStream);
            string digest = SaveSlotStore.ComputeAggregateDigest(stagingDirectory, manifest);
            using var descriptor = new FileStream(
                Path.Combine(operationDirectory, "publish.json"),
                FileMode.CreateNew,
                System.IO.FileAccess.Write,
                FileShare.None);
            V3PublicationDescriptorCodec.Write(descriptor, new V3PublicationDescriptor(
                slotID,
                operationToken,
                null,
                digest,
                "staging",
                "backup"));
        }
        return new PublicationFixture(
            operationDirectory,
            slotDirectory,
            stagingDirectory,
            backupDirectory,
            Path.Combine(operationDirectory, "publish.json"));
    }

    private void RewriteManifest(string slotID, Func<V3Manifest, V3Manifest> update)
    {
        string path = Path.Combine(V3Root, slotID, "manifest.json");
        V3Manifest manifest;
        using (var input = File.OpenRead(path))
            manifest = V3ManifestCodec.Read(input);
        using var output = new FileStream(
            path,
            FileMode.Create,
            System.IO.FileAccess.Write,
            FileShare.None);
        V3ManifestCodec.Write(output, update(manifest));
    }

    private void RewritePayloadAndManifest(string slotID, string payload)
    {
        string payloadPath = Path.Combine(V3Root, slotID, "road_network.json");
        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        File.WriteAllBytes(payloadPath, bytes);
        RewriteManifest(slotID, manifest => manifest with
        {
            Files = [new V3ManifestFile(
                "road_network.json",
                bytes.LongLength,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant())],
        });
    }

    private static void CreateCanary(string root, string content)
    {
        string slot = Path.Combine(root, "autosave");
        Directory.CreateDirectory(slot);
        File.WriteAllText(Path.Combine(slot, "manifest.json"), content);
        File.WriteAllText(Path.Combine(slot, "road_network.json"), content + "-road");
        DateTime timestamp = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        foreach (string path in Directory.GetFiles(slot))
            File.SetLastWriteTimeUtc(path, timestamp);
        Directory.SetLastWriteTimeUtc(slot, timestamp);
        Directory.SetLastWriteTimeUtc(root, timestamp);
    }

    private static LegacyRootSnapshot CaptureLegacyRoot(string root) => new(
        Directory.GetDirectories(root, "*", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetRelativePath(root, path))
            .Order(StringComparer.Ordinal)
            .ToArray(),
        Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(path => new LegacyFileSnapshot(
                Path.GetRelativePath(root, path),
                Convert.ToBase64String(File.ReadAllBytes(path)),
                File.GetLastWriteTimeUtc(path)))
            .ToArray(),
        Directory.GetLastWriteTimeUtc(root));

    private sealed record LegacyRootSnapshot(
        string[] Directories,
        LegacyFileSnapshot[] Files,
        DateTime LastWriteTimeUtc)
    {
        public bool Equals(LegacyRootSnapshot? other) =>
            other is not null &&
            Directories.SequenceEqual(other.Directories) &&
            Files.SequenceEqual(other.Files) &&
            LastWriteTimeUtc == other.LastWriteTimeUtc;

        public override int GetHashCode() => HashCode.Combine(Directories.Length, Files.Length, LastWriteTimeUtc);
    }

    private sealed record LegacyFileSnapshot(string Path, string Bytes, DateTime LastWriteTimeUtc);

    private sealed record PublicationFixture(
        string OperationDirectory,
        string SlotDirectory,
        string StagingDirectory,
        string BackupDirectory,
        string DescriptorPath);

    private sealed record TestSnapshot(int Value) : ISaveSnapshot;
    private sealed record TestPreparedState(int Value) : IPreparedSaveState;

    private class TestSaveable(string fileName, int value)
        : IStreamingSaveable, IStreamingLoadReader
    {
        public string SaveFileName { get; } = fileName;
        public int Value { get; set; } = value;
        public int CaptureCount { get; private set; }
        public int PrepareCount { get; private set; }
        public int CommitCount { get; private set; }
        public bool ThrowDuringPrepare { get; init; }
        public Action? DuringPrepare { get; init; }

        public virtual ISaveSnapshot CaptureSnapshot()
        {
            CaptureCount++;
            return new TestSnapshot(Value);
        }

        public virtual void WriteSnapshot(Stream destination, ISaveSnapshot snapshot)
        {
            var state = Assert.IsType<TestSnapshot>(snapshot);
            using var writer = new Utf8JsonWriter(destination);
            writer.WriteStartObject();
            writer.WriteNumber("value", state.Value);
            writer.WriteEndObject();
            writer.Flush();
        }

        public IStreamingLoadReader CaptureLoadReader() => this;

        public IPreparedSaveState PrepareLoad(Stream source)
        {
            PrepareCount++;
            if (ThrowDuringPrepare)
                throw new InvalidDataException("Injected preparation failure.");
            DuringPrepare?.Invoke();
            using JsonDocument document = JsonDocument.Parse(source);
            return new TestPreparedState(document.RootElement.GetProperty("value").GetInt32());
        }

        public void CommitPreparedLoad(IPreparedSaveState preparedState)
        {
            Value = Assert.IsType<TestPreparedState>(preparedState).Value;
            CommitCount++;
        }
    }

    private sealed class ThrowingCaptureSaveable : TestSaveable
    {
        internal ThrowingCaptureSaveable() : base("road_network", 0) { }
        public override ISaveSnapshot CaptureSnapshot() =>
            throw new InvalidOperationException("Injected capture failure.");
    }

    private sealed class ThrowingWriteSaveable : TestSaveable
    {
        internal ThrowingWriteSaveable() : base("road_network", 0) { }
        public override void WriteSnapshot(Stream destination, ISaveSnapshot snapshot) =>
            throw new JsonException("Injected serialization failure.");
    }

    private sealed class DiskFullAfterPartialWriteSaveable(int hResult)
        : TestSaveable("road_network", 0)
    {
        public override void WriteSnapshot(Stream destination, ISaveSnapshot snapshot)
        {
            destination.Write(Encoding.UTF8.GetBytes("{\"value\":"));
            destination.Flush();
            throw new IOException("Simulated disk exhaustion after a partial payload write.", hResult);
        }
    }
}
