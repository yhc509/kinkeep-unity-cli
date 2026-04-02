using UnityCli.Cli.Models;
using UnityCli.Protocol;

namespace UnityCli.Cli.Services;

public sealed class InstanceRegistryStore
{
    private readonly string _registryPath;

    public InstanceRegistryStore()
        : this(RegistryPathUtility.GetRegistryFilePath())
    {
    }

    public InstanceRegistryStore(string registryPath)
    {
        _registryPath = registryPath;
    }

    public InstanceRegistry Load()
    {
        if (!File.Exists(_registryPath))
        {
            return new InstanceRegistry();
        }

        var json = File.ReadAllText(_registryPath);
        var registry = ProtocolJson.Deserialize<InstanceRegistry>(json) ?? new InstanceRegistry();
        registry.instances ??= Array.Empty<InstanceRecord>();
        return Sanitize(registry);
    }

    public void Save(InstanceRegistry registry)
    {
        registry.instances ??= Array.Empty<InstanceRecord>();
        var json = ProtocolJson.Serialize(registry);
        File.WriteAllText(_registryPath, json);
    }

    private static string? ResolveProjectRootByName(InstanceRegistry registry, string projectName)
    {
        if (string.IsNullOrWhiteSpace(projectName))
        {
            return null;
        }

        var trimmedProjectName = projectName.Trim();
        registry.instances ??= Array.Empty<InstanceRecord>();

        var matches = registry.instances
            .Where(item => string.Equals(item.projectName, trimmedProjectName, StringComparison.OrdinalIgnoreCase))
            .Select(item => ProtocolConstants.GetCanonicalPath(item.projectRoot))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (matches.Length == 0)
        {
            return null;
        }

        if (matches.Length > 1)
        {
            throw CreateAmbiguousProjectNameException(trimmedProjectName, matches);
        }

        return matches[0];
    }

    private static string? TryResolveProjectRootOverride(InstanceRegistry registry, string input)
    {
        var trimmed = input.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        if (Directory.Exists(trimmed))
        {
            return ProtocolConstants.GetCanonicalPath(trimmed);
        }

        return ResolveProjectRootByName(registry, trimmed);
    }

    private static CliUsageException CreateUnknownProjectOverrideException(string input)
    {
        return new CliUsageException(
            $"'{input}' is not a registered project name or a valid directory path. Run 'unity-cli instances list' to see registered projects.");
    }

    private static CliUsageException CreateUnknownInstanceTargetException(string input)
    {
        return new CliUsageException(
            $"'{input}' is not a known project hash, a registered project name, or a valid directory path. Run 'unity-cli instances list' to see registered projects.");
    }

    public InstanceRecord ResolveOrCreateTarget(InstanceRegistry registry, string input)
    {
        registry.instances ??= Array.Empty<InstanceRecord>();

        var trimmed = input.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new CliUsageException("project hash, project path 또는 project name이 필요합니다.");
        }

        if (trimmed.Length == 12 && !trimmed.Contains(Path.DirectorySeparatorChar) && !trimmed.Contains(Path.AltDirectorySeparatorChar))
        {
            var existing = registry.instances.FirstOrDefault(item => item.projectHash == trimmed);
            if (existing is not null)
            {
                return existing;
            }
        }

        var projectRoot = TryResolveProjectRootOverride(registry, trimmed);
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            throw CreateUnknownInstanceTargetException(trimmed);
        }

        var projectHash = ProtocolConstants.ComputeProjectHash(projectRoot);
        var match = registry.instances.FirstOrDefault(item => item.projectHash == projectHash);
        if (match is not null)
        {
            match.projectRoot = projectRoot;
            match.projectName = string.IsNullOrWhiteSpace(match.projectName) ? Path.GetFileName(projectRoot) : match.projectName;
            match.projectHash = projectHash;
            match.pipeName = string.IsNullOrWhiteSpace(match.pipeName) ? ProtocolConstants.BuildPipeName(projectHash) : match.pipeName;
            return match;
        }

        var created = new InstanceRecord
        {
            projectRoot = projectRoot,
            projectName = Path.GetFileName(projectRoot),
            projectHash = projectHash,
            pipeName = ProtocolConstants.BuildPipeName(projectHash),
            state = "offline",
            lastSeenUtc = DateTimeOffset.UtcNow.ToString("O"),
        };

        registry.instances = registry.instances.Append(created).ToArray();
        return created;
    }

    public string ResolveProjectRootOverride(InstanceRegistry registry, string input)
    {
        var trimmed = input.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new CliUsageException("project path 또는 project name이 필요합니다.");
        }

        return TryResolveProjectRootOverride(registry, trimmed)
            ?? throw CreateUnknownProjectOverrideException(trimmed);
    }

    private static InstanceRegistry Sanitize(InstanceRegistry registry)
    {
        var changed = false;
        var instancesByHash = new Dictionary<string, InstanceRecord>(StringComparer.OrdinalIgnoreCase);

        foreach (var instance in registry.instances)
        {
            if (string.IsNullOrWhiteSpace(instance.projectRoot) || !Directory.Exists(instance.projectRoot))
            {
                changed = true;
                continue;
            }

            var projectRoot = ProtocolConstants.GetCanonicalPath(instance.projectRoot);
            var projectHash = ProtocolConstants.ComputeProjectHash(projectRoot);
            var normalized = new InstanceRecord
            {
                projectRoot = projectRoot,
                projectName = string.IsNullOrWhiteSpace(instance.projectName) ? Path.GetFileName(projectRoot) : instance.projectName,
                projectHash = projectHash,
                pipeName = string.IsNullOrWhiteSpace(instance.pipeName) ? ProtocolConstants.BuildPipeName(projectHash) : instance.pipeName,
                editorProcessId = instance.editorProcessId,
                unityVersion = instance.unityVersion ?? string.Empty,
                state = instance.state ?? "offline",
                lastSeenUtc = instance.lastSeenUtc ?? string.Empty,
                capabilities = instance.capabilities ?? Array.Empty<string>(),
            };

            if (IsStale(normalized))
            {
                normalized.state = "offline";
                normalized.editorProcessId = 0;
                changed = true;
            }

            if (!instancesByHash.TryGetValue(normalized.projectHash, out var existing)
                || CompareLastSeen(normalized.lastSeenUtc, existing.lastSeenUtc) >= 0)
            {
                instancesByHash[normalized.projectHash] = normalized;
            }
        }

        var instances = instancesByHash.Values
            .OrderBy(item => item.projectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.projectRoot, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (instances.Length != registry.instances.Length)
        {
            changed = true;
        }

        registry.instances = instances;
        if (string.IsNullOrWhiteSpace(registry.activeProjectHash)
            || registry.instances.All(item => !string.Equals(item.projectHash, registry.activeProjectHash, StringComparison.OrdinalIgnoreCase))
            || registry.instances.FirstOrDefault(item => string.Equals(item.projectHash, registry.activeProjectHash, StringComparison.OrdinalIgnoreCase))?.state == "offline")
        {
            registry.activeProjectHash = registry.instances.FirstOrDefault(item => item.state != "offline")?.projectHash
                ?? registry.instances.FirstOrDefault()?.projectHash;
            changed = true;
        }

        if (changed)
        {
            registry.instances ??= Array.Empty<InstanceRecord>();
        }

        return registry;
    }

    private static bool IsStale(InstanceRecord record)
    {
        if (!DateTimeOffset.TryParse(record.lastSeenUtc, out var lastSeen))
        {
            return false;
        }

        var maxAgeSeconds = ProtocolConstants.RegistryHeartbeatSeconds * 3;
        if ((DateTimeOffset.UtcNow - lastSeen).TotalSeconds <= maxAgeSeconds)
        {
            return false;
        }

        if (record.editorProcessId > 0 && IsProcessAlive(record.editorProcessId))
        {
            return false;
        }

        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(record.pipeName) || !File.Exists(record.pipeName);
    }

    private static bool IsProcessAlive(int processId)
    {
        try
        {
            var process = System.Diagnostics.Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static int CompareLastSeen(string? left, string? right)
    {
        var leftParsed = DateTimeOffset.TryParse(left, out var leftValue);
        var rightParsed = DateTimeOffset.TryParse(right, out var rightValue);

        return (leftParsed, rightParsed) switch
        {
            (true, true) => leftValue.CompareTo(rightValue),
            (true, false) => 1,
            (false, true) => -1,
            _ => 0,
        };
    }

    private static CliUsageException CreateAmbiguousProjectNameException(string projectName, string[] candidatePaths)
    {
        return new CliUsageException(
            $"등록된 프로젝트 이름이 중복되어 대상을 결정할 수 없습니다: {projectName}. project path를 사용하세요. 후보: {string.Join(", ", candidatePaths)}");
    }
}
