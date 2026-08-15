using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3SaveRootLockTests
{
    [Fact]
    public void TryGetLockPath_ReturnsExpectedPath()
    {
        Assert.True(V3SaveRootLock.TryGetLockPath("user://saves-v3", out string path));
        Assert.Equal("user://saves-v3/.save-root.lock", path);
    }

    [Fact]
    public void TryGetLockPath_RejectsEmptyRoot()
    {
        Assert.False(V3SaveRootLock.TryGetLockPath("", out _));
        Assert.False(V3SaveRootLock.TryGetLockPath(null!, out _));
    }

    [Fact]
    public void TryGetLockPath_TrimsTrailingSlash()
    {
        Assert.True(V3SaveRootLock.TryGetLockPath("user://saves-v3/", out string path));
        Assert.Equal("user://saves-v3/.save-root.lock", path);
    }
}
