using System;
using System.IO;
using Newtonsoft.Json;
using UnityCli.Protocol;
using UnityEditor;
using UnityEngine;

namespace KinKeep.UnityCli.Bridge.Editor
{
    internal sealed partial class PrefabCommandHandler
    {
        private static readonly JsonSerializer _serializer = JsonSerializer.CreateDefault(BridgeJsonSettings.CamelCaseIgnoreNull);

        public bool CanHandle(string command)
        {
            return ProtocolHelpers.IsPrefabCommand(command);
        }

        public string Handle(string command, string argumentsJson)
        {
            switch (command)
            {
                case ProtocolConstants.CommandPrefabInspect:
                    return HandleInspect(argumentsJson);
                case ProtocolConstants.CommandPrefabCreate:
                    return HandleCreate(argumentsJson);
                case ProtocolConstants.CommandPrefabPatch:
                    return HandlePatch(argumentsJson);
                default:
                    throw new InvalidOperationException("지원하지 않는 prefab 명령입니다: " + command);
            }
        }

        private static string HandleInspect(string argumentsJson)
        {
            PrefabInspectArgs args = ProtocolJson.Deserialize<PrefabInspectArgs>(argumentsJson) ?? new PrefabInspectArgs();
            string path = RequireExistingPrefabPath(args.path, "prefab-inspect");
            GameObject root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                return PrefabInspector.BuildInspectPayload(path, root, args.withValues);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string HandleCreate(string argumentsJson)
        {
            PrefabCreateArgs args = ProtocolJson.Deserialize<PrefabCreateArgs>(argumentsJson) ?? new PrefabCreateArgs();
            string path = ResolvePrefabPath(args.path);
            AssetCommandSupport.EnsureParentFolderExists(path);
            bool isOverwritten = AssetCommandSupport.DeleteIfTargetExists(path, args.force, "prefab-create");

            PrefabCreateSpec spec = DeserializeSpec<PrefabCreateSpec>(args.specJson, "prefab-create");
            ValidateVersion(spec.Version, "prefab-create");
            if (spec.Root == null)
            {
                throw new CommandFailureException("PREFAB_SPEC_INVALID", "`root`가 필요합니다.");
            }

            string rootName = string.IsNullOrWhiteSpace(spec.Root.Name)
                ? Path.GetFileNameWithoutExtension(path)
                : spec.Root.Name.Trim();
            var root = new GameObject(rootName);

            try
            {
                PrefabInspector.ApplyNodeState(root, spec.Root, rootName, allowMissingName: true);
                AddComponents(root, spec.Root.Components, "prefab-create");
                AddChildren(root.transform, spec.Root.Children, "prefab-create");

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (saved == null)
                {
                    throw new CommandFailureException("PREFAB_SAVE_FAILED", "prefab을 저장하지 못했습니다: " + path);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return ProtocolJson.Serialize(new PrefabMutationPayload
            {
                asset = AssetCommandSupport.BuildRecordFromPath(path),
                created = true,
                overwritten = isOverwritten,
            });
        }

        private static string HandlePatch(string argumentsJson)
        {
            PrefabPatchArgs args = ProtocolJson.Deserialize<PrefabPatchArgs>(argumentsJson) ?? new PrefabPatchArgs();
            string path = RequireExistingPrefabPath(args.path, "prefab-patch");

            PrefabPatchSpec spec = DeserializeSpec<PrefabPatchSpec>(args.specJson, "prefab-patch");
            ValidateVersion(spec.Version, "prefab-patch");
            if (spec.Operations == null || spec.Operations.Length == 0)
            {
                throw new CommandFailureException("PREFAB_SPEC_INVALID", "`operations`가 비어 있습니다.");
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ApplyPatchOperations(root, spec.Operations);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (saved == null)
                {
                    throw new CommandFailureException("PREFAB_SAVE_FAILED", "prefab을 저장하지 못했습니다: " + path);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return ProtocolJson.Serialize(new PrefabMutationPayload
            {
                asset = AssetCommandSupport.BuildRecordFromPath(path),
                patched = true,
            });
        }

        private static T DeserializeSpec<T>(string specJson, string commandName) where T : class
        {
            if (string.IsNullOrWhiteSpace(specJson))
            {
                throw new CommandFailureException("PREFAB_SPEC_INVALID", commandName + " spec이 비어 있습니다.");
            }

            try
            {
                T spec = JsonConvert.DeserializeObject<T>(specJson, BridgeJsonSettings.CamelCaseIgnoreNull);

                if (spec == null)
                {
                    throw new CommandFailureException("PREFAB_SPEC_INVALID", commandName + " spec을 해석하지 못했습니다.");
                }

                return spec;
            }
            catch (JsonException exception)
            {
                throw new CommandFailureException("PREFAB_SPEC_INVALID", commandName + " spec JSON이 잘못되었습니다.", exception.Message);
            }
        }

        private static void ValidateVersion(int version, string commandName)
        {
            if (version > 1)
            {
                throw new CommandFailureException("PREFAB_SPEC_INVALID", commandName + " spec version을 지원하지 않습니다: " + version);
            }
        }

        private static string ResolvePrefabPath(string path)
        {
            string normalizedPath = AssetCommandSupport.NormalizeAssetPath(path);
            string extension = Path.GetExtension(normalizedPath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                return normalizedPath + ".prefab";
            }

            if (!string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase))
            {
                throw new CommandFailureException("PREFAB_PATH_INVALID", "prefab 경로는 `.prefab` 확장자를 사용해야 합니다: " + normalizedPath);
            }

            return normalizedPath;
        }

        private static string RequireExistingPrefabPath(string path, string commandName)
        {
            string prefabPath = ResolvePrefabPath(path);
            prefabPath = AssetCommandSupport.RequireExistingAssetPath(prefabPath, commandName);
            Type mainType = AssetDatabase.GetMainAssetTypeAtPath(prefabPath);
            if (mainType != typeof(GameObject))
            {
                throw new CommandFailureException("PREFAB_PATH_INVALID", "prefab asset이 아닙니다: " + prefabPath);
            }

            return prefabPath;
        }
    }
}
