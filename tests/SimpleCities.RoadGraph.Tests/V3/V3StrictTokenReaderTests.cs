using SimpleCities.Core.V3;
using System.IO;
using System.Text;

namespace SimpleCities.Tests.V3;

public sealed class V3StrictTokenReaderTests
{
    [Fact]
    public void ReadFile_ValidJson_ReturnsLengthHashAndEof()
    {
        string path = GetTempFile();
        try
        {
            const string json = """{"value":1.0,"name":"city"}""";
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            File.WriteAllBytes(path, bytes);

            V3StrictTokenResult result = V3StrictTokenReader.ReadFile(path);

            Assert.True(result.Success, result.Error);
            Assert.Equal(bytes.LongLength, result.InitialLength);
            Assert.Equal(bytes.LongLength, result.ConsumedBytes);
            Assert.True(result.EndOfFile);
            Assert.Equal(V3PayloadDigest.ComputeSha256(bytes), result.Sha256);
            Assert.Equal(json, result.Json);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void ReadFile_MissingFile_Fails()
    {
        V3StrictTokenResult result = V3StrictTokenReader.ReadFile(GetTempFile());

        Assert.False(result.Success);
        Assert.Equal("FileMissing", result.Error);
    }

    [Fact]
    public void ReadFile_Utf8Bom_Fails()
    {
        string path = GetTempFile();
        try
        {
            byte[] bytes = [0xEF, 0xBB, 0xBF, (byte)'{', (byte)'}'];
            File.WriteAllBytes(path, bytes);

            V3StrictTokenResult result = V3StrictTokenReader.ReadFile(path);

            Assert.False(result.Success);
            Assert.Equal("BomNotAllowed", result.Error);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void ReadFile_InvalidUtf8_Fails()
    {
        string path = GetTempFile();
        try
        {
            byte[] bytes = [0x7B, 0xFF, 0x7D];
            File.WriteAllBytes(path, bytes);

            V3StrictTokenResult result = V3StrictTokenReader.ReadFile(path);

            Assert.False(result.Success);
            Assert.Equal("InvalidUtf8", result.Error);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void ReadFile_DuplicateKey_Fails()
    {
        string path = GetTempFile();
        try
        {
            const string json = """{"value":1,"value":2}""";
            File.WriteAllText(path, json);

            V3StrictTokenResult result = V3StrictTokenReader.ReadFile(path);

            Assert.False(result.Success);
            Assert.StartsWith("DuplicateKey:", result.Error);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void ReadFile_InvalidNumberLexeme_Fails()
    {
        string path = GetTempFile();
        try
        {
            const string json = """{"value":-0}""";
            File.WriteAllText(path, json);

            V3StrictTokenResult result = V3StrictTokenReader.ReadFile(path);

            Assert.False(result.Success);
            Assert.Equal("InvalidNumberToken", result.Error);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void ReadFile_MalformedJson_Fails()
    {
        string path = GetTempFile();
        try
        {
            const string json = """{"value":}""";
            File.WriteAllText(path, json);

            V3StrictTokenResult result = V3StrictTokenReader.ReadFile(path);

            Assert.False(result.Success);
            Assert.Equal("MalformedJson", result.Error);
        }
        finally
        {
            Cleanup(path);
        }
    }

    private static string GetTempFile() =>
        Path.Combine(Path.GetTempPath(), $"v3-token-{Guid.NewGuid():N}.json");

    private static void Cleanup(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // best-effort cleanup
        }
    }
}
