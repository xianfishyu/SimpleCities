using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Text.Json;

internal readonly record struct V3JsonReaderBudget(
    long MaximumEncodedBytes,
    long MaximumTokens,
    int MaximumDepth,
    int MaximumPropertyNameBytes,
    int MaximumStringTokenBytes,
    int MaximumNumberTokenBytes)
{
    internal void Validate()
    {
        if (MaximumEncodedBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaximumEncodedBytes));
        if (MaximumTokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaximumTokens));
        if (MaximumDepth <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaximumDepth));
        if (MaximumPropertyNameBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaximumPropertyNameBytes));
        if (MaximumStringTokenBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaximumStringTokenBytes));
        if (MaximumNumberTokenBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaximumNumberTokenBytes));
    }
}

internal static class V3StorageBudget
{
    internal const long MaximumManifestEncodedBytes = 64 * 1024;
    internal const int MaximumManifestFiles = 64;
    internal const long MaximumPayloadEncodedBytes = 8L * 1024 * 1024 * 1024;
    internal const long MaximumSlotEncodedBytes = 16L * 1024 * 1024 * 1024;
    internal const long MaximumThumbnailEncodedBytes = 16L * 1024 * 1024;
    internal const long MaximumThumbnailDecodedBytes = 128L * 1024 * 1024;
    internal const int MaximumThumbnailDimension = 4096;
    internal const long MaximumThumbnailPixels = 16_777_216;

    internal static V3JsonReaderBudget ManifestJson { get; } = new(
        MaximumManifestEncodedBytes,
        2_048,
        16,
        64,
        4 * 1024,
        64);

    internal static V3JsonReaderBudget CreateRoadGraphJson(RoadGraphCapacity capacity)
    {
        ArgumentNullException.ThrowIfNull(capacity);
        long maximumEncodedBytes = SaturatingAdd(
            1024 * 1024,
            SaturatingMultiply(capacity.MaximumNodes, 128),
            SaturatingMultiply(capacity.MaximumEdges, 256),
            SaturatingMultiply(capacity.MaximumGeometrySegments, 1024));
        maximumEncodedBytes = Math.Min(maximumEncodedBytes, MaximumPayloadEncodedBytes);

        long maximumTokens = SaturatingAdd(
            64,
            SaturatingMultiply(capacity.MaximumNodes, 8),
            SaturatingMultiply(capacity.MaximumEdges, 16),
            SaturatingMultiply(capacity.MaximumGeometrySegments, 40));
        return new V3JsonReaderBudget(
            maximumEncodedBytes,
            maximumTokens,
            16,
            64,
            4 * 1024,
            64);
    }

    private static long SaturatingMultiply(long left, long right) =>
        left <= 0 || right <= 0 ? 0 : left > long.MaxValue / right ? long.MaxValue : left * right;

    private static long SaturatingAdd(params long[] values)
    {
        long total = 0;
        foreach (long value in values)
        {
            if (value < 0 || total > long.MaxValue - value)
                return long.MaxValue;
            total += value;
        }
        return total;
    }
}

internal sealed class V3JsonStreamReader : IDisposable
{
    private const int BufferSize = 8 * 1024;

    private readonly Stream _source;
    private readonly V3JsonReaderBudget _budget;
    private readonly byte[] _buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
    private readonly byte[] _overflowProbe = new byte[1];
    private JsonReaderState _state;
    private int _buffered;
    private int _consumed;
    private long _discarded;
    private long _bytesRead;
    private long _tokenCount;
    private bool _isFinalBlock;
    private bool _finished;
    private bool _returnedBuffer;
    private string? _textValue;

    internal V3JsonStreamReader(Stream source, V3JsonReaderBudget budget)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
            throw new ArgumentException("JSON source must be readable.", nameof(source));
        budget.Validate();
        _source = source;
        _budget = budget;
        _state = new JsonReaderState(new JsonReaderOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = budget.MaximumDepth,
        });
    }

    ~V3JsonStreamReader() => ReturnBuffer();

    internal JsonTokenType TokenType { get; private set; }
    internal long BytesRead => _bytesRead;
    internal long BytesConsumed => _discarded + _consumed;
    internal long TokenCount => _tokenCount;

    internal bool Read()
    {
        if (_finished)
            return false;
        _textValue = null;

        while (true)
        {
            CompactConsumedBytes();
            FillAvailableBuffer();

            var reader = new Utf8JsonReader(
                _buffer.AsSpan(0, _buffered),
                _isFinalBlock,
                _state);
            if (reader.Read())
            {
                _state = reader.CurrentState;
                _consumed = checked((int)reader.BytesConsumed);
                TokenType = reader.TokenType;
                _tokenCount++;
                if (_tokenCount > _budget.MaximumTokens)
                    throw new JsonException("JSON token budget is exceeded.");
                CaptureTokenValue(ref reader);
                return true;
            }

            _state = reader.CurrentState;
            _consumed = checked((int)reader.BytesConsumed);
            if (_isFinalBlock)
            {
                _finished = true;
                ReturnBuffer();
                return false;
            }
            if (_consumed == 0 && _buffered == _buffer.Length)
                throw new JsonException("JSON token exceeds its bounded reader buffer.");
        }
    }

    internal void ReadRequired(string context)
    {
        if (!Read())
            throw new JsonException($"{context} ended before its required JSON token.");
    }

    internal void Expect(JsonTokenType expected, string context)
    {
        if (TokenType != expected)
            throw new JsonException($"{context} must be {expected}, not {TokenType}.");
    }

    internal string GetPropertyName(string context)
    {
        Expect(JsonTokenType.PropertyName, context);
        return _textValue ?? throw new JsonException($"{context} property name is unavailable.");
    }

    internal string GetString(string context)
    {
        Expect(JsonTokenType.String, context);
        return _textValue ?? throw new JsonException($"{context} string value cannot be null.");
    }

    internal string GetNumberToken(string context)
    {
        Expect(JsonTokenType.Number, context);
        return _textValue ?? throw new JsonException($"{context} number token is unavailable.");
    }

    internal void RequireEndOfDocument(string context)
    {
        if (Read())
            throw new JsonException($"{context} contains more than one JSON value.");
        if (BytesConsumed != BytesRead)
            throw new JsonException($"{context} was not consumed to EOF.");
    }

    public void Dispose()
    {
        ReturnBuffer();
        GC.SuppressFinalize(this);
    }

    private void CaptureTokenValue(ref Utf8JsonReader reader)
    {
        ReadOnlySpan<byte> value = reader.HasValueSequence
            ? reader.ValueSequence.ToArray()
            : reader.ValueSpan;
        switch (reader.TokenType)
        {
            case JsonTokenType.PropertyName:
                if (reader.ValueIsEscaped || value.Length > _budget.MaximumPropertyNameBytes)
                    throw new JsonException("JSON property name exceeds its raw UTF-8 budget or is escaped.");
                _textValue = Encoding.UTF8.GetString(value);
                break;
            case JsonTokenType.String:
                if (value.Length > _budget.MaximumStringTokenBytes)
                    throw new JsonException("JSON string token exceeds its raw UTF-8 budget.");
                _textValue = reader.GetString();
                break;
            case JsonTokenType.Number:
                if (value.Length > _budget.MaximumNumberTokenBytes)
                    throw new JsonException("JSON number token exceeds its lexeme budget.");
                _textValue = Encoding.UTF8.GetString(value);
                break;
        }
    }

    private void CompactConsumedBytes()
    {
        if (_consumed == 0)
            return;
        int remaining = _buffered - _consumed;
        if (remaining > 0)
            Buffer.BlockCopy(_buffer, _consumed, _buffer, 0, remaining);
        _discarded += _consumed;
        _buffered = remaining;
        _consumed = 0;
    }

    private void FillAvailableBuffer()
    {
        if (_isFinalBlock || _buffered == _buffer.Length)
            return;

        long remainingBudget = _budget.MaximumEncodedBytes - _bytesRead;
        if (remainingBudget <= 0)
        {
            int overflow = _source.Read(_overflowProbe, 0, 1);
            if (overflow == 0)
            {
                _isFinalBlock = true;
                return;
            }
            throw new JsonException("JSON encoded byte budget is exceeded.");
        }

        int requested = (int)Math.Min(_buffer.Length - _buffered, remainingBudget);
        int read = _source.Read(_buffer, _buffered, requested);
        if (read == 0)
        {
            _isFinalBlock = true;
            return;
        }
        _buffered += read;
        _bytesRead += read;
    }

    private void ReturnBuffer()
    {
        if (_returnedBuffer)
            return;
        _returnedBuffer = true;
        ArrayPool<byte>.Shared.Return(_buffer);
    }
}
