using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// 列车控制器 - 管理列车在网络上按时刻表运行
/// 基于 RailwayNetwork 进行寻路和移动
/// </summary>
public partial class TrainController : Node2D
{
    /// <summary>当前模拟时间（秒，从00:00开始）</summary>
    [Export] public float CurrentTimeSeconds = 0f;

    /// <summary>是否正在运行模拟</summary>
    [Export] public bool IsRunning = false;

    /// <summary>列车渲染宽度</summary>
    [Export] public float TrainWidth = 2f;

    /// <summary>列车渲染长度</summary>
    [Export] public float TrainRenderLength = 8f;

    // 铁路网络引用
    private RailwayNetwork network;

    // 列车列表
    private List<Train> trains = new();
    private List<ColorRect> trainVisuals = new();

    public override void _Ready()
    {
    }

    public override void _Process(double delta)
    {
        if (!IsRunning) return;
        if (GameTime.IsPaused) return;

        // 从 GameTime 获取当前模拟时间（秒）
        CurrentTimeSeconds = GameTime.GetTimeOfDaySeconds();

        // 更新所有列车
        for (int i = 0; i < trains.Count; i++)
        {
            UpdateTrain(trains[i], (float)delta);
            UpdateTrainVisual(i);
        }
    }

    /// <summary>
    /// 初始化（传入铁路网络）
    /// </summary>
    public void Initialize(RailwayNetwork railwayNetwork)
    {
        network = railwayNetwork;
    }

    /// <summary>
    /// 添加列车（根据时刻表）
    /// </summary>
    public void AddTrain(TrainSchedule schedule)
    {
        var train = new Train(schedule);

        // 设置初始位置（第一站）
        if (schedule.Entries.Count > 0 && network != null)
        {
            var firstEntry = schedule.Entries[0];
            var platform = network.FindPlatformByInfo(firstEntry.Station + "_Track" + firstEntry.Track);
            if (platform != null)
            {
                train.PositionX = platform.X;
                train.PositionY = platform.Y;
                train.CurrentNodeId = platform.Id;
            }
        }

        // 设置列车颜色
        train.TrainColor = GetTrainColor(schedule.TrainId);

        trains.Add(train);

        // 创建可视化节点
        var visual = new ColorRect();
        visual.Size = new Vector2(TrainRenderLength, TrainWidth);
        visual.Color = train.TrainColor;
        visual.ZIndex = 10;
        AddChild(visual);
        trainVisuals.Add(visual);

        GD.Print($"Added train {schedule.TrainId} with {schedule.Entries.Count} schedule entries");
    }

    /// <summary>
    /// 设置模拟开始时间
    /// </summary>
    public void SetStartTime(string timeStr)
    {
        if (GameTime.Instance != null)
        {
            GameTime.Instance.SetStartTimeFromString(timeStr);
        }

        var parts = timeStr.Split(':');
        if (parts.Length >= 2 &&
            int.TryParse(parts[0], out int hours) &&
            int.TryParse(parts[1], out int minutes))
        {
            CurrentTimeSeconds = (hours * 60 + minutes) * 60;
        }
    }

    /// <summary>
    /// 开始模拟
    /// </summary>
    public void StartSimulation()
    {
        IsRunning = true;
    }

    /// <summary>
    /// 暂停模拟
    /// </summary>
    public void PauseSimulation()
    {
        IsRunning = false;
    }

    /// <summary>
    /// 获取当前时间字符串
    /// </summary>
    public string GetCurrentTimeString()
    {
        int totalMinutes = (int)(CurrentTimeSeconds / 60);
        int hours = (totalMinutes / 60) % 24;
        int minutes = totalMinutes % 60;
        return $"{hours:D2}:{minutes:D2}";
    }

    /// <summary>
    /// 更新单个列车
    /// </summary>
    private void UpdateTrain(Train train, float delta)
    {
        if (train.State == TrainState.Arrived || network == null) return;

        var currentEntry = train.GetCurrentEntry();
        var nextEntry = train.GetNextEntry();

        if (currentEntry == null) return;

        float currentTimeSeconds = CurrentTimeSeconds;
        float entryTimeSeconds = currentEntry.TimeInSeconds;

        switch (train.State)
        {
            case TrainState.WaitingToDepart:
                // 等待发车时间
                if (currentTimeSeconds >= entryTimeSeconds && currentEntry.Event == ScheduleEventType.Departure)
                {
                    train.State = TrainState.Running;
                    train.MoveToNextEntry();
                    GD.Print($"{train.TrainId} departed from {currentEntry.Station}");
                }
                break;

            case TrainState.Running:
                // 沿路径移动
                if (currentEntry.Event == ScheduleEventType.Arrival)
                {
                    int prevIndex = train.CurrentEntryIndex - 1;
                    if (prevIndex >= 0)
                    {
                        var departEntry = train.Schedule.Entries[prevIndex];
                        float departTime = departEntry.TimeInSeconds;
                        float arriveTime = currentEntry.TimeInSeconds;

                        // 计算进度
                        float progress = 0f;
                        if (arriveTime > departTime)
                        {
                            progress = (currentTimeSeconds - departTime) / (arriveTime - departTime);
                            progress = Mathf.Clamp(progress, 0f, 1f);
                        }

                        // 获取起点和终点节点
                        var fromPlatform = network.FindPlatformByInfo(departEntry.Station + "_Track" + departEntry.Track);
                        var toPlatform = network.FindPlatformByInfo(currentEntry.Station + "_Track" + currentEntry.Track);

                        if (fromPlatform != null && toPlatform != null)
                        {
                            // 简单线性插值（未来可以用路径）
                            train.PositionX = Mathf.Lerp(fromPlatform.X, toPlatform.X, progress);
                            train.PositionY = Mathf.Lerp(fromPlatform.Y, toPlatform.Y, progress);

                            // 检查是否到达
                            if (currentTimeSeconds >= arriveTime)
                            {
                                train.State = TrainState.Stopped;
                                train.PositionX = toPlatform.X;
                                train.PositionY = toPlatform.Y;
                                train.CurrentNodeId = toPlatform.Id;
                                GD.Print($"{train.TrainId} arrived at {currentEntry.Station}");
                                train.MoveToNextEntry();
                            }
                        }
                    }
                }
                break;

            case TrainState.Stopped:
                // 等待出发
                if (currentEntry != null && currentEntry.Event == ScheduleEventType.Departure)
                {
                    if (currentTimeSeconds >= currentEntry.TimeInSeconds)
                    {
                        train.State = TrainState.Running;
                        GD.Print($"{train.TrainId} departed from {currentEntry.Station}");
                        train.MoveToNextEntry();
                    }
                }
                else if (currentEntry == null)
                {
                    train.State = TrainState.Arrived;
                }
                break;
        }
    }

    /// <summary>
    /// 更新列车可视化位置
    /// </summary>
    private void UpdateTrainVisual(int index)
    {
        if (index >= trains.Count || index >= trainVisuals.Count) return;

        var train = trains[index];
        var visual = trainVisuals[index];

        visual.Position = new Vector2(
            train.PositionX - TrainRenderLength / 2,
            train.PositionY - TrainWidth / 2
        );
        visual.Color = train.TrainColor;
    }

    /// <summary>
    /// 根据车次获取列车颜色
    /// </summary>
    private Color GetTrainColor(string trainId)
    {
        if (trainId.StartsWith("C"))
            return new Color(0.2f, 0.4f, 0.8f);
        if (trainId.StartsWith("G"))
            return new Color(0.8f, 0.2f, 0.2f);
        if (trainId.StartsWith("D"))
            return new Color(0.2f, 0.7f, 0.3f);
        return Colors.Blue;
    }

    /// <summary>
    /// 获取所有列车
    /// </summary>
    public List<Train> GetTrains() => trains;

    /// <summary>
    /// 清除所有列车
    /// </summary>
    public void ClearTrains()
    {
        foreach (var visual in trainVisuals)
        {
            visual.QueueFree();
        }
        trains.Clear();
        trainVisuals.Clear();
    }
}
