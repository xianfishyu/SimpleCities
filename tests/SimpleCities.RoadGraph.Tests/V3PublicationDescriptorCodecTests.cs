using System.Text;
using System.Text.Json;

namespace SimpleCities.Tests;

public sealed class V3PublicationDescriptorCodecTests
{
    private const string OldDigest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string NewDigest = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string OperationToken = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void WriteRead_RoundTripsDeterministicCanonicalDescriptor()
    {
        var descriptor = new V3PublicationDescriptor(
            "manual-1", OperationToken, OldDigest, NewDigest, "staging", "backup");

        string first = Write(descriptor);
        string second = Write(descriptor);
        V3PublicationDescriptor restored = Read(first);

        Assert.Equal(first, second);
        Assert.DoesNotContain('\n', first);
        Assert.Equal(descriptor, restored);
    }

    [Theory]
    [InlineData("\"descriptorType\":\"publish\"", "\"DescriptorType\":\"publish\"")]
    [InlineData("\"descriptorType\":\"publish\"", "\"descriptorType\":\"delete\"")]
    [InlineData("\"stagingPath\":\"staging\"", "\"stagingPath\":\"../staging\"")]
    [InlineData("\"backupPath\":\"backup\"", "\"backupPath\":\"Backup\"")]
    [InlineData(OperationToken, "0123456789ABCDEF0123456789ABCDEF")]
    public void Read_RejectsNonCanonicalIdentityOrPaths(string original, string replacement)
    {
        string json = Write(new V3PublicationDescriptor(
            "manual-1", OperationToken, OldDigest, NewDigest, "staging", "backup"));

        Assert.ThrowsAny<JsonException>(() => Read(
            json.Replace(original, replacement, StringComparison.Ordinal)));
    }

    [Fact]
    public void Read_RejectsDuplicateKnownProperty()
    {
        string json = Write(new V3PublicationDescriptor(
            "manual-1", OperationToken, OldDigest, NewDigest, "staging", "backup"));
        string duplicate = json.Replace(
            "{\"formatFamily\"",
            "{\"formatFamily\":\"simple-cities-v3\",\"formatFamily\"",
            StringComparison.Ordinal);

        Assert.Throws<JsonException>(() => Read(duplicate));
    }

    [Fact]
    public void Read_RejectsDescriptorOverByteBudgetBeforeJsonAllocation()
    {
        string oversized = "{" + new string(' ', V3PublicationDescriptorCodec.MaximumEncodedBytes) + "}";

        Assert.Throws<InvalidDataException>(() => Read(oversized));
    }

    private static string Write(V3PublicationDescriptor descriptor)
    {
        using var stream = new MemoryStream();
        V3PublicationDescriptorCodec.Write(stream, descriptor);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static V3PublicationDescriptor Read(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json), writable: false);
        return V3PublicationDescriptorCodec.Read(stream);
    }
}
