using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3FileSetValidatorTests
{
    [Fact]
    public void Validate_ExactMatch_Succeeds()
    {
        var declared = new[] { new V3ManifestFile("road_network.json", 1, new string('a', 64)) };
        var actual = new HashSet<string> { "road_network.json" };

        V3FileSetValidationResult result = V3FileSetValidator.Validate(declared, actual);

        Assert.True(result.Success, result.Error);
    }

    [Fact]
    public void Validate_MissingDeclaredFile_Fails()
    {
        var declared = new[] { new V3ManifestFile("road_network.json", 1, new string('a', 64)) };
        var actual = new HashSet<string>();

        V3FileSetValidationResult result = V3FileSetValidator.Validate(declared, actual);

        Assert.False(result.Success);
        Assert.StartsWith("MissingDeclaredFile:", result.Error);
    }

    [Fact]
    public void Validate_UndeclaredFile_Fails()
    {
        var declared = Array.Empty<V3ManifestFile>();
        var actual = new HashSet<string> { "extra.json" };

        V3FileSetValidationResult result = V3FileSetValidator.Validate(declared, actual);

        Assert.False(result.Success);
        Assert.StartsWith("UndeclaredFile:", result.Error);
    }
}
