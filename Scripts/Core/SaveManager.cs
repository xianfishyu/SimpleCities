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
    private const string ExportSaveBaseDir = "user://saves";
    internal const string RoadGraphSaveFileName = "road_network";
    private static readonly string[] V2SaveFileNames = [RoadGraphSaveFileName];
    public const string AutosaveSlotID = "autosave";
    public const string AutosaveDisplayName = "自动存档";
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
                string.Equals(existing.SaveFileName, saveable.SaveFileName, StringComparison.OrdinalIgnoreCase));
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
                ? AutosaveDisplayName
                : store.ReadManifest(slotID).DisplayName;
            int savedFileCount = store.Save(slotID, displayName, GetV2Saveables());

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

    /// <summary>覆盖保留自动槽，但不改变玩家当前选中的手动槽。</summary>
    public bool SaveAutosave()
    {
        try
        {
            int savedFileCount = CreateSlotStore().Save(
                AutosaveSlotID,
                AutosaveDisplayName,
                GetV2Saveables());
            GD.Print($"[SaveManager] Autosaved ({savedFileCount} files)");
            return true;
        }
        catch (Exception e)
        {
            GD.PushError($"[SaveManager] Autosave failed: {e.Message}");
            return false;
        }
    }

    /// <summary>使用玩家可见名称创建独立手动存档；名称不会参与路径计算。</summary>
    public bool SaveAs(string displayName)
    {
        try
        {
            SaveSlotStore store = CreateSlotStore();
            IReadOnlyList<ISaveable> saveables = GetV2Saveables();
            string slotID = store.Create(displayName, saveables);

            CurrentSlotID = slotID;
            GD.Print($"[SaveManager] Saved '{displayName}' to slot '{slotID}' ({saveables.Count} files)");
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
            int loadedFileCount = CreateSlotStore().Load(slotID, GetV2Saveables());

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

    private IReadOnlyList<ISaveable> GetV2Saveables() =>
        SelectSaveables(_saveables, V2SaveFileNames);

    internal static IReadOnlyList<ISaveable> SelectSaveables(
        IReadOnlyList<ISaveable> saveables,
        IReadOnlyList<string> saveFileNames)
    {
        var selected = new List<ISaveable>(saveFileNames.Count);
        foreach (string saveFileName in saveFileNames)
        {
            ISaveable? saveable = null;
            foreach (ISaveable candidate in saveables)
            {
                if (!string.Equals(candidate.SaveFileName, saveFileName, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (saveable != null)
                    throw new InvalidOperationException($"Multiple saveables provide '{saveFileName}'.");
                saveable = candidate;
            }

            selected.Add(saveable ?? throw new InvalidOperationException(
                $"Required saveable '{saveFileName}' is not registered."));
        }

        return selected;
    }

    private string GetSaveBaseDir() => ResolveSaveBaseDir(
        OS.HasFeature("editor"),
        ProjectSettings.GlobalizePath);

    internal static string ResolveSaveBaseDir(
        bool isEditor,
        Func<string, string> globalizePath)
    {
        ArgumentNullException.ThrowIfNull(globalizePath);
        return globalizePath(isEditor ? EditorSaveBaseDir : ExportSaveBaseDir);
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
        if (!DateTimeOffset.TryParse(
                manifest.Timestamp,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out DateTimeOffset savedAt) ||
            savedAt.Offset != TimeSpan.Zero)
        {
            throw new JsonException("Save manifest timestamp must be a valid UTC time.");
        }
        try
        {
            SaveSlotStore.ValidateDisplayName(manifest.DisplayName);
        }
        catch (ArgumentException e)
        {
            throw new JsonException("Save manifest displayName is invalid.", e);
        }

        var fileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string fileName in manifest.Files)
        {
            try
            {
                SaveSlotStore.ValidateManifestFileName(fileName);
            }
            catch (ArgumentException e)
            {
                throw new JsonException($"Save manifest file '{fileName}' is invalid.", e);
            }

            if (!fileNames.Add(fileName))
                throw new JsonException($"Save manifest contains duplicate file '{fileName}'.");
        }

        return manifest;
    }
}
