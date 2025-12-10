using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

/// <summary>
/// 铁路网络节点类型
/// </summary>
public enum RailwayNodeType
{
    /// <summary>普通端点</summary>
    Endpoint,
    /// <summary>连接点（两条轨道相连）</summary>
    Connection,
    /// <summary>道岔（多条轨道可切换连接）</summary>
    Switch
}

/// <summary>
/// 轨道类型
/// </summary>
public enum TrackType
{
    /// <summary>正线 - 不停车通过的线路</summary>
    MainLine,
    /// <summary>到发线 - 用于列车停靠的线路</summary>
    ArrivalDeparture,
    /// <summary>正线兼到发线 - 既可通过又可停靠</summary>
    MainLineWithPlatform,
    /// <summary>渡线/连接线 - 连接不同轨道的斜向轨道</summary>
    Crossover
}

/// <summary>
/// 站场股道定义
/// </summary>
public class StationTrack
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>股道名称（如"1道"、"II道"）</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>股道类型</summary>
    [JsonPropertyName("trackType")]
    public TrackType TrackType { get; set; } = TrackType.ArrivalDeparture;

    /// <summary>关联的边ID列表（组成这条股道的边）</summary>
    [JsonPropertyName("edgeIds")]
    public List<string> EdgeIds { get; set; } = new();

    /// <summary>是否可办理客运</summary>
    [JsonPropertyName("canPassenger")]
    public bool CanPassenger { get; set; } = true;

    public StationTrack() { }

    public StationTrack(string id, string name, TrackType type = TrackType.ArrivalDeparture)
    {
        Id = id;
        Name = name;
        TrackType = type;
    }
}

/// <summary>
/// 站场定义
/// </summary>
public class Station
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>站名</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>站场边界框（左上角X）</summary>
    [JsonPropertyName("boundsX")]
    public float BoundsX { get; set; }

    /// <summary>站场边界框（左上角Y）</summary>
    [JsonPropertyName("boundsY")]
    public float BoundsY { get; set; }

    /// <summary>站场边界框宽度</summary>
    [JsonPropertyName("boundsWidth")]
    public float BoundsWidth { get; set; } = 200f;

    /// <summary>站场边界框高度</summary>
    [JsonPropertyName("boundsHeight")]
    public float BoundsHeight { get; set; } = 100f;

    /// <summary>站内股道</summary>
    [JsonPropertyName("tracks")]
    public List<StationTrack> Tracks { get; set; } = new();

    [JsonIgnore]
    public Rect2 Bounds => new Rect2(BoundsX, BoundsY, BoundsWidth, BoundsHeight);

    public Station() { }

    public Station(string id, string name, float x, float y, float width = 200f, float height = 100f)
    {
        Id = id;
        Name = name;
        BoundsX = x;
        BoundsY = y;
        BoundsWidth = width;
        BoundsHeight = height;
    }

    /// <summary>
    /// 检查节点是否在站场范围内
    /// </summary>
    public bool ContainsNode(RailwayNode node)
    {
        return Bounds.HasPoint(new Vector2(node.X, node.Y));
    }

    /// <summary>
    /// 获取股道
    /// </summary>
    public StationTrack GetTrack(string trackId)
    {
        return Tracks.Find(t => t.Id == trackId);
    }

    /// <summary>
    /// 添加股道
    /// </summary>
    public StationTrack AddTrack(string name, TrackType type = TrackType.ArrivalDeparture)
    {
        string id = $"{Id}_T{Tracks.Count + 1}";
        var track = new StationTrack(id, name, type);
        Tracks.Add(track);
        return track;
    }
}

/// <summary>
/// 铁路网络节点
/// </summary>
public class RailwayNode
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("type")]
    public RailwayNodeType Type { get; set; } = RailwayNodeType.Connection;

    /// <summary>道岔当前指向的边ID（仅当Type为Switch时使用）</summary>
    [JsonPropertyName("switchTarget")]
    public string SwitchTarget { get; set; } = "";

    [JsonIgnore]
    public Vector2 Position => new Vector2(X, Y);

    public RailwayNode() { }

    public RailwayNode(string id, float x, float y, RailwayNodeType type = RailwayNodeType.Connection)
    {
        Id = id;
        X = x;
        Y = y;
        Type = type;
    }

    public RailwayNode(string id, Vector2 pos, RailwayNodeType type = RailwayNodeType.Connection)
        : this(id, pos.X, pos.Y, type) { }
}

/// <summary>
/// 铁路网络边（轨道段）
/// </summary>
public class RailwayEdge
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("fromNode")]
    public string FromNode { get; set; }

    [JsonPropertyName("toNode")]
    public string ToNode { get; set; }

    /// <summary>轨道长度（自动计算或手动指定）</summary>
    [JsonPropertyName("length")]
    public float Length { get; set; }

    /// <summary>轨道类型</summary>
    [JsonPropertyName("trackType")]
    public TrackType TrackType { get; set; } = TrackType.ArrivalDeparture;

    /// <summary>轨道编号（用于显示，如"18道"）</summary>
    [JsonPropertyName("trackNumber")]
    public int TrackNumber { get; set; } = 0;

    /// <summary>限速（km/h，0表示无限制）</summary>
    [JsonPropertyName("speedLimit")]
    public float SpeedLimit { get; set; } = 0f;

    // 兼容旧属性（JSON反序列化用）
    [JsonPropertyName("isMainLine")]
    public bool IsMainLine
    {
        get => TrackType == TrackType.MainLine || TrackType == TrackType.MainLineWithPlatform;
        set { if (value && TrackType == TrackType.ArrivalDeparture) TrackType = TrackType.MainLine; }
    }

    [JsonPropertyName("isCrossover")]
    public bool IsCrossover
    {
        get => TrackType == TrackType.Crossover;
        set { if (value) TrackType = TrackType.Crossover; }
    }

    public RailwayEdge() { }

    public RailwayEdge(string id, string fromNode, string toNode, float length = 0f)
    {
        Id = id;
        FromNode = fromNode;
        ToNode = toNode;
        Length = length;
    }
}

/// <summary>
/// 铁路网络 - 表示完整的轨道拓扑结构，支持寻路
/// </summary>
public class RailwayNetwork
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "新网络";

    [JsonPropertyName("nodes")]
    public List<RailwayNode> Nodes { get; set; } = new();

    [JsonPropertyName("edges")]
    public List<RailwayEdge> Edges { get; set; } = new();

    [JsonPropertyName("stations")]
    public List<Station> Stations { get; set; } = new();

    // 运行时索引（不序列化）
    [JsonIgnore]
    private Dictionary<string, RailwayNode> nodeIndex = new();
    [JsonIgnore]
    private Dictionary<string, RailwayEdge> edgeIndex = new();
    [JsonIgnore]
    private Dictionary<string, List<string>> adjacencyList = new();
    [JsonIgnore]
    private Dictionary<string, Station> stationIndex = new();

    /// <summary>
    /// 构建索引（加载后调用）
    /// </summary>
    public void BuildIndex()
    {
        nodeIndex.Clear();
        edgeIndex.Clear();
        adjacencyList.Clear();
        stationIndex.Clear();

        foreach (var node in Nodes)
        {
            nodeIndex[node.Id] = node;
            adjacencyList[node.Id] = new List<string>();
        }

        foreach (var edge in Edges)
        {
            edgeIndex[edge.Id] = edge;
            if (adjacencyList.ContainsKey(edge.FromNode))
                adjacencyList[edge.FromNode].Add(edge.Id);
            if (adjacencyList.ContainsKey(edge.ToNode))
                adjacencyList[edge.ToNode].Add(edge.Id);
        }

        foreach (var station in Stations)
        {
            stationIndex[station.Id] = station;
        }
    }

    /// <summary>
    /// 获取节点
    /// </summary>
    public RailwayNode GetNode(string id) => nodeIndex.TryGetValue(id, out var node) ? node : null;

    /// <summary>
    /// 获取边
    /// </summary>
    public RailwayEdge GetEdge(string id) => edgeIndex.TryGetValue(id, out var edge) ? edge : null;

    /// <summary>
    /// 获取节点连接的所有边ID
    /// </summary>
    public List<string> GetConnectedEdges(string nodeId) =>
        adjacencyList.TryGetValue(nodeId, out var edges) ? edges : new List<string>();

    /// <summary>
    /// 添加节点
    /// </summary>
    public RailwayNode AddNode(float x, float y, RailwayNodeType type = RailwayNodeType.Connection)
    {
        string id = $"N{Nodes.Count + 1}_{DateTime.Now.Ticks % 10000}";
        var node = new RailwayNode(id, x, y, type);
        Nodes.Add(node);
        nodeIndex[id] = node;
        adjacencyList[id] = new List<string>();
        return node;
    }

    /// <summary>
    /// 添加边
    /// </summary>
    public RailwayEdge AddEdge(string fromNodeId, string toNodeId, bool isMainLine = false, bool isCrossover = false)
    {
        var fromNode = GetNode(fromNodeId);
        var toNode = GetNode(toNodeId);
        if (fromNode == null || toNode == null) return null;

        string id = $"E{Edges.Count + 1}_{DateTime.Now.Ticks % 10000}";
        float length = fromNode.Position.DistanceTo(toNode.Position);

        var edge = new RailwayEdge(id, fromNodeId, toNodeId, length)
        {
            IsMainLine = isMainLine,
            IsCrossover = isCrossover
        };

        Edges.Add(edge);
        edgeIndex[id] = edge;
        adjacencyList[fromNodeId].Add(id);
        adjacencyList[toNodeId].Add(id);

        return edge;
    }

    /// <summary>
    /// 删除节点（同时删除相关边）
    /// </summary>
    public void RemoveNode(string nodeId)
    {
        var node = GetNode(nodeId);
        if (node == null) return;

        // 删除相关边
        var edgesToRemove = new List<string>(GetConnectedEdges(nodeId));
        foreach (var edgeId in edgesToRemove)
        {
            RemoveEdge(edgeId);
        }

        Nodes.Remove(node);
        nodeIndex.Remove(nodeId);
        adjacencyList.Remove(nodeId);
    }

    /// <summary>
    /// 删除边
    /// </summary>
    public void RemoveEdge(string edgeId)
    {
        var edge = GetEdge(edgeId);
        if (edge == null) return;

        if (adjacencyList.ContainsKey(edge.FromNode))
            adjacencyList[edge.FromNode].Remove(edgeId);
        if (adjacencyList.ContainsKey(edge.ToNode))
            adjacencyList[edge.ToNode].Remove(edgeId);

        Edges.Remove(edge);
        edgeIndex.Remove(edgeId);
    }

    /// <summary>
    /// 获取站场
    /// </summary>
    public Station GetStation(string id) => stationIndex.TryGetValue(id, out var station) ? station : null;

    /// <summary>
    /// 添加站场
    /// </summary>
    public Station AddStation(string name, float x, float y, float width, float height)
    {
        string id = $"S{Stations.Count + 1}_{DateTime.Now.Ticks % 10000}";
        var station = new Station
        {
            Id = id,
            Name = name,
            BoundsX = x,
            BoundsY = y,
            BoundsWidth = width,
            BoundsHeight = height
        };
        Stations.Add(station);
        stationIndex[id] = station;
        return station;
    }

    /// <summary>
    /// 删除站场
    /// </summary>
    public void RemoveStation(string stationId)
    {
        var station = GetStation(stationId);
        if (station == null) return;

        Stations.Remove(station);
        stationIndex.Remove(stationId);
    }

    /// <summary>
    /// 查找包含指定节点的站场
    /// </summary>
    public Station FindStationContainingNode(string nodeId)
    {
        var node = GetNode(nodeId);
        if (node == null) return null;

        foreach (var station in Stations)
        {
            if (station.ContainsNode(node))
                return station;
        }
        return null;
    }

    /// <summary>
    /// 查找包含指定边的站场（两端节点都在站场内）
    /// </summary>
    public Station FindStationContainingEdge(string edgeId)
    {
        var edge = GetEdge(edgeId);
        if (edge == null) return null;

        var fromNode = GetNode(edge.FromNode);
        var toNode = GetNode(edge.ToNode);
        if (fromNode == null || toNode == null) return null;

        foreach (var station in Stations)
        {
            if (station.ContainsNode(fromNode) && station.ContainsNode(toNode))
                return station;
        }
        return null;
    }

    /// <summary>
    /// 获取站场内的所有节点
    /// </summary>
    public List<RailwayNode> GetNodesInStation(string stationId)
    {
        var station = GetStation(stationId);
        if (station == null) return new List<RailwayNode>();

        return Nodes.Where(n => station.ContainsNode(n)).ToList();
    }

    /// <summary>
    /// 获取站场内的所有边（两端节点都在站场内）
    /// </summary>
    public List<RailwayEdge> GetEdgesInStation(string stationId)
    {
        var station = GetStation(stationId);
        if (station == null) return new List<RailwayEdge>();

        var nodesInStation = new HashSet<string>(GetNodesInStation(stationId).Select(n => n.Id));
        return Edges.Where(e => nodesInStation.Contains(e.FromNode) && nodesInStation.Contains(e.ToNode)).ToList();
    }

    /// <summary>
    /// 寻路：从起点到终点的最短路径（Dijkstra算法）
    /// 返回边ID列表
    /// </summary>
    public List<string> FindPath(string startNodeId, string endNodeId)
    {
        if (!nodeIndex.ContainsKey(startNodeId) || !nodeIndex.ContainsKey(endNodeId))
            return null;

        var distances = new Dictionary<string, float>();
        var previous = new Dictionary<string, (string nodeId, string edgeId)>();
        var unvisited = new HashSet<string>();

        foreach (var node in Nodes)
        {
            distances[node.Id] = float.MaxValue;
            unvisited.Add(node.Id);
        }
        distances[startNodeId] = 0;

        while (unvisited.Count > 0)
        {
            // 找最小距离节点
            string current = null;
            float minDist = float.MaxValue;
            foreach (var nodeId in unvisited)
            {
                if (distances[nodeId] < minDist)
                {
                    minDist = distances[nodeId];
                    current = nodeId;
                }
            }

            if (current == null || current == endNodeId)
                break;

            unvisited.Remove(current);

            // 更新邻居
            foreach (var edgeId in GetConnectedEdges(current))
            {
                var edge = GetEdge(edgeId);
                string neighbor = edge.FromNode == current ? edge.ToNode : edge.FromNode;

                if (!unvisited.Contains(neighbor)) continue;

                float newDist = distances[current] + edge.Length;
                if (newDist < distances[neighbor])
                {
                    distances[neighbor] = newDist;
                    previous[neighbor] = (current, edgeId);
                }
            }
        }

        // 重建路径
        if (!previous.ContainsKey(endNodeId) && startNodeId != endNodeId)
            return null;

        var path = new List<string>();
        string currentNode = endNodeId;
        while (previous.ContainsKey(currentNode))
        {
            path.Insert(0, previous[currentNode].edgeId);
            currentNode = previous[currentNode].nodeId;
        }

        return path;
    }

    /// <summary>
    /// 从JSON文件加载
    /// </summary>
    public static RailwayNetwork LoadFromFile(string path)
    {
        string absolutePath = ProjectSettings.GlobalizePath(path);
        string json = System.IO.File.ReadAllText(absolutePath);
        var network = JsonSerializer.Deserialize<RailwayNetwork>(json, GetJsonOptions());
        network?.BuildIndex();
        return network;
    }

    /// <summary>
    /// 保存到JSON文件
    /// </summary>
    public void SaveToFile(string path)
    {
        string absolutePath = ProjectSettings.GlobalizePath(path);
        // 确保目录存在
        string dir = System.IO.Path.GetDirectoryName(absolutePath);
        if (!System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        string json = JsonSerializer.Serialize(this, GetJsonOptions());
        System.IO.File.WriteAllText(absolutePath, json);
    }

    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }
}
