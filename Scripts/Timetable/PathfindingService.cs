using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// 寻路选项
/// </summary>
public class PathfindingOptions
{
    /// <summary>是否下行（true=X增大方向，false=X减小方向）</summary>
    public bool? IsDownbound { get; set; } = null;

    /// <summary>是否启用靠左行驶规则</summary>
    public bool UseLeftHandRule { get; set; } = true;

    /// <summary>首选股道ID列表</summary>
    public List<string> PreferredTrackIds { get; set; } = null;

    /// <summary>避开的边ID列表（如已被占用的轨道）</summary>
    public HashSet<string> AvoidEdges { get; set; } = null;

    /// <summary>渡线惩罚系数（默认2.0）</summary>
    public float CrossoverPenalty { get; set; } = 2.0f;

    /// <summary>逆向正线惩罚系数（默认1.5）</summary>
    public float WrongDirectionPenalty { get; set; } = 1.5f;

    /// <summary>正确方向正线奖励系数（默认0.5）</summary>
    public float CorrectDirectionBonus { get; set; } = 0.5f;
}

/// <summary>
/// 寻路结果
/// </summary>
public class PathfindingResult
{
    /// <summary>是否成功找到路径</summary>
    public bool Success { get; set; }

    /// <summary>边ID列表</summary>
    public List<string> EdgeIds { get; set; } = new();

    /// <summary>节点ID列表</summary>
    public List<string> NodeIds { get; set; } = new();

    /// <summary>路径总长度</summary>
    public float TotalLength { get; set; }

    /// <summary>路径加权代价（考虑各种惩罚后）</summary>
    public float TotalCost { get; set; }

    /// <summary>错误信息（如果失败）</summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static PathfindingResult Fail(string message)
    {
        return new PathfindingResult
        {
            Success = false,
            ErrorMessage = message
        };
    }
}

/// <summary>
/// 列车寻路服务 - 提供基于铁路规则的寻路功能
/// 支持正线靠左行驶等中国铁路运行规则
/// </summary>
public class PathfindingService
{
    private readonly RailwayNetwork network;

    public PathfindingService(RailwayNetwork network)
    {
        this.network = network ?? throw new ArgumentNullException(nameof(network));
    }

    /// <summary>
    /// 寻找从起点到终点的路径
    /// </summary>
    /// <param name="startNodeId">起点节点ID</param>
    /// <param name="endNodeId">终点节点ID</param>
    /// <param name="options">寻路选项（可选）</param>
    /// <returns>寻路结果</returns>
    public PathfindingResult FindPath(string startNodeId, string endNodeId, PathfindingOptions options = null)
    {
        options ??= new PathfindingOptions();

        var startNode = network.GetNode(startNodeId);
        var endNode = network.GetNode(endNodeId);

        if (startNode == null)
            return PathfindingResult.Fail($"起点节点不存在: {startNodeId}");
        if (endNode == null)
            return PathfindingResult.Fail($"终点节点不存在: {endNodeId}");

        // 确定行驶方向
        bool isDownbound = options.IsDownbound ?? (endNode.X > startNode.X);

        // 使用 A* 算法寻路
        var result = AStarSearch(startNode, endNode, isDownbound, options);

        return result;
    }

    /// <summary>
    /// 为列车计算路径（根据时刻表中的站间运行）
    /// </summary>
    /// <param name="train">列车</param>
    /// <param name="fromStation">出发站名</param>
    /// <param name="toStation">到达站名</param>
    /// <param name="departTrack">出发股道号</param>
    /// <param name="arriveTrack">到达股道号</param>
    /// <returns>寻路结果</returns>
    public PathfindingResult FindPathForTrain(Train train, string fromStation, string toStation,
        int departTrack = 0, int arriveTrack = 0)
    {
        // 查找对应的站场和股道节点
        string startNodeId = FindTrackNode(fromStation, departTrack, true);
        string endNodeId = FindTrackNode(toStation, arriveTrack, false);

        if (string.IsNullOrEmpty(startNodeId))
            return PathfindingResult.Fail($"无法找到 {fromStation} 站 {departTrack} 道的出发节点");
        if (string.IsNullOrEmpty(endNodeId))
            return PathfindingResult.Fail($"无法找到 {toStation} 站 {arriveTrack} 道的到达节点");

        var options = new PathfindingOptions
        {
            IsDownbound = train.IsDownbound,
            UseLeftHandRule = true
        };

        return FindPath(startNodeId, endNodeId, options);
    }

    /// <summary>
    /// A* 搜索算法实现
    /// </summary>
    private PathfindingResult AStarSearch(RailwayNode start, RailwayNode end,
        bool isDownbound, PathfindingOptions options)
    {
        var openSet = new SortedSet<(float fScore, string nodeId)>(
            Comparer<(float, string)>.Create((a, b) =>
            {
                int cmp = a.Item1.CompareTo(b.Item1);
                return cmp != 0 ? cmp : string.Compare(a.Item2, b.Item2, StringComparison.Ordinal);
            }));

        var gScore = new Dictionary<string, float>();
        var fScore = new Dictionary<string, float>();
        var cameFrom = new Dictionary<string, (string nodeId, string edgeId)>();
        var inOpenSet = new HashSet<string>();

        gScore[start.Id] = 0;
        fScore[start.Id] = HeuristicCost(start, end);
        openSet.Add((fScore[start.Id], start.Id));
        inOpenSet.Add(start.Id);

        while (openSet.Count > 0)
        {
            var current = openSet.Min;
            openSet.Remove(current);
            string currentId = current.nodeId;
            inOpenSet.Remove(currentId);

            if (currentId == end.Id)
            {
                // 重建路径
                return ReconstructPath(cameFrom, start.Id, end.Id);
            }

            var currentNode = network.GetNode(currentId);

            foreach (var edgeId in network.GetConnectedEdges(currentId))
            {
                // 检查是否需要避开此边
                if (options.AvoidEdges != null && options.AvoidEdges.Contains(edgeId))
                    continue;

                var edge = network.GetEdge(edgeId);
                string neighborId = edge.FromNode == currentId ? edge.ToNode : edge.FromNode;
                var neighborNode = network.GetNode(neighborId);

                if (neighborNode == null) continue;

                // 计算边代价
                float edgeCost = CalculateEdgeCost(edge, currentNode, neighborNode, isDownbound, options);
                float tentativeG = gScore.GetValueOrDefault(currentId, float.MaxValue) + edgeCost;

                if (tentativeG < gScore.GetValueOrDefault(neighborId, float.MaxValue))
                {
                    cameFrom[neighborId] = (currentId, edgeId);
                    gScore[neighborId] = tentativeG;
                    float f = tentativeG + HeuristicCost(neighborNode, end);
                    fScore[neighborId] = f;

                    if (!inOpenSet.Contains(neighborId))
                    {
                        openSet.Add((f, neighborId));
                        inOpenSet.Add(neighborId);
                    }
                }
            }
        }

        return PathfindingResult.Fail("无法找到从起点到终点的路径");
    }

    /// <summary>
    /// 启发式代价估算（曼哈顿距离）
    /// </summary>
    private float HeuristicCost(RailwayNode from, RailwayNode to)
    {
        return Math.Abs(to.X - from.X) + Math.Abs(to.Y - from.Y);
    }

    /// <summary>
    /// 计算边的代价（考虑轨道类型和行驶方向）
    /// </summary>
    private float CalculateEdgeCost(RailwayEdge edge, RailwayNode fromNode, RailwayNode toNode,
        bool isDownbound, PathfindingOptions options)
    {
        float baseCost = edge.Length;

        // 渡线惩罚
        if (edge.TrackType == TrackType.Crossover)
        {
            baseCost *= options.CrossoverPenalty;
        }

        // 正线方向判断
        if (options.UseLeftHandRule &&
            (edge.TrackType == TrackType.MainLine || edge.TrackType == TrackType.MainLineWithPlatform))
        {
            float avgY = (fromNode.Y + toNode.Y) / 2;
            bool isPreferred = IsPreferredMainLine(edge, avgY, isDownbound);

            if (isPreferred)
            {
                baseCost *= options.CorrectDirectionBonus;
            }
            else
            {
                baseCost *= options.WrongDirectionPenalty;
            }
        }

        return baseCost;
    }

    /// <summary>
    /// 判断是否是首选正线（靠左行驶规则）
    /// </summary>
    private bool IsPreferredMainLine(RailwayEdge edge, float avgY, bool isDownbound)
    {
        var fromNode = network.GetNode(edge.FromNode);
        var toNode = network.GetNode(edge.ToNode);
        float midX = (fromNode.X + toNode.X) / 2;

        float searchRadius = 100f;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        foreach (var otherEdge in network.Edges)
        {
            if (otherEdge.TrackType != TrackType.MainLine &&
                otherEdge.TrackType != TrackType.MainLineWithPlatform)
                continue;

            var otherFrom = network.GetNode(otherEdge.FromNode);
            var otherTo = network.GetNode(otherEdge.ToNode);
            if (otherFrom == null || otherTo == null) continue;

            float otherMidX = (otherFrom.X + otherTo.X) / 2;

            if (Math.Abs(otherMidX - midX) < searchRadius)
            {
                float otherAvgY = (otherFrom.Y + otherTo.Y) / 2;
                minY = Math.Min(minY, otherAvgY);
                maxY = Math.Max(maxY, otherAvgY);
            }
        }

        if (Math.Abs(maxY - minY) < 0.1f)
            return true;

        // 靠左行驶：下行走Y较大，上行走Y较小
        float midY = (minY + maxY) / 2;
        return isDownbound ? avgY >= midY : avgY <= midY;
    }

    /// <summary>
    /// 重建路径
    /// </summary>
    private PathfindingResult ReconstructPath(
        Dictionary<string, (string nodeId, string edgeId)> cameFrom,
        string startId, string endId)
    {
        var edgeIds = new List<string>();
        var nodeIds = new List<string>();
        float totalLength = 0f;

        string current = endId;
        nodeIds.Add(current);

        while (cameFrom.ContainsKey(current))
        {
            var (prevNode, edgeId) = cameFrom[current];
            edgeIds.Insert(0, edgeId);
            nodeIds.Insert(0, prevNode);

            var edge = network.GetEdge(edgeId);
            if (edge != null)
                totalLength += edge.Length;

            current = prevNode;
        }

        return new PathfindingResult
        {
            Success = true,
            EdgeIds = edgeIds,
            NodeIds = nodeIds,
            TotalLength = totalLength
        };
    }

    /// <summary>
    /// 根据站名和股道号查找节点ID
    /// </summary>
    /// <param name="stationName">站名</param>
    /// <param name="trackNumber">股道号</param>
    /// <param name="isExit">是否为出口端（右侧）</param>
    /// <returns>节点ID</returns>
    private string FindTrackNode(string stationName, int trackNumber, bool isExit)
    {
        // 根据站名缩写查找
        string prefix = GetStationPrefix(stationName);
        if (string.IsNullOrEmpty(prefix))
        {
            GD.PrintErr($"Unknown station name: {stationName}");
            return null;
        }

        string suffix = isExit ? "_R" : "_L";

        // 尝试匹配股道节点 (T = Track)
        string trackNodeId = $"{prefix}_T{trackNumber}{suffix}";
        if (network.GetNode(trackNodeId) != null)
            return trackNodeId;

        // 尝试匹配正线节点 (M = MainLine)
        string mainNodeId = $"{prefix}_M{trackNumber}{suffix}";
        if (network.GetNode(mainNodeId) != null)
            return mainNodeId;

        // 尝试用罗马数字匹配正线（I=1, II=2, III=3, IV=4）
        string romanNumeral = TrackNumberToRoman(trackNumber);
        if (!string.IsNullOrEmpty(romanNumeral))
        {
            string romanNodeId = $"{prefix}_M{romanNumeral}{suffix}";
            if (network.GetNode(romanNodeId) != null)
                return romanNodeId;
        }

        // 如果没找到具体股道，尝试找站场内的任意可用节点
        var station = network.Stations.Find(s => s.Name == stationName);
        if (station != null)
        {
            var nodesInStation = network.GetNodesInStation(station.Id);
            if (nodesInStation.Count > 0)
            {
                // 过滤出左/右端点
                var endNodes = nodesInStation.FindAll(n => n.Id.EndsWith(suffix));
                if (endNodes.Count > 0)
                {
                    // 返回最靠近出/入口方向的节点
                    endNodes.Sort((a, b) =>
                        isExit ? b.X.CompareTo(a.X) : a.X.CompareTo(b.X));
                    return endNodes[0].Id;
                }

                // 如果没有端点匹配，返回位置最合适的节点
                nodesInStation.Sort((a, b) =>
                    isExit ? b.X.CompareTo(a.X) : a.X.CompareTo(b.X));
                return nodesInStation[0].Id;
            }
        }

        GD.PrintErr($"Cannot find track node for {stationName} track {trackNumber} ({(isExit ? "exit" : "entry")})");
        return null;
    }

    /// <summary>
    /// 将阿拉伯数字转为罗马数字
    /// </summary>
    private string TrackNumberToRoman(int number)
    {
        return number switch
        {
            1 => "I",
            2 => "II",
            3 => "III",
            4 => "IV",
            5 => "V",
            _ => null
        };
    }

    /// <summary>
    /// 获取站名的前缀缩写
    /// </summary>
    private string GetStationPrefix(string stationName)
    {
        // 常见站名映射（支持多种写法）
        return stationName switch
        {
            "北京南" or "北京南站" or "BJN" => "BJS",
            "亦庄" or "亦庄站" => "YZ",
            "武清" or "武清站" => "WQ",
            "天津" or "天津站" or "天津西" => "TJ",
            "永乐" or "永乐站" => "YL",
            "塘沽" or "塘沽站" => "TG",
            "军粮城" or "军粮城站" => "JLC",
            "杨村" or "杨村站" => "YC",
            _ => TryExtractPrefix(stationName)
        };
    }

    /// <summary>
    /// 尝试从站名提取前缀（用于动态支持新站名）
    /// </summary>
    private string TryExtractPrefix(string stationName)
    {
        if (string.IsNullOrEmpty(stationName))
            return null;

        // 尝试在网络中查找匹配的站场
        var station = network.Stations.Find(s => 
            s.Name == stationName || 
            s.Name.Contains(stationName) || 
            stationName.Contains(s.Name));

        if (station != null)
        {
            // 从站场ID中提取前缀（假设站场ID格式为 "XXX_Station"）
            var parts = station.Id.Split('_');
            if (parts.Length > 0)
                return parts[0];
        }

        // 如果还是找不到，尝试用站名首字母拼音
        // 这里简化处理，直接返回null
        return null;
    }
}
