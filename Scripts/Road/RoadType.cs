public enum RoadType
{
    Dirt = 0,
    Street = 1,
    Arterial = 2,
    Highway = 3,
}

internal static class RoadTypeContract
{
    internal static bool IsDefined(RoadType roadType) => roadType is
        RoadType.Dirt or
        RoadType.Street or
        RoadType.Arterial or
        RoadType.Highway;

    internal static string ToStorageToken(RoadType roadType) => roadType switch
    {
        RoadType.Dirt => "dirt",
        RoadType.Street => "street",
        RoadType.Arterial => "arterial",
        RoadType.Highway => "highway",
        _ => throw new System.ArgumentOutOfRangeException(nameof(roadType)),
    };

    internal static bool TryParseStorageToken(string? token, out RoadType roadType)
    {
        roadType = token switch
        {
            "dirt" => RoadType.Dirt,
            "street" => RoadType.Street,
            "arterial" => RoadType.Arterial,
            "highway" => RoadType.Highway,
            _ => default,
        };
        return token is "dirt" or "street" or "arterial" or "highway";
    }
}
