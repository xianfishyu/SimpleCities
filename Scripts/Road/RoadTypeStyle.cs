using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Display-only style mapped to one stable <see cref="RoadType"/> value.
/// </summary>
[GlobalClass]
public partial class RoadTypeStyle : Resource
{
    [Export] public RoadType RoadType { get; set; } = RoadType.Street;

    [Export] public string DisplayName { get; set; } = string.Empty;

    [Export] public Color Color { get; set; } = Colors.White;

    [Export] public float Width { get; set; } = RoadConfig.DefaultRoadWidth;

    public bool TryValidate(out string error) =>
        TryValidate(ToDefinition(), out error);

    public Godot.Collections.Dictionary GetValidationResult()
    {
        bool valid = TryValidate(out string error);
        return new Godot.Collections.Dictionary
        {
            ["valid"] = valid,
            ["error"] = error,
        };
    }

    internal RoadTypeStyleDefinition ToDefinition() =>
        new(RoadType, DisplayName, Color, Width);

    internal static bool TryValidate(
        RoadTypeStyleDefinition style,
        out string error)
    {
        if (!RoadTypeContract.IsDefined(style.RoadType))
        {
            error = $"Road type value '{(int)style.RoadType}' is not defined.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(style.DisplayName))
        {
            error = $"RoadTypeStyle '{style.RoadType}' display name cannot be empty.";
            return false;
        }

        if (!IsFinite(style.Color))
        {
            error = $"RoadTypeStyle '{style.RoadType}' color must be finite.";
            return false;
        }

        if (style.Color.A <= 0f)
        {
            error = $"RoadTypeStyle '{style.RoadType}' color must be visible (alpha > 0).";
            return false;
        }

        if (!float.IsFinite(style.Width) || style.Width <= 0f)
        {
            error = $"RoadTypeStyle '{style.RoadType}' width must be positive and finite.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool IsFinite(Color color) =>
        float.IsFinite(color.R) &&
        float.IsFinite(color.G) &&
        float.IsFinite(color.B) &&
        float.IsFinite(color.A);
}

internal readonly record struct RoadTypeStyleDefinition(
    RoadType RoadType,
    string DisplayName,
    Color Color,
    float Width);

internal readonly record struct RoadTypeStyleSnapshot(
    RoadTypeStyleDefinition Dirt,
    RoadTypeStyleDefinition Street,
    RoadTypeStyleDefinition Arterial,
    RoadTypeStyleDefinition Highway)
{
    internal static RoadTypeStyleSnapshot Create(
        IReadOnlyList<RoadTypeStyleDefinition>? styles)
    {
        if (!RoadConfig.TryValidateRoadTypeStyles(styles, out string error))
            throw new InvalidOperationException($"RoadConfig RoadTypeStyles are invalid: {error}");

        return new RoadTypeStyleSnapshot(
            Find(styles!, RoadType.Dirt),
            Find(styles!, RoadType.Street),
            Find(styles!, RoadType.Arterial),
            Find(styles!, RoadType.Highway));
    }

    internal RoadTypeStyleDefinition Resolve(RoadType roadType) => roadType switch
    {
        RoadType.Dirt => Dirt,
        RoadType.Street => Street,
        RoadType.Arterial => Arterial,
        RoadType.Highway => Highway,
        _ => throw new ArgumentOutOfRangeException(
            nameof(roadType),
            roadType,
            "RoadType is not defined."),
    };

    internal void Validate()
    {
        RoadTypeStyleSnapshot validated = Create([Dirt, Street, Arterial, Highway]);
        if (validated != this)
            throw new InvalidOperationException("RoadTypeStyle snapshot changed during validation.");
    }

    private static RoadTypeStyleDefinition Find(
        IReadOnlyList<RoadTypeStyleDefinition> styles,
        RoadType roadType)
    {
        foreach (RoadTypeStyleDefinition style in styles)
        {
            if (style.RoadType == roadType)
                return style;
        }

        throw new InvalidOperationException(
            $"RoadConfig does not contain RoadTypeStyle '{roadType}'.");
    }
}
