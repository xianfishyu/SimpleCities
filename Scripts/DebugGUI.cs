using Godot;
using System;
using ImGuiNET;
using static Godot.GD;

public partial class DebugGUI : Node
{
    public override void _Process(double delta)
    {
        ImGui.Begin("Debug Window");

        ImGui.Text("Hello, Debug GUI!");

        ImGui.End();
    }
}
