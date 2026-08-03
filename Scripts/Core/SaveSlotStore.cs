using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

internal sealed class SaveSlotStore
{
    private const string ManifestFile = "manifest.json";
    internal const int MaxDisplayNameLength = 128;
    private readonly string _saveBaseDir;

    public SaveSlotStore(string saveBaseDir)
    {
        if (string.IsNullOrWhiteSpace(saveBaseDir))
            throw new ArgumentException("Save base directory cannot be empty.", nameof(saveBaseDir));

        _saveBaseDir = Path.GetFullPath(saveBaseDir);
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
        string slotDir = GetSlotDir(slotID);
        var saveTargets = new List<(ISaveable Saveable, string FileName)>(saveables.Count);
        foreach (ISaveable saveable in saveables)
            saveTargets.Add((saveable, GetDataFileName(saveable.SaveFileName)));

        Directory.CreateDirectory(slotDir);
        var savedFiles = new List<string>();

        foreach ((ISaveable saveable, string fileName) in saveTargets)
        {
            string temporaryPath = Path.Combine(slotDir, fileName + ".tmp");
            string finalPath = Path.Combine(slotDir, fileName);
            string json = SaveJson.Serialize(saveable.CaptureState());

            File.WriteAllText(temporaryPath, json, Encoding.UTF8);
            if (File.Exists(finalPath))
                File.Delete(finalPath);
            File.Move(temporaryPath, finalPath);
            savedFiles.Add(fileName);
        }

        WriteManifest(slotDir, slotID, displayName, savedFiles);
        return savedFiles.Count;
    }

    public int Load(string slotID, IReadOnlyList<ISaveable> saveables)
    {
        string slotDir = GetSlotDir(slotID);
        if (!Directory.Exists(slotDir))
            throw new DirectoryNotFoundException($"Save slot '{slotID}' not found.");

        ManifestData manifest = ReadManifest(slotID);
        var fileSet = new HashSet<string>(manifest.Files, StringComparer.Ordinal);
        var systemMap = new Dictionary<string, ISaveable>(StringComparer.Ordinal);

        foreach (ISaveable saveable in saveables)
        {
            string fileName = GetDataFileName(saveable.SaveFileName);
            if (fileSet.Contains(fileName))
                systemMap[fileName] = saveable;
        }

        foreach ((string fileName, ISaveable saveable) in systemMap)
        {
            string filePath = Path.Combine(slotDir, fileName);
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File '{fileName}' missing in slot '{slotID}'.", filePath);

            saveable.RestoreState(File.ReadAllText(filePath, Encoding.UTF8));
        }

        return systemMap.Count;
    }

    public ManifestData ReadManifest(string slotID)
    {
        string manifestPath = Path.Combine(GetSlotDir(slotID), ManifestFile);
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"Manifest not found in slot '{slotID}'.", manifestPath);

        ManifestData manifest = SaveManager.ParseAndValidateManifest(
            File.ReadAllText(manifestPath, Encoding.UTF8));
        if (!string.Equals(manifest.SlotID, slotID, StringComparison.Ordinal))
            throw new InvalidDataException($"Manifest slotId '{manifest.SlotID}' does not match directory '{slotID}'.");

        return manifest;
    }

    public bool Exists(string slotID) => File.Exists(Path.Combine(GetSlotDir(slotID), ManifestFile));

    public bool Delete(string slotID)
    {
        string slotDir = GetSlotDir(slotID);
        if (!Directory.Exists(slotDir))
            return false;

        Directory.Delete(slotDir, recursive: true);
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
