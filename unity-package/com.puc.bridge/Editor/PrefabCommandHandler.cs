using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityCli.Protocol;
using UnityEditor;
using UnityEngine;

namespace PUC.Editor
{
    internal sealed class PrefabCommandHandler
    {
        private static readonly JsonSerializer Serializer = JsonSerializer.CreateDefault(new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
        });

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
                JObject payload = new JObject
                {
                    ["asset"] = BuildAssetToken(path),
                    ["root"] = BuildNodeToken(root, args.withValues),
                };
                return payload.ToString(Formatting.None);
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
            bool overwritten = AssetCommandSupport.DeleteIfTargetExists(path, args.force, "prefab-create");

            PrefabCreateSpec spec = DeserializeSpec<PrefabCreateSpec>(args.specJson, "prefab-create");
            ValidateVersion(spec.version, "prefab-create");
            if (spec.root == null)
            {
                throw new CommandFailureException("PREFAB_SPEC_INVALID", "`root`가 필요합니다.");
            }

            string rootName = string.IsNullOrWhiteSpace(spec.root.name)
                ? Path.GetFileNameWithoutExtension(path)
                : spec.root.name.Trim();
            var root = new GameObject(rootName);

            try
            {
                ApplyNodeState(root, spec.root, rootName, allowMissingName: true);
                AddComponents(root, spec.root.components, "prefab-create");
                AddChildren(root.transform, spec.root.children, "prefab-create");

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
                overwritten = overwritten,
            });
        }

        private static string HandlePatch(string argumentsJson)
        {
            PrefabPatchArgs args = ProtocolJson.Deserialize<PrefabPatchArgs>(argumentsJson) ?? new PrefabPatchArgs();
            string path = RequireExistingPrefabPath(args.path, "prefab-patch");

            PrefabPatchSpec spec = DeserializeSpec<PrefabPatchSpec>(args.specJson, "prefab-patch");
            ValidateVersion(spec.version, "prefab-patch");
            if (spec.operations == null || spec.operations.Length == 0)
            {
                throw new CommandFailureException("PREFAB_SPEC_INVALID", "`operations`가 비어 있습니다.");
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ApplyPatchOperations(root, spec.operations);

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
                T spec = JsonConvert.DeserializeObject<T>(specJson, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                });

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

        private static void AddChildren(Transform parent, PrefabNodeSpec[] children, string commandName)
        {
            if (children == null)
            {
                return;
            }

            foreach (PrefabNodeSpec childSpec in children)
            {
                string childName = RequireNodeName(childSpec == null ? null : childSpec.name, commandName);
                var child = new GameObject(childName);
                child.transform.SetParent(parent, false);
                ApplyNodeState(child, childSpec, childName, allowMissingName: false);
                AddComponents(child, childSpec.components, commandName);
                AddChildren(child.transform, childSpec.children, commandName);
            }
        }

        private static void AddComponents(GameObject target, PrefabComponentSpec[] components, string commandName)
        {
            if (components == null)
            {
                return;
            }

            foreach (PrefabComponentSpec componentSpec in components)
            {
                AddComponent(target, componentSpec, commandName);
            }
        }

        private static Component AddComponent(GameObject target, PrefabComponentSpec componentSpec, string commandName)
        {
            if (componentSpec == null || string.IsNullOrWhiteSpace(componentSpec.type))
            {
                throw new CommandFailureException("PREFAB_COMPONENT_INVALID", commandName + " component type이 비어 있습니다.");
            }

            Type componentType = ResolveComponentType(componentSpec.type, commandName);
            if (componentType == typeof(Transform))
            {
                throw new CommandFailureException("PREFAB_COMPONENT_INVALID", "Transform은 직접 추가할 수 없습니다.");
            }

            Component component;
            try
            {
                component = target.AddComponent(componentType);
            }
            catch (Exception exception)
            {
                throw new CommandFailureException("PREFAB_COMPONENT_INVALID", "component를 추가하지 못했습니다: " + componentSpec.type, exception.Message);
            }

            if (componentSpec.values != null)
            {
                SerializedValueApplier.Apply(component, componentSpec.values);
            }

            return component;
        }

        private static void ApplyPatchOperations(GameObject root, PrefabPatchOperationSpec[] operations)
        {
            foreach (PrefabPatchOperationSpec operation in operations)
            {
                if (operation == null || string.IsNullOrWhiteSpace(operation.op))
                {
                    throw new CommandFailureException("PREFAB_SPEC_INVALID", "patch operation `op`가 비어 있습니다.");
                }

                switch (operation.op.Trim().ToLowerInvariant())
                {
                    case "add-child":
                    {
                        GameObject parent = ResolveNode(root, operation.parent, "add-child");
                        if (operation.node == null)
                        {
                            throw new CommandFailureException("PREFAB_SPEC_INVALID", "`add-child`에는 `node`가 필요합니다.");
                        }

                        AddChildren(parent.transform, new[] { operation.node }, "add-child");
                        break;
                    }
                    case "remove-node":
                    {
                        GameObject target = ResolveNode(root, operation.target, "remove-node");
                        if (target == root)
                        {
                            throw new CommandFailureException("PREFAB_NODE_INVALID", "루트 오브젝트는 삭제할 수 없습니다.");
                        }

                        UnityEngine.Object.DestroyImmediate(target);
                        break;
                    }
                    case "set-node":
                    {
                        GameObject target = ResolveNode(root, operation.target, "set-node");
                        PrefabNodeMutationSpec values = operation.values == null
                            ? null
                            : operation.values.ToObject<PrefabNodeMutationSpec>(Serializer);
                        if (values == null)
                        {
                            throw new CommandFailureException("PREFAB_SPEC_INVALID", "`set-node`에는 `values`가 필요합니다.");
                        }

                        ApplyNodeState(target, values);
                        break;
                    }
                    case "add-component":
                    {
                        GameObject target = ResolveNode(root, operation.target, "add-component");
                        if (operation.component == null)
                        {
                            throw new CommandFailureException("PREFAB_SPEC_INVALID", "`add-component`에는 `component`가 필요합니다.");
                        }

                        AddComponent(target, operation.component, "add-component");
                        break;
                    }
                    case "remove-component":
                    {
                        GameObject target = ResolveNode(root, operation.target, "remove-component");
                        Component component = ResolveComponent(target, operation.componentType, operation.componentIndex, "remove-component");
                        if (component is Transform)
                        {
                            throw new CommandFailureException("PREFAB_COMPONENT_INVALID", "Transform은 제거할 수 없습니다.");
                        }

                        UnityEngine.Object.DestroyImmediate(component, true);
                        break;
                    }
                    case "set-component-values":
                    {
                        GameObject target = ResolveNode(root, operation.target, "set-component-values");
                        Component component = ResolveComponent(target, operation.componentType, operation.componentIndex, "set-component-values");
                        if (operation.values == null || operation.values.Type != JTokenType.Object)
                        {
                            throw new CommandFailureException("PREFAB_SPEC_INVALID", "`set-component-values`에는 object 형태의 `values`가 필요합니다.");
                        }

                        SerializedValueApplier.Apply(component, (JObject)operation.values);
                        break;
                    }
                    default:
                        throw new CommandFailureException("PREFAB_SPEC_INVALID", "지원하지 않는 patch operation입니다: " + operation.op);
                }
            }
        }

        private static Component ResolveComponent(GameObject target, string componentTypeName, int? componentIndex, string commandName)
        {
            Type componentType = ResolveComponentType(componentTypeName, commandName);
            Component[] matches = target.GetComponents<Component>()
                .Where(component => component != null && componentType.IsAssignableFrom(component.GetType()))
                .ToArray();
            if (matches.Length == 0)
            {
                throw new CommandFailureException("PREFAB_COMPONENT_NOT_FOUND", commandName + " 대상 component를 찾지 못했습니다: " + componentTypeName);
            }

            int index = componentIndex ?? 0;
            if (index < 0 || index >= matches.Length)
            {
                throw new CommandFailureException("PREFAB_COMPONENT_NOT_FOUND", commandName + " component index가 범위를 벗어났습니다: " + index);
            }

            return matches[index];
        }

        private static Type ResolveComponentType(string typeName, string commandName)
        {
            string normalized = string.IsNullOrWhiteSpace(typeName) ? string.Empty : typeName.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new CommandFailureException("PREFAB_COMPONENT_INVALID", commandName + " component type이 비어 있습니다.");
            }

            List<Type> exactMatches = FindTypes(type => string.Equals(type.FullName, normalized, StringComparison.Ordinal));
            if (exactMatches.Count == 1)
            {
                return exactMatches[0];
            }

            if (exactMatches.Count > 1)
            {
                throw new CommandFailureException("PREFAB_COMPONENT_INVALID", "동일한 full name의 component 타입이 여러 개 있습니다: " + normalized);
            }

            List<Type> shortMatches = FindTypes(type => string.Equals(type.Name, normalized, StringComparison.Ordinal));
            if (shortMatches.Count == 1)
            {
                return shortMatches[0];
            }

            if (shortMatches.Count > 1)
            {
                throw new CommandFailureException("PREFAB_COMPONENT_INVALID", "짧은 이름이 같은 component 타입이 여러 개 있습니다. full name을 사용하세요: " + normalized);
            }

            throw new CommandFailureException("PREFAB_COMPONENT_INVALID", "component 타입을 찾지 못했습니다: " + normalized);
        }

        private static List<Type> FindTypes(Func<Type, bool> predicate)
        {
            var results = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(item => item != null).ToArray();
                }

                foreach (Type type in types)
                {
                    if (type != null
                        && typeof(Component).IsAssignableFrom(type)
                        && !type.IsAbstract
                        && !type.ContainsGenericParameters
                        && predicate(type))
                    {
                        results.Add(type);
                    }
                }
            }

            return results;
        }

        private static GameObject ResolveNode(GameObject root, string path, string commandName)
        {
            string normalizedPath = string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();
            if (normalizedPath == "/")
            {
                return root;
            }

            if (!normalizedPath.StartsWith("/", StringComparison.Ordinal))
            {
                throw new CommandFailureException("PREFAB_NODE_INVALID", commandName + " path는 `/`로 시작해야 합니다: " + normalizedPath);
            }

            Transform current = root.transform;
            string[] segments = normalizedPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string segment in segments)
            {
                (string name, int index) = ParsePathSegment(segment, commandName);
                List<Transform> matches = new List<Transform>();
                for (int childIndex = 0; childIndex < current.childCount; childIndex++)
                {
                    Transform child = current.GetChild(childIndex);
                    if (string.Equals(child.name, name, StringComparison.Ordinal))
                    {
                        matches.Add(child);
                    }
                }

                if (index < 0 || index >= matches.Count)
                {
                    throw new CommandFailureException("PREFAB_NODE_NOT_FOUND", commandName + " path를 찾지 못했습니다: " + normalizedPath);
                }

                current = matches[index];
            }

            return current.gameObject;
        }

        private static (string name, int index) ParsePathSegment(string segment, string commandName)
        {
            int bracketIndex = segment.LastIndexOf('[');
            if (bracketIndex > 0 && segment.EndsWith("]", StringComparison.Ordinal))
            {
                string name = segment.Substring(0, bracketIndex);
                string indexText = segment.Substring(bracketIndex + 1, segment.Length - bracketIndex - 2);
                if (int.TryParse(indexText, out int index))
                {
                    return (name, index);
                }
            }

            if (string.IsNullOrWhiteSpace(segment))
            {
                throw new CommandFailureException("PREFAB_NODE_INVALID", commandName + " path segment가 비어 있습니다.");
            }

            return (segment, 0);
        }

        private static void ApplyNodeState(GameObject target, PrefabNodeSpec spec, string defaultName, bool allowMissingName)
        {
            if (spec == null)
            {
                throw new CommandFailureException("PREFAB_SPEC_INVALID", "node spec이 비어 있습니다.");
            }

            string name = string.IsNullOrWhiteSpace(spec.name)
                ? (allowMissingName ? defaultName : RequireNodeName(spec.name, "prefab"))
                : spec.name.Trim();
            target.name = name;

            ApplyNodeStateCore(target, spec.active, spec.tag, spec.layer, spec.transform);
        }

        private static void ApplyNodeState(GameObject target, PrefabNodeMutationSpec spec)
        {
            if (spec == null)
            {
                throw new CommandFailureException("PREFAB_SPEC_INVALID", "node values가 비어 있습니다.");
            }

            if (spec.name != null)
            {
                target.name = RequireNodeName(spec.name, "set-node");
            }

            ApplyNodeStateCore(target, spec.active, spec.tag, spec.layer, spec.transform);
        }

        private static void ApplyNodeStateCore(GameObject target, bool? active, string tag, JToken layer, PrefabTransformSpec transformSpec)
        {
            if (active.HasValue)
            {
                target.SetActive(active.Value);
            }

            if (tag != null)
            {
                string normalizedTag = tag.Trim();
                if (string.IsNullOrWhiteSpace(normalizedTag))
                {
                    throw new CommandFailureException("PREFAB_NODE_INVALID", "tag가 비어 있습니다.");
                }

                target.tag = normalizedTag;
            }

            if (layer != null)
            {
                target.layer = ResolveLayer(layer);
            }

            if (transformSpec != null)
            {
                ApplyTransformSpec(target.transform, transformSpec);
            }
        }

        private static string RequireNodeName(string name, string commandName)
        {
            string normalized = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new CommandFailureException("PREFAB_NODE_INVALID", commandName + " node 이름이 비어 있습니다.");
            }

            return normalized;
        }

        private static int ResolveLayer(JToken token)
        {
            if (token.Type == JTokenType.Integer)
            {
                int layerIndex = token.Value<int>();
                if (layerIndex < 0 || layerIndex > 31)
                {
                    throw new CommandFailureException("PREFAB_NODE_INVALID", "layer index가 범위를 벗어났습니다: " + layerIndex);
                }

                return layerIndex;
            }

            if (token.Type != JTokenType.String)
            {
                throw new CommandFailureException("PREFAB_NODE_INVALID", "layer는 string 또는 int여야 합니다.");
            }

            string layerName = token.Value<string>();
            if (int.TryParse(layerName, out int numericLayer))
            {
                return ResolveLayer(new JValue(numericLayer));
            }

            int resolvedLayer = LayerMask.NameToLayer(layerName);
            if (resolvedLayer < 0)
            {
                throw new CommandFailureException("PREFAB_NODE_INVALID", "layer를 찾지 못했습니다: " + layerName);
            }

            return resolvedLayer;
        }

        private static void ApplyTransformSpec(Transform transform, PrefabTransformSpec spec)
        {
            if (spec.localPosition != null)
            {
                Vector3 current = transform.localPosition;
                transform.localPosition = new Vector3(
                    spec.localPosition.x ?? current.x,
                    spec.localPosition.y ?? current.y,
                    spec.localPosition.z ?? current.z);
            }

            if (spec.localRotationEuler != null)
            {
                Vector3 current = transform.localEulerAngles;
                transform.localEulerAngles = new Vector3(
                    spec.localRotationEuler.x ?? current.x,
                    spec.localRotationEuler.y ?? current.y,
                    spec.localRotationEuler.z ?? current.z);
            }

            if (spec.localScale != null)
            {
                Vector3 current = transform.localScale;
                transform.localScale = new Vector3(
                    spec.localScale.x ?? current.x,
                    spec.localScale.y ?? current.y,
                    spec.localScale.z ?? current.z);
            }
        }

        private static JObject BuildAssetToken(string path)
        {
            AssetRecord record = AssetCommandSupport.BuildRecordFromPath(path);
            return new JObject
            {
                ["path"] = record.path,
                ["guid"] = record.guid,
                ["assetName"] = record.assetName,
                ["mainType"] = record.mainType,
                ["isFolder"] = record.isFolder,
                ["exists"] = record.exists,
            };
        }

        private static JObject BuildNodeToken(GameObject gameObject, bool withValues)
        {
            var node = new JObject
            {
                ["path"] = BuildNodePath(gameObject.transform),
                ["name"] = gameObject.name,
                ["active"] = gameObject.activeSelf,
                ["tag"] = gameObject.tag,
                ["layer"] = BuildLayerToken(gameObject.layer),
                ["transform"] = new JObject
                {
                    ["localPosition"] = BuildVector3Token(gameObject.transform.localPosition),
                    ["localRotationEuler"] = BuildVector3Token(gameObject.transform.localEulerAngles),
                    ["localScale"] = BuildVector3Token(gameObject.transform.localScale),
                },
            };

            var components = new JArray();
            var componentIndices = new Dictionary<Type, int>();
            foreach (Component component in gameObject.GetComponents<Component>())
            {
                if (component is Transform)
                {
                    continue;
                }

                int index = componentIndices.TryGetValue(component.GetType(), out int currentIndex)
                    ? currentIndex
                    : 0;
                componentIndices[component.GetType()] = index + 1;

                var componentToken = new JObject
                {
                    ["type"] = component.GetType().FullName,
                    ["componentIndex"] = index,
                };
                if (withValues)
                {
                    componentToken["values"] = SerializedValueApplier.BuildInspectableValues(component);
                }

                components.Add(componentToken);
            }

            node["components"] = components;

            var children = new JArray();
            for (int index = 0; index < gameObject.transform.childCount; index++)
            {
                children.Add(BuildNodeToken(gameObject.transform.GetChild(index).gameObject, withValues));
            }

            node["children"] = children;
            return node;
        }

        private static string BuildNodePath(Transform transform)
        {
            if (transform.parent == null)
            {
                return "/";
            }

            List<string> segments = new List<string>();
            Transform current = transform;
            while (current.parent != null)
            {
                int siblingIndex = 0;
                for (int index = 0; index < current.parent.childCount; index++)
                {
                    Transform sibling = current.parent.GetChild(index);
                    if (sibling == current)
                    {
                        break;
                    }

                    if (string.Equals(sibling.name, current.name, StringComparison.Ordinal))
                    {
                        siblingIndex++;
                    }
                }

                segments.Add(current.name + "[" + siblingIndex + "]");
                current = current.parent;
            }

            segments.Reverse();
            return "/" + string.Join("/", segments);
        }

        private static JToken BuildLayerToken(int layerIndex)
        {
            string layerName = LayerMask.LayerToName(layerIndex);
            return string.IsNullOrWhiteSpace(layerName)
                ? (JToken)new JValue(layerIndex)
                : new JValue(layerName);
        }

        private static JObject BuildVector3Token(Vector3 vector)
        {
            return new JObject
            {
                ["x"] = vector.x,
                ["y"] = vector.y,
                ["z"] = vector.z,
            };
        }

        [Serializable]
        private sealed class PrefabCreateSpec
        {
            public int version;
            public PrefabNodeSpec root;
        }

        [Serializable]
        private sealed class PrefabPatchSpec
        {
            public int version;
            public PrefabPatchOperationSpec[] operations = Array.Empty<PrefabPatchOperationSpec>();
        }

        [Serializable]
        private sealed class PrefabNodeSpec
        {
            public string name;
            public bool? active;
            public string tag;
            public JToken layer;
            public PrefabTransformSpec transform;
            public PrefabComponentSpec[] components = Array.Empty<PrefabComponentSpec>();
            public PrefabNodeSpec[] children = Array.Empty<PrefabNodeSpec>();
        }

        [Serializable]
        private sealed class PrefabNodeMutationSpec
        {
            public string name;
            public bool? active;
            public string tag;
            public JToken layer;
            public PrefabTransformSpec transform;
        }

        [Serializable]
        private sealed class PrefabTransformSpec
        {
            public PrefabVector3Spec localPosition;
            public PrefabVector3Spec localRotationEuler;
            public PrefabVector3Spec localScale;
        }

        [Serializable]
        private sealed class PrefabVector3Spec
        {
            public float? x;
            public float? y;
            public float? z;
        }

        [Serializable]
        private sealed class PrefabComponentSpec
        {
            public string type;
            public JObject values;
        }

        [Serializable]
        private sealed class PrefabPatchOperationSpec
        {
            public string op;
            public string parent;
            public string target;
            public PrefabNodeSpec node;
            public JToken values;
            public PrefabComponentSpec component;
            public string componentType;
            public int? componentIndex;
        }
    }
}
