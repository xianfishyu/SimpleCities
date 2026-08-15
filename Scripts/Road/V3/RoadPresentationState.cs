namespace SimpleCities.Road.V3;

/// <summary>
/// 道路表现接管状态：保存 desired/presented token 与已呈现快照，
/// 只允许与 desired 完全匹配的后台结果发布。
/// </summary>
public sealed class RoadPresentationState
{
    public RoadRenderToken DesiredToken { get; private set; }
    public RoadRenderToken PresentedToken { get; private set; }
    public RoadSurfaceSnapshot? PresentedSnapshot { get; private set; }

    public bool IsStalled => DesiredToken != PresentedToken;

    public RoadPresentationState(RoadRenderToken initialToken)
    {
        DesiredToken = initialToken;
        PresentedToken = initialToken;
    }

    public void SetDesired(RoadRenderToken token) => DesiredToken = token;

    public bool TryPublish(RoadRenderToken resultToken, RoadSurfaceSnapshot snapshot)
    {
        if (!resultToken.Matches(DesiredToken) || !snapshot.IsValid)
            return false;

        PresentedToken = resultToken;
        PresentedSnapshot = snapshot;
        return true;
    }
}
