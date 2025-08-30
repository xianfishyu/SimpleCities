using Godot;
using Godot.Collections;
using System;
using static Godot.GD;






// [Tool]
public partial class TrainManager : Node2D
{
    // [ExportCategory("Train Generate")]
    // [ExportToolButton("Check to generate a train")] public Callable generate_button => Callable.From(TrainGenerate);

    [Export] public PackedScene train_scene;
    [Export] public Path2D train_path;

    [Export] public Array<Node2D> trains = [];

    private void TrainGenerate()
    {
        TrainBehavior new_train = train_scene.Instantiate<TrainBehavior>();
        new_train.Initialize(train_path);

        trains.Add(new_train);
        AddChild(new_train);
    }

    public override void _Ready()
    {
        TrainGenerate();
    }
}
