using Godot;
using Godot.Collections;
using System;
using System.Linq;
using static Godot.GD;

public partial class TrackManager : Node2D
{
    [Export] public string TrackInfoPath;
    private RailwayParser parser;
    private Dictionary<int, RailwayData> railwayDataDic;
    private Line2D trackPrefab = new();

    public override void _Ready()
    {
        parser = new(TrackInfoPath);
        railwayDataDic = parser.GetRailwayDataDic;
        InitializeTrackPrefab(trackPrefab);

        foreach (RailwayData rail in railwayDataDic.Values)
        {
            AddTrack(rail);
        }

    }

    public override void _Draw()
    {
        
    }


    private void InitializeTrackPrefab(Line2D trackPrefab)
    {
        trackPrefab.Width = 1.435f;
        trackPrefab.DefaultColor = new Color(0.663f, 0.396f, 0.286f, 1.0f);
        ZIndex = -1;
    }

    private void AddTrack(RailwayData data)
    {
        Line2D track = (Line2D)trackPrefab.Duplicate();
        track.Name = $"ID_{data.ID}/Name_{data.Name}";
        track.Points = [.. data.Geometry];
        AddChild(track);
    }
}
