namespace SimpleCities.Core.V3;

/// <summary>
/// V3 保存根与格式常量。编辑器和导出统一使用 `user://saves-v3`，
/// 与 V2 的 `res://saves` / `user://saves` 完全隔离。
/// </summary>
public static class V3SaveRoot
{
    public const string EditorRoot = "user://saves-v3";
    public const string ExportRoot = "user://saves-v3";
    public const string FormatFamily = "simple-cities-v3";
    public const int SchemaVersion = 1;

    public const string V2EditorRoot = "res://saves";
    public const string V2ExportRoot = "user://saves";

    public static string GetRoot(bool isExport) => isExport ? ExportRoot : EditorRoot;

    public static bool IsV2Root(string path) =>
        string.Equals(path, V2EditorRoot, System.StringComparison.Ordinal) ||
        string.Equals(path, V2ExportRoot, System.StringComparison.Ordinal);
}
