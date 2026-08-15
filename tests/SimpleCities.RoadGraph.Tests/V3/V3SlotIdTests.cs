using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3SlotIdTests
{
    [Theory]
    [InlineData("city-001")]
    [InlineData("a")]
    [InlineData("A_Z-9")]
    public void IsValid_AcceptsTypicalNames(string slotId)
    {
        Assert.True(V3SlotId.IsValid(slotId));
    }

    [Fact]
    public void IsValid_RejectsEmpty()
    {
        Assert.False(V3SlotId.IsValid(""));
        Assert.False(V3SlotId.IsValid(null));
    }

    [Fact]
    public void IsValid_RejectsTooLong()
    {
        Assert.False(V3SlotId.IsValid(new string('a', V3SlotId.MaxLength + 1)));
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a.b")]
    [InlineData("城市")]
    [InlineData("a b")]
    public void IsValid_RejectsInvalidCharacters(string slotId)
    {
        Assert.False(V3SlotId.IsValid(slotId));
    }
}
