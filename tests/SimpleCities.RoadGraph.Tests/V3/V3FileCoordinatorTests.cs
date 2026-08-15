using System.IO;
using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3FileCoordinatorTests
{
    [Fact]
    public void TryAcquire_ThenSecondCoordinatorFails()
    {
        string root = GetTempRoot();
        Directory.CreateDirectory(root);
        try
        {
            using var first = new V3FileCoordinator();
            using var second = new V3FileCoordinator();

            Assert.True(first.TryAcquire(root, out _));
            Assert.False(second.TryAcquire(root, out _));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Release_AllowsNextAcquire()
    {
        string root = GetTempRoot();
        Directory.CreateDirectory(root);
        try
        {
            var first = new V3FileCoordinator();
            Assert.True(first.TryAcquire(root, out _));
            first.Release();

            using var second = new V3FileCoordinator();
            Assert.True(second.TryAcquire(root, out _));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void TryAcquire_WithInvalidRoot_Fails()
    {
        using var coordinator = new V3FileCoordinator();

        Assert.False(coordinator.TryAcquire("", out _));
    }

    private static string GetTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"v3-fcoord-{Guid.NewGuid():N}");

    private static void Cleanup(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch (IOException)
        {
            // best-effort cleanup
        }
    }
}
