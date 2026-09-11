using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>由场景装配提供的保存根和必需载荷，创建后保持不变。</summary>
internal sealed class SceneStoragePolicy
{
    internal SceneStoragePolicy(string saveBasePath, IEnumerable<string> requiredSaveFileNames)
    {
        if (string.IsNullOrWhiteSpace(saveBasePath))
            throw new ArgumentException("A scene save root is required.", nameof(saveBasePath));
        ArgumentNullException.ThrowIfNull(requiredSaveFileNames);
        string[] names = requiredSaveFileNames.ToArray();
        if (names.Length == 0 || names.Any(string.IsNullOrWhiteSpace) ||
            names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length)
        {
            throw new ArgumentException("Scene payload names must be nonempty and unique.", nameof(requiredSaveFileNames));
        }

        SaveBasePath = saveBasePath;
        RequiredSaveFileNames = Array.AsReadOnly(names);
    }

    internal string SaveBasePath { get; }
    internal IReadOnlyList<string> RequiredSaveFileNames { get; }

    internal string ResolveSaveBaseDir(Func<string, string> globalizePath)
    {
        ArgumentNullException.ThrowIfNull(globalizePath);
        string resolved = globalizePath(SaveBasePath);
        if (string.IsNullOrWhiteSpace(resolved))
            throw new InvalidOperationException("The scene save root could not be resolved.");
        return resolved;
    }

    internal IReadOnlyList<IStreamingSaveable> SelectParticipants(IReadOnlyList<IStreamingSaveable> saveables)
    {
        ArgumentNullException.ThrowIfNull(saveables);
        var selected = new List<IStreamingSaveable>(RequiredSaveFileNames.Count);
        foreach (string fileName in RequiredSaveFileNames)
        {
            IStreamingSaveable? match = null;
            foreach (IStreamingSaveable candidate in saveables)
            {
                if (!string.Equals(candidate.SaveFileName, fileName, StringComparison.Ordinal))
                    continue;
                if (match is not null)
                    throw new InvalidOperationException($"Multiple saveables provide '{fileName}'.");
                match = candidate;
            }
            selected.Add(match ?? throw new InvalidOperationException(
                $"Required saveable '{fileName}' is not registered."));
        }
        return selected;
    }
}
