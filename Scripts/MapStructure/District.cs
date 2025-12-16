using Godot;
using System;
using System.Collections.Generic;
using static Godot.GD;



public partial class District : Line2D, IMapStructure
{

    public District(string name, List<Vector2> boundary)
    {
        Name = name;
        Points = [.. boundary];
    }
}