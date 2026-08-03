using Godot;
using System;
using System.Collections.Generic;

/// <summary>以 pointy-top 六边形单元中心为锚点的六方向输入策略。</summary>
public sealed class HexSixRoadInputStrategy : IRoadInputStrategy
{
    private static readonly float SqrtThree = Mathf.Sqrt(3f);
    private readonly float _stepLength;
    private readonly Vector2[] _steps;

    public float InteractionRadius => _stepLength * 0.8f;

    public HexSixRoadInputStrategy(float stepLength)
    {
        if (!float.IsFinite(stepLength) || stepLength <= 0f)
            throw new ArgumentOutOfRangeException(nameof(stepLength), "Step length must be finite and positive.");

        _stepLength = stepLength;
        float halfWidth = SqrtThree * stepLength / 2f;
        _steps =
        [
            new Vector2(halfWidth, -stepLength / 2f),
            new Vector2(halfWidth, stepLength / 2f),
            new Vector2(0f, stepLength),
            new Vector2(-halfWidth, stepLength / 2f),
            new Vector2(-halfWidth, -stepLength / 2f),
            new Vector2(0f, -stepLength),
        ];
    }

    public Vector2 SnapPointer(Vector2 worldPosition)
    {
        float axialQ = worldPosition.X / (SqrtThree * _stepLength / 2f);
        float axialR = worldPosition.Y / _stepLength - axialQ / 2f;
        (int q, int r) = RoundAxial(axialQ, axialR);
        return AxialToWorld(q, r);
    }

    public RoadPathDraft BuildDraft(Vector2 startPosition, Vector2 pointerPosition)
    {
        Vector2 pointerDelta = pointerPosition - startPosition;
        Vector2 bestStep = _steps[0];
        float bestProjection = 0f;
        foreach (Vector2 step in _steps)
        {
            float projection = pointerDelta.Dot(step / _stepLength);
            if (projection > bestProjection)
            {
                bestProjection = projection;
                bestStep = step;
            }
        }

        int stepCount = Math.Max(0, Mathf.RoundToInt(bestProjection / _stepLength));
        if (stepCount == 0)
            return RoadPathDraft.Empty(startPosition);

        var points = new List<Vector2>(stepCount + 1) { startPosition };
        for (int index = 1; index <= stepCount; index++)
            points.Add(startPosition + bestStep * index);
        return RoadPathDraft.FromPolyline(points);
    }

    private Vector2 AxialToWorld(int q, int r) => new(
        SqrtThree * _stepLength / 2f * q,
        _stepLength * (r + q / 2f));

    private static (int q, int r) RoundAxial(float q, float r)
    {
        float cubeY = -q - r;
        int roundedQ = Mathf.RoundToInt(q);
        int roundedR = Mathf.RoundToInt(r);
        int roundedY = Mathf.RoundToInt(cubeY);
        float qDifference = Mathf.Abs(roundedQ - q);
        float rDifference = Mathf.Abs(roundedR - r);
        float yDifference = Mathf.Abs(roundedY - cubeY);

        if (qDifference > rDifference && qDifference > yDifference)
            roundedQ = -roundedR - roundedY;
        else if (rDifference > yDifference)
            roundedR = -roundedQ - roundedY;

        return (roundedQ, roundedR);
    }
}
