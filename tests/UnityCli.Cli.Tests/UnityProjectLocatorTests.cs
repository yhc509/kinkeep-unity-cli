using UnityCli.Cli.Services;
using UnityCli.Protocol;

namespace UnityCli.Cli.Tests;

public sealed class UnityProjectLocatorTests
{
    [Fact]
    public void TryFindProjectRoot_FindsImmediateChildProject()
    {
        using var temp = new TempDirectory();
        var repoRoot = temp.Path;
        var projectRoot = System.IO.Path.Combine(repoRoot, "My project");

        Directory.CreateDirectory(System.IO.Path.Combine(projectRoot, "Assets"));
        Directory.CreateDirectory(System.IO.Path.Combine(projectRoot, "Packages"));
        Directory.CreateDirectory(System.IO.Path.Combine(projectRoot, "ProjectSettings"));

        var locator = new UnityProjectLocator();
        var detected = locator.TryFindProjectRoot(repoRoot);

        Assert.Equal(ProtocolConstants.GetCanonicalPath(projectRoot), detected);
    }
}
