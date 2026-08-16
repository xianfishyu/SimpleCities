using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Road.V3;

/// <summary>
/// V3 道路改造选择会话：先选择 canonical Edge ID，再一次性提交目标 RoadType。
/// </summary>
public sealed class RoadUpgradeSessionV3
{
    private readonly HashSet<int> _selectedEdgeIDs = [];
    private RoadType _targetType;

    public RoadType TargetType => _targetType;
    public IReadOnlyList<int> SelectedEdgeIDs => _selectedEdgeIDs.Order().ToArray();
    public int SelectionCount => _selectedEdgeIDs.Count;

    public RoadUpgradeSessionV3(RoadType targetType)
    {
        if (!RoadTypeChangeValidator.IsValidRoadType(targetType))
            throw new ArgumentOutOfRangeException(nameof(targetType), targetType, "Unknown road type.");
        _targetType = targetType;
    }

    public bool TrySelectEdge(int edgeID)
    {
        if (edgeID < 0)
            return false;
        return _selectedEdgeIDs.Add(edgeID);
    }

    public bool TrySelectHit(RoadSurfaceHit hit)
    {
        if (hit is null || !hit.IsValid || hit.EdgeID is not int edgeID)
            return false;
        return TrySelectEdge(edgeID);
    }

    public bool DeselectEdge(int edgeID) => _selectedEdgeIDs.Remove(edgeID);

    public void ClearSelection() => _selectedEdgeIDs.Clear();

    public bool TryCommit(out IReadOnlyList<int> selection)
    {
        selection = SelectedEdgeIDs;
        return selection.Count > 0;
    }
}
