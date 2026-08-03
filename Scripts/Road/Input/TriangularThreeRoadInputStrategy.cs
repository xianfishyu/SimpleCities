using Godot;
using System;
using System.Collections.Generic;

/// <summary>以三角形单元中心为锚点、每格三个跨边邻居的输入策略。</summary>
public sealed class TriangularThreeRoadInputStrategy : IRoadInputStrategy
{
    private static readonly float SqrtThree = Mathf.Sqrt(3f);
    private readonly float _stepLength;
    private readonly Vector2 _basisQ;
    private readonly Vector2 _basisR;
    private readonly Vector2[] _fromPrimary;
    private readonly Vector2[] _fromSecondary;

    public float InteractionRadius => _stepLength * 0.8f;

    public TriangularThreeRoadInputStrategy(float stepLength)
    {
        if (!float.IsFinite(stepLength) || stepLength <= 0f)
            throw new ArgumentOutOfRangeException(nameof(stepLength), "Step length must be finite and positive.");

        _stepLength = stepLength;
        float halfWidth = SqrtThree * stepLength / 2f;
        _basisQ = new Vector2(-halfWidth, stepLength * 1.5f);
        _basisR = new Vector2(halfWidth, stepLength * 1.5f);
        _fromPrimary =
        [
            new Vector2(0f, stepLength),
            new Vector2(halfWidth, -stepLength / 2f),
            new Vector2(-halfWidth, -stepLength / 2f),
        ];
        _fromSecondary =
        [
            -_fromPrimary[0],
            -_fromPrimary[1],
            -_fromPrimary[2],
        ];
    }

    public Vector2 SnapPointer(Vector2 worldPosition) => FindClosestSite(worldPosition).Position;

    public RoadPathDraft BuildDraft(Vector2 startPosition, Vector2 pointerPosition)
    {
        Vector2 pointerDelta = pointerPosition - startPosition;
        int stepCount = Math.Max(0, Mathf.RoundToInt(pointerDelta.Length() / _stepLength));
        if (stepCount == 0)
            return RoadPathDraft.Empty(startPosition);

        bool isPrimary = FindClosestSite(startPosition).IsPrimary;
        Vector2 current = startPosition;
        var points = new List<Vector2>(stepCount + 1) { startPosition };
        for (int index = 0; index < stepCount; index++)
        {
            Vector2 remaining = pointerPosition - current;
            Vector2[] candidates = isPrimary ? _fromPrimary : _fromSecondary;
            Vector2 bestStep = candidates[0];
            float bestProjection = float.NegativeInfinity;
            foreach (Vector2 step in candidates)
            {
                float projection = remaining.Dot(step / _stepLength);
                if (projection > bestProjection)
                {
                    bestProjection = projection;
                    bestStep = step;
                }
            }

            current += bestStep;
            points.Add(current);
            isPrimary = !isPrimary;
        }

        return RoadPathDraft.FromPolyline(points);
    }

    private (Vector2 Position, bool IsPrimary) FindClosestSite(Vector2 worldPosition)
    {
        Vector2 bestPosition = Vector2.Zero;
        bool bestIsPrimary = true;
        float bestDistanceSquared = float.MaxValue;
        EvaluateLattice(worldPosition, Vector2.Zero, true, ref bestPosition, ref bestIsPrimary, ref bestDistanceSquared);
        EvaluateLattice(
            worldPosition,
            new Vector2(0f, _stepLength),
            false,
            ref bestPosition,
            ref bestIsPrimary,
            ref bestDistanceSquared);
        return (bestPosition, bestIsPrimary);
    }

    private void EvaluateLattice(
        Vector2 worldPosition,
        Vector2 offset,
        bool isPrimary,
        ref Vector2 bestPosition,
        ref bool bestIsPrimary,
        ref float bestDistanceSquared)
    {
        Vector2 local = worldPosition - offset;
        float horizontal = local.X / (SqrtThree * _stepLength / 2f);
        float vertical = local.Y / (_stepLength * 1.5f);
        float q = (vertical - horizontal) / 2f;
        float r = (vertical + horizontal) / 2f;
        int baseQ = Mathf.FloorToInt(q);
        int baseR = Mathf.FloorToInt(r);

        for (int qOffset = -1; qOffset <= 2; qOffset++)
        {
            for (int rOffset = -1; rOffset <= 2; rOffset++)
            {
                Vector2 candidate = offset + _basisQ * (baseQ + qOffset) + _basisR * (baseR + rOffset);
                float distanceSquared = worldPosition.DistanceSquaredTo(candidate);
                if (distanceSquared >= bestDistanceSquared)
                    continue;

                bestDistanceSquared = distanceSquared;
                bestPosition = candidate;
                bestIsPrimary = isPrimary;
            }
        }
    }
}
