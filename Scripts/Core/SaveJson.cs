using System.Text.Json;

/// <summary>
/// JSON 序列化/反序列化静态工具。
/// 统一序列化选项，供 SaveManager 和各子系统使用。
/// </summary>
public static class SaveJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>将对象序列化为 JSON 字符串</summary>
    public static string Serialize(object data) =>
        JsonSerializer.Serialize(data, Options);

    /// <summary>将 JSON 字符串反序列化为指定类型</summary>
    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options)!;
}
