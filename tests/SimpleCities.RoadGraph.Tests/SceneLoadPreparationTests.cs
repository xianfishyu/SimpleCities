using Godot;

namespace SimpleCities.Tests;

public sealed class SceneLoadPreparationTests
{
    [Fact]
    public async Task V3Slot_PreparesThroughGenericContextBeforeLegacyCommit()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"simple-cities-load-context-{Guid.NewGuid():N}");
        try
        {
            var saved = new RoadGraph();
            Assert.True(saved.SubmitPolyline(RoadType.Street, [Vector2.Zero, new Vector2(100f, 0f)]).Success);
            string expected = RoadGraphTestCodec.CaptureJson(saved);
            var store = new SaveSlotStore(directory);
            string slotID = store.Create("Generic preparation", [saved]);

            var active = new RoadGraph();
            Assert.True(active.SubmitPolyline(RoadType.Dirt, [new Vector2(0f, 50f), new Vector2(20f, 50f)]).Success);
            string original = RoadGraphTestCodec.CaptureJson(active);
            GraphStateToken originalToken = active.CurrentStateToken;
            var context = new SceneLoadPreparationContext(7, active, CreatePreparer());
            IReadOnlyList<CapturedLoadParticipant> readers = SaveSlotStore.CaptureLoadParticipants([active]);

            PreparedSceneLoad prepared = await Task.Run(() =>
            {
                PreparedSaveSlot slot = store.PrepareLoad(
                    slotID, readers, new UncoordinatedStorageOperationLease(SaveOperationKind.Load));
                IPreparedSaveState networkState = slot.GetPreparedState(context.NetworkTarget);
                IPreparedScenePresentation presentation = context.PresentationPreparer.Prepare(networkState);
                return new PreparedSceneLoad(context, slot, networkState, presentation, TimeSpan.Zero);
            });

            Assert.Equal(original, RoadGraphTestCodec.CaptureJson(active));
            Assert.Equal(originalToken, active.CurrentStateToken);
            Assert.Equal(slotID, prepared.Slot.SlotID);
            Assert.Equal(7, prepared.Context.Generation);
            RoadRendererPreparedLoad presentation = Assert.IsType<RoadRendererPreparedLoad>(prepared.Presentation);
            Assert.Equal(4, presentation.RoadVertices.Length);
            Assert.Equal([Vector2.Zero, new Vector2(100f, 0f)], Assert.Single(presentation.EdgePoints).Value);

            active.CommitPreparedLoad(prepared.NetworkState);

            Assert.Equal(expected, RoadGraphTestCodec.CaptureJson(active));
            Assert.NotEqual(originalToken.LineageID, active.CurrentStateToken.LineageID);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreparedLoad_RejectsAStateOrTargetFromAnotherPreparation(bool replaceState)
    {
        var active = new RoadGraph();
        var other = new RoadGraph();
        Assert.True(other.SubmitPolyline(RoadType.Street, [Vector2.Zero, new Vector2(100f, 0f)]).Success);
        IPreparedSaveState state = RoadGraphTestCodec.PrepareJson(active, RoadGraphTestCodec.CaptureJson(other));
        var slot = new PreparedSaveSlot("manual-1", [new PreparedSaveParticipant(active, state)]);
        var context = new SceneLoadPreparationContext(7, replaceState ? active : other, CreatePreparer());
        IPreparedSaveState suppliedState = replaceState
            ? RoadGraphTestCodec.PrepareJson(active, RoadGraphTestCodec.CaptureJson(active))
            : state;
        IPreparedScenePresentation presentation = context.PresentationPreparer.Prepare(suppliedState);
        string original = RoadGraphTestCodec.CaptureJson(active);

        Assert.Throws<InvalidOperationException>(() =>
            new PreparedSceneLoad(context, slot, suppliedState, presentation, TimeSpan.Zero));

        Assert.Equal(original, RoadGraphTestCodec.CaptureJson(active));
    }

    private static V3RoadLoadPresentationPreparer CreatePreparer()
    {
        RoadTypeStyleSnapshot styles = RoadTypeStyleSnapshot.Create(
            Enum.GetValues<RoadType>()
                .Select(type => new RoadTypeStyleDefinition(type, type.ToString(), Colors.White, 6f))
                .ToArray());
        return new V3RoadLoadPresentationPreparer(
            new RoadRenderer.RoadRendererLoadPreparer(new RoadRendererLoadSettings(0.25f, styles)));
    }
}
