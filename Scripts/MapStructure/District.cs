using Godot;
using System;
using System.Collections.Generic;
using static Godot.GD;



public class District : MapStructure
{
    public List<Vector2> Boundary { get; private set; }

    public override void StructureGenerate(Node parent)
    {
        Line2D line = new Line2D();
        line.Points = Boundary.ToArray();
        line.Width = 1;
        line.DefaultColor = Colors.Red;
        line.Closed = true;
        parent.AddChild(line);
    }

    public District(string name, List<Vector2> boundary)
    {
        Name = name;
        Boundary = boundary;
    }
}