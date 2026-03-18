using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using UnityCli.Protocol;

namespace UnityCli.Cli.Models;

public enum CommandKind
{
    Help,
    Status,
    Compile,
    Refresh,
    RunTests,
    ReadConsole,
    Play,
    Pause,
    Stop,
    ExecuteMenu,
    AssetFind,
    AssetTypes,
    AssetInfo,
    AssetReimport,
    AssetMkdir,
    AssetMove,
    AssetRename,
    AssetDelete,
    AssetCreate,
    PrefabInspect,
    PrefabCreate,
    PrefabPatch,
    InstancesList,
    InstancesUse,
    Doctor,
    Raw,
}

public sealed class ParsedCommand
{
    public ParsedCommand(CommandKind kind)
    {
        Kind = kind;
    }

    public CommandKind Kind { get; }
    public bool JsonOutput { get; set; }
    public string? ProjectOverride { get; set; }
    public string? TestMode { get; set; }
    public int TimeoutMs { get; set; } = ProtocolConstants.DefaultLiveTimeoutMs;
    public int ConsoleLimit { get; set; } = ProtocolConstants.DefaultConsoleLimit;
    public string? ConsoleType { get; set; }
    public string? MenuPath { get; set; }
    public string? InstanceTarget { get; set; }
    public string? RawJson { get; set; }
    public string? AssetName { get; set; }
    public string? AssetType { get; set; }
    public string? AssetFolder { get; set; }
    public int AssetLimit { get; set; } = ProtocolConstants.DefaultAssetFindLimit;
    public string? AssetPath { get; set; }
    public string? AssetGuid { get; set; }
    public string? AssetFrom { get; set; }
    public string? AssetTo { get; set; }
    public string? AssetNewName { get; set; }
    public string? AssetCreateType { get; set; }
    public string? AssetShader { get; set; }
    public string? AssetScript { get; set; }
    public string? AssetTypeName { get; set; }
    public string? AssetDataJson { get; set; }
    public bool AssetLegacy { get; set; }
    public string? AssetInitialMap { get; set; }
    public string? AssetRootName { get; set; }
    public string? AssetBaseController { get; set; }
    public int? AssetWidth { get; set; }
    public int? AssetHeight { get; set; }
    public int? AssetDepth { get; set; }
    public Dictionary<string, object?> AssetCustomOptions { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string? PrefabPath { get; set; }
    public string? PrefabSpecFile { get; set; }
    public string? PrefabSpecJson { get; set; }
    public bool PrefabWithValues { get; set; }
    public bool Force { get; set; }

    public CommandEnvelope ToEnvelope()
    {
        if (Kind == CommandKind.Raw)
        {
            var raw = JsonDocument.Parse(RawJson ?? throw new CliUsageException("raw 명령에는 `--json` payload가 필요합니다."));
            var root = raw.RootElement;
            if (!root.TryGetProperty("command", out var commandElement))
            {
                throw new CliUsageException("raw payload에는 `command` 필드가 필요합니다.");
            }

            var rawRequest = new CommandEnvelope
            {
                requestId = Guid.NewGuid().ToString("N"),
                command = commandElement.GetString() ?? string.Empty,
                argumentsJson = root.TryGetProperty("arguments", out var argumentsElement)
                    ? argumentsElement.GetRawText()
                    : "{}",
            };

            return rawRequest;
        }

        var request = new CommandEnvelope
        {
            requestId = Guid.NewGuid().ToString("N"),
            command = Kind switch
            {
                CommandKind.Status => "status",
                CommandKind.Compile => "compile",
                CommandKind.Refresh => "refresh",
                CommandKind.ReadConsole => "read-console",
                CommandKind.Play => "play",
                CommandKind.Pause => "pause",
                CommandKind.Stop => "stop",
                CommandKind.ExecuteMenu => "execute-menu",
                CommandKind.AssetFind => ProtocolConstants.CommandAssetFind,
                CommandKind.AssetTypes => ProtocolConstants.CommandAssetTypes,
                CommandKind.AssetInfo => ProtocolConstants.CommandAssetInfo,
                CommandKind.AssetReimport => ProtocolConstants.CommandAssetReimport,
                CommandKind.AssetMkdir => ProtocolConstants.CommandAssetMkdir,
                CommandKind.AssetMove => ProtocolConstants.CommandAssetMove,
                CommandKind.AssetRename => ProtocolConstants.CommandAssetRename,
                CommandKind.AssetDelete => ProtocolConstants.CommandAssetDelete,
                CommandKind.AssetCreate => ProtocolConstants.CommandAssetCreate,
                CommandKind.PrefabInspect => ProtocolConstants.CommandPrefabInspect,
                CommandKind.PrefabCreate => ProtocolConstants.CommandPrefabCreate,
                CommandKind.PrefabPatch => ProtocolConstants.CommandPrefabPatch,
                _ => throw new CliUsageException($"지원하지 않는 live 명령입니다: {Kind}"),
            },
            argumentsJson = BuildArgumentsJson(),
        };

        return request;
    }

    private string BuildArgumentsJson()
    {
        object payload = Kind switch
        {
            CommandKind.ReadConsole => new
            {
                limit = ConsoleLimit,
                type = ConsoleType,
            },
            CommandKind.ExecuteMenu => new
            {
                path = MenuPath,
            },
            CommandKind.AssetFind => new AssetFindArgs
            {
                name = AssetName ?? string.Empty,
                type = AssetType,
                folder = AssetFolder,
                limit = AssetLimit,
            },
            CommandKind.AssetTypes => new { },
            CommandKind.AssetInfo => new AssetInfoArgs
            {
                path = AssetPath,
                guid = AssetGuid,
            },
            CommandKind.AssetReimport => new AssetPathArgs
            {
                path = AssetPath ?? string.Empty,
            },
            CommandKind.AssetMkdir => new AssetPathArgs
            {
                path = AssetPath ?? string.Empty,
            },
            CommandKind.AssetMove => new AssetMoveArgs
            {
                from = AssetFrom ?? string.Empty,
                to = AssetTo ?? string.Empty,
                force = Force,
            },
            CommandKind.AssetRename => new AssetRenameArgs
            {
                path = AssetPath ?? string.Empty,
                name = AssetNewName ?? string.Empty,
                force = Force,
            },
            CommandKind.AssetDelete => new AssetPathArgs
            {
                path = AssetPath ?? string.Empty,
            },
            CommandKind.AssetCreate => new AssetCreateArgs
            {
                type = AssetCreateType ?? string.Empty,
                path = AssetPath ?? string.Empty,
                force = Force,
                script = AssetScript,
                typeName = AssetTypeName,
                dataJson = AssetDataJson,
                optionsJson = BuildAssetCreateOptionsJson(),
            },
            CommandKind.PrefabInspect => new PrefabInspectArgs
            {
                path = PrefabPath ?? string.Empty,
                withValues = PrefabWithValues,
            },
            CommandKind.PrefabCreate => new PrefabCreateArgs
            {
                path = PrefabPath ?? string.Empty,
                force = Force,
                specJson = ResolvePrefabSpecJson(),
            },
            CommandKind.PrefabPatch => new PrefabPatchArgs
            {
                path = PrefabPath ?? string.Empty,
                specJson = ResolvePrefabSpecJson(),
            },
            _ => new { },
        };

        return JsonSerializer.Serialize(payload, ProtocolJson.Default);
    }

    private string? BuildAssetCreateOptionsJson()
    {
        var options = new Dictionary<string, object?>(AssetCustomOptions, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(AssetShader))
        {
            options["shader"] = AssetShader;
        }

        if (AssetLegacy)
        {
            options["legacy"] = true;
        }

        if (!string.IsNullOrWhiteSpace(AssetInitialMap))
        {
            options["initialMap"] = AssetInitialMap;
        }

        if (!string.IsNullOrWhiteSpace(AssetRootName))
        {
            options["rootName"] = AssetRootName;
        }

        if (!string.IsNullOrWhiteSpace(AssetBaseController))
        {
            options["baseController"] = AssetBaseController;
        }

        if (AssetWidth.HasValue)
        {
            options["width"] = AssetWidth.Value;
        }

        if (AssetHeight.HasValue)
        {
            options["height"] = AssetHeight.Value;
        }

        if (AssetDepth.HasValue)
        {
            options["depth"] = AssetDepth.Value;
        }

        return options.Count == 0 ? null : JsonSerializer.Serialize(options, ProtocolJson.Default);
    }

    private string ResolvePrefabSpecJson()
    {
        string? specJson = PrefabSpecJson;
        if (!string.IsNullOrWhiteSpace(PrefabSpecFile))
        {
            string filePath = Path.GetFullPath(PrefabSpecFile);
            if (!File.Exists(filePath))
            {
                throw new CliUsageException("spec 파일을 찾지 못했습니다: " + filePath);
            }

            specJson = File.ReadAllText(filePath);
        }

        if (string.IsNullOrWhiteSpace(specJson))
        {
            throw new CliUsageException("prefab spec이 비어 있습니다.");
        }

        return specJson;
    }
}

public sealed class CliUsageException : Exception
{
    public CliUsageException(string message)
        : base(message)
    {
    }
}
