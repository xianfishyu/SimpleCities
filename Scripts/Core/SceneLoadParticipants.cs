using System;
using System.Linq;

/// <summary>网络参与者的存储与加载准入入口。</summary>
internal interface ISceneNetworkLoadParticipant : IStreamingSaveable
{
    ISceneNetworkLoadAdmission BeginSceneLoadAdmission();
}

internal interface ISceneNetworkLoadAdmission : IDisposable
{
    INonThrowingLoadCommitPlan PreflightPreparedLoad(
        IPreparedSaveState preparedState,
        out IPreparedSaveState targetState);
}

internal interface ISceneToolLoadParticipant
{
    ISceneToolLoadAdmission BeginSceneLoadAdmission();
}

internal interface ISceneToolLoadAdmission : IDisposable
{
    INonThrowingLoadCommitPlan PreflightFullReset();
}

internal interface IScenePresentationLoadParticipant
{
    void ConfigureSceneGeneration(long generation);
    IScenePresentationLoadAdmission BeginSceneLoadAdmission();
}

internal interface IScenePresentationLoadAdmission : IDisposable
{
    IScenePresentationPreparer Preparer { get; }
    INonThrowingLoadCommitPlan PreflightPreparedLoad(
        IPreparedScenePresentation presentation,
        IPreparedSaveState targetState);
}

/// <summary>同一活动场景中的网络、工具和表现参与者。</summary>
internal sealed class SceneLoadParticipants
{
    internal SceneLoadParticipants(
        ISceneNetworkLoadParticipant network,
        ISceneToolLoadParticipant tools,
        IScenePresentationLoadParticipant presentation,
        SceneStoragePolicy storage)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(storage);
        if (!storage.RequiredSaveFileNames.Contains(network.SaveFileName, StringComparer.Ordinal))
            throw new ArgumentException("Scene storage must include its network payload.", nameof(storage));
        Network = network;
        Tools = tools;
        Presentation = presentation;
        Storage = storage;
    }

    internal ISceneNetworkLoadParticipant Network { get; }
    internal ISceneToolLoadParticipant Tools { get; }
    internal IScenePresentationLoadParticipant Presentation { get; }
    internal SceneStoragePolicy Storage { get; }
}
