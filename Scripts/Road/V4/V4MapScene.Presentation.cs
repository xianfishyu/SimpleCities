using Godot;
using System;
using System.Threading;
using System.Threading.Tasks;
using SimpleCities.RoadCore;

public partial class V4MapScene
{
    private enum DisplayPhase { Current, Failed, Preparing, AwaitingDraw }
    private DisplayPhase _displayPhase;
    private string _displayError = "";
    private long _displayGeneration;
    private bool _displayRetryRunning;
    private CancellationTokenSource? _displayRetryCancellation;
    private Control _displayRecovery = null!;
    private Button _retryDisplay = null!;
    private Label _displayMessage = null!;

    public string PresentationPhase => _displayPhase.ToString();
    public string PresentationError => _displayError;
    public bool IsDisplayRetryBusy => _displayRetryRunning ||
        (_displayPhase == DisplayPhase.AwaitingDraw && _displayError.Length != 0);
    private bool CanReadDisplay => IsPresentationCurrent && _displayPhase == DisplayPhase.Current;
    private bool CanRetryDisplay => _displayPhase == DisplayPhase.Failed && !_displayRetryRunning &&
        !_buildOperation.IsBusy && !_saveManager.IsOperationBusy && _pendingOperation.Length == 0 && _toolAdmission is null;

    private void InitializeDisplayRecovery()
    {
        _displayRecovery = GetNode<Control>(Controls + "DisplayRecovery");
        _retryDisplay = GetNode<Button>(Controls + "DisplayRecovery/Retry");
        _displayMessage = GetNode<Label>(Controls + "DisplayRecovery/Message");
        _retryDisplay.Pressed += () => RetryRoadDisplay();
    }

    private void UpdateDisplayRecoveryControls()
    {
        _displayRecovery.Visible = _displayError.Length != 0;
        _retryDisplay.Disabled = !CanRetryDisplay;
        _retryDisplay.Text = IsDisplayRetryBusy ? "正在重试显示…" : "重试道路显示";
        _displayMessage.Text = "道路显示更新失败\n道路编辑已暂停，可保存调试存档或重试。";
        _displayMessage.TooltipText = _displayError;
    }

    // Only reference/state writes here: this also runs inside aggregate Load commit.
    private void ResetDisplayRecoveryReferences()
    {
        _displayGeneration++;
        _displayPhase = DisplayPhase.Current;
        _displayError = "";
    }

    private void FailRoadDisplay(string reason)
    {
        _displayPhase = DisplayPhase.Failed;
        _displayError = reason;
        _status.Text = "道路显示更新失败，可重试显示";
        _buildOperation.Finish("DisplayFailed");
        CancelDraft();
        ClearRoadSelection();
        UpdateMapInfo();
        UpdateHistoryControls();
        UpdateDisplayRecoveryControls();
    }

    public Godot.Collections.Dictionary GetPresentationState() => new()
    {
        ["desiredToken"] = StateToken,
        ["presentedToken"] = _view.Presented?.Token.ToString() ?? "",
        ["drawnToken"] = _view.DrawSubmittedToken,
        ["phase"] = PresentationPhase,
        ["error"] = PresentationError,
        ["canEdit"] = CanEdit,
    };

    public bool RetryRoadDisplay()
    {
        if (!CanRetryDisplay) return false;
        _displayRetryRunning = true;
        _displayPhase = DisplayPhase.Preparing;
        _status.Text = "正在重新准备道路显示…";
        UpdateDisplayRecoveryControls();
        PrepareDisplayRetry();
        return true;
    }

    private async void PrepareDisplayRetry()
    {
        RoadNetwork network = _roads!.Network;
        RoadSnapshot snapshot = network.Snapshot;
        long generation = _displayGeneration;
        using var cancellation = new CancellationTokenSource();
        _displayRetryCancellation = cancellation;
        CancellationToken token = cancellation.Token;
        V4RoadDisplay? display = null;
#if DEBUG
        Action? beforeWork = BeforeDisplayRetryWork;
#endif
        bool IsCurrent() => IsSceneAlive && generation == _displayGeneration &&
            ReferenceEquals(_roads?.Network, network) && ReferenceEquals(network.Snapshot, snapshot);
        try
        {
            RoadSurfaceData? surface = await Task.Run(() =>
            {
#if DEBUG
                beforeWork?.Invoke();
#endif
                token.ThrowIfCancellationRequested();
                return RoadPresentation.Prepare(snapshot.Nodes, snapshot.Edges, token);
            });
            // A load in admission has priority. If it fails, leave the explicit retry available.
            if (!IsCurrent() || _toolAdmission is not null || token.IsCancellationRequested) return;
            display = _view.PrepareDisplay(snapshot, surface);
            if (!IsCurrent() || _toolAdmission is not null || token.IsCancellationRequested) return;
            _view.PublishDisplay(display);
            display = null;
            _displayPhase = DisplayPhase.AwaitingDraw;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (IsCurrent()) FailRoadDisplay(exception.Message);
        }
        finally
        {
            display?.Dispose();
            _displayRetryRunning = false;
            if (ReferenceEquals(_displayRetryCancellation, cancellation)) _displayRetryCancellation = null;
            if (IsCurrent() && _displayPhase == DisplayPhase.Preparing) _displayPhase = DisplayPhase.Failed;
            if (IsSceneAlive) UpdateDisplayRecoveryControls();
        }
    }

    private bool CompleteRoadDisplayFrame()
    {
        if (_view.DrawError.Length != 0)
        {
            if (_displayPhase is DisplayPhase.Current or DisplayPhase.AwaitingDraw) FailRoadDisplay(_view.DrawError);
            return false;
        }
        if (_displayPhase != DisplayPhase.AwaitingDraw || !IsPresentationCurrent ||
            _view.DrawSubmittedToken != StateToken) return false;
        _view.CompleteDisplayDraw();
        bool recovered = _displayError.Length != 0;
        _displayPhase = DisplayPhase.Current;
        _displayError = "";
        if (recovered) _status.Text = "道路显示已恢复";
        UpdateDisplayRecoveryControls();
        UpdateHistoryControls();
        return true;
    }
}
