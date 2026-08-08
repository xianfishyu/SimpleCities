using Godot;
using System;
using System.Text.Json;


public partial class MainCamera : Camera2D, IPreparedSaveable
{
	private const float MinimumZoomScale = 0.000001f;
	private const float ResponseRemainingAtConfiguredTime = 0.05f;
	private const float MinimumResponseTime = 0.001f;
	private const float ScreenVelocityStopThreshold = 0.01f;

	[ExportGroup("Zoom")]
	[ExportSubgroup("Wheel Target and Limits")]
	// Target zoom saved with the camera state.
	[Export(PropertyHint.Range, "0.000001, 16, 0.000001, or_greater, exp")]
	private float defaultScale = 1f;
	// Relative zoom change applied by one wheel step.
	[Export(PropertyHint.Range, "0.01, 1, 0.01")]
	public float scaleFactor = 0.125f;
	// Lowest zoom allowed after wheel input.
	[Export(PropertyHint.Range, "0.000001, 16, 0.000001, or_greater, exp")]
	public float minScale = 0.125f;
	// Highest zoom allowed after wheel input.
	[Export(PropertyHint.Range, "0.000001, 16, 0.000001, or_greater, exp")]
	public float maxScale = 4f;

	[ExportSubgroup("Smoothing")]
	// Smoothing weight at the reference frame rate.
	[Export(PropertyHint.Range, "0.01, 0.99, 0.01")]
	private float smoothing = 0.25f;
	// Frame rate used to convert the smoothing weight to elapsed time.
	[Export(PropertyHint.Range, "1, 240, 1, or_greater")]
	private float referenceFps = 60f;

	[ExportGroup("Keyboard Movement")]
	[ExportSubgroup("Speed")]
	private Vector2 moveInput = new();
	private Vector2 screenVelocity = new();
	// Base keyboard movement speed in screen pixels per second at zoom 1.
	[Export(PropertyHint.Range, "1, 3000, 1, or_greater")]
	private float panSpeed = 2048f;
	// Blend between constant world speed (0) and constant screen speed (1).
	[Export(PropertyHint.Range, "0, 1, 0.05")]
	private float zoomInfluence = 0.75f;

	[ExportSubgroup("Response")]
	// Seconds to reach about 95% of the requested movement speed.
	[Export(PropertyHint.Range, "0.01, 0.5, 0.01, or_greater")]
	private float accelerationTime = 0.175f;
	// Seconds to remove about 95% of movement speed after release.
	[Export(PropertyHint.Range, "0.01, 0.5, 0.01, or_greater")]
	private float decelerationTime = 0.175f;

	private Vector2 mousePos = new();
	private Vector2 zoomAnchorViewportPosition = new();
	private bool isMiddleDragging;
	private bool hasZoomAnchor;

	public static MainCamera Instance { get; private set; } = null!;

	public override void _Ready()
	{
		NormalizeZoomConfiguration();
		Instance = this;
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
		ScaleUpdate(delta);
		KeyPosUpdate(delta);
		MousePosUpdate();
	}

	private void KeyPosUpdate(double delta)
	{
		if (isMiddleDragging)
			return;

		float elapsed = (float)delta;
		float currentZoom = Mathf.Max(Zoom.X, MinimumZoomScale);
		float zoomSpeedFactor = Mathf.Pow(currentZoom, 1f - zoomInfluence);
		Vector2 targetScreenVelocity = moveInput * panSpeed * zoomSpeedFactor;
		float responseTime = targetScreenVelocity.IsZeroApprox() ? decelerationTime : accelerationTime;
		float responseRate = -Mathf.Log(ResponseRemainingAtConfiguredTime) /
			Mathf.Max(responseTime, MinimumResponseTime);
		float responseDecay = Mathf.Exp(-responseRate * elapsed);
		Vector2 screenDisplacement = targetScreenVelocity * elapsed +
			(screenVelocity - targetScreenVelocity) * (1f - responseDecay) / responseRate;

		screenVelocity = targetScreenVelocity +
			(screenVelocity - targetScreenVelocity) * responseDecay;
		if (targetScreenVelocity.IsZeroApprox() &&
			screenVelocity.LengthSquared() <= ScreenVelocityStopThreshold * ScreenVelocityStopThreshold)
		{
			screenVelocity = Vector2.Zero;
		}

		Position += screenDisplacement / currentZoom;
	}

	private void ScaleUpdate(double delta)
	{
		float smoothingWeight = 1f - Mathf.Pow(
			1f - smoothing,
			(float)delta * referenceFps);
		Vector2 previousZoom = Zoom;
		Vector2 nextZoom = Zoom.Lerp(new(defaultScale, defaultScale), smoothingWeight);
		bool reachedTarget = Mathf.IsEqualApprox(nextZoom.X, defaultScale);

		if (reachedTarget)
			nextZoom = new(defaultScale, defaultScale);

		if (hasZoomAnchor && !previousZoom.IsEqualApprox(nextZoom))
		{
			Rect2 viewportRect = GetViewport().GetVisibleRect();
			Vector2 viewportCenter = viewportRect.Position + viewportRect.Size * 0.5f;
			Vector2 anchorOffset = zoomAnchorViewportPosition - viewportCenter;
			Position += anchorOffset * (1f / previousZoom.X - 1f / nextZoom.X);
		}

		Zoom = nextZoom;
		if (reachedTarget)
			hasZoomAnchor = false;
	}

	private void NormalizeZoomConfiguration()
	{
		minScale = float.IsFinite(minScale) ? Mathf.Max(minScale, MinimumZoomScale) : MinimumZoomScale;
		maxScale = float.IsFinite(maxScale) ? Mathf.Max(maxScale, minScale) : minScale;
		defaultScale = float.IsFinite(defaultScale) ? ClampZoom(defaultScale) : minScale;
	}

	private float ClampZoom(float scale)
	{
		return Mathf.Clamp(scale, minScale, maxScale);
	}

	private void SetZoomTarget(float scale, Vector2 anchorViewportPosition)
	{
		float nextScale = ClampZoom(scale);
		if (Mathf.IsEqualApprox(nextScale, defaultScale))
			return;

		defaultScale = nextScale;
		zoomAnchorViewportPosition = anchorViewportPosition;
		hasZoomAnchor = true;
	}

	private void MousePosUpdate()
	{
		if (isMiddleDragging)
		{
			Vector2 deltaPos = mousePos - GetGlobalMousePosition();

			Position += deltaPos;
		}
	}

	private void SetMiddleDragging(bool pressed)
	{
		isMiddleDragging = pressed;
		if (isMiddleDragging)
		{
			mousePos = GetGlobalMousePosition();
			moveInput = Vector2.Zero;
			screenVelocity = Vector2.Zero;
			return;
		}

		moveInput = Input.GetVector(
			InputBindingManager.CameraMoveLeftAction,
			InputBindingManager.CameraMoveRightAction,
			InputBindingManager.CameraMoveUpAction,
			InputBindingManager.CameraMoveDownAction);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseEvent)
		{
			switch (mouseEvent.ButtonIndex)
			{
				case MouseButton.WheelUp:
					if (!mouseEvent.Pressed)
						return;
					SetZoomTarget(defaultScale * (1f + scaleFactor), mouseEvent.Position);
					break;
				case MouseButton.WheelDown:
					if (!mouseEvent.Pressed)
						return;
					SetZoomTarget(defaultScale * (1f - scaleFactor), mouseEvent.Position);
					break;
				case MouseButton.Middle:
					SetMiddleDragging(mouseEvent.Pressed);
					break;
			}
			return;
		}

		if (Input.IsMouseButtonPressed(MouseButton.Middle))
			return;

		moveInput = Input.GetVector(
			InputBindingManager.CameraMoveLeftAction,
			InputBindingManager.CameraMoveRightAction,
			InputBindingManager.CameraMoveUpAction,
			InputBindingManager.CameraMoveDownAction);
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
		RestorePreparedState(PrepareRestoreState(json));
	}

	public object PrepareRestoreState(string json)
	{
		CameraData? data = SaveJson.Deserialize<CameraData>(json);
		if (data == null || !float.IsFinite(data.PositionX) || !float.IsFinite(data.PositionY) ||
			!float.IsFinite(data.Zoom) || data.Zoom <= 0f)
		{
			throw new JsonException("Camera save payload must contain finite coordinates and a positive zoom.");
		}

		return data;
	}

	public void RestorePreparedState(object preparedState)
	{
		if (preparedState is not CameraData data)
			throw new ArgumentException("Prepared state is not camera data.", nameof(preparedState));

		Position = new Vector2(data.PositionX, data.PositionY);
		screenVelocity = Vector2.Zero;
		hasZoomAnchor = false;
		defaultScale = ClampZoom(data.Zoom);
	}

}
