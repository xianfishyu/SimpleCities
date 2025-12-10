using Godot;

/// <summary>
/// 编辑器共享状态
/// </summary>
public class EditorState
{
    // 编辑模式
    public EditMode CurrentMode { get; set; } = EditMode.Select;
    public TrackType CurrentTrackType { get; set; } = TrackType.ArrivalDeparture;

    // 选中状态
    public string SelectedNodeId { get; set; } = null;
    public string SelectedEdgeId { get; set; } = null;
    public string SelectedStationId { get; set; } = null;

    // 悬停状态
    public string HoveredNodeId { get; set; } = null;
    public string HoveredEdgeId { get; set; } = null;
    public string HoveredStationId { get; set; } = null;

    // 绘制状态
    public string DrawStartNodeId { get; set; } = null;
    public bool IsDragging { get; set; } = false;

    // 站场绘制状态
    public bool IsDrawingStation { get; set; } = false;
    public Vector2 StationDrawStart { get; set; }

    // 站场拖拽状态
    public int DraggingStationHandle { get; set; } = -1; // 0=左上, 1=右上, 2=右下, 3=左下, -1=无
    public string DraggingStationId { get; set; } = null;

    // 调试选项
    public bool ShowCollisionBoxes { get; set; } = false;
    public bool ShowStationBorders { get; set; } = true;

    /// <summary>
    /// 清除所有选择状态
    /// </summary>
    public void ClearSelection()
    {
        SelectedNodeId = null;
        SelectedEdgeId = null;
        SelectedStationId = null;
    }

    /// <summary>
    /// 重置绘制状态
    /// </summary>
    public void ResetDrawingState()
    {
        DrawStartNodeId = null;
        IsDrawingStation = false;
        DraggingStationHandle = -1;
        DraggingStationId = null;
    }
}
