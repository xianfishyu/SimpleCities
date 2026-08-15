using Godot;
using SimpleCities.Core.V3;
using SimpleCities.Road.V3;
using System.IO;

namespace SimpleCities.Tests.V3;

public sealed class V3RoadPayloadStrictFileReaderTests
{
    [Fact]
    public void Read_ValidFile_Succeeds()
    {
        string path = GetTempFile();
        try
        {
            File.WriteAllText(path, CreateJson());

            V3StrictRoadPayloadResult result = V3RoadPayloadStrictFileReader.Read(path, V3PayloadBudget.Default);

            Assert.True(result.Success, result.Error);
            Assert.NotNull(result.Graph);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void Read_MissingFile_Fails()
    {
        V3StrictRoadPayloadResult result = V3RoadPayloadStrictFileReader.Read(GetTempFile(), V3PayloadBudget.Default);

        Assert.False(result.Success);
        Assert.Equal("FileMissing", result.Error);
    }

    [Fact]
    public void Read_DuplicateKeyFile_Fails()
    {
        string path = GetTempFile();
        try
        {
            const string json = """{"formatFamily":"simple-cities-v3","payloadType":"road-network","schemaVersion":1,"nextID":1,"nextID":2,"nodes":[],"edges":[]}""";
            File.WriteAllText(path, json);

            V3StrictRoadPayloadResult result = V3RoadPayloadStrictFileReader.Read(path, V3PayloadBudget.Default);

            Assert.False(result.Success);
            Assert.StartsWith("DuplicateKey:", result.Error);
        }
        finally
        {
            Cleanup(path);
        }
    }

    private static string GetTempFile() =>
        Path.Combine(Path.GetTempPath(), $"v3-road-{Guid.NewGuid():N}.json");

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

    private static string CreateJson()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out int a);
        revision.TryAddNode(new Vector2(1f, 0f), out revision, out int b);
        revision.TryAddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out revision, out _);
        return RoadGraphV3Persistence.Serialize(revision);
    }
}
