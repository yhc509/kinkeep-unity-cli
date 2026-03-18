using UnityCli.Cli.Services;
using UnityCli.Protocol;

namespace UnityCli.Cli.Tests;

public sealed class InstanceRegistryStoreTests
{
    [Fact]
    public void ResolveOrCreateTarget_CreatesOfflineEntryFromProjectPath()
    {
        using var temp = new TempDirectory();
        var projectRoot = System.IO.Path.Combine(temp.Path, "ProjectA");
        Directory.CreateDirectory(projectRoot);

        var store = new InstanceRegistryStore(System.IO.Path.Combine(temp.Path, "instances.json"));
        var registry = new InstanceRegistry();
        var target = store.ResolveOrCreateTarget(registry, projectRoot, null);

        Assert.Equal(ProtocolConstants.GetCanonicalPath(projectRoot), target.projectRoot);
        Assert.Single(registry.instances);
        Assert.Equal(target.projectHash, registry.instances[0].projectHash);
        Assert.Equal(ProtocolConstants.BuildPipeName(target.projectHash), target.pipeName);
    }

    [Fact]
    public void Load_RemovesMissingProjectsAndPromotesLiveInstance()
    {
        using var temp = new TempDirectory();
        var existingProject = Path.Combine(temp.Path, "ProjectA");
        Directory.CreateDirectory(existingProject);

        var registryPath = Path.Combine(temp.Path, "instances.json");
        var store = new InstanceRegistryStore(registryPath);
        store.Save(new InstanceRegistry
        {
            activeProjectHash = "missing",
            instances =
            [
                new InstanceRecord
                {
                    projectRoot = Path.Combine(temp.Path, "MissingProject"),
                    projectName = "MissingProject",
                    projectHash = "missing",
                    pipeName = "missing.sock",
                    state = "offline",
                    lastSeenUtc = DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O"),
                },
                new InstanceRecord
                {
                    projectRoot = existingProject,
                    projectName = "ProjectA",
                    projectHash = ProtocolConstants.ComputeProjectHash(existingProject),
                    pipeName = ProtocolConstants.BuildPipeName(ProtocolConstants.ComputeProjectHash(existingProject)),
                    state = "idle",
                    lastSeenUtc = DateTimeOffset.UtcNow.ToString("O"),
                },
            ],
        });

        var registry = store.Load();

        Assert.Single(registry.instances);
        Assert.Equal("ProjectA", registry.instances[0].projectName);
        Assert.Equal(registry.instances[0].projectHash, registry.activeProjectHash);
    }
}
