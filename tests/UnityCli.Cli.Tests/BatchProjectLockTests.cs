using UnityCli.Cli.Services;

namespace UnityCli.Cli.Tests;

public sealed class BatchProjectLockTests
{
    [Fact]
    public async Task AcquireAsync_TimesOut_WhenSameProjectLockIsAlreadyHeld()
    {
        using var temp = new TempDirectory();
        var projectRoot = Path.Combine(temp.Path, "ProjectA");
        Directory.CreateDirectory(projectRoot);

        using var firstLock = await BatchProjectLock.AcquireAsync(projectRoot, 200);
        await Assert.ThrowsAsync<TimeoutException>(() => BatchProjectLock.AcquireAsync(projectRoot, 150));
    }
}
