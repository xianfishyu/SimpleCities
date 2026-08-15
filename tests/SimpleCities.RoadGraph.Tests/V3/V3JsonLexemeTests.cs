using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3JsonLexemeTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("123")]
    [InlineData("2147483647")]
    public void IsValidCanonicalInteger_AcceptsValidTokens(string token)
    {
        Assert.True(V3JsonLexeme.IsValidCanonicalInteger(token));
    }

    [Theory]
    [InlineData("01")]
    [InlineData("+1")]
    [InlineData("1.0")]
    [InlineData("1e3")]
    [InlineData("-0")]
    [InlineData("")]
    [InlineData(null)]
    public void IsValidCanonicalInteger_RejectsInvalidTokens(string? token)
    {
        Assert.False(V3JsonLexeme.IsValidCanonicalInteger(token));
    }

    [Theory]
    [InlineData("0.5")]
    [InlineData("-1.25")]
    [InlineData("1e3")]
    public void IsValidFiniteFloatLexeme_AcceptsNormalNumbers(string token)
    {
        Assert.True(V3JsonLexeme.IsValidFiniteFloatLexeme(token));
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    public void IsValidFiniteFloatLexeme_RejectsInvalidTokens(string? token)
    {
        Assert.False(V3JsonLexeme.IsValidFiniteFloatLexeme(token));
    }
}
