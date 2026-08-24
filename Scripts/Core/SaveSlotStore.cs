using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal sealed class SaveSlotStore
{
    private const string ManifestFile = "manifest.json";
    private const string TransactionRootName = ".save-transactions";
    private const string QuarantineRootName = ".save-quarantine";
    private const string RootLockFile = ".save-root.lock";
    private const string PublicationDescriptorFile = "publish.json";
    private const string PublicationDescriptorTemporaryFile = ".publish.json.tmp";
    private const string DeletionDescriptorFile = "delete.json";
    private const string DeletionDescriptorTemporaryFile = ".delete.json.tmp";
    private const int MaximumOccupantDigestEntries = 100_000;
    internal const int MaxDisplayNameLength = V3ManifestCodec.MaximumTextScalars;
    internal const int MaxSlotIDLength = 128;

    private readonly string _saveBaseDir;
    private readonly Action<SavePublicationPhase>? _publicationObserver;

    public SaveSlotStore(
        string saveBaseDir,
        Action<SavePublicationPhase>? publicationObserver = null)
    {
        if (string.IsNullOrWhiteSpace(saveBaseDir))
            throw new ArgumentException("Save base directory cannot be empty.", nameof(saveBaseDir));
        _saveBaseDir = Path.GetFullPath(saveBaseDir);
        _publicationObserver = publicationObserver;
    }

    public string Create(string displayName, IReadOnlyList<IStreamingSaveable> saveables)
    {
        ValidateDisplayName(displayName);
        ArgumentNullException.ThrowIfNull(saveables);

        string slotID;
        do
        {
            slotID = $"manual-{Guid.NewGuid():N}";
        }
        while (Directory.Exists(GetSlotDir(slotID)));

        SavePublishResult result = Save(slotID, displayName, saveables);
        if (!result.IsPublished)
            throw new InvalidOperationException($"Save slot '{slotID}' was not published.");
        return slotID;
    }

    public SavePublishResult Save(
        string slotID,
        string displayName,
        IReadOnlyList<IStreamingSaveable> saveables)
    {
        return SaveCore(
            slotID,
            displayName,
            saveables,
            capturedParticipants: null,
            requireExisting: false,
            new UncoordinatedStorageOperationLease(SaveOperationKind.Publish));
    }

    internal SavePublishResult SaveCaptured(
        string slotID,
        string displayName,
        IReadOnlyList<CapturedSaveParticipant> capturedParticipants,
        IStorageOperationLease operationLease)
    {
        ArgumentNullException.ThrowIfNull(capturedParticipants);
        return SaveCore(
            slotID,
            displayName,
            capturedParticipants.Select(participant => participant.Saveable).ToArray(),
            capturedParticipants,
            requireExisting: false,
            operationLease);
    }

    internal SavePublishResult SaveCapturedExisting(
        string slotID,
        IReadOnlyList<CapturedSaveParticipant> capturedParticipants,
        IStorageOperationLease operationLease)
    {
        ArgumentNullException.ThrowIfNull(capturedParticipants);
        return SaveCore(
            slotID,
            displayName: null,
            capturedParticipants.Select(participant => participant.Saveable).ToArray(),
            capturedParticipants,
            requireExisting: true,
            operationLease);
    }

    internal static IReadOnlyList<CapturedSaveParticipant> CaptureSnapshots(
        IReadOnlyList<IStreamingSaveable> saveables)
    {
        ArgumentNullException.ThrowIfNull(saveables);
        IReadOnlyList<SaveParticipantDefinition> definitions = ValidateParticipants(saveables);
        return definitions.Select(definition => new CapturedSaveParticipant(
            definition.FileName,
            definition.Saveable,
            definition.Saveable.CaptureSnapshot())).ToArray();
    }

    private SavePublishResult SaveCore(
        string slotID,
        string? displayName,
        IReadOnlyList<IStreamingSaveable> saveables,
        IReadOnlyList<CapturedSaveParticipant>? capturedParticipants,
        bool requireExisting,
        IStorageOperationLease operationLease)
    {
        ValidateSlotID(slotID);
        if (displayName is not null)
            ValidateDisplayName(displayName);
        else if (!requireExisting)
            throw new ArgumentNullException(nameof(displayName));
        ArgumentNullException.ThrowIfNull(saveables);
        ArgumentNullException.ThrowIfNull(operationLease);
        if (operationLease.Kind is not SaveOperationKind.Publish and not SaveOperationKind.Autosave)
            throw new ArgumentException("A publish operation requires a publish or autosave lease.", nameof(operationLease));
        V3PublicationDescriptorCodec.ValidateOperationToken(operationLease.OperationToken);

        IReadOnlyList<SaveParticipantDefinition> participantDefinitions =
            ValidateParticipants(saveables);
        string slotDir = GetSlotDir(slotID);
        string operationToken = operationLease.OperationToken;
        string operationDir = GetOperationDir(slotID, operationToken);
        string stagingDir = Path.Combine(operationDir, V3PublicationDescriptorCodec.StagingPath);
        string backupDir = Path.Combine(operationDir, V3PublicationDescriptorCodec.BackupPath);

        EnsureRootDirectory();
        using FileStream rootLock = AcquireRootLock();
        RecoverInterruptedTransactions();
        operationLease.ThrowIfCancellationRequested();
        SaveSlotClassification occupant = ClassifySlotCore(slotID, slotDir);
        if (occupant.Kind is not SaveSlotOccupantKind.Absent and not SaveSlotOccupantKind.CompleteV3)
        {
            throw new InvalidDataException(
                $"Save slot '{slotID}' is occupied by {occupant.Kind} and cannot be overwritten.");
        }
        if (requireExisting && occupant.Kind != SaveSlotOccupantKind.CompleteV3)
            throw new InvalidDataException($"Save slot '{slotID}' does not exist as a complete V3 slot.");
        string effectiveDisplayName = displayName ?? occupant.Manifest!.DisplayName;

        IReadOnlyList<SaveParticipant> participants = capturedParticipants is null
            ? participantDefinitions.Select(definition => new SaveParticipant(
                definition.FileName,
                definition.Saveable,
                definition.Saveable.CaptureSnapshot())).ToArray()
            : MatchCapturedParticipants(participantDefinitions, capturedParticipants);
        operationLease.ThrowIfCancellationRequested();

        Directory.CreateDirectory(operationDir);
        Directory.CreateDirectory(stagingDir);
        try
        {
            var files = new List<V3ManifestFile>(participants.Count);
            foreach (SaveParticipant participant in participants)
            {
                operationLease.ThrowIfCancellationRequested();
                string path = Path.Combine(stagingDir, participant.FileName);
                using var stream = new FileStream(
                    path,
                    FileMode.CreateNew,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 64 * 1024,
                    FileOptions.SequentialScan);
                using (var cancellable = new V3OperationWriteStream(stream, operationLease))
                    participant.Saveable.WriteSnapshot(cancellable, participant.Snapshot);
                stream.Flush(flushToDisk: false);
                long length = stream.Length;
                stream.Position = 0;
                string sha256 = ComputeSha256(stream, operationLease);
                files.Add(new V3ManifestFile(participant.FileName, length, sha256));
            }

            operationLease.ThrowIfCancellationRequested();

            var manifest = new V3Manifest(
                slotID,
                effectiveDisplayName,
                V3ManifestCodec.CreateTimestamp(DateTime.UtcNow),
                "Unknown City",
                null,
                null,
                null,
                files.OrderBy(file => file.Name, StringComparer.Ordinal).ToArray());
            using (var manifestStream = new FileStream(
                       Path.Combine(stagingDir, ManifestFile),
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            {
                V3ManifestCodec.Write(manifestStream, manifest);
                manifestStream.Flush(flushToDisk: false);
            }

            SaveSlotClassification staged = ClassifySlotCore(slotID, stagingDir);
            if (staged.Kind != SaveSlotOccupantKind.CompleteV3)
                throw new InvalidDataException($"Staged V3 slot is invalid: {staged.Error}");

            string newDigest = ComputeAggregateDigest(stagingDir, staged.Manifest!);
            string? oldDigest = occupant.Kind == SaveSlotOccupantKind.CompleteV3
                ? ComputeAggregateDigest(slotDir, occupant.Manifest!)
                : null;
            WritePublicationDescriptor(operationDir, new V3PublicationDescriptor(
                slotID,
                operationToken,
                oldDigest,
                newDigest,
                V3PublicationDescriptorCodec.StagingPath,
                V3PublicationDescriptorCodec.BackupPath));

            _publicationObserver?.Invoke(SavePublicationPhase.Staged);
            operationLease.ThrowIfCancellationRequested();
            operationLease.AcquireCommitLease();
            _publicationObserver?.Invoke(SavePublicationPhase.CommitLeaseAcquired);
        }
        catch
        {
            TryDeleteTransactionDirectory(stagingDir);
            TryDeleteTransactionDirectory(operationDir);
            TryDeleteEmptyTransactionParents(slotID);
            throw;
        }

        bool previousSlotMoved = false;
        try
        {
            if (Directory.Exists(slotDir))
            {
                operationLease.CrossCommitBoundary(() => Directory.Move(slotDir, backupDir));
                previousSlotMoved = true;
                _publicationObserver?.Invoke(SavePublicationPhase.PreviousSlotMoved);
                Directory.Move(stagingDir, slotDir);
                operationLease.MarkCommitted();
            }
            else
            {
                operationLease.CrossCommitBoundary(() => Directory.Move(stagingDir, slotDir));
                operationLease.MarkCommitted();
            }
        }
        catch
        {
            if (previousSlotMoved)
                TryRestoreBackup(slotDir, backupDir);
            if (!Directory.Exists(slotDir) || previousSlotMoved)
            {
                TryDeleteTransactionDirectory(stagingDir);
                TryDeleteTransactionDirectory(operationDir);
                TryDeleteEmptyTransactionParents(slotID);
            }
            throw;
        }

        V3PublicationDescriptor descriptor = ReadPublicationDescriptor(operationDir);
        PathDigestState published = ReadPathDigestState(slotID, slotDir);
        if (!published.Matches(descriptor.NewAggregateDigest))
        {
            throw new SavePublicationRecoveryException(
                $"Published slot '{slotID}' does not match its descriptor digest; recovery evidence was preserved.");
        }

        try
        {
            _publicationObserver?.Invoke(SavePublicationPhase.CanonicalPublished);
            CleanupPublishedOperation(slotID, operationDir, stagingDir, backupDir);
            return new SavePublishResult(
                SavePublishResultKind.Published,
                slotID,
                operationToken,
                participants.Count);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new SavePublishResult(
                SavePublishResultKind.PublishedWithCleanupPending,
                slotID,
                operationToken,
                participants.Count,
                exception.Message);
        }
    }

    public int Load(string slotID, IReadOnlyList<IStreamingSaveable> saveables)
    {
        var operationLease = new UncoordinatedStorageOperationLease(SaveOperationKind.Load);
        PreparedSaveSlot prepared = PrepareLoad(slotID, saveables, operationLease);
        operationLease.ThrowIfCancellationRequested();
        operationLease.EnterCommitBoundary();
        return prepared.CommitLegacy();
    }

    internal PreparedSaveSlot PrepareLoad(
        string slotID,
        IReadOnlyList<IStreamingSaveable> saveables,
        IStorageOperationLease operationLease)
    {
        ArgumentNullException.ThrowIfNull(saveables);
        return PrepareLoad(
            slotID,
            CaptureLoadParticipants(saveables),
            operationLease);
    }

    internal PreparedSaveSlot PrepareLoad(
        string slotID,
        IReadOnlyList<CapturedLoadParticipant> loadParticipants,
        IStorageOperationLease operationLease)
    {
        ArgumentNullException.ThrowIfNull(loadParticipants);
        ArgumentNullException.ThrowIfNull(operationLease);
        if (operationLease.Kind != SaveOperationKind.Load)
            throw new ArgumentException("Preparing a load requires a load operation lease.", nameof(operationLease));
        string slotDir = GetSlotDir(slotID);
        EnsureRootDirectory();
        using FileStream rootLock = AcquireRootLock();
        RecoverInterruptedTransactions();
        operationLease.ThrowIfCancellationRequested();
        EnsureOrdinaryDirectory(slotDir, "slot");

        SlotEntrySnapshot firstEntries = CaptureSlotEntries(slotDir);
        string manifestPath = Path.Combine(slotDir, ManifestFile);
        if (!firstEntries.Files.Contains(ManifestFile, StringComparer.Ordinal))
            throw new InvalidDataException($"Save slot '{slotID}' does not contain a V3 manifest.");
        EnsureOrdinaryFile(manifestPath, "manifest");

        V3Manifest manifest;
        var payloadHandles = new List<(V3ManifestFile File, FileStream Handle)>();
        var prepared = new List<(IStreamingLoadTarget Target, IPreparedSaveState State)>();
        using (var manifestHandle = new FileStream(
                   manifestPath,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.Read,
                   bufferSize: 16 * 1024,
                   FileOptions.SequentialScan))
        {
            long manifestInitialLength = manifestHandle.Length;
            if (manifestInitialLength is <= 0 or > V3StorageBudget.MaximumManifestEncodedBytes)
                throw new InvalidDataException("V3 manifest exceeds its encoded byte budget.");
            bool declaresV3 = false;
            try
            {
                manifest = V3ManifestCodec.Read(manifestHandle, ref declaresV3);
            }
            catch (JsonException exception)
            {
                string kind = declaresV3 ? "corrupt V3" : "foreign";
                throw new InvalidDataException(
                    $"Save slot '{slotID}' has a {kind} manifest and cannot be loaded.",
                    exception);
            }
            if (manifestHandle.Position != manifestInitialLength ||
                manifestHandle.Length != manifestInitialLength)
            {
                throw new InvalidDataException("V3 manifest changed or was not consumed to EOF.");
            }
            if (!string.Equals(manifest.SlotID, slotID, StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"Manifest slotId '{manifest.SlotID}' does not match directory '{slotID}'.");

            ValidateSlotEntries(firstEntries, manifest);
            Dictionary<string, CapturedLoadParticipant> loadParticipantMap =
                MatchLoadParticipants(loadParticipants, manifest);
            prepared.Capacity = loadParticipantMap.Count;
            try
            {
                foreach (V3ManifestFile file in manifest.Files)
                {
                    operationLease.ThrowIfCancellationRequested();
                    string path = Path.Combine(slotDir, file.Name);
                    EnsureOrdinaryFile(path, $"payload '{file.Name}'");
                    var handle = new FileStream(
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 64 * 1024,
                        FileOptions.SequentialScan);
                    if (handle.Length != file.EncodedLength)
                    {
                        handle.Dispose();
                        throw new InvalidDataException(
                            $"Payload '{file.Name}' initial length does not match manifest.");
                    }
                    payloadHandles.Add((file, handle));
                }

                SlotEntrySnapshot secondEntries = CaptureSlotEntries(slotDir);
                ValidateSlotEntries(secondEntries, manifest);
                if (!firstEntries.Equals(secondEntries))
                    throw new InvalidDataException("Save slot file set changed while payload handles were opened.");

                foreach ((V3ManifestFile file, FileStream handle) in payloadHandles)
                {
                    operationLease.ThrowIfCancellationRequested();
                    using var verified = new V3VerifiedReadStream(handle, operationLease);
                    CapturedLoadParticipant participant = loadParticipantMap[file.Name];
                    IPreparedSaveState state = participant.Reader.PrepareLoad(verified);
                    verified.VerifyComplete(file);
                    prepared.Add((participant.Target, state));
                }

                SlotEntrySnapshot thirdEntries = CaptureSlotEntries(slotDir);
                ValidateSlotEntries(thirdEntries, manifest);
                if (!firstEntries.Equals(thirdEntries))
                    throw new InvalidDataException("Save slot file set changed during payload preparation.");
            }
            finally
            {
                foreach ((_, FileStream handle) in payloadHandles)
                    handle.Dispose();
            }
        }

        operationLease.ThrowIfCancellationRequested();
        return new PreparedSaveSlot(
            slotID,
            prepared.Select(item => new PreparedSaveParticipant(item.Target, item.State)));
    }

    internal static IReadOnlyList<CapturedLoadParticipant> CaptureLoadParticipants(
        IReadOnlyList<IStreamingSaveable> saveables)
    {
        ArgumentNullException.ThrowIfNull(saveables);
        IReadOnlyList<SaveParticipantDefinition> definitions = ValidateParticipants(saveables);
        return definitions.Select(definition => new CapturedLoadParticipant(
            definition.FileName,
            definition.Saveable,
            definition.Saveable.CaptureLoadReader())).ToArray();
    }

    private static Dictionary<string, CapturedLoadParticipant> MatchLoadParticipants(
        IReadOnlyList<CapturedLoadParticipant> loadParticipants,
        V3Manifest manifest)
    {
        var manifestFiles = manifest.Files.ToDictionary(file => file.Name, StringComparer.Ordinal);
        var participantMap = new Dictionary<string, CapturedLoadParticipant>(StringComparer.Ordinal);
        foreach (CapturedLoadParticipant participant in loadParticipants)
        {
            ArgumentNullException.ThrowIfNull(participant);
            ArgumentNullException.ThrowIfNull(participant.Target);
            ArgumentNullException.ThrowIfNull(participant.Reader);
            string fileName = GetDataFileName(participant.Target.SaveFileName);
            if (!string.Equals(fileName, participant.FileName, StringComparison.Ordinal))
                throw new InvalidDataException($"Captured load reader name '{participant.FileName}' is stale.");
            if (!participantMap.TryAdd(fileName, participant))
                throw new InvalidDataException($"Multiple saveables require file '{fileName}'.");
            if (!manifestFiles.ContainsKey(fileName))
                throw new InvalidDataException($"Manifest does not contain required file '{fileName}'.");
        }
        if (participantMap.Count != manifestFiles.Count)
            throw new InvalidDataException("Manifest declares an unsupported business payload.");
        return participantMap;
    }

    private static SlotEntrySnapshot CaptureSlotEntries(string slotDir)
    {
        string[] directories = Directory.EnumerateDirectories(slotDir, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray()!;
        string[] files = Directory.EnumerateFiles(slotDir, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray()!;
        return new SlotEntrySnapshot(directories, files);
    }

    private static void ValidateSlotEntries(SlotEntrySnapshot entries, V3Manifest manifest)
    {
        if (entries.Directories.Length != 0)
            throw new InvalidDataException("Save slot contains a subdirectory.");
        var expected = new HashSet<string>(StringComparer.Ordinal) { ManifestFile };
        foreach (V3ManifestFile file in manifest.Files)
            expected.Add(file.Name);
        var actual = new HashSet<string>(entries.Files, StringComparer.Ordinal);
        if (manifest.ThumbnailFile is not null)
        {
            string thumbnail = manifest.ThumbnailFile;
            if (actual.Contains(thumbnail))
                expected.Add(thumbnail);
        }
        if (!expected.SetEquals(actual))
            throw new InvalidDataException("Save slot file set does not match manifest.");
    }

    internal static void EnsureOrdinaryFile(string path, string kind)
    {
        if (!File.Exists(path))
            throw new InvalidDataException($"Save {kind} is missing.");
        if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
            throw new IOException($"Save {kind} cannot be a filesystem link.");
    }

    public V3Manifest ReadManifest(string slotID)
    {
        string slotDir = GetSlotDir(slotID);
        EnsureRootDirectory();
        using FileStream rootLock = AcquireRootLock();
        RecoverInterruptedTransactions();
        SaveSlotClassification classification = ClassifySlotCore(slotID, slotDir);
        if (classification.Kind != SaveSlotOccupantKind.CompleteV3 || classification.Manifest is null)
        {
            throw new InvalidDataException(
                $"Save slot '{slotID}' is not a complete V3 slot: {classification.Kind}.");
        }
        return classification.Manifest;
    }

    public IReadOnlyList<SaveSlotSummary> ListSlots()
    {
        if (!Directory.Exists(_saveBaseDir))
            return Array.Empty<SaveSlotSummary>();
        EnsureRootDirectory();
        using FileStream rootLock = AcquireRootLock();
        RecoverInterruptedTransactions();

        var summaries = new List<SaveSlotSummary>();
        foreach (string slotDir in Directory.EnumerateDirectories(_saveBaseDir, "*", SearchOption.TopDirectoryOnly))
        {
            string slotID = Path.GetFileName(slotDir);
            if (string.Equals(slotID, TransactionRootName, StringComparison.Ordinal))
                continue;

            SaveSlotClassification classification;
            try
            {
                classification = ClassifySlotCore(slotID, slotDir);
            }
            catch (ArgumentException)
            {
                continue;
            }
            if (classification.Kind is SaveSlotOccupantKind.Foreign or
                SaveSlotOccupantKind.Unsafe or SaveSlotOccupantKind.Absent)
            {
                continue;
            }

            if (classification.Kind == SaveSlotOccupantKind.CompleteV3 &&
                classification.Manifest is V3Manifest manifest)
            {
                DateTimeOffset savedAtUtc = DateTimeOffset.ParseExact(
                    manifest.Timestamp,
                    "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                summaries.Add(new SaveSlotSummary
                {
                    SlotID = slotID,
                    DisplayName = manifest.DisplayName,
                    SavedAtUtc = savedAtUtc,
                    CityName = manifest.CityName,
                    Population = manifest.Population,
                    Funds = manifest.Funds,
                    ThumbnailPath = ResolveThumbnailPath(
                        slotDir,
                        manifest.ThumbnailFile,
                        out string? thumbnailWarning),
                    Warning = thumbnailWarning,
                    Files = manifest.Files.Select(file => file.Name).ToArray(),
                    IsValid = true,
                    OccupantKind = SaveSlotOccupantKind.CompleteV3,
                    OccupantDigest = ComputeAggregateDigest(slotDir, manifest),
                });
            }
            else
            {
                summaries.Add(new SaveSlotSummary
                {
                    SlotID = slotID,
                    DisplayName = slotID,
                    IsValid = false,
                    Error = classification.Error ?? "V3 slot is corrupt.",
                    OccupantKind = SaveSlotOccupantKind.CorruptV3,
                    OccupantDigest = ComputeRawDirectoryDigest(slotDir),
                });
            }
        }

        return summaries
            .OrderByDescending(summary => summary.IsValid)
            .ThenByDescending(summary => summary.SavedAtUtc)
            .ThenBy(summary => summary.SlotID, StringComparer.Ordinal)
            .ToArray();
    }

    public SaveSlotClassification ClassifySlot(string slotID)
    {
        string slotDir = GetSlotDir(slotID);
        if (!Directory.Exists(_saveBaseDir))
            return ClassifySlotCore(slotID, slotDir);
        EnsureRootDirectory();
        using FileStream rootLock = AcquireRootLock();
        RecoverInterruptedTransactions();
        return ClassifySlotCore(slotID, slotDir);
    }

    public bool Exists(string slotID) =>
        ClassifySlot(slotID).Kind == SaveSlotOccupantKind.CompleteV3;

    public SaveDeleteResult Delete(SaveDeletionAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        return Delete(
            authorization,
            new UncoordinatedStorageOperationLease(
                SaveOperationKind.Delete,
                authorization.OperationToken));
    }

    internal SaveDeleteResult Delete(
        SaveDeletionAuthorization authorization,
        IStorageOperationLease operationLease)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(operationLease);
        if (operationLease.Kind != SaveOperationKind.Delete)
            throw new ArgumentException("Deleting a slot requires a delete operation lease.", nameof(operationLease));
        if (!string.Equals(
                operationLease.OperationToken,
                authorization.OperationToken,
                StringComparison.Ordinal))
        {
            throw new ArgumentException("Delete lease token does not match its authorization.", nameof(operationLease));
        }
        ValidateSlotID(authorization.SlotID);
        V3PublicationDescriptorCodec.ValidateOperationToken(authorization.OperationToken);
        if (authorization.UIGeneration <= 0)
            throw new ArgumentException("Deletion UI generation must be positive.", nameof(authorization));
        string slotID = authorization.SlotID;
        string slotDir = GetSlotDir(slotID);
        if (!Directory.Exists(_saveBaseDir))
            throw new InvalidDataException($"Save slot '{slotID}' does not exist.");
        EnsureRootDirectory();
        using FileStream rootLock = AcquireRootLock();
        RecoverInterruptedTransactions();
        operationLease.ThrowIfCancellationRequested();
        SaveSlotClassification classification = ClassifySlotCore(slotID, slotDir);
        if (classification.Kind is not SaveSlotOccupantKind.CompleteV3 and
            not SaveSlotOccupantKind.CorruptV3)
        {
            throw new InvalidDataException(
                $"Save slot '{slotID}' is {classification.Kind} and cannot be deleted by DeleteV3.");
        }
        if (classification.Kind != authorization.OccupantKind)
            throw new InvalidDataException("Deletion authority occupant kind is stale.");
        string occupantDigest = ComputeOccupantDigest(slotDir, classification);
        if (!string.Equals(occupantDigest, authorization.OccupantDigest, StringComparison.Ordinal))
            throw new InvalidDataException("Deletion authority occupant digest is stale.");

        string operationDir = GetOperationDir(slotID, authorization.OperationToken);
        string tombstoneDir = Path.Combine(operationDir, V3DeletionDescriptorCodec.TombstonePath);
        Directory.CreateDirectory(operationDir);
        var descriptor = new V3DeletionDescriptor(
            slotID,
            authorization.OperationToken,
            authorization.UIGeneration,
            authorization.OccupantKind,
            authorization.OccupantDigest,
            V3DeletionDescriptorCodec.TombstonePath,
            authorization.ConfirmationSummary);
        WriteDeletionDescriptor(operationDir, descriptor);
        _publicationObserver?.Invoke(SavePublicationPhase.DeletionDescriptorPublished);
        operationLease.ThrowIfCancellationRequested();
        operationLease.AcquireCommitLease();

        operationLease.CrossCommitBoundary(() => Directory.Move(slotDir, tombstoneDir));
        operationLease.MarkCommitted();
        try
        {
            _publicationObserver?.Invoke(SavePublicationPhase.DeletionTombstoned);
            TryDeleteTransactionDirectory(tombstoneDir);
            TryDeleteTransactionDirectory(operationDir);
            TryDeleteEmptyTransactionParents(slotID);
            return new SaveDeleteResult(
                SaveDeleteResultKind.Deleted,
                slotID,
                authorization.OperationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new SaveDeleteResult(
                SaveDeleteResultKind.DeletedWithCleanupPending,
                slotID,
                authorization.OperationToken,
                exception.Message);
        }
    }

    private static IReadOnlyList<SaveParticipantDefinition> ValidateParticipants(
        IReadOnlyList<IStreamingSaveable> saveables)
    {
        var participants = new List<SaveParticipantDefinition>(saveables.Count);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (IStreamingSaveable saveable in saveables)
        {
            ArgumentNullException.ThrowIfNull(saveable);
            string fileName = GetDataFileName(saveable.SaveFileName);
            if (string.Equals(fileName, ManifestFile, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Save file name 'manifest' is reserved.", nameof(saveables));
            if (!names.Add(fileName))
                throw new ArgumentException($"Duplicate save file name '{fileName}'.", nameof(saveables));
            participants.Add(new SaveParticipantDefinition(fileName, saveable));
        }
        return participants.OrderBy(participant => participant.FileName, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<SaveParticipant> MatchCapturedParticipants(
        IReadOnlyList<SaveParticipantDefinition> definitions,
        IReadOnlyList<CapturedSaveParticipant> capturedParticipants)
    {
        if (definitions.Count != capturedParticipants.Count)
            throw new ArgumentException("Captured participant count does not match saveables.", nameof(capturedParticipants));

        var capturedByName = new Dictionary<string, CapturedSaveParticipant>(StringComparer.Ordinal);
        foreach (CapturedSaveParticipant captured in capturedParticipants)
        {
            ArgumentNullException.ThrowIfNull(captured);
            ArgumentNullException.ThrowIfNull(captured.Saveable);
            ArgumentNullException.ThrowIfNull(captured.Snapshot);
            if (!capturedByName.TryAdd(captured.FileName, captured))
                throw new ArgumentException($"Duplicate captured file name '{captured.FileName}'.", nameof(capturedParticipants));
        }

        var participants = new List<SaveParticipant>(definitions.Count);
        foreach (SaveParticipantDefinition definition in definitions)
        {
            if (!capturedByName.TryGetValue(definition.FileName, out CapturedSaveParticipant? captured) ||
                !ReferenceEquals(definition.Saveable, captured.Saveable))
            {
                throw new ArgumentException(
                    $"Captured participant '{definition.FileName}' does not match its active saveable.",
                    nameof(capturedParticipants));
            }
            participants.Add(new SaveParticipant(
                definition.FileName,
                definition.Saveable,
                captured.Snapshot));
        }
        return participants;
    }

    private SaveSlotClassification ClassifySlotCore(string slotID, string slotDir)
    {
        if (!Directory.Exists(slotDir))
        {
            return File.Exists(slotDir)
                ? new SaveSlotClassification(
                    SaveSlotOccupantKind.Unsafe,
                    Error: "Slot path is occupied by a file.")
                : new SaveSlotClassification(SaveSlotOccupantKind.Absent);
        }
        try
        {
            if (File.GetAttributes(slotDir).HasFlag(FileAttributes.ReparsePoint))
                return new SaveSlotClassification(SaveSlotOccupantKind.Unsafe, Error: "Slot is a filesystem link.");

            string manifestPath = Path.Combine(slotDir, ManifestFile);
            if (!File.Exists(manifestPath))
                return new SaveSlotClassification(SaveSlotOccupantKind.Foreign, Error: "V3 manifest is missing.");
            if (File.GetAttributes(manifestPath).HasFlag(FileAttributes.ReparsePoint))
                return new SaveSlotClassification(SaveSlotOccupantKind.Unsafe, Error: "Manifest is a filesystem link.");

            V3Manifest manifest;
            using (var stream = new FileStream(
                       manifestPath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read,
                       bufferSize: 16 * 1024,
                       FileOptions.SequentialScan))
            {
                bool declaresV3 = false;
                try
                {
                    manifest = V3ManifestCodec.Read(stream, ref declaresV3);
                }
                catch (Exception exception) when (exception is JsonException or InvalidDataException)
                {
                    return new SaveSlotClassification(
                        declaresV3 ? SaveSlotOccupantKind.CorruptV3 : SaveSlotOccupantKind.Foreign,
                        Error: exception.Message);
                }
            }

            if (!string.Equals(manifest.SlotID, slotID, StringComparison.Ordinal))
            {
                return new SaveSlotClassification(
                    SaveSlotOccupantKind.CorruptV3,
                    Error: $"Manifest slotId '{manifest.SlotID}' does not match directory '{slotID}'.");
            }

            var allowedFiles = new HashSet<string>(StringComparer.Ordinal) { ManifestFile };
            foreach (V3ManifestFile file in manifest.Files)
                allowedFiles.Add(file.Name);
            if (manifest.ThumbnailFile is not null)
                allowedFiles.Add(manifest.ThumbnailFile);

            string[] directories = Directory.EnumerateDirectories(slotDir, "*", SearchOption.TopDirectoryOnly)
                .ToArray();
            if (directories.Length != 0)
                return new SaveSlotClassification(SaveSlotOccupantKind.CorruptV3, Error: "Slot contains a subdirectory.");
            string[] actualFiles = Directory.EnumerateFiles(slotDir, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .ToArray()!;
            var actualFileSet = new HashSet<string>(actualFiles, StringComparer.Ordinal);
            bool fileSetMatches = manifest.ThumbnailFile is null
                ? allowedFiles.SetEquals(actualFileSet)
                : actualFileSet.IsSubsetOf(allowedFiles) &&
                  manifest.Files.All(file => actualFileSet.Contains(file.Name)) &&
                  actualFileSet.Contains(ManifestFile);
            if (!fileSetMatches)
                return new SaveSlotClassification(SaveSlotOccupantKind.CorruptV3, Error: "Slot file set does not match manifest.");

            foreach (V3ManifestFile file in manifest.Files)
            {
                string path = Path.Combine(slotDir, file.Name);
                if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
                    return new SaveSlotClassification(SaveSlotOccupantKind.Unsafe, Error: $"Payload '{file.Name}' is a filesystem link.");
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 64 * 1024,
                    FileOptions.SequentialScan);
                ValidatePayloadStream(stream, file);
            }

            return new SaveSlotClassification(SaveSlotOccupantKind.CompleteV3, manifest);
        }
        catch (InvalidDataException exception)
        {
            return new SaveSlotClassification(SaveSlotOccupantKind.CorruptV3, Error: exception.Message);
        }
        catch (JsonException exception)
        {
            return new SaveSlotClassification(SaveSlotOccupantKind.CorruptV3, Error: exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new SaveSlotClassification(SaveSlotOccupantKind.Unsafe, Error: exception.Message);
        }
    }

    private static void ValidatePayloadStream(Stream stream, V3ManifestFile file)
    {
        if (stream.Length != file.EncodedLength)
            throw new InvalidDataException($"Payload '{file.Name}' length does not match manifest.");
        stream.Position = 0;
        string actualHash = ComputeSha256(stream);
        if (!string.Equals(actualHash, file.Sha256, StringComparison.Ordinal))
            throw new InvalidDataException($"Payload '{file.Name}' SHA-256 does not match manifest.");
    }

    private static string ComputeSha256(Stream stream) =>
        Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();

    private static string ComputeSha256(
        Stream stream,
        IStorageOperationLease operationLease)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(operationLease);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[64 * 1024];
        while (true)
        {
            operationLease.ThrowIfCancellationRequested();
            int read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0)
                break;
            hash.AppendData(buffer, 0, read);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    internal static string ComputeAggregateDigest(string slotDir, V3Manifest manifest)
    {
        var fileNames = new List<string>(manifest.Files.Count + 2) { ManifestFile };
        fileNames.AddRange(manifest.Files.Select(file => file.Name));
        fileNames.Sort(StringComparer.Ordinal);

        using IncrementalHash aggregate = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> lengthBytes = stackalloc byte[sizeof(long)];
        foreach (string fileName in fileNames)
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(fileName);
            BinaryPrimitives.WriteInt64BigEndian(lengthBytes, nameBytes.LongLength);
            aggregate.AppendData(lengthBytes);
            aggregate.AppendData(nameBytes);

            string path = Path.Combine(slotDir, fileName);
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.SequentialScan);
            BinaryPrimitives.WriteInt64BigEndian(lengthBytes, stream.Length);
            aggregate.AppendData(lengthBytes);
            byte[] fileDigest = SHA256.HashData(stream);
            aggregate.AppendData(fileDigest);
        }
        return Convert.ToHexString(aggregate.GetHashAndReset()).ToLowerInvariant();
    }

    private static string ComputeRawDirectoryDigest(string directory)
    {
        EnsureOrdinaryDirectory(directory, "occupant");
        string root = Path.GetFullPath(directory);
        string[] entries = Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
            .Take(MaximumOccupantDigestEntries + 1)
            .ToArray();
        if (entries.Length > MaximumOccupantDigestEntries)
            throw new InvalidDataException("Save occupant exceeds the deletion digest entry budget.");

        using IncrementalHash aggregate = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> lengthBytes = stackalloc byte[sizeof(long)];
        foreach (string entry in entries.OrderBy(
                     path => Path.GetRelativePath(root, path),
                     StringComparer.Ordinal))
        {
            string relative = Path.GetRelativePath(root, entry);
            if (relative == "." || relative.StartsWith("..", StringComparison.Ordinal))
                throw new IOException("Save occupant entry resolves outside its root.");
            FileAttributes attributes = File.GetAttributes(entry);
            if (attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new IOException("Save occupant contains a filesystem link.");

            bool isDirectory = attributes.HasFlag(FileAttributes.Directory);
            aggregate.AppendData([isDirectory ? (byte)'D' : (byte)'F']);
            byte[] pathBytes = Encoding.UTF8.GetBytes(relative.Replace('\\', '/'));
            BinaryPrimitives.WriteInt64BigEndian(lengthBytes, pathBytes.LongLength);
            aggregate.AppendData(lengthBytes);
            aggregate.AppendData(pathBytes);
            if (isDirectory)
                continue;

            using var stream = new FileStream(
                entry,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.SequentialScan);
            BinaryPrimitives.WriteInt64BigEndian(lengthBytes, stream.Length);
            aggregate.AppendData(lengthBytes);
            aggregate.AppendData(SHA256.HashData(stream));
        }
        return Convert.ToHexString(aggregate.GetHashAndReset()).ToLowerInvariant();
    }

    private static string ComputeOccupantDigest(string slotDir, SaveSlotClassification classification) =>
        classification.Kind == SaveSlotOccupantKind.CompleteV3 && classification.Manifest is not null
            ? ComputeAggregateDigest(slotDir, classification.Manifest)
            : classification.Kind == SaveSlotOccupantKind.CorruptV3
                ? ComputeRawDirectoryDigest(slotDir)
                : throw new InvalidDataException(
                    $"Occupant kind '{classification.Kind}' cannot receive deletion authority.");

    private void EnsureRootDirectory()
    {
        Directory.CreateDirectory(_saveBaseDir);
        if (File.GetAttributes(_saveBaseDir).HasFlag(FileAttributes.ReparsePoint))
            throw new IOException("V3 save root cannot be a filesystem link.");
    }

    private FileStream AcquireRootLock()
    {
        string path = Path.Combine(_saveBaseDir, RootLockFile);
        try
        {
            return new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.None);
        }
        catch (IOException exception)
        {
            throw new SaveRootBusyException("The V3 save root is locked by another operation or process.", exception);
        }
    }

    private string GetSlotDir(string slotID)
    {
        ValidateSlotID(slotID);
        string slotDir = Path.GetFullPath(Path.Combine(_saveBaseDir, slotID));
        string relativePath = Path.GetRelativePath(_saveBaseDir, slotDir);
        if (!string.Equals(relativePath, slotID, StringComparison.Ordinal))
            throw new InvalidOperationException($"Save slot '{slotID}' resolves outside the save root.");
        return slotDir;
    }

    private string GetOperationDir(string slotID, string operationToken)
    {
        ValidateSlotID(slotID);
        V3PublicationDescriptorCodec.ValidateOperationToken(operationToken);
        string relativeName = Path.Combine(TransactionRootName, slotID, operationToken);
        string operationDir = Path.GetFullPath(Path.Combine(_saveBaseDir, relativeName));
        string relativePath = Path.GetRelativePath(_saveBaseDir, operationDir);
        if (!string.Equals(relativePath, relativeName, StringComparison.Ordinal))
            throw new InvalidOperationException($"Transaction path for slot '{slotID}' resolves outside the save root.");
        return operationDir;
    }

    private void RecoverInterruptedTransactions()
    {
        string transactionRoot = Path.Combine(_saveBaseDir, TransactionRootName);
        if (!Directory.Exists(transactionRoot))
        {
            if (File.Exists(transactionRoot))
                throw new SavePublicationRecoveryException("Publication transaction root is occupied by a file.");
            return;
        }
        EnsureOrdinaryDirectory(transactionRoot, "transaction root");

        string[] slotDirectories = Directory.GetDirectories(transactionRoot)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (Directory.GetFiles(transactionRoot).Length != 0)
            throw new SavePublicationRecoveryException("Publication transaction root contains an unexpected file.");
        foreach (string slotTransactions in slotDirectories)
        {
            string slotID = Path.GetFileName(slotTransactions);
            try
            {
                ValidateSlotID(slotID);
            }
            catch (ArgumentException exception)
            {
                throw new SavePublicationRecoveryException(
                    $"Publication transaction slot directory '{slotID}' is invalid.", exception);
            }
            EnsureOrdinaryDirectory(slotTransactions, "slot transaction");
            if (Directory.GetFiles(slotTransactions).Length != 0)
                throw new SavePublicationRecoveryException(
                    $"Publication transaction slot '{slotID}' contains an unexpected file.");

            foreach (string operationDir in Directory.GetDirectories(slotTransactions)
                         .Order(StringComparer.Ordinal))
            {
                RecoverPublicationOperation(slotID, operationDir);
            }
            TryDeleteEmptyDirectory(slotTransactions);
        }
        TryDeleteEmptyDirectory(transactionRoot);
    }

    private void RecoverPublicationOperation(string slotID, string operationDir)
    {
        EnsureOrdinaryDirectory(operationDir, "publication operation");
        string operationToken = Path.GetFileName(operationDir);
        try
        {
            V3PublicationDescriptorCodec.ValidateOperationToken(operationToken);
        }
        catch (JsonException exception)
        {
            throw new SavePublicationRecoveryException(
                $"Publication operation directory '{operationToken}' is invalid.", exception);
        }

        string descriptorPath = Path.Combine(operationDir, PublicationDescriptorFile);
        string deletionDescriptorPath = Path.Combine(operationDir, DeletionDescriptorFile);
        bool hasPublishDescriptor = File.Exists(descriptorPath);
        bool hasDeleteDescriptor = File.Exists(deletionDescriptorPath);
        if (hasPublishDescriptor && hasDeleteDescriptor)
            throw new SavePublicationRecoveryException("Transaction contains both publish and delete descriptors.");
        if (hasDeleteDescriptor)
        {
            RecoverDeletionOperation(slotID, operationToken, operationDir, deletionDescriptorPath);
            return;
        }
        if (!hasPublishDescriptor)
        {
            QuarantineOperation(slotID, operationToken, operationDir);
            return;
        }
        if (File.GetAttributes(descriptorPath).HasFlag(FileAttributes.ReparsePoint))
            throw new SavePublicationRecoveryException("Publication descriptor is a filesystem link.");

        V3PublicationDescriptor descriptor;
        try
        {
            using var descriptorStream = new FileStream(
                descriptorPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4 * 1024,
                FileOptions.SequentialScan);
            descriptor = V3PublicationDescriptorCodec.Read(descriptorStream);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or IOException)
        {
            throw new SavePublicationRecoveryException("Publication descriptor is invalid.", exception);
        }
        if (descriptor.SlotID != slotID || descriptor.OperationToken != operationToken)
            throw new SavePublicationRecoveryException("Publication descriptor identity does not match its path.");

        string slotDir = GetSlotDir(slotID);
        string stagingDir = Path.Combine(operationDir, descriptor.StagingPath);
        string backupDir = Path.Combine(operationDir, descriptor.BackupPath);
        PathDigestState canonical = ReadPathDigestState(slotID, slotDir);
        PathDigestState staging = ReadPathDigestState(slotID, stagingDir);
        PathDigestState backup = ReadPathDigestState(slotID, backupDir);
        bool canonicalNew = canonical.Matches(descriptor.NewAggregateDigest);
        bool canonicalOld = descriptor.OldAggregateDigest is not null &&
                            canonical.Matches(descriptor.OldAggregateDigest);
        bool stagingNew = staging.Matches(descriptor.NewAggregateDigest);
        bool backupOld = descriptor.OldAggregateDigest is not null &&
                         backup.Matches(descriptor.OldAggregateDigest);

        bool stagingConsistentWithNew = staging.Kind == PathDigestKind.Absent || stagingNew;
        bool backupConsistentWithOld = backup.Kind == PathDigestKind.Absent || backupOld;
        if (canonicalNew && stagingConsistentWithNew && backupConsistentWithOld)
        {
            CleanupPublishedOperation(slotID, operationDir, stagingDir, backupDir);
            return;
        }
        if (canonicalOld && stagingNew && backup.Kind == PathDigestKind.Absent)
        {
            QuarantineOperation(slotID, operationToken, operationDir);
            return;
        }
        if (canonical.Kind == PathDigestKind.Absent && backupOld && stagingNew)
        {
            Directory.Move(stagingDir, slotDir);
            PathDigestState published = ReadPathDigestState(slotID, slotDir);
            if (!published.Matches(descriptor.NewAggregateDigest))
                throw new SavePublicationRecoveryException("Recovered publication does not match its new digest.");
            CleanupPublishedOperation(slotID, operationDir, stagingDir, backupDir);
            return;
        }
        if (canonical.Kind == PathDigestKind.Absent && backupOld &&
            staging.Kind == PathDigestKind.Absent)
        {
            Directory.Move(backupDir, slotDir);
            TryDeleteTransactionDirectory(operationDir);
            TryDeleteEmptyTransactionParents(slotID);
            return;
        }
        if (descriptor.OldAggregateDigest is null &&
            canonical.Kind == PathDigestKind.Absent && stagingNew &&
            backup.Kind == PathDigestKind.Absent)
        {
            QuarantineOperation(slotID, operationToken, operationDir);
            return;
        }

        throw new SavePublicationRecoveryException(
            $"Publication recovery is ambiguous for slot '{slotID}' operation '{operationToken}'.");
    }

    private void RecoverDeletionOperation(
        string slotID,
        string operationToken,
        string operationDir,
        string descriptorPath)
    {
        if (File.GetAttributes(descriptorPath).HasFlag(FileAttributes.ReparsePoint))
            throw new SaveDeletionRecoveryException("Deletion descriptor is a filesystem link.");
        V3DeletionDescriptor descriptor;
        try
        {
            using var stream = new FileStream(
                descriptorPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4 * 1024,
                FileOptions.SequentialScan);
            descriptor = V3DeletionDescriptorCodec.Read(stream);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or IOException)
        {
            throw new SaveDeletionRecoveryException("Deletion descriptor is invalid.", exception);
        }
        if (descriptor.SlotID != slotID || descriptor.OperationToken != operationToken)
            throw new SaveDeletionRecoveryException("Deletion descriptor identity does not match its path.");

        string slotDir = GetSlotDir(slotID);
        string tombstoneDir = Path.Combine(operationDir, descriptor.TombstonePath);
        OccupantDigestState canonical = ReadDeletionDigestState(
            slotID, slotDir, descriptor.OccupantKind);
        OccupantDigestState tombstone = ReadDeletionDigestState(
            slotID, tombstoneDir, descriptor.OccupantKind);
        bool canonicalMatches = canonical.Matches(descriptor.OccupantDigest);
        bool tombstoneMatches = tombstone.Matches(descriptor.OccupantDigest);

        if (canonicalMatches && tombstone.Kind == OccupantDigestKind.Absent)
        {
            Directory.Move(slotDir, tombstoneDir);
            tombstone = ReadDeletionDigestState(slotID, tombstoneDir, descriptor.OccupantKind);
            if (!tombstone.Matches(descriptor.OccupantDigest))
                throw new SaveDeletionRecoveryException("Deletion tombstone does not match its descriptor digest.");
            TryDeleteTransactionDirectory(tombstoneDir);
            TryDeleteTransactionDirectory(operationDir);
            TryDeleteEmptyTransactionParents(slotID);
            return;
        }
        if (canonical.Kind == OccupantDigestKind.Absent && tombstoneMatches)
        {
            TryDeleteTransactionDirectory(tombstoneDir);
            TryDeleteTransactionDirectory(operationDir);
            TryDeleteEmptyTransactionParents(slotID);
            return;
        }

        throw new SaveDeletionRecoveryException(
            $"Deletion recovery is ambiguous for slot '{slotID}' operation '{operationToken}'.");
    }

    private static void WritePublicationDescriptor(
        string operationDir,
        V3PublicationDescriptor descriptor)
    {
        string temporaryPath = Path.Combine(operationDir, PublicationDescriptorTemporaryFile);
        string finalPath = Path.Combine(operationDir, PublicationDescriptorFile);
        using (var stream = new FileStream(
                   temporaryPath,
                   FileMode.CreateNew,
                   FileAccess.Write,
                   FileShare.None,
                   bufferSize: 4 * 1024,
                   FileOptions.WriteThrough))
        {
            V3PublicationDescriptorCodec.Write(stream, descriptor);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temporaryPath, finalPath);
    }

    private static void WriteDeletionDescriptor(
        string operationDir,
        V3DeletionDescriptor descriptor)
    {
        string temporaryPath = Path.Combine(operationDir, DeletionDescriptorTemporaryFile);
        string finalPath = Path.Combine(operationDir, DeletionDescriptorFile);
        using (var stream = new FileStream(
                   temporaryPath,
                   FileMode.CreateNew,
                   FileAccess.Write,
                   FileShare.None,
                   bufferSize: 4 * 1024,
                   FileOptions.WriteThrough))
        {
            V3DeletionDescriptorCodec.Write(stream, descriptor);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temporaryPath, finalPath);
    }

    private static V3PublicationDescriptor ReadPublicationDescriptor(string operationDir)
    {
        using var stream = new FileStream(
            Path.Combine(operationDir, PublicationDescriptorFile),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4 * 1024,
            FileOptions.SequentialScan);
        return V3PublicationDescriptorCodec.Read(stream);
    }

    private PathDigestState ReadPathDigestState(string slotID, string path)
    {
        SaveSlotClassification classification = ClassifySlotCore(slotID, path);
        return classification.Kind switch
        {
            SaveSlotOccupantKind.Absent => new PathDigestState(PathDigestKind.Absent),
            SaveSlotOccupantKind.CompleteV3 when classification.Manifest is not null =>
                new PathDigestState(
                    PathDigestKind.CompleteV3,
                    ComputeAggregateDigest(path, classification.Manifest)),
            _ => new PathDigestState(PathDigestKind.Invalid, Error: classification.Error),
        };
    }

    private OccupantDigestState ReadDeletionDigestState(
        string slotID,
        string path,
        SaveSlotOccupantKind expectedKind)
    {
        SaveSlotClassification classification = ClassifySlotCore(slotID, path);
        if (classification.Kind == SaveSlotOccupantKind.Absent)
            return new OccupantDigestState(OccupantDigestKind.Absent);
        if (classification.Kind != expectedKind)
            return new OccupantDigestState(OccupantDigestKind.Invalid, Error: classification.Error);
        return new OccupantDigestState(
            OccupantDigestKind.MatchingKind,
            ComputeOccupantDigest(path, classification));
    }

    private void CleanupPublishedOperation(
        string slotID,
        string operationDir,
        string stagingDir,
        string backupDir)
    {
        TryDeleteTransactionDirectory(stagingDir);
        TryDeleteTransactionDirectory(backupDir);
        TryDeleteTransactionDirectory(operationDir);
        TryDeleteEmptyTransactionParents(slotID);
    }

    private void QuarantineOperation(string slotID, string operationToken, string operationDir)
    {
        string quarantineRoot = Path.Combine(_saveBaseDir, QuarantineRootName);
        if (File.Exists(quarantineRoot))
            throw new SavePublicationRecoveryException("Publication quarantine root is occupied by a file.");
        Directory.CreateDirectory(quarantineRoot);
        EnsureOrdinaryDirectory(quarantineRoot, "quarantine root");
        string quarantineSlot = Path.Combine(quarantineRoot, slotID);
        if (File.Exists(quarantineSlot))
            throw new SavePublicationRecoveryException("Publication quarantine slot is occupied by a file.");
        Directory.CreateDirectory(quarantineSlot);
        EnsureOrdinaryDirectory(quarantineSlot, "quarantine slot");

        string destination = Path.Combine(quarantineSlot, operationToken);
        if (Directory.Exists(destination) || File.Exists(destination))
        {
            destination = Path.Combine(
                quarantineSlot,
                $"{operationToken}-{Guid.NewGuid():N}");
        }
        Directory.Move(operationDir, destination);
        TryDeleteEmptyTransactionParents(slotID);
    }

    private void TryDeleteEmptyTransactionParents(string slotID)
    {
        string transactionRoot = Path.Combine(_saveBaseDir, TransactionRootName);
        TryDeleteEmptyDirectory(Path.Combine(transactionRoot, slotID));
        TryDeleteEmptyDirectory(transactionRoot);
    }

    private static void TryDeleteEmptyDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;
        EnsureOrdinaryDirectory(path, "transaction");
        if (!Directory.EnumerateFileSystemEntries(path).Any())
            Directory.Delete(path);
    }

    private static void TryRestoreBackup(string slotDir, string backupDir)
    {
        if (Directory.Exists(slotDir) || !Directory.Exists(backupDir))
            return;
        EnsureOrdinaryDirectory(backupDir, "backup");
        Directory.Move(backupDir, slotDir);
    }

    private static void TryDeleteTransactionDirectory(string transactionDir)
    {
        if (!Directory.Exists(transactionDir))
            return;
        EnsureOrdinaryDirectory(transactionDir, "transaction");
        Directory.Delete(transactionDir, recursive: true);
    }

    private static void EnsureOrdinaryDirectory(string path, string kind)
    {
        if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
            throw new IOException($"Save {kind} directory cannot be a filesystem link.");
    }

    private static string GetDataFileName(string saveFileName)
    {
        if (string.IsNullOrWhiteSpace(saveFileName))
            throw new ArgumentException("Save file name cannot be empty.", nameof(saveFileName));
        foreach (char character in saveFileName)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-')
            {
                throw new ArgumentException(
                    "Save file name may only contain ASCII letters, digits, '_' or '-'.",
                    nameof(saveFileName));
            }
        }
        return saveFileName + ".json";
    }

    internal static void ValidateManifestFileName(string fileName)
    {
        const string extension = ".json";
        if (string.Equals(fileName, ManifestFile, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Manifest file name is reserved.", nameof(fileName));
        if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(extension, StringComparison.Ordinal))
            throw new ArgumentException("Manifest file name must use the .json extension.", nameof(fileName));
        string saveFileName = fileName[..^extension.Length];
        if (!string.Equals(GetDataFileName(saveFileName), fileName, StringComparison.Ordinal))
            throw new ArgumentException("Manifest file name is not a safe system JSON name.", nameof(fileName));
    }

    internal static void ValidateSlotID(string slotID)
    {
        if (string.IsNullOrWhiteSpace(slotID) || slotID.Length > MaxSlotIDLength)
            throw new ArgumentException("Save slot ID must contain 1 to 128 characters.", nameof(slotID));
        foreach (char character in slotID)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-')
                throw new ArgumentException("Save slot ID contains an unsafe character.", nameof(slotID));
        }
    }

    internal static void ValidateDisplayName(string displayName)
    {
        try
        {
            V3ManifestCodec.ValidateText(displayName, "displayName");
        }
        catch (JsonException exception)
        {
            throw new ArgumentException(exception.Message, nameof(displayName), exception);
        }
    }

    private static string? ResolveThumbnailPath(
        string slotDir,
        string? thumbnailFile,
        out string? warning)
    {
        warning = null;
        if (thumbnailFile is null)
            return null;
        string thumbnailPath = Path.GetFullPath(Path.Combine(slotDir, thumbnailFile));
        string relative = Path.GetRelativePath(slotDir, thumbnailPath);
        if (!string.Equals(relative, thumbnailFile, StringComparison.Ordinal))
        {
            warning = "Thumbnail path resolves outside its slot.";
            return null;
        }
        if (!File.Exists(thumbnailPath))
        {
            warning = "Thumbnail file is missing; using the placeholder.";
            return null;
        }
        warning = V3PngValidator.Validate(thumbnailPath);
        if (warning is not null)
            return null;
        return thumbnailPath;
    }

    private sealed record SaveParticipant(
        string FileName,
        IStreamingSaveable Saveable,
        ISaveSnapshot Snapshot);

    private sealed record SaveParticipantDefinition(
        string FileName,
        IStreamingSaveable Saveable);

    private sealed record SlotEntrySnapshot(string[] Directories, string[] Files)
    {
        public bool Equals(SlotEntrySnapshot? other) =>
            other is not null &&
            Directories.SequenceEqual(other.Directories, StringComparer.Ordinal) &&
            Files.SequenceEqual(other.Files, StringComparer.Ordinal);

        public override int GetHashCode() => HashCode.Combine(Directories.Length, Files.Length);
    }

    private enum PathDigestKind
    {
        Absent,
        CompleteV3,
        Invalid,
    }

    private enum OccupantDigestKind
    {
        Absent,
        MatchingKind,
        Invalid,
    }

    private sealed record PathDigestState(
        PathDigestKind Kind,
        string? Digest = null,
        string? Error = null)
    {
        internal bool Matches(string digest) =>
            Kind == PathDigestKind.CompleteV3 &&
            string.Equals(Digest, digest, StringComparison.Ordinal);
    }

    private sealed record OccupantDigestState(
        OccupantDigestKind Kind,
        string? Digest = null,
        string? Error = null)
    {
        internal bool Matches(string digest) =>
            Kind == OccupantDigestKind.MatchingKind &&
            string.Equals(Digest, digest, StringComparison.Ordinal);
    }
}

internal enum SavePublicationPhase
{
    Staged,
    CommitLeaseAcquired,
    PreviousSlotMoved,
    CanonicalPublished,
    DeletionDescriptorPublished,
    DeletionTombstoned,
}

internal sealed class SaveRootBusyException(string message, Exception? innerException = null)
    : IOException(message, innerException);

internal sealed class SavePublicationRecoveryException(string message, Exception? innerException = null)
    : IOException(message, innerException);

internal sealed class SaveDeletionRecoveryException(string message, Exception? innerException = null)
    : IOException(message, innerException);
