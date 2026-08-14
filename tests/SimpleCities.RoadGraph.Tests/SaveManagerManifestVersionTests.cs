using System.Text;
using System.Text.Json;

namespace SimpleCities.Tests;

public sealed class SaveManagerManifestVersionTests
{
    private const string ValidManifest = """
        {"formatFamily":"simple-cities-v3","schemaVersion":1,"slotId":"autosave","displayName":"Autosave","timestamp":"2026-08-04T00:00:00.0000000Z","cityName":"My City","population":null,"funds":null,"thumbnailFile":null,"files":[{"name":"road_network.json","encodedLength":2,"sha256":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"}]}
        """;

    [Fact]
    public void Read_ExactV3ManifestIsAccepted()
    {
        V3Manifest manifest = Read(ValidManifest);

        Assert.Equal("autosave", manifest.SlotID);
        Assert.Equal("Autosave", manifest.DisplayName);
        V3ManifestFile file = Assert.Single(manifest.Files);
        Assert.Equal("road_network.json", file.Name);
        Assert.Equal(2, file.EncodedLength);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(31)]
    public void Read_HandlesChunkBoundariesAndNonSeekableStreams(int chunkSize)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(ValidManifest);
        using var stream = new ChunkedNonSeekableStream(bytes, chunkSize);

        V3Manifest manifest = V3ManifestCodec.Read(stream);

        Assert.Equal("autosave", manifest.SlotID);
        Assert.Equal(bytes.Length, stream.BytesRead);
    }

    [Fact]
    public void Read_RejectsManifestAboveEncodedByteBudget()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(
            ValidManifest + new string(' ', checked((int)V3StorageBudget.MaximumManifestEncodedBytes)));
        using var stream = new MemoryStream(bytes, writable: false);

        Assert.Throws<JsonException>(() => V3ManifestCodec.Read(stream));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(V3StorageBudget.MaximumManifestFiles + 1)]
    public void Read_RejectsBusinessFileCountOutsideBudget(int count)
    {
        string manifest = CreateManifest(Enumerable.Range(0, count)
            .Select(index => new V3ManifestFile(
                $"payload_{index:D2}.json",
                1,
                new string('a', 64)))
            .ToArray());

        Assert.Throws<JsonException>(() => Read(manifest));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(V3StorageBudget.MaximumPayloadEncodedBytes + 1)]
    public void Read_RejectsPayloadLengthOutsideBudget(long encodedLength)
    {
        string manifest = CreateManifest(
        [
            new V3ManifestFile("road_network.json", encodedLength, new string('a', 64)),
        ]);

        Assert.Throws<JsonException>(() => Read(manifest));
    }

    [Fact]
    public void Read_RejectsAggregatePayloadLengthAboveSlotBudget()
    {
        string manifest = CreateManifest(
        [
            new V3ManifestFile("a.json", V3StorageBudget.MaximumPayloadEncodedBytes, new string('a', 64)),
            new V3ManifestFile("b.json", V3StorageBudget.MaximumPayloadEncodedBytes, new string('b', 64)),
            new V3ManifestFile("c.json", 1, new string('c', 64)),
        ]);

        Assert.Throws<JsonException>(() => Read(manifest));
    }

    [Theory]
    [InlineData("\"formatFamily\":\"simple-cities-v3\",", "")]
    [InlineData("simple-cities-v3", "Simple-Cities-V3")]
    [InlineData("\"schemaVersion\":1,", "")]
    [InlineData("\"schemaVersion\":1", "\"schemaVersion\":0")]
    [InlineData("\"schemaVersion\":1", "\"schemaVersion\":2")]
    [InlineData("\"schemaVersion\":1", "\"schemaVersion\":1.0")]
    [InlineData("\"formatFamily\"", "\"FormatFamily\"")]
    public void Read_RejectsMissingWrongOrCaseVariantAdmission(
        string original,
        string replacement)
    {
        Assert.Throws<JsonException>(() => Read(
            ValidManifest.Replace(original, replacement, StringComparison.Ordinal)));
    }

    [Fact]
    public void Read_RejectsDuplicateKnownProperty()
    {
        string duplicate = ValidManifest.Replace(
            "{\"formatFamily\"",
            "{\"formatFamily\":\"simple-cities-v3\",\"formatFamily\"",
            StringComparison.Ordinal);

        Assert.Throws<JsonException>(() => Read(duplicate));
    }

    [Theory]
    [InlineData("2026-08-04T00:00:00Z")]
    [InlineData("2026-08-04T08:00:00.0000000+08:00")]
    [InlineData("not-a-time")]
    public void Read_RejectsNonCanonicalTimestamp(string timestamp)
    {
        Assert.ThrowsAny<JsonException>(() => Read(ValidManifest.Replace(
            "2026-08-04T00:00:00.0000000Z",
            timestamp,
            StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("../road_network.json")]
    [InlineData("road_network.txt")]
    [InlineData("Manifest.json")]
    [InlineData("road.network.json")]
    public void Read_RejectsUnsafeOrNonCanonicalPayloadName(string fileName)
    {
        Assert.ThrowsAny<Exception>(() => Read(ValidManifest.Replace(
            "road_network.json",
            fileName,
            StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("1e2")]
    [InlineData("01")]
    [InlineData("-0")]
    public void Read_RejectsNonCanonicalEncodedLength(string length)
    {
        Assert.ThrowsAny<JsonException>(() => Read(ValidManifest.Replace(
            "\"encodedLength\":2",
            $"\"encodedLength\":{length}",
            StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef0123456789")]
    [InlineData("abcd")]
    public void Read_RejectsNonCanonicalSha256(string sha256)
    {
        Assert.ThrowsAny<JsonException>(() => Read(ValidManifest.Replace(
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            sha256,
            StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("1e2")]
    [InlineData("01")]
    [InlineData("1.")]
    [InlineData("1.234")]
    [InlineData("-0")]
    public void Read_RejectsNonCanonicalFunds(string funds)
    {
        Assert.ThrowsAny<JsonException>(() => Read(ValidManifest.Replace(
            "\"funds\":null",
            $"\"funds\":{funds}",
            StringComparison.Ordinal)));
    }

    [Fact]
    public void Write_IsDeterministicUnindentedAndSortedByFileName()
    {
        var manifest = new V3Manifest(
            "manual-1",
            "River City",
            "2026-08-04T00:00:00.0000000Z",
            "River City",
            null,
            12.50m,
            null,
            [
                new V3ManifestFile("road_network.json", 3, new string('b', 64)),
                new V3ManifestFile("economy.json", 2, new string('a', 64)),
            ]);

        string first = Write(manifest);
        string second = Write(manifest);

        Assert.Equal(first, second);
        Assert.DoesNotContain('\n', first);
        Assert.True(first.IndexOf("economy.json", StringComparison.Ordinal) <
                    first.IndexOf("road_network.json", StringComparison.Ordinal));
        V3Manifest restored = Read(first);
        Assert.Equal(manifest.SlotID, restored.SlotID);
        Assert.Equal(manifest.DisplayName, restored.DisplayName);
        Assert.Equal(manifest.Timestamp, restored.Timestamp);
        Assert.Equal(manifest.CityName, restored.CityName);
        Assert.Equal(manifest.Population, restored.Population);
        Assert.Equal(manifest.Funds, restored.Funds);
        Assert.Equal(manifest.ThumbnailFile, restored.ThumbnailFile);
        Assert.Equal(
            manifest.Files.OrderBy(file => file.Name, StringComparer.Ordinal),
            restored.Files);
    }

    private static V3Manifest Read(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json), writable: false);
        return V3ManifestCodec.Read(stream);
    }

    private static string Write(V3Manifest manifest)
    {
        using var stream = new MemoryStream();
        V3ManifestCodec.Write(stream, manifest);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string CreateManifest(IReadOnlyList<V3ManifestFile> files)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        writer.WriteString("formatFamily", "simple-cities-v3");
        writer.WriteNumber("schemaVersion", 1);
        writer.WriteString("slotId", "autosave");
        writer.WriteString("displayName", "Autosave");
        writer.WriteString("timestamp", "2026-08-04T00:00:00.0000000Z");
        writer.WriteString("cityName", "My City");
        writer.WriteNull("population");
        writer.WriteNull("funds");
        writer.WriteNull("thumbnailFile");
        writer.WriteStartArray("files");
        foreach (V3ManifestFile file in files)
        {
            writer.WriteStartObject();
            writer.WriteString("name", file.Name);
            writer.WriteNumber("encodedLength", file.EncodedLength);
            writer.WriteString("sha256", file.Sha256);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private sealed class ChunkedNonSeekableStream(byte[] data, int chunkSize) : Stream
    {
        private int _position;

        internal int BytesRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            int count = Math.Min(Math.Min(buffer.Length, chunkSize), data.Length - _position);
            if (count == 0)
                return 0;
            data.AsSpan(_position, count).CopyTo(buffer);
            _position += count;
            BytesRead += count;
            return count;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
