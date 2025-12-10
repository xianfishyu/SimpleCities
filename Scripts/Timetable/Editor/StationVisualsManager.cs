using Godot;
using System.Collections.Generic;

/// <summary>
/// 车站可视化和交互管理器
/// </summary>
public class StationVisualsManager
{
    private readonly Node2D parent;
    private readonly EditorConfig config;
    private readonly EditorState state;
    private readonly RailwayNetwork network;

    private readonly Dictionary<string, Line2D> stationVisuals = new();
    private readonly Dictionary<string, Label> stationLabels = new();
    private readonly Dictionary<string, List<ColorRect>> stationHandles = new();

    public StationVisualsManager(Node2D parent, EditorConfig config, EditorState state, RailwayNetwork network)
    {
        this.parent = parent;
        this.config = config;
        this.state = state;
        this.network = network;
    }

    /// <summary>
    /// 渲染所有车站
    /// </summary>
    public void RenderAll()
    {
        foreach (var station in network.Stations)
            RenderStation(station);
    }

    /// <summary>
    /// 渲染单个车站
    /// </summary>
    public void RenderStation(Station station)
    {
        if (stationVisuals.ContainsKey(station.Id))
        {
            UpdateStationVisual(station);
            return;
        }

        // 边界线
        var border = new Line2D();
        var bounds = station.Bounds;
        border.AddPoint(bounds.Position);
        border.AddPoint(new Vector2(bounds.Position.X + bounds.Size.X, bounds.Position.Y));
        border.AddPoint(bounds.Position + bounds.Size);
        border.AddPoint(new Vector2(bounds.Position.X, bounds.Position.Y + bounds.Size.Y));
        border.AddPoint(bounds.Position);
        border.Width = 2f;
        border.DefaultColor = config.StationBorderColor;
        border.ZIndex = 3;
        border.Visible = state.ShowStationBorders;
        parent.AddChild(border);
        stationVisuals[station.Id] = border;

        // 名称标签
        var label = new Label();
        label.Text = station.Name;
        label.Position = new Vector2(bounds.Position.X + 5, bounds.Position.Y + 5);
        label.ZIndex = 15;
        label.AddThemeColorOverride("font_color", config.StationLabelColor);
        parent.AddChild(label);
        stationLabels[station.Id] = label;

        // 四角手柄
        var handles = new List<ColorRect>();
        var corners = new Vector2[] {
            bounds.Position,
            new Vector2(bounds.Position.X + bounds.Size.X, bounds.Position.Y),
            bounds.Position + bounds.Size,
            new Vector2(bounds.Position.X, bounds.Position.Y + bounds.Size.Y)
        };
        for (int i = 0; i < 4; i++)
        {
            var handle = new ColorRect();
            handle.Size = new Vector2(config.StationHandleSize, config.StationHandleSize);
            handle.Position = corners[i] - new Vector2(config.StationHandleSize / 2, config.StationHandleSize / 2);
            handle.Color = config.StationHandleColor;
            handle.ZIndex = 20;
            handle.Visible = state.ShowStationBorders;
            parent.AddChild(handle);
            handles.Add(handle);
        }
        stationHandles[station.Id] = handles;
    }

    /// <summary>
    /// 更新车站可视化
    /// </summary>
    public void UpdateStationVisual(Station station)
    {
        if (!stationVisuals.TryGetValue(station.Id, out var border)) return;

        var bounds = station.Bounds;
        border.ClearPoints();
        border.AddPoint(bounds.Position);
        border.AddPoint(new Vector2(bounds.Position.X + bounds.Size.X, bounds.Position.Y));
        border.AddPoint(bounds.Position + bounds.Size);
        border.AddPoint(new Vector2(bounds.Position.X, bounds.Position.Y + bounds.Size.Y));
        border.AddPoint(bounds.Position);
        border.DefaultColor = station.Id == state.SelectedStationId ? config.SelectedColor :
                              station.Id == state.HoveredStationId ? config.HoverColor : config.StationBorderColor;
        border.Visible = state.ShowStationBorders;

        if (stationLabels.TryGetValue(station.Id, out var label))
        {
            label.Text = station.Name;
            label.Position = new Vector2(bounds.Position.X + 5, bounds.Position.Y + 5);
        }

        if (stationHandles.TryGetValue(station.Id, out var handles))
        {
            var corners = new Vector2[] {
                bounds.Position,
                new Vector2(bounds.Position.X + bounds.Size.X, bounds.Position.Y),
                bounds.Position + bounds.Size,
                new Vector2(bounds.Position.X, bounds.Position.Y + bounds.Size.Y)
            };
            for (int i = 0; i < 4 && i < handles.Count; i++)
            {
                handles[i].Position = corners[i] - new Vector2(config.StationHandleSize / 2, config.StationHandleSize / 2);
                handles[i].Color = i == state.DraggingStationHandle ? config.SelectedColor : config.StationHandleColor;
                handles[i].Visible = state.ShowStationBorders;
            }
        }
    }

    /// <summary>
    /// 更新所有车站可视化
    /// </summary>
    public void UpdateAllStationVisuals()
    {
        foreach (var station in network.Stations)
            UpdateStationVisual(station);
    }

    /// <summary>
    /// 移除车站可视化
    /// </summary>
    public void RemoveStationVisual(string stationId)
    {
        if (stationVisuals.TryGetValue(stationId, out var border))
        {
            border.QueueFree();
            stationVisuals.Remove(stationId);
        }
        if (stationLabels.TryGetValue(stationId, out var label))
        {
            label.QueueFree();
            stationLabels.Remove(stationId);
        }
        if (stationHandles.TryGetValue(stationId, out var handles))
        {
            foreach (var handle in handles)
                handle.QueueFree();
            stationHandles.Remove(stationId);
        }
    }

    /// <summary>
    /// 查找位置处的车站边界或手柄
    /// </summary>
    public string FindStationAtPosition(Vector2 pos, out int handleIndex)
    {
        handleIndex = -1;
        float threshold = 5f;

        foreach (var station in network.Stations)
        {
            // 先检查手柄
            if (state.ShowStationBorders && stationHandles.TryGetValue(station.Id, out var handles))
            {
                for (int i = 0; i < handles.Count; i++)
                {
                    var handleRect = new Rect2(handles[i].Position, handles[i].Size);
                    if (handleRect.HasPoint(pos))
                    {
                        handleIndex = i;
                        return station.Id;
                    }
                }
            }

            // 再检查边界线
            if (state.ShowStationBorders)
            {
                var bounds = station.Bounds;
                var corners = new Vector2[] {
                    bounds.Position,
                    new Vector2(bounds.Position.X + bounds.Size.X, bounds.Position.Y),
                    bounds.Position + bounds.Size,
                    new Vector2(bounds.Position.X, bounds.Position.Y + bounds.Size.Y)
                };

                for (int i = 0; i < 4; i++)
                {
                    int next = (i + 1) % 4;
                    float dist = NodeEdgeVisualsManager.PointToSegmentDistance(pos, corners[i], corners[next]);
                    if (dist < threshold)
                        return station.Id;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 拖拽车站手柄
    /// </summary>
    public void DragStationHandle(string stationId, int handleIndex, Vector2 newPos)
    {
        var station = network.GetStation(stationId);
        if (station == null || handleIndex < 0 || handleIndex > 3) return;

        var bounds = station.Bounds;
        Vector2 oppositeCorner;

        switch (handleIndex)
        {
            case 0: // 左上
                oppositeCorner = bounds.Position + bounds.Size;
                SetStationBounds(station,
                    Mathf.Min(newPos.X, oppositeCorner.X), Mathf.Min(newPos.Y, oppositeCorner.Y),
                    Mathf.Abs(oppositeCorner.X - newPos.X), Mathf.Abs(oppositeCorner.Y - newPos.Y));
                break;
            case 1: // 右上
                oppositeCorner = new Vector2(bounds.Position.X, bounds.Position.Y + bounds.Size.Y);
                SetStationBounds(station,
                    Mathf.Min(oppositeCorner.X, newPos.X), Mathf.Min(newPos.Y, oppositeCorner.Y),
                    Mathf.Abs(newPos.X - oppositeCorner.X), Mathf.Abs(oppositeCorner.Y - newPos.Y));
                break;
            case 2: // 右下
                oppositeCorner = bounds.Position;
                SetStationBounds(station,
                    Mathf.Min(oppositeCorner.X, newPos.X), Mathf.Min(oppositeCorner.Y, newPos.Y),
                    Mathf.Abs(newPos.X - oppositeCorner.X), Mathf.Abs(newPos.Y - oppositeCorner.Y));
                break;
            case 3: // 左下
                oppositeCorner = new Vector2(bounds.Position.X + bounds.Size.X, bounds.Position.Y);
                SetStationBounds(station,
                    Mathf.Min(newPos.X, oppositeCorner.X), Mathf.Min(oppositeCorner.Y, newPos.Y),
                    Mathf.Abs(oppositeCorner.X - newPos.X), Mathf.Abs(newPos.Y - oppositeCorner.Y));
                break;
        }

        UpdateStationVisual(station);
    }

    /// <summary>
    /// 设置车站边界
    /// </summary>
    private void SetStationBounds(Station station, float x, float y, float width, float height)
    {
        station.BoundsX = x;
        station.BoundsY = y;
        station.BoundsWidth = width;
        station.BoundsHeight = height;
    }

    /// <summary>
    /// 清除所有可视化
    /// </summary>
    public void Clear()
    {
        foreach (var v in stationVisuals.Values) v.QueueFree();
        foreach (var v in stationLabels.Values) v.QueueFree();
        foreach (var handles in stationHandles.Values)
            foreach (var h in handles) h.QueueFree();
        stationVisuals.Clear();
        stationLabels.Clear();
        stationHandles.Clear();
    }
}
