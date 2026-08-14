using System.Text;
using System.Text.Json;

namespace SimpleCities.Tests;

public sealed class V3DeletionDescriptorCodecTests
{
    private const string Digest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string OperationToken = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void WriteRead_RoundTripsCompleteAndCorruptOccupants()
    {
        foreach (SaveSlotOccupantKind kind in new[]
                 {
                     SaveSlotOccupantKind.CompleteV3,
                     SaveSlotOccupantKind.CorruptV3,
                 })
        {
            var descriptor = new V3DeletionDescriptor(
                "manual-1", OperationToken, 7, kind, Digest, "tombstone", "Manual");

            string json = Write(descriptor);

            Assert.DoesNotContain('\n', json);
            Assert.Equal(descriptor, Read(json));
        }
    }

    [Theory]
    [InlineData("\"descriptorType\":\"delete\"", "\"descriptorType\":\"publish\"")]
    [InlineData("\"uiGeneration\":7", "\"uiGeneration\":0")]
    [InlineData("\"uiGeneration\":7", "\"uiGeneration\":7.0")]
    [InlineData("\"occupantKind\":\"completeV3\"", "\"occupantKind\":\"foreign\"")]
    [InlineData("\"tombstonePath\":\"tombstone\"", "\"tombstonePath\":\"../slot\"")]
    [InlineData(OperationToken, "0123456789ABCDEF0123456789ABCDEF")]
    public void Read_RejectsInvalidAuthorityOrPath(string original, string replacement)
    {
        string json = Write(new V3DeletionDescriptor(
            "manual-1", OperationToken, 7, SaveSlotOccupantKind.CompleteV3,
            Digest, "tombstone", "Manual"));

        Assert.ThrowsAny<JsonException>(() => Read(
            json.Replace(original, replacement, StringComparison.Ordinal)));
    }

    [Fact]
    public void Read_RejectsDuplicateKnownPropertyAndByteBudgetOverflow()
    {
        string json = Write(new V3DeletionDescriptor(
            "manual-1", OperationToken, 7, SaveSlotOccupantKind.CompleteV3,
            Digest, "tombstone", "Manual"));
        string duplicate = json.Replace(
            "{\"formatFamily\"",
            "{\"formatFamily\":\"simple-cities-v3\",\"formatFamily\"",
            StringComparison.Ordinal);

        Assert.Throws<JsonException>(() => Read(duplicate));
        Assert.Throws<InvalidDataException>(() => Read(
            "{" + new string(' ', V3DeletionDescriptorCodec.MaximumEncodedBytes) + "}"));
    }

    private static string Write(V3DeletionDescriptor descriptor)
    {
        using var stream = new MemoryStream();
        V3DeletionDescriptorCodec.Write(stream, descriptor);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static V3DeletionDescriptor Read(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json), writable: false);
        return V3DeletionDescriptorCodec.Read(stream);
    }
}
