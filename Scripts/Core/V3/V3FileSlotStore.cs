using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SimpleCities.Core.V3;

/// <summary>
/// 基于真实文件系统的 V3 槽存储：在 root 下按 slotId 目录保存 manifest/payload，
/// 使用根锁文件进行排他。
/// </summary>
public sealed class V3FileSlotStore
{
    private readonly string _root;

    public V3FileSlotStore(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("Root must not be empty.", nameof(root));
        _root = root;
        Directory.CreateDirectory(_root);
    }

    public bool Save(string slotId, V3Manifest manifest, IReadOnlyDictionary<string, byte[]> payloads)
    {
        if (!V3SlotId.IsValid(slotId) || !string.Equals(slotId, manifest.SlotId, StringComparison.Ordinal))
            return false;

        using var fileLock = new V3FileLock();
        if (!fileLock.TryAcquire(GetLockPath()))
            return false;

        try
        {
            IReadOnlyDictionary<string, byte[]> files = V3SlotWriter.BuildFiles(manifest, payloads);
            string slotDirectory = Path.Combine(_root, slotId);
            Directory.CreateDirectory(slotDirectory);
            foreach (KeyValuePair<string, byte[]> file in files)
                File.WriteAllBytes(Path.Combine(slotDirectory, file.Key), file.Value);
            return true;
        }
        finally
        {
            fileLock.Dispose();
        }
    }

    public V3SlotReadResult Load(string slotId)
    {
        if (!V3SlotId.IsValid(slotId))
            return V3SlotReadResult.Failure("InvalidSlotId");

        string slotDirectory = Path.Combine(_root, slotId);
        if (!Directory.Exists(slotDirectory))
            return V3SlotReadResult.Failure("MissingSlot");

        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (string filePath in Directory.EnumerateFiles(slotDirectory))
            files[Path.GetFileName(filePath)] = File.ReadAllBytes(filePath);

        return V3SlotReader.Read(files);
    }

    public bool Delete(string slotId)
    {
        if (!V3SlotId.IsValid(slotId))
            return false;

        string slotDirectory = Path.Combine(_root, slotId);
        if (!Directory.Exists(slotDirectory))
            return false;

        Directory.Delete(slotDirectory, recursive: true);
        return true;
    }

    public IReadOnlyList<V3SlotSummary> List()
    {
        var summaries = new List<V3SlotSummary>();
        foreach (string directory in Directory.EnumerateDirectories(_root))
        {
            string name = Path.GetFileName(directory);
            if (!V3SlotId.IsValid(name))
            {
                summaries.Add(new V3SlotSummary(name, name, V3SlotOccupant.Unsafe, null));
                continue;
            }

            string manifestPath = Path.Combine(directory, V3SlotReader.ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                summaries.Add(new V3SlotSummary(name, name, V3SlotOccupant.Foreign, null));
                continue;
            }

            if (!V3SlotIntegrity.Verify(directory).Success)
            {
                summaries.Add(new V3SlotSummary(name, name, V3SlotOccupant.CorruptV3, null));
                continue;
            }

            V3ManifestCodecResult manifestResult = V3ManifestStrictFileReader.Read(manifestPath);
            if (!manifestResult.Success || manifestResult.Manifest is null)
            {
                summaries.Add(new V3SlotSummary(name, name, V3SlotOccupant.CorruptV3, null));
                continue;
            }

            summaries.Add(new V3SlotSummary(
                name,
                manifestResult.Manifest.DisplayName,
                V3SlotOccupant.CompleteV3,
                manifestResult.Manifest.Timestamp));
        }

        return summaries
            .OrderBy(summary => summary.SlotId, StringComparer.Ordinal)
            .ToList();
    }

    private string GetLockPath() => Path.Combine(_root, ".save-root.lock");
}
