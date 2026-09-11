using System;

/// <summary>可跨准备线程传递的纯表现数据，不拥有已创建的引擎资源。</summary>
internal interface IPreparedScenePresentation;

/// <summary>从已验证的网络载荷准备纯表现数据。</summary>
internal interface IScenePresentationPreparer
{
    IPreparedScenePresentation Prepare(IPreparedSaveState networkState);
}

/// <summary>在活动场景捕获、供加载准备阶段使用的目标与准备器。</summary>
internal sealed class SceneLoadPreparationContext
{
    internal SceneLoadPreparationContext(
        long generation,
        IStreamingLoadTarget networkTarget,
        IScenePresentationPreparer presentationPreparer)
    {
        ArgumentNullException.ThrowIfNull(networkTarget);
        ArgumentNullException.ThrowIfNull(presentationPreparer);
        Generation = generation;
        NetworkTarget = networkTarget;
        PresentationPreparer = presentationPreparer;
    }

    internal long Generation { get; }
    internal IStreamingLoadTarget NetworkTarget { get; }
    internal IScenePresentationPreparer PresentationPreparer { get; }
}

/// <summary>完成准备但尚未发布的场景加载载荷。</summary>
internal sealed class PreparedSceneLoad
{
    internal PreparedSceneLoad(
        SceneLoadPreparationContext context,
        PreparedSaveSlot slot,
        IPreparedSaveState networkState,
        IPreparedScenePresentation presentation,
        TimeSpan workerPrepareDuration)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(networkState);
        ArgumentNullException.ThrowIfNull(presentation);
        if (!ReferenceEquals(slot.GetPreparedState(context.NetworkTarget), networkState))
            throw new InvalidOperationException("Prepared network state does not belong to this scene load target.");

        Context = context;
        Slot = slot;
        NetworkState = networkState;
        Presentation = presentation;
        WorkerPrepareDuration = workerPrepareDuration;
    }

    internal SceneLoadPreparationContext Context { get; }
    internal PreparedSaveSlot Slot { get; }
    internal IPreparedSaveState NetworkState { get; }
    internal IPreparedScenePresentation Presentation { get; }
    internal TimeSpan WorkerPrepareDuration { get; }
}
