using Godot;
using Godot.Collections;
using System;
using System.Linq;
using static Godot.GD;

public partial class TrackManager : Node2D
{
    [Export] public string TrackInfoPath;
    [Export] public Array<string> highLight = [];
    private RailwayParser parser;
    private Dictionary<int, RailwayData> railwayDataDic;
    private Line2D trackPrefab = new();

    public override void _Ready()
    {
        InitializeTrackPrefab(trackPrefab);

        parser = new(TrackInfoPath);
        railwayDataDic = parser.GetRailwayDataDic();

        foreach (RailwayData rail in railwayDataDic.Values)
        {
            if (highLight.Contains(rail.Name) || highLight.Count == 0)
            {
                AddTrack(rail);
            }
        }
    }

    public override void _Process(double delta)
    {
        // QueueRedraw();
    }

    // private Dictionary<int, Vector2> drawPointDic = [];

    // public override void _Draw()
    // {
    //     Camera2D camera = GetViewport().GetCamera2D();
    //     Vector2 cameraPosition = camera.GlobalPosition;
    //     float cameraZoom = camera.Zoom.X;
    //     Vector2 screenResolution = GetViewport().GetVisibleRect().Size;


    //     Vector2 topLeft = cameraPosition - screenResolution / 2 / cameraZoom;
    //     Vector2 bottomRight = cameraPosition + screenResolution / 2 / cameraZoom;

    //     Rect2 worldRect2 = new() { Position = topLeft, End = bottomRight };

    //     foreach (RailwayData rail in railwayDataDic.Values)
    //     {
    //         if (highLight.Contains(rail.Name) || highLight.Count == 0)
    //         {
    //             Array<Vector2> nodePosition = rail.Geometry;
    //             Array<double> nodeID = rail.Nodes;
    //             for (var i = 0; i < nodeID.Count; i++)
    //             {
    //                 // if (worldRect2.HasPoint(nodePosition[i]))
    //                 // {
    //                     // if (!drawPointDic.ContainsKey(i)) {
    //                         DrawCircle(nodePosition[i], 2f, Colors.White);
    //                         // drawPointDic.Add(i, nodePosition[i]);
    //                     // }
    //                     // else
    //                     //     continue;
    //                 // }
    //             }
    //             // for (var i = 0; i < drawPointDic.Count; i++)
    //             // {
    //             //     if (!worldRect2.HasPoint(drawPointDic[i]))
    //             //     {
    //             //         drawPointDic.Remove(i);
    //             //     }
    //             // }
    //         }
    //     }
    //     // Print(drawPointDic.Count);
    // }


    private void InitializeTrackPrefab(Line2D trackPrefab)
    {
        trackPrefab.Width = 1.435f;
        trackPrefab.DefaultColor = Colors.Black;
        trackPrefab.ZIndex = -1;
    }

    private void AddTrack(RailwayData data)
    {
        Line2D track = (Line2D)trackPrefab.Duplicate();
        track.Name = $"ID_{data.ID}/Name_{data.Name}";
        track.Points = [.. data.Geometry];
        AddChild(track);
    }
}
