using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

public partial class RoadGraph
{
    private const string RoadGraphPayloadType = "road-network";
    private const float MinimumIntersectionEndpointParameterTolerance = 1e-4f;
    private const float MaximumIntersectionEndpointParameterTolerance = 1e-2f;

    public ISaveSnapshot CaptureSnapshot() => CaptureRevision();

    public void WriteSnapshot(Stream destination, ISaveSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
            throw new ArgumentException("Snapshot destination must be writable.", nameof(destination));
        if (snapshot is not RoadGraphRevision revision)
            throw new ArgumentException("Snapshot is not a RoadGraph revision.", nameof(snapshot));

        using var writer = new Utf8JsonWriter(destination, new JsonWriterOptions
        {
            Indented = false,
            SkipValidation = false,
        });
        WritePayload(writer, revision);
        writer.Flush();
    }

    private static IPreparedSaveState PrepareLoad(
        Stream source,
        RoadGraphCapacity capacity,
        float bucketSize)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
            throw new ArgumentException("Save payload source must be readable.", nameof(source));

        try
        {
            using var reader = new V3JsonStreamReader(
                source,
                V3StorageBudget.CreateRoadGraphJson(capacity));
            PreparedRoadGraphTopology topology = ReadTopology(reader, capacity);
            RoadGraph preparedGraph = FromPreparedTopology(
                topology,
                capacity,
                bucketSize);
            ValidateCanonicalIntersections(preparedGraph);
            return preparedGraph.CaptureRevision();
        }
        catch (JsonException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            throw new JsonException("RoadGraph payload does not describe a canonical graph.", exception);
        }
    }

    private sealed class RoadGraphLoadReader(
        RoadGraphCapacity capacity,
        float bucketSize) : IStreamingLoadReader
    {
        public IPreparedSaveState PrepareLoad(Stream source) =>
            RoadGraph.PrepareLoad(source, capacity, bucketSize);
    }

    public void CommitPreparedLoad(IPreparedSaveState preparedState)
    {
        using RoadGraphLoadAdmission admission = BeginLoadAdmission();
        using var aggregate = new PreparedAggregateLoad(
            [PreflightPreparedLoad(admission, preparedState, out _)]);
        aggregate.Commit(new UncoordinatedStorageOperationLease(SaveOperationKind.Load));
    }

    private static void WritePayload(Utf8JsonWriter writer, RoadGraphRevision revision)
    {
        writer.WriteStartObject();
        writer.WriteString("formatFamily", V3Json.FormatFamily);
        writer.WriteString("payloadType", RoadGraphPayloadType);
        writer.WriteNumber("schemaVersion", V3Json.SchemaVersion);
        writer.WriteNumber("nextID", revision.NextIDWatermark);

        writer.WriteStartArray("nodes");
        foreach (GraphNode node in revision.Nodes.Values.OrderBy(node => node.ID))
        {
            writer.WriteStartObject();
            writer.WriteNumber("id", node.ID);
            writer.WriteNumber("x", node.Position.X);
            writer.WriteNumber("y", node.Position.Y);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteStartArray("edges");
        foreach (GraphEdge edge in revision.Edges.Values.OrderBy(edge => edge.ID))
        {
            writer.WriteStartObject();
            writer.WriteNumber("id", edge.ID);
            writer.WriteNumber("nodeAID", edge.NodeA);
            writer.WriteNumber("nodeBID", edge.NodeB);
            writer.WriteString("roadType", RoadTypeContract.ToStorageToken(edge.RoadType));
            writer.WriteStartArray("geometry");
            foreach (RoadGeometrySegment geometry in edge.GeometrySegments)
                WriteGeometry(writer, geometry);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteGeometry(Utf8JsonWriter writer, RoadGeometrySegment geometry)
    {
        writer.WriteStartObject();
        writer.WriteNumber("version", 1);
        switch (geometry)
        {
            case LineRoadGeometrySegment line:
                writer.WriteString("kind", "line");
                WritePoint(writer, "start", line.Start);
                WritePoint(writer, "end", line.End);
                break;
            case CubicBezierRoadGeometrySegment cubic:
                writer.WriteString("kind", "cubicBezier");
                WritePoint(writer, "start", cubic.Start);
                WritePoint(writer, "control1", cubic.Control1);
                WritePoint(writer, "control2", cubic.Control2);
                WritePoint(writer, "end", cubic.End);
                break;
            case CubicHermiteRoadGeometrySegment hermite:
                writer.WriteString("kind", "cubicHermite");
                WritePoint(writer, "start", hermite.Start);
                WritePoint(writer, "startTangent", hermite.StartTangent);
                WritePoint(writer, "end", hermite.End);
                WritePoint(writer, "endTangent", hermite.EndTangent);
                break;
            case CircularArcRoadGeometrySegment arc:
                writer.WriteString("kind", "circularArc");
                WritePoint(writer, "start", arc.Start);
                WritePoint(writer, "end", arc.End);
                WritePoint(writer, "center", arc.Center);
                writer.WriteNumber("radius", arc.Radius);
                writer.WriteNumber("startAngle", arc.StartAngle);
                writer.WriteNumber("endAngle", arc.EndAngle);
                writer.WriteNumber("sweepAngle", arc.SweepAngle);
                break;
            case ClothoidRoadGeometrySegment clothoid:
                writer.WriteString("kind", "clothoid");
                WritePoint(writer, "start", clothoid.Start);
                WritePoint(writer, "end", clothoid.End);
                writer.WriteNumber("startHeading", clothoid.StartHeading);
                writer.WriteNumber("reverseStartHeading", clothoid.ReverseStartHeading);
                writer.WriteNumber("startCurvature", clothoid.StartCurvature);
                writer.WriteNumber("endCurvature", clothoid.EndCurvature);
                writer.WriteNumber("arcLength", clothoid.ArcLength);
                break;
            case RationalQuadraticRoadGeometrySegment rational:
                writer.WriteString("kind", "rationalQuadratic");
                WritePoint(writer, "start", rational.Start);
                writer.WriteNumber("startWeight", rational.StartWeight);
                WritePoint(writer, "control1", rational.Control);
                writer.WriteNumber("controlWeight", rational.ControlWeight);
                WritePoint(writer, "end", rational.End);
                writer.WriteNumber("endWeight", rational.EndWeight);
                break;
            default:
                throw new NotSupportedException(
                    $"Unsupported road geometry type: {geometry.GetType().Name}.");
        }
        writer.WriteEndObject();
    }

    private static void WritePoint(Utf8JsonWriter writer, string propertyName, Vector2 point)
    {
        writer.WriteStartObject(propertyName);
        writer.WriteNumber("x", point.X);
        writer.WriteNumber("y", point.Y);
        writer.WriteEndObject();
    }

    private static void ValidateCanonicalIntersections(RoadGraph graph)
    {
        var testedPairs = new HashSet<(int FirstEdge, int FirstGeometry, int SecondEdge, int SecondGeometry)>();
        foreach (GraphEdge edge in graph._edges.Values.OrderBy(edge => edge.ID))
        {
            for (int geometryIndex = 0; geometryIndex < edge.GeometrySegments.Count; geometryIndex++)
            {
                RoadGeometrySegment geometry = edge.GeometrySegments[geometryIndex];
                foreach (EdgeGeometryRef candidate in graph._spatialIndex.QueryBounds(geometry.Bounds)
                             .OfType<EdgeGeometryRef>())
                {
                    if (candidate.EdgeID < edge.ID ||
                        (candidate.EdgeID == edge.ID && candidate.GeometryIndex <= geometryIndex))
                    {
                        continue;
                    }
                    var key = (edge.ID, geometryIndex, candidate.EdgeID, candidate.GeometryIndex);
                    if (!testedPairs.Add(key))
                        continue;

                    GraphEdge secondEdge = graph._edges[candidate.EdgeID];
                    RoadGeometrySegment secondGeometry = secondEdge.GeometrySegments[candidate.GeometryIndex];
                    RoadGeometryIntersectionResult result =
                        RoadGeometryIntersectionQuery.FindIntersections(geometry, secondGeometry);
                    if (result.HasOverlap)
                        throw new JsonException("RoadGraph payload contains overlapping geometry.");
                    foreach (RoadGeometryIntersection intersection in result.Intersections)
                    {
                        if (!IsAllowedTopologyJoin(
                                graph,
                                edge,
                                geometryIndex,
                                secondEdge,
                                candidate.GeometryIndex,
                                intersection))
                        {
                            throw new JsonException(
                                FormattableString.Invariant(
                                    $"RoadGraph payload contains an internal intersection without a node: edge {edge.ID} geometry {geometryIndex} at {intersection.FirstParameter:R}, edge {secondEdge.ID} geometry {candidate.GeometryIndex} at {intersection.SecondParameter:R} ({intersection.Kind})."));
                        }
                    }
                }
            }
        }
    }

    private static bool IsAllowedTopologyJoin(
        RoadGraph graph,
        GraphEdge firstEdge,
        int firstGeometryIndex,
        GraphEdge secondEdge,
        int secondGeometryIndex,
        RoadGeometryIntersection intersection)
    {
        if (intersection.Kind != RoadGeometryIntersectionKind.EndpointTouch)
            return false;
        RoadGeometrySegment firstGeometry = firstEdge.GeometrySegments[firstGeometryIndex];
        RoadGeometrySegment secondGeometry = secondEdge.GeometrySegments[secondGeometryIndex];
        bool firstAtStart = IsStartParameter(intersection.FirstParameter, firstGeometry) &&
                            firstGeometryIndex == 0;
        bool firstAtEnd = IsEndParameter(intersection.FirstParameter, firstGeometry) &&
                          firstGeometryIndex == firstEdge.GeometrySegments.Count - 1;
        bool secondAtStart = IsStartParameter(intersection.SecondParameter, secondGeometry) &&
                             secondGeometryIndex == 0;
        bool secondAtEnd = IsEndParameter(intersection.SecondParameter, secondGeometry) &&
                            secondGeometryIndex == secondEdge.GeometrySegments.Count - 1;

        if (firstEdge.ID == secondEdge.ID)
        {
            bool adjacent = secondGeometryIndex == firstGeometryIndex + 1 &&
                            IsEndParameter(intersection.FirstParameter, firstGeometry) &&
                            IsStartParameter(intersection.SecondParameter, secondGeometry) &&
                            IsNearCanonicalJoin(intersection.Position, firstGeometry.End);
            bool loopSeam = firstEdge.NodeA == firstEdge.NodeB && firstGeometryIndex == 0 &&
                            secondGeometryIndex == firstEdge.GeometrySegments.Count - 1 &&
                            IsStartParameter(intersection.FirstParameter, firstGeometry) &&
                            IsEndParameter(intersection.SecondParameter, secondGeometry) &&
                            IsNearCanonicalJoin(
                                intersection.Position,
                                graph._nodes[firstEdge.NodeA].Position);
            return adjacent || loopSeam;
        }

        int? firstNodeID = firstAtStart ? firstEdge.NodeA : firstAtEnd ? firstEdge.NodeB : null;
        int? secondNodeID = secondAtStart ? secondEdge.NodeA : secondAtEnd ? secondEdge.NodeB : null;
        return firstNodeID.HasValue && firstNodeID == secondNodeID &&
               IsNearCanonicalJoin(
                   intersection.Position,
                   graph._nodes[firstNodeID.Value].Position);
    }

    private static bool IsStartParameter(float value, RoadGeometrySegment geometry) =>
        value <= GetEndpointParameterTolerance(geometry);

    private static bool IsEndParameter(float value, RoadGeometrySegment geometry) =>
        value >= 1f - GetEndpointParameterTolerance(geometry);

    private static float GetEndpointParameterTolerance(RoadGeometrySegment geometry) =>
        Mathf.Clamp(
            RoadNumericPolicy.IntersectionClusterEpsilon * 8f /
            Mathf.Max(geometry.Length, RoadNumericPolicy.IntersectionClusterEpsilon),
            MinimumIntersectionEndpointParameterTolerance,
            MaximumIntersectionEndpointParameterTolerance);

    private static bool IsNearCanonicalJoin(Vector2 position, Vector2 canonicalPosition) =>
        RoadNumericPolicy.DistanceSquared(position, canonicalPosition) <=
        (double)RoadNumericPolicy.IntersectionClusterEpsilon *
        RoadNumericPolicy.IntersectionClusterEpsilon;

}
