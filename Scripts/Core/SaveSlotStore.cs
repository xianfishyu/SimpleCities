using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

internal sealed class SaveSlotStore
{
    private const string ManifestFile = "manifest.json";
    private readonly string _saveBaseDir;

    public SaveSlotStore(string saveBaseDir)
    {
        if (string.IsNullOrWhiteSpace(saveBaseDir))
            throw new ArgumentException("Save base directory cannot be empty.", nameof(saveBaseDir));

        _saveBaseDir = Path.GetFullPath(saveBaseDir);
    }

    public int Save(string slotName, IReadOnlyList<ISaveable> saveables)
    {
        string slotDir = GetSlotDir(slotName);
        Directory.CreateDirectory(slotDir);
        var savedFiles = new List<string>();

        foreach (ISaveable saveable in saveables)
        {
            string fileName = saveable.SaveFileName + ".json";
            string temporaryPath = Path.Combine(slotDir, fileName + ".tmp");
            string finalPath = Path.Combine(slotDir, fileName);
            string json = SaveJson.Serialize(saveable.CaptureState());

            File.WriteAllText(temporaryPath, json, Encoding.UTF8);
            if (File.Exists(finalPath))
                File.Delete(finalPath);
            File.Move(temporaryPath, finalPath);
            savedFiles.Add(fileName);
        }

        WriteManifest(slotDir, slotName, savedFiles);
        return savedFiles.Count;
    }

    public int Load(string slotName, IReadOnlyList<ISaveable> saveables)
    {
        string slotDir = GetSlotDir(slotName);
        if (!Directory.Exists(slotDir))
            throw new DirectoryNotFoundException($"Save slot '{slotName}' not found.");

        string manifestPath = Path.Combine(slotDir, ManifestFile);
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"Manifest not found in slot '{slotName}'.", manifestPath);

        ManifestData manifest = SaveManager.ParseAndValidateManifest(
            File.ReadAllText(manifestPath, Encoding.UTF8));
        var fileSet = new HashSet<string>(manifest.Files, StringComparer.Ordinal);
        var systemMap = new Dictionary<string, ISaveable>(StringComparer.Ordinal);

        foreach (ISaveable saveable in saveables)
        {
            string fileName = saveable.SaveFileName + ".json";
            if (fileSet.Contains(fileName))
                systemMap[fileName] = saveable;
        }

        foreach ((string fileName, ISaveable saveable) in systemMap)
        {
            string filePath = Path.Combine(slotDir, fileName);
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File '{fileName}' missing in slot '{slotName}'.", filePath);

            saveable.RestoreState(File.ReadAllText(filePath, Encoding.UTF8));
        }

        return systemMap.Count;
    }

    public bool Exists(string slotName) => File.Exists(Path.Combine(GetSlotDir(slotName), ManifestFile));

    public bool Delete(string slotName)
    {
        string slotDir = GetSlotDir(slotName);
        if (!Directory.Exists(slotDir))
            return false;

        Directory.Delete(slotDir, recursive: true);
        return true;
    }

    private string GetSlotDir(string slotName)
    {
        ValidateSlotName(slotName);
        return Path.Combine(_saveBaseDir, slotName);
    }

    private static void ValidateSlotName(string slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName))
            throw new ArgumentException("Save slot name cannot be empty.", nameof(slotName));

        foreach (char character in slotName)
        {
            bool isAllowed = char.IsAsciiLetterOrDigit(character) || character is '_' or '-';
            if (!isAllowed)
                throw new ArgumentException("Save slot name may only contain ASCII letters, digits, '_' or '-'.", nameof(slotName));
        }
    }

    private static void WriteManifest(string slotDir, string slotName, List<string> files)
    {
        var manifest = new ManifestData
        {
            SchemaVersion = SaveManager.ManifestSchemaVersion,
            SlotName = slotName,
            Timestamp = DateTime.UtcNow.ToString("O"),
            Files = files,
        };
        File.WriteAllText(
            Path.Combine(slotDir, ManifestFile),
            SaveJson.Serialize(manifest),
            Encoding.UTF8);
    }
}
