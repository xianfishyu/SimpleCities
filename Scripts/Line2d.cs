using Godot;
using System;


[Tool]
public partial class Line2d : Line2D
{
    [Export] public Path2D path;



    public override void _Ready()
    {
        Curve2D curve = path.Curve;
        if (curve == null) return;

        int pointCount = curve.GetPointCount();
        Vector2[] points = new Vector2[pointCount];
        points = curve.TessellateEvenLength(5);
        Points = points;
    }
}
