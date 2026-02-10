using Godot;
using System;
using System.Collections;
using ImGuiNET;
using System.Collections.Generic;
using System.Linq;
using static Godot.GD;



public static class MapData
{

    public static List<IMapStructure> MapStructure{get;private set;} = [];
    public static Dictionary<string, Type> MapStructureTypes {get;private set;} = [];


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
                    typeof(IMapStructure).IsAssignableFrom(t) &&
                    !t.IsAbstract &&
                    !t.IsInterface &&
                    !t.IsAssignableFrom(typeof(IMapStructure)));
        foreach (Type type in types)
        {
            MapStructureTypes.Add(type.Name, type);
        }
    }

    private static void TEST()
    {
        // List<Vector2> boundary = [new Vector2(0, 0), new Vector2(100, 0), new Vector2(100, 100), new Vector2(0, 100)];
        // MapStructure.Add(new District("District 1", boundary));
    }
}

public interface IMapStructure
{
    public static Node StructureFolder;
}

public static partial class DebugInfo
{
    [DebugGUI("MapStructureInfo")]
    public static void MapStructureInfo()
    {
        ImGui.Text($"Registered Map Structure Types: {MapData.MapStructureTypes.Count}");
        foreach (var typeName in MapData.MapStructureTypes.Keys)
        {
            ImGui.BulletText(typeName);
        }
    }
}

