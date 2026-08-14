using Godot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

public class GraphEdge
{
    public int ID { get; }
    public int NodeA { get; }
    public int NodeB { get; }
    public RoadType RoadType { get; }

    private readonly RoadGeometrySegment[] _geometrySegments;
    private readonly ReadOnlyCollection<RoadGeometrySegment> _readOnlyGeometrySegments;

    /// <summary>保留类型与控制参数的权威原生几何段。</summary>
    public IReadOnlyList<RoadGeometrySegment> GeometrySegments => _readOnlyGeometrySegments;

    /// <summary>几何段之间的中间锚点（不含两端节点坐标）。</summary>
    private readonly Vector2[] _points;
    public Vector2[] Points => (Vector2[])_points.Clone();
    internal Vector2[] InternalPoints => _points;

    public float Length { get; }

    public GraphEdge(
        RoadType roadType,
        int id,
        int nodeA,
        int nodeB,
        IReadOnlyList<RoadGeometrySegment> geometrySegments)
    {
        ArgumentNullException.ThrowIfNull(geometrySegments);
        if (id < 0)
            throw new ArgumentOutOfRangeException(nameof(id));
        if (nodeA < 0)
            throw new ArgumentOutOfRangeException(nameof(nodeA));
        if (nodeB < 0)
            throw new ArgumentOutOfRangeException(nameof(nodeB));
        if (!RoadTypeContract.IsDefined(roadType))
            throw new ArgumentOutOfRangeException(nameof(roadType));
        if (geometrySegments.Count == 0)
            throw new ArgumentException("An edge must contain at least one geometry segment.", nameof(geometrySegments));

        var normalizedGeometry = new RoadGeometrySegment[geometrySegments.Count];
        for (int i = 0; i < normalizedGeometry.Length; i++)
        {
            if (geometrySegments[i] is null)
                throw new ArgumentException("Geometry segments cannot contain null.", nameof(geometrySegments));
            normalizedGeometry[i] = RoadGeometryCanonicalizer.CanonicalizeSegment(
                geometrySegments[i],
                out _);
            if (i > 0 && !RoadExactPredicates.SameBits(
                    normalizedGeometry[i - 1].End,
                    normalizedGeometry[i].Start))
                throw new ArgumentException("Geometry segments must form a continuous path.", nameof(geometrySegments));
        }

        normalizedGeometry = [.. RoadGeometryCanonicalizer.Canonicalize(normalizedGeometry).GeometrySegments];

        if (nodeA != nodeB && nodeA > nodeB)
        {
            (nodeA, nodeB) = (nodeB, nodeA);
            normalizedGeometry = [.. RoadGeometryDirection.ReverseChain(normalizedGeometry)];
        }
        else if (nodeA == nodeB)
        {
            if (!RoadExactPredicates.SameBits(normalizedGeometry[0].Start, normalizedGeometry[^1].End))
                throw new ArgumentException(
                    "A self-loop geometry chain must close exactly at its seam.",
                    nameof(geometrySegments));
            IReadOnlyList<RoadGeometrySegment> reversed =
                RoadGeometryDirection.ReverseChain(normalizedGeometry);
            if (RoadGeometryDirection.CompareCanonicalKeys(reversed, normalizedGeometry) < 0)
                normalizedGeometry = [.. reversed];
        }

        for (int index = 1; index < normalizedGeometry.Length; index++)
        {
            if (!RoadExactPredicates.SameBits(
                    normalizedGeometry[index - 1].End,
                    normalizedGeometry[index].Start))
            {
                throw new ArgumentException(
                    "Directed geometry segments must remain bitwise continuous.",
                    nameof(geometrySegments));
            }
        }

        _geometrySegments = normalizedGeometry;

        ID = id;
        NodeA = nodeA;
        NodeB = nodeB;
        RoadType = roadType;
        _readOnlyGeometrySegments = Array.AsReadOnly(_geometrySegments);
        _points = _geometrySegments.Take(_geometrySegments.Length - 1).Select(segment => segment.End).ToArray();
        Length = _geometrySegments.Sum(segment => segment.Length);
    }

    /// <summary>
    /// 返回完整路径：[NodeA.Position, ...Points, NodeB.Position]。
    /// </summary>
    public Vector2[] GetFullPath(Func<int, GraphNode?> getNode)
    {
        var nodeA = getNode(NodeA);
        var nodeB = getNode(NodeB);
        if (nodeA == null || nodeB == null)
            throw new InvalidOperationException(
                $"Edge {ID} cannot build a full path because endpoint nodes {NodeA} and {NodeB} must both exist.");

        var result = new Vector2[_points.Length + 2];
        result[0] = nodeA.Position;
        for (int i = 0; i < _points.Length; i++)
            result[i + 1] = _points[i];
        result[result.Length - 1] = nodeB.Position;
        return result;
    }
}
