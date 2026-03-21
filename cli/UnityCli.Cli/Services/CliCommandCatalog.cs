using UnityCli.Cli.Models;
using ProtocolCliCommandCatalog = UnityCli.Protocol.CliCommandCatalog;

namespace UnityCli.Cli.Services;

public static class CliCommandMetadata
{
    private static readonly IReadOnlyDictionary<CommandKind, string> CommandPaths = new Dictionary<CommandKind, string>
    {
        [CommandKind.Status] = "status",
        [CommandKind.Compile] = "compile",
        [CommandKind.Refresh] = "refresh",
        [CommandKind.RunTests] = "run-tests",
        [CommandKind.ReadConsole] = "read-console",
        [CommandKind.Play] = "play",
        [CommandKind.Pause] = "pause",
        [CommandKind.Stop] = "stop",
        [CommandKind.ExecuteMenu] = "execute-menu",
        [CommandKind.Screenshot] = "screenshot",
        [CommandKind.ExecuteCode] = "execute",
        [CommandKind.PackageList] = "package list",
        [CommandKind.PackageAdd] = "package add",
        [CommandKind.PackageRemove] = "package remove",
        [CommandKind.PackageSearch] = "package search",
        [CommandKind.MaterialInfo] = "material info",
        [CommandKind.MaterialSet] = "material set",
        [CommandKind.AssetFind] = "asset find",
        [CommandKind.Custom] = "custom",
        [CommandKind.AssetTypes] = "asset types",
        [CommandKind.AssetInfo] = "asset info",
        [CommandKind.AssetReimport] = "asset reimport",
        [CommandKind.AssetMkdir] = "asset mkdir",
        [CommandKind.AssetMove] = "asset move",
        [CommandKind.AssetRename] = "asset rename",
        [CommandKind.AssetDelete] = "asset delete",
        [CommandKind.AssetCreate] = "asset create",
        [CommandKind.SceneOpen] = "scene open",
        [CommandKind.SceneInspect] = "scene inspect",
        [CommandKind.ScenePatch] = "scene patch",
        [CommandKind.SceneAddObject] = "scene add-object",
        [CommandKind.SceneSetTransform] = "scene set-transform",
        [CommandKind.SceneAddComponent] = "scene add-component",
        [CommandKind.SceneRemoveComponent] = "scene remove-component",
        [CommandKind.PrefabInspect] = "prefab inspect",
        [CommandKind.PrefabCreate] = "prefab create",
        [CommandKind.PrefabPatch] = "prefab patch",
        [CommandKind.InstancesList] = "instances list",
        [CommandKind.InstancesUse] = "instances use",
        [CommandKind.Doctor] = "doctor",
        [CommandKind.Raw] = "raw",
    };

    public static string BuildHelpText()
    {
        return ProtocolCliCommandCatalog.BuildHelpText().TrimEnd();
    }

    public static bool SupportsBatch(CommandKind kind)
    {
        return CommandPaths.TryGetValue(kind, out string? commandPath)
            && ProtocolCliCommandCatalog.FindByCommand(commandPath)?.supportsBatch == true;
    }
}
