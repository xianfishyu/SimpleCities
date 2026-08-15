using System.IO;
using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3FileLockTests
{
    [Fact]
    public void TryAcquire_ThenSecondLockFails()
    {
        string path = GetTempLockPath();
        try
        {
            using var first = new V3FileLock();
            using var second = new V3FileLock();

            Assert.True(first.TryAcquire(path));
            Assert.True(first.IsHeld);
            Assert.False(second.TryAcquire(path));
            Assert.False(second.IsHeld);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void Dispose_AllowsNextAcquire()
    {
        string path = GetTempLockPath();
        try
        {
            var first = new V3FileLock();
            Assert.True(first.TryAcquire(path));
            first.Dispose();

            using var second = new V3FileLock();
            Assert.True(second.TryAcquire(path));
        }
        finally
        {
            Cleanup(path);
        }
    }

    private static string GetTempLockPath() =>
        Path.Combine(Path.GetTempPath(), $"v3-lock-{Guid.NewGuid():N}.lock");

    private static void Cleanup(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // best-effort cleanup
        }
    }
}
