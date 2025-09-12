using Godot;
using System;
using static Godot.GD;
using Godot.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;


[Tool]
public partial class RailwayParser
{
    //选择任意一个你认为简单的Samples文件夹里面的文件进行解析
    //输出一个dictionary,key是id,value是一个包含所有geometry信息的数组
    //即Dictionary<id,Array<Vector2>>
    //默认使用godot类型

    //已完成
    private Vector2 basePoint = Vector2.Zero;

    public RailwayParser(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }
        JsonParser(path);
    }

    public Dictionary<int, RailwayData> RailwayDataDic = [];


    private void JsonParser(string path)
    {
        Json json = Load<Json>(path);
        string data = json.Data.ToString();
        Error error = json.Parse(data);

        // if (error == Error.ParseError)
        // {
        //     Print($"{path} is not a json");
        //     return;
        // }

        Root root = JsonSerializer.Deserialize<Root>(data);
        basePoint = new Vector2(root.elements[0].geometry[0].lon, root.elements[0].geometry[0].lat);
        Print(1);
        foreach (Element element in root.elements)
        {
            Array<Vector2> geometryArray = [];
            for (int i = 0; i < element.geometry.Count; i++)
                geometryArray.Add(new Vector2((element.geometry[i].lon - basePoint.X) * 86414.25f, -(element.geometry[i].lat - basePoint.Y) * 111194.93f)); //经纬反转 纬度取反,因为坐标系问题 参数一经度 参数二纬度


            RailwayDataDic.Add
            ((int)element.id,

            new RailwayData(
            type: element.type,
            name: element.tags.railwaytrack_ref,
            id: (int)element.id,
            bounds: (element.bounds.GetMinVector2, element.bounds.GetMinVector2),
            nodes: element.nodes,
            geometry: geometryArray,
            passengerLines: element.tags.passenger_lines
            ));
        }

    }

    public Dictionary<int, RailwayData> GetRailwayDataDic() => RailwayDataDic;



    public class Root
    {
        public double version { get; set; }
        public string generator { get; set; }
        public Osm3s osm3s { get; set; }
        public Array<Element> elements { get; set; }
    }

    public class Osm3s
    {
        public DateTime timestamp_osm_base { get; set; }
        public string copyright { get; set; }
    }

    public partial class Element : GodotObject
    {
        public string type { get; set; }
        public double id { get; set; }
        public Bounds bounds { get; set; }
        public Array<double> nodes { get; set; }
        public Array<Geometry> geometry { get; set; }
        public Tags tags { get; set; }
    }

    public class Bounds
    {
        public float minlat { get; set; }
        public float minlon { get; set; }
        public float maxlat { get; set; }
        public float maxlon { get; set; }

        public Vector2 GetMinVector2 => new Vector2(minlon, minlat);
        public Vector2 GetMaxVector2 => new Vector2(maxlon, maxlat);
    }

    public partial class Geometry : GodotObject
    {
        public float lat { get; set; }
        public float lon { get; set; }
    }

    public class Tags
    {
        public string electrified { get; set; }
        public string frequency { get; set; }
        public string gauge { get; set; }
        public string highspeed { get; set; }
        public string maxspeed { get; set; }
        public string name { get; set; }

        [JsonPropertyName("name:en")]
        public string nameen { get; set; }
        public string railway { get; set; }
        public string @ref { get; set; }
        public string usage { get; set; }
        public string voltage { get; set; }
        public string bridge { get; set; }
        public string layer { get; set; }
        public string service { get; set; }

        [JsonPropertyName("railway:track_ref")]
        public string railwaytrack_ref { get; set; }
        public string alt_name { get; set; }
        public string passenger_lines { get; set; }
        public string tunnel { get; set; }
    }
}

public partial class RailwayData : GodotObject
{
    public RailwayData(string type, string name, int id, (Vector2 minBounds, Vector2 maxBounds) bounds, Array<double> nodes, Array<Vector2> geometry, string passengerLines)
    {
        Type = type;
        Name = name is not null ? name : null;
        ID = id;
        RailwayBounds = new Bounds(bounds.minBounds, bounds.maxBounds);
        Nodes = nodes;
        Geometry = geometry;
        PassengerLines = passengerLines;
    }

    public string Type { get; set; }
    public string Name { get; set; }
    public int ID { get; set; }
    public Bounds RailwayBounds { get; set; }
    public Array<double> Nodes { get; set; }
    public Array<Vector2> Geometry { get; set; }
    public string PassengerLines { get; set; }

    public class Bounds(Vector2 min, Vector2 max)
    {
        public Vector2 Min { get; set; } = min;
        public Vector2 Max { get; set; } = max;
    }
}


