using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace SimpleCities.Core.V3;

/// <summary>
/// 使用 Utf8JsonReader 检测 JSON 对象中的重复属性名。
/// </summary>
public static class V3JsonDuplicateDetector
{
    public static bool TryDetectDuplicateKey(string json, out string? duplicateKey)
    {
        ArgumentNullException.ThrowIfNull(json);

        var stack = new Stack<HashSet<string>>();
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    stack.Push(new HashSet<string>(StringComparer.Ordinal));
                    break;
                case JsonTokenType.EndObject:
                    if (stack.Count > 0)
                        stack.Pop();
                    break;
                case JsonTokenType.PropertyName:
                    string key = reader.GetString() ?? string.Empty;
                    if (stack.Count > 0 && !stack.Peek().Add(key))
                    {
                        duplicateKey = key;
                        return true;
                    }
                    break;
            }
        }

        duplicateKey = null;
        return false;
    }
}
