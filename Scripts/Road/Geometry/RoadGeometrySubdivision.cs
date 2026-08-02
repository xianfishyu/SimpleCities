using System;
using System.Collections.Generic;

public readonly record struct RoadGeometrySubsegment(
    float ParameterStart,
    float ParameterEnd,
    RoadGeometrySegment Geometry);

public static class RoadGeometrySubdivision
{
    public static IReadOnlyList<RoadGeometrySubsegment> SplitAtParameters(
        RoadGeometrySegment geometry,
        IEnumerable<float> parameters,
        float parameterTolerance = 1e-5f)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(parameters);
        if (!float.IsFinite(parameterTolerance) ||
            parameterTolerance < 0f ||
            parameterTolerance >= 0.5f)
            throw new ArgumentOutOfRangeException(nameof(parameterTolerance));

        var sorted = new List<float>();
        foreach (float parameter in parameters)
        {
            if (!float.IsFinite(parameter) || parameter < 0f || parameter > 1f)
                throw new ArgumentOutOfRangeException(
                    nameof(parameters), parameter, "Split parameters must be finite and in [0, 1].");
            if (parameter <= parameterTolerance || parameter >= 1f - parameterTolerance)
                continue;
            sorted.Add(parameter);
        }
        sorted.Sort();

        var unique = new List<float>(sorted.Count);
        foreach (float parameter in sorted)
        {
            if (unique.Count == 0 || parameter - unique[^1] > parameterTolerance)
                unique.Add(parameter);
        }

        var result = new List<RoadGeometrySubsegment>(unique.Count + 1);
        RoadGeometrySegment remaining = geometry;
        float previousParameter = 0f;
        foreach (float parameter in unique)
        {
            float localParameter = (parameter - previousParameter) / (1f - previousParameter);
            RoadGeometrySplit split = remaining.Split(localParameter);
            result.Add(new RoadGeometrySubsegment(
                previousParameter,
                parameter,
                split.Before));
            remaining = split.After;
            previousParameter = parameter;
        }
        result.Add(new RoadGeometrySubsegment(previousParameter, 1f, remaining));
        return Array.AsReadOnly([.. result]);
    }
}
