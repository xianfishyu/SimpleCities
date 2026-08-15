using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Road.V3;

/// <summary>
/// 从前后不可变 root 构建可逆 delta，供规范化/批量变更记录历史。
/// </summary>
public static class RoadGraphV3DeltaBuilder
{
    public static RoadGraphV3Delta BuildDelta(
        RoadGraphV3Revision before,
        RoadGraphV3Revision after,
        long beforeRevisionID,
        long afterRevisionID)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var nodeChanges = new List<RoadGraphV3EntityChange<RoadGraphV3Node>>();
        foreach (int id in before.Nodes.Keys.Except(after.Nodes.Keys).Order())
            nodeChanges.Add(new RoadGraphV3EntityChange<RoadGraphV3Node>(before.Nodes[id], null));
        foreach (int id in after.Nodes.Keys.Except(before.Nodes.Keys).Order())
            nodeChanges.Add(new RoadGraphV3EntityChange<RoadGraphV3Node>(null, after.Nodes[id]));
        foreach (int id in before.Nodes.Keys.Intersect(after.Nodes.Keys).Order())
        {
            if (!before.Nodes[id].Equals(after.Nodes[id]))
                nodeChanges.Add(new RoadGraphV3EntityChange<RoadGraphV3Node>(before.Nodes[id], after.Nodes[id]));
        }

        var edgeChanges = new List<RoadGraphV3EntityChange<RoadGraphV3Edge>>();
        foreach (int id in before.Edges.Keys.Except(after.Edges.Keys).Order())
            edgeChanges.Add(new RoadGraphV3EntityChange<RoadGraphV3Edge>(before.Edges[id], null));
        foreach (int id in after.Edges.Keys.Except(before.Edges.Keys).Order())
            edgeChanges.Add(new RoadGraphV3EntityChange<RoadGraphV3Edge>(null, after.Edges[id]));
        foreach (int id in before.Edges.Keys.Intersect(after.Edges.Keys).Order())
        {
            if (!before.Edges[id].Equals(after.Edges[id]))
                edgeChanges.Add(new RoadGraphV3EntityChange<RoadGraphV3Edge>(before.Edges[id], after.Edges[id]));
        }

        return new RoadGraphV3Delta(beforeRevisionID, afterRevisionID, nodeChanges, edgeChanges);
    }
}
