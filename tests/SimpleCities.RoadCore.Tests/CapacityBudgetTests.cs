using System.Text.Json.Nodes;

namespace SimpleCities.RoadCore.Tests;

public sealed class CapacityBudgetTests
{
    [Fact]
    public void CodecAcceptsAndRoundTripsExactly131072LegalPolylinePoints()
    {
        RoadNetwork network = TopologyCapacityTests.LoadLines(SawtoothLines(131072));
        Assert.Equal(205, network.Snapshot.EdgeCount);
        Assert.Equal(410, network.Snapshot.NodeCount);
        Assert.Equal(131072, network.Snapshot.Edges.Sum(edge => edge.Points.Count));

        using var saved = new MemoryStream();
        RoadCodec.Write(saved, network.Snapshot);
        saved.Position = 0;
        PreparedRoadState restored = RoadCodec.Read(saved);
        Assert.Equal(131072, restored.Edges.Sum(edge => edge.Points.Count));
        for (int i = 0; i < restored.Edges.Count; i++)
            Assert.Equal(network.Snapshot.Edges[i].Points, restored.Edges[i].Points);
    }

    [Fact]
    public void CodecRejects131073OtherwiseLegalPointsAcrossMultipleEdges()
    {
        // The additional point continues the last separated sawtooth chain;
        // it does not add an overlap, invalid bend or out-of-map coordinate.
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            TopologyCapacityTests.LoadLines(SawtoothLines(131073)));
        Assert.Contains("edge points must contain endpoints and fit the road topology budget", exception.Message);
    }

    [Fact]
    public void CodecRejects32769NodesBeforeDecodingEntriesAndPreservesActiveState()
    {
        RoadNetwork active = TopologyCapacityTests.LoadLines([[new(0, 0), new(25, 0)]]);
        RoadSnapshot before = active.Snapshot;
        RoadEditHistory history = active.History;
        using var saved = new MemoryStream();
        RoadCodec.Write(saved, before);
        JsonObject payload = JsonNode.Parse(saved.ToArray())!.AsObject();
        // Null sentinels deliberately distinguish the array admission check
        // from later per-node decoding. This is not a claimed legal road map.
        payload["nodes"] = new JsonArray(new JsonNode?[32769]);
        using var source = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(payload.ToJsonString()));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            active.PlanLoad(RoadCodec.Read(source)));

        Assert.Contains("nodes or edges exceed the supported road topology budget", exception.Message);
        Assert.Same(before, active.Snapshot);
        Assert.Same(history, active.History);
    }

    private static IEnumerable<RoadPoint[]> SawtoothLines(int totalPoints)
    {
        // Each row occupies [y, y+12.5] while rows are 25 m apart. Every
        // consecutive half-cell diagonal changes slope, so no redundant
        // collinear vertices, crossings or shared endpoints are introduced.
        for (int row = 0, remaining = totalPoints; remaining > 0; row++)
        {
            int count = Math.Min(641, remaining);
            var points = new RoadPoint[count];
            for (int column = 0; column < count; column++)
                points[column] = new(-4000 + column * 12.5, -4000 + row * 25 + column % 2 * 12.5);
            yield return points;
            remaining -= count;
        }
    }
}
