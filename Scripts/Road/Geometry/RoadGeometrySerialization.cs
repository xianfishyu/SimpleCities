using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

public enum RoadGeometryDataError
{
    None,
    EmptyPayload,
    MalformedJson,
    UnsupportedVersion,
    MissingGeometryKind,
    UnknownGeometryKind,
    MissingRequiredParameter,
    UnexpectedParameter,
    NonFiniteCoordinate,
    InvalidGeometry,
}

public readonly record struct RoadGeometryDeserializationResult(
    RoadGeometrySegment? Geometry,
    RoadGeometryDataError Error)
{
    public bool Success => Geometry is not null && Error == RoadGeometryDataError.None;
}

public sealed class RoadGeometryPointData
{
    [JsonPropertyName("x")]
    public float? X { get; set; }

    [JsonPropertyName("y")]
    public float? Y { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraFields { get; set; }

    public RoadGeometryPointData() { }

    public RoadGeometryPointData(Vector2 point)
    {
        X = point.X;
        Y = point.Y;
    }
}

public sealed class RoadGeometryData
{
    public const int CurrentVersion = 1;
    public const string LineKind = "line";
    public const string CubicBezierKind = "cubicBezier";
    public const string CubicHermiteKind = "cubicHermite";
    public const string CircularArcKind = "circularArc";
    public const string ClothoidKind = "clothoid";

    [JsonPropertyName("version")]
    public int? Version { get; set; }

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("start")]
    public RoadGeometryPointData? Start { get; set; }

    [JsonPropertyName("control1")]
    public RoadGeometryPointData? Control1 { get; set; }

    [JsonPropertyName("control2")]
    public RoadGeometryPointData? Control2 { get; set; }

    [JsonPropertyName("end")]
    public RoadGeometryPointData? End { get; set; }

    [JsonPropertyName("startTangent")]
    public RoadGeometryPointData? StartTangent { get; set; }

    [JsonPropertyName("endTangent")]
    public RoadGeometryPointData? EndTangent { get; set; }

    [JsonPropertyName("center")]
    public RoadGeometryPointData? Center { get; set; }

    [JsonPropertyName("radius")]
    public float? Radius { get; set; }

    [JsonPropertyName("startAngle")]
    public float? StartAngle { get; set; }

    [JsonPropertyName("sweepAngle")]
    public float? SweepAngle { get; set; }

    [JsonPropertyName("startHeading")]
    public float? StartHeading { get; set; }

    [JsonPropertyName("startCurvature")]
    public float? StartCurvature { get; set; }

    [JsonPropertyName("endCurvature")]
    public float? EndCurvature { get; set; }

    [JsonPropertyName("arcLength")]
    public float? ArcLength { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraFields { get; set; }
}

public static class RoadGeometrySerializer
{
    public static string Serialize(RoadGeometrySegment geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        return SaveJson.Serialize(ToData(geometry));
    }

    public static RoadGeometryData ToData(RoadGeometrySegment geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        return geometry switch
        {
            LineRoadGeometrySegment line => new RoadGeometryData
            {
                Version = RoadGeometryData.CurrentVersion,
                Kind = RoadGeometryData.LineKind,
                Start = new RoadGeometryPointData(line.Start),
                End = new RoadGeometryPointData(line.End),
            },
            CubicBezierRoadGeometrySegment cubic => new RoadGeometryData
            {
                Version = RoadGeometryData.CurrentVersion,
                Kind = RoadGeometryData.CubicBezierKind,
                Start = new RoadGeometryPointData(cubic.Start),
                Control1 = new RoadGeometryPointData(cubic.Control1),
                Control2 = new RoadGeometryPointData(cubic.Control2),
                End = new RoadGeometryPointData(cubic.End),
            },
            CubicHermiteRoadGeometrySegment hermite => new RoadGeometryData
            {
                Version = RoadGeometryData.CurrentVersion,
                Kind = RoadGeometryData.CubicHermiteKind,
                Start = new RoadGeometryPointData(hermite.Start),
                StartTangent = new RoadGeometryPointData(hermite.StartTangent),
                End = new RoadGeometryPointData(hermite.End),
                EndTangent = new RoadGeometryPointData(hermite.EndTangent),
            },
            CircularArcRoadGeometrySegment arc => new RoadGeometryData
            {
                Version = RoadGeometryData.CurrentVersion,
                Kind = RoadGeometryData.CircularArcKind,
                Center = new RoadGeometryPointData(arc.Center),
                Radius = arc.Radius,
                StartAngle = arc.StartAngle,
                SweepAngle = arc.SweepAngle,
            },
            ClothoidRoadGeometrySegment clothoid => new RoadGeometryData
            {
                Version = RoadGeometryData.CurrentVersion,
                Kind = RoadGeometryData.ClothoidKind,
                Start = new RoadGeometryPointData(clothoid.Start),
                StartHeading = clothoid.StartHeading,
                StartCurvature = clothoid.StartCurvature,
                EndCurvature = clothoid.EndCurvature,
                ArcLength = clothoid.ArcLength,
            },
            _ => throw new NotSupportedException($"Unsupported road geometry type: {geometry.GetType().Name}.")
        };
    }

    public static RoadGeometryDeserializationResult Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Failure(RoadGeometryDataError.EmptyPayload);

        try
        {
            RoadGeometryData? data = SaveJson.Deserialize<RoadGeometryData>(json);
            return FromData(data);
        }
        catch (JsonException)
        {
            return Failure(RoadGeometryDataError.MalformedJson);
        }
        catch (NotSupportedException)
        {
            return Failure(RoadGeometryDataError.MalformedJson);
        }
    }

    public static RoadGeometryDeserializationResult FromData(RoadGeometryData? data)
    {
        if (data is null)
            return Failure(RoadGeometryDataError.MalformedJson);
        if (data.Version != RoadGeometryData.CurrentVersion)
            return Failure(RoadGeometryDataError.UnsupportedVersion);
        if (string.IsNullOrWhiteSpace(data.Kind))
            return Failure(RoadGeometryDataError.MissingGeometryKind);
        if (HasExtraFields(data.ExtraFields))
            return Failure(RoadGeometryDataError.UnexpectedParameter);

        return data.Kind switch
        {
            RoadGeometryData.LineKind => DeserializeLine(data),
            RoadGeometryData.CubicBezierKind => DeserializeCubicBezier(data),
            RoadGeometryData.CubicHermiteKind => DeserializeCubicHermite(data),
            RoadGeometryData.CircularArcKind => DeserializeCircularArc(data),
            RoadGeometryData.ClothoidKind => DeserializeClothoid(data),
            _ => Failure(RoadGeometryDataError.UnknownGeometryKind),
        };
    }

    private static RoadGeometryDeserializationResult DeserializeLine(RoadGeometryData data)
    {
        if (data.Control1 is not null || data.Control2 is not null ||
            data.StartTangent is not null || data.EndTangent is not null)
            return Failure(RoadGeometryDataError.UnexpectedParameter);
        if (HasArcParameters(data))
            return Failure(RoadGeometryDataError.UnexpectedParameter);
        if (HasClothoidParameters(data))
            return Failure(RoadGeometryDataError.UnexpectedParameter);
        if (!TryReadPoint(data.Start, out Vector2 start, out RoadGeometryDataError error) ||
            !TryReadPoint(data.End, out Vector2 end, out error))
            return Failure(error);

        return CreateGeometry(() => new LineRoadGeometrySegment(start, end));
    }

    private static RoadGeometryDeserializationResult DeserializeCubicBezier(RoadGeometryData data)
    {
        if (data.StartTangent is not null || data.EndTangent is not null)
            return Failure(RoadGeometryDataError.UnexpectedParameter);
        if (HasArcParameters(data))
            return Failure(RoadGeometryDataError.UnexpectedParameter);
        if (HasClothoidParameters(data))
            return Failure(RoadGeometryDataError.UnexpectedParameter);
        if (!TryReadPoint(data.Start, out Vector2 start, out RoadGeometryDataError error) ||
            !TryReadPoint(data.Control1, out Vector2 control1, out error) ||
            !TryReadPoint(data.Control2, out Vector2 control2, out error) ||
            !TryReadPoint(data.End, out Vector2 end, out error))
            return Failure(error);

        return CreateGeometry(() => new CubicBezierRoadGeometrySegment(start, control1, control2, end));
    }

    private static RoadGeometryDeserializationResult DeserializeCubicHermite(RoadGeometryData data)
    {
        if (data.Control1 is not null || data.Control2 is not null)
            return Failure(RoadGeometryDataError.UnexpectedParameter);
        if (HasArcParameters(data))
            return Failure(RoadGeometryDataError.UnexpectedParameter);
        if (HasClothoidParameters(data))
            return Failure(RoadGeometryDataError.UnexpectedParameter);
        if (!TryReadPoint(data.Start, out Vector2 start, out RoadGeometryDataError error) ||
            !TryReadPoint(data.StartTangent, out Vector2 startTangent, out error) ||
            !TryReadPoint(data.End, out Vector2 end, out error) ||
            !TryReadPoint(data.EndTangent, out Vector2 endTangent, out error))
            return Failure(error);

        return CreateGeometry(() =>
            new CubicHermiteRoadGeometrySegment(start, startTangent, end, endTangent));
    }

    private static RoadGeometryDeserializationResult DeserializeCircularArc(RoadGeometryData data)
    {
        if (data.Start is not null || data.End is not null || data.Control1 is not null ||
            data.Control2 is not null || data.StartTangent is not null || data.EndTangent is not null)
            return Failure(RoadGeometryDataError.UnexpectedParameter);
        if (HasClothoidParameters(data))
            return Failure(RoadGeometryDataError.UnexpectedParameter);
        if (!TryReadPoint(data.Center, out Vector2 center, out RoadGeometryDataError error) ||
            !TryReadFinite(data.Radius, out float radius, out error) ||
            !TryReadFinite(data.StartAngle, out float startAngle, out error) ||
            !TryReadFinite(data.SweepAngle, out float sweepAngle, out error))
            return Failure(error);

        return CreateGeometry(() =>
            new CircularArcRoadGeometrySegment(center, radius, startAngle, sweepAngle));
    }

    private static RoadGeometryDeserializationResult DeserializeClothoid(RoadGeometryData data)
    {
        if (data.End is not null || data.Control1 is not null || data.Control2 is not null ||
            data.StartTangent is not null || data.EndTangent is not null || HasArcParameters(data))
            return Failure(RoadGeometryDataError.UnexpectedParameter);
        if (!TryReadPoint(data.Start, out Vector2 start, out RoadGeometryDataError error) ||
            !TryReadFinite(data.StartHeading, out float startHeading, out error) ||
            !TryReadFinite(data.StartCurvature, out float startCurvature, out error) ||
            !TryReadFinite(data.EndCurvature, out float endCurvature, out error) ||
            !TryReadFinite(data.ArcLength, out float arcLength, out error))
            return Failure(error);

        return CreateGeometry(() => new ClothoidRoadGeometrySegment(
            start, startHeading, startCurvature, endCurvature, arcLength));
    }

    private static bool TryReadPoint(
        RoadGeometryPointData? data,
        out Vector2 point,
        out RoadGeometryDataError error)
    {
        point = default;
        if (data?.X is null || data.Y is null)
        {
            error = RoadGeometryDataError.MissingRequiredParameter;
            return false;
        }
        if (HasExtraFields(data.ExtraFields))
        {
            error = RoadGeometryDataError.UnexpectedParameter;
            return false;
        }
        if (!float.IsFinite(data.X.Value) || !float.IsFinite(data.Y.Value))
        {
            error = RoadGeometryDataError.NonFiniteCoordinate;
            return false;
        }

        point = new Vector2(data.X.Value, data.Y.Value);
        error = RoadGeometryDataError.None;
        return true;
    }

    private static RoadGeometryDeserializationResult CreateGeometry(Func<RoadGeometrySegment> factory)
    {
        try
        {
            return new RoadGeometryDeserializationResult(factory(), RoadGeometryDataError.None);
        }
        catch (ArgumentException)
        {
            return Failure(RoadGeometryDataError.InvalidGeometry);
        }
    }

    private static bool TryReadFinite(
        float? data,
        out float value,
        out RoadGeometryDataError error)
    {
        value = default;
        if (data is null)
        {
            error = RoadGeometryDataError.MissingRequiredParameter;
            return false;
        }
        if (!float.IsFinite(data.Value))
        {
            error = RoadGeometryDataError.NonFiniteCoordinate;
            return false;
        }

        value = data.Value;
        error = RoadGeometryDataError.None;
        return true;
    }

    private static bool HasArcParameters(RoadGeometryData data) =>
        data.Center is not null || data.Radius is not null ||
        data.StartAngle is not null || data.SweepAngle is not null;

    private static bool HasClothoidParameters(RoadGeometryData data) =>
        data.StartHeading is not null || data.StartCurvature is not null ||
        data.EndCurvature is not null || data.ArcLength is not null;

    private static bool HasExtraFields(Dictionary<string, JsonElement>? fields) => fields?.Count > 0;

    private static RoadGeometryDeserializationResult Failure(RoadGeometryDataError error) =>
        new(null, error);
}
