using Godot;
using System;
using System.Collections.Generic;
using static Godot.GD;



public class District : Zone
{
    public District(string name, List<Vector2> boundary)
    {
        Name = name;
        Boundary = boundary;
    }
}