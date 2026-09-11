/// <summary>V3 道路装配拥有的存储约定，通用协调器不决定代际或文件名。</summary>
internal static class V3RoadStorage
{
    internal const string NetworkFileName = "road_network";

    internal static SceneStoragePolicy Policy { get; } = new(
        "user://saves-v3",
        [NetworkFileName]);
}
