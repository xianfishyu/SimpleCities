using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadRenderTokenTests
{
    [Fact]
    public void IsValid_ValidToken_ReturnsTrue()
    {
        RoadRenderToken token = CreateToken();

        Assert.True(token.IsValid);
    }

    [Fact]
    public void IsValid_NegativeChangeSequence_ReturnsFalse()
    {
        RoadRenderToken token = CreateToken() with { ChangeSequence = -1 };

        Assert.False(token.IsValid);
    }

    [Fact]
    public void Matches_SameToken_ReturnsTrue()
    {
        RoadRenderToken token = CreateToken();

        Assert.True(token.Matches(token));
    }

    [Fact]
    public void Matches_DifferentRequestID_ReturnsFalse()
    {
        RoadRenderToken token = CreateToken();
        RoadRenderToken other = token with { RenderRequestID = token.RenderRequestID + 1 };

        Assert.False(token.Matches(other));
    }

    private static RoadRenderToken CreateToken() =>
        new(SceneGeneration: 1, GraphFacadeID: 2, GraphFacadeGeneration: 3, ChangeSequence: 4, RoadStyleRevision: 5, RenderRequestID: 6);
}
