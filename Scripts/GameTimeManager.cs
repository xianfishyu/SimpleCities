using Godot;
using System;
using static Godot.GD;

/// <summary>
/// 游戏内时间管理系统
/// 初始化时与现实时间同步，支持加速、减速、暂停
/// </summary>
public partial class GameTimeManager : Node
{
    public static GameTimeManager Instance { get; private set; }

    private DateTime gameTime;
    private static readonly DateTime defaultTime = new(2025, 1, 1, 0, 0, 0);
    [Export] private float timeScale = 1.0f;
    [Export] private bool isPaused = false;

    public override void _Ready()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }
        Instance = this;
        gameTime = defaultTime;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (isPaused) return;

        float scaledDelta = (float)delta * timeScale;
        gameTime = gameTime.AddSeconds(scaledDelta);
    }

    
    public static string CurrentTimeString => Instance.gameTime.ToString("HH:mm:ss");
    public static string CurrentDateString => Instance.gameTime.ToString("yyyy-MM-dd");
    public static string CurrentDateTimeString => Instance.gameTime.ToString("yyyy-MM-dd HH:mm:ss");

    public static TimeOnly CurrentTime => TimeOnly.FromDateTime(Instance.gameTime);
    public static DateOnly CurrentDate => DateOnly.FromDateTime(Instance.gameTime);
    public static DateTime CurrentDateTime => Instance.gameTime;

    public static float TimeScale { get => Instance.timeScale; set => Instance.timeScale = value; }
    public static bool IsPaused { get => Instance.isPaused; set => Instance.isPaused = value; }

    public static void ResetGameTime() => Instance.gameTime = defaultTime;
    public static void SetGameTime(DateTime setTime) => Instance.gameTime = setTime;
    public static void SetGameTimeToNow() => Instance.gameTime = DateTime.Now;

}
