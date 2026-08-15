using Godot;
using SimpleCities.Core.V3;
using SimpleCities.Road.V3;
using System.IO;

namespace SimpleCities.Tests.V3;

public sealed class V3SlotAutosaveServiceTests
{
    [Fact]
    public void TryAutosave_WhenBusy_QueuesPending()
    {
        var service = new V3SlotAutosaveService();
        RoadGraphV3Revision revision = CreateRevision();

        V3AutosaveDecision decision = service.TryAutosave("city-001", GetTempRoot(), revision, isBusy: true, hasNewerSuccess: false, out bool saved);

        Assert.Equal(V3AutosaveDecision.QueuePending, decision);
        Assert.False(saved);
        Assert.True(service.HasPendingAutosave);
    }

    [Fact]
    public void TryAutosave_WhenIdleWithoutNewer_RunsAndSaves()
    {
        string root = GetTempRoot();
        try
        {
            var service = new V3SlotAutosaveService();
            RoadGraphV3Revision revision = CreateRevision();

            V3AutosaveDecision decision = service.TryAutosave("city-001", root, revision, isBusy: false, hasNewerSuccess: false, out bool saved);

            Assert.Equal(V3AutosaveDecision.RunNow, decision);
            Assert.True(saved);
            Assert.False(service.HasPendingAutosave);
            Assert.True(new V3FileSlotStore(root).Load("city-001").Success);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void TryAutosave_WhenBusyWithPending_Skips()
    {
        var service = new V3SlotAutosaveService();
        RoadGraphV3Revision revision = CreateRevision();
        service.TryAutosave("city-001", GetTempRoot(), revision, isBusy: true, hasNewerSuccess: false, out _);

        V3AutosaveDecision decision = service.TryAutosave("city-001", GetTempRoot(), revision, isBusy: true, hasNewerSuccess: false, out bool saved);

        Assert.Equal(V3AutosaveDecision.SkipBusy, decision);
        Assert.False(saved);
    }

    private static string GetTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"v3-auto-{Guid.NewGuid():N}");

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
