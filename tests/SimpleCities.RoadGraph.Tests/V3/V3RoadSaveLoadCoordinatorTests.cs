using Godot;
using SimpleCities.Core.V3;
using SimpleCities.Road.V3;
using System.IO;

namespace SimpleCities.Tests.V3;

public sealed class V3RoadSaveLoadCoordinatorTests
{
    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        string root = GetTempRoot();
        try
        {
            RoadGraphV3Revision revision = CreateRevision();
            var coordinator = new V3RoadSaveLoadCoordinator();

            Assert.True(coordinator.Save("city-001", root, revision, "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            RoadGraphV3Controller? controller = coordinator.Load("city-001", root, RoadGraphCapacity.Default, V3PayloadBudget.Default);

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
    public void Save_WhenGateHeld_Fails()
    {
        string root = GetTempRoot();
        try
        {
            var gate = new V3CoordinatorGate();
            Assert.True(gate.TryAcquire(out _));
            var coordinator = new V3RoadSaveLoadCoordinator(gate);
            RoadGraphV3Revision revision = CreateRevision();

            bool result = coordinator.Save("city-001", root, revision, "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null);

            Assert.False(result);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Load_WhenGateHeld_ReturnsNull()
    {
        string root = GetTempRoot();
        try
        {
            var gate = new V3CoordinatorGate();
            Assert.True(gate.TryAcquire(out _));
            var coordinator = new V3RoadSaveLoadCoordinator(gate);

            Assert.Null(coordinator.Load("city-001", root, RoadGraphCapacity.Default, V3PayloadBudget.Default));
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string GetTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"v3-saveload-{Guid.NewGuid():N}");

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
