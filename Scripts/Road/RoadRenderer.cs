using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class RoadRenderer : Node2D
{
    [Export] public RoadConfig Config { get; set; } = null!;

    private RoadGraph? _network;

    // Edge.ID → 确定显示点列；静态道路和动态高亮共用。
    private readonly Dictionary<int, Vector2[]> _edgePoints = new();

    private MeshInstance2D _roadBatchLayer = null!;
    private MultiMeshInstance2D _nodeBatchLayer = null!;
    private int _roadMeshVertexCount;
    private bool _staticBatchRebuildScheduled;
    private bool _graphEventsSubscribed;

    // 施工预览
    private Vector2[] _previewPoints = [];
    public Vector2[] PreviewPoints
    {
        get => (Vector2[])_previewPoints.Clone();
        set => _previewPoints = value == null ? [] : (Vector2[])value.Clone();
    }

    public int GetPreviewPointCount() => _previewPoints.Length;

    public Vector2 GetPreviewPoint(int index) => _previewPoints[index];

    private int[] _removalPreviewEdgeIDs = [];
    public int[] RemovalPreviewEdgeIDs
    {
        get => (int[])_removalPreviewEdgeIDs.Clone();
        set => _removalPreviewEdgeIDs = value == null ? [] : value.Distinct().Order().ToArray();
    }

    public Rect2? RemovalSelectionBounds { get; set; }

    public int GetRemovalPreviewEdgeCount() => _removalPreviewEdgeIDs.Length;

    public int GetRenderedEdgeCount() => _edgePoints.Count;

    public int GetRenderedPointCount(int edgeID) => _edgePoints[edgeID].Length;

    public Vector2 GetRenderedPoint(int edgeID, int pointIndex) => _edgePoints[edgeID][pointIndex];

    public int GetStaticRenderNodeCount() => 2;

    public int GetRoadMeshVertexCount() => _roadMeshVertexCount;

    /// <summary>拆除工具悬停的 Edge ID（null = 未悬停在任何 Edge 上）</summary>
    public int? HoveredEdgeID { get; set; }

    public override void _Ready()
    {
        if (Config == null)
        {
            GD.PushError("RoadRenderer: Config (RoadConfig resource) is not assigned in the scene.");
            Config = new RoadConfig();
        }
        Config.NormalizeRuntimeValues(message => GD.PushWarning($"RoadRenderer: {message}"));
        if (!float.IsFinite(Config.CurveDisplayTolerance) || Config.CurveDisplayTolerance <= 0f)
        {
            GD.PushError("RoadRenderer: CurveDisplayTolerance must be positive and finite; using the default.");
            Config.CurveDisplayTolerance = RoadGeometryDisplaySampler.DefaultTolerance;
        }

        _roadBatchLayer = new MeshInstance2D
        {
            ZIndex = 0,
            Modulate = Config.RoadColor,
            Material = CreateRoadMaterial(),
        };
        AddChild(_roadBatchLayer);

        _nodeBatchLayer = CreateBatchLayer(useColors: true, zIndex: 1);
        _nodeBatchLayer.Material = CreateCircleMaterial();
        AddChild(_nodeBatchLayer);
    }

    public override void _EnterTree()
    {
        SubscribeGraphEvents();
    }

    public override void _ExitTree()
    {
        UnsubscribeGraphEvents();
        _staticBatchRebuildScheduled = false;
    }

    public void SetGraph(RoadGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        UnsubscribeGraphEvents();
        _network = graph;
        _staticBatchRebuildScheduled = false;
        _edgePoints.Clear();
        foreach (GraphEdge edge in _network.GetAllEdges())
            CacheEdgePoints(edge);
        SubscribeGraphEvents();

        if (IsInsideTree() &&
            GodotObject.IsInstanceValid(_roadBatchLayer) &&
            GodotObject.IsInstanceValid(_nodeBatchLayer))
        {
            RebuildStaticBatches();
        }
    }

    private void SubscribeGraphEvents()
    {
        if (_network == null || _graphEventsSubscribed || !IsInsideTree())
            return;

        _network.EdgeAdded += OnEdgeAdded;
        _network.EdgeRemoved += OnEdgeRemoved;
        _network.GraphCleared += OnGraphCleared;
        _graphEventsSubscribed = true;
    }

    private void UnsubscribeGraphEvents()
    {
        if (_network == null || !_graphEventsSubscribed)
            return;

        _network.EdgeAdded -= OnEdgeAdded;
        _network.EdgeRemoved -= OnEdgeRemoved;
        _network.GraphCleared -= OnGraphCleared;
        _graphEventsSubscribed = false;
    }

    // ── 整网重载（存档加载后） ──

    private void OnGraphCleared()
    {
        _staticBatchRebuildScheduled = false;
        _edgePoints.Clear();

        if (_network == null) return;
        foreach (var edge in _network.GetAllEdges())
            CacheEdgePoints(edge);

        RebuildStaticBatches();
    }

    // ── Edge 增删 → 显示点缓存与静态批次同步 ──

    private void OnEdgeAdded(GraphEdge edge)
    {
        CacheEdgePoints(edge);
        ScheduleStaticBatchRebuild();
    }

    private void CacheEdgePoints(GraphEdge edge)
    {
        if (_network == null) return;

        _edgePoints[edge.ID] = RoadGeometryDisplaySampler.SampleSegments(
            edge.GeometrySegments,
            Config.CurveDisplayTolerance);
    }

    private void OnEdgeRemoved(GraphEdge edge)
    {
        _edgePoints.Remove(edge.ID);
        ScheduleStaticBatchRebuild();
    }

    // ── 静态道路和节点批处理 ──

    private void ScheduleStaticBatchRebuild()
    {
        if (_staticBatchRebuildScheduled)
            return;

        _staticBatchRebuildScheduled = true;
        Callable.From(FlushScheduledStaticBatchRebuild).CallDeferred();
    }

    private void FlushScheduledStaticBatchRebuild()
    {
        if (!_staticBatchRebuildScheduled)
            return;

        _staticBatchRebuildScheduled = false;
        if (IsInsideTree())
            RebuildStaticBatches();
    }

    private void RebuildStaticBatches()
    {
        if (_network == null) return;

        var roadVertices = new List<Vector2>();
        var roadUvs = new List<Vector2>();
        var roadIndices = new List<int>();
        foreach (Vector2[] points in _edgePoints.OrderBy(pair => pair.Key).Select(pair => pair.Value))
            AppendRoadRibbon(points, Config.RoadWidth * 0.5f, roadVertices, roadUvs, roadIndices);

        _roadMeshVertexCount = roadVertices.Count;
        _roadBatchLayer.Mesh = CreateRoadMesh(roadVertices, roadUvs, roadIndices);

        GraphNode[] nodes = _network.GetAllNodes()
            .Where(node => GetNodeMarkerRadius(
                _network,
                node,
                Config.EndpointRadius,
                Config.JunctionRadius) > 0f)
            .OrderBy(node => node.ID)
            .ToArray();
        MultiMesh nodeBatch = _nodeBatchLayer.Multimesh;
        nodeBatch.InstanceCount = nodes.Length;
        for (int index = 0; index < nodes.Length; index++)
        {
            GraphNode node = nodes[index];
            bool junction = IsJunctionNode(_network, node);
            float diameter = GetNodeMarkerRadius(
                _network,
                node,
                Config.EndpointRadius,
                Config.JunctionRadius) * 2f;
            var transform = new Transform2D(0f, node.Position)
                .ScaledLocal(new Vector2(diameter, diameter));
            nodeBatch.SetInstanceTransform2D(index, transform);
            nodeBatch.SetInstanceColor(index, junction ? Config.JunctionColor : Config.EndpointColor);
        }
    }

    private static void AppendRoadRibbon(
        IReadOnlyList<Vector2> points,
        float halfWidth,
        List<Vector2> vertices,
        List<Vector2> uvs,
        List<int> indices)
    {
        if (points.Count < 2)
            return;

        int vertexOffset = vertices.Count;
        for (int index = 0; index < points.Count; index++)
        {
            Vector2 offset = CalculateRoadOffset(points, index, halfWidth);
            vertices.Add(points[index] - offset);
            uvs.Add(Vector2.Zero);
            vertices.Add(points[index] + offset);
            uvs.Add(Vector2.Down);
        }

        for (int index = 1; index < points.Count; index++)
        {
            int previous = vertexOffset + (index - 1) * 2;
            int current = vertexOffset + index * 2;
            indices.Add(previous);
            indices.Add(previous + 1);
            indices.Add(current);
            indices.Add(current);
            indices.Add(previous + 1);
            indices.Add(current + 1);
        }
    }

    private static Vector2 CalculateRoadOffset(IReadOnlyList<Vector2> points, int index, float halfWidth)
    {
        Vector2 previousDirection = index == 0
            ? Vector2.Zero
            : (points[index] - points[index - 1]).Normalized();
        Vector2 nextDirection = index == points.Count - 1
            ? Vector2.Zero
            : (points[index + 1] - points[index]).Normalized();
        if (previousDirection.IsZeroApprox())
            previousDirection = nextDirection;
        if (nextDirection.IsZeroApprox())
            nextDirection = previousDirection;
        if (previousDirection.IsZeroApprox())
            return Vector2.Zero;

        var previousNormal = new Vector2(-previousDirection.Y, previousDirection.X);
        var nextNormal = new Vector2(-nextDirection.Y, nextDirection.X);
        Vector2 miter = (previousNormal + nextNormal).Normalized();
        float denominator = miter.Dot(nextNormal);
        if (miter.IsZeroApprox() || Mathf.Abs(denominator) < 0.25f)
            return nextNormal * halfWidth;

        float miterLength = Mathf.Clamp(halfWidth / denominator, -halfWidth * 4f, halfWidth * 4f);
        return miter * miterLength;
    }

    private static ArrayMesh? CreateRoadMesh(
        IReadOnlyCollection<Vector2> vertices,
        IReadOnlyCollection<Vector2> uvs,
        IReadOnlyCollection<int> indices)
    {
        if (vertices.Count == 0)
            return null;

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    private static MultiMeshInstance2D CreateBatchLayer(bool useColors, int zIndex)
    {
        var batch = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
            UseColors = useColors,
            Mesh = new QuadMesh { Size = Vector2.One },
        };
        return new MultiMeshInstance2D
        {
            Multimesh = batch,
            ZIndex = zIndex,
        };
    }

    private static ShaderMaterial CreateCircleMaterial()
    {
        var shader = new Shader
        {
            Code = """
                shader_type canvas_item;

                void fragment() {
                    vec2 centered = UV * 2.0 - 1.0;
                    float distance_to_center = length(centered);
                    float antialias_width = fwidth(distance_to_center);
                    float coverage = 1.0 - smoothstep(1.0 - antialias_width, 1.0, distance_to_center);
                    COLOR.a *= coverage;
                }
                """,
        };
        return new ShaderMaterial { Shader = shader };
    }

    private static ShaderMaterial CreateRoadMaterial()
    {
        var shader = new Shader
        {
            Code = """
                shader_type canvas_item;

                void fragment() {
                    float edge_distance = abs(UV.y - 0.5);
                    float antialias_width = fwidth(edge_distance);
                    float coverage = 1.0 - smoothstep(0.5 - antialias_width, 0.5, edge_distance);
                    COLOR.a *= coverage;
                }
                """,
        };
        return new ShaderMaterial { Shader = shader };
    }

    // ── RoadRenderer._Draw() 只画施工预览 ──

    public override void _Draw()
    {
        foreach (int edgeID in _removalPreviewEdgeIDs)
            DrawEdgeHighlight(edgeID);

        if (RemovalSelectionBounds is { } bounds && bounds.Size.X > 0f && bounds.Size.Y > 0f)
            DrawRect(bounds, Config.HoverHighlightColor, false, 2f);

        if (HoveredEdgeID.HasValue && _network != null)
            DrawEdgeHighlight(HoveredEdgeID.Value);

        for (int index = 1; index < _previewPoints.Length; index++)
        {
            Vector2 from = _previewPoints[index - 1];
            Vector2 to = _previewPoints[index];
            if (from != to)
                DrawDashedLine(from, to, new Color(1, 1, 1, 0.5f));
        }
    }

    private void DrawEdgeHighlight(int edgeID)
    {
        GraphEdge? edge = _network?.GetEdge(edgeID);
        if (edge == null || _network == null)
            return;

        GraphNode? nodeA = _network.GetNode(edge.NodeA);
        GraphNode? nodeB = _network.GetNode(edge.NodeB);
        if (nodeA == null || nodeB == null)
            return;

        if (!_edgePoints.TryGetValue(edgeID, out Vector2[]? points))
            return;

        DrawPolyline(points, Config.HoverHighlightColor, Config.HoverHighlightWidth);
        DrawNodeHighlight(nodeA);
        DrawNodeHighlight(nodeB);
    }

    private void DrawNodeHighlight(GraphNode node)
    {
        if (_network == null)
            return;

        float radius = GetNodeMarkerRadius(
            _network,
            node,
            Config.EndpointRadius,
            Config.JunctionRadius);
        if (radius > 0f)
            DrawCircle(node.Position, radius * 1.3f, Config.HoverHighlightColor);
    }

    internal static float GetNodeMarkerRadius(
        RoadGraph graph,
        GraphNode node,
        float endpointRadius,
        float junctionRadius)
    {
        if (node.EdgeCount == 1)
            return endpointRadius;
        return IsJunctionNode(graph, node) ? junctionRadius : 0f;
    }

    internal static bool IsJunctionNode(RoadGraph graph, GraphNode node)
    {
        if (node.EdgeCount >= 3)
            return true;
        if (node.EdgeCount != 2)
            return false;

        if (!TryGetOutgoingDirection(graph, node, node.Edges[0], out Vector2 first) ||
            !TryGetOutgoingDirection(graph, node, node.Edges[1], out Vector2 second))
        {
            return true;
        }

        return first.Dot(second) > -0.999f;
    }

    private static bool TryGetOutgoingDirection(
        RoadGraph graph,
        GraphNode node,
        EdgeRef edgeRef,
        out Vector2 direction)
    {
        direction = Vector2.Zero;
        GraphEdge? edge = graph.GetEdge(edgeRef.EdgeID);
        if (edge == null)
            return false;

        if (edge.NodeA == node.ID)
            direction = edge.GeometrySegments[0].GetUnitTangent(0f);
        else if (edge.NodeB == node.ID)
            direction = -edge.GeometrySegments[^1].GetUnitTangent(1f);
        else
            return false;

        return direction.IsFinite() && !direction.IsZeroApprox();
    }

    // ── 虚线工具 ──

    private void DrawDashedLine(Vector2 from, Vector2 to, Color color, float width = 2f, float dashLength = 6f)
    {
        Vector2 delta = to - from;
        float total = delta.Length();
        if (total < 0.01f) return;

        Vector2 dir = delta / total;
        float drawn = 0f;
        bool draw = true;

        while (drawn < total)
        {
            float seg = Mathf.Min(dashLength, total - drawn);
            if (draw)
            {
                Vector2 start = from + dir * drawn;
                Vector2 end = start + dir * seg;
                DrawLine(start, end, color, width);
            }
            drawn += seg;
            draw = !draw;
        }
    }
}
