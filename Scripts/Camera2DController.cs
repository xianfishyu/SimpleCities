using Godot;
using System;


public partial class Camera2DController : Camera2D
{
	[Export]private float defaultScale = 1f;
	[Export] public float scaleFactor = 0.125f;
	[Export] public float minScale = 0.125f;
	[Export] public float maxScale = 4f;


	private Vector2 moveInput = new();
	private Vector2 nextPos = new();
	[Export] public float keyMoveFactor = 10f;
	[Export] public float moveSpeed = 1.25f;

	private Vector2 mousePos = new();

	public override void _Ready()
	{
		nextPos = Position;
	}

	public override void _Process(double delta)
	{
		KeyPosUpdate();
		ScaleUpdate();
		MousePosUpdate();
	}

	private void KeyPosUpdate()
	{
		nextPos += Mathf.Pow(2, -defaultScale) * keyMoveFactor * moveSpeed * moveInput;
		Position = Position.Lerp(nextPos, 0.1f);
	}

	private void ScaleUpdate()
	{
		Zoom = Zoom.Lerp(new(defaultScale, defaultScale), 0.1f);
	}

	private void MousePosUpdate()
	{
		if (Input.IsMouseButtonPressed(MouseButton.Middle))
		{
			Vector2 deltaPos = mousePos - GetGlobalMousePosition();

			Position += deltaPos;
			nextPos = Position;
		}
	}

	public override void _Input(InputEvent @event)
	{
		//WASD
		if (Input.IsMouseButtonPressed(MouseButton.Middle) == false)
			moveInput = Input.GetVector("KeyBoard_MoveLeft", "KeyBoard_MoveRight", "KeyBoard_MoveUp", "KeyBoard_MoveDown");

		//MouseWheel
		if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
		{
			switch (mouseEvent.ButtonIndex)
			{
				case MouseButton.WheelUp:
					defaultScale += scaleFactor * defaultScale;
					defaultScale = Mathf.Min(Mathf.Max(defaultScale, minScale), maxScale);
					break;
				case MouseButton.WheelDown:
					defaultScale -= scaleFactor * defaultScale;
					defaultScale = Mathf.Min(Mathf.Max(defaultScale, minScale), maxScale);
					break;
				case MouseButton.Middle:
					mousePos = GetGlobalMousePosition();
					break;
			}
		}
	}

}
