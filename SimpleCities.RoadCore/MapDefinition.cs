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
}
