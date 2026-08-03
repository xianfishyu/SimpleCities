using Godot;
using System;
using System.Collections.Generic;
using System.IO;
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
    public const string AutosaveSlotID = "autosave";
    internal const int ManifestSchemaVersion = 1;

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
    };

    public string CurrentSlotID { get; private set; } = AutosaveSlotID;
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
    public bool Save(string slotID = AutosaveSlotID)
    {
        try
        {
            SaveSlotStore store = CreateSlotStore();
            string displayName = string.Equals(slotID, AutosaveSlotID, StringComparison.Ordinal)
                ? "Autosave"
                : store.ReadManifest(slotID).DisplayName;
            int savedFileCount = store.Save(slotID, displayName, _saveables);

            CurrentSlotID = slotID;
            GD.Print($"[SaveManager] Saved to slot '{slotID}' ({savedFileCount} files)");
            return true;
        }
        catch (Exception e)
        {
            GD.PushError($"[SaveManager] Save failed: {e.Message}");
            return false;
        }
    }

    /// <summary>使用玩家可见名称创建独立手动存档；名称不会参与路径计算。</summary>
    public bool SaveAs(string displayName)
    {
        try
        {
            SaveSlotStore store = CreateSlotStore();
            string slotID = store.Create(displayName, _saveables);

            CurrentSlotID = slotID;
            GD.Print($"[SaveManager] Saved '{displayName}' to slot '{slotID}' ({_saveables.Count} files)");
            return true;
        }
        catch (Exception e)
        {
            GD.PushError($"[SaveManager] Save As failed: {e.Message}");
            return false;
        }
    }

    // ═══════════════════════════════════════════════
    // 加载
    // ═══════════════════════════════════════════════

    /// <summary>从指定存档槽加载所有已注册系统</summary>
    public bool Load(string slotID = AutosaveSlotID)
    {
        try
        {
            int loadedFileCount = CreateSlotStore().Load(slotID, _saveables);

            CurrentSlotID = slotID;
            GD.Print($"[SaveManager] Loaded from slot '{slotID}' ({loadedFileCount} files)");
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

    public bool SaveSlotExists(string slotID)
    {
        try
        {
            return CreateSlotStore().Exists(slotID);
        }
        catch (Exception e)
        {
            GD.PushError($"[SaveManager] Cannot inspect slot '{slotID}': {e.Message}");
            return false;
        }
    }

    public IReadOnlyList<SaveSlotSummary> ListSlots()
    {
        try
        {
            IReadOnlyList<SaveSlotSummary> summaries = CreateSlotStore().ListSlots();
            foreach (SaveSlotSummary summary in summaries)
            {
                if (!summary.IsValid)
                    GD.PushWarning($"[SaveManager] Invalid slot '{summary.SlotID}': {summary.Error}");
            }

            return summaries;
        }
        catch (Exception e)
        {
            GD.PushError($"[SaveManager] Cannot list save slots: {e.Message}");
            return Array.Empty<SaveSlotSummary>();
        }
    }

    public bool DeleteSlot(string slotID)
    {
        try
        {
            if (!CreateSlotStore().Delete(slotID))
                return false;
            if (string.Equals(CurrentSlotID, slotID, StringComparison.Ordinal))
                CurrentSlotID = AutosaveSlotID;

            GD.Print($"[SaveManager] Deleted slot '{slotID}'");
            return true;
        }
        catch (Exception e)
        {
            GD.PushError($"[SaveManager] Delete failed for slot '{slotID}': {e.Message}");
            return false;
        }
    }

    private SaveSlotStore CreateSlotStore() => new(GetSaveBaseDir());

    private string GetSaveBaseDir()
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

        if (string.IsNullOrWhiteSpace(manifest.SlotID))
            throw new JsonException("Save manifest slotId is missing.");
        if (string.IsNullOrWhiteSpace(manifest.DisplayName))
            throw new JsonException("Save manifest displayName is missing.");
        if (string.IsNullOrWhiteSpace(manifest.CityName))
            throw new JsonException("Save manifest cityName is missing.");
        if (manifest.Files is null)
            throw new JsonException("Save manifest files are missing.");
        try
        {
            SaveSlotStore.ValidateDisplayName(manifest.DisplayName);
        }
        catch (ArgumentException e)
        {
            throw new JsonException("Save manifest displayName is invalid.", e);
        }

        return manifest;
    }
}
