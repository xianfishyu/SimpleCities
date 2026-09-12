namespace SimpleCities.RoadCore;

/// <summary>固定米制地图参数；调整格长需要创建另一张地图。</summary>
public sealed record MapDefinition
{
    public MapDefinition(int cellSizeMetres = 100)
    {
        if (cellSizeMetres is not (25 or 50 or 100 or 200))
            throw new ArgumentOutOfRangeException(nameof(cellSizeMetres), "Cell size must be 25, 50, 100 or 200 metres.");
        CellSizeMetres = cellSizeMetres;
    }

    public int CellSizeMetres { get; }
    public int MinimumMetres => -4000;
    public int MaximumMetres => 4000;

    public bool IsPrimaryPoint(RoadPoint point) => point.IsFinite &&
        point.X >= MinimumMetres && point.X <= MaximumMetres && point.Y >= MinimumMetres && point.Y <= MaximumMetres &&
        point.X % CellSizeMetres == 0 && point.Y % CellSizeMetres == 0;

    internal bool IsEightDirection(RoadPoint a, RoadPoint b) =>
        a.X == b.X || a.Y == b.Y || Math.Abs(b.X - a.X) == Math.Abs(b.Y - a.Y);

    public RoadPoint SnapPrimary(RoadPoint point) => new(
        Math.Clamp(Math.Round(point.X / CellSizeMetres, MidpointRounding.AwayFromZero) * CellSizeMetres, MinimumMetres, MaximumMetres),
        Math.Clamp(Math.Round(point.Y / CellSizeMetres, MidpointRounding.AwayFromZero) * CellSizeMetres, MinimumMetres, MaximumMetres));

    /// <summary>按最近八方向投影，越界时保留方向并截断到最远合法主格点。</summary>
    public RoadPoint SnapDragEnd(RoadPoint start, RoadPoint cursor)
    {
        if (!IsPrimaryPoint(start) || !cursor.IsFinite)
            return start;
        double dx = cursor.X - start.X;
        double dy = cursor.Y - start.Y;
        double angle = Math.Atan2(dy, dx);
        int direction = (int)Math.Round(angle / (Math.PI / 4));
        int sx = Math.Sign(Math.Round(Math.Cos(direction * Math.PI / 4)));
        int sy = Math.Sign(Math.Round(Math.Sin(direction * Math.PI / 4)));
        double projectedCells = (dx * sx + dy * sy) / ((sx * sx + sy * sy) * CellSizeMetres);
        double maximum = double.PositiveInfinity;
        if (sx != 0) maximum = Math.Min(maximum, (sx > 0 ? MaximumMetres - start.X : start.X - MinimumMetres) / CellSizeMetres);
        if (sy != 0) maximum = Math.Min(maximum, (sy > 0 ? MaximumMetres - start.Y : start.Y - MinimumMetres) / CellSizeMetres);
        double cells = Math.Clamp(Math.Round(projectedCells, MidpointRounding.AwayFromZero), 0, maximum);
        return new RoadPoint(start.X + sx * cells * CellSizeMetres, start.Y + sy * cells * CellSizeMetres);
    }
}
