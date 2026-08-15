using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3SlotDigestTests
{
    [Fact]
    public void Compute_StableForSameFiles()
    {
        var files = new Dictionary<string, byte[]>
        {
            ["a.json"] = "a"u8.ToArray(),
            ["b.json"] = "b"u8.ToArray(),
        };

        string first = V3SlotDigest.Compute(files);
        string second = V3SlotDigest.Compute(files);

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void Compute_ChangesWhenFileChanges()
    {
        var files = new Dictionary<string, byte[]> { ["a.json"] = "a"u8.ToArray() };
        var changed = new Dictionary<string, byte[]> { ["a.json"] = "b"u8.ToArray() };

        Assert.NotEqual(V3SlotDigest.Compute(files), V3SlotDigest.Compute(changed));
    }

    [Fact]
    public void Compute_SortsByName()
    {
        var unordered = new Dictionary<string, byte[]>
        {
            ["b.json"] = "b"u8.ToArray(),
            ["a.json"] = "a"u8.ToArray(),
        };
        var ordered = new Dictionary<string, byte[]>
        {
            ["a.json"] = "a"u8.ToArray(),
            ["b.json"] = "b"u8.ToArray(),
        };

        Assert.Equal(V3SlotDigest.Compute(unordered), V3SlotDigest.Compute(ordered));
    }
}
