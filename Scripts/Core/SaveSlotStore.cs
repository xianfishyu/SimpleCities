using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

internal sealed class SaveSlotStore
{
    private const string ManifestFile = "manifest.json";
    private const string StagingSuffix = ".staging";
    private const string BackupSuffix = ".backup";
    internal const int MaxDisplayNameLength = 128;
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

    public string Create(string displayName, IReadOnlyList<ISaveable> saveables)
    {
        ValidateDisplayName(displayName);

        string slotID;
        do
        {
            slotID = $"manual-{Guid.NewGuid():N}";
        }
        while (Directory.Exists(GetSlotDir(slotID)));

        Save(slotID, displayName, saveables);
        return slotID;
    }

    public int Save(string slotID, string displayName, IReadOnlyList<ISaveable> saveables)
    {
        ValidateDisplayName(displayName);
        var fileNames = new List<string>(saveables.Count);
        var uniqueFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ISaveable saveable in saveables)
        {
            string fileName = GetDataFileName(saveable.SaveFileName);
            if (string.Equals(fileName, ManifestFile, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Save file name 'manifest' is reserved.", nameof(saveables));
            if (!uniqueFileNames.Add(fileName))
                throw new ArgumentException($"Duplicate save file name '{fileName}'.", nameof(saveables));
            fileNames.Add(fileName);
        }

        string slotDir = GetSlotDir(slotID);
        string stagingDir = GetTransactionDir(slotID, StagingSuffix);
        string backupDir = GetTransactionDir(slotID, BackupSuffix);
        var payloads = new List<(string FileName, string Json)>(saveables.Count);
        for (int index = 0; index < saveables.Count; index++)
        {
            string json = SaveJson.Serialize(saveables[index].CaptureState());
            payloads.Add((fileNames[index], json));
        }

        Directory.CreateDirectory(_saveBaseDir);
        RecoverSlotPublication(slotDir, stagingDir, backupDir);
        Directory.CreateDirectory(stagingDir);

        try
        {
            foreach ((string fileName, string json) in payloads)
                File.WriteAllText(Path.Combine(stagingDir, fileName), json, Encoding.UTF8);

            WriteManifest(stagingDir, slotID, displayName, payloads.Select(item => item.FileName).ToList());
            _publicationObserver?.Invoke(SavePublicationPhase.Staged);
            PublishSlot(slotDir, stagingDir, backupDir);
        }
        catch
        {
            TryRestoreBackup(slotDir, backupDir);
            TryDeleteTransactionDirectory(stagingDir);
            throw;
        }

        return payloads.Count;
    }

    public int Load(string slotID, IReadOnlyList<ISaveable> saveables)
    {
        string slotDir = GetRecoveredSlotDir(slotID);
        if (!Directory.Exists(slotDir))
            throw new DirectoryNotFoundException($"Save slot '{slotID}' not found.");

        ManifestData manifest = ReadManifest(slotID);
        var fileSet = new HashSet<string>(manifest.Files, StringComparer.OrdinalIgnoreCase);
        var systemMap = new Dictionary<string, ISaveable>(StringComparer.OrdinalIgnoreCase);

        foreach (ISaveable saveable in saveables)
        {
            string fileName = GetDataFileName(saveable.SaveFileName);
            if (!fileSet.Contains(fileName))
                throw new InvalidDataException($"Manifest does not contain required file '{fileName}'.");
            if (!systemMap.TryAdd(fileName, saveable))
                throw new InvalidDataException($"Multiple saveables require file '{fileName}'.");
        }

        var rawStates = new List<(ISaveable Saveable, string Json)>(systemMap.Count);
        foreach ((string fileName, ISaveable saveable) in systemMap)
        {
            string filePath = Path.Combine(slotDir, fileName);
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File '{fileName}' missing in slot '{slotID}'.", filePath);

            string json = File.ReadAllText(filePath, Encoding.UTF8);
            using (JsonDocument.Parse(json)) { }
            rawStates.Add((saveable, json));
        }

        var preparedStates = new List<(ISaveable Saveable, object State)>(rawStates.Count);
        foreach ((ISaveable saveable, string json) in rawStates)
        {
            object state = saveable is IPreparedSaveable preparedSaveable
                ? preparedSaveable.PrepareRestoreState(json)
                : json;
            preparedStates.Add((saveable, state));
        }

        foreach ((ISaveable saveable, object state) in preparedStates)
        {
            if (saveable is IPreparedSaveable preparedSaveable)
                preparedSaveable.RestorePreparedState(state);
            else
                saveable.RestoreState((string)state);
        }

        return systemMap.Count;
    }

    public ManifestData ReadManifest(string slotID)
    {
        string manifestPath = Path.Combine(GetRecoveredSlotDir(slotID), ManifestFile);
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"Manifest not found in slot '{slotID}'.", manifestPath);

        ManifestData manifest = SaveManager.ParseAndValidateManifest(
            File.ReadAllText(manifestPath, Encoding.UTF8));
        if (!string.Equals(manifest.SlotID, slotID, StringComparison.Ordinal))
            throw new InvalidDataException($"Manifest slotId '{manifest.SlotID}' does not match directory '{slotID}'.");

        return manifest;
    }

    public IReadOnlyList<SaveSlotSummary> ListSlots()
    {
        if (!Directory.Exists(_saveBaseDir))
            return Array.Empty<SaveSlotSummary>();

        RecoverInterruptedPublications();
        var summaries = new List<SaveSlotSummary>();
        foreach (string slotDir in Directory.EnumerateDirectories(_saveBaseDir))
        {
            string slotID = Path.GetFileName(slotDir);
            if (IsTransactionDirectoryName(slotID))
                continue;
            try
            {
                ManifestData manifest = ReadManifest(slotID);
                foreach (string fileName in manifest.Files)
                {
                    string filePath = Path.Combine(slotDir, fileName);
                    if (!File.Exists(filePath))
                        throw new FileNotFoundException($"File '{fileName}' missing in slot '{slotID}'.", filePath);
                    if (File.GetAttributes(filePath).HasFlag(FileAttributes.ReparsePoint))
                        throw new IOException($"File '{fileName}' in slot '{slotID}' cannot be a filesystem link.");
                }

                if (!DateTimeOffset.TryParse(
                        manifest.Timestamp,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AssumeUniversal |
                            System.Globalization.DateTimeStyles.AdjustToUniversal,
                        out DateTimeOffset savedAtUtc))
                {
                    throw new InvalidDataException($"Manifest timestamp '{manifest.Timestamp}' is not valid UTC time.");
                }

                summaries.Add(new SaveSlotSummary
                {
                    SlotID = slotID,
                    DisplayName = manifest.DisplayName,
                    SavedAtUtc = savedAtUtc,
                    CityName = manifest.CityName,
                    Population = manifest.Population,
                    Funds = manifest.Funds,
                    ThumbnailPath = ResolveThumbnailPath(slotDir, manifest.ThumbnailFile),
                    Files = manifest.Files.ToArray(),
                    IsValid = true,
                });
            }
            catch (Exception e)
            {
                summaries.Add(new SaveSlotSummary
                {
                    SlotID = slotID,
                    DisplayName = slotID,
                    IsValid = false,
                    Error = e.Message,
                });
            }
        }

        return summaries
            .OrderByDescending(summary => summary.IsValid)
            .ThenByDescending(summary => summary.SavedAtUtc)
            .ThenBy(summary => summary.SlotID, StringComparer.Ordinal)
            .ToArray();
    }

    public bool Exists(string slotID) => File.Exists(Path.Combine(GetRecoveredSlotDir(slotID), ManifestFile));

    public bool Delete(string slotID)
    {
        string slotDir = GetRecoveredSlotDir(slotID);
        if (!Directory.Exists(slotDir))
            return false;

        Directory.Delete(slotDir, recursive: true);
        TryDeleteTransactionDirectory(GetTransactionDir(slotID, StagingSuffix));
        TryDeleteTransactionDirectory(GetTransactionDir(slotID, BackupSuffix));
        return true;
    }

    private string GetSlotDir(string slotID)
    {
        ValidateSlotID(slotID);
        string slotDir = Path.GetFullPath(Path.Combine(_saveBaseDir, slotID));
        string relativePath = Path.GetRelativePath(_saveBaseDir, slotDir);
        if (!string.Equals(relativePath, slotID, StringComparison.Ordinal))
            throw new InvalidOperationException($"Save slot '{slotID}' resolves outside the save root.");
        if (Directory.Exists(slotDir) &&
            File.GetAttributes(slotDir).HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException($"Save slot '{slotID}' cannot be a filesystem link.");
        }

        return slotDir;
    }

    private string GetRecoveredSlotDir(string slotID)
    {
        string slotDir = GetSlotDir(slotID);
        if (Directory.Exists(_saveBaseDir))
        {
            RecoverSlotPublication(
                slotDir,
                GetTransactionDir(slotID, StagingSuffix),
                GetTransactionDir(slotID, BackupSuffix));
        }

        return slotDir;
    }

    private string GetTransactionDir(string slotID, string suffix)
    {
        string directoryName = $".{slotID}{suffix}";
        string transactionDir = Path.GetFullPath(Path.Combine(_saveBaseDir, directoryName));
        string relativePath = Path.GetRelativePath(_saveBaseDir, transactionDir);
        if (!string.Equals(relativePath, directoryName, StringComparison.Ordinal))
            throw new InvalidOperationException($"Transaction path for slot '{slotID}' resolves outside the save root.");
        if (Directory.Exists(transactionDir) &&
            File.GetAttributes(transactionDir).HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException($"Transaction path for slot '{slotID}' cannot be a filesystem link.");
        }

        return transactionDir;
    }

    private void RecoverSlotPublication(string slotDir, string stagingDir, string backupDir)
    {
        if (File.Exists(stagingDir) || File.Exists(backupDir))
            throw new IOException("Save transaction path is occupied by a file.");

        TryDeleteTransactionDirectory(stagingDir);
        if (!Directory.Exists(backupDir))
            return;

        if (!Directory.Exists(slotDir))
        {
            Directory.Move(backupDir, slotDir);
            return;
        }

        if (!File.Exists(Path.Combine(slotDir, ManifestFile)))
        {
            Directory.Delete(slotDir, recursive: true);
            Directory.Move(backupDir, slotDir);
            return;
        }

        Directory.Delete(backupDir, recursive: true);
    }

    private void RecoverInterruptedPublications()
    {
        var slotIDs = new HashSet<string>(StringComparer.Ordinal);
        foreach (string transactionDir in Directory.EnumerateDirectories(_saveBaseDir))
        {
            string directoryName = Path.GetFileName(transactionDir);
            if (TryGetTransactionSlotID(directoryName, StagingSuffix, out string stagingSlotID))
                slotIDs.Add(stagingSlotID);
            else if (TryGetTransactionSlotID(directoryName, BackupSuffix, out string backupSlotID))
                slotIDs.Add(backupSlotID);
        }

        foreach (string slotID in slotIDs)
            GetRecoveredSlotDir(slotID);
    }

    private static bool TryGetTransactionSlotID(
        string directoryName,
        string suffix,
        out string slotID)
    {
        slotID = "";
        if (directoryName.Length <= suffix.Length + 1 ||
            directoryName[0] != '.' ||
            !directoryName.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        string candidate = directoryName[1..^suffix.Length];
        try
        {
            ValidateSlotID(candidate);
        }
        catch (ArgumentException)
        {
            return false;
        }

        slotID = candidate;
        return true;
    }

    private void PublishSlot(string slotDir, string stagingDir, string backupDir)
    {
        if (Directory.Exists(slotDir))
        {
            Directory.Move(slotDir, backupDir);
            _publicationObserver?.Invoke(SavePublicationPhase.PreviousSlotMoved);
        }

        Directory.Move(stagingDir, slotDir);
        try
        {
            TryDeleteTransactionDirectory(backupDir);
        }
        catch (IOException)
        {
            // The new slot is already fully published. A hidden backup can be
            // cleaned by the next save without turning success into failure.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above: preserve the successfully published slot.
        }
    }

    private static void TryRestoreBackup(string slotDir, string backupDir)
    {
        if (Directory.Exists(slotDir) || !Directory.Exists(backupDir))
            return;

        Directory.Move(backupDir, slotDir);
    }

    private static void TryDeleteTransactionDirectory(string transactionDir)
    {
        if (!Directory.Exists(transactionDir))
            return;
        if (File.GetAttributes(transactionDir).HasFlag(FileAttributes.ReparsePoint))
            throw new IOException($"Save transaction directory '{Path.GetFileName(transactionDir)}' cannot be a filesystem link.");

        Directory.Delete(transactionDir, recursive: true);
    }

    private static bool IsTransactionDirectoryName(string directoryName) =>
        directoryName.Length > 0 && directoryName[0] == '.' &&
        (directoryName.EndsWith(StagingSuffix, StringComparison.Ordinal) ||
         directoryName.EndsWith(BackupSuffix, StringComparison.Ordinal));

    private static string GetDataFileName(string saveFileName)
    {
        if (string.IsNullOrWhiteSpace(saveFileName))
            throw new ArgumentException("Save file name cannot be empty.", nameof(saveFileName));

        foreach (char character in saveFileName)
        {
            bool isAllowed = char.IsAsciiLetterOrDigit(character) || character is '_' or '-';
            if (!isAllowed)
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
        if (string.IsNullOrWhiteSpace(fileName) ||
            !fileName.EndsWith(extension, StringComparison.Ordinal))
        {
            throw new ArgumentException("Manifest file name must use the .json extension.", nameof(fileName));
        }

        string saveFileName = fileName[..^extension.Length];
        if (!string.Equals(GetDataFileName(saveFileName), fileName, StringComparison.Ordinal))
            throw new ArgumentException("Manifest file name is not a safe system JSON name.", nameof(fileName));
    }

    private static string? ResolveThumbnailPath(string slotDir, string? thumbnailFile)
    {
        if (string.IsNullOrWhiteSpace(thumbnailFile))
            return null;
        if (!string.Equals(Path.GetFileName(thumbnailFile), thumbnailFile, StringComparison.Ordinal))
            return null;

        string thumbnailPath = Path.GetFullPath(Path.Combine(slotDir, thumbnailFile));
        string relativePath = Path.GetRelativePath(slotDir, thumbnailPath);
        if (!string.Equals(relativePath, thumbnailFile, StringComparison.Ordinal) || !File.Exists(thumbnailPath))
            return null;
        if (File.GetAttributes(thumbnailPath).HasFlag(FileAttributes.ReparsePoint))
            return null;

        return thumbnailPath;
    }

    private static void ValidateSlotID(string slotID)
    {
        if (string.IsNullOrWhiteSpace(slotID))
            throw new ArgumentException("Save slot ID cannot be empty.", nameof(slotID));

        foreach (char character in slotID)
        {
            bool isAllowed = char.IsAsciiLetterOrDigit(character) || character is '_' or '-';
            if (!isAllowed)
                throw new ArgumentException("Save slot ID may only contain ASCII letters, digits, '_' or '-'.", nameof(slotID));
        }
    }

    internal static void ValidateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Save display name cannot be empty.", nameof(displayName));
        if (displayName.Length > MaxDisplayNameLength)
            throw new ArgumentException(
                $"Save display name cannot exceed {MaxDisplayNameLength} characters.",
                nameof(displayName));
    }

    private static void WriteManifest(string slotDir, string slotID, string displayName, List<string> files)
    {
        var manifest = new ManifestData
        {
            SchemaVersion = SaveManager.ManifestSchemaVersion,
            SlotID = slotID,
            DisplayName = displayName,
            Timestamp = DateTime.UtcNow.ToString("O"),
            Files = files,
        };
        File.WriteAllText(
            Path.Combine(slotDir, ManifestFile),
            SaveJson.Serialize(manifest),
            Encoding.UTF8);
    }
}

internal enum SavePublicationPhase
{
    Staged,
    PreviousSlotMoved,
}
