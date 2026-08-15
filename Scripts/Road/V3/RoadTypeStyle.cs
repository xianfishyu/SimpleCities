using Godot;
using System;

namespace SimpleCities.Road.V3;

/// <summary>
/// V3 道路类型展示样式数据。首版只包含稳定 key、展示名称、颜色与正有限宽度；
/// 虚线、纹理、路肩与中央分隔带不在本版范围。
/// </summary>
public sealed class RoadTypeStyle
{
    public RoadType RoadType { get; set; } = RoadType.Street;
    public string DisplayName { get; set; } = string.Empty;
    public Color Color { get; set; } = Colors.White;
    public float Width { get; set; } = 1f;

    public bool Validate(out string? error)
    {
        if (!Enum.IsDefined(RoadType))
        {
            error = "InvalidRoadType";
            return false;
        }

        if (string.IsNullOrWhiteSpace(DisplayName) ||
            char.IsWhiteSpace(DisplayName[0]) ||
            char.IsWhiteSpace(DisplayName[^1]))
        {
            error = "InvalidDisplayName";
            return false;
        }

        if (!IsFiniteColor(Color))
        {
            error = "InvalidColor";
            return false;
        }

        if (!float.IsFinite(Width) || Width <= 0f)
        {
            error = "InvalidWidth";
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsFiniteColor(Color color) =>
        float.IsFinite(color.R) &&
        float.IsFinite(color.G) &&
        float.IsFinite(color.B) &&
        float.IsFinite(color.A);
}
