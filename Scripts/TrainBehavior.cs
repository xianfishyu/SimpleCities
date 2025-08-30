using Godot;
using Godot.Collections;
using Godot.NativeInterop;
using System;
using static Godot.GD;

public partial class TrainBehavior : Node2D
{
    [ExportGroup("Train Group Settings")]
    [Export] public Node2D[] train_list;
    [Export] public Node2D[] true_train_truck;
    public PathFollow2D[] gene_train_truck;

    [ExportGroup("Path Settings")]
    [Export] public Path2D path;

    [ExportGroup("Train physics Settings")]
    [Export(PropertyHint.None, "suffix:m")] public float train_length = 100f;
    [Export(PropertyHint.None, "suffix:m")] public float train_width = 3.3f;
    [Export(PropertyHint.None, "suffix:m/s\u00b2")] public float max_acceleration = 0.8f;
    [Export(PropertyHint.None, "suffix:m/s\u00b2")] public float max_braking = 1.0f;
    [Export(PropertyHint.None, "suffix:m/s\u00b2")] public float emergency_braking = 1.4f;
    [Export(PropertyHint.None, "suffix:km/h")] public float operation_speed = 160f;
    [Export(PropertyHint.None, "suffix:km/h")] public float max_speed = 180f;
    [Export(PropertyHint.None, "suffix:km/h")] private float current_speed = 0f;

    private float operation_speed_mps => operation_speed / 3.6f; // Convert km/h to m/s
    private float max_speed_mps => max_speed / 3.6f; // Convert km/h to m/s
    private float current_speed_mps => current_speed / 3.6f; // Convert km/h to m/s

    [ExportGroup("Train Settings")]
    [Export] public string train_model = "CJ6";
    [Export] public string train_number = "G1";
    [Export(PropertyHint.None, "suffix:member")] public int max_passenger = 240;
    [Export(PropertyHint.None, "suffix:carriages")] public int train_carriage = 4;


    public override void _Ready()
    {
        gene_train_truck = new PathFollow2D[train_list.Length * 2];
        for (int i = 0; i < train_list.Length * 2; i++)
        {
            gene_train_truck[i] = new PathFollow2D();
            path.AddChild(gene_train_truck[i]);
            gene_train_truck[i].Progress = true_train_truck[i].GlobalPosition.Y;
            gene_train_truck[i].Loop = true;
        }

    }


    public override void _Process(double delta)
    {
        GlobalPosition = gene_train_truck[0].GlobalPosition;

        if (gene_train_truck[0].Loop != false || gene_train_truck[0].ProgressRatio != 1f)
        {
            foreach (var follow2D in gene_train_truck)
            {
                follow2D.Progress += operation_speed_mps * (float)delta;
            }
        }

        for (int i = 0; i < train_list.Length; i++)
        {
            train_list[i].GlobalPosition = (gene_train_truck[i * 2].GlobalPosition + gene_train_truck[i * 2 + 1].GlobalPosition) / 2;
            float angle = Mathf.DegToRad(90f) + (gene_train_truck[i * 2 + 1].GlobalPosition - gene_train_truck[i * 2].GlobalPosition).Angle();
            train_list[i].Rotation = angle;
        }

    }

    public void Initialize(Path2D path)
    {
        this.path = path;
    }
}
