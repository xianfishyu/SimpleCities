using System;
using SimpleCities.Core.V3;

/// <summary>
/// 暂停菜单存档操作状态机：跟踪当前 operation token，阻止重复提交，并在可取消阶段响应 Escape。
/// </summary>
public sealed class V3SaveOperationController
{
    private V3SaveOperationToken? _activeToken;
    private V3SaveOperationUiState _state = V3SaveOperationUiState.Idle();

    public V3SaveOperationUiState State => _state;

    public V3SaveOperationToken? ActiveToken => _activeToken;

    public bool IsBusy => _state.IsBusy;

    public bool IsCancelling => _state.Phase == V3SaveOperationUiPhase.Cancelling;

    public bool TryBegin(V3SaveOperationKind kind, long sceneGeneration)
    {
        if (_state.IsBusy)
            return false;

        V3SaveOperationToken token = V3SaveOperationToken.Create(kind, sceneGeneration);
        _activeToken = token;
        _state = new V3SaveOperationUiState(
            kind,
            V3SaveOperationUiPhase.Busy,
            V3SaveOperationPhase.Admission,
            true,
            true,
            false,
            false,
            false,
            null,
            null);
        return true;
    }

    public V3SaveOperationUiState Complete(V3SaveOperationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (_activeToken == null || result.Token != _activeToken)
            return _state;

        _state = V3SaveOperationUiState.FromResult(result);
        if (_state.IsTerminal)
            _activeToken = null;
        return _state;
    }

    public V3SaveOperationUiState RequestCancel()
    {
        if (!_state.IsCancellable || _activeToken == null)
            return _state;

        _state = V3SaveOperationUiState.Cancelling(_state.Kind);
        return _state;
    }

    public void Reset()
    {
        _activeToken = null;
        _state = V3SaveOperationUiState.Idle();
    }
}
