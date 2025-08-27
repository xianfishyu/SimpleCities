using Godot;
using System;
using static Godot.GD;






// [Tool]
public partial class TrainManager : Node2D
{
    // [ExportCategory("Train Generate")]
    // [ExportToolButton("Check to generate a train")] public Callable generate_button => Callable.From(TrainGenerate);

    // [Export] public PackedScene train_scene;
    // [Export] public Path2D train_path;

    private void TrainGenerate()
    {
        // AddChild(train_scene.Instantiate());
        // Print("Train Generated!");
    }

    public override void _Ready()
    {
        
    }
}
