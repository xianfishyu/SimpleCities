using System.Text.Json;
using System.Text.RegularExpressions;

namespace SimpleCities.Tests;

public sealed class ExportPresetContractTests
{
    [Fact]
    public void WindowsDesktopQa_ExportsOnlyTheDedicatedSaveContractResourcesFromTests()
    {
        string projectRoot = FindProjectRoot();
        IReadOnlyDictionary<string, string> preset = ReadPreset(projectRoot, "Windows Desktop QA");
        Assert.Equal("all_resources", DecodeString(preset["export_filter"]));

        string[] excludePatterns = DecodeString(preset["exclude_filter"])
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Contains("tests/SimpleCities.RoadGraph.Tests/**", excludePatterns);
        Assert.Contains("tests/SimpleCities.RoadGraph.Performance/**", excludePatterns);
        Assert.Contains("tests/SimpleCities.SaveLockProbe/**", excludePatterns);

        string testsRoot = Path.Combine(projectRoot, "tests");
        string[] exportedTestFiles = Directory
            .EnumerateFiles(testsRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(projectRoot, path).Replace('\\', '/'))
            .Where(path => !excludePatterns.Any(pattern => GlobMatches(pattern, path)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "tests/godot/exported_save_runtime_contract.gd",
                "tests/godot/exported_save_runtime_contract.gd.uid",
                "tests/godot/exported_save_runtime_contract.tscn",
                "tests/godot/v3_save_fixture.gd",
                "tests/godot/v3_save_fixture.gd.uid",
            ],
            exportedTestFiles);
    }

    private static IReadOnlyDictionary<string, string> ReadPreset(
        string projectRoot,
        string presetName)
    {
        var sections = new List<Dictionary<string, string>>();
        Dictionary<string, string>? current = null;
        foreach (string rawLine in File.ReadLines(Path.Combine(projectRoot, "export_presets.cfg")))
        {
            string line = rawLine.Trim();
            if (line.StartsWith("[preset.", StringComparison.Ordinal) &&
                line.EndsWith(']') &&
                !line.EndsWith(".options]", StringComparison.Ordinal))
            {
                current = new Dictionary<string, string>(StringComparer.Ordinal);
                sections.Add(current);
                continue;
            }
            if (current is null || line.Length == 0 || line.StartsWith(';'))
                continue;
            int separator = line.IndexOf('=');
            if (separator > 0)
                current[line[..separator]] = line[(separator + 1)..];
        }

        return Assert.Single(
            sections,
            section => section.TryGetValue("name", out string? value) &&
                       string.Equals(DecodeString(value), presetName, StringComparison.Ordinal));
    }

    private static string DecodeString(string encoded) =>
        JsonSerializer.Deserialize<string>(encoded) ??
        throw new InvalidDataException("Export preset string cannot be null.");

    private static bool GlobMatches(string pattern, string path)
    {
        string expression = "^" + Regex.Escape(pattern)
            .Replace(@"\*\*", ".*", StringComparison.Ordinal)
            .Replace(@"\*", "[^/]*", StringComparison.Ordinal)
            .Replace(@"\?", "[^/]", StringComparison.Ordinal) + "$";
        return Regex.IsMatch(path, expression, RegexOptions.CultureInvariant);
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "SimpleCities.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName
            ?? throw new DirectoryNotFoundException("SimpleCities project root was not found.");
    }
}
