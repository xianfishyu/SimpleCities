using Godot;
using System;

/// <summary>
/// 游戏场景内的周期自动存档调度器。Timer 继承场景暂停状态，因此暂停菜单打开时不会推进周期。
/// </summary>
public partial class AutosaveController : Node
{
    private const double MinimumIntervalSeconds = 0.001d;

    private Timer _timer = null!;

    [Export(PropertyHint.Range, "1,3600,1,or_greater")]
    public double IntervalSeconds { get; set; } = 300d;

    [Export]
    public bool AutosaveEnabled { get; set; } = true;

    public int AttemptCount { get; private set; }
    public int SuccessfulSaveCount { get; private set; }
    public int FailedSaveCount { get; private set; }
    public bool LastAttemptSucceeded { get; private set; }

    [Signal]
    public delegate void AutosaveCompletedEventHandler(bool success);

    public override void _Ready()
    {
        _timer = new Timer
        {
            Name = "AutosaveTimer",
            OneShot = false,
        };
        AddChild(_timer);
        _timer.Timeout += OnAutosaveTimeout;

        if (AutosaveEnabled)
            StartTimer();
    }

    public override void _ExitTree()
    {
        if (_timer != null && GodotObject.IsInstanceValid(_timer))
        {
            _timer.Timeout -= OnAutosaveTimeout;
            _timer.Stop();
        }
    }

    /// <summary>启用或停止周期触发；重新启用时从完整周期开始计时。</summary>
    public void SetAutosaveEnabled(bool enabled)
    {
        AutosaveEnabled = enabled;
        if (_timer == null || !GodotObject.IsInstanceValid(_timer))
            return;

        if (enabled)
            StartTimer();
        else
            _timer.Stop();
    }

    /// <summary>立即执行一次自动存档；不会改变玩家当前选中的手动槽。</summary>
    public bool RunAutosaveNow()
    {
        AttemptCount++;
        SaveManager? saveManager = GodotObject.IsInstanceValid(SaveManager.Instance)
            ? SaveManager.Instance
            : null;
        bool success = saveManager?.SaveAutosave() == true;
        LastAttemptSucceeded = success;
        if (success)
            SuccessfulSaveCount++;
        else
            FailedSaveCount++;

        EmitSignal(SignalName.AutosaveCompleted, success);
        return success;
    }

    private void StartTimer()
    {
        if (!double.IsFinite(IntervalSeconds) || IntervalSeconds <= 0d)
        {
            AutosaveEnabled = false;
            _timer.Stop();
            GD.PushError($"AutosaveController: interval must be finite and positive, got {IntervalSeconds}.");
            return;
        }

        _timer.WaitTime = Math.Max(IntervalSeconds, MinimumIntervalSeconds);
        _timer.Start();
    }

    private void OnAutosaveTimeout() => RunAutosaveNow();
}
