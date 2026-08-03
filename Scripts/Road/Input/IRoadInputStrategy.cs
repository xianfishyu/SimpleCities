using Godot;

/// <summary>把指针轨迹投影为可预览、可提交道路路径的输入策略。</summary>
public interface IRoadInputStrategy
{
    float InteractionRadius { get; }

    Vector2 SnapPointer(Vector2 worldPosition);

    RoadPathDraft BuildDraft(Vector2 startPosition, Vector2 pointerPosition);
}
