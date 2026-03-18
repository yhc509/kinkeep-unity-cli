using System.Linq;
using UnityCli.Cli.Models;
using UnityCli.Protocol;

namespace UnityCli.Cli.Services;

public static class CliArgumentParser
{
    public static ParsedCommand Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new ParsedCommand(CommandKind.Help);
        }

        var tokens = new Queue<string>(args);
        var parsed = new ParsedCommand(CommandKind.Help);
        var outputJson = false;
        string? projectOverride = null;

        while (tokens.Count > 0)
        {
            if (tokens.Peek() == "--json")
            {
                outputJson = true;
                tokens.Dequeue();
                continue;
            }

            if (tokens.Peek() == "--project")
            {
                tokens.Dequeue();
                projectOverride = RequireValue(tokens, "--project");
                continue;
            }

            break;
        }

        if (tokens.Count == 0)
        {
            return parsed;
        }

        var command = tokens.Dequeue().ToLowerInvariant();
        parsed = command switch
        {
            "status" => new ParsedCommand(CommandKind.Status),
            "compile" => new ParsedCommand(CommandKind.Compile),
            "refresh" => new ParsedCommand(CommandKind.Refresh),
            "run-tests" => new ParsedCommand(CommandKind.RunTests),
            "read-console" => new ParsedCommand(CommandKind.ReadConsole),
            "play" => new ParsedCommand(CommandKind.Play),
            "pause" => new ParsedCommand(CommandKind.Pause),
            "stop" => new ParsedCommand(CommandKind.Stop),
            "execute-menu" => new ParsedCommand(CommandKind.ExecuteMenu),
            "asset" => ParseAsset(tokens),
            "prefab" => ParsePrefab(tokens),
            "instances" => ParseInstances(tokens),
            "doctor" => new ParsedCommand(CommandKind.Doctor),
            "raw" => new ParsedCommand(CommandKind.Raw),
            "help" or "--help" or "-h" => new ParsedCommand(CommandKind.Help),
            _ => throw new CliUsageException($"알 수 없는 명령입니다: {command}"),
        };

        parsed.JsonOutput = outputJson;
        parsed.ProjectOverride = projectOverride;

        ParseCommandOptions(parsed, tokens);
        return parsed;
    }

    public static string BuildHelpText()
    {
        return """
usage: unity-cli [--json] [--project <path>] <command> [options]

commands:
  status
  compile
  refresh
  run-tests --mode edit|play
  read-console [--limit N] [--type log|warning|error]
  play
  pause
  stop
  execute-menu --path "Menu/Item"
  asset find --name <term> [--type <type>] [--folder <Assets/...>] [--limit N]
  asset types
  asset info (--path <Assets/...> | --guid <guid>)
  asset reimport --path <Assets/...>
  asset mkdir --path <Assets/...>
  asset move --from <Assets/...> --to <Assets/...> [--force]
  asset rename --path <Assets/...> --name <newName> [--force]
  asset delete --path <Assets/...> --force
  asset create --type <kind> --path <Assets/...> [--data-json <json>] [options]
  prefab inspect --path <Assets/...> [--with-values]
  prefab create --path <Assets/...> (--spec-file <file.json> | --spec-json <json>) [--force]
  prefab patch --path <Assets/...> (--spec-file <file.json> | --spec-json <json>)
  instances list
  instances use <projectHash|projectPath>
  doctor
  raw --json '{"command":"status","arguments":{}}'
""";
    }

    private static ParsedCommand ParseInstances(Queue<string> tokens)
    {
        if (tokens.Count == 0)
        {
            throw new CliUsageException("`instances` 다음에는 `list` 또는 `use`가 필요합니다.");
        }

        var subCommand = tokens.Dequeue().ToLowerInvariant();
        return subCommand switch
        {
            "list" => new ParsedCommand(CommandKind.InstancesList),
            "use" => new ParsedCommand(CommandKind.InstancesUse)
            {
                InstanceTarget = tokens.Count > 0 ? tokens.Dequeue() : null,
            },
            _ => throw new CliUsageException($"알 수 없는 instances 하위 명령입니다: {subCommand}"),
        };
    }

    private static ParsedCommand ParseAsset(Queue<string> tokens)
    {
        if (tokens.Count == 0)
        {
            throw new CliUsageException("`asset` 다음에는 하위 명령이 필요합니다.");
        }

        var subCommand = tokens.Dequeue().ToLowerInvariant();
        return subCommand switch
        {
            "find" => new ParsedCommand(CommandKind.AssetFind),
            "types" => new ParsedCommand(CommandKind.AssetTypes),
            "info" => new ParsedCommand(CommandKind.AssetInfo),
            "reimport" => new ParsedCommand(CommandKind.AssetReimport),
            "mkdir" => new ParsedCommand(CommandKind.AssetMkdir),
            "move" => new ParsedCommand(CommandKind.AssetMove),
            "rename" => new ParsedCommand(CommandKind.AssetRename),
            "delete" => new ParsedCommand(CommandKind.AssetDelete),
            "create" => new ParsedCommand(CommandKind.AssetCreate),
            _ => throw new CliUsageException($"알 수 없는 asset 하위 명령입니다: {subCommand}"),
        };
    }

    private static ParsedCommand ParsePrefab(Queue<string> tokens)
    {
        if (tokens.Count == 0)
        {
            throw new CliUsageException("`prefab` 다음에는 하위 명령이 필요합니다.");
        }

        var subCommand = tokens.Dequeue().ToLowerInvariant();
        return subCommand switch
        {
            "inspect" => new ParsedCommand(CommandKind.PrefabInspect),
            "create" => new ParsedCommand(CommandKind.PrefabCreate),
            "patch" => new ParsedCommand(CommandKind.PrefabPatch),
            _ => throw new CliUsageException($"알 수 없는 prefab 하위 명령입니다: {subCommand}"),
        };
    }

    private static void ParseCommandOptions(ParsedCommand parsed, Queue<string> tokens)
    {
        var timeoutExplicitlySet = false;

        while (tokens.Count > 0)
        {
            var token = tokens.Dequeue();
            if (token == "--project")
            {
                parsed.ProjectOverride = RequireValue(tokens, "--project");
                continue;
            }

            if (token == "--timeout-ms")
            {
                parsed.TimeoutMs = RequireInt(RequireValue(tokens, "--timeout-ms"), "--timeout-ms");
                timeoutExplicitlySet = true;
                continue;
            }

            if (token == "--json" && parsed.Kind != CommandKind.Raw)
            {
                parsed.JsonOutput = true;
                continue;
            }

            switch (parsed.Kind)
            {
                case CommandKind.Raw when token == "--json":
                    parsed.RawJson = RequireValue(tokens, "--json");
                    break;
                case CommandKind.RunTests when token == "--mode":
                    parsed.TestMode = RequireMode(RequireValue(tokens, "--mode"));
                    break;
                case CommandKind.ReadConsole when token == "--limit":
                    parsed.ConsoleLimit = RequireInt(RequireValue(tokens, "--limit"), "--limit");
                    break;
                case CommandKind.ReadConsole when token == "--type":
                    parsed.ConsoleType = RequireValue(tokens, "--type");
                    break;
                case CommandKind.ExecuteMenu when token == "--path":
                    parsed.MenuPath = RequireValue(tokens, "--path");
                    break;
                case CommandKind.AssetFind when token == "--name":
                    parsed.AssetName = RequireValue(tokens, "--name");
                    break;
                case CommandKind.AssetFind when token == "--type":
                    parsed.AssetType = RequireValue(tokens, "--type");
                    break;
                case CommandKind.AssetFind when token == "--folder":
                    parsed.AssetFolder = RequireAssetPath(RequireValue(tokens, "--folder"), "--folder");
                    break;
                case CommandKind.AssetFind when token == "--limit":
                    parsed.AssetLimit = RequireInt(RequireValue(tokens, "--limit"), "--limit");
                    break;
                case CommandKind.AssetInfo when token == "--path":
                    parsed.AssetPath = RequireAssetPath(RequireValue(tokens, "--path"), "--path");
                    break;
                case CommandKind.AssetInfo when token == "--guid":
                    parsed.AssetGuid = RequireValue(tokens, "--guid");
                    break;
                case CommandKind.AssetReimport when token == "--path":
                case CommandKind.AssetMkdir when token == "--path":
                case CommandKind.AssetDelete when token == "--path":
                    parsed.AssetPath = RequireAssetPath(RequireValue(tokens, "--path"), "--path");
                    break;
                case CommandKind.AssetMove when token == "--from":
                    parsed.AssetFrom = RequireAssetPath(RequireValue(tokens, "--from"), "--from");
                    break;
                case CommandKind.AssetMove when token == "--to":
                    parsed.AssetTo = RequireAssetPath(RequireValue(tokens, "--to"), "--to");
                    break;
                case CommandKind.AssetRename when token == "--path":
                    parsed.AssetPath = RequireAssetPath(RequireValue(tokens, "--path"), "--path");
                    break;
                case CommandKind.AssetRename when token == "--name":
                    parsed.AssetNewName = RequireValue(tokens, "--name");
                    break;
                case CommandKind.AssetMove when token == "--force":
                case CommandKind.AssetRename when token == "--force":
                case CommandKind.AssetDelete when token == "--force":
                case CommandKind.AssetCreate when token == "--force":
                    parsed.Force = true;
                    break;
                case CommandKind.AssetCreate when token == "--type":
                    parsed.AssetCreateType = RequireAssetCreateType(RequireValue(tokens, "--type"));
                    break;
                case CommandKind.AssetCreate when token == "--path":
                    parsed.AssetPath = RequireAssetPath(RequireValue(tokens, "--path"), "--path");
                    break;
                case CommandKind.AssetCreate when token == "--data-json":
                    parsed.AssetDataJson = RequireValue(tokens, "--data-json");
                    break;
                case CommandKind.AssetCreate when token == "--shader":
                    parsed.AssetShader = RequireValue(tokens, "--shader");
                    break;
                case CommandKind.AssetCreate when token == "--script":
                    parsed.AssetScript = RequireAssetPath(RequireValue(tokens, "--script"), "--script");
                    break;
                case CommandKind.AssetCreate when token == "--type-name":
                    parsed.AssetTypeName = RequireValue(tokens, "--type-name");
                    break;
                case CommandKind.AssetCreate when token == "--legacy":
                    parsed.AssetLegacy = true;
                    break;
                case CommandKind.AssetCreate when token == "--initial-map":
                    parsed.AssetInitialMap = RequireValue(tokens, "--initial-map");
                    break;
                case CommandKind.AssetCreate when token == "--root-name":
                    parsed.AssetRootName = RequireValue(tokens, "--root-name");
                    break;
                case CommandKind.AssetCreate when token == "--base-controller":
                    parsed.AssetBaseController = RequireAssetPath(RequireValue(tokens, "--base-controller"), "--base-controller");
                    break;
                case CommandKind.AssetCreate when token == "--width":
                    parsed.AssetWidth = RequireInt(RequireValue(tokens, "--width"), "--width");
                    break;
                case CommandKind.AssetCreate when token == "--height":
                    parsed.AssetHeight = RequireInt(RequireValue(tokens, "--height"), "--height");
                    break;
                case CommandKind.AssetCreate when token == "--depth":
                    parsed.AssetDepth = RequireNonNegativeInt(RequireValue(tokens, "--depth"), "--depth");
                    break;
                case CommandKind.PrefabInspect when token == "--path":
                    parsed.PrefabPath = RequireAssetPath(RequireValue(tokens, "--path"), "--path");
                    break;
                case CommandKind.PrefabInspect when token == "--with-values":
                    parsed.PrefabWithValues = true;
                    break;
                case CommandKind.PrefabCreate when token == "--path":
                case CommandKind.PrefabPatch when token == "--path":
                    parsed.PrefabPath = RequireAssetPath(RequireValue(tokens, "--path"), "--path");
                    break;
                case CommandKind.PrefabCreate when token == "--force":
                    parsed.Force = true;
                    break;
                case CommandKind.PrefabCreate when token == "--spec-file":
                case CommandKind.PrefabPatch when token == "--spec-file":
                    parsed.PrefabSpecFile = RequireValue(tokens, "--spec-file");
                    break;
                case CommandKind.PrefabCreate when token == "--spec-json":
                case CommandKind.PrefabPatch when token == "--spec-json":
                    parsed.PrefabSpecJson = RequireValue(tokens, "--spec-json");
                    break;
                default:
                    if (parsed.Kind == CommandKind.AssetCreate && token.StartsWith("--", StringComparison.Ordinal))
                    {
                        AddAssetCreateCustomOption(parsed, token, tokens);
                        break;
                    }

                    throw new CliUsageException($"지원하지 않는 옵션입니다: {token}");
            }
        }

        if (parsed.Kind == CommandKind.RunTests && string.IsNullOrWhiteSpace(parsed.TestMode))
        {
            parsed.TestMode = "edit";
        }

        if (!timeoutExplicitlySet && IsBatchCapable(parsed.Kind))
        {
            parsed.TimeoutMs = ProtocolConstants.DefaultBatchTimeoutMs;
        }

        if (parsed.Kind == CommandKind.ExecuteMenu && string.IsNullOrWhiteSpace(parsed.MenuPath))
        {
            throw new CliUsageException("`execute-menu`에는 `--path`가 필요합니다.");
        }

        ValidateAssetOptions(parsed);

        if (parsed.Kind == CommandKind.Raw && string.IsNullOrWhiteSpace(parsed.RawJson))
        {
            throw new CliUsageException("`raw`에는 `--json` payload가 필요합니다.");
        }
    }

    private static string RequireMode(string mode)
    {
        return mode.ToLowerInvariant() switch
        {
            "edit" or "editmode" => "edit",
            "play" or "playmode" => "play",
            _ => throw new CliUsageException("`--mode`는 `edit` 또는 `play`만 지원합니다."),
        };
    }

    private static int RequireInt(string value, string option)
    {
        if (!int.TryParse(value, out var result) || result <= 0)
        {
            throw new CliUsageException($"{option} 값은 1 이상의 정수여야 합니다.");
        }

        return result;
    }

    private static int RequireNonNegativeInt(string value, string option)
    {
        if (!int.TryParse(value, out var result) || result < 0)
        {
            throw new CliUsageException($"{option} 값은 0 이상의 정수여야 합니다.");
        }

        return result;
    }

    private static string RequireValue(Queue<string> tokens, string option)
    {
        if (tokens.Count == 0)
        {
            throw new CliUsageException($"{option} 값이 비어 있습니다.");
        }

        return tokens.Dequeue();
    }

    private static string RequireAssetPath(string value, string option)
    {
        var normalized = value.Replace('\\', '/').Trim();
        if (normalized == "Assets" || normalized.StartsWith("Assets/", StringComparison.Ordinal))
        {
            return normalized.TrimEnd('/');
        }

        throw new CliUsageException($"{option} 값은 `Assets/...` 형식이어야 합니다.");
    }

    private static string RequireAssetCreateType(string value)
    {
        string normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "material" => "material",
            "physics-material" or "physic-material" => "physics-material",
            "physics-material-2d" or "physic-material-2d" => "physics-material-2d",
            "animator-controller" or "controller" => "animator-controller",
            "animator-override-controller" or "override-controller" => "animator-override-controller",
            "animation-clip" or "clip" => "animation-clip",
            "input-actions" or "inputactions" => "input-actions",
            "scene" => "scene",
            "prefab" => "prefab",
            "render-texture" or "rendertexture" => "render-texture",
            "avatar-mask" or "avatarmask" => "avatar-mask",
            "volume-profile" or "volumeprofile" => "volume-profile",
            "scriptable-object" or "scriptableobject" => "scriptable-object",
            _ when string.IsNullOrWhiteSpace(normalized) => throw new CliUsageException("`asset create --type` 값이 비어 있습니다."),
            _ => normalized,
        };
    }

    private static void ValidateAssetOptions(ParsedCommand parsed)
    {
        switch (parsed.Kind)
        {
            case CommandKind.AssetFind when string.IsNullOrWhiteSpace(parsed.AssetName):
                throw new CliUsageException("`asset find`에는 `--name`이 필요합니다.");
            case CommandKind.AssetInfo:
            {
                var hasPath = !string.IsNullOrWhiteSpace(parsed.AssetPath);
                var hasGuid = !string.IsNullOrWhiteSpace(parsed.AssetGuid);
                if (hasPath == hasGuid)
                {
                    throw new CliUsageException("`asset info`에는 `--path` 또는 `--guid` 중 하나만 필요합니다.");
                }

                break;
            }
            case CommandKind.AssetReimport when string.IsNullOrWhiteSpace(parsed.AssetPath):
                throw new CliUsageException("`asset reimport`에는 `--path`가 필요합니다.");
            case CommandKind.AssetMkdir when string.IsNullOrWhiteSpace(parsed.AssetPath):
                throw new CliUsageException("`asset mkdir`에는 `--path`가 필요합니다.");
            case CommandKind.AssetMove when string.IsNullOrWhiteSpace(parsed.AssetFrom) || string.IsNullOrWhiteSpace(parsed.AssetTo):
                throw new CliUsageException("`asset move`에는 `--from`과 `--to`가 모두 필요합니다.");
            case CommandKind.AssetRename when string.IsNullOrWhiteSpace(parsed.AssetPath) || string.IsNullOrWhiteSpace(parsed.AssetNewName):
                throw new CliUsageException("`asset rename`에는 `--path`와 `--name`이 모두 필요합니다.");
            case CommandKind.AssetDelete when string.IsNullOrWhiteSpace(parsed.AssetPath):
                throw new CliUsageException("`asset delete`에는 `--path`가 필요합니다.");
            case CommandKind.AssetDelete when !parsed.Force:
                throw new CliUsageException("`asset delete`는 `--force`가 필요합니다.");
            case CommandKind.AssetCreate when string.IsNullOrWhiteSpace(parsed.AssetCreateType) || string.IsNullOrWhiteSpace(parsed.AssetPath):
                throw new CliUsageException("`asset create`에는 `--type`과 `--path`가 필요합니다.");
            case CommandKind.AssetCreate when parsed.AssetCreateType == "scriptable-object"
                && string.IsNullOrWhiteSpace(parsed.AssetScript)
                && string.IsNullOrWhiteSpace(parsed.AssetTypeName):
                throw new CliUsageException("`asset create --type scriptable-object`에는 `--script` 또는 `--type-name`이 필요합니다.");
            case CommandKind.AssetCreate when parsed.AssetCreateType == "animator-override-controller"
                && string.IsNullOrWhiteSpace(parsed.AssetBaseController):
                throw new CliUsageException("`asset create --type animator-override-controller`에는 `--base-controller`가 필요합니다.");
            case CommandKind.AssetCreate when IsBuiltInAssetCreateType(parsed.AssetCreateType)
                && parsed.AssetCustomOptions.Count > 0:
                throw new CliUsageException(
                    "`asset create --type " + parsed.AssetCreateType + "`에서 지원하지 않는 옵션입니다: "
                    + string.Join(", ", parsed.AssetCustomOptions.Keys.Select(key => "--" + ToKebabCase(key))));
            case CommandKind.PrefabInspect when string.IsNullOrWhiteSpace(parsed.PrefabPath):
                throw new CliUsageException("`prefab inspect`에는 `--path`가 필요합니다.");
            case CommandKind.PrefabCreate when string.IsNullOrWhiteSpace(parsed.PrefabPath):
                throw new CliUsageException("`prefab create`에는 `--path`가 필요합니다.");
            case CommandKind.PrefabPatch when string.IsNullOrWhiteSpace(parsed.PrefabPath):
                throw new CliUsageException("`prefab patch`에는 `--path`가 필요합니다.");
            case CommandKind.PrefabCreate when HasInvalidPrefabSpecSource(parsed):
                throw new CliUsageException("`prefab create`에는 `--spec-file` 또는 `--spec-json` 중 하나만 필요합니다.");
            case CommandKind.PrefabPatch when HasInvalidPrefabSpecSource(parsed):
                throw new CliUsageException("`prefab patch`에는 `--spec-file` 또는 `--spec-json` 중 하나만 필요합니다.");
        }
    }

    private static bool HasInvalidPrefabSpecSource(ParsedCommand parsed)
    {
        bool hasFile = !string.IsNullOrWhiteSpace(parsed.PrefabSpecFile);
        bool hasInline = !string.IsNullOrWhiteSpace(parsed.PrefabSpecJson);
        return hasFile == hasInline;
    }

    private static void AddAssetCreateCustomOption(ParsedCommand parsed, string token, Queue<string> tokens)
    {
        string optionName = NormalizeCustomOptionName(token);
        object value = true;
        if (tokens.Count > 0 && !tokens.Peek().StartsWith("--", StringComparison.Ordinal))
        {
            value = ParseCustomOptionValue(tokens.Dequeue());
        }

        parsed.AssetCustomOptions[optionName] = value;
    }

    private static object ParseCustomOptionValue(string rawValue)
    {
        if (bool.TryParse(rawValue, out bool boolValue))
        {
            return boolValue;
        }

        if (int.TryParse(rawValue, out int intValue))
        {
            return intValue;
        }

        if (double.TryParse(rawValue, out double doubleValue))
        {
            return doubleValue;
        }

        return rawValue;
    }

    private static string NormalizeCustomOptionName(string token)
    {
        string trimmed = token.Trim();
        if (!trimmed.StartsWith("--", StringComparison.Ordinal) || trimmed.Length <= 2)
        {
            throw new CliUsageException($"지원하지 않는 옵션입니다: {token}");
        }

        string body = trimmed[2..];
        string[] parts = body.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            throw new CliUsageException($"지원하지 않는 옵션입니다: {token}");
        }

        return string.Concat(parts.Select((part, index) =>
            index == 0
                ? char.ToLowerInvariant(part[0]) + part[1..]
                : char.ToUpperInvariant(part[0]) + part[1..]));
    }

    private static bool IsBuiltInAssetCreateType(string? type)
    {
        return type is "material"
            or "physics-material"
            or "physics-material-2d"
            or "animator-controller"
            or "animator-override-controller"
            or "animation-clip"
            or "input-actions"
            or "scene"
            or "prefab"
            or "render-texture"
            or "avatar-mask"
            or "volume-profile"
            or "scriptable-object";
    }

    private static string ToKebabCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var builder = new System.Text.StringBuilder(value.Length + 4);
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (char.IsUpper(character) && index > 0)
            {
                builder.Append('-');
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    private static bool IsBatchCapable(CommandKind kind)
    {
        return kind is CommandKind.Compile
            or CommandKind.Refresh
            or CommandKind.RunTests
            or CommandKind.AssetFind
            or CommandKind.AssetTypes
            or CommandKind.AssetInfo
            or CommandKind.AssetReimport
            or CommandKind.AssetMkdir
            or CommandKind.AssetMove
            or CommandKind.AssetRename
            or CommandKind.AssetDelete
            or CommandKind.AssetCreate
            or CommandKind.PrefabInspect
            or CommandKind.PrefabCreate
            or CommandKind.PrefabPatch;
    }
}
