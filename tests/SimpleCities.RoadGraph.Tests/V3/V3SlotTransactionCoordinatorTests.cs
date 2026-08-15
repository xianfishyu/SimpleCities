using SimpleCities.Core.V3;
using System.IO;

namespace SimpleCities.Tests.V3;

public sealed class V3SlotTransactionCoordinatorTests
{
    [Fact]
    public void Publish_Delete_Recover_RoundTrips_UnderCoordinator()
    {
        string root = GetTempRoot();
        string backupRoot = GetTempRoot();
        try
        {
            Directory.CreateDirectory(root);
            var coordinator = new V3SlotTransactionCoordinator();

            byte[] data = "road-network"u8.ToArray();
            V3Manifest manifest = CreateManifest("city-001", data);
            var payloads = new Dictionary<string, byte[]> { ["road_network.json"] = data };

            V3PublishResult publish = coordinator.Publish("city-001", root, manifest, payloads);
            Assert.True(publish.Success, publish.Error ?? "null");
            Assert.NotNull(publish.Descriptor);

            V3SlotBackupService.Backup("city-001", root, backupRoot);
            V3DeleteResult delete = coordinator.Delete("city-001", root);
            Assert.True(delete.Success);
            Assert.False(new V3FileSlotStore(root).Load("city-001").Success);

            Assert.True(coordinator.Recover("city-001", root, backupRoot));
            Assert.True(new V3FileSlotStore(root).Load("city-001").Success);
        }
        finally
        {
            Cleanup(root);
            Cleanup(backupRoot);
        }
    }

    [Fact]
    public void Publish_WhenGateIsHeld_ReturnsBusy()
    {
        string root = GetTempRoot();
        try
        {
            var gate = new V3CoordinatorGate();
            Assert.True(gate.TryAcquire(out _));

            var coordinator = new V3SlotTransactionCoordinator(gate);
            byte[] data = "road-network"u8.ToArray();
            V3Manifest manifest = CreateManifest("city-001", data);
            var payloads = new Dictionary<string, byte[]> { ["road_network.json"] = data };

            V3PublishResult result = coordinator.Publish("city-001", root, manifest, payloads);

            Assert.False(result.Success);
            Assert.Equal("Busy", result.Error);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void List_ReturnsPublishedSlots()
    {
        string root = GetTempRoot();
        try
        {
            Directory.CreateDirectory(root);
            var coordinator = new V3SlotTransactionCoordinator();
            byte[] data = "road-network"u8.ToArray();
            V3Manifest manifest = CreateManifest("city-001", data);
            var payloads = new Dictionary<string, byte[]> { ["road_network.json"] = data };
            Assert.True(coordinator.Publish("city-001", root, manifest, payloads).Success);

            IReadOnlyList<V3SlotSummary> list = coordinator.List(root);

            V3SlotSummary summary = Assert.Single(list);
            Assert.Equal("city-001", summary.SlotId);
            Assert.True(summary.IsUsable);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void GetStatus_ReturnsCompleteAfterPublish()
    {
        string root = GetTempRoot();
        try
        {
            Directory.CreateDirectory(root);
            var coordinator = new V3SlotTransactionCoordinator();
            byte[] data = "road-network"u8.ToArray();
            V3Manifest manifest = CreateManifest("city-001", data);
            var payloads = new Dictionary<string, byte[]> { ["road_network.json"] = data };
            Assert.True(coordinator.Publish("city-001", root, manifest, payloads).Success);

            V3SlotSummary summary = coordinator.GetStatus("city-001", root);

            Assert.Equal(V3SlotOccupant.CompleteV3, summary.Occupant);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void GetStatus_WhenGateIsHeld_ReturnsUnsafe()
    {
        string root = GetTempRoot();
        try
        {
            var gate = new V3CoordinatorGate();
            Assert.True(gate.TryAcquire(out _));
            var coordinator = new V3SlotTransactionCoordinator(gate);

            V3SlotSummary summary = coordinator.GetStatus("city-001", root);

            Assert.Equal(V3SlotOccupant.Unsafe, summary.Occupant);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void List_WhenGateIsHeld_ReturnsEmpty()
    {
        string root = GetTempRoot();
        try
        {
            var gate = new V3CoordinatorGate();
            Assert.True(gate.TryAcquire(out _));
            var coordinator = new V3SlotTransactionCoordinator(gate);

            Assert.Empty(coordinator.List(root));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Delete_WhenGateIsHeld_ReturnsBusy()
    {
        string root = GetTempRoot();
        try
        {
            var gate = new V3CoordinatorGate();
            Assert.True(gate.TryAcquire(out _));

            var coordinator = new V3SlotTransactionCoordinator(gate);
            V3DeleteResult result = coordinator.Delete("city-001", root);

            Assert.False(result.Success);
            Assert.Equal("Busy", result.Error);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string GetTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"v3-txcoord-{Guid.NewGuid():N}");

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

    private static V3Manifest CreateManifest(string slotId, byte[] data) =>
        new(
            V3SaveRoot.FormatFamily,
            V3SaveRoot.SchemaVersion,
            slotId,
            slotId,
            "2026-08-12T08:00:00.0000000Z",
            slotId,
            0,
            0m,
            null,
            [new V3ManifestFile("road_network.json", data.LongLength, V3PayloadDigest.ComputeSha256(data))]);
}
