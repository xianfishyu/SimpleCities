using Godot;
using System;
using System.Collections.Generic;

public sealed class CubicBezierRoadGeometrySegment : RoadGeometrySegment
{
    private const int MaxLengthSubdivisionDepth = 20;
    private const float RelativeLengthTolerance = 1e-4f;

    public override RoadGeometryKind Kind => RoadGeometryKind.CubicBezier;
    public override Vector2 Start { get; }
    public Vector2 Control1 { get; }
    public Vector2 Control2 { get; }
    public override Vector2 End { get; }
    public override float Length { get; }
    public override Rect2 Bounds { get; }

    public CubicBezierRoadGeometrySegment(
        Vector2 start,
        Vector2 control1,
        Vector2 control2,
        Vector2 end)
    {
        if (!IsFinite(start))
            throw new ArgumentException("Start must contain finite coordinates.", nameof(start));
        if (!IsFinite(control1))
            throw new ArgumentException("Control1 must contain finite coordinates.", nameof(control1));
        if (!IsFinite(control2))
            throw new ArgumentException("Control2 must contain finite coordinates.", nameof(control2));
        if (!IsFinite(end))
            throw new ArgumentException("End must contain finite coordinates.", nameof(end));
        if (start == control1 && start == control2 && start == end)
            throw new ArgumentException("A cubic Bezier segment must contain non-constant geometry.", nameof(end));

        Start = start;
        Control1 = control1;
        Control2 = control2;
        End = end;

        float controlPolygonLength = ControlPolygonLength(start, control1, control2, end);
        float tolerance = RelativeLengthTolerance * Mathf.Max(1f, controlPolygonLength);
        Length = ComputeLength(start, control1, control2, end, tolerance, 0);
        if (!float.IsFinite(Length) || Length <= 0f)
            throw new ArgumentException("A cubic Bezier segment must have positive finite length.", nameof(end));

        Bounds = ComputeBounds();
    }

    public override Vector2 GetPosition(float parameter)
    {
        EnsureParameterInDomain(parameter);
        Vector2 a = Start.Lerp(Control1, parameter);
        Vector2 b = Control1.Lerp(Control2, parameter);
        Vector2 c = Control2.Lerp(End, parameter);
        Vector2 d = a.Lerp(b, parameter);
        Vector2 e = b.Lerp(c, parameter);
        return d.Lerp(e, parameter);
    }

    public override Vector2 GetUnitTangent(float parameter)
    {
        EnsureParameterInDomain(parameter);
        float inverse = 1f - parameter;
        Vector2 derivative = 3f * (
            inverse * inverse * (Control1 - Start) +
            2f * inverse * parameter * (Control2 - Control1) +
            parameter * parameter * (End - Control2));
        if (derivative.LengthSquared() > 0f)
            return derivative.Normalized();

        Vector2 secondDerivative = 6f * (
            inverse * (Control2 - 2f * Control1 + Start) +
            parameter * (End - 2f * Control2 + Control1));
        if (secondDerivative.LengthSquared() > 0f)
            return (parameter == ParameterEnd ? -secondDerivative : secondDerivative).Normalized();

        Vector2 thirdDerivative = 6f * (End - 3f * Control2 + 3f * Control1 - Start);
        if (thirdDerivative.LengthSquared() > 0f)
            return thirdDerivative.Normalized();

        throw new InvalidOperationException("The cubic Bezier tangent is undefined for constant geometry.");
    }

    public override RoadGeometrySplit Split(float parameter)
    {
        EnsureInteriorParameter(parameter);
        SplitControlPolygon(
            Start,
            Control1,
            Control2,
            End,
            parameter,
            out var a,
            out var b,
            out var c,
            out var d,
            out var e,
            out var splitPoint);

        return new RoadGeometrySplit(
            new CubicBezierRoadGeometrySegment(Start, a, d, splitPoint),
            new CubicBezierRoadGeometrySegment(splitPoint, e, c, End));
    }

    private Rect2 ComputeBounds()
    {
        var parameters = new List<float> { ParameterStart, ParameterEnd };
        AddExtremaParameters(Start.X, Control1.X, Control2.X, End.X, parameters);
        AddExtremaParameters(Start.Y, Control1.Y, Control2.Y, End.Y, parameters);

        Vector2 first = GetPosition(parameters[0]);
        float minX = first.X;
        float maxX = first.X;
        float minY = first.Y;
        float maxY = first.Y;
        foreach (float parameter in parameters)
        {
            Vector2 point = GetPosition(parameter);
            minX = Mathf.Min(minX, point.X);
            maxX = Mathf.Max(maxX, point.X);
            minY = Mathf.Min(minY, point.Y);
            maxY = Mathf.Max(maxY, point.Y);
        }

        return new Rect2(minX, minY, maxX - minX, maxY - minY);
    }

    private static void AddExtremaParameters(
        float p0,
        float p1,
        float p2,
        float p3,
        List<float> parameters)
    {
        double a = -p0 + 3d * p1 - 3d * p2 + p3;
        double b = 2d * (p0 - 2d * p1 + p2);
        double c = p1 - p0;
        const double coefficientEpsilon = 1e-12;

        if (Math.Abs(a) <= coefficientEpsilon)
        {
            if (Math.Abs(b) <= coefficientEpsilon) return;
            AddInteriorParameter(-c / b, parameters);
            return;
        }

        double discriminant = b * b - 4d * a * c;
        if (discriminant < 0d) return;

        double root = Math.Sqrt(discriminant);
        AddInteriorParameter((-b - root) / (2d * a), parameters);
        AddInteriorParameter((-b + root) / (2d * a), parameters);
    }

    private static void AddInteriorParameter(double parameter, List<float> parameters)
    {
        if (parameter <= ParameterStart || parameter >= ParameterEnd) return;
        float value = (float)parameter;
        if (!parameters.Contains(value)) parameters.Add(value);
    }

    private static float ComputeLength(
        Vector2 p0,
        Vector2 p1,
        Vector2 p2,
        Vector2 p3,
        float tolerance,
        int depth)
    {
        float chordLength = p0.DistanceTo(p3);
        float polygonLength = ControlPolygonLength(p0, p1, p2, p3);
        if (depth >= MaxLengthSubdivisionDepth || polygonLength - chordLength <= 2f * tolerance)
            return (polygonLength + chordLength) * 0.5f;

        SplitControlPolygon(
            p0,
            p1,
            p2,
            p3,
            0.5f,
            out var a,
            out var b,
            out var c,
            out var d,
            out var e,
            out var splitPoint);
        float childTolerance = tolerance * 0.5f;
        return ComputeLength(p0, a, d, splitPoint, childTolerance, depth + 1) +
               ComputeLength(splitPoint, e, c, p3, childTolerance, depth + 1);
    }

    private static float ControlPolygonLength(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3) =>
        p0.DistanceTo(p1) + p1.DistanceTo(p2) + p2.DistanceTo(p3);

    private static void SplitControlPolygon(
        Vector2 p0,
        Vector2 p1,
        Vector2 p2,
        Vector2 p3,
        float parameter,
        out Vector2 a,
        out Vector2 b,
        out Vector2 c,
        out Vector2 d,
        out Vector2 e,
        out Vector2 splitPoint)
    {
        a = p0.Lerp(p1, parameter);
        b = p1.Lerp(p2, parameter);
        c = p2.Lerp(p3, parameter);
        d = a.Lerp(b, parameter);
        e = b.Lerp(c, parameter);
        splitPoint = d.Lerp(e, parameter);
    }
}
