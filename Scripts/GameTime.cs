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

        // 初始化游戏启动时间为当前现实时间
        startRealTime = DateTime.Now;
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
    /// 加速（增加时间流速）
    /// </summary>
    /// <param name="increment">增加的倍数</param>
    public void SpeedUp(float increment = 0.5f)
    {
        SetTimeScale(TimeScale + increment);
    }

    /// <summary>
    /// 减速（降低时间流速）
    /// </summary>
    /// <param name="decrement">降低的倍数</param>
    public void SlowDown(float decrement = 0.5f)
    {
        SetTimeScale(TimeScale - decrement);
    }

    /// <summary>
    /// 重置时间到初始状态
    /// </summary>
    public void ResetTime()
    {
        startRealTime = DateTime.Now;
        gameTimeElapsed = 0.0;
        pausedGameTime = 0.0;
        TimeScale = 1.0f;
        IsPaused = false;
    }

    /// <summary>
    /// 设置游戏时间为特定时刻
    /// </summary>
    /// <param name="seconds">距离启动的秒数</param>
    public void SetGameTime(double seconds)
    {
        gameTimeElapsed = Mathf.Max(0.0f, (float)seconds);
        startRealTime = DateTime.Now.AddSeconds(-gameTimeElapsed);
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
    /// 获取游戏年份
    /// </summary>
    public static int GetYear() => GetGameDateTime().Year;

    /// <summary>
    /// 获取游戏月份
    /// </summary>
    public static int GetMonth() => GetGameDateTime().Month;

    /// <summary>
    /// 获取游戏日期
    /// </summary>
    public static int GetDay() => GetGameDateTime().Day;

    /// <summary>
    /// 获取游戏小时
    /// </summary>
    public static int GetHour() => GetGameDateTime().Hour;

    /// <summary>
    /// 获取游戏分钟
    /// </summary>
    public static int GetMinute() => GetGameDateTime().Minute;

    /// <summary>
    /// 获取游戏秒钟
    /// </summary>
    public static int GetSecond() => GetGameDateTime().Second;

    /// <summary>
    /// 获取游戏启动以来的总时间（秒）
    /// </summary>
    public static double GetElapsedSeconds() => Instance.gameTimeElapsed;

    /// <summary>
    /// 获取当前时间流速倍数
    /// </summary>
    public static float GetTimeScale() => Instance.TimeScale;
}
