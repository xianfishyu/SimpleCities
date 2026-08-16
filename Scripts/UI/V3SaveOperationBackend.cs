using System;
using System.Collections.Generic;
using SimpleCities.Core.V3;

/// <summary>
/// 暂停菜单存档操作后端抽象：隐藏 V3 应用门面的同步 bool API，暴露 operation token/result。
/// </summary>
public interface IV3SaveOperationBackend
{
    IReadOnlyList<V3SlotSummary> ListSlots();

    V3SaveOperationResult SaveAs(
        string displayName,
        string cityName,
        string timestamp,
        long? population,
        decimal? funds,
        string? thumbnailFile);

    V3SaveOperationResult Save(
        string slotId,
        string displayName,
        string cityName,
        string timestamp,
        long? population,
        decimal? funds,
        string? thumbnailFile);

    V3SaveOperationResult Load(string slotId, long lineageID);

    V3SaveOperationResult Delete(string slotId);
}

/// <summary>
/// 将 <see cref="RoadGraphV3Application"/> 包装为 <see cref="IV3SaveOperationBackend"/>。
/// 当前底层仍是同步 bool；此适配器先统一 token/result 契约，供后续异步化替换。
/// </summary>
public sealed class V3ApplicationSaveOperationBackend : IV3SaveOperationBackend
{
    private readonly RoadGraphV3Application _application;
    private readonly long _sceneGeneration;

    public V3ApplicationSaveOperationBackend(RoadGraphV3Application application, long sceneGeneration = 1)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _sceneGeneration = sceneGeneration;
    }

    public IReadOnlyList<V3SlotSummary> ListSlots() => _application.List();

    public V3SaveOperationResult SaveAs(
        string displayName,
        string cityName,
        string timestamp,
        long? population,
        decimal? funds,
        string? thumbnailFile)
    {
        string slotId = $"manual-{Guid.NewGuid():N}";
        return Save(slotId, displayName, cityName, timestamp, population, funds, thumbnailFile);
    }

    public V3SaveOperationResult Save(
        string slotId,
        string displayName,
        string cityName,
        string timestamp,
        long? population,
        decimal? funds,
        string? thumbnailFile)
    {
        V3SaveOperationToken token = V3SaveOperationToken.Create(V3SaveOperationKind.Publish, _sceneGeneration);
        bool success = _application.Save(slotId, displayName, cityName, timestamp, population, funds, thumbnailFile);
        return success
            ? V3SaveOperationResult.Succeeded(token)
            : V3SaveOperationResult.FailedBeforeCommit(token, V3SaveOperationPhase.Prepare, "SaveRejected");
    }

    public V3SaveOperationResult Load(string slotId, long lineageID)
    {
        V3SaveOperationToken token = V3SaveOperationToken.Create(V3SaveOperationKind.Load, _sceneGeneration);
        bool success = _application.Load(slotId, lineageID);
        return success
            ? V3SaveOperationResult.Succeeded(token)
            : V3SaveOperationResult.FailedBeforeCommit(token, V3SaveOperationPhase.Prepare, "LoadFailed");
    }

    public V3SaveOperationResult Delete(string slotId)
    {
        V3SaveOperationToken token = V3SaveOperationToken.Create(V3SaveOperationKind.Delete, _sceneGeneration);
        bool success = _application.Delete(slotId);
        return success
            ? V3SaveOperationResult.Succeeded(token)
            : V3SaveOperationResult.FailedBeforeCommit(token, V3SaveOperationPhase.Prepare, "DeleteRejected");
    }
}
