using System;

namespace SimpleCities.Core.V3;

/// <summary>
/// V3 缩略图校验：PNG signature 与像素预算。
/// </summary>
public static class V3ThumbnailValidator
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static bool HasPngSignature(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < PngSignature.Length)
            return false;

        for (int i = 0; i < PngSignature.Length; i++)
        {
            if (data[i] != PngSignature[i])
                return false;
        }

        return true;
    }

    public static bool IsWithinPixelBudget(int width, int height, long maxPixels) =>
        width > 0 && height > 0 && (long)width * height <= maxPixels;
}
