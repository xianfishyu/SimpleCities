using Godot;
using SimpleCities.Core.V3;
using SimpleCities.Road.V3;
using System.IO;

namespace SimpleCities.Tests.V3;

public sealed class V3SlotAutosaveCoordinatorTests
{
    [Fact]
    public void TryAutosave_WhenGateFree_Saves()
    {
        string root = GetTempRoot();
        try
        {
            var coordinator = new V3SlotAutosaveCoordinator();
            RoadGraphV3Revision revision = CreateRevision();

            V3AutosaveDecision decision = coordinator.TryAutosave(
                "city-001",
                root,
                revision,
                hasNewerSuccess: false,
                out bool saved);

            Assert.Equal(V3AutosaveDecision.RunNow, decision);
            Assert.True(saved);
            Assert.True(new V3FileSlotStore(root).Load("city-001").Success);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void TryAutosave_WhenGateHeld_QueuesPending()
    {
        string root = GetTempRoot();
        try
        {
            var gate = new V3CoordinatorGate();
            Assert.True(gate.TryAcquire(out _));
            var coordinator = new V3SlotAutosaveCoordinator(gate, new V3SlotAutosaveService());
            RoadGraphV3Revision revision = CreateRevision();

            V3AutosaveDecision decision = coordinator.TryAutosave(
                "city-001",
                root,
                revision,
                hasNewerSuccess: false,
                out bool saved);

            Assert.Equal(V3AutosaveDecision.QueuePending, decision);
            Assert.False(saved);
            Assert.True(coordinator.HasPendingAutosave);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void TryAutosave_WhenGateHeldAndPending_Skips()
    {
        string root = GetTempRoot();
        try
        {
            var gate = new V3CoordinatorGate();
            Assert.True(gate.TryAcquire(out _));
            var coordinator = new V3SlotAutosaveCoordinator(gate, new V3SlotAutosaveService());
            RoadGraphV3Revision revision = CreateRevision();
            coordinator.TryAutosave("city-001", root, revision, hasNewerSuccess: false, out _);

            V3AutosaveDecision decision = coordinator.TryAutosave(
                "city-001",
                root,
                revision,
                hasNewerSuccess: false,
                out bool saved);

            Assert.Equal(V3AutosaveDecision.SkipBusy, decision);
            Assert.False(saved);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void TryAutosave_AfterGateReleased_RunsPending()
    {
        string root = GetTempRoot();
        try
        {
            var gate = new V3CoordinatorGate();
            var coordinator = new V3SlotAutosaveCoordinator(gate, new V3SlotAutosaveService());
            RoadGraphV3Revision revision = CreateRevision();

            Assert.True(gate.TryAcquire(out Guid heldOperationId));
            coordinator.TryAutosave("city-001", root, revision, hasNewerSuccess: false, out _);
            gate.Release(heldOperationId);

            V3AutosaveDecision decision = coordinator.TryAutosave(
                "city-001",
                root,
                revision,
                hasNewerSuccess: false,
                out bool saved);

            Assert.Equal(V3AutosaveDecision.RunNow, decision);
            Assert.True(saved);
            Assert.False(coordinator.HasPendingAutosave);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string GetTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"v3-autocoord-{Guid.NewGuid():N}");

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
