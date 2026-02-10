using Godot;
using System;
using System.Collections.Generic;
using static Godot.GD;



public class Zone
{
    public string Name;
    public List<Vector2> Boundary = [];
    public float Area { get => GetArea(Boundary); }

    public static float GetArea(List<Vector2> points)
    {
        int n = points.Count;
        if (n < 3)
            return 0f;

        float area = 0f;
        for (int i = 0; i < n; i++)
        {
            Vector2 p1 = points[i];
            Vector2 p2 = points[(i + 1) % n];

            area += p1.X * p2.Y - p2.X * p1.Y;
        }
        return Mathf.Abs(area) * 0.5f;
    }

    public Vector2[] BoundaryArray => [.. Boundary];
    
}