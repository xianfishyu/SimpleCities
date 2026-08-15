using Godot;
using SimpleCities.Core.V3;
using SimpleCities.Road.V3;
using System.IO;

namespace SimpleCities.Tests.V3;

public sealed class V3RoadSavePipelineTests
{
    [Fact]
    public void Save_ThenLoadController_RoundTrips()
    {
        string root = GetTempRoot();
        try
        {
            RoadGraphV3Revision revision = CreateRevision();

            Assert.True(V3RoadSavePipeline.Save("city-001", root, revision, "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            RoadGraphV3Controller? controller = V3RoadSavePipeline.LoadController("city-001", root, RoadGraphCapacity.Default, V3PayloadBudget.Default);

            Assert.NotNull(controller);
            Assert.Equal(revision.Nodes.Count, controller!.Facade.Revision.Nodes.Count);
            Assert.Equal(revision.Edges.Count, controller.Facade.Revision.Edges.Count);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void LoadController_MissingSlot_ReturnsNull()
    {
        string root = GetTempRoot();
        try
        {
            Assert.Null(V3RoadSavePipeline.LoadController("missing", root, RoadGraphCapacity.Default, V3PayloadBudget.Default));
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string GetTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"v3-pipeline-{Guid.NewGuid():N}");

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
