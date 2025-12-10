using Godot;
using System;

public partial class TrainBehavior : Node2D
{
    [ExportGroup("Path Settings")]
    [Export] public Path2D path;

    [ExportGroup("Train Physics Settings")]
    [Export(PropertyHint.None, "suffix:m")] public float carriage_length = 25f;
    [Export(PropertyHint.None, "suffix:m")] public float train_width = 5f;
    [Export(PropertyHint.None, "suffix:m")] public float carriage_gap = 1f;
    [Export(PropertyHint.None, "suffix:m/s\u00b2")] public float max_acceleration = 0.8f;
    [Export(PropertyHint.None, "suffix:m/s\u00b2")] public float max_braking = 1.0f;
    [Export(PropertyHint.None, "suffix:m/s\u00b2")] public float emergency_braking = 1.4f;
    [Export(PropertyHint.None, "suffix:km/h")] public float operation_speed = 160f;
    [Export(PropertyHint.None, "suffix:km/h")] public float max_speed = 180f;
    [Export(PropertyHint.None, "suffix:km/h")] private float current_speed = 0f;

    private float operation_speed_mps => operation_speed / 3.6f;
    private float max_speed_mps => max_speed / 3.6f;
    private float current_speed_mps => current_speed / 3.6f;

    [ExportGroup("Train Settings")]
    [Export] public string train_model = "CJ6";
    [Export] public string train_number = "G1";
    [Export(PropertyHint.None, "suffix:人")] public int max_passenger = 240;
    [Export(PropertyHint.Range, "1,16,1")] public int carriage_count = 4;

    [ExportGroup("Visual Settings")]
    [Export] public Color train_color = new Color(0.2f, 0.4f, 0.8f, 1.0f);
    [Export] public Color window_color = new Color(0.6f, 0.8f, 1.0f, 1.0f);
    [Export] public bool show_windows = true;
    [Export] public int windows_per_carriage = 6;

    [ExportGroup("Bogie Settings (转向架)")]
    [Export(PropertyHint.Range, "0.01,1,0.01")] public float body_rotation_smoothing = 0.12f;
    [Export(PropertyHint.None, "suffix:m")] public float bogie_offset = 8f;
    [Export] public bool enable_smooth_rotation = true;

    // 生成的车厢视觉节点
    private ColorRect[] carriage_visuals;
    // 转向架跟随点（每节车厢2个）
    private PathFollow2D[] bogie_followers;
    // 每节车厢的当前平滑角度
    private float[] carriage_current_angles;
    // 是否已初始化
    private bool is_initialized = false;

    public override void _Ready()
    {
        if (path != null)
        {
            Initialize(path);
        }
    }

    public void Initialize(Path2D targetPath)
    {
        this.path = targetPath;

        if (path == null || path.Curve == null)
        {
            GD.PrintErr("TrainBehavior: Path is null or has no curve!");
            return;
        }

        // 清理旧的节点
        CleanupOldNodes();

        // 创建转向架跟随点（每节车厢2个）
        bogie_followers = new PathFollow2D[carriage_count * 2];
        carriage_visuals = new ColorRect[carriage_count];
        carriage_current_angles = new float[carriage_count];

        float totalCarriageLength = carriage_length + carriage_gap;

        for (int i = 0; i < carriage_count; i++)
        {
            // 创建前转向架跟随点
            var frontBogie = new PathFollow2D();
            frontBogie.Name = $"Bogie_Front_{i}";
            frontBogie.Loop = false;
            frontBogie.Rotates = false;
            path.AddChild(frontBogie);
            bogie_followers[i * 2] = frontBogie;

            // 创建后转向架跟随点
            var rearBogie = new PathFollow2D();
            rearBogie.Name = $"Bogie_Rear_{i}";
            rearBogie.Loop = false;
            rearBogie.Rotates = false;
            path.AddChild(rearBogie);
            bogie_followers[i * 2 + 1] = rearBogie;

            // 设置初始位置（从路径起点开始）
            float carriageCenter = i * totalCarriageLength + carriage_length / 2;
            frontBogie.Progress = carriageCenter - bogie_offset;
            rearBogie.Progress = carriageCenter + bogie_offset;

            // 创建车厢视觉节点
            var carriage = CreateCarriageVisual(i);
            AddChild(carriage);
            carriage_visuals[i] = carriage;

            // 初始化角度
            carriage_current_angles[i] = 0f;
        }

        is_initialized = true;
        GD.Print($"TrainBehavior: Initialized {carriage_count} carriages on path");
    }

    private ColorRect CreateCarriageVisual(int index)
    {
        var carriage = new ColorRect();
        carriage.Name = $"Carriage_{index}";
        carriage.Size = new Vector2(carriage_length, train_width);
        carriage.Color = train_color;
        carriage.PivotOffset = new Vector2(carriage_length / 2, train_width / 2);
        carriage.ZIndex = 10;

        // 添加窗户装饰
        if (show_windows)
        {
            float windowWidth = 2f;
            float windowHeight = train_width * 0.4f;
            float windowY = (train_width - windowHeight) / 2;
            float startX = 3f;
            float spacing = (carriage_length - startX * 2 - windowWidth) / (windows_per_carriage - 1);

            for (int w = 0; w < windows_per_carriage; w++)
            {
                var window = new ColorRect();
                window.Name = $"Window_{w}";
                window.Size = new Vector2(windowWidth, windowHeight);
                window.Position = new Vector2(startX + w * spacing, windowY);
                window.Color = window_color;
                carriage.AddChild(window);
            }
        }

        // 添加车头/车尾标识
        if (index == 0 || index == carriage_count - 1)
        {
            var headMarker = new ColorRect();
            headMarker.Name = "HeadMarker";
            headMarker.Size = new Vector2(3f, train_width);
            headMarker.Position = index == 0 ? Vector2.Zero : new Vector2(carriage_length - 3f, 0);
            headMarker.Color = train_color.Darkened(0.3f);
            carriage.AddChild(headMarker);
        }

        return carriage;
    }

    private void CleanupOldNodes()
    {
        // 清理旧的车厢视觉节点
        if (carriage_visuals != null)
        {
            foreach (var visual in carriage_visuals)
            {
                visual?.QueueFree();
            }
        }

        // 清理旧的转向架跟随点
        if (bogie_followers != null)
        {
            foreach (var follower in bogie_followers)
            {
                follower?.QueueFree();
            }
        }
    }

    public override void _Process(double delta)
    {
        if (!is_initialized || bogie_followers == null || bogie_followers.Length == 0)
            return;

        // 检查是否到达路径终点
        bool atEnd = bogie_followers[0].ProgressRatio >= 1f;

        if (!atEnd)
        {
            // 移动所有转向架
            float movement = operation_speed_mps * (float)delta;
            foreach (var follower in bogie_followers)
            {
                if (follower != null)
                {
                    follower.Progress += movement;
                }
            }
        }

        // 更新车厢位置和角度
        UpdateCarriagePositions();

        // 更新主节点位置（跟随第一节车厢）
        if (carriage_visuals.Length > 0 && carriage_visuals[0] != null)
        {
            GlobalPosition = carriage_visuals[0].GlobalPosition;
        }
    }

    private void UpdateCarriagePositions()
    {
        for (int i = 0; i < carriage_count; i++)
        {
            if (bogie_followers[i * 2] == null || bogie_followers[i * 2 + 1] == null)
                continue;
            if (carriage_visuals[i] == null)
                continue;

            // 获取前后转向架位置
            Vector2 frontPos = bogie_followers[i * 2].GlobalPosition;
            Vector2 rearPos = bogie_followers[i * 2 + 1].GlobalPosition;

            // 计算车厢中心位置
            Vector2 centerPos = (frontPos + rearPos) / 2;

            // 计算目标角度
            Vector2 direction = rearPos - frontPos;
            float targetAngle = direction.Angle() + Mathf.DegToRad(90f);

            // 应用平滑旋转
            float finalAngle;
            if (enable_smooth_rotation)
            {
                finalAngle = LerpAngle(carriage_current_angles[i], targetAngle, body_rotation_smoothing);
                carriage_current_angles[i] = finalAngle;
            }
            else
            {
                finalAngle = targetAngle;
                carriage_current_angles[i] = targetAngle;
            }

            // 更新车厢位置和角度
            carriage_visuals[i].GlobalPosition = centerPos - carriage_visuals[i].PivotOffset;
            carriage_visuals[i].Rotation = finalAngle;
        }
    }

    private float LerpAngle(float from, float to, float weight)
    {
        float diff = to - from;

        // 规范化到 -PI 到 PI
        while (diff > Mathf.Pi)
            diff -= Mathf.Pi * 2;
        while (diff < -Mathf.Pi)
            diff += Mathf.Pi * 2;

        return from + diff * weight;
    }

    /// <summary>
    /// 设置列车颜色
    /// </summary>
    public void SetTrainColor(Color color)
    {
        train_color = color;
        if (carriage_visuals != null)
        {
            foreach (var visual in carriage_visuals)
            {
                if (visual != null)
                    visual.Color = color;
            }
        }
    }

    /// <summary>
    /// 获取列车总长度
    /// </summary>
    public float GetTotalLength()
    {
        return carriage_count * carriage_length + (carriage_count - 1) * carriage_gap;
    }

    /// <summary>
    /// 获取当前速度 (km/h)
    /// </summary>
    public float GetCurrentSpeed() => current_speed;

    /// <summary>
    /// 设置运行速度 (km/h)
    /// </summary>
    public void SetOperationSpeed(float speed)
    {
        operation_speed = Mathf.Clamp(speed, 0, max_speed);
    }

    public override void _ExitTree()
    {
        CleanupOldNodes();
    }
}
