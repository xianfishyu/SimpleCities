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

    public bool IsCellCenter(RoadPoint point) => point.IsFinite &&
        point.X >= MinimumMetres && point.X <= MaximumMetres && point.Y >= MinimumMetres && point.Y <= MaximumMetres &&
        Math.Abs(point.X % CellSizeMetres) == CellSizeMetres / 2d &&
        Math.Abs(point.Y % CellSizeMetres) == CellSizeMetres / 2d;

    public bool IsBuildPoint(RoadPoint point) => IsPrimaryPoint(point) || IsCellCenter(point);

    internal bool IsBuildSegment(RoadPoint a, RoadPoint b) =>
        IsBuildPoint(a) && IsBuildPoint(b) && a != b && IsEightDirection(a, b) &&
        (!(IsCellCenter(a) || IsCellCenter(b)) || Math.Abs(b.X - a.X) == Math.Abs(b.Y - a.Y));

    internal bool IsEightDirection(RoadPoint a, RoadPoint b) =>
        a.X == b.X || a.Y == b.Y || Math.Abs(b.X - a.X) == Math.Abs(b.Y - a.Y);

    public RoadPoint SnapPrimary(RoadPoint point) => new(
        Math.Clamp(Math.Round(point.X / CellSizeMetres, MidpointRounding.AwayFromZero) * CellSizeMetres, MinimumMetres, MaximumMetres),
        Math.Clamp(Math.Round(point.Y / CellSizeMetres, MidpointRounding.AwayFromZero) * CellSizeMetres, MinimumMetres, MaximumMetres));

    /// <summary>选择地图内最近的主格点或格心；两组格点等距时优先主格点。</summary>
    public RoadPoint SnapBuildPoint(RoadPoint point)
    {
        if (!point.IsFinite) return point;
        RoadPoint primary = SnapPrimary(point);
        double half = CellSizeMetres / 2d;
        var center = new RoadPoint(
            Math.Clamp(Math.Round((point.X - half) / CellSizeMetres, MidpointRounding.AwayFromZero) * CellSizeMetres + half,
                MinimumMetres + half, MaximumMetres - half),
            Math.Clamp(Math.Round((point.Y - half) / CellSizeMetres, MidpointRounding.AwayFromZero) * CellSizeMetres + half,
                MinimumMetres + half, MaximumMetres - half));
        double primaryDistance = Math.Pow(primary.X - point.X, 2) + Math.Pow(primary.Y - point.Y, 2);
        double centerDistance = Math.Pow(center.X - point.X, 2) + Math.Pow(center.Y - point.Y, 2);
        return primaryDistance <= centerDistance ? primary : center;
    }

    /// <summary>按最近八方向投影；后续核心校验方向合法性，不将格心横竖拖动偷偷改为对角。</summary>
    public RoadPoint SnapDragEnd(RoadPoint start, RoadPoint cursor)
    {
        if (!IsBuildPoint(start) || !cursor.IsFinite)
            return start;
        double dx = cursor.X - start.X;
        double dy = cursor.Y - start.Y;
        double angle = Math.Atan2(dy, dx);
        int direction = (int)Math.Round(angle / (Math.PI / 4));
        int sx = Math.Sign(Math.Round(Math.Cos(direction * Math.PI / 4)));
        int sy = Math.Sign(Math.Round(Math.Sin(direction * Math.PI / 4)));
        double step = sx != 0 && sy != 0 ? CellSizeMetres / 2d : CellSizeMetres;
        double projectedSteps = (dx * sx + dy * sy) / ((sx * sx + sy * sy) * step);
        double maximum = double.PositiveInfinity;
        if (sx != 0) maximum = Math.Min(maximum, (sx > 0 ? MaximumMetres - start.X : start.X - MinimumMetres) / step);
        if (sy != 0) maximum = Math.Min(maximum, (sy > 0 ? MaximumMetres - start.Y : start.Y - MinimumMetres) / step);
        double steps = Math.Clamp(Math.Round(projectedSteps, MidpointRounding.AwayFromZero), 0, Math.Floor(maximum));
        return new RoadPoint(start.X + sx * steps * step, start.Y + sy * steps * step);
    }
}
