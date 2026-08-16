using Godot;
using SimpleCities.Core.V3;
using SimpleCities.Road.V3;
using System.IO;
using System.Linq;

namespace SimpleCities.Tests;

public sealed class V3SaveOperationBackendTests
{
    [Fact]
    public void SaveAs_ReturnsCompletedPublishResultAndSetsCurrentSlot()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            var backend = new V3ApplicationSaveOperationBackend(app, sceneGeneration: 5);

            V3SaveOperationResult result = backend.SaveAs(
                "My City",
                "My City",
                "2026-08-16T00:00:00.0000000Z",
                null,
                null,
                null);

            Assert.True(result.Success);
            Assert.True(result.CommitCompleted);
            Assert.Equal(V3SaveOperationKind.Publish, result.Token.Kind);
            Assert.Equal(5, result.Token.SceneGeneration);
            Assert.StartsWith("manual-", app.CurrentSlotID);
            Assert.Equal(app.CurrentSlotID, backend.CurrentSlotID);
            Assert.Contains(app.CurrentSlotID, backend.ListSlots().Select(summary => summary.SlotId));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void SaveAs_GeneratesDistinctSlotIds()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            var backend = new V3ApplicationSaveOperationBackend(app);

            backend.SaveAs("A", "A", "2026-08-16T00:00:00.0000000Z", null, null, null);
            string first = app.CurrentSlotID;
            backend.SaveAs("B", "B", "2026-08-16T00:00:00.0000000Z", null, null, null);
            string second = app.CurrentSlotID;

            Assert.NotEqual(first, second);
            Assert.Equal(2, backend.ListSlots().Count);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Save_OverwriteExistingSlot_KeepsSingleSlot()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            var backend = new V3ApplicationSaveOperationBackend(app);
            backend.SaveAs("First", "First", "2026-08-16T00:00:00.0000000Z", null, null, null);
            string slotId = app.CurrentSlotID;

            V3SaveOperationResult result = backend.Save(
                slotId,
                "Second",
                "Second",
                "2026-08-16T00:00:00.0000000Z",
                null,
                null,
                null);

            Assert.True(result.Success);
            Assert.Equal(1, backend.ListSlots().Count);
            Assert.Equal("Second", backend.ListSlots().First(s => s.SlotId == slotId).DisplayName);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Save_ReturnsCompletedPublishResult()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            var backend = new V3ApplicationSaveOperationBackend(app);

            V3SaveOperationResult result = backend.Save(
                "city-001",
                "City",
                "City",
                "2026-08-16T00:00:00.0000000Z",
                null,
                null,
                null);

            Assert.True(result.Success);
            Assert.True(result.CommitCompleted);
            Assert.Equal("city-001", app.CurrentSlotID);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Load_ReturnsCompletedLoadResult()
    {
        string root = GetTempRoot();
        try
        {
            var source = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            var sourceBackend = new V3ApplicationSaveOperationBackend(source);
            sourceBackend.SaveAs("City", "City", "2026-08-16T00:00:00.0000000Z", null, null, null);
            string slotId = source.CurrentSlotID;

            var target = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            var targetBackend = new V3ApplicationSaveOperationBackend(target, sceneGeneration: 9);

            V3SaveOperationResult result = targetBackend.Load(slotId, lineageID: 3);

            Assert.True(result.Success);
            Assert.True(result.CommitCompleted);
            Assert.Equal(V3SaveOperationKind.Load, result.Token.Kind);
            Assert.Equal(9, result.Token.SceneGeneration);
            Assert.Equal(slotId, target.CurrentSlotID);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Delete_ReturnsCompletedDeleteResultAndRemovesSlot()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            var backend = new V3ApplicationSaveOperationBackend(app);
            backend.SaveAs("City", "City", "2026-08-16T00:00:00.0000000Z", null, null, null);
            string slotId = app.CurrentSlotID;

            V3SaveOperationResult result = backend.Delete(slotId);

            Assert.True(result.Success);
            Assert.True(result.CommitCompleted);
            Assert.Equal(V3SaveOperationKind.Delete, result.Token.Kind);
            Assert.DoesNotContain(slotId, backend.ListSlots().Select(summary => summary.SlotId));
            Assert.Empty(backend.ListSlots());
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Load_MissingSlot_ReturnsFailedBeforeCommit()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            var backend = new V3ApplicationSaveOperationBackend(app);

            V3SaveOperationResult result = backend.Load("missing-001", lineageID: 1);

            Assert.False(result.Success);
            Assert.False(result.CommitCompleted);
            Assert.Equal(V3SaveOperationPhase.Prepare, result.Phase);
            Assert.Equal(V3SaveOperationKind.Load, result.Token.Kind);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Load_AfterDelete_ReturnsFailedBeforeCommit()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            var backend = new V3ApplicationSaveOperationBackend(app);
            backend.SaveAs("City", "City", "2026-08-16T00:00:00.0000000Z", null, null, null);
            string slotId = app.CurrentSlotID;
            backend.Delete(slotId);

            V3SaveOperationResult result = backend.Load(slotId, lineageID: 1);

            Assert.False(result.Success);
            Assert.False(result.CommitCompleted);
            Assert.Equal(V3SaveOperationPhase.Prepare, result.Phase);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Delete_MissingSlot_ReturnsFailedBeforeCommit()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            var backend = new V3ApplicationSaveOperationBackend(app);

            V3SaveOperationResult result = backend.Delete("missing-001");

            Assert.False(result.Success);
            Assert.False(result.CommitCompleted);
            Assert.Equal(V3SaveOperationPhase.Prepare, result.Phase);
            Assert.Equal(V3SaveOperationKind.Delete, result.Token.Kind);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string GetTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"v3-backend-{Guid.NewGuid():N}");

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
}
