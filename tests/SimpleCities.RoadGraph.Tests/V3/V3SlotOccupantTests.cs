using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3SlotOccupantTests
{
    [Fact]
    public void Classify_DirectoryWithValidManifestAndPayloads_ReturnsCompleteV3()
    {
        Assert.Equal(
            V3SlotOccupant.CompleteV3,
            V3SlotClassifier.Classify(isDirectory: true, manifestDeclaresV3: true, manifestValid: true, payloadsValid: true));
    }

    [Fact]
    public void Classify_DirectoryWithV3ManifestButInvalidPayloads_ReturnsCorruptV3()
    {
        Assert.Equal(
            V3SlotOccupant.CorruptV3,
            V3SlotClassifier.Classify(isDirectory: true, manifestDeclaresV3: true, manifestValid: true, payloadsValid: false));
    }

    [Fact]
    public void Classify_DirectoryWithInvalidV3Manifest_ReturnsCorruptV3()
    {
        Assert.Equal(
            V3SlotOccupant.CorruptV3,
            V3SlotClassifier.Classify(isDirectory: true, manifestDeclaresV3: true, manifestValid: false, payloadsValid: false));
    }

    [Fact]
    public void Classify_DirectoryWithoutManifest_ReturnsForeign()
    {
        Assert.Equal(
            V3SlotOccupant.Foreign,
            V3SlotClassifier.Classify(isDirectory: true, manifestDeclaresV3: false, manifestValid: false, payloadsValid: false));
    }

    [Fact]
    public void Classify_File_ReturnsUnsafe()
    {
        Assert.Equal(
            V3SlotOccupant.Unsafe,
            V3SlotClassifier.Classify(isDirectory: false, manifestDeclaresV3: false, manifestValid: false, payloadsValid: false));
    }
}
