using System;
using System.Collections.Generic;

namespace SimpleCities.Road.V3;

/// <summary>
/// 将可逆 delta 应用到不可变 root，返回新 revision；失败时不修改输入。
/// allocator watermark 只前进不回退，符合 V3 的 lineage/revision 规则。
/// </summary>
public static class RoadGraphV3DeltaApplier
{
    public static bool TryApply(
        RoadGraphV3Revision revision,
        RoadGraphV3Delta delta,
        out RoadGraphV3Revision result)
    {
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(delta);

        var nodes = new Dictionary<int, RoadGraphV3Node>(revision.Nodes);
        var edges = new Dictionary<int, RoadGraphV3Edge>(revision.Edges);
        int nextNodeID = revision.NextNodeID;
        int nextEdgeID = revision.NextEdgeID;

        foreach (RoadGraphV3EntityChange<RoadGraphV3Node> change in delta.NodeChanges)
        {
            if (change.IsCreated)
            {
                RoadGraphV3Node after = change.After!;
                if (nodes.ContainsKey(after.ID))
                {
                    result = revision;
                    return false;
                }

                nodes[after.ID] = after;
                nextNodeID = Math.Max(nextNodeID, after.ID + 1);
            }
            else if (change.IsRemoved)
            {
                if (!nodes.Remove(change.Before!.ID))
                {
                    result = revision;
                    return false;
                }
            }
            else if (change.IsUpdated)
            {
                if (!nodes.ContainsKey(change.Before!.ID))
                {
                    result = revision;
                    return false;
                }

                nodes[change.Before.ID] = change.After!;
            }
        }

        foreach (RoadGraphV3EntityChange<RoadGraphV3Edge> change in delta.EdgeChanges)
        {
            if (change.IsCreated)
            {
                RoadGraphV3Edge after = change.After!;
                if (edges.ContainsKey(after.ID))
                {
                    result = revision;
                    return false;
                }

                edges[after.ID] = after;
                nextEdgeID = Math.Max(nextEdgeID, after.ID + 1);
            }
            else if (change.IsRemoved)
            {
                if (!edges.Remove(change.Before!.ID))
                {
                    result = revision;
                    return false;
                }
            }
            else if (change.IsUpdated)
            {
                if (!edges.ContainsKey(change.Before!.ID))
                {
                    result = revision;
                    return false;
                }

                edges[change.Before.ID] = change.After!;
            }
        }

        result = new RoadGraphV3Revision(revision.Capacity, nodes, edges, nextNodeID, nextEdgeID);
        return true;
    }
}
