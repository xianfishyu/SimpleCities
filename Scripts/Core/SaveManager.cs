using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

/// <summary>
/// 存档管理器 — Autoload 单例。
/// 各系统通过 Register() 注册，Save/Load 时遍历所有已注册系统。
/// </summary>
public partial class SaveManager : Node
{
    public static SaveManager Instance { get; private set; } = null!;

    private readonly List<ISaveable> _saveables = new();

    private const string EditorSaveBaseDir = "res://saves";
    private const string ExportSaveDirectoryName = "saves";
    private const string ManifestFile = "manifest.json";
    private const int ManifestSchemaVersion = 1;

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
    };

    public string CurrentSlotName { get; private set; } = "autosave";
    public int RegisteredSaveableCount => _saveables.Count;

    public override void _Ready()
    {
        Instance ??= this;
    }

    // ═══════════════════════════════════════════════
    // 注册
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 注册一个可持久化系统。相同实例可重复调用；不同实例不能占用同一个 SaveFileName。
    /// </summary>
    public bool Register(ISaveable saveable)
    {
        ArgumentNullException.ThrowIfNull(saveable);

        if (!_saveables.Contains(saveable))
        {
            ISaveable? conflict = _saveables.Find(existing =>
                string.Equals(existing.SaveFileName, saveable.SaveFileName, StringComparison.Ordinal));
            if (conflict != null)
            {
                GD.PushError($"SaveManager: duplicate active SaveFileName '{saveable.SaveFileName}' rejected.");
                return false;
            }

            _saveables.Add(saveable);
        }

        return true;
    }

    /// <summary>注销离开场景树的可持久化系统；重复注销安全返回 false。</summary>
    public bool Unregister(ISaveable saveable) => _saveables.Remove(saveable);

    // ═══════════════════════════════════════════════
    // 保存
    // ═══════════════════════════════════════════════

    /// <summary>保存所有已注册系统到指定存档槽</summary>
    public bool Save(string slotName = "autosave")
    {
        try
        {
            string slotDir = GetSlotDir(slotName);
            DirAccess.MakeDirRecursiveAbsolute(slotDir);

            var savedFiles = new List<string>();

            foreach (var saveable in _saveables)
            {
                string fileName = saveable.SaveFileName + ".json";
                string tmpPath = Path.Combine(slotDir, fileName + ".tmp");
                string finalPath = Path.Combine(slotDir, fileName);

                object state = saveable.CaptureState();
                string json = SaveJson.Serialize(state);

                // 先写 .tmp，成功后再 rename（防断电写坏文件）
                File.WriteAllText(tmpPath, json, Encoding.UTF8);
                if (File.Exists(finalPath))
                    File.Delete(finalPath);
                File.Move(tmpPath, finalPath);

                savedFiles.Add(fileName);
            }

            // 写入 manifest
            WriteManifest(slotDir, slotName, savedFiles);

            CurrentSlotName = slotName;
            GD.Print($"[SaveManager] Saved to slot '{slotName}' ({savedFiles.Count} files)");
            return true;
        }
        catch (Exception e)
        {
            GD.PushError($"[SaveManager] Save failed: {e.Message}");
            return false;
        }
    }

    // ═══════════════════════════════════════════════
    // 加载
    // ═══════════════════════════════════════════════

    /// <summary>从指定存档槽加载所有已注册系统</summary>
    public bool Load(string slotName = "autosave")
    {
        try
        {
            string slotDir = GetSlotDir(slotName);

            if (!DirAccess.DirExistsAbsolute(slotDir))
            {
                GD.PushError($"[SaveManager] Save slot '{slotName}' not found");
                return false;
            }

            // 读取 manifest
            string manifestPath = Path.Combine(slotDir, ManifestFile);
            if (!File.Exists(manifestPath))
            {
                GD.PushError($"[SaveManager] Manifest not found in slot '{slotName}'");
                return false;
            }
            string manifestJson = File.ReadAllText(manifestPath, Encoding.UTF8);
            ManifestData manifest = ParseAndValidateManifest(manifestJson);

            // 从清单中收集可加载的文件
            var fileSet = new HashSet<string>(manifest.Files);
            var systemMap = new Dictionary<string, ISaveable>();
            foreach (var saveable in _saveables)
            {
                string fileName = saveable.SaveFileName + ".json";
                if (fileSet.Contains(fileName))
                    systemMap[fileName] = saveable;
            }

            // 逐个加载
            foreach (var (fileName, saveable) in systemMap)
            {
                string filePath = Path.Combine(slotDir, fileName);
                if (!File.Exists(filePath))
                {
                    GD.PushError($"[SaveManager] File '{fileName}' missing in slot '{slotName}'");
                    return false;
                }

                string json = File.ReadAllText(filePath, Encoding.UTF8);

                // 需要知道 DTO 类型 → 通过反射从 CaptureState 返回值推断
                // 这里用一个技巧：先调 CaptureState 看类型，再用该类型反序列化
                // 但在 restore 前我们不想要副作用，所以约定 RestoreState 接收 raw json
                // → 改用泛型或 json 字符串传递
                saveable.RestoreState(json);
            }

            CurrentSlotName = slotName;
            GD.Print($"[SaveManager] Loaded from slot '{slotName}' ({systemMap.Count} files)");
            return true;
        }
        catch (Exception e)
        {
            GD.PushError($"[SaveManager] Load failed: {e.Message}");
            return false;
        }
    }

    // ═══════════════════════════════════════════════
    // 辅助
    // ═══════════════════════════════════════════════

    public bool SaveSlotExists(string slotName)
    {
        try
        {
            string manifestPath = Path.Combine(GetSlotDir(slotName), ManifestFile);
            return File.Exists(manifestPath);
        }
        catch (Exception e)
        {
            GD.PushError($"[SaveManager] Cannot inspect slot '{slotName}': {e.Message}");
            return false;
        }
    }

    public void DeleteSlot(string slotName)
    {
        try
        {
            string slotDir = GetSlotDir(slotName);
            if (DirAccess.DirExistsAbsolute(slotDir))
            {
                DirAccess.RemoveAbsolute(slotDir);
                GD.Print($"[SaveManager] Deleted slot '{slotName}'");
            }
        }
        catch (Exception e)
        {
            GD.PushError($"[SaveManager] Delete failed for slot '{slotName}': {e.Message}");
        }
    }

    private static string GetSlotDir(string slotName)
    {
        ValidateSlotName(slotName);
        return Path.Combine(GetSaveBaseDir(), slotName);
    }

    private static string GetSaveBaseDir()
    {
        if (OS.HasFeature("editor"))
            return ProjectSettings.GlobalizePath(EditorSaveBaseDir);

        string executablePath = OS.GetExecutablePath();
        if (string.IsNullOrWhiteSpace(executablePath) || !Path.IsPathFullyQualified(executablePath))
            throw new InvalidOperationException($"Cannot resolve executable path '{executablePath}'.");

        string? executableDir = Path.GetDirectoryName(executablePath);
        if (string.IsNullOrWhiteSpace(executableDir))
            throw new InvalidOperationException($"Cannot resolve executable directory from '{executablePath}'.");

        return Path.Combine(executableDir, ExportSaveDirectoryName);
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
            SchemaVersion = ManifestSchemaVersion,
            SlotName = slotName,
            Timestamp = DateTime.UtcNow.ToString("O"),
            Files = files
        };
        string json = SaveJson.Serialize(manifest);
        string path = Path.Combine(slotDir, ManifestFile);
        File.WriteAllText(path, json, Encoding.UTF8);
    }

    internal static ManifestData ParseAndValidateManifest(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new JsonException("Save manifest is empty.");

        ManifestData manifest = JsonSerializer.Deserialize<ManifestData>(json, ManifestJsonOptions)
            ?? throw new JsonException("Save manifest must be a JSON object.");

        if (manifest.SchemaVersion != ManifestSchemaVersion)
        {
            throw new JsonException(
                $"Unsupported manifest schemaVersion '{manifest.SchemaVersion?.ToString() ?? "missing"}'.");
        }

        return manifest;
    }
}
