using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public partial class RoadGraph
{
    private const ulong GeometryVersion = 1UL << 0;
    private const ulong GeometryKind = 1UL << 1;
    private const ulong GeometryStart = 1UL << 2;
    private const ulong GeometryEnd = 1UL << 3;
    private const ulong GeometryControl1 = 1UL << 4;
    private const ulong GeometryControl2 = 1UL << 5;
    private const ulong GeometryStartTangent = 1UL << 6;
    private const ulong GeometryEndTangent = 1UL << 7;
    private const ulong GeometryCenter = 1UL << 8;
    private const ulong GeometryRadius = 1UL << 9;
    private const ulong GeometryStartAngle = 1UL << 10;
    private const ulong GeometryEndAngle = 1UL << 11;
    private const ulong GeometrySweepAngle = 1UL << 12;
    private const ulong GeometryStartHeading = 1UL << 13;
    private const ulong GeometryReverseStartHeading = 1UL << 14;
    private const ulong GeometryStartCurvature = 1UL << 15;
    private const ulong GeometryEndCurvature = 1UL << 16;
    private const ulong GeometryArcLength = 1UL << 17;
    private const ulong GeometryStartWeight = 1UL << 18;
    private const ulong GeometryControlWeight = 1UL << 19;
    private const ulong GeometryEndWeight = 1UL << 20;

    private static PreparedRoadGraphTopology ReadTopology(
        V3JsonStreamReader reader,
        RoadGraphCapacity capacity)
    {
        reader.ReadRequired("RoadGraph payload");
        reader.Expect(JsonTokenType.StartObject, "RoadGraph payload");

        ulong seen = 0;
        string? formatFamily = null;
        string? payloadType = null;
        int nextID = 0;
        List<PreparedRoadNode>? nodes = null;
        List<PreparedRoadEdge>? edges = null;
        long geometryCount = 0;
        var allocation = new PreparedAllocationTracker(capacity);
        while (ReadObjectProperty(reader, "RoadGraph payload", out string property))
        {
            reader.ReadRequired($"RoadGraph property '{property}'");
            switch (property)
            {
                case "formatFamily":
                    MarkField(ref seen, 1UL << 0, "RoadGraph payload", property);
                    formatFamily = reader.GetString("RoadGraph formatFamily");
                    break;
                case "payloadType":
                    MarkField(ref seen, 1UL << 1, "RoadGraph payload", property);
                    payloadType = reader.GetString("RoadGraph payloadType");
                    break;
                case "schemaVersion":
                    MarkField(ref seen, 1UL << 2, "RoadGraph payload", property);
                    if (ReadNonNegativeInt32(reader, "RoadGraph schemaVersion") != V3Json.SchemaVersion)
                        throw new JsonException("RoadGraph schemaVersion is not supported.");
                    break;
                case "nextID":
                    MarkField(ref seen, 1UL << 3, "RoadGraph payload", property);
                    nextID = ReadNonNegativeInt32(reader, "RoadGraph nextID");
                    break;
                case "nodes":
                    MarkField(ref seen, 1UL << 4, "RoadGraph payload", property);
                    nodes = ReadNodes(reader, capacity, allocation);
                    break;
                case "edges":
                    MarkField(ref seen, 1UL << 5, "RoadGraph payload", property);
                    edges = ReadEdges(reader, capacity, allocation, ref geometryCount);
                    break;
                default:
                    throw new JsonException($"RoadGraph payload contains unknown property '{property}'.");
            }
        }

        RequireFields(seen, (1UL << 6) - 1, "RoadGraph payload");
        if (formatFamily != V3Json.FormatFamily)
            throw new JsonException("RoadGraph formatFamily is not simple-cities-v3.");
        if (payloadType != RoadGraphPayloadType)
            throw new JsonException("RoadGraph payloadType is not road-network.");

        reader.RequireEndOfDocument("RoadGraph payload");
        ValidatePreparedTopology(nextID, nodes!, edges!, capacity);
        return new PreparedRoadGraphTopology(nextID, nodes!, edges!);
    }

    private static List<PreparedRoadNode> ReadNodes(
        V3JsonStreamReader reader,
        RoadGraphCapacity capacity,
        PreparedAllocationTracker allocation)
    {
        reader.Expect(JsonTokenType.StartArray, "RoadGraph nodes");
        var nodes = new List<PreparedRoadNode>();
        int previousID = -1;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                return nodes;
            if (nodes.Count >= capacity.MaximumNodes)
                throw new JsonException("RoadGraph node capacity is exceeded.");
            allocation.ReserveNode();
            reader.Expect(JsonTokenType.StartObject, "RoadGraph node");

            ulong seen = 0;
            int id = 0;
            float x = 0;
            float y = 0;
            while (ReadObjectProperty(reader, "RoadGraph node", out string property))
            {
                reader.ReadRequired($"RoadGraph node property '{property}'");
                switch (property)
                {
                    case "id":
                        MarkField(ref seen, 1UL << 0, "RoadGraph node", property);
                        id = ReadNonNegativeInt32(reader, "RoadGraph node id");
                        break;
                    case "x":
                        MarkField(ref seen, 1UL << 1, "RoadGraph node", property);
                        x = ReadFiniteSingle(reader, "RoadGraph node x");
                        break;
                    case "y":
                        MarkField(ref seen, 1UL << 2, "RoadGraph node", property);
                        y = ReadFiniteSingle(reader, "RoadGraph node y");
                        break;
                    default:
                        throw new JsonException($"RoadGraph node contains unknown property '{property}'.");
                }
            }
            RequireFields(seen, 0b111, "RoadGraph node");
            if (id <= previousID)
                throw new JsonException("RoadGraph nodes must use unique, ascending IDs.");
            previousID = id;
            var position = new Vector2(x, y);
            if (!RoadNumericPolicy.IsWithinCoordinateRange(position))
                throw new JsonException($"RoadGraph node {id} is outside the numeric policy.");
            nodes.Add(new PreparedRoadNode(id, position));
        }
        throw new JsonException("RoadGraph nodes array is incomplete.");
    }

    private static List<PreparedRoadEdge> ReadEdges(
        V3JsonStreamReader reader,
        RoadGraphCapacity capacity,
        PreparedAllocationTracker allocation,
        ref long totalGeometryCount)
    {
        reader.Expect(JsonTokenType.StartArray, "RoadGraph edges");
        var edges = new List<PreparedRoadEdge>();
        int previousID = -1;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                return edges;
            if (edges.Count >= capacity.MaximumEdges)
                throw new JsonException("RoadGraph edge capacity is exceeded.");
            allocation.ReserveEdge();
            reader.Expect(JsonTokenType.StartObject, "RoadGraph edge");

            ulong seen = 0;
            int id = 0;
            int nodeAID = 0;
            int nodeBID = 0;
            RoadType roadType = default;
            List<RoadGeometrySegment>? geometry = null;
            while (ReadObjectProperty(reader, "RoadGraph edge", out string property))
            {
                reader.ReadRequired($"RoadGraph edge property '{property}'");
                switch (property)
                {
                    case "id":
                        MarkField(ref seen, 1UL << 0, "RoadGraph edge", property);
                        id = ReadNonNegativeInt32(reader, "RoadGraph edge id");
                        break;
                    case "nodeAID":
                        MarkField(ref seen, 1UL << 1, "RoadGraph edge", property);
                        nodeAID = ReadNonNegativeInt32(reader, "RoadGraph edge nodeAID");
                        break;
                    case "nodeBID":
                        MarkField(ref seen, 1UL << 2, "RoadGraph edge", property);
                        nodeBID = ReadNonNegativeInt32(reader, "RoadGraph edge nodeBID");
                        break;
                    case "roadType":
                        MarkField(ref seen, 1UL << 3, "RoadGraph edge", property);
                        if (!RoadTypeContract.TryParseStorageToken(
                                reader.GetString("RoadGraph edge roadType"),
                                out roadType))
                        {
                            throw new JsonException("RoadGraph edge roadType is invalid.");
                        }
                        break;
                    case "geometry":
                        MarkField(ref seen, 1UL << 4, "RoadGraph edge", property);
                        geometry = ReadGeometryArray(
                            reader,
                            capacity,
                            allocation,
                            ref totalGeometryCount);
                        break;
                    default:
                        throw new JsonException($"RoadGraph edge contains unknown property '{property}'.");
                }
            }
            RequireFields(seen, 0b1_1111, "RoadGraph edge");
            if (id <= previousID)
                throw new JsonException("RoadGraph edges must use unique, ascending IDs.");
            previousID = id;
            if (nodeAID != nodeBID && nodeAID > nodeBID)
                throw new JsonException($"Edge {id} is not stored in canonical endpoint order.");
            ValidateGeometryChain(id, nodeAID, nodeBID, geometry!);
            edges.Add(new PreparedRoadEdge(roadType, id, nodeAID, nodeBID, geometry!));
        }
        throw new JsonException("RoadGraph edges array is incomplete.");
    }

    private static List<RoadGeometrySegment> ReadGeometryArray(
        V3JsonStreamReader reader,
        RoadGraphCapacity capacity,
        PreparedAllocationTracker allocation,
        ref long totalGeometryCount)
    {
        reader.Expect(JsonTokenType.StartArray, "RoadGraph edge geometry");
        var geometry = new List<RoadGeometrySegment>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                if (geometry.Count == 0)
                    throw new JsonException("RoadGraph edge must contain geometry.");
                return geometry;
            }
            if (geometry.Count >= capacity.MaximumGeometrySegmentsPerEdge)
                throw new JsonException("RoadGraph per-edge geometry capacity is exceeded.");
            if (totalGeometryCount >= capacity.MaximumGeometrySegments)
                throw new JsonException("RoadGraph geometry capacity is exceeded.");
            allocation.ReserveGeometry();
            reader.Expect(JsonTokenType.StartObject, "RoadGraph geometry");
            geometry.Add(ReadGeometry(reader, geometry.Count));
            totalGeometryCount++;
        }
        throw new JsonException("RoadGraph geometry array is incomplete.");
    }

    private static RoadGeometrySegment ReadGeometry(V3JsonStreamReader reader, int index)
    {
        string context = $"RoadGraph geometry {index}";
        ulong seen = 0;
        int version = 0;
        string? kind = null;
        Vector2 start = default;
        Vector2 end = default;
        Vector2 control1 = default;
        Vector2 control2 = default;
        Vector2 startTangent = default;
        Vector2 endTangent = default;
        Vector2 center = default;
        float radius = 0;
        float startAngle = 0;
        float endAngle = 0;
        float sweepAngle = 0;
        float startHeading = 0;
        float reverseStartHeading = 0;
        float startCurvature = 0;
        float endCurvature = 0;
        float arcLength = 0;
        float startWeight = 0;
        float controlWeight = 0;
        float endWeight = 0;

        while (ReadObjectProperty(reader, context, out string property))
        {
            reader.ReadRequired($"{context} property '{property}'");
            switch (property)
            {
                case "version": MarkField(ref seen, GeometryVersion, context, property); version = ReadNonNegativeInt32(reader, $"{context} version"); break;
                case "kind": MarkField(ref seen, GeometryKind, context, property); kind = reader.GetString($"{context} kind"); break;
                case "start": MarkField(ref seen, GeometryStart, context, property); start = ReadPoint(reader, $"{context} start"); break;
                case "end": MarkField(ref seen, GeometryEnd, context, property); end = ReadPoint(reader, $"{context} end"); break;
                case "control1": MarkField(ref seen, GeometryControl1, context, property); control1 = ReadPoint(reader, $"{context} control1"); break;
                case "control2": MarkField(ref seen, GeometryControl2, context, property); control2 = ReadPoint(reader, $"{context} control2"); break;
                case "startTangent": MarkField(ref seen, GeometryStartTangent, context, property); startTangent = ReadPoint(reader, $"{context} startTangent"); break;
                case "endTangent": MarkField(ref seen, GeometryEndTangent, context, property); endTangent = ReadPoint(reader, $"{context} endTangent"); break;
                case "center": MarkField(ref seen, GeometryCenter, context, property); center = ReadPoint(reader, $"{context} center"); break;
                case "radius": MarkField(ref seen, GeometryRadius, context, property); radius = ReadFiniteSingle(reader, $"{context} radius"); break;
                case "startAngle": MarkField(ref seen, GeometryStartAngle, context, property); startAngle = ReadFiniteSingle(reader, $"{context} startAngle"); break;
                case "endAngle": MarkField(ref seen, GeometryEndAngle, context, property); endAngle = ReadFiniteSingle(reader, $"{context} endAngle"); break;
                case "sweepAngle": MarkField(ref seen, GeometrySweepAngle, context, property); sweepAngle = ReadFiniteSingle(reader, $"{context} sweepAngle"); break;
                case "startHeading": MarkField(ref seen, GeometryStartHeading, context, property); startHeading = ReadFiniteSingle(reader, $"{context} startHeading"); break;
                case "reverseStartHeading": MarkField(ref seen, GeometryReverseStartHeading, context, property); reverseStartHeading = ReadFiniteSingle(reader, $"{context} reverseStartHeading"); break;
                case "startCurvature": MarkField(ref seen, GeometryStartCurvature, context, property); startCurvature = ReadFiniteSingle(reader, $"{context} startCurvature"); break;
                case "endCurvature": MarkField(ref seen, GeometryEndCurvature, context, property); endCurvature = ReadFiniteSingle(reader, $"{context} endCurvature"); break;
                case "arcLength": MarkField(ref seen, GeometryArcLength, context, property); arcLength = ReadFiniteSingle(reader, $"{context} arcLength"); break;
                case "startWeight": MarkField(ref seen, GeometryStartWeight, context, property); startWeight = ReadFiniteSingle(reader, $"{context} startWeight"); break;
                case "controlWeight": MarkField(ref seen, GeometryControlWeight, context, property); controlWeight = ReadFiniteSingle(reader, $"{context} controlWeight"); break;
                case "endWeight": MarkField(ref seen, GeometryEndWeight, context, property); endWeight = ReadFiniteSingle(reader, $"{context} endWeight"); break;
                default: throw new JsonException($"{context} contains unknown property '{property}'.");
            }
        }

        if (version != 1)
            throw new JsonException($"{context} version is not supported.");
        RoadGeometrySegment result = kind switch
        {
            "line" => CreateLine(),
            "cubicBezier" => CreateCubicBezier(),
            "cubicHermite" => CreateCubicHermite(),
            "circularArc" => CreateCircularArc(),
            "clothoid" => CreateClothoid(),
            "rationalQuadratic" => CreateRationalQuadratic(),
            null => throw new JsonException($"{context} is missing its kind."),
            _ => throw new JsonException($"{context} kind '{kind}' is not supported."),
        };
        if (RoadGeometryCanonicalizer.CanonicalizeSegment(result, out bool changed) is null || changed)
            throw new JsonException($"{context} contains non-canonical numeric values.");
        return result;

        LineRoadGeometrySegment CreateLine()
        {
            RequireFields(seen, GeometryVersion | GeometryKind | GeometryStart | GeometryEnd, context);
            return new LineRoadGeometrySegment(start, end);
        }

        CubicBezierRoadGeometrySegment CreateCubicBezier()
        {
            RequireFields(seen, GeometryVersion | GeometryKind | GeometryStart | GeometryControl1 | GeometryControl2 | GeometryEnd, context);
            return new CubicBezierRoadGeometrySegment(start, control1, control2, end);
        }

        CubicHermiteRoadGeometrySegment CreateCubicHermite()
        {
            RequireFields(seen, GeometryVersion | GeometryKind | GeometryStart | GeometryStartTangent | GeometryEnd | GeometryEndTangent, context);
            return new CubicHermiteRoadGeometrySegment(start, startTangent, end, endTangent);
        }

        CircularArcRoadGeometrySegment CreateCircularArc()
        {
            RequireFields(seen, GeometryVersion | GeometryKind | GeometryStart | GeometryEnd | GeometryCenter | GeometryRadius | GeometryStartAngle | GeometryEndAngle | GeometrySweepAngle, context);
            return CircularArcRoadGeometrySegment.CreateAnchored(center, radius, startAngle, sweepAngle, start, end, endAngle);
        }

        ClothoidRoadGeometrySegment CreateClothoid()
        {
            RequireFields(seen, GeometryVersion | GeometryKind | GeometryStart | GeometryEnd | GeometryStartHeading | GeometryReverseStartHeading | GeometryStartCurvature | GeometryEndCurvature | GeometryArcLength, context);
            return ClothoidRoadGeometrySegment.CreateAnchored(start, startHeading, startCurvature, endCurvature, arcLength, end, reverseStartHeading);
        }

        RationalQuadraticRoadGeometrySegment CreateRationalQuadratic()
        {
            RequireFields(seen, GeometryVersion | GeometryKind | GeometryStart | GeometryStartWeight | GeometryControl1 | GeometryControlWeight | GeometryEnd | GeometryEndWeight, context);
            return new RationalQuadraticRoadGeometrySegment(start, startWeight, control1, controlWeight, end, endWeight);
        }
    }

    private static Vector2 ReadPoint(V3JsonStreamReader reader, string context)
    {
        reader.Expect(JsonTokenType.StartObject, context);
        ulong seen = 0;
        float x = 0;
        float y = 0;
        while (ReadObjectProperty(reader, context, out string property))
        {
            reader.ReadRequired($"{context} property '{property}'");
            switch (property)
            {
                case "x": MarkField(ref seen, 1UL << 0, context, property); x = ReadFiniteSingle(reader, $"{context} x"); break;
                case "y": MarkField(ref seen, 1UL << 1, context, property); y = ReadFiniteSingle(reader, $"{context} y"); break;
                default: throw new JsonException($"{context} contains unknown property '{property}'.");
            }
        }
        RequireFields(seen, 0b11, context);
        return new Vector2(x, y);
    }

    private static void ValidateGeometryChain(
        int edgeID,
        int nodeAID,
        int nodeBID,
        IReadOnlyList<RoadGeometrySegment> geometry)
    {
        for (int index = 1; index < geometry.Count; index++)
        {
            if (!RoadExactPredicates.SameBits(geometry[index - 1].End, geometry[index].Start))
                throw new JsonException($"Edge {edgeID} geometry is not bitwise continuous.");
        }
        RoadGeometryCanonicalizationResult canonical = RoadGeometryCanonicalizer.Canonicalize(geometry);
        if (canonical.Changed)
            throw new JsonException($"Edge {edgeID} geometry is not canonical.");
        if (nodeAID == nodeBID && RoadGeometryDirection.CompareCanonicalKeys(
                geometry,
                RoadGeometryDirection.ReverseChain(geometry)) > 0)
        {
            throw new JsonException($"Self-loop edge {edgeID} does not use its canonical direction.");
        }

        var edge = new GraphEdge(default, edgeID, nodeAID, nodeBID, geometry);
        if (edge.GeometrySegments.Count != geometry.Count ||
            edge.GeometrySegments.Where((segment, index) => !ReferenceEquals(segment, geometry[index])).Any())
        {
            throw new JsonException($"Edge {edgeID} required canonical repair.");
        }
    }

    private static void ValidatePreparedTopology(
        int nextID,
        IReadOnlyList<PreparedRoadNode> nodes,
        IReadOnlyList<PreparedRoadEdge> edges,
        RoadGraphCapacity capacity)
    {
        var nodePositions = nodes.ToDictionary(node => node.ID, node => node.Position);
        var allIDs = new HashSet<int>(nodePositions.Keys);
        foreach (PreparedRoadEdge edge in edges)
        {
            if (!allIDs.Add(edge.ID))
                throw new JsonException("RoadGraph uses duplicate global entity IDs.");
            if (!nodePositions.TryGetValue(edge.NodeAID, out Vector2 nodeA) ||
                !nodePositions.TryGetValue(edge.NodeBID, out Vector2 nodeB))
            {
                throw new JsonException($"Edge {edge.ID} references a missing endpoint node.");
            }
            if (!RoadExactPredicates.SameBits(edge.GeometrySegments[0].Start, nodeA) ||
                !RoadExactPredicates.SameBits(edge.GeometrySegments[^1].End, nodeB))
            {
                throw new JsonException($"Edge {edge.ID} geometry endpoints do not match its nodes.");
            }
            if (capacity.ValidateEdgeGeometryCount(edge.GeometrySegments.Count) != RoadGraphCapacityError.None)
                throw new JsonException($"Edge {edge.ID} exceeds its geometry capacity.");
        }
        if (allIDs.Count > 0 && nextID <= allIDs.Max())
            throw new JsonException("RoadGraph nextID must exceed every entity ID.");
        if (capacity.ValidatePreparedAllocation(nodes.Count, edges.Count, edges.Sum(edge => (long)edge.GeometrySegments.Count)) != RoadGraphCapacityError.None)
            throw new JsonException("RoadGraph prepared allocation budget is exceeded.");
    }

    private static bool ReadObjectProperty(
        V3JsonStreamReader reader,
        string context,
        out string property)
    {
        reader.ReadRequired(context);
        if (reader.TokenType == JsonTokenType.EndObject)
        {
            property = string.Empty;
            return false;
        }
        property = reader.GetPropertyName(context);
        return true;
    }

    private static int ReadNonNegativeInt32(V3JsonStreamReader reader, string context) =>
        V3Json.ReadNonNegativeInt32(reader.GetNumberToken(context), context);

    private static float ReadFiniteSingle(V3JsonStreamReader reader, string context) =>
        V3Json.ReadFiniteSingle(reader.GetNumberToken(context), context);

    private static void MarkField(ref ulong seen, ulong field, string context, string property)
    {
        if ((seen & field) != 0)
            throw new JsonException($"{context} contains duplicate property '{property}'.");
        seen |= field;
    }

    private static void RequireFields(ulong seen, ulong expected, string context)
    {
        if (seen != expected)
            throw new JsonException($"{context} does not contain exactly its required properties.");
    }

    private sealed class PreparedAllocationTracker(RoadGraphCapacity capacity)
    {
        private long _nodes;
        private long _edges;
        private long _geometry;

        internal void ReserveNode()
        {
            _nodes++;
            Validate();
        }

        internal void ReserveEdge()
        {
            _edges++;
            Validate();
        }

        internal void ReserveGeometry()
        {
            _geometry++;
            Validate();
        }

        private void Validate()
        {
            if (capacity.ValidatePreparedAllocation(_nodes, _edges, _geometry) !=
                RoadGraphCapacityError.None)
            {
                throw new JsonException("RoadGraph prepared allocation budget is exceeded.");
            }
        }
    }
}
