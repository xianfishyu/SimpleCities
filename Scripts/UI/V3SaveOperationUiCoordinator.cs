using System;
using SimpleCities.Core.V3;

/// <summary>
/// 将 V3 存档后端与操作状态机组合为 UI 可直接调用的入口。
/// 当前后端仍为同步实现，但调用方只依赖 token/result 状态，便于后续替换为异步后端。
/// </summary>
public sealed class V3SaveOperationUiCoordinator
{
    private readonly IV3SaveOperationBackend _backend;
    private readonly V3SaveOperationController _controller;
    private readonly long _sceneGeneration;

    public V3SaveOperationUiCoordinator(
        IV3SaveOperationBackend backend,
        V3SaveOperationController? controller = null,
        long sceneGeneration = 1)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _controller = controller ?? new V3SaveOperationController();
        _sceneGeneration = sceneGeneration;
    }

    public V3SaveOperationUiState State => _controller.State;

    public bool IsBusy => _controller.IsBusy;

    public V3SaveOperationUiState SaveAs(
        string displayName,
        string cityName,
        string timestamp,
        long? population,
        decimal? funds,
        string? thumbnailFile)
    {
        if (!_controller.TryBegin(V3SaveOperationKind.Publish, _sceneGeneration))
            return _controller.State;

        V3SaveOperationResult result = _backend.SaveAs(
            displayName,
            cityName,
            timestamp,
            population,
            funds,
            thumbnailFile);
        return _controller.Complete(result);
    }

    public V3SaveOperationUiState Save(
        string slotId,
        string displayName,
        string cityName,
        string timestamp,
        long? population,
        decimal? funds,
        string? thumbnailFile)
    {
        if (!_controller.TryBegin(V3SaveOperationKind.Publish, _sceneGeneration))
            return _controller.State;

        V3SaveOperationResult result = _backend.Save(
            slotId,
            displayName,
            cityName,
            timestamp,
            population,
            funds,
            thumbnailFile);
        return _controller.Complete(result);
    }

    public V3SaveOperationUiState Load(string slotId, long lineageID)
    {
        if (!_controller.TryBegin(V3SaveOperationKind.Load, _sceneGeneration))
            return _controller.State;

        V3SaveOperationResult result = _backend.Load(slotId, lineageID);
        return _controller.Complete(result);
    }

    public V3SaveOperationUiState Delete(string slotId)
    {
        if (!_controller.TryBegin(V3SaveOperationKind.Delete, _sceneGeneration))
            return _controller.State;

        V3SaveOperationResult result = _backend.Delete(slotId);
        return _controller.Complete(result);
    }

    public V3SaveOperationUiState RequestCancel() => _controller.RequestCancel();

    public void Reset() => _controller.Reset();
}
