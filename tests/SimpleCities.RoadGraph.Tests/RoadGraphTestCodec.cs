using System.Text;

namespace SimpleCities.Tests;

internal static class RoadGraphTestCodec
{
    internal static byte[] CaptureBytes(RoadGraph graph)
    {
        using var stream = new MemoryStream();
        graph.WriteSnapshot(stream, graph.CaptureSnapshot());
        return stream.ToArray();
    }

    internal static string CaptureJson(RoadGraph graph) =>
        Encoding.UTF8.GetString(CaptureBytes(graph));

    internal static RoadGraphRevision PrepareJson(RoadGraph graph, string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json), writable: false);
        return Assert.IsType<RoadGraphRevision>(graph.CaptureLoadReader().PrepareLoad(stream));
    }

    internal static RoadGraphRevision PrepareStream(RoadGraph graph, Stream stream) =>
        Assert.IsType<RoadGraphRevision>(graph.CaptureLoadReader().PrepareLoad(stream));

    internal static void LoadJson(RoadGraph graph, string json) =>
        graph.CommitPreparedLoad(PrepareJson(graph, json));

    internal static RoadGraph Clone(RoadGraph graph)
    {
        var clone = new RoadGraph();
        LoadJson(clone, CaptureJson(graph));
        return clone;
    }
}
