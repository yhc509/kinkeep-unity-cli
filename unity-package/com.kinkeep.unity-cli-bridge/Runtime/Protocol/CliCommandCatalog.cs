#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

namespace UnityCli.Protocol
{
    public enum CliCommandGroup
    {
        EditorControl,
        AssetWorkflows,
        SceneWorkflows,
        PrefabWorkflows,
        InstanceManagement,
        Diagnostics,
        PackageManagement,
        MaterialWorkflows,
        QaWorkflows,
    }

    public sealed class CliCommandDescriptor
    {
        public CliCommandDescriptor(
            string command,
            string synopsis,
            string summary,
            CliCommandGroup group,
            string? protocolCommand,
            bool canUseLocal,
            bool canUseLive,
            bool isAllowedWhileBusy,
            string[]? notes = null)
        {
            Command = command;
            Synopsis = synopsis;
            Summary = summary;
            Group = group;
            ProtocolCommand = protocolCommand;
            CanUseLocal = canUseLocal;
            CanUseLive = canUseLive;
            IsAllowedWhileBusy = isAllowedWhileBusy;
            Notes = notes ?? Array.Empty<string>();
        }

        public string Command { get; }

        public string Synopsis { get; }

        public string Summary { get; }

        public CliCommandGroup Group { get; }

        public string? ProtocolCommand { get; }

        public bool CanUseLocal { get; }

        public bool CanUseLive { get; }
        public bool IsAllowedWhileBusy { get; }

        public string[] Notes { get; }
    }

    public static class CliCommandCatalog
    {
        private static readonly CliCommandDescriptor[] _commands =
        {
            new CliCommandDescriptor(
                "status",
                "status",
                "Reports the selected project and live editor state when a running bridge is reachable, with a local fallback when it is not.",
                CliCommandGroup.EditorControl,
                ProtocolConstants.CommandStatus,
                canUseLocal: true,
                canUseLive: true,
                isAllowedWhileBusy: true,
                notes: new[] { "Falls back to local registry and Unity-path inspection when no live editor is reachable." }),
            new CliCommandDescriptor(
                "compile",
                "compile",
                "Triggers a script compile in the running editor.",
                CliCommandGroup.EditorControl,
                ProtocolConstants.CommandCompile,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false),
            new CliCommandDescriptor(
                "refresh",
                "refresh",
                "Refreshes the AssetDatabase in the running editor.",
                CliCommandGroup.EditorControl,
                ProtocolConstants.CommandRefresh,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false),
            new CliCommandDescriptor(
                "read-console",
                "read-console [--limit N] [--type log|warning|error]",
                "Reads recent editor console entries from a running editor.",
                CliCommandGroup.EditorControl,
                ProtocolConstants.CommandReadConsole,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: true),
            new CliCommandDescriptor(
                "play",
                "play",
                "Starts Play Mode in a running editor.",
                CliCommandGroup.EditorControl,
                ProtocolConstants.CommandPlay,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false),
            new CliCommandDescriptor(
                "pause",
                "pause",
                "Pauses Play Mode in a running editor.",
                CliCommandGroup.EditorControl,
                ProtocolConstants.CommandPause,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false),
            new CliCommandDescriptor(
                "stop",
                "stop",
                "Stops Play Mode in a running editor.",
                CliCommandGroup.EditorControl,
                ProtocolConstants.CommandStop,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false),
            new CliCommandDescriptor(
                "execute-menu",
                "execute-menu (--path \"Menu/Item\" | --list \"Prefix\")",
                "Executes a Unity menu item or lists registered menu items matching a prefix in a running editor.",
                CliCommandGroup.EditorControl,
                ProtocolConstants.CommandExecuteMenu,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false,
                notes: new[] { "Use --list to inspect registered menu item paths before executing one." }),
            new CliCommandDescriptor(
                "screenshot",
                "screenshot (--view game|scene | --camera <name>) [--path <output.png>] [--width N] [--height N]",
                "Captures a screenshot from the Game View, Scene View, or a named camera. In Play Mode, --view game can downscale the native Game View capture but does not upscale it.",
                CliCommandGroup.EditorControl,
                ProtocolConstants.CommandScreenshot,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false,
                notes: new[]
                {
                    "Live-only.",
                    "Play Mode --view game captures at the native Game View size first; larger --width/--height requests warn and save at native resolution instead of upscaling.",
                }),
            new CliCommandDescriptor(
                "execute",
                "execute (--code <csharp> | --file <path>) --force",
                "Executes arbitrary C# code in the running editor context; always requires --force.",
                CliCommandGroup.EditorControl,
                ProtocolConstants.CommandExecuteCode,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false,
                notes: new[] { "Live-only.", "Always requires --force as a safety gate.", "C# 5.0 이하 문법만 지원합니다 (CodeDOM 제한)." }),
            new CliCommandDescriptor(
                "custom",
                "custom <command-name> [--json <args>]",
                "Invokes a project-defined custom command registered via [PucCommand] attribute.",
                CliCommandGroup.EditorControl,
                ProtocolConstants.CommandCustom,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false,
                notes: new[] { "Live-only.", "Custom commands are registered via [PucCommand(\"name\")] attribute on static methods." }),
            new CliCommandDescriptor(
                "asset find",
                "asset find --name <term> [--type <type>] [--folder <Assets/...>] [--limit N]",
                "Finds assets by name, optional type, and optional folder.",
                CliCommandGroup.AssetWorkflows,
                ProtocolConstants.CommandAssetFind,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: true),
            new CliCommandDescriptor(
                "asset types",
                "asset types",
                "Lists built-in and project extension asset-create type descriptors available to the target project.",
                CliCommandGroup.AssetWorkflows,
                ProtocolConstants.CommandAssetTypes,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: true),
            new CliCommandDescriptor(
                "asset info",
                "asset info (--path <Assets/...> | --guid <guid>)",
                "Reads asset metadata by path or GUID.",
                CliCommandGroup.AssetWorkflows,
                ProtocolConstants.CommandAssetInfo,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: true),
            new CliCommandDescriptor(
                "asset reimport",
                "asset reimport --path <Assets/...>",
                "Reimports an existing asset.",
                CliCommandGroup.AssetWorkflows,
                ProtocolConstants.CommandAssetReimport,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false),
            new CliCommandDescriptor(
                "asset mkdir",
                "asset mkdir --path <Assets/...>",
                "Creates missing folders under `Assets/...`.",
                CliCommandGroup.AssetWorkflows,
                ProtocolConstants.CommandAssetMkdir,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false),
            new CliCommandDescriptor(
                "asset move",
                "asset move --from <Assets/...> --to <Assets/...> [--force]",
                "Moves an asset to a new path; overwriting the destination requires --force.",
                CliCommandGroup.AssetWorkflows,
                ProtocolConstants.CommandAssetMove,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false,
                notes: new[] { "Overwriting an existing target requires --force." }),
            new CliCommandDescriptor(
                "asset rename",
                "asset rename --path <Assets/...> --name <newName> [--force]",
                "Renames an asset in place; overwriting the destination requires --force.",
                CliCommandGroup.AssetWorkflows,
                ProtocolConstants.CommandAssetRename,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false,
                notes: new[] { "Overwriting an existing target requires --force." }),
            new CliCommandDescriptor(
                "asset delete",
                "asset delete --path <Assets/...> --force",
                "Deletes an asset and always requires --force.",
                CliCommandGroup.AssetWorkflows,
                ProtocolConstants.CommandAssetDelete,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false,
                notes: new[] { "Deletion is always gated by --force." }),
            new CliCommandDescriptor(
                "asset create",
                "asset create --type <kind> --path <Assets/...> [--data-json <json>] [options]",
                "Creates a built-in or extension asset type; overwriting an existing asset requires --force.",
                CliCommandGroup.AssetWorkflows,
                ProtocolConstants.CommandAssetCreate,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false,
                notes: new[] { "This repo ships the built-in asset types documented below.", "Runtime extension providers can add more types." }),
            new CliCommandDescriptor(
                "scene open",
                "scene open --path <Assets/...> [--force]",
                "Opens a saved scene asset; use --force to discard dirty loaded scenes.",
                CliCommandGroup.SceneWorkflows,
                ProtocolConstants.CommandSceneOpen,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false),
            new CliCommandDescriptor(
                "scene inspect",
                "scene inspect --path <Assets/...> [--with-values]",
                "Inspects a saved scene hierarchy; use --with-values when authoring scene patch specs.",
                CliCommandGroup.SceneWorkflows,
                ProtocolConstants.CommandSceneInspect,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false,
                notes: new[] { "Use --with-values before authoring a patch spec.", "Detailed scene patch rules live in docs/scene-spec.md." }),
            new CliCommandDescriptor(
                "scene patch",
                "scene patch --path <Assets/...> (--spec-file <file.json> | --spec-json <json>) [--force]",
                "Applies a deterministic scene patch spec; destructive operations require --force.",
                CliCommandGroup.SceneWorkflows,
                ProtocolConstants.CommandScenePatch,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false,
                notes: new[] { "Detailed scene patch rules live in docs/scene-spec.md." }),
            new CliCommandDescriptor(
                "scene add-object",
                "scene add-object --path <Assets/...> [--parent <scenePath>] --name <name> [--components \"Type1,Type2\"]",
                "Adds a new GameObject to a scene; shortcut for a single add-gameobject scene patch operation.",
                CliCommandGroup.SceneWorkflows,
                ProtocolConstants.CommandScenePatch,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false,
                notes: new[] { "Internally delegates to scene patch." }),
            new CliCommandDescriptor(
                "scene set-transform",
                "scene set-transform --path <Assets/...> --target <scenePath> (--position x,y,z | --rotation x,y,z | --scale x,y,z)",
                "Sets the transform of a GameObject; shortcut for a single modify-gameobject scene patch operation.",
                CliCommandGroup.SceneWorkflows,
                ProtocolConstants.CommandScenePatch,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false,
                notes: new[] { "Internally delegates to scene patch." }),
            new CliCommandDescriptor(
                "scene add-component",
                "scene add-component --path <Assets/...> --target <scenePath> --type <ComponentType> [--values <json>]",
                "Adds a component to a GameObject; shortcut for a single add-component scene patch operation.",
                CliCommandGroup.SceneWorkflows,
                ProtocolConstants.CommandScenePatch,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false,
                notes: new[] { "Internally delegates to scene patch." }),
            new CliCommandDescriptor(
                "scene remove-component",
                "scene remove-component --path <Assets/...> --target <scenePath> --type <ComponentType> --force",
                "Removes a component from a GameObject; shortcut for a single remove-component scene patch operation.",
                CliCommandGroup.SceneWorkflows,
                ProtocolConstants.CommandScenePatch,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false,
                notes: new[] { "Always requires --force.", "Internally delegates to scene patch." }),
            new CliCommandDescriptor(
                "prefab inspect",
                "prefab inspect --path <Assets/...> [--with-values]",
                "Inspects prefab hierarchy and serialized property paths; use --with-values when authoring patch specs.",
                CliCommandGroup.PrefabWorkflows,
                ProtocolConstants.CommandPrefabInspect,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: true,
                notes: new[] { "Use --with-values before authoring a patch spec.", "Detailed prefab patch rules live in docs/prefab-spec.md." }),
            new CliCommandDescriptor(
                "prefab create",
                "prefab create --path <Assets/...> (--spec-file <file.json> | --spec-json <json>) [--force]",
                "Creates a prefab from a JSON structure spec; use --force to overwrite an existing asset.",
                CliCommandGroup.PrefabWorkflows,
                ProtocolConstants.CommandPrefabCreate,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false,
                notes: new[] { "Use this instead of asset create --type prefab for structured prefab authoring.", "Detailed prefab patch rules live in docs/prefab-spec.md." }),
            new CliCommandDescriptor(
                "prefab patch",
                "prefab patch --path <Assets/...> (--spec-file <file.json> | --spec-json <json>)",
                "Applies a deterministic patch spec to an existing prefab.",
                CliCommandGroup.PrefabWorkflows,
                ProtocolConstants.CommandPrefabPatch,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false,
                notes: new[] { "Detailed prefab patch rules live in docs/prefab-spec.md." }),
            new CliCommandDescriptor(
                "package list",
                "package list",
                "Lists all installed packages in the project.",
                CliCommandGroup.PackageManagement,
                ProtocolConstants.CommandPackageList,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: true),
            new CliCommandDescriptor(
                "package add",
                "package add --name <package> [--version <version>]",
                "Adds a package to the project; supports registry, git URL, and local paths.",
                CliCommandGroup.PackageManagement,
                ProtocolConstants.CommandPackageAdd,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false,
                notes: new[] { "패키지 작업 중 Editor가 일시 정지될 수 있습니다." }),
            new CliCommandDescriptor(
                "package remove",
                "package remove --name <package> --force",
                "Removes a package from the project; always requires --force.",
                CliCommandGroup.PackageManagement,
                ProtocolConstants.CommandPackageRemove,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false,
                notes: new[] { "Removal is always gated by --force.", "패키지 작업 중 Editor가 일시 정지될 수 있습니다." }),
            new CliCommandDescriptor(
                "package search",
                "package search --query <text>",
                "Searches the Unity registry for packages matching the query.",
                CliCommandGroup.PackageManagement,
                ProtocolConstants.CommandPackageSearch,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: true),
            new CliCommandDescriptor(
                "material info",
                "material info --path <Assets/...mat>",
                "Inspects a material's shader and property values.",
                CliCommandGroup.MaterialWorkflows,
                ProtocolConstants.CommandMaterialInfo,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: true),
            new CliCommandDescriptor(
                "material set",
                "material set --path <Assets/...mat> (--property <name> --value <val> | --texture <name> --asset <Assets/...>)",
                "Sets a material property value or texture.",
                CliCommandGroup.MaterialWorkflows,
                ProtocolConstants.CommandMaterialSet,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false),
            new CliCommandDescriptor(
                "qa click",
                "qa click (--qa-id <id> | --target <path>)",
                "Clicks a UI element identified by QA ID or GameObject path; requires Play Mode.",
                CliCommandGroup.QaWorkflows,
                ProtocolConstants.CommandQaClick,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false),
            new CliCommandDescriptor(
                "qa tap",
                "qa tap --x <int> --y <int>",
                "Taps at a screen coordinate; requires Play Mode.",
                CliCommandGroup.QaWorkflows,
                ProtocolConstants.CommandQaTap,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false),
            new CliCommandDescriptor(
                "qa swipe",
                "qa swipe [--target <path>] --from <x,y> --to <x,y> [--duration <ms>]",
                "Swipes over multiple frames; when --target is supplied, --from/--to become pixel offsets from the target RectTransform center before resolving to screen coordinates; requires Play Mode.",
                CliCommandGroup.QaWorkflows,
                ProtocolConstants.CommandQaSwipe,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false),
            new CliCommandDescriptor(
                "qa key",
                "qa key --key <keyName>",
                "Simulates a key press via Input System; requires Play Mode.",
                CliCommandGroup.QaWorkflows,
                ProtocolConstants.CommandQaKey,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false),
            new CliCommandDescriptor(
                "qa wait",
                "qa wait --ms <int>",
                "Waits for the specified number of milliseconds (local only, does not contact the editor).",
                CliCommandGroup.QaWorkflows,
                protocolCommand: null,
                canUseLocal: true,
                canUseLive: false,
                isAllowedWhileBusy: false),
            new CliCommandDescriptor(
                "qa wait-until",
                "qa wait-until (--scene <name> | --log-contains <text> | --object-exists <qa-id|path>) [--timeout <ms>]",
                "Polls the editor until a condition is met or timeout expires; requires Play Mode.",
                CliCommandGroup.QaWorkflows,
                ProtocolConstants.CommandQaWaitUntil,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false),
            new CliCommandDescriptor(
                "instances list",
                "instances list",
                "Lists known Unity project instances and the active registry selection.",
                CliCommandGroup.InstanceManagement,
                protocolCommand: null,
                canUseLocal: true,
                canUseLive: false,
                isAllowedWhileBusy: false),
            new CliCommandDescriptor(
                "instances use",
                "instances use <projectHash|projectPath|projectName>",
                "Pins the active target project by hash, project path, or registered project name. Existing directory paths win over name matches.",
                CliCommandGroup.InstanceManagement,
                protocolCommand: null,
                canUseLocal: true,
                canUseLive: false,
                isAllowedWhileBusy: false),
            new CliCommandDescriptor(
                "doctor",
                "doctor",
                "Shows registry, project detection, Unity path, and live reachability diagnostics.",
                CliCommandGroup.Diagnostics,
                protocolCommand: null,
                canUseLocal: true,
                canUseLive: false,
                isAllowedWhileBusy: false),
            new CliCommandDescriptor(
                "raw",
                "raw --json '{\"command\":\"status\",\"arguments\":{}}'",
                "Sends a raw live protocol envelope for low-level debugging.",
                CliCommandGroup.Diagnostics,
                protocolCommand: null,
                canUseLocal: false,
                canUseLive: true,
                isAllowedWhileBusy: false,
                notes: new[] { "This bypasses typed CLI validation." }),
        };

        public static CliCommandDescriptor[] GetCommands()
        {
            return (CliCommandDescriptor[])_commands.Clone();
        }

        public static string BuildHelpText()
        {
            var builder = new StringBuilder();
            builder.AppendLine("usage: unity-cli [--json] [--project <path|name>] <command> [options]");
            builder.AppendLine();
            builder.AppendLine("options:");
            builder.AppendLine("  --project <path|name>  Existing directory paths take precedence over registered project names. Project-name matches are case-insensitive.");
            builder.AppendLine();
            builder.AppendLine("commands:");
            foreach (CliCommandDescriptor command in _commands)
            {
                builder.Append("  ");
                builder.AppendLine(command.Synopsis);
            }

            return builder.ToString();
        }

        public static string[] GetSupportedProtocolCommands()
        {
            var commands = new List<string> { ProtocolConstants.CommandPing };
            foreach (CliCommandDescriptor descriptor in _commands)
            {
                if (descriptor.CanUseLive && !string.IsNullOrWhiteSpace(descriptor.ProtocolCommand))
                {
                    commands.Add(descriptor.ProtocolCommand!);
                }
            }

            return commands.ToArray();
        }

        public static bool IsCommandAllowedWhileBusy(string command)
        {
            if (string.Equals(command, ProtocolConstants.CommandPing, StringComparison.Ordinal))
            {
                return true;
            }

            CliCommandDescriptor? descriptor = FindByProtocolCommand(command);
            return descriptor is not null && descriptor.IsAllowedWhileBusy;
        }

        public static bool IsProtocolCommandInGroup(string command, CliCommandGroup group)
        {
            CliCommandDescriptor? descriptor = FindByProtocolCommand(command);
            return descriptor is not null && descriptor.Group == group;
        }

        public static CliCommandDescriptor? FindByCommand(string command)
        {
            foreach (CliCommandDescriptor descriptor in _commands)
            {
                if (string.Equals(descriptor.Command, command, StringComparison.Ordinal))
                {
                    return descriptor;
                }
            }

            return null;
        }

        public static CliCommandDescriptor? FindByProtocolCommand(string command)
        {
            foreach (CliCommandDescriptor descriptor in _commands)
            {
                if (!string.IsNullOrWhiteSpace(descriptor.ProtocolCommand)
                    && string.Equals(descriptor.ProtocolCommand, command, StringComparison.Ordinal))
                {
                    return descriptor;
                }
            }

            return null;
        }
    }
}
