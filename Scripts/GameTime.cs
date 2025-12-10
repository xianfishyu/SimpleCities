using Godot;
using System;

/// <summary>
/// 游戏内时间管理系统
/// 初始化时与现实时间同步，支持加速、减速、暂停
/// </summary>
public partial class GameTime : Node
{
    public static GameTime Instance { get; private set; }

    /// <summary>
    /// 时间流速倍数（1.0 = 正常速度，2.0 = 2倍速，0.5 = 0.5倍速）
    /// </summary>
    [Export] public float TimeScale = 1.0f;

    /// <summary>
    /// 游戏时间是否暂停
    /// </summary>
    public static bool IsPaused { get; set; } = false;

    /// <summary>
    /// 游戏启动以来经过的游戏时间（秒）
    /// </summary>
    private double gameTimeElapsed = 0.0;

    /// <summary>
    /// 游戏启动时的现实时间
    /// </summary>
    private DateTime startRealTime;

    /// <summary>
    /// 上一帧暂停时的游戏时间
    /// </summary>
    private double pausedGameTime = 0.0;

    public override void _Ready()
    {
        // 单例模式保护
        if (Instance != null)
        {
            QueueFree();
            return;
        }
        Instance = this;

        // 初始化游戏启动时间为固定时刻，便于调试
        startRealTime = new DateTime(2025, 12, 9, 3, 0, 0);
        gameTimeElapsed = 0.0;
        pausedGameTime = 0.0;
    }

    public override void _Process(double delta)
    {
        if (IsPaused)
        {
            // 暂停时，保存当前游戏时间以防止时间流逝
            pausedGameTime = gameTimeElapsed;
            return;
        }

        // 应用时间缩放
        gameTimeElapsed += delta * TimeScale;
    }

    /// <summary>
    /// 获取当前游戏时间（秒）
    /// </summary>
    public static double GetGameTime() => Instance.gameTimeElapsed;

    /// <summary>
    /// 获取当前游戏时间的 DateTime 对象
    /// </summary>
    public static DateTime GetGameDateTime()
    {
        return Instance.startRealTime.AddSeconds(Instance.gameTimeElapsed);
    }

    /// <summary>
    /// 暂停游戏时间
    /// </summary>
    public static void Pause()
    {
        IsPaused = true;
    }

    /// <summary>
    /// 恢复游戏时间
    /// </summary>
    public static void Resume()
    {
        IsPaused = false;
    }

    /// <summary>
    /// 切换暂停状态
    /// </summary>
    public static void TogglePause()
    {
        IsPaused = !IsPaused;
    }

    /// <summary>
    /// 设置时间流速倍数
    /// </summary>
    /// <param name="scale">时间流速（1.0 = 正常，2.0 = 2倍速，0.5 = 0.5倍速）</param>
    public void SetTimeScale(float scale)
    {
        TimeScale = Mathf.Max(0f, scale); // 防止负数
    }

    /// <summary>
    /// 重置时间到初始状态
    /// </summary>
    public void ResetTime()
    {
        gameTimeElapsed = 0.0;
        TimeScale = 1.0f;
        IsPaused = false;
    }

    /// <summary>
    /// 设置游戏开始时间
    /// </summary>
    /// <param name="dateTime">要设置的时间</param>
    public void SetStartTime(DateTime dateTime)
    {
        startRealTime = dateTime;
        gameTimeElapsed = 0.0;
    }

    /// <summary>
    /// 设置游戏开始时间（使用时间字符串，如 "18:45"）
    /// 日期保持当前日期
    /// </summary>
    public void SetStartTimeFromString(string timeStr)
    {
        var parts = timeStr.Split(':');
        if (parts.Length >= 2 &&
            int.TryParse(parts[0], out int hours) &&
            int.TryParse(parts[1], out int minutes))
        {
            int seconds = parts.Length >= 3 && int.TryParse(parts[2], out int s) ? s : 0;
            startRealTime = new DateTime(startRealTime.Year, startRealTime.Month, startRealTime.Day,
                hours, minutes, seconds);
            gameTimeElapsed = 0.0;
        }
    }

    /// <summary>
    /// 获取当前游戏时间（从00:00开始的秒数）
    /// </summary>
    public static int GetTimeOfDaySeconds()
    {
        var dt = GetGameDateTime();
        return dt.Hour * 3600 + dt.Minute * 60 + dt.Second;
    }

    /// <summary>
    /// 获取格式化的游戏时间字符串（HH:mm:ss）
    /// </summary>
    public static string GetFormattedTime()
    {
        DateTime gameTime = GetGameDateTime();
        return gameTime.ToString("HH:mm:ss");
    }

    /// <summary>
    /// 获取格式化的游戏日期字符串（yyyy-MM-dd）
    /// </summary>
    public static string GetFormattedDate()
    {
        DateTime gameTime = GetGameDateTime();
        return gameTime.ToString("yyyy-MM-dd");
    }

    /// <summary>
    /// 获取格式化的完整日期时间字符串（yyyy-MM-dd HH:mm:ss）
    /// </summary>
    public static string GetFormattedDateTime()
    {
        DateTime gameTime = GetGameDateTime();
        return gameTime.ToString("yyyy-MM-dd HH:mm:ss");
    }

    /// <summary>
    /// 获取当前时间流速倍数
    /// </summary>
    public static float GetTimeScale() => Instance.TimeScale;
}
