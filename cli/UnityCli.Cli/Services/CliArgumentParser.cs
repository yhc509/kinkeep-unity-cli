using System.Linq;
using System.Text.Json;
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
            "read-console" => new ParsedCommand(CommandKind.ReadConsole),
            "play" => new ParsedCommand(CommandKind.Play),
            "pause" => new ParsedCommand(CommandKind.Pause),
            "stop" => new ParsedCommand(CommandKind.Stop),
            "execute-menu" => new ParsedCommand(CommandKind.ExecuteMenu),
            "screenshot" => new ParsedCommand(CommandKind.Screenshot),
            "execute" => new ParsedCommand(CommandKind.ExecuteCode),
            "custom" => ParseCustom(tokens),
            "asset" => ParseAsset(tokens),
            "scene" => ParseScene(tokens),
            "prefab" => ParsePrefab(tokens),
            "package" => ParsePackage(tokens),
            "material" => ParseMaterial(tokens),
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
        return CliCommandMetadata.BuildHelpText();
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

    private static ParsedCommand ParseScene(Queue<string> tokens)
    {
        if (tokens.Count == 0)
        {
            throw new CliUsageException("`scene` 다음에는 하위 명령이 필요합니다.");
        }

        var subCommand = tokens.Dequeue().ToLowerInvariant();
        return subCommand switch
        {
            "open" => new ParsedCommand(CommandKind.SceneOpen),
            "inspect" => new ParsedCommand(CommandKind.SceneInspect),
            "patch" => new ParsedCommand(CommandKind.ScenePatch),
            "add-object" => new ParsedCommand(CommandKind.SceneAddObject),
            "set-transform" => new ParsedCommand(CommandKind.SceneSetTransform),
            "add-component" => new ParsedCommand(CommandKind.SceneAddComponent),
            "remove-component" => new ParsedCommand(CommandKind.SceneRemoveComponent),
            _ => throw new CliUsageException($"알 수 없는 scene 하위 명령입니다: {subCommand}"),
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

    private static ParsedCommand ParsePackage(Queue<string> tokens)
    {
        if (tokens.Count == 0)
        {
            throw new CliUsageException("`package` 다음에는 하위 명령이 필요합니다.");
        }

        var subCommand = tokens.Dequeue().ToLowerInvariant();
        return subCommand switch
        {
            "list" => new ParsedCommand(CommandKind.PackageList),
            "add" => new ParsedCommand(CommandKind.PackageAdd),
            "remove" => new ParsedCommand(CommandKind.PackageRemove),
            "search" => new ParsedCommand(CommandKind.PackageSearch),
            _ => throw new CliUsageException($"알 수 없는 package 하위 명령입니다: {subCommand}"),
        };
    }

    private static ParsedCommand ParseMaterial(Queue<string> tokens)
    {
        if (tokens.Count == 0)
        {
            throw new CliUsageException("`material` 다음에는 하위 명령이 필요합니다.");
        }

        var subCommand = tokens.Dequeue().ToLowerInvariant();
        return subCommand switch
        {
            "info" => new ParsedCommand(CommandKind.MaterialInfo),
            "set" => new ParsedCommand(CommandKind.MaterialSet),
            _ => throw new CliUsageException($"알 수 없는 material 하위 명령입니다: {subCommand}"),
        };
    }

    private static ParsedCommand ParseCustom(Queue<string> tokens)
    {
        if (tokens.Count == 0)
        {
            throw new CliUsageException("`custom` 다음에는 명령 이름이 필요합니다.");
        }

        var commandName = tokens.Dequeue();
        return new ParsedCommand(CommandKind.Custom)
        {
            CustomCommandName = commandName,
        };
    }

    private static void ParseCommandOptions(ParsedCommand parsed, Queue<string> tokens)
    {
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
                continue;
            }

            if (token == "--json" && parsed.Kind != CommandKind.Raw && parsed.Kind != CommandKind.Custom)
            {
                parsed.JsonOutput = true;
                continue;
            }

            switch (parsed.Kind)
            {
                case CommandKind.Raw when token == "--json":
                    parsed.RawJson = RequireValue(tokens, "--json");
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
                case CommandKind.Screenshot when token == "--view":
                    parsed.ScreenshotView = RequireScreenshotView(RequireValue(tokens, "--view"));
                    break;
                case CommandKind.Screenshot when token == "--camera":
                    parsed.ScreenshotCamera = RequireValue(tokens, "--camera");
                    break;
                case CommandKind.Screenshot when token == "--path":
                    parsed.ScreenshotPath = RequireValue(tokens, "--path");
                    break;
                case CommandKind.Screenshot when token == "--width":
                    parsed.ScreenshotWidth = RequireInt(RequireValue(tokens, "--width"), "--width");
                    break;
                case CommandKind.Screenshot when token == "--height":
                    parsed.ScreenshotHeight = RequireInt(RequireValue(tokens, "--height"), "--height");
                    break;
                case CommandKind.ExecuteCode when token == "--code":
                    parsed.ExecuteCodeSnippet = RequireValue(tokens, "--code");
                    break;
                case CommandKind.ExecuteCode when token == "--file":
                    parsed.ExecuteCodeFile = RequireValue(tokens, "--file");
                    break;
                case CommandKind.ExecuteCode when token == "--force":
                    parsed.Force = true;
                    break;
                case CommandKind.Custom when token == "--json":
                    parsed.CustomArgsJson = RequireValue(tokens, "--json");
                    break;
                case CommandKind.PackageAdd when token == "--name":
                    parsed.PackageName = RequireValue(tokens, "--name");
                    break;
                case CommandKind.PackageAdd when token == "--version":
                    parsed.PackageVersion = RequireValue(tokens, "--version");
                    break;
                case CommandKind.PackageRemove when token == "--name":
                    parsed.PackageName = RequireValue(tokens, "--name");
                    break;
                case CommandKind.PackageRemove when token == "--force":
                    parsed.Force = true;
                    break;
                case CommandKind.PackageSearch when token == "--query":
                    parsed.PackageQuery = RequireValue(tokens, "--query");
                    break;
                case CommandKind.MaterialInfo when token == "--path":
                case CommandKind.MaterialSet when token == "--path":
                    parsed.MaterialPath = RequireAssetPath(RequireValue(tokens, "--path"), "--path");
                    break;
                case CommandKind.MaterialSet when token == "--property":
                    parsed.MaterialProperty = RequireValue(tokens, "--property");
                    break;
                case CommandKind.MaterialSet when token == "--value":
                    parsed.MaterialValue = RequireValue(tokens, "--value");
                    break;
                case CommandKind.MaterialSet when token == "--texture":
                    parsed.MaterialTexture = RequireValue(tokens, "--texture");
                    break;
                case CommandKind.MaterialSet when token == "--asset":
                    parsed.MaterialTextureAsset = RequireAssetPath(RequireValue(tokens, "--asset"), "--asset");
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
                case CommandKind.SceneOpen when token == "--path":
                case CommandKind.SceneInspect when token == "--path":
                case CommandKind.ScenePatch when token == "--path":
                case CommandKind.SceneAddObject when token == "--path":
                case CommandKind.SceneSetTransform when token == "--path":
                case CommandKind.SceneAddComponent when token == "--path":
                case CommandKind.SceneRemoveComponent when token == "--path":
                    parsed.ScenePath = RequireAssetPath(RequireValue(tokens, "--path"), "--path");
                    break;
                case CommandKind.SceneOpen when token == "--force":
                case CommandKind.ScenePatch when token == "--force":
                case CommandKind.SceneRemoveComponent when token == "--force":
                    parsed.Force = true;
                    break;
                case CommandKind.SceneInspect when token == "--with-values":
                    parsed.SceneWithValues = true;
                    break;
                case CommandKind.ScenePatch when token == "--spec-file":
                    parsed.SceneSpecFile = RequireValue(tokens, "--spec-file");
                    break;
                case CommandKind.ScenePatch when token == "--spec-json":
                    parsed.SceneSpecJson = RequireValue(tokens, "--spec-json");
                    break;
                case CommandKind.SceneAddObject when token == "--parent":
                    parsed.SceneParent = RequireValue(tokens, "--parent");
                    break;
                case CommandKind.SceneAddObject when token == "--name":
                    parsed.SceneObjectName = RequireValue(tokens, "--name");
                    break;
                case CommandKind.SceneAddObject when token == "--components":
                    parsed.SceneComponents = RequireValue(tokens, "--components");
                    break;
                case CommandKind.SceneSetTransform when token == "--target":
                case CommandKind.SceneAddComponent when token == "--target":
                case CommandKind.SceneRemoveComponent when token == "--target":
                    parsed.SceneTarget = RequireValue(tokens, "--target");
                    break;
                case CommandKind.SceneSetTransform when token == "--position":
                    parsed.ScenePosition = RequireValue(tokens, "--position");
                    break;
                case CommandKind.SceneSetTransform when token == "--rotation":
                    parsed.SceneRotation = RequireValue(tokens, "--rotation");
                    break;
                case CommandKind.SceneSetTransform when token == "--scale":
                    parsed.SceneScale = RequireValue(tokens, "--scale");
                    break;
                case CommandKind.SceneAddComponent when token == "--type":
                case CommandKind.SceneRemoveComponent when token == "--type":
                    parsed.SceneComponentType = RequireValue(tokens, "--type");
                    break;
                case CommandKind.SceneAddComponent when token == "--values":
                    parsed.SceneComponentValues = RequireValue(tokens, "--values");
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

        if (parsed.Kind == CommandKind.ExecuteMenu && string.IsNullOrWhiteSpace(parsed.MenuPath))
        {
            throw new CliUsageException("`execute-menu`에는 `--path`가 필요합니다.");
        }

        ValidateAssetOptions(parsed);
        ValidateSceneOptions(parsed);
        ValidateScreenshotOptions(parsed);
        ValidatePackageOptions(parsed);
        ValidateMaterialOptions(parsed);
        ValidateExecuteOptions(parsed);

        if (parsed.Kind == CommandKind.Raw && string.IsNullOrWhiteSpace(parsed.RawJson))
        {
            throw new CliUsageException("`raw`에는 `--json` payload가 필요합니다.");
        }
    }

    private static string RequireScreenshotView(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "game" => "game",
            "scene" => "scene",
            _ => throw new CliUsageException("`--view`는 `game` 또는 `scene`만 지원합니다."),
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
        string normalized = BuiltInAssetCreateCatalog.NormalizeTypeId(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new CliUsageException("`asset create --type` 값이 비어 있습니다.");
        }

        return normalized;
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

    private static void ValidateSceneOptions(ParsedCommand parsed)
    {
        switch (parsed.Kind)
        {
            case CommandKind.SceneOpen when string.IsNullOrWhiteSpace(parsed.ScenePath):
                throw new CliUsageException("`scene open`에는 `--path`가 필요합니다.");
            case CommandKind.SceneInspect when string.IsNullOrWhiteSpace(parsed.ScenePath):
                throw new CliUsageException("`scene inspect`에는 `--path`가 필요합니다.");
            case CommandKind.ScenePatch when string.IsNullOrWhiteSpace(parsed.ScenePath):
                throw new CliUsageException("`scene patch`에는 `--path`가 필요합니다.");
            case CommandKind.ScenePatch when HasInvalidSceneSpecSource(parsed):
                throw new CliUsageException("`scene patch`에는 `--spec-file` 또는 `--spec-json` 중 하나만 필요합니다.");
            case CommandKind.ScenePatch when !parsed.Force && ScenePatchContainsDestructiveOperation(parsed):
                throw new CliUsageException("`scene patch`에서 `delete-gameobject` 또는 `remove-component`를 쓰려면 `--force`가 필요합니다.");
            case CommandKind.SceneAddObject when string.IsNullOrWhiteSpace(parsed.ScenePath):
                throw new CliUsageException("`scene add-object`에는 `--path`가 필요합니다.");
            case CommandKind.SceneAddObject when string.IsNullOrWhiteSpace(parsed.SceneObjectName):
                throw new CliUsageException("`scene add-object`에는 `--name`이 필요합니다.");
            case CommandKind.SceneSetTransform when string.IsNullOrWhiteSpace(parsed.ScenePath):
                throw new CliUsageException("`scene set-transform`에는 `--path`가 필요합니다.");
            case CommandKind.SceneSetTransform when string.IsNullOrWhiteSpace(parsed.SceneTarget):
                throw new CliUsageException("`scene set-transform`에는 `--target`이 필요합니다.");
            case CommandKind.SceneSetTransform
                when string.IsNullOrWhiteSpace(parsed.ScenePosition)
                && string.IsNullOrWhiteSpace(parsed.SceneRotation)
                && string.IsNullOrWhiteSpace(parsed.SceneScale):
                throw new CliUsageException("`scene set-transform`에는 `--position`, `--rotation`, `--scale` 중 하나 이상이 필요합니다.");
            case CommandKind.SceneAddComponent when string.IsNullOrWhiteSpace(parsed.ScenePath):
                throw new CliUsageException("`scene add-component`에는 `--path`가 필요합니다.");
            case CommandKind.SceneAddComponent when string.IsNullOrWhiteSpace(parsed.SceneTarget):
                throw new CliUsageException("`scene add-component`에는 `--target`이 필요합니다.");
            case CommandKind.SceneAddComponent when string.IsNullOrWhiteSpace(parsed.SceneComponentType):
                throw new CliUsageException("`scene add-component`에는 `--type`이 필요합니다.");
            case CommandKind.SceneRemoveComponent when string.IsNullOrWhiteSpace(parsed.ScenePath):
                throw new CliUsageException("`scene remove-component`에는 `--path`가 필요합니다.");
            case CommandKind.SceneRemoveComponent when string.IsNullOrWhiteSpace(parsed.SceneTarget):
                throw new CliUsageException("`scene remove-component`에는 `--target`이 필요합니다.");
            case CommandKind.SceneRemoveComponent when string.IsNullOrWhiteSpace(parsed.SceneComponentType):
                throw new CliUsageException("`scene remove-component`에는 `--type`이 필요합니다.");
            case CommandKind.SceneRemoveComponent when !parsed.Force:
                throw new CliUsageException("`scene remove-component`는 `--force`가 필요합니다.");
        }
    }

    private static void ValidateScreenshotOptions(ParsedCommand parsed)
    {
        if (parsed.Kind != CommandKind.Screenshot)
        {
            return;
        }

        bool hasView = !string.IsNullOrWhiteSpace(parsed.ScreenshotView);
        bool hasCamera = !string.IsNullOrWhiteSpace(parsed.ScreenshotCamera);

        if (hasView == hasCamera)
        {
            throw new CliUsageException("`screenshot`에는 `--view` 또는 `--camera` 중 하나만 필요합니다.");
        }
    }

    private static void ValidatePackageOptions(ParsedCommand parsed)
    {
        switch (parsed.Kind)
        {
            case CommandKind.PackageAdd when string.IsNullOrWhiteSpace(parsed.PackageName):
                throw new CliUsageException("`package add`에는 `--name`이 필요합니다.");
            case CommandKind.PackageRemove when string.IsNullOrWhiteSpace(parsed.PackageName):
                throw new CliUsageException("`package remove`에는 `--name`이 필요합니다.");
            case CommandKind.PackageRemove when !parsed.Force:
                throw new CliUsageException("`package remove`는 `--force`가 필요합니다.");
            case CommandKind.PackageSearch when string.IsNullOrWhiteSpace(parsed.PackageQuery):
                throw new CliUsageException("`package search`에는 `--query`가 필요합니다.");
        }
    }

    private static void ValidateMaterialOptions(ParsedCommand parsed)
    {
        switch (parsed.Kind)
        {
            case CommandKind.MaterialInfo when string.IsNullOrWhiteSpace(parsed.MaterialPath):
                throw new CliUsageException("`material info`에는 `--path`가 필요합니다.");
            case CommandKind.MaterialSet when string.IsNullOrWhiteSpace(parsed.MaterialPath):
                throw new CliUsageException("`material set`에는 `--path`가 필요합니다.");
            case CommandKind.MaterialSet:
            {
                bool hasPropertySet = !string.IsNullOrWhiteSpace(parsed.MaterialProperty)
                    && !string.IsNullOrWhiteSpace(parsed.MaterialValue);
                bool hasTextureSet = !string.IsNullOrWhiteSpace(parsed.MaterialTexture)
                    && !string.IsNullOrWhiteSpace(parsed.MaterialTextureAsset);
                if (!hasPropertySet && !hasTextureSet)
                {
                    throw new CliUsageException("`material set`에는 `--property`+`--value` 또는 `--texture`+`--asset` 조합이 필요합니다.");
                }

                break;
            }
        }
    }

    private static void ValidateExecuteOptions(ParsedCommand parsed)
    {
        if (parsed.Kind != CommandKind.ExecuteCode)
        {
            return;
        }

        bool hasCode = !string.IsNullOrWhiteSpace(parsed.ExecuteCodeSnippet);
        bool hasFile = !string.IsNullOrWhiteSpace(parsed.ExecuteCodeFile);

        if (hasCode == hasFile)
        {
            throw new CliUsageException("`execute`에는 `--code` 또는 `--file` 중 하나만 필요합니다.");
        }

        if (!parsed.Force)
        {
            throw new CliUsageException("`execute`는 `--force`가 필요합니다.");
        }
    }

    private static bool HasInvalidPrefabSpecSource(ParsedCommand parsed)
    {
        bool hasFile = !string.IsNullOrWhiteSpace(parsed.PrefabSpecFile);
        bool hasInline = !string.IsNullOrWhiteSpace(parsed.PrefabSpecJson);
        return hasFile == hasInline;
    }

    private static bool HasInvalidSceneSpecSource(ParsedCommand parsed)
    {
        bool hasFile = !string.IsNullOrWhiteSpace(parsed.SceneSpecFile);
        bool hasInline = !string.IsNullOrWhiteSpace(parsed.SceneSpecJson);
        return hasFile == hasInline;
    }

    private static bool ScenePatchContainsDestructiveOperation(ParsedCommand parsed)
    {
        string specJson = parsed.ResolveSceneSpecJson();

        try
        {
            using var document = JsonDocument.Parse(specJson);
            if (!document.RootElement.TryGetProperty("operations", out JsonElement operations)
                || operations.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (JsonElement operation in operations.EnumerateArray())
            {
                if (!operation.TryGetProperty("op", out JsonElement opElement)
                    || opElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                string? op = opElement.GetString();
                if (string.Equals(op, "delete-gameobject", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(op, "remove-component", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
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
        return BuiltInAssetCreateCatalog.IsBuiltInType(type);
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

}
