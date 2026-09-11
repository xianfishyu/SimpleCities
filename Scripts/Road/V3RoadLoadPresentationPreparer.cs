using System;

/// <summary>将 V3 道路纯表现准备器接入通用场景加载契约。</summary>
internal sealed class V3RoadLoadPresentationPreparer(
    RoadRenderer.RoadRendererLoadPreparer preparer) : IScenePresentationPreparer
{
    private readonly RoadRenderer.RoadRendererLoadPreparer _preparer =
        preparer ?? throw new ArgumentNullException(nameof(preparer));

    public IPreparedScenePresentation Prepare(IPreparedSaveState networkState)
    {
        if (networkState is not RoadGraphRevision revision)
            throw new InvalidOperationException("RoadGraph load reader did not produce a revision.");

        return _preparer.Prepare(revision);
    }
}
