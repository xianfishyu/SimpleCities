using System;
using System.Collections.Generic;
using System.Linq;

internal sealed record V3ManifestFile(
    string Name,
    long EncodedLength,
    string Sha256);

internal sealed record V3Manifest(
    string SlotID,
    string DisplayName,
    string Timestamp,
    string CityName,
    long? Population,
    decimal? Funds,
    string? ThumbnailFile,
    IReadOnlyList<V3ManifestFile> Files);

internal enum SaveSlotOccupantKind
{
    Absent,
    CompleteV3,
    CorruptV3,
    Foreign,
    Unsafe,
}

internal sealed record SaveSlotClassification(
    SaveSlotOccupantKind Kind,
    V3Manifest? Manifest = null,
    string? Error = null);

internal enum SavePublishResultKind
{
    Published,
    PublishedWithCleanupPending,
}

internal sealed record SavePublishResult(
    SavePublishResultKind Kind,
    string SlotID,
    string OperationToken,
    int SavedFileCount,
    string? Warning = null)
{
    internal bool IsPublished => Kind is SavePublishResultKind.Published or
        SavePublishResultKind.PublishedWithCleanupPending;
}

internal enum SaveDeleteResultKind
{
    Deleted,
    DeletedWithCleanupPending,
}

internal sealed record SaveDeleteResult(
    SaveDeleteResultKind Kind,
    string SlotID,
    string OperationToken,
    string? Warning = null)
{
    internal bool IsDeleted => Kind is SaveDeleteResultKind.Deleted or
        SaveDeleteResultKind.DeletedWithCleanupPending;
}

internal sealed record SaveDeletionAuthorization(
    string SlotID,
    long UIGeneration,
    string OperationToken,
    SaveSlotOccupantKind OccupantKind,
    string OccupantDigest,
    string ConfirmationSummary);

internal sealed record CapturedSaveParticipant(
    string FileName,
    IStreamingSaveable Saveable,
    ISaveSnapshot Snapshot);

internal sealed record PreparedSaveParticipant(
    IStreamingLoadTarget Target,
    IPreparedSaveState State);

internal sealed class PreparedSaveSlot
{
    private readonly PreparedSaveParticipant[] _participants;

    internal PreparedSaveSlot(
        string slotID,
        IEnumerable<PreparedSaveParticipant> participants)
    {
        if (string.IsNullOrWhiteSpace(slotID))
            throw new ArgumentException("Prepared slot ID cannot be empty.", nameof(slotID));
        ArgumentNullException.ThrowIfNull(participants);
        SlotID = slotID;
        _participants = participants.ToArray();
    }

    internal string SlotID { get; }
    internal IReadOnlyList<PreparedSaveParticipant> Participants => _participants;

    internal IPreparedSaveState GetPreparedState(IStreamingLoadTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        PreparedSaveParticipant? match = null;
        foreach (PreparedSaveParticipant participant in _participants)
        {
            if (!ReferenceEquals(participant.Target, target))
                continue;
            if (match is not null)
                throw new InvalidOperationException("A load target has multiple prepared states.");
            match = participant;
        }
        return match?.State ?? throw new InvalidOperationException(
            $"Load target '{target.SaveFileName}' has no prepared state.");
    }

    internal int CommitLegacy()
    {
        foreach (PreparedSaveParticipant participant in _participants)
            participant.Target.CommitPreparedLoad(participant.State);
        return _participants.Length;
    }
}

internal sealed record CapturedLoadParticipant(
    string FileName,
    IStreamingLoadTarget Target,
    IStreamingLoadReader Reader);

public sealed class SaveSlotSummary
{
    public string SlotID { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public DateTimeOffset? SavedAtUtc { get; init; }
    public string CityName { get; init; } = "Unknown City";
    public long? Population { get; init; }
    public decimal? Funds { get; init; }
    public string? ThumbnailPath { get; init; }
    public IReadOnlyList<string> Files { get; init; } = Array.Empty<string>();
    public bool IsValid { get; init; }
    public string? Error { get; init; }
    public string? Warning { get; init; }
    public bool IsAutosave => string.Equals(SlotID, SaveManager.AutosaveSlotID, StringComparison.Ordinal);
    internal SaveSlotOccupantKind OccupantKind { get; init; }
    internal bool SupportsDeletion => OccupantKind is SaveSlotOccupantKind.CompleteV3 or
        SaveSlotOccupantKind.CorruptV3;
    internal string? OccupantDigest { get; init; }
    internal long UIGeneration { get; set; }
    internal string DeleteOperationToken { get; set; } = "";
}
