using Godot;
using System;
using System.Collections.Generic;

/// <summary>当前玩家玩法使用的方格八方向（米字型）拖拽策略。</summary>
public sealed class SquareEightRoadInputStrategy : IRoadInputStrategy
{
    private readonly float _cellSize;

    public float InteractionRadius => _cellSize * 0.8f;

    public SquareEightRoadInputStrategy(float cellSize)
    {
        if (!float.IsFinite(cellSize) || cellSize <= 0f)
            throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be finite and positive.");
        _cellSize = cellSize;
    }

    public static SquareEightRoadInputStrategy FromConfig(RoadConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new SquareEightRoadInputStrategy(config.CellSize);
    }

    public Vector2 SnapPointer(Vector2 worldPosition) => new(
        Mathf.Round(worldPosition.X / _cellSize) * _cellSize,
        Mathf.Round(worldPosition.Y / _cellSize) * _cellSize);

    public RoadPathDraft BuildDraft(Vector2 startPosition, Vector2 pointerPosition)
    {
        Vector2 pointerDelta = pointerPosition - startPosition;
        bool offsetStart = !IsPrimarySnapPoint(startPosition);
        Direction bestDirection = Direction.E;
        float bestProjection = 0f;

        foreach (Direction direction in DirectionUtil.All)
        {
            if (offsetStart && !DirectionUtil.IsDiagonal(direction))
                continue;

            Vector2I displacement = DirectionUtil.GetDisplacement(direction);
            Vector2 unitDirection = new Vector2(displacement.X, displacement.Y).Normalized();
            float projection = pointerDelta.Dot(unitDirection);
            if (projection > bestProjection)
            {
                bestProjection = projection;
                bestDirection = direction;
            }
        }

        float stepLength = DirectionUtil.Length(bestDirection, _cellSize);
        int cellCount = Math.Max(0, Mathf.RoundToInt(bestProjection / stepLength));
        if (cellCount == 0)
            return RoadPathDraft.Empty(startPosition);

        Vector2I bestDisplacement = DirectionUtil.GetDisplacement(bestDirection);
        Vector2 step = new(bestDisplacement.X * _cellSize, bestDisplacement.Y * _cellSize);
        Vector2 anchor = offsetStart ? startPosition - step / 2f : startPosition;
        var points = new List<Vector2>(cellCount + 1) { startPosition };
        for (int index = 1; index <= cellCount; index++)
            points.Add(anchor + step * index);

        var segments = new RoadGeometrySegment?[points.Count - 1];
        for (int index = 0; index < segments.Length; index++)
            segments[index] = new LineRoadGeometrySegment(points[index], points[index + 1]);

        return new RoadPathDraft(points, new RoadPath(segments));
    }

    private bool IsPrimarySnapPoint(Vector2 position)
    {
        float column = position.X / _cellSize;
        float row = position.Y / _cellSize;
        return Mathf.Abs(column - Mathf.Round(column)) < 1e-3f &&
               Mathf.Abs(row - Mathf.Round(row)) < 1e-3f;
    }
}
