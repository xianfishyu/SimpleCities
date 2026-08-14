using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

internal static class V3PngValidator
{
    private static ReadOnlySpan<byte> Signature => [137, 80, 78, 71, 13, 10, 26, 10];

    internal static string? Validate(string path)
    {
        try
        {
            ValidateCore(path);
            return null;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return exception.Message;
        }
    }

    private static void ValidateCore(string path)
    {
        SaveSlotStore.EnsureOrdinaryFile(path, "thumbnail");
        using var source = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 16 * 1024,
            FileOptions.SequentialScan);
        long initialLength = source.Length;
        if (initialLength is <= 0 or > V3StorageBudget.MaximumThumbnailEncodedBytes)
            throw new InvalidDataException("Thumbnail exceeds its encoded byte budget.");

        byte[] encoded = new byte[checked((int)initialLength)];
        source.ReadExactly(encoded);
        if (source.Length != initialLength || source.ReadByte() != -1)
            throw new InvalidDataException("Thumbnail changed while it was being read.");
        ValidateEncodedPng(encoded);
    }

    internal static void ValidateEncodedPng(ReadOnlySpan<byte> encoded)
    {
        if (encoded.Length > V3StorageBudget.MaximumThumbnailEncodedBytes)
            throw new InvalidDataException("Thumbnail exceeds its encoded byte budget.");
        if (encoded.Length < Signature.Length || !encoded[..Signature.Length].SequenceEqual(Signature))
            throw new InvalidDataException("Thumbnail does not have a PNG signature.");

        int offset = Signature.Length;
        bool sawHeader = false;
        bool sawPalette = false;
        bool sawImageData = false;
        bool imageDataEnded = false;
        bool sawEnd = false;
        int width = 0;
        int height = 0;
        int bitDepth = 0;
        int colorType = 0;
        int interlace = 0;
        using var imageData = new MemoryStream();
        while (offset < encoded.Length)
        {
            if (encoded.Length - offset < 12)
                throw new InvalidDataException("Thumbnail contains a truncated PNG chunk.");
            uint lengthValue = BinaryPrimitives.ReadUInt32BigEndian(encoded.Slice(offset, 4));
            if (lengthValue > int.MaxValue)
                throw new InvalidDataException("Thumbnail PNG chunk length is invalid.");
            int length = (int)lengthValue;
            offset += 4;
            ReadOnlySpan<byte> type = encoded.Slice(offset, 4);
            offset += 4;
            ValidateChunkType(type);
            if (length > encoded.Length - offset - 4)
                throw new InvalidDataException("Thumbnail PNG chunk length is invalid.");
            ReadOnlySpan<byte> data = encoded.Slice(offset, length);
            uint expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(encoded.Slice(offset + length, 4));
            if (ComputeCrc32(type, data) != expectedCrc)
                throw new InvalidDataException("Thumbnail PNG chunk CRC is invalid.");
            offset += length + 4;

            string chunkType = Encoding.ASCII.GetString(type);
            switch (chunkType)
            {
                case "IHDR":
                    if (sawHeader || offset - length - 12 != Signature.Length || length != 13)
                        throw new InvalidDataException("Thumbnail PNG IHDR is invalid.");
                    width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data[..4]));
                    height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4, 4)));
                    bitDepth = data[8];
                    colorType = data[9];
                    if (data[10] != 0 || data[11] != 0 || data[12] > 1)
                        throw new InvalidDataException("Thumbnail PNG uses an unsupported compression, filter, or interlace method.");
                    interlace = data[12];
                    ValidateDimensionsAndFormat(width, height, bitDepth, colorType);
                    sawHeader = true;
                    break;
                case "PLTE":
                    if (!sawHeader || sawImageData || sawPalette || length is 0 or > 768 || length % 3 != 0)
                        throw new InvalidDataException("Thumbnail PNG palette is invalid.");
                    sawPalette = true;
                    break;
                case "IDAT":
                    if (!sawHeader || imageDataEnded || length == 0)
                        throw new InvalidDataException("Thumbnail PNG image data ordering is invalid.");
                    if (colorType == 3 && !sawPalette)
                        throw new InvalidDataException("Indexed thumbnail PNG is missing its palette.");
                    if (imageData.Length > V3StorageBudget.MaximumThumbnailEncodedBytes - length)
                        throw new InvalidDataException("Thumbnail PNG image data exceeds its byte budget.");
                    imageData.Write(data);
                    sawImageData = true;
                    break;
                case "IEND":
                    if (!sawHeader || !sawImageData || sawEnd || length != 0)
                        throw new InvalidDataException("Thumbnail PNG IEND is invalid.");
                    sawEnd = true;
                    imageDataEnded = true;
                    if (offset != encoded.Length)
                        throw new InvalidDataException("Thumbnail PNG contains bytes after IEND.");
                    break;
                default:
                    if (!sawHeader || sawEnd)
                        throw new InvalidDataException("Thumbnail PNG chunk ordering is invalid.");
                    if (sawImageData)
                        imageDataEnded = true;
                    if (char.IsAsciiLetterUpper(chunkType[0]))
                        throw new InvalidDataException($"Thumbnail PNG contains unsupported critical chunk '{chunkType}'.");
                    break;
            }
        }

        if (!sawEnd)
            throw new InvalidDataException("Thumbnail PNG is missing IEND.");
        ValidateDecodedScanlines(imageData.ToArray(), width, height, bitDepth, colorType, interlace);
    }

    private static void ValidateChunkType(ReadOnlySpan<byte> type)
    {
        foreach (byte value in type)
        {
            if (value is not (>= (byte)'A' and <= (byte)'Z') and
                not (>= (byte)'a' and <= (byte)'z'))
            {
                throw new InvalidDataException("Thumbnail PNG chunk type is invalid.");
            }
        }
        if (type[2] is >= (byte)'a' and <= (byte)'z')
            throw new InvalidDataException("Thumbnail PNG chunk type uses an invalid reserved bit.");
    }

    private static void ValidateDimensionsAndFormat(int width, int height, int bitDepth, int colorType)
    {
        if (width <= 0 || height <= 0 ||
            width > V3StorageBudget.MaximumThumbnailDimension ||
            height > V3StorageBudget.MaximumThumbnailDimension ||
            (long)width * height > V3StorageBudget.MaximumThumbnailPixels)
        {
            throw new InvalidDataException("Thumbnail PNG exceeds its dimension or pixel budget.");
        }

        bool valid = colorType switch
        {
            0 => bitDepth is 1 or 2 or 4 or 8 or 16,
            2 => bitDepth is 8 or 16,
            3 => bitDepth is 1 or 2 or 4 or 8,
            4 => bitDepth is 8 or 16,
            6 => bitDepth is 8 or 16,
            _ => false,
        };
        if (!valid)
            throw new InvalidDataException("Thumbnail PNG color type and bit depth are invalid.");
    }

    private static void ValidateDecodedScanlines(
        byte[] compressed,
        int width,
        int height,
        int bitDepth,
        int colorType,
        int interlace)
    {
        long[] rowLengths = interlace == 0
            ? EnumerableRows(height, RowBytes(width, bitDepth, colorType))
            : Adam7RowLengths(width, height, bitDepth, colorType);
        long expected = 0;
        foreach (long rowLength in rowLengths)
            expected = checked(expected + rowLength + 1);
        if (expected > V3StorageBudget.MaximumThumbnailDecodedBytes)
            throw new InvalidDataException("Thumbnail PNG decoded data exceeds its byte budget.");

        using var compressedStream = new MemoryStream(compressed, writable: false);
        using var inflater = new ZLibStream(compressedStream, CompressionMode.Decompress, leaveOpen: false);
        byte[] chunk = new byte[64 * 1024];
        long decoded = 0;
        int rowIndex = 0;
        long rowOffset = 0;
        int read;
        while ((read = inflater.Read(chunk, 0, chunk.Length)) > 0)
        {
            decoded += read;
            if (decoded > expected)
                throw new InvalidDataException("Thumbnail PNG decodes beyond its expected scanline budget.");
            for (int index = 0; index < read; index++)
            {
                if (rowIndex >= rowLengths.Length)
                    throw new InvalidDataException("Thumbnail PNG contains excess decoded scanlines.");
                if (rowOffset == 0 && chunk[index] > 4)
                    throw new InvalidDataException("Thumbnail PNG uses an invalid scanline filter.");
                rowOffset++;
                if (rowOffset == rowLengths[rowIndex] + 1)
                {
                    rowOffset = 0;
                    rowIndex++;
                }
            }
        }
        if (decoded != expected || rowOffset != 0 || rowIndex != rowLengths.Length)
            throw new InvalidDataException("Thumbnail PNG decoded scanline length is invalid.");
    }

    private static long[] EnumerableRows(int height, long rowBytes)
    {
        var rows = new long[height];
        Array.Fill(rows, rowBytes);
        return rows;
    }

    private static long[] Adam7RowLengths(int width, int height, int bitDepth, int colorType)
    {
        int[] xStart = [0, 4, 0, 2, 0, 1, 0];
        int[] yStart = [0, 0, 4, 0, 2, 0, 1];
        int[] xStep = [8, 8, 4, 4, 2, 2, 1];
        int[] yStep = [8, 8, 8, 4, 4, 2, 2];
        var rows = new List<long>();
        for (int pass = 0; pass < 7; pass++)
        {
            int passWidth = PassSize(width, xStart[pass], xStep[pass]);
            int passHeight = PassSize(height, yStart[pass], yStep[pass]);
            if (passWidth == 0 || passHeight == 0)
                continue;
            long rowBytes = RowBytes(passWidth, bitDepth, colorType);
            for (int row = 0; row < passHeight; row++)
                rows.Add(rowBytes);
        }
        return rows.ToArray();
    }

    private static int PassSize(int size, int start, int step) =>
        size <= start ? 0 : (size - start + step - 1) / step;

    private static long RowBytes(int width, int bitDepth, int colorType)
    {
        int channels = colorType switch
        {
            0 or 3 => 1,
            2 => 3,
            4 => 2,
            6 => 4,
            _ => throw new InvalidDataException("Thumbnail PNG color type is invalid."),
        };
        return checked(((long)width * channels * bitDepth + 7) / 8);
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in type)
            crc = UpdateCrc(crc, value);
        foreach (byte value in data)
            crc = UpdateCrc(crc, value);
        return ~crc;
    }

    private static uint UpdateCrc(uint crc, byte value)
    {
        crc ^= value;
        for (int bit = 0; bit < 8; bit++)
            crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320u : crc >> 1;
        return crc;
    }
}
