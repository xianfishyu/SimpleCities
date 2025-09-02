using Godot;
using System;
using System.Collections.Generic;
using static Godot.GD;

public partial class RailwayParser : Node
{
    //选择任意一个你认为简单的Samples文件夹里面的文件进行解析
    //输出一个dictionary,key是id,value是一个包含所有geometry信息的数组
    //即Dictionary<id,Array<Vector2>>
    //默认使用godot类型

    public string Path { get; private set; }
    private RailwayFileType FileType { get; set; }
    public Dictionary<int, RailwayData> RailwayData { get; private set; }


    public void JsonParser(string path)
    {
        Json json = Load<Json>(path);
        Error type = json.Parse(json.Data.ToString());
        Variant data = json.Data;

        FileType = RailwayFileType.Json;
    }

    public string GetFileType => FileType.ToString();
    
    enum RailwayFileType
    {
        Geojson,
        Gpx,
        Json,
        Kml,
    }
}



public partial class RailwayData : GodotObject
{
    public string Type { get; set; }
    public int ID { get; set; }
    public (Vector2 min, Vector2 max) Bounds { get; set; }
    public List<int> Nodes { get; set; }
    public List<Vector2> Geometry { get; set; }

    public string Electrified { get; set; }
    public string Frequency { get; set; }
    public string Gauge { get; set; }
    public string HighSpeed { get; set; }
    public string MaxSpeed { get; set; }
    public string Name { get; set; }
    public string NameEN { get; set; }
    public string Railway { get; set; }
    public string RefID { get; set; }
    public string Usage { get; set; }
    public string Voltage { get; set; }
}
