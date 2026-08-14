using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace SimpleCities.Tests;

public sealed class V3PngValidatorTests
{
    [Fact]
    public void ValidateEncodedPng_AcceptsDecodedRgbaScanlinesWithinBudgets()
    {
        byte[] png = PngTestData.CreateRgba(2, 2, 0x33);

        V3PngValidator.ValidateEncodedPng(png);
    }

    [Fact]
    public void ValidateEncodedPng_RejectsBadCrcAndTruncatedDecodedScanlines()
    {
        byte[] badCrc = PngTestData.CreateRgba(1, 1, 0x44);
        badCrc[^5] ^= 0x01;
        byte[] truncated = PngTestData.Create(1, 1, [0, 0, 0]);

        Assert.Throws<InvalidDataException>(() => V3PngValidator.ValidateEncodedPng(badCrc));
        Assert.Throws<InvalidDataException>(() => V3PngValidator.ValidateEncodedPng(truncated));
    }

    [Fact]
    public void ValidateEncodedPng_RejectsDimensionsAbovePixelBudgetBeforeDecode()
    {
        byte[] png = PngTestData.Create(
            V3StorageBudget.MaximumThumbnailDimension + 1,
            1,
            [0]);

        Assert.Throws<InvalidDataException>(() => V3PngValidator.ValidateEncodedPng(png));
    }

    [Theory]
    [InlineData("a0Bc")]
    [InlineData("abcD")]
    public void ValidateEncodedPng_RejectsInvalidChunkTypeOrReservedBit(string chunkType)
    {
        byte[] png = PngTestData.CreateRgbaWithAncillaryChunk(chunkType);

        Assert.Throws<InvalidDataException>(() => V3PngValidator.ValidateEncodedPng(png));
    }
}

internal static class PngTestData
{
    internal static byte[] CreateRgba(int width, int height, byte value)
    {
        byte[] scanlines = new byte[checked(height * (1 + width * 4))];
        int offset = 0;
        for (int y = 0; y < height; y++)
        {
            scanlines[offset++] = 0;
            for (int x = 0; x < width; x++)
            {
                scanlines[offset++] = value;
                scanlines[offset++] = value;
                scanlines[offset++] = value;
                scanlines[offset++] = byte.MaxValue;
            }
        }
        return Create(width, height, scanlines);
    }

    internal static byte[] Create(int width, int height, byte[] decodedScanlines)
    {
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(decodedScanlines);

        using var png = new MemoryStream();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header[..4], checked((uint)width));
        BinaryPrimitives.WriteUInt32BigEndian(header.Slice(4, 4), checked((uint)height));
        header[8] = 8;
        header[9] = 6;
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    internal static byte[] CreateRgbaWithAncillaryChunk(string chunkType)
    {
        byte[] valid = CreateRgba(1, 1, 0x22);
        using var output = new MemoryStream();
        output.Write(valid.AsSpan(0, 33));
        WriteChunk(output, chunkType, []);
        output.Write(valid.AsSpan(33));
        return output.ToArray();
    }

    private static void WriteChunk(Stream destination, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)data.Length));
        destination.Write(length);
        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        destination.Write(typeBytes);
        destination.Write(data);
        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, ComputeCrc32(typeBytes, data));
        destination.Write(crc);
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in type)
            crc = Update(crc, value);
        foreach (byte value in data)
            crc = Update(crc, value);
        return ~crc;
    }

    private static uint Update(uint crc, byte value)
    {
        crc ^= value;
        for (int bit = 0; bit < 8; bit++)
            crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320u : crc >> 1;
        return crc;
    }
}
