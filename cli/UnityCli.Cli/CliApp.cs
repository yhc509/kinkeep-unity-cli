using System.Text.Json;
using UnityCli.Cli.Models;
using UnityCli.Cli.Services;
using UnityCli.Protocol;

namespace UnityCli.Cli;

public static class CliApp
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var parsed = CliArgumentParser.Parse(args);
            if (parsed.Kind == CommandKind.Help)
            {
                Console.WriteLine(CliArgumentParser.BuildHelpText());
                return 0;
            }

            var registryStore = new InstanceRegistryStore();
            var locator = new UnityProjectLocator();
            var projectRoot = ResolveProjectRoot(parsed, locator);

            var response = parsed.Kind switch
            {
                CommandKind.Status => await RunStatusAsync(registryStore, projectRoot),
                CommandKind.InstancesList => ListInstances(registryStore, projectRoot),
                CommandKind.InstancesUse => UseInstance(registryStore, parsed, projectRoot),
                CommandKind.Doctor => await RunDoctorAsync(registryStore, locator, parsed, projectRoot),
                _ => await ExecuteUnityCommandAsync(parsed, registryStore, projectRoot),
            };

            Console.WriteLine(ResponseFormatter.Format(parsed, response));
            return response.status == "success" ? 0 : 1;
        }
        catch (CliUsageException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine();
            Console.Error.WriteLine(CliArgumentParser.BuildHelpText());
            return 2;
        }
        catch (Exception ex)
        {
            var response = ResponseEnvelope.Failure(
                Guid.NewGuid().ToString("N"),
                null,
                "CLI_ERROR",
                ex.Message,
                retryable: false,
                details: ex.ToString(),
                transport: "cli");
            Console.Error.WriteLine(ResponseFormatter.Format(new ParsedCommand(CommandKind.Help), response));
            return 1;
        }
    }

    private static string? ResolveProjectRoot(ParsedCommand parsed, UnityProjectLocator locator)
    {
        if (!string.IsNullOrWhiteSpace(parsed.ProjectOverride))
        {
            return ProtocolConstants.GetCanonicalPath(parsed.ProjectOverride);
        }

        return locator.TryFindProjectRoot(Environment.CurrentDirectory);
    }

    private static ResponseEnvelope ListInstances(InstanceRegistryStore registryStore, string? projectRoot)
    {
        var registry = registryStore.Load();
        var activeHash = registry.activeProjectHash;
        var data = new
        {
            activeProjectHash = activeHash,
            currentProjectHash = !string.IsNullOrWhiteSpace(projectRoot) ? ProtocolConstants.ComputeProjectHash(projectRoot) : null,
            instances = registry.instances
                .OrderBy(item => item.projectName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.projectRoot, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
        };

        return ResponseEnvelope.Success(
            Guid.NewGuid().ToString("N"),
            null,
            ProtocolJson.Serialize(data),
            durationMs: 0,
            transport: "cli");
    }

    private static ResponseEnvelope UseInstance(InstanceRegistryStore registryStore, ParsedCommand parsed, string? projectRoot)
    {
        if (string.IsNullOrWhiteSpace(parsed.InstanceTarget))
        {
            throw new CliUsageException("`instances use`에는 project hash 또는 project path가 필요합니다.");
        }

        var registry = registryStore.Load();
        var target = registryStore.ResolveOrCreateTarget(registry, parsed.InstanceTarget!, projectRoot);
        registry.activeProjectHash = target.projectHash;
        registryStore.Save(registry);

        var data = new
        {
            activeProjectHash = target.projectHash,
            target.projectName,
            target.projectRoot,
            target.pipeName,
            target.state,
        };

        return ResponseEnvelope.Success(
            Guid.NewGuid().ToString("N"),
            target.projectHash,
            ProtocolJson.Serialize(data),
            durationMs: 0,
            transport: "cli");
    }

    private static async Task<ResponseEnvelope> RunStatusAsync(InstanceRegistryStore registryStore, string? projectRoot)
    {
        var registry = registryStore.Load();
        var target = ResolveTarget(registryStore, registry, projectRoot);
        if (target is not null)
        {
            try
            {
                using var cts = new CancellationTokenSource(5_000);
                return await new LocalIpcClient().SendAsync(
                    target,
                    new CommandEnvelope
                    {
                        requestId = Guid.NewGuid().ToString("N"),
                        command = ProtocolConstants.CommandStatus,
                        argumentsJson = "{}",
                    },
                    5_000,
                    cts.Token);
            }
            catch
            {
            }
        }

        var data = new
        {
            projectRoot,
            projectHash = !string.IsNullOrWhiteSpace(projectRoot) ? ProtocolConstants.ComputeProjectHash(projectRoot) : null,
            activeProjectHash = registry.activeProjectHash,
            liveReachable = false,
            unityPath = !string.IsNullOrWhiteSpace(projectRoot) ? UnityEditorLocator.TryResolve(projectRoot) : null,
            registryPath = RegistryPathUtility.GetRegistryFilePath(),
        };

        return ResponseEnvelope.Success(
            Guid.NewGuid().ToString("N"),
            target?.projectHash,
            ProtocolJson.Serialize(data),
            durationMs: 0,
            transport: "cli");
    }

    private static async Task<ResponseEnvelope> RunDoctorAsync(
        InstanceRegistryStore registryStore,
        UnityProjectLocator locator,
        ParsedCommand parsed,
        string? projectRoot)
    {
        var registry = registryStore.Load();
        var target = ResolveTarget(registryStore, registry, projectRoot);
        var unityPath = !string.IsNullOrWhiteSpace(projectRoot)
            ? UnityEditorLocator.TryResolve(projectRoot)
            : null;

        var liveReachable = false;
        if (target is not null)
        {
            try
            {
                using var cts = new CancellationTokenSource(5_000);
                var ipcClient = new LocalIpcClient();
                var ping = new CommandEnvelope
                {
                    requestId = Guid.NewGuid().ToString("N"),
                    command = "ping",
                    argumentsJson = "{}",
                };
                var response = await ipcClient.SendAsync(target, ping, 5_000, cts.Token);
                liveReachable = response.status == "success";
            }
            catch
            {
                liveReachable = false;
            }
        }

        var data = new
        {
            registryPath = RegistryPathUtility.GetRegistryFilePath(),
            workingDirectory = Environment.CurrentDirectory,
            projectRoot,
            projectDetectedFromChildren = string.IsNullOrWhiteSpace(projectRoot) ? locator.TryFindProjectRoot(Environment.CurrentDirectory) : projectRoot,
            targetProjectHash = target?.projectHash,
            targetProjectName = target?.projectName,
            pipeName = target?.pipeName,
            liveReachable,
            unityPath,
            instanceCount = registry.instances.Length,
        };

        return ResponseEnvelope.Success(
            Guid.NewGuid().ToString("N"),
            target?.projectHash,
            ProtocolJson.Serialize(data),
            durationMs: 0,
            transport: "cli");
    }

    private static async Task<ResponseEnvelope> ExecuteUnityCommandAsync(
        ParsedCommand parsed,
        InstanceRegistryStore registryStore,
        string? projectRoot)
    {
        var registry = registryStore.Load();
        var target = ResolveTarget(registryStore, registry, projectRoot);

        if (parsed.Kind == CommandKind.RunTests)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                return ResponseEnvelope.Failure(
                    Guid.NewGuid().ToString("N"),
                    null,
                    "PROJECT_NOT_FOUND",
                    "batch 테스트를 실행할 Unity 프로젝트 루트를 찾지 못했습니다.",
                    retryable: false,
                    transport: "batch");
            }

            return await RunBatchCommandAsync(parsed, projectRoot);
        }

        if (target is not null)
        {
            try
            {
                using var cts = new CancellationTokenSource(parsed.TimeoutMs);
                var liveResponse = await new LocalIpcClient().SendAsync(target, parsed.ToEnvelope(), parsed.TimeoutMs, cts.Token);
                return liveResponse;
            }
            catch (Exception ex) when (IsBatchCapable(parsed.Kind) && !string.IsNullOrWhiteSpace(projectRoot))
            {
                return await RunBatchCommandAsync(parsed, projectRoot!, ex.Message);
            }
            catch
            {
                return ResponseEnvelope.Failure(
                    Guid.NewGuid().ToString("N"),
                    target.projectHash,
                    "LIVE_UNAVAILABLE",
                    "실행 중인 Unity Editor는 보이지만 bridge가 아직 연결되지 않았습니다.",
                    retryable: true,
                    transport: "cli",
                    details: "Unity가 로컬 패키지를 import/compile 중인지 확인하세요.");
            }
        }

        if (IsBatchCapable(parsed.Kind) && !string.IsNullOrWhiteSpace(projectRoot))
        {
            return await RunBatchCommandAsync(parsed, projectRoot!);
        }

        return ResponseEnvelope.Failure(
            Guid.NewGuid().ToString("N"),
            target?.projectHash,
            "NO_TARGET",
            "실행 중인 Unity Editor 대상이 없습니다.",
            retryable: false,
            transport: "cli",
            details: "Unity 프로젝트 루트에서 실행하거나 `unity-cli instances use <projectPath>`로 대상을 고정하세요.");
    }

    private static bool IsBatchCapable(CommandKind kind)
    {
        return CliCommandMetadata.SupportsBatch(kind);
    }

    private static async Task<ResponseEnvelope> RunBatchCommandAsync(ParsedCommand parsed, string projectRoot, string? fallbackReason = null)
    {
        var unityPath = UnityEditorLocator.TryResolve(projectRoot);
        if (string.IsNullOrWhiteSpace(unityPath))
        {
            return ResponseEnvelope.Failure(
                Guid.NewGuid().ToString("N"),
                ProtocolConstants.ComputeProjectHash(projectRoot),
                "UNITY_NOT_FOUND",
                "이 프로젝트에 맞는 Unity Editor 경로를 찾지 못했습니다.",
                retryable: false,
                transport: "batch");
        }

        var runner = new BatchModeRunner();
        return parsed.Kind switch
            {
                CommandKind.Compile => await runner.RunCompileAsync(projectRoot, unityPath, parsed.TimeoutMs, fallbackReason),
                CommandKind.RunTests => await runner.RunTestsAsync(projectRoot, unityPath, parsed.TestMode ?? "edit", parsed.TimeoutMs, fallbackReason),
                _ when IsBatchCapable(parsed.Kind) => await runner.RunRequestAsync(projectRoot, unityPath, parsed.ToEnvelope(), parsed.TimeoutMs, fallbackReason),
                _ => ResponseEnvelope.Failure(
                Guid.NewGuid().ToString("N"),
                ProtocolConstants.ComputeProjectHash(projectRoot),
                "UNSUPPORTED_BATCH",
                "이 명령은 batch fallback을 지원하지 않습니다.",
                retryable: false,
                transport: "batch"),
        };
    }

    private static InstanceRecord? ResolveTarget(InstanceRegistryStore registryStore, InstanceRegistry registry, string? projectRoot)
    {
        if (!string.IsNullOrWhiteSpace(projectRoot))
        {
            var projectHash = ProtocolConstants.ComputeProjectHash(projectRoot);
            var match = registry.instances.FirstOrDefault(item => item.projectHash == projectHash);
            if (match is not null)
            {
                return match;
            }
        }

        if (!string.IsNullOrWhiteSpace(registry.activeProjectHash))
        {
            return registry.instances.FirstOrDefault(item => item.projectHash == registry.activeProjectHash);
        }

        return null;
    }
}
