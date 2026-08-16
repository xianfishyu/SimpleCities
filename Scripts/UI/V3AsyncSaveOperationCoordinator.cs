using System;
using System.Threading.Tasks;
using SimpleCities.Core.V3;

/// <summary>
/// V3 存档操作的异步 UI 协调器：把后端 I/O 放到线程池执行，操作状态仍由
/// <see cref="V3SaveOperationController"/> 在主线程侧维护。
/// </summary>
public sealed class V3AsyncSaveOperationCoordinator
{
    private readonly IV3SaveOperationBackend _backend;
    private readonly V3SaveOperationController _controller;
    private readonly long _sceneGeneration;

    public V3AsyncSaveOperationCoordinator(
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

    public async Task<V3SaveOperationUiState> SaveAsAsync(
        string displayName,
        string cityName,
        string timestamp,
        long? population,
        decimal? funds,
        string? thumbnailFile)
    {
        if (!_controller.TryBegin(V3SaveOperationKind.Publish, _sceneGeneration))
            return _controller.State;

        V3SaveOperationResult result = await Task.Run(() => _backend.SaveAs(
            displayName,
            cityName,
            timestamp,
            population,
            funds,
            thumbnailFile));
        return _controller.Complete(result);
    }

    public async Task<V3SaveOperationUiState> SaveAsync(
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

        V3SaveOperationResult result = await Task.Run(() => _backend.Save(
            slotId,
            displayName,
            cityName,
            timestamp,
            population,
            funds,
            thumbnailFile));
        return _controller.Complete(result);
    }

    public async Task<V3SaveOperationUiState> LoadAsync(string slotId, long lineageID)
    {
        if (!_controller.TryBegin(V3SaveOperationKind.Load, _sceneGeneration))
            return _controller.State;

        V3SaveOperationResult result = await Task.Run(() => _backend.Load(slotId, lineageID));
        return _controller.Complete(result);
    }

    public async Task<V3SaveOperationUiState> DeleteAsync(string slotId)
    {
        if (!_controller.TryBegin(V3SaveOperationKind.Delete, _sceneGeneration))
            return _controller.State;

        V3SaveOperationResult result = await Task.Run(() => _backend.Delete(slotId));
        return _controller.Complete(result);
    }

    public V3SaveOperationUiState RequestCancel() => _controller.RequestCancel();

    public void Reset() => _controller.Reset();
}
