using Godot;
using System;
using System.Collections;
using ImGuiNET;
using System.Collections.Generic;
using static Godot.GD;




public partial class MapStructureManager : Node
{
    private Dictionary<string, Node> StructureFolder = [];
    public override void _Ready()
    {
        CreateStructureFolders(MapData.GetMapStructureTypes);
        CreateStructures(MapData.GetMapStructure);
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
    }

    private void CreateStructures(List<MapStructure> structures)
    {
        foreach (var structure in structures)
            CreateStructure(structure);
    }

    private void CreateStructure(MapStructure structure)
    {
        structure.StructureGenerate(StructureFolder[structure.GetType().Name]);
    }
}

public static partial class DebugInfo
{
    [DebugGUI("MapStructureInfo")]
    public static void MapStructureInfo()
    {
        ImGui.Text($"Registered Map Structure Types: {MapData.GetMapStructureTypes.Count}");
        foreach (var typeName in MapData.GetMapStructureTypes)
        {
            ImGui.BulletText(typeName);
        }
    }
}