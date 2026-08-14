using System;

public sealed record RoadBuildRequest
{
    public RoadPath Path { get; }
    public RoadType RoadType { get; }

    public RoadBuildRequest(RoadPath path, RoadType roadType)
    {
        ArgumentNullException.ThrowIfNull(path);
        Path = path;
        RoadType = roadType;
    }
}
