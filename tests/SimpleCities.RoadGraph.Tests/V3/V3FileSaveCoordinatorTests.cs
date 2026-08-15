using System.IO;
using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3FileSaveCoordinatorTests
{
    [Fact]
    public void TrySave_ThenTryLoad_Works()
    {
        string root = GetTempRoot();
        try
        {
            var coordinator = new V3FileSaveCoordinator(new V3FileSlotStore(root), new V3CoordinatorGate());
            byte[] data = "road-network"u8.ToArray();
            V3Manifest manifest = CreateManifest("city-001", data);
            var payloads = new Dictionary<string, byte[]> { ["road_network.json"] = data };

            V3SaveOperationResult save = coordinator.TrySave("city-001", manifest, payloads);
            V3SaveOperationResult load = coordinator.TryLoad("city-001", out V3SlotReadResult readResult);

            Assert.True(save.Success);
            Assert.True(load.Success);
            Assert.True(readResult.Success);
            Assert.Equal("city-001", readResult.Manifest!.SlotId);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void TryDelete_RemovesSlot()
    {
        string root = GetTempRoot();
        try
        {
            var coordinator = new V3FileSaveCoordinator(new V3FileSlotStore(root), new V3CoordinatorGate());
            byte[] data = "road-network"u8.ToArray();
            V3Manifest manifest = CreateManifest("city-001", data);
            coordinator.TrySave("city-001", manifest, new Dictionary<string, byte[]> { ["road_network.json"] = data });

            V3SaveOperationResult delete = coordinator.TryDelete("city-001");

            Assert.True(delete.Success);
            Assert.False(coordinator.TryLoad("city-001", out _).Success);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void TrySave_WhenBusy_ReturnsFailure()
    {
        string root = GetTempRoot();
        try
        {
            var gate = new V3CoordinatorGate();
            var coordinator = new V3FileSaveCoordinator(new V3FileSlotStore(root), gate);
            gate.TryAcquire(out _);
            byte[] data = "road-network"u8.ToArray();
            V3Manifest manifest = CreateManifest("city-001", data);

            V3SaveOperationResult result = coordinator.TrySave("city-001", manifest, new Dictionary<string, byte[]> { ["road_network.json"] = data });

            Assert.False(result.Success);
            Assert.Equal("Busy", result.Error);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string GetTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"v3-fcoord-{Guid.NewGuid():N}");

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
