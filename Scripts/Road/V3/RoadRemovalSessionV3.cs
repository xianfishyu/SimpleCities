using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Road.V3;

/// <summary>
/// V3 道路拆除选择会话：先选择 canonical Edge ID，再一次性提交删除。
/// </summary>
public sealed class RoadRemovalSessionV3
{
    private readonly HashSet<int> _selectedEdgeIDs = [];

    public IReadOnlyList<int> SelectedEdgeIDs => _selectedEdgeIDs.Order().ToArray();
    public int SelectionCount => _selectedEdgeIDs.Count;

    public bool TrySelectEdge(int edgeID)
    {
        if (edgeID < 0)
            return false;
        return _selectedEdgeIDs.Add(edgeID);
    }

    public bool DeselectEdge(int edgeID) => _selectedEdgeIDs.Remove(edgeID);

    public void ClearSelection() => _selectedEdgeIDs.Clear();

    public bool TryCommit(out IReadOnlyList<int> selection)
    {
        selection = SelectedEdgeIDs;
        return selection.Count > 0;
    }
}
