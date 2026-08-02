using Godot;
using System;
using System.Collections.Generic;

public sealed class RationalQuadraticRoadGeometrySegment : RoadGeometrySegment
{
    private const int MaxLengthSubdivisionDepth = 20;
    private const float RelativeLengthTolerance = 1e-4f;

    private readonly Vector2 _derivativeQuadratic;
    private readonly Vector2 _derivativeLinear;
    private readonly Vector2 _derivativeConstant;

    public override RoadGeometryKind Kind => RoadGeometryKind.RationalQuadratic;
    public override Vector2 Start { get; }
    public float StartWeight { get; }
    public Vector2 Control { get; }
    public float ControlWeight { get; }
    public override Vector2 End { get; }
    public float EndWeight { get; }
    public override float Length { get; }
    public override Rect2 Bounds { get; }

    public RationalQuadraticRoadGeometrySegment(
        Vector2 start,
        float startWeight,
        Vector2 control,
        float controlWeight,
        Vector2 end,
        float endWeight)
    {
        EnsureWeightedPoint(start, startWeight, nameof(start), nameof(startWeight));
        EnsureWeightedPoint(control, controlWeight, nameof(control), nameof(controlWeight));
        EnsureWeightedPoint(end, endWeight, nameof(end), nameof(endWeight));
        if (start == control && start == end)
            throw new ArgumentException("A rational quadratic segment must contain non-constant geometry.", nameof(end));

        Start = start;
        StartWeight = startWeight;
        Control = control;
        ControlWeight = controlWeight;
        End = end;
        EndWeight = endWeight;

        Vector2 weightedStart = startWeight * start;
        Vector2 weightedControl = controlWeight * control;
        Vector2 weightedEnd = endWeight * end;
        Vector2 numeratorA = weightedStart - 2f * weightedControl + weightedEnd;
        Vector2 numeratorB = 2f * (weightedControl - weightedStart);
        Vector2 numeratorC = weightedStart;
        float denominatorA = startWeight - 2f * controlWeight + endWeight;
        float denominatorB = 2f * (controlWeight - startWeight);
        float denominatorC = startWeight;
        _derivativeQuadratic = numeratorA * denominatorB - numeratorB * denominatorA;
        _derivativeLinear = 2f * (numeratorA * denominatorC - numeratorC * denominatorA);
        _derivativeConstant = numeratorB * denominatorC - numeratorC * denominatorB;

        var h0 = new HomogeneousPoint(weightedStart, startWeight);
        var h1 = new HomogeneousPoint(weightedControl, controlWeight);
        var h2 = new HomogeneousPoint(weightedEnd, endWeight);
        float polygonLength = start.DistanceTo(control) + control.DistanceTo(end);
        float tolerance = RelativeLengthTolerance * Mathf.Max(1f, polygonLength);
        Length = ComputeLength(h0, h1, h2, tolerance, 0);
        if (!float.IsFinite(Length) || Length <= 0f)
            throw new ArgumentException("A rational quadratic segment must have positive finite length.", nameof(end));

        Bounds = ComputeBounds();
    }

    public override Vector2 GetPosition(float parameter)
    {
        EnsureParameterInDomain(parameter);
        float inverse = 1f - parameter;
        float b0 = inverse * inverse;
        float b1 = 2f * inverse * parameter;
        float b2 = parameter * parameter;
        float denominator = b0 * StartWeight + b1 * ControlWeight + b2 * EndWeight;
        return (b0 * StartWeight * Start + b1 * ControlWeight * Control + b2 * EndWeight * End) / denominator;
    }

    public override Vector2 GetUnitTangent(float parameter)
    {
        EnsureParameterInDomain(parameter);
        Vector2 derivative = EvaluateDerivativeNumerator(parameter);
        if (derivative.LengthSquared() > 0f)
            return derivative.Normalized();

        Vector2 derivativeSlope = 2f * _derivativeQuadratic * parameter + _derivativeLinear;
        if (derivativeSlope.LengthSquared() > 0f)
            return (parameter == ParameterEnd ? -derivativeSlope : derivativeSlope).Normalized();
        if (_derivativeQuadratic.LengthSquared() > 0f)
            return _derivativeQuadratic.Normalized();

        throw new InvalidOperationException("The rational quadratic tangent is undefined for constant geometry.");
    }

    public override RoadGeometrySplit Split(float parameter)
    {
        EnsureInteriorParameter(parameter);
        var h0 = new HomogeneousPoint(StartWeight * Start, StartWeight);
        var h1 = new HomogeneousPoint(ControlWeight * Control, ControlWeight);
        var h2 = new HomogeneousPoint(EndWeight * End, EndWeight);
        HomogeneousPoint h01 = HomogeneousPoint.Lerp(h0, h1, parameter);
        HomogeneousPoint h12 = HomogeneousPoint.Lerp(h1, h2, parameter);
        HomogeneousPoint split = HomogeneousPoint.Lerp(h01, h12, parameter);
        return new RoadGeometrySplit(Create(h0, h01, split), Create(split, h12, h2));
    }

    private Rect2 ComputeBounds()
    {
        var parameters = new List<float> { ParameterStart, ParameterEnd };
        AddRoots(_derivativeQuadratic.X, _derivativeLinear.X, _derivativeConstant.X, parameters);
        AddRoots(_derivativeQuadratic.Y, _derivativeLinear.Y, _derivativeConstant.Y, parameters);

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

    private Vector2 EvaluateDerivativeNumerator(float parameter) =>
        (_derivativeQuadratic * parameter + _derivativeLinear) * parameter + _derivativeConstant;

    private static void AddRoots(float a, float b, float c, List<float> parameters)
    {
        const double coefficientEpsilon = 1e-12;
        if (Math.Abs(a) <= coefficientEpsilon)
        {
            if (Math.Abs(b) <= coefficientEpsilon) return;
            AddInteriorRoot(-c / b, parameters);
            return;
        }

        double discriminant = (double)b * b - 4d * a * c;
        if (discriminant < 0d) return;
        double root = Math.Sqrt(discriminant);
        AddInteriorRoot((-b - root) / (2d * a), parameters);
        AddInteriorRoot((-b + root) / (2d * a), parameters);
    }

    private static void AddInteriorRoot(double root, List<float> parameters)
    {
        if (root <= ParameterStart || root >= ParameterEnd) return;
        float parameter = (float)root;
        if (!parameters.Contains(parameter)) parameters.Add(parameter);
    }

    private static float ComputeLength(
        HomogeneousPoint h0,
        HomogeneousPoint h1,
        HomogeneousPoint h2,
        float tolerance,
        int depth)
    {
        Vector2 p0 = h0.Project();
        Vector2 p1 = h1.Project();
        Vector2 p2 = h2.Project();
        float chordLength = p0.DistanceTo(p2);
        float polygonLength = p0.DistanceTo(p1) + p1.DistanceTo(p2);
        if (depth >= MaxLengthSubdivisionDepth || polygonLength - chordLength <= 2f * tolerance)
            return (polygonLength + chordLength) * 0.5f;

        HomogeneousPoint h01 = HomogeneousPoint.Lerp(h0, h1, 0.5f);
        HomogeneousPoint h12 = HomogeneousPoint.Lerp(h1, h2, 0.5f);
        HomogeneousPoint middle = HomogeneousPoint.Lerp(h01, h12, 0.5f);
        float childTolerance = tolerance * 0.5f;
        return ComputeLength(h0, h01, middle, childTolerance, depth + 1) +
               ComputeLength(middle, h12, h2, childTolerance, depth + 1);
    }

    private static RationalQuadraticRoadGeometrySegment Create(
        HomogeneousPoint h0,
        HomogeneousPoint h1,
        HomogeneousPoint h2) =>
        new(h0.Project(), h0.Weight, h1.Project(), h1.Weight, h2.Project(), h2.Weight);

    private static void EnsureWeightedPoint(Vector2 point, float weight, string pointName, string weightName)
    {
        if (!IsFinite(point))
            throw new ArgumentException($"{pointName} must contain finite coordinates.", pointName);
        if (!float.IsFinite(weight) || weight <= 0f)
            throw new ArgumentOutOfRangeException(weightName, weight, "Rational weights must be positive and finite.");
    }

    private readonly record struct HomogeneousPoint(Vector2 WeightedPosition, float Weight)
    {
        public Vector2 Project() => WeightedPosition / Weight;

        public static HomogeneousPoint Lerp(HomogeneousPoint a, HomogeneousPoint b, float parameter) =>
            new(a.WeightedPosition.Lerp(b.WeightedPosition, parameter), Mathf.Lerp(a.Weight, b.Weight, parameter));
    }
}
