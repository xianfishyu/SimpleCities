using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using static Godot.GD;



public static class MapData
{

    private static List<MapStructure> MapStructure = [];
    private static List<string> MapStructureTypes = [];


    static MapData()
    {
        RegisterMapStructureType();
        TEST();
    }

    public static void RegisterMapStructureType()
    {
        IEnumerable<Type> types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t =>
                    typeof(MapStructure).IsAssignableFrom(t) &&
                    !t.IsAbstract &&
                    !t.IsInterface &&
                    !t.IsAssignableFrom(typeof(MapStructure)));
        foreach (var type in types)
        {
            MapStructureTypes.Add(type.Name);
        }
    }

    public static List<MapStructure> GetMapStructure => MapStructure;
    public static List<string> GetMapStructureTypes => MapStructureTypes;

    private static void TEST()
    {
        List<Vector2> boundary = [new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10), new Vector2(0, 10)];
        MapStructure.Add(new District("District 1", boundary));
    }
}

public class MapStructure
{
    public string Name { get; set; }
    public virtual void StructureGenerate(Node parent){}
}