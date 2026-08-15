using SimpleCities.Core.V3;
using System.Text.Json;

namespace SimpleCities.Tests.V3;

public sealed class V3JsonDuplicateDetectorTests
{
    [Fact]
    public void DetectDuplicate_ReturnsTrueForRootDuplicate()
    {
        const string json = """{"a":1,"a":2}""";

        Assert.True(V3JsonDuplicateDetector.TryDetectDuplicateKey(json, out string? key));
        Assert.Equal("a", key);
    }

    [Fact]
    public void DetectDuplicate_ReturnsTrueForNestedDuplicate()
    {
        const string json = """{"outer":{"b":1,"b":2}}""";

        Assert.True(V3JsonDuplicateDetector.TryDetectDuplicateKey(json, out string? key));
        Assert.Equal("b", key);
    }

    [Fact]
    public void DetectDuplicate_ReturnsFalseForValidJson()
    {
        const string json = """{"a":1,"b":{"c":2}}""";

        Assert.False(V3JsonDuplicateDetector.TryDetectDuplicateKey(json, out _));
    }

    [Fact]
    public void DetectDuplicate_ThrowsOnMalformedJson()
    {
        Assert.ThrowsAny<JsonException>(() => V3JsonDuplicateDetector.TryDetectDuplicateKey("{", out _));
    }
}
