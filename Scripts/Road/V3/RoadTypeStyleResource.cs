using Godot;
using System;

namespace SimpleCities.Road.V3;

/// <summary>
/// Godot Resource 形式的 RoadTypeStyle，支持 .tres 序列化与编辑器导出。
/// 纯 C# 数据类见 <see cref="RoadTypeStyle"/>。
/// </summary>
[GlobalClass]
public partial class RoadTypeStyleResource : Resource
{
    [Export] public RoadType RoadType { get; set; } = RoadType.Street;
    [Export] public string DisplayName { get; set; } = string.Empty;
    [Export] public Color Color { get; set; } = Colors.White;
    [Export] public float Width { get; set; } = 1f;

    public RoadTypeStyle ToData() =>
        new()
        {
            RoadType = RoadType,
            DisplayName = DisplayName,
            Color = Color,
            Width = Width,
        };

    public void FromData(RoadTypeStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        RoadType = style.RoadType;
        DisplayName = style.DisplayName;
        Color = style.Color;
        Width = style.Width;
    }

    public bool Validate(out string? error) => ToData().Validate(out error);
}
