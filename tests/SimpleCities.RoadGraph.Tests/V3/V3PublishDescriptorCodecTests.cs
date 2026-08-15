using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3PublishDescriptorCodecTests
{
    [Fact]
    public void Serialize_ThenDeserialize_RoundTripsDescriptor()
    {
        V3PublishDescriptor descriptor = ValidDescriptor();

        string json = V3PublishDescriptorCodec.Serialize(descriptor);
        V3PublishDescriptorCodecResult result = V3PublishDescriptorCodec.Deserialize(json);

        Assert.True(result.Success, result.Error);
        Assert.Equal(descriptor.SlotId, result.Descriptor!.SlotId);
        Assert.Equal(descriptor.NewDigest, result.Descriptor.NewDigest);
    }

    [Fact]
    public void Deserialize_RejectsInvalidJson()
    {
        V3PublishDescriptorCodecResult result = V3PublishDescriptorCodec.Deserialize("not json");

        Assert.False(result.Success);
        Assert.Equal("MalformedJson", result.Error);
    }

    [Fact]
    public void Deserialize_RejectsInvalidDescriptor()
    {
        const string json = """
            {
              "operationId": "op-1",
              "slotId": "bad/slot",
              "oldDigest": "old",
              "newDigest": "new",
              "stagingPath": "staging",
              "backupPath": "backup"
            }
            """;

        V3PublishDescriptorCodecResult result = V3PublishDescriptorCodec.Deserialize(json);

        Assert.False(result.Success);
        Assert.Equal("InvalidDescriptor", result.Error);
    }

    [Fact]
    public void Serialize_UsesCamelCaseFieldNames()
    {
        string json = V3PublishDescriptorCodec.Serialize(ValidDescriptor());

        Assert.Contains("\"operationId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"slotId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"newDigest\"", json, StringComparison.Ordinal);
    }

    private static V3PublishDescriptor ValidDescriptor() =>
        new(
            "op-1",
            "city-001",
            "old-digest",
            "new-digest",
            "user://saves-v3/.save-transactions/city-001/op-1/staging",
            "user://saves-v3/.save-transactions/city-001/op-1/backup");
}
