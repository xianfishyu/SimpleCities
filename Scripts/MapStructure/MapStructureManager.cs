using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using ImGuiNET;
using static Godot.GD;
using System.Linq;




public partial class MapStructureManager : Node
{
    private Dictionary<string, Node> StructureFolder = [];

    public override void _EnterTree()
    {
        CreateStructureFolders([.. MapData.MapStructureTypes.Keys]);
        CreateStructures(MapData.MapStructure);
    }

    private void CreateStructureFolders(List<string> typeNames)
    {
        foreach (var typeName in typeNames)
            CreateStructureFolder(typeName);
    }

    private void CreateStructureFolder(string typeName)
    {
        Node typeFolder = new()
        {
            Name = typeName + "s"
        };
        StructureFolder.TryAdd(typeName, typeFolder);
        AddChild(typeFolder);
        MapData.MapStructureTypes.TryGetValue(typeName, out Type type);
        if(type != null)
        {
            type
        }
    }

    private void CreateStructures(List<IMapStructure> structures)
    {
        // foreach (var structure in structures)
            // structure.StructureGenerate(StructureFolder[structure.GetType().Name]);
    }

    private void CreateStructure(IMapStructure structure)
    {
        // structure.StructureGenerate(StructureFolder[structure.GetType().Name]);
    }
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