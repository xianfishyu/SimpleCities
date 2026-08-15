using Godot;
using SimpleCities.Core.V3;
using SimpleCities.Road.V3;
using System.IO;

namespace SimpleCities.Tests.V3;

public sealed class RoadGraphV3ApplicationTests
{
    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            RoadGraphV3Revision revision = CreateRevision();
            app.Controller.ReplaceWithFullReset(revision, 1);

            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));
            Assert.Equal("city-001", app.CurrentSlotID);

            var loaded = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            Assert.True(loaded.Load("city-001"));
            Assert.Equal("city-001", loaded.CurrentSlotID);
            Assert.Equal(revision.Nodes.Count, loaded.Controller.Facade.Revision.Nodes.Count);
            Assert.Equal(revision.Edges.Count, loaded.Controller.Facade.Revision.Edges.Count);
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
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);

            Assert.False(app.Load("missing"));
            Assert.Equal(string.Empty, app.CurrentSlotID);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void TryAutosave_WhenGateFree_Saves()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            RoadGraphV3Revision revision = CreateRevision();

            V3AutosaveDecision decision = app.TryAutosave(
                "city-001",
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
    public void TryUndo_WithCurrentToken_UndoesNode()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            Assert.True(app.Controller.TryAddNode(Vector2.Zero, out _));
            GraphStateToken token = app.Controller.Facade.CurrentToken;

            Assert.True(app.TryUndo(token, out _));
            Assert.Empty(app.Controller.Facade.Revision.Nodes);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Delete_RemovesSlot()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            Assert.True(app.Delete("city-001"));
            Assert.Empty(app.List());
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void List_ReturnsSavedSlots()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));
            Assert.True(app.Save("city-002", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            IReadOnlyList<V3SlotSummary> list = app.List();

            Assert.Equal(2, list.Count);
            Assert.All(list, summary => Assert.True(summary.IsUsable));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void NewCity_ResetsControllerAndSlot()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            app.NewCity(lineageID: 42);

            Assert.Empty(app.Controller.Facade.Revision.Nodes);
            Assert.Empty(app.Controller.Facade.Revision.Edges);
            Assert.Equal(42, app.Controller.Facade.LineageID);
            Assert.Equal(string.Empty, app.CurrentSlotID);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string GetTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"v3-app-{Guid.NewGuid():N}");

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
