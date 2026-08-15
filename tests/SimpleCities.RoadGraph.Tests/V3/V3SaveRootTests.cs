using SimpleCities.Core.V3;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3SaveRootTests
{
    [Fact]
    public void EditorAndExportRootsUseUserSavesV3()
    {
        Assert.Equal("user://saves-v3", V3SaveRoot.EditorRoot);
        Assert.Equal("user://saves-v3", V3SaveRoot.ExportRoot);
    }

    [Fact]
    public void GetRootReturnsSameForBothModes()
    {
        Assert.Equal(V3SaveRoot.EditorRoot, V3SaveRoot.GetRoot(isExport: false));
        Assert.Equal(V3SaveRoot.ExportRoot, V3SaveRoot.GetRoot(isExport: true));
    }

    [Fact]
    public void IsV2Root_DetectsV2Roots()
    {
        Assert.True(V3SaveRoot.IsV2Root(V3SaveRoot.V2EditorRoot));
        Assert.True(V3SaveRoot.IsV2Root(V3SaveRoot.V2ExportRoot));
        Assert.False(V3SaveRoot.IsV2Root(V3SaveRoot.EditorRoot));
    }

    [Fact]
    public void FormatConstantsMatchCodec()
    {
        Assert.Equal(RoadGraphV3Codec.FormatFamily, V3SaveRoot.FormatFamily);
        Assert.Equal(RoadGraphV3Codec.SchemaVersion, V3SaveRoot.SchemaVersion);
    }
}
