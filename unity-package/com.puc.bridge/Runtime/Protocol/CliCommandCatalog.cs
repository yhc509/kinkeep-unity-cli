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
    }

    public sealed class CliCommandDescriptor
    {
        public CliCommandDescriptor(
            string command,
            string synopsis,
            string summary,
            CliCommandGroup group,
            string? protocolCommand,
            bool supportsLocal,
            bool supportsLive,
            bool allowedWhileBusy,
            string[]? notes = null)
        {
            this.command = command;
            this.synopsis = synopsis;
            this.summary = summary;
            this.group = group;
            this.protocolCommand = protocolCommand;
            this.supportsLocal = supportsLocal;
            this.supportsLive = supportsLive;
            this.allowedWhileBusy = allowedWhileBusy;
            this.notes = notes ?? Array.Empty<string>();
        }

        public string command { get; }

        public string synopsis { get; }

        public string summary { get; }

        public CliCommandGroup group { get; }

        public string? protocolCommand { get; }

        public bool supportsLocal { get; }

        public bool supportsLive { get; }
        public bool allowedWhileBusy { get; }

        public string[] notes { get; }
    }

    public static class CliCommandCatalog
    {
        private static readonly CliCommandDescriptor[] Commands =
        {
            new CliCommandDescriptor(
                "status",
                "status",
                "Reports the selected project and live editor state when a running bridge is reachable, with a local fallback when it is not.",
                CliCommandGroup.EditorControl,
                ProtocolConstants.CommandStatus,
                supportsLocal: true,
                supportsLive: true,
                allowedWhileBusy: true,
                notes: new[] { "Falls back to local registry and Unity-path inspection when no live editor is reachable." }),
            new CliCommandDescriptor(
                "compile",
                "compile",
                "Triggers a script compile in the running editor.",
                CliCommandGroup.EditorControl,
                ProtocolConstants.CommandCompile,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false),
            new CliCommandDescriptor(
                "refresh",
                "refresh",
                "Refreshes the AssetDatabase in the running editor.",
                CliCommandGroup.EditorControl,
                ProtocolConstants.CommandRefresh,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false),
            new CliCommandDescriptor(
                "read-console",
                "read-console [--limit N] [--type log|warning|error]",
                "Reads recent editor console entries from a running editor.",
                CliCommandGroup.EditorControl,
                ProtocolConstants.CommandReadConsole,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: true),
            new CliCommandDescriptor(
                "play",
                "play",
                "Starts Play Mode in a running editor.",
                CliCommandGroup.EditorControl,
                ProtocolConstants.CommandPlay,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false),
            new CliCommandDescriptor(
                "pause",
                "pause",
                "Pauses Play Mode in a running editor.",
                CliCommandGroup.EditorControl,
                ProtocolConstants.CommandPause,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false),
            new CliCommandDescriptor(
                "stop",
                "stop",
                "Stops Play Mode in a running editor.",
                CliCommandGroup.EditorControl,
                ProtocolConstants.CommandStop,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false),
            new CliCommandDescriptor(
                "execute-menu",
                "execute-menu --path \"Menu/Item\"",
                "Executes a Unity menu item in a running editor.",
                CliCommandGroup.EditorControl,
                ProtocolConstants.CommandExecuteMenu,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false),
            new CliCommandDescriptor(
                "screenshot",
                "screenshot (--view game|scene | --camera <name>) [--path <output.png>] [--width N] [--height N]",
                "Captures a screenshot from the Game View, Scene View, or a named camera.",
                CliCommandGroup.EditorControl,
                ProtocolConstants.CommandScreenshot,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false,
                notes: new[] { "Live-only." }),
            new CliCommandDescriptor(
                "execute",
                "execute (--code <csharp> | --file <path>) --force",
                "Executes arbitrary C# code in the running editor context; always requires --force.",
                CliCommandGroup.EditorControl,
                ProtocolConstants.CommandExecuteCode,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false,
                notes: new[] { "Live-only.", "Always requires --force as a safety gate.", "C# 5.0 이하 문법만 지원합니다 (CodeDOM 제한)." }),
            new CliCommandDescriptor(
                "custom",
                "custom <command-name> [--json <args>]",
                "Invokes a project-defined custom command registered via [PucCommand] attribute.",
                CliCommandGroup.EditorControl,
                ProtocolConstants.CommandCustom,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false,
                notes: new[] { "Live-only.", "Custom commands are registered via [PucCommand(\"name\")] attribute on static methods." }),
            new CliCommandDescriptor(
                "asset find",
                "asset find --name <term> [--type <type>] [--folder <Assets/...>] [--limit N]",
                "Finds assets by name, optional type, and optional folder.",
                CliCommandGroup.AssetWorkflows,
                ProtocolConstants.CommandAssetFind,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: true),
            new CliCommandDescriptor(
                "asset types",
                "asset types",
                "Lists built-in and project extension asset-create type descriptors available to the target project.",
                CliCommandGroup.AssetWorkflows,
                ProtocolConstants.CommandAssetTypes,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: true),
            new CliCommandDescriptor(
                "asset info",
                "asset info (--path <Assets/...> | --guid <guid>)",
                "Reads asset metadata by path or GUID.",
                CliCommandGroup.AssetWorkflows,
                ProtocolConstants.CommandAssetInfo,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: true),
            new CliCommandDescriptor(
                "asset reimport",
                "asset reimport --path <Assets/...>",
                "Reimports an existing asset.",
                CliCommandGroup.AssetWorkflows,
                ProtocolConstants.CommandAssetReimport,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false),
            new CliCommandDescriptor(
                "asset mkdir",
                "asset mkdir --path <Assets/...>",
                "Creates missing folders under `Assets/...`.",
                CliCommandGroup.AssetWorkflows,
                ProtocolConstants.CommandAssetMkdir,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false),
            new CliCommandDescriptor(
                "asset move",
                "asset move --from <Assets/...> --to <Assets/...> [--force]",
                "Moves an asset to a new path; overwriting the destination requires --force.",
                CliCommandGroup.AssetWorkflows,
                ProtocolConstants.CommandAssetMove,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false,
                notes: new[] { "Overwriting an existing target requires --force." }),
            new CliCommandDescriptor(
                "asset rename",
                "asset rename --path <Assets/...> --name <newName> [--force]",
                "Renames an asset in place; overwriting the destination requires --force.",
                CliCommandGroup.AssetWorkflows,
                ProtocolConstants.CommandAssetRename,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false,
                notes: new[] { "Overwriting an existing target requires --force." }),
            new CliCommandDescriptor(
                "asset delete",
                "asset delete --path <Assets/...> --force",
                "Deletes an asset and always requires --force.",
                CliCommandGroup.AssetWorkflows,
                ProtocolConstants.CommandAssetDelete,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false,
                notes: new[] { "Deletion is always gated by --force." }),
            new CliCommandDescriptor(
                "asset create",
                "asset create --type <kind> --path <Assets/...> [--data-json <json>] [options]",
                "Creates a built-in or extension asset type; overwriting an existing asset requires --force.",
                CliCommandGroup.AssetWorkflows,
                ProtocolConstants.CommandAssetCreate,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false,
                notes: new[] { "This repo ships the built-in asset types documented below.", "Runtime extension providers can add more types." }),
            new CliCommandDescriptor(
                "scene open",
                "scene open --path <Assets/...> [--force]",
                "Opens a saved scene asset; use --force to discard dirty loaded scenes.",
                CliCommandGroup.SceneWorkflows,
                ProtocolConstants.CommandSceneOpen,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false),
            new CliCommandDescriptor(
                "scene inspect",
                "scene inspect --path <Assets/...> [--with-values]",
                "Inspects a saved scene hierarchy; use --with-values when authoring scene patch specs.",
                CliCommandGroup.SceneWorkflows,
                ProtocolConstants.CommandSceneInspect,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false,
                notes: new[] { "Use --with-values before authoring a patch spec.", "Detailed scene patch rules live in docs/scene-spec.md." }),
            new CliCommandDescriptor(
                "scene patch",
                "scene patch --path <Assets/...> (--spec-file <file.json> | --spec-json <json>) [--force]",
                "Applies a deterministic scene patch spec; destructive operations require --force.",
                CliCommandGroup.SceneWorkflows,
                ProtocolConstants.CommandScenePatch,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false,
                notes: new[] { "Detailed scene patch rules live in docs/scene-spec.md." }),
            new CliCommandDescriptor(
                "scene add-object",
                "scene add-object --path <Assets/...> [--parent <scenePath>] --name <name> [--components \"Type1,Type2\"]",
                "Adds a new GameObject to a scene; shortcut for a single add-gameobject scene patch operation.",
                CliCommandGroup.SceneWorkflows,
                ProtocolConstants.CommandScenePatch,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false,
                notes: new[] { "Internally delegates to scene patch." }),
            new CliCommandDescriptor(
                "scene set-transform",
                "scene set-transform --path <Assets/...> --target <scenePath> (--position x,y,z | --rotation x,y,z | --scale x,y,z)",
                "Sets the transform of a GameObject; shortcut for a single modify-gameobject scene patch operation.",
                CliCommandGroup.SceneWorkflows,
                ProtocolConstants.CommandScenePatch,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false,
                notes: new[] { "Internally delegates to scene patch." }),
            new CliCommandDescriptor(
                "scene add-component",
                "scene add-component --path <Assets/...> --target <scenePath> --type <ComponentType> [--values <json>]",
                "Adds a component to a GameObject; shortcut for a single add-component scene patch operation.",
                CliCommandGroup.SceneWorkflows,
                ProtocolConstants.CommandScenePatch,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false,
                notes: new[] { "Internally delegates to scene patch." }),
            new CliCommandDescriptor(
                "scene remove-component",
                "scene remove-component --path <Assets/...> --target <scenePath> --type <ComponentType> --force",
                "Removes a component from a GameObject; shortcut for a single remove-component scene patch operation.",
                CliCommandGroup.SceneWorkflows,
                ProtocolConstants.CommandScenePatch,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false,
                notes: new[] { "Always requires --force.", "Internally delegates to scene patch." }),
            new CliCommandDescriptor(
                "prefab inspect",
                "prefab inspect --path <Assets/...> [--with-values]",
                "Inspects prefab hierarchy and serialized property paths; use --with-values when authoring patch specs.",
                CliCommandGroup.PrefabWorkflows,
                ProtocolConstants.CommandPrefabInspect,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: true,
                notes: new[] { "Use --with-values before authoring a patch spec.", "Detailed prefab patch rules live in docs/prefab-spec.md." }),
            new CliCommandDescriptor(
                "prefab create",
                "prefab create --path <Assets/...> (--spec-file <file.json> | --spec-json <json>) [--force]",
                "Creates a prefab from a JSON structure spec; use --force to overwrite an existing asset.",
                CliCommandGroup.PrefabWorkflows,
                ProtocolConstants.CommandPrefabCreate,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false,
                notes: new[] { "Use this instead of asset create --type prefab for structured prefab authoring.", "Detailed prefab patch rules live in docs/prefab-spec.md." }),
            new CliCommandDescriptor(
                "prefab patch",
                "prefab patch --path <Assets/...> (--spec-file <file.json> | --spec-json <json>)",
                "Applies a deterministic patch spec to an existing prefab.",
                CliCommandGroup.PrefabWorkflows,
                ProtocolConstants.CommandPrefabPatch,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false,
                notes: new[] { "Detailed prefab patch rules live in docs/prefab-spec.md." }),
            new CliCommandDescriptor(
                "package list",
                "package list",
                "Lists all installed packages in the project.",
                CliCommandGroup.PackageManagement,
                ProtocolConstants.CommandPackageList,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: true),
            new CliCommandDescriptor(
                "package add",
                "package add --name <package> [--version <version>]",
                "Adds a package to the project; supports registry, git URL, and local paths.",
                CliCommandGroup.PackageManagement,
                ProtocolConstants.CommandPackageAdd,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false,
                notes: new[] { "패키지 작업 중 Editor가 일시 정지될 수 있습니다." }),
            new CliCommandDescriptor(
                "package remove",
                "package remove --name <package> --force",
                "Removes a package from the project; always requires --force.",
                CliCommandGroup.PackageManagement,
                ProtocolConstants.CommandPackageRemove,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false,
                notes: new[] { "Removal is always gated by --force.", "패키지 작업 중 Editor가 일시 정지될 수 있습니다." }),
            new CliCommandDescriptor(
                "package search",
                "package search --query <text>",
                "Searches the Unity registry for packages matching the query.",
                CliCommandGroup.PackageManagement,
                ProtocolConstants.CommandPackageSearch,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: true),
            new CliCommandDescriptor(
                "material info",
                "material info --path <Assets/...mat>",
                "Inspects a material's shader and property values.",
                CliCommandGroup.MaterialWorkflows,
                ProtocolConstants.CommandMaterialInfo,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: true),
            new CliCommandDescriptor(
                "material set",
                "material set --path <Assets/...mat> (--property <name> --value <val> | --texture <name> --asset <Assets/...>)",
                "Sets a material property value or texture.",
                CliCommandGroup.MaterialWorkflows,
                ProtocolConstants.CommandMaterialSet,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false),
            new CliCommandDescriptor(
                "instances list",
                "instances list",
                "Lists known Unity project instances and the active registry selection.",
                CliCommandGroup.InstanceManagement,
                protocolCommand: null,
                supportsLocal: true,
                supportsLive: false,
                allowedWhileBusy: false),
            new CliCommandDescriptor(
                "instances use",
                "instances use <projectHash|projectPath>",
                "Pins the active target project by hash or project path.",
                CliCommandGroup.InstanceManagement,
                protocolCommand: null,
                supportsLocal: true,
                supportsLive: false,
                allowedWhileBusy: false),
            new CliCommandDescriptor(
                "doctor",
                "doctor",
                "Shows registry, project detection, Unity path, and live reachability diagnostics.",
                CliCommandGroup.Diagnostics,
                protocolCommand: null,
                supportsLocal: true,
                supportsLive: false,
                allowedWhileBusy: false),
            new CliCommandDescriptor(
                "raw",
                "raw --json '{\"command\":\"status\",\"arguments\":{}}'",
                "Sends a raw live protocol envelope for low-level debugging.",
                CliCommandGroup.Diagnostics,
                protocolCommand: null,
                supportsLocal: false,
                supportsLive: true,
                allowedWhileBusy: false,
                notes: new[] { "This bypasses typed CLI validation." }),
        };

        public static CliCommandDescriptor[] GetCommands()
        {
            return (CliCommandDescriptor[])Commands.Clone();
        }

        public static string BuildHelpText()
        {
            var builder = new StringBuilder();
            builder.AppendLine("usage: unity-cli [--json] [--project <path>] <command> [options]");
            builder.AppendLine();
            builder.AppendLine("commands:");
            foreach (CliCommandDescriptor command in Commands)
            {
                builder.Append("  ");
                builder.AppendLine(command.synopsis);
            }

            return builder.ToString();
        }

        public static string[] GetSupportedProtocolCommands()
        {
            var commands = new List<string> { ProtocolConstants.CommandPing };
            foreach (CliCommandDescriptor descriptor in Commands)
            {
                if (descriptor.supportsLive && !string.IsNullOrWhiteSpace(descriptor.protocolCommand))
                {
                    commands.Add(descriptor.protocolCommand!);
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
            return descriptor is not null && descriptor.allowedWhileBusy;
        }

        public static bool IsProtocolCommandInGroup(string command, CliCommandGroup group)
        {
            CliCommandDescriptor? descriptor = FindByProtocolCommand(command);
            return descriptor is not null && descriptor.group == group;
        }

        public static CliCommandDescriptor? FindByCommand(string command)
        {
            foreach (CliCommandDescriptor descriptor in Commands)
            {
                if (string.Equals(descriptor.command, command, StringComparison.Ordinal))
                {
                    return descriptor;
                }
            }

            return null;
        }

        public static CliCommandDescriptor? FindByProtocolCommand(string command)
        {
            foreach (CliCommandDescriptor descriptor in Commands)
            {
                if (!string.IsNullOrWhiteSpace(descriptor.protocolCommand)
                    && string.Equals(descriptor.protocolCommand, command, StringComparison.Ordinal))
                {
                    return descriptor;
                }
            }

            return null;
        }
    }
}
