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
        return FindPath(startNodeId, endNodeId, null);
    }

    /// <summary>
    /// 寻路：从起点到终点的路径，支持正线靠左行驶规则
    /// </summary>
    /// <param name="startNodeId">起点节点ID</param>
    /// <param name="endNodeId">终点节点ID</param>
    /// <param name="isDownbound">行驶方向：true=下行（X增大），false=上行（X减小），null=不考虑方向</param>
    /// <returns>边ID列表，如果找不到路径返回null</returns>
    public List<string> FindPath(string startNodeId, string endNodeId, bool? isDownbound)
    {
        if (!nodeIndex.ContainsKey(startNodeId) || !nodeIndex.ContainsKey(endNodeId))
            return null;

        var startNode = GetNode(startNodeId);
        var endNode = GetNode(endNodeId);

        // 如果没有指定方向，根据起点终点X坐标判断
        bool goingDownbound = isDownbound ?? (endNode.X > startNode.X);

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

                // 计算带权重的距离（考虑轨道类型和行驶方向）
                float edgeCost = CalculateEdgeCost(edge, current, neighbor, goingDownbound);
                float newDist = distances[current] + edgeCost;

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
    /// 计算边的寻路代价（考虑轨道类型和靠左行驶规则）
    /// </summary>
    /// <param name="edge">边</param>
    /// <param name="fromNodeId">当前节点</param>
    /// <param name="toNodeId">目标节点</param>
    /// <param name="isDownbound">是否下行</param>
    /// <returns>边的代价</returns>
    private float CalculateEdgeCost(RailwayEdge edge, string fromNodeId, string toNodeId, bool isDownbound)
    {
        float baseCost = edge.Length;

        // 获取节点位置用于判断轨道位置
        var fromNode = GetNode(fromNodeId);
        var toNode = GetNode(toNodeId);

        // 正线/正线带站台优先
        bool isMainLine = edge.TrackType == TrackType.MainLine ||
                          edge.TrackType == TrackType.MainLineWithPlatform;

        // 渡线惩罚（不优先使用渡线）
        if (edge.TrackType == TrackType.Crossover)
        {
            baseCost *= 2.0f; // 渡线代价翻倍
        }

        // 实现靠左行驶规则
        // 在中国铁路中：
        // - 下行（X增大方向）应走Y坐标较大的正线（II道/下行正线）
        // - 上行（X减小方向）应走Y坐标较小的正线（I道/上行正线）
        if (isMainLine)
        {
            // 计算边的平均Y坐标
            float avgY = (fromNode.Y + toNode.Y) / 2;

            // 判断是否是正确方向的正线
            // 查找同一区间的其他正线进行比较
            bool isPreferredMainLine = IsPreferredMainLineForDirection(edge, avgY, isDownbound);

            if (isPreferredMainLine)
            {
                baseCost *= 0.5f; // 正确方向的正线代价减半（优先选择）
            }
            else
            {
                baseCost *= 1.5f; // 逆向正线代价增加
            }
        }

        return baseCost;
    }

    /// <summary>
    /// 判断边是否是指定方向的首选正线
    /// </summary>
    /// <param name="edge">当前边</param>
    /// <param name="avgY">边的平均Y坐标</param>
    /// <param name="isDownbound">是否下行</param>
    /// <returns>是否是首选正线</returns>
    private bool IsPreferredMainLineForDirection(RailwayEdge edge, float avgY, bool isDownbound)
    {
        // 获取边两端节点
        var fromNode = GetNode(edge.FromNode);
        var toNode = GetNode(edge.ToNode);

        // 计算边的中点X坐标
        float midX = (fromNode.X + toNode.X) / 2;

        // 查找同一X范围内的其他正线
        float searchRadius = 100f; // 搜索半径
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        foreach (var otherEdge in Edges)
        {
            if (otherEdge.TrackType != TrackType.MainLine &&
                otherEdge.TrackType != TrackType.MainLineWithPlatform)
                continue;

            var otherFrom = GetNode(otherEdge.FromNode);
            var otherTo = GetNode(otherEdge.ToNode);
            if (otherFrom == null || otherTo == null) continue;

            float otherMidX = (otherFrom.X + otherTo.X) / 2;

            // 只比较同一区间的正线
            if (Math.Abs(otherMidX - midX) < searchRadius)
            {
                float otherAvgY = (otherFrom.Y + otherTo.Y) / 2;
                minY = Math.Min(minY, otherAvgY);
                maxY = Math.Max(maxY, otherAvgY);
            }
        }

        // 如果只有一条正线，则它是首选
        if (Math.Abs(maxY - minY) < 0.1f)
            return true;

        // 靠左行驶规则：
        // 下行（X增大）走Y较大的线（II道）
        // 上行（X减小）走Y较小的线（I道）
        if (isDownbound)
        {
            // 下行走较大Y的线
            return avgY >= (minY + maxY) / 2;
        }
        else
        {
            // 上行走较小Y的线
            return avgY <= (minY + maxY) / 2;
        }
    }

    /// <summary>
    /// 高级寻路：支持指定停靠站台/股道
    /// </summary>
    /// <param name="startNodeId">起点节点ID</param>
    /// <param name="endNodeId">终点节点ID</param>
    /// <param name="isDownbound">是否下行</param>
    /// <param name="preferredTrackIds">首选股道ID列表（按优先级排序）</param>
    /// <returns>边ID列表</returns>
    public List<string> FindPathWithTrackPreference(
        string startNodeId,
        string endNodeId,
        bool isDownbound,
        List<string> preferredTrackIds = null)
    {
        // 基础寻路
        var path = FindPath(startNodeId, endNodeId, isDownbound);

        // 如果没有指定首选股道，直接返回
        if (preferredTrackIds == null || preferredTrackIds.Count == 0)
            return path;

        // TODO: 实现股道偏好逻辑（在站场内优先选择指定股道）
        // 当前版本先返回基础路径

        return path;
    }

    /// <summary>
    /// 获取路径的总长度
    /// </summary>
    /// <param name="edgeIds">边ID列表</param>
    /// <returns>总长度</returns>
    public float GetPathLength(List<string> edgeIds)
    {
        if (edgeIds == null) return 0f;

        float totalLength = 0f;
        foreach (var edgeId in edgeIds)
        {
            var edge = GetEdge(edgeId);
            if (edge != null)
                totalLength += edge.Length;
        }
        return totalLength;
    }

    /// <summary>
    /// 获取路径经过的节点列表
    /// </summary>
    /// <param name="edgeIds">边ID列表</param>
    /// <param name="startNodeId">起始节点ID</param>
    /// <returns>节点ID列表</returns>
    public List<string> GetPathNodes(List<string> edgeIds, string startNodeId)
    {
        if (edgeIds == null || edgeIds.Count == 0)
            return new List<string> { startNodeId };

        var nodes = new List<string> { startNodeId };
        string currentNode = startNodeId;

        foreach (var edgeId in edgeIds)
        {
            var edge = GetEdge(edgeId);
            if (edge == null) continue;

            // 确定下一个节点
            string nextNode = edge.FromNode == currentNode ? edge.ToNode : edge.FromNode;
            nodes.Add(nextNode);
            currentNode = nextNode;
        }

        return nodes;
    }

    /// <summary>
    /// 验证路径是否有效（所有边连续相连）
    /// </summary>
    /// <param name="edgeIds">边ID列表</param>
    /// <returns>是否有效</returns>
    public bool IsValidPath(List<string> edgeIds)
    {
        if (edgeIds == null || edgeIds.Count == 0)
            return true;

        for (int i = 0; i < edgeIds.Count - 1; i++)
        {
            var edge1 = GetEdge(edgeIds[i]);
            var edge2 = GetEdge(edgeIds[i + 1]);
            if (edge1 == null || edge2 == null)
                return false;

            // 检查两条边是否共享一个节点
            bool connected = edge1.FromNode == edge2.FromNode ||
                             edge1.FromNode == edge2.ToNode ||
                             edge1.ToNode == edge2.FromNode ||
                             edge1.ToNode == edge2.ToNode;
            if (!connected)
                return false;
        }

        return true;
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
