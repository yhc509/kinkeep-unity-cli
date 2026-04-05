using UnityCli.Cli.Services;
using UnityCli.Protocol;

namespace UnityCli.Cli.Tests;

[Collection(CurrentDirectoryCollection.Name)]
public sealed class InstanceRegistryFileTests
{
    [Fact]
    public async Task Load_RetriesWhileRegistryFileIsTemporarilyLocked()
    {
        using var temp = new TempDirectory();
        string registryPath = Path.Combine(temp.Path, "instances.json");
        File.WriteAllText(
            registryPath,
            "{\"activeProjectHash\":\"abc\",\"instances\":[{\"projectRoot\":\"C:/Project\",\"projectName\":\"Project\",\"projectHash\":\"abc\",\"pipeName\":\"unity-cli-abc\",\"state\":\"idle\",\"lastSeenUtc\":\"2026-04-05T00:00:00Z\"}]}");

        using var lockStream = new FileStream(registryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Task releaseLockTask = Task.Run(async () =>
        {
            await Task.Delay(150);
            lockStream.Dispose();
        });

        InstanceRegistry registry = InstanceRegistryFile.Load(registryPath);
        await releaseLockTask;

        Assert.Equal("abc", registry.activeProjectHash);
        Assert.Single(registry.instances);
    }

    [Fact]
    public async Task Save_RetriesWhileRegistryFileIsTemporarilyLocked()
    {
        using var temp = new TempDirectory();
        string registryPath = Path.Combine(temp.Path, "instances.json");
        File.WriteAllText(registryPath, "{\"instances\":[]}");

        using var lockStream = new FileStream(registryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Task releaseLockTask = Task.Run(async () =>
        {
            await Task.Delay(150);
            lockStream.Dispose();
        });

        var store = new InstanceRegistryStore(registryPath);
        store.Save(new InstanceRegistry
        {
            activeProjectHash = "abc",
            instances =
            [
                new InstanceRecord
                {
                    projectRoot = "C:/Project",
                    projectName = "Project",
                    projectHash = "abc",
                    pipeName = "unity-cli-abc",
                    state = "idle",
                    lastSeenUtc = "2026-04-05T00:00:00Z",
                },
            ],
        });
        await releaseLockTask;

        InstanceRegistry registry = InstanceRegistryFile.Load(registryPath);
        Assert.Equal("abc", registry.activeProjectHash);
        Assert.Single(registry.instances);
    }
}
