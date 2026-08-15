using Godot;
using SimpleCities.Core.V3;
using SimpleCities.Road.V3;
using System.IO;

namespace SimpleCities.Tests.V3;

public sealed class V3RoadLoadPipelineTests
{
    [Fact]
    public void Load_RoundTripsController()
    {
        string root = GetTempRoot();
        try
        {
            RoadGraphV3Revision revision = CreateRevision();
            Assert.True(V3RoadSavePipeline.Save("city-001", root, revision, "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            V3RoadLoadPipelineResult result = V3RoadLoadPipeline.Load(
                "city-001",
                root,
                RoadGraphCapacity.Default,
                V3PayloadBudget.Default);

            Assert.True(result.Success, result.Error);
            Assert.Equal(V3LoadPhase.Completed, result.Phase);
            Assert.NotNull(result.Controller);
            Assert.Equal(revision.Nodes.Count, result.Controller!.Facade.Revision.Nodes.Count);
            Assert.Equal(revision.Edges.Count, result.Controller.Facade.Revision.Edges.Count);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Load_MissingSlot_Fails()
    {
        string root = GetTempRoot();
        try
        {
            V3RoadLoadPipelineResult result = V3RoadLoadPipeline.Load(
                "missing",
                root,
                RoadGraphCapacity.Default,
                V3PayloadBudget.Default);

            Assert.False(result.Success);
            Assert.Equal(V3LoadPhase.Failed, result.Phase);
            Assert.Null(result.Controller);
            Assert.NotNull(result.Error);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string GetTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"v3-loadpipe-{Guid.NewGuid():N}");

    private static void Cleanup(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch (IOException)
        {
            // best-effort cleanup
        }
    }

    private static RoadGraphV3Revision CreateRevision()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out int a);
        revision.TryAddNode(new Vector2(1f, 0f), out revision, out int b);
        revision.TryAddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out revision, out _);
        return revision;
    }
}
