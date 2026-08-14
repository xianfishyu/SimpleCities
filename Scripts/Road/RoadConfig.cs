using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 共享配置资源：网格尺寸 + 渲染参数。
/// 在 Godot 编辑器中创建一个 .tres 文件，由 RoadBuilder / RoadRenderer / RoadSystem 共同引用，
/// 避免多处 [Export] _cellSize 各自漂移导致逻辑与渲染错位。
/// </summary>
[GlobalClass]
public partial class RoadConfig : Resource
{
    public const float DefaultCellSize = 64f;
    public const float DefaultRoadWidth = 12f;

    private static readonly RoadType[] RequiredRoadTypes =
    [
        RoadType.Dirt,
        RoadType.Street,
        RoadType.Arterial,
        RoadType.Highway,
    ];

    /// <summary>网格单元尺寸（像素）。所有 Road 端点 / waypoint 都对齐到 cell 中心点。</summary>
    [Export] public float CellSize { get; set; } = DefaultCellSize;

    /// <summary>道路颜色。</summary>
    [Export] public Color RoadColor { get; set; } = new("#37474F");

    /// <summary>道路线宽（像素）。</summary>
    [Export] public float RoadWidth { get; set; } = DefaultRoadWidth;

    /// <summary>四类稳定 RoadType 的唯一展示样式映射。</summary>
    [Export]
    public Godot.Collections.Array<RoadTypeStyle>? RoadTypeStyles { get; set; } =
        CreateDefaultRoadTypeStyles();

    /// <summary>权威曲线细分为显示折线时允许的最大世界空间误差。</summary>
    [Export] public float CurveDisplayTolerance { get; set; } = RoadGeometryDisplaySampler.DefaultTolerance;

    /// <summary>
    /// 真路口（IncidenceCount >= 3）的圆点半径。
    /// degree-2 semantic boundary 由道路类型过渡 join 表现，不使用该圆点。
    /// </summary>
    [Export] public float JunctionRadius { get; set; } = 10f;

    /// <summary>真路口圆点颜色。默认偏黄，便于在深色路面上一眼可见。</summary>
    [Export] public Color JunctionColor { get; set; } = new("#FFC107");

    /// <summary>端点（ConnectionCount == 1）圆点半径。可设 0 关掉端点显示。</summary>
    [Export] public float EndpointRadius { get; set; } = 6f;

    /// <summary>端点圆点颜色（区别于 JunctionColor 与 RoadColor，便于辨认）。</summary>
    [Export] public Color EndpointColor { get; set; } = new("#90A4AE");

    /// <summary>拆除工具悬停高亮色（半透明亮色，叠加在路面上）。</summary>
    [Export] public Color HoverHighlightColor { get; set; } = new(1f, 0.8f, 0.2f, 0.6f);

    /// <summary>拆除工具悬停高亮线宽（比 RoadWidth 稍宽以视觉突出）。</summary>
    [Export] public float HoverHighlightWidth { get; set; } = 18f;

    public void NormalizeRuntimeValues(Action<string>? report = null)
    {
        if (!float.IsFinite(CellSize) || CellSize <= 0f)
        {
            report?.Invoke($"CellSize must be positive and finite; using {DefaultCellSize}.");
            CellSize = DefaultCellSize;
        }

        if (!float.IsFinite(RoadWidth) || RoadWidth <= 0f)
        {
            report?.Invoke($"RoadWidth must be positive and finite; using {DefaultRoadWidth}.");
            RoadWidth = DefaultRoadWidth;
        }
    }

    public bool TryValidateRoadTypeStyles(out string error)
    {
        if (!TryCreateRoadTypeStyleDefinitions(out List<RoadTypeStyleDefinition> styles, out error))
            return false;

        return TryValidateRoadTypeStyles(styles, out error);
    }

    public Godot.Collections.Dictionary GetRoadTypeStylesValidationResult()
    {
        bool valid = TryValidateRoadTypeStyles(out string error);
        return new Godot.Collections.Dictionary
        {
            ["valid"] = valid,
            ["error"] = error,
        };
    }

    public RoadTypeStyle GetRoadTypeStyle(RoadType roadType)
    {
        if (!TryCreateRoadTypeStyleDefinitions(out List<RoadTypeStyleDefinition> styles, out string error) ||
            !TryValidateRoadTypeStyles(styles, out error))
        {
            throw new InvalidOperationException($"RoadConfig RoadTypeStyles are invalid: {error}");
        }

        RoadTypeStyleDefinition resolved = ResolveRoadTypeStyle(styles, roadType);
        foreach (RoadTypeStyle style in RoadTypeStyles!)
        {
            if (style.RoadType == resolved.RoadType)
                return style;
        }

        throw new InvalidOperationException($"RoadConfig does not contain RoadTypeStyle '{roadType}'.");
    }

    internal RoadTypeStyleSnapshot CaptureRoadTypeStyleSnapshot()
    {
        if (!TryCreateRoadTypeStyleDefinitions(
                out List<RoadTypeStyleDefinition> styles,
                out string error))
        {
            throw new InvalidOperationException($"RoadConfig RoadTypeStyles are invalid: {error}");
        }

        return RoadTypeStyleSnapshot.Create(styles);
    }

    internal static bool TryValidateRoadTypeStyles(
        IReadOnlyList<RoadTypeStyleDefinition>? styles,
        out string error)
    {
        if (styles is null)
        {
            error = "RoadTypeStyles array cannot be null.";
            return false;
        }

        if (styles.Count != RequiredRoadTypes.Length)
        {
            error = $"RoadTypeStyles must contain exactly {RequiredRoadTypes.Length} entries; found {styles.Count}.";
            return false;
        }

        var seen = new HashSet<RoadType>();
        for (int index = 0; index < styles.Count; index++)
        {
            RoadTypeStyleDefinition style = styles[index];
            if (!RoadTypeStyle.TryValidate(style, out error))
            {
                error = $"RoadTypeStyles[{index}] is invalid: {error}";
                return false;
            }

            if (!seen.Add(style.RoadType))
            {
                error = $"RoadTypeStyles contains duplicate RoadType '{style.RoadType}'.";
                return false;
            }
        }

        foreach (RoadType roadType in RequiredRoadTypes)
        {
            if (!seen.Contains(roadType))
            {
                error = $"RoadTypeStyles is missing RoadType '{roadType}'.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    internal static RoadTypeStyleDefinition ResolveRoadTypeStyle(
        IReadOnlyList<RoadTypeStyleDefinition>? styles,
        RoadType roadType)
    {
        if (!RoadTypeContract.IsDefined(roadType))
            throw new ArgumentOutOfRangeException(nameof(roadType), roadType, "RoadType is not defined.");
        if (!TryValidateRoadTypeStyles(styles, out string error))
            throw new InvalidOperationException($"RoadConfig RoadTypeStyles are invalid: {error}");

        foreach (RoadTypeStyleDefinition style in styles!)
        {
            if (style.RoadType == roadType)
                return style;
        }

        throw new InvalidOperationException($"RoadConfig does not contain RoadTypeStyle '{roadType}'.");
    }

    private bool TryCreateRoadTypeStyleDefinitions(
        out List<RoadTypeStyleDefinition> styles,
        out string error)
    {
        styles = [];
        if (RoadTypeStyles is null)
        {
            error = "RoadTypeStyles array cannot be null.";
            return false;
        }

        styles.Capacity = RoadTypeStyles.Count;
        for (int index = 0; index < RoadTypeStyles.Count; index++)
        {
            RoadTypeStyle? style = RoadTypeStyles[index];
            if (style is null)
            {
                error = $"RoadTypeStyles[{index}] cannot be null.";
                return false;
            }
            styles.Add(style.ToDefinition());
        }

        error = string.Empty;
        return true;
    }

    private static Godot.Collections.Array<RoadTypeStyle> CreateDefaultRoadTypeStyles() =>
    [
        new RoadTypeStyle
        {
            RoadType = RoadType.Dirt,
            DisplayName = "土路",
            Color = new Color("#8A6652"),
            Width = 14f,
        },
        new RoadTypeStyle
        {
            RoadType = RoadType.Street,
            DisplayName = "街道",
            Color = new Color("#60727C"),
            Width = 20f,
        },
        new RoadTypeStyle
        {
            RoadType = RoadType.Arterial,
            DisplayName = "主干道",
            Color = new Color("#D7A928"),
            Width = 26f,
        },
        new RoadTypeStyle
        {
            RoadType = RoadType.Highway,
            DisplayName = "高速道路",
            Color = new Color("#C84B3A"),
            Width = 32f,
        },
    ];
}
