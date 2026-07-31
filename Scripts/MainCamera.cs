using Godot;
using System;
using System.Text.Json;


public partial class MainCamera : Camera2D, ISaveable
{
	[Export] private float defaultScale = 1f;
	[Export] public float scaleFactor = 0.125f;
	[Export] public float minScale = 0.125f;
	[Export] public float maxScale = 4f;


	private Vector2 moveInput = new();
	private Vector2 nextPos = new();
	[Export] public float keyMoveFactor = 10f;
	[Export] public float moveSpeed = 1.25f;

	private Vector2 mousePos = new();

	public static MainCamera Instance { get; private set; } = null!;

	public override void _Ready()
	{
		Instance = this;
		nextPos = Position;
		SaveManager.Instance.Register(this);
	}

	public override void _ExitTree()
	{
		if (SaveManager.Instance != null && GodotObject.IsInstanceValid(SaveManager.Instance))
			SaveManager.Instance.Unregister(this);
		if (ReferenceEquals(Instance, this))
			Instance = null!;
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
			moveInput = Input.GetVector(
				InputBindingManager.CameraMoveLeftAction,
				InputBindingManager.CameraMoveRightAction,
				InputBindingManager.CameraMoveUpAction,
				InputBindingManager.CameraMoveDownAction);

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

	// ═══════════════════════════════════════════════
	// ISaveable 实现
	// ═══════════════════════════════════════════════

	public string SaveFileName => "camera";

	public object CaptureState()
	{
		return new CameraData
		{
			PositionX = Position.X,
			PositionY = Position.Y,
			Zoom = defaultScale
		};
	}

	public void RestoreState(string json)
	{
		var data = SaveJson.Deserialize<CameraData>(json);
		Position = new Vector2(data.PositionX, data.PositionY);
		nextPos = Position;
		defaultScale = data.Zoom;
	}

}
