using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace SimpleCities.Core.V3;

public sealed record V3StrictTokenResult(
    bool Success,
    long InitialLength,
    long ConsumedBytes,
    bool EndOfFile,
    string? Sha256,
    string? Json,
    string? Error)
{
    public static V3StrictTokenResult Failure(long consumedBytes, string error) =>
        new(false, 0, consumedBytes, false, null, null, error);
}

/// <summary>
/// 严格 format v1 token reader：从禁止共享写/删的同一句柄读取完整字节，
/// 校验无 BOM、严格 UTF-8、重复属性名、canonical number lexeme、
/// 初始/已消费长度、EOF 与 SHA-256，并返回可继续解析的 JSON 文本。
/// </summary>
public static class V3StrictTokenReader
{
    public static V3StrictTokenResult ReadFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (!File.Exists(filePath))
            return V3StrictTokenResult.Failure(0, "FileMissing");

        byte[] bytes;
        long initialLength;
        try
        {
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            initialLength = stream.Length;
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            bytes = buffer.ToArray();
        }
        catch (IOException)
        {
            return V3StrictTokenResult.Failure(0, "FileUnreadable");
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return V3StrictTokenResult.Failure(3, "BomNotAllowed");

        string json;
        try
        {
            json = new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return V3StrictTokenResult.Failure(bytes.LongLength, "InvalidUtf8");
        }

        string? error = ValidateTokens(bytes);
        if (error is not null)
            return V3StrictTokenResult.Failure(bytes.LongLength, error);

        string sha256 = V3PayloadDigest.ComputeSha256(bytes);
        return new V3StrictTokenResult(true, initialLength, bytes.LongLength, true, sha256, json, null);
    }

    private static string? ValidateTokens(byte[] bytes)
    {
        var stack = new Stack<HashSet<string>>();
        var reader = new Utf8JsonReader(bytes);

        try
        {
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
                            return $"DuplicateKey:{key}";
                        break;
                    case JsonTokenType.Number:
                        string token = Encoding.UTF8.GetString(reader.ValueSpan);
                        if (!IsValidCanonicalNumber(token))
                            return "InvalidNumberToken";
                        break;
                }
            }
        }
        catch (JsonException)
        {
            return "MalformedJson";
        }

        return null;
    }

    private static bool IsValidCanonicalNumber(string token)
    {
        bool isFloat = token.IndexOf('.') >= 0 || token.IndexOf('e') >= 0 || token.IndexOf('E') >= 0;
        return isFloat
            ? V3JsonLexeme.IsValidFiniteFloatLexeme(token)
            : V3JsonLexeme.IsValidCanonicalInteger(token);
    }
}
