using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3ThumbnailValidatorTests
{
    [Fact]
    public void HasPngSignature_AcceptsPngHeader()
    {
        byte[] data = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];

        Assert.True(V3ThumbnailValidator.HasPngSignature(data));
    }

    [Fact]
    public void HasPngSignature_RejectsShortOrInvalidData()
    {
        Assert.False(V3ThumbnailValidator.HasPngSignature([]));
        Assert.False(V3ThumbnailValidator.HasPngSignature("not a png"u8.ToArray()));
    }

    [Fact]
    public void IsWithinPixelBudget_AcceptsReasonableDimensions()
    {
        Assert.True(V3ThumbnailValidator.IsWithinPixelBudget(640, 480, 1_000_000));
    }

    [Fact]
    public void IsWithinPixelBudget_RejectsZeroNegativeOrExceeded()
    {
        Assert.False(V3ThumbnailValidator.IsWithinPixelBudget(0, 480, 1_000_000));
        Assert.False(V3ThumbnailValidator.IsWithinPixelBudget(-1, 480, 1_000_000));
        Assert.False(V3ThumbnailValidator.IsWithinPixelBudget(2000, 2000, 1_000_000));
    }
}
