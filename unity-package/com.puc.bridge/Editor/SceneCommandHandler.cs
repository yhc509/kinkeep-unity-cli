using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityCli.Protocol;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PUC.Editor
{
    internal sealed class SceneCommandHandler
    {
        private static readonly JsonSerializer Serializer = JsonSerializer.CreateDefault(new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
        });

        public bool CanHandle(string command)
        {
            return ProtocolHelpers.IsSceneCommand(command);
        }

        public string Handle(string command, string argumentsJson)
        {
            switch (command)
            {
                case ProtocolConstants.CommandSceneOpen:
                    return HandleOpen(argumentsJson);
                case ProtocolConstants.CommandSceneInspect:
                    return HandleInspect(argumentsJson);
                case ProtocolConstants.CommandScenePatch:
                    return HandlePatch(argumentsJson);
                default:
                    throw new InvalidOperationException("지원하지 않는 scene 명령입니다: " + command);
            }
        }

        private static string HandleOpen(string argumentsJson)
        {
            SceneOpenArgs args = ProtocolJson.Deserialize<SceneOpenArgs>(argumentsJson) ?? new SceneOpenArgs();
            string path = RequireExistingScenePath(args.path, "scene-open");

            PrepareForOpen(args.force);

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new CommandFailureException("SCENE_OPEN_FAILED", "scene를 열지 못했습니다: " + path);
            }

            SceneManager.SetActiveScene(scene);
            return ProtocolJson.Serialize(new SceneOpenPayload
            {
                asset = AssetCommandSupport.BuildRecordFromPath(path),
                activeScenePath = scene.path,
                opened = true,
            });
        }

        private static string HandleInspect(string argumentsJson)
        {
            SceneInspectArgs args = ProtocolJson.Deserialize<SceneInspectArgs>(argumentsJson) ?? new SceneInspectArgs();
            string path = RequireExistingScenePath(args.path, "scene-inspect");

            return WithLoadedScene(path, "scene-inspect", delegate(Scene scene)
            {
                var roots = new JArray();
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    roots.Add(BuildNodeToken(root, args.withValues));
                }

                var payload = new JObject
                {
                    ["asset"] = BuildAssetToken(path),
                    ["activeScenePath"] = EditorSceneManager.GetActiveScene().path,
                    ["scene"] = new JObject
                    {
                        ["path"] = scene.path,
                        ["name"] = scene.name,
                        ["isLoaded"] = scene.isLoaded,
                        ["isDirty"] = scene.isDirty,
                        ["roots"] = roots,
                    },
                };

                return payload.ToString(Formatting.None);
            });
        }

        private static string HandlePatch(string argumentsJson)
        {
            ScenePatchArgs args = ProtocolJson.Deserialize<ScenePatchArgs>(argumentsJson) ?? new ScenePatchArgs();
            string path = RequireExistingScenePath(args.path, "scene-patch");

            ScenePatchSpec spec = DeserializeSpec<ScenePatchSpec>(args.specJson, "scene-patch");
            ValidateVersion(spec.version, "scene-patch");
            if (spec.operations == null || spec.operations.Length == 0)
            {
                throw new CommandFailureException("SCENE_SPEC_INVALID", "`operations`가 비어 있습니다.");
            }

            if (HasDestructiveOperation(spec) && !args.force)
            {
                throw new CommandFailureException("SCENE_FORCE_REQUIRED", "`delete-gameobject` 또는 `remove-component`를 쓰려면 --force가 필요합니다.");
            }

            return WithLoadedScene(path, "scene-patch", delegate(Scene scene)
            {
                ApplyPatchOperations(scene, spec.operations);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new CommandFailureException("SCENE_SAVE_FAILED", "scene를 저장하지 못했습니다: " + path);
                }

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                return ProtocolJson.Serialize(new SceneMutationPayload
                {
                    asset = AssetCommandSupport.BuildRecordFromPath(path),
                    activeScenePath = EditorSceneManager.GetActiveScene().path,
                    patched = true,
                });
            });
        }

        private static T WithLoadedScene<T>(string path, string commandName, Func<Scene, T> action)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedHere = false;

            if (scene.IsValid() && scene.isLoaded)
            {
                if (scene.isDirty)
                {
                    throw new CommandFailureException(
                        "SCENE_DIRTY",
                        commandName + " 대상 scene에 저장되지 않은 변경이 있습니다. 먼저 저장하거나 변경을 버리세요: " + path);
                }
            }
            else
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                openedHere = true;
            }

            try
            {
                return action(scene);
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void PrepareForOpen(bool force)
        {
            bool hasDirtyScene = false;
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.isLoaded && scene.isDirty)
                {
                    hasDirtyScene = true;
                    if (!force)
                    {
                        string sceneName = string.IsNullOrWhiteSpace(scene.path) ? scene.name : scene.path;
                        throw new CommandFailureException(
                            "SCENE_DIRTY",
                            "저장되지 않은 scene 변경이 있습니다. 버리고 열려면 --force를 사용하세요: " + sceneName);
                    }
                }
            }

            if (force && hasDirtyScene)
            {
                // Reset to a fresh scene first so `scene open --force` discards dirty state deterministically.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static T DeserializeSpec<T>(string specJson, string commandName) where T : class
        {
            if (string.IsNullOrWhiteSpace(specJson))
            {
                throw new CommandFailureException("SCENE_SPEC_INVALID", commandName + " spec이 비어 있습니다.");
            }

            try
            {
                T spec = JsonConvert.DeserializeObject<T>(specJson, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                });

                if (spec == null)
                {
                    throw new CommandFailureException("SCENE_SPEC_INVALID", commandName + " spec을 해석하지 못했습니다.");
                }

                return spec;
            }
            catch (JsonException exception)
            {
                throw new CommandFailureException("SCENE_SPEC_INVALID", commandName + " spec JSON이 잘못되었습니다.", exception.Message);
            }
        }

        private static void ValidateVersion(int version, string commandName)
        {
            if (version > 1)
            {
                throw new CommandFailureException("SCENE_SPEC_INVALID", commandName + " spec version을 지원하지 않습니다: " + version);
            }
        }

        private static bool HasDestructiveOperation(ScenePatchSpec spec)
        {
            return spec.operations.Any(operation =>
                operation != null
                && !string.IsNullOrWhiteSpace(operation.op)
                && (string.Equals(operation.op, "delete-gameobject", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(operation.op, "remove-component", StringComparison.OrdinalIgnoreCase)));
        }

        private static string ResolveScenePath(string path)
        {
            string normalizedPath;
            try
            {
                normalizedPath = AssetCommandSupport.NormalizeAssetPath(path);
            }
            catch (Exception exception)
            {
                throw new CommandFailureException("SCENE_PATH_INVALID", exception.Message, exception.ToString());
            }

            string extension = Path.GetExtension(normalizedPath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                return normalizedPath + ".unity";
            }

            if (!string.Equals(extension, ".unity", StringComparison.OrdinalIgnoreCase))
            {
                throw new CommandFailureException("SCENE_PATH_INVALID", "scene 경로는 `.unity` 확장자를 사용해야 합니다: " + normalizedPath);
            }

            return normalizedPath;
        }

        private static string RequireExistingScenePath(string path, string commandName)
        {
            string scenePath = ResolveScenePath(path);
            if (!AssetCommandSupport.AssetExists(scenePath))
            {
                throw new CommandFailureException("SCENE_PATH_INVALID", commandName + " 대상 scene이 없습니다: " + scenePath);
            }

            Type mainType = AssetDatabase.GetMainAssetTypeAtPath(scenePath);
            if (mainType != typeof(SceneAsset))
            {
                throw new CommandFailureException("SCENE_PATH_INVALID", "scene asset이 아닙니다: " + scenePath);
            }

            return scenePath;
        }

        private static void ApplyPatchOperations(Scene scene, ScenePatchOperationSpec[] operations)
        {
            foreach (ScenePatchOperationSpec operation in operations)
            {
                if (operation == null || string.IsNullOrWhiteSpace(operation.op))
                {
                    throw new CommandFailureException("SCENE_SPEC_INVALID", "patch operation `op`가 비어 있습니다.");
                }

                switch (operation.op.Trim().ToLowerInvariant())
                {
                    case "add-gameobject":
                    {
                        Transform parent = ResolveParent(scene, operation.parent, "add-gameobject");
                        if (operation.node == null)
                        {
                            throw new CommandFailureException("SCENE_SPEC_INVALID", "`add-gameobject`에는 `node`가 필요합니다.");
                        }

                        AddNodes(scene, parent, new[] { operation.node }, "add-gameobject");
                        break;
                    }
                    case "modify-gameobject":
                    {
                        GameObject target = ResolveNode(scene, operation.target, "modify-gameobject");
                        SceneNodeMutationSpec values = operation.values == null
                            ? null
                            : operation.values.ToObject<SceneNodeMutationSpec>(Serializer);
                        if (values == null)
                        {
                            throw new CommandFailureException("SCENE_SPEC_INVALID", "`modify-gameobject`에는 `values`가 필요합니다.");
                        }

                        ApplyNodeState(target, values);
                        break;
                    }
                    case "delete-gameobject":
                    {
                        GameObject target = ResolveNode(scene, operation.target, "delete-gameobject");
                        UnityEngine.Object.DestroyImmediate(target);
                        break;
                    }
                    case "add-component":
                    {
                        GameObject target = ResolveNode(scene, operation.target, "add-component");
                        if (operation.component == null)
                        {
                            throw new CommandFailureException("SCENE_SPEC_INVALID", "`add-component`에는 `component`가 필요합니다.");
                        }

                        AddComponent(target, operation.component, "add-component");
                        break;
                    }
                    case "modify-component":
                    {
                        GameObject target = ResolveNode(scene, operation.target, "modify-component");
                        Component component = ResolveComponent(target, operation.componentType, operation.componentIndex, "modify-component");
                        if (operation.values == null || operation.values.Type != JTokenType.Object)
                        {
                            throw new CommandFailureException("SCENE_SPEC_INVALID", "`modify-component`에는 object 형태의 `values`가 필요합니다.");
                        }

                        SerializedValueApplier.Apply(component, (JObject)operation.values);
                        break;
                    }
                    case "remove-component":
                    {
                        GameObject target = ResolveNode(scene, operation.target, "remove-component");
                        Component component = ResolveComponent(target, operation.componentType, operation.componentIndex, "remove-component");
                        if (component is Transform)
                        {
                            throw new CommandFailureException("SCENE_COMPONENT_INVALID", "Transform은 제거할 수 없습니다.");
                        }

                        UnityEngine.Object.DestroyImmediate(component, true);
                        break;
                    }
                    default:
                        throw new CommandFailureException("SCENE_SPEC_INVALID", "지원하지 않는 patch operation입니다: " + operation.op);
                }
            }
        }

        private static void AddNodes(Scene scene, Transform parent, SceneNodeSpec[] children, string commandName)
        {
            if (children == null)
            {
                return;
            }

            foreach (SceneNodeSpec childSpec in children)
            {
                string childName = RequireNodeName(childSpec == null ? null : childSpec.name, commandName);
                var child = new GameObject(childName);
                SceneManager.MoveGameObjectToScene(child, scene);
                if (parent != null)
                {
                    child.transform.SetParent(parent, false);
                }

                ApplyNodeState(child, childSpec, childName, allowMissingName: false);
                AddComponents(child, childSpec.components, commandName);
                AddNodes(scene, child.transform, childSpec.children, commandName);
            }
        }

        private static void AddComponents(GameObject target, SceneComponentSpec[] components, string commandName)
        {
            if (components == null)
            {
                return;
            }

            foreach (SceneComponentSpec componentSpec in components)
            {
                AddComponent(target, componentSpec, commandName);
            }
        }

        private static Component AddComponent(GameObject target, SceneComponentSpec componentSpec, string commandName)
        {
            if (componentSpec == null || string.IsNullOrWhiteSpace(componentSpec.type))
            {
                throw new CommandFailureException("SCENE_COMPONENT_INVALID", commandName + " component type이 비어 있습니다.");
            }

            Type componentType = ResolveComponentType(componentSpec.type, commandName);
            if (componentType == typeof(Transform))
            {
                throw new CommandFailureException("SCENE_COMPONENT_INVALID", "Transform은 직접 추가할 수 없습니다.");
            }

            Component component;
            try
            {
                component = target.AddComponent(componentType);
            }
            catch (Exception exception)
            {
                throw new CommandFailureException("SCENE_COMPONENT_INVALID", "component를 추가하지 못했습니다: " + componentSpec.type, exception.Message);
            }

            if (componentSpec.values != null)
            {
                SerializedValueApplier.Apply(component, componentSpec.values);
            }

            return component;
        }

        private static Component ResolveComponent(GameObject target, string componentTypeName, int? componentIndex, string commandName)
        {
            Type componentType = ResolveComponentType(componentTypeName, commandName);
            Component[] matches = target.GetComponents<Component>()
                .Where(component => component != null && componentType.IsAssignableFrom(component.GetType()))
                .ToArray();
            if (matches.Length == 0)
            {
                throw new CommandFailureException("SCENE_COMPONENT_NOT_FOUND", commandName + " 대상 component를 찾지 못했습니다: " + componentTypeName);
            }

            int index = componentIndex ?? 0;
            if (index < 0 || index >= matches.Length)
            {
                throw new CommandFailureException("SCENE_COMPONENT_NOT_FOUND", commandName + " component index가 범위를 벗어났습니다: " + index);
            }

            return matches[index];
        }

        private static Type ResolveComponentType(string typeName, string commandName)
        {
            string normalized = string.IsNullOrWhiteSpace(typeName) ? string.Empty : typeName.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new CommandFailureException("SCENE_COMPONENT_INVALID", commandName + " component type이 비어 있습니다.");
            }

            List<Type> exactMatches = FindTypes(type => string.Equals(type.FullName, normalized, StringComparison.Ordinal));
            if (exactMatches.Count == 1)
            {
                return exactMatches[0];
            }

            if (exactMatches.Count > 1)
            {
                throw new CommandFailureException("SCENE_COMPONENT_INVALID", "동일한 full name의 component 타입이 여러 개 있습니다: " + normalized);
            }

            List<Type> shortMatches = FindTypes(type => string.Equals(type.Name, normalized, StringComparison.Ordinal));
            if (shortMatches.Count == 1)
            {
                return shortMatches[0];
            }

            if (shortMatches.Count > 1)
            {
                throw new CommandFailureException("SCENE_COMPONENT_INVALID", "짧은 이름이 같은 component 타입이 여러 개 있습니다. full name을 사용하세요: " + normalized);
            }

            throw new CommandFailureException("SCENE_COMPONENT_INVALID", "component 타입을 찾지 못했습니다: " + normalized);
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

        private static Transform ResolveParent(Scene scene, string path, string commandName)
        {
            string normalizedPath = string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();
            if (normalizedPath == "/")
            {
                return null;
            }

            return ResolveNode(scene, normalizedPath, commandName).transform;
        }

        private static GameObject ResolveNode(Scene scene, string path, string commandName)
        {
            string normalizedPath = string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();
            if (normalizedPath == "/")
            {
                throw new CommandFailureException("SCENE_NODE_INVALID", commandName + " path는 scene root `/`를 직접 가리킬 수 없습니다.");
            }

            if (!normalizedPath.StartsWith("/", StringComparison.Ordinal))
            {
                throw new CommandFailureException("SCENE_NODE_INVALID", commandName + " path는 `/`로 시작해야 합니다: " + normalizedPath);
            }

            string[] segments = normalizedPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                throw new CommandFailureException("SCENE_NODE_INVALID", commandName + " path가 비어 있습니다.");
            }

            GameObject current = ResolveRoot(scene, segments[0], normalizedPath, commandName);
            for (int index = 1; index < segments.Length; index++)
            {
                (string name, int siblingIndex) = ParsePathSegment(segments[index], commandName);
                List<Transform> matches = new List<Transform>();
                for (int childIndex = 0; childIndex < current.transform.childCount; childIndex++)
                {
                    Transform child = current.transform.GetChild(childIndex);
                    if (string.Equals(child.name, name, StringComparison.Ordinal))
                    {
                        matches.Add(child);
                    }
                }

                if (siblingIndex < 0 || siblingIndex >= matches.Count)
                {
                    throw new CommandFailureException("SCENE_NODE_NOT_FOUND", commandName + " path를 찾지 못했습니다: " + normalizedPath);
                }

                current = matches[siblingIndex].gameObject;
            }

            return current;
        }

        private static GameObject ResolveRoot(Scene scene, string segment, string normalizedPath, string commandName)
        {
            (string name, int rootIndex) = ParsePathSegment(segment, commandName);
            GameObject[] matches = scene.GetRootGameObjects()
                .Where(gameObject => string.Equals(gameObject.name, name, StringComparison.Ordinal))
                .ToArray();
            if (rootIndex < 0 || rootIndex >= matches.Length)
            {
                throw new CommandFailureException("SCENE_NODE_NOT_FOUND", commandName + " path를 찾지 못했습니다: " + normalizedPath);
            }

            return matches[rootIndex];
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
                throw new CommandFailureException("SCENE_NODE_INVALID", commandName + " path segment가 비어 있습니다.");
            }

            return (segment, 0);
        }

        private static void ApplyNodeState(GameObject target, SceneNodeSpec spec, string defaultName, bool allowMissingName)
        {
            if (spec == null)
            {
                throw new CommandFailureException("SCENE_SPEC_INVALID", "node spec이 비어 있습니다.");
            }

            string name = string.IsNullOrWhiteSpace(spec.name)
                ? (allowMissingName ? defaultName : RequireNodeName(spec.name, "scene"))
                : spec.name.Trim();
            target.name = name;

            ApplyNodeStateCore(target, spec.active, spec.tag, spec.layer, spec.transform);
        }

        private static void ApplyNodeState(GameObject target, SceneNodeMutationSpec spec)
        {
            if (spec == null)
            {
                throw new CommandFailureException("SCENE_SPEC_INVALID", "node values가 비어 있습니다.");
            }

            if (spec.name != null)
            {
                target.name = RequireNodeName(spec.name, "modify-gameobject");
            }

            ApplyNodeStateCore(target, spec.active, spec.tag, spec.layer, spec.transform);
        }

        private static void ApplyNodeStateCore(GameObject target, bool? active, string tag, JToken layer, SceneTransformSpec transformSpec)
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
                    throw new CommandFailureException("SCENE_NODE_INVALID", "tag가 비어 있습니다.");
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
                throw new CommandFailureException("SCENE_NODE_INVALID", commandName + " node 이름이 비어 있습니다.");
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
                    throw new CommandFailureException("SCENE_NODE_INVALID", "layer index가 범위를 벗어났습니다: " + layerIndex);
                }

                return layerIndex;
            }

            if (token.Type != JTokenType.String)
            {
                throw new CommandFailureException("SCENE_NODE_INVALID", "layer는 string 또는 int여야 합니다.");
            }

            string layerName = token.Value<string>();
            if (int.TryParse(layerName, out int numericLayer))
            {
                return ResolveLayer(new JValue(numericLayer));
            }

            int resolvedLayer = LayerMask.NameToLayer(layerName);
            if (resolvedLayer < 0)
            {
                throw new CommandFailureException("SCENE_NODE_INVALID", "layer를 찾지 못했습니다: " + layerName);
            }

            return resolvedLayer;
        }

        private static void ApplyTransformSpec(Transform transform, SceneTransformSpec spec)
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
                ["path"] = BuildNodePath(gameObject),
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

        private static string BuildNodePath(GameObject gameObject)
        {
            var segments = new List<string>();
            Transform current = gameObject.transform;
            while (current != null)
            {
                int siblingIndex = current.parent == null
                    ? GetRootIndex(current)
                    : GetSiblingIndex(current);
                segments.Add(current.name + "[" + siblingIndex + "]");
                current = current.parent;
            }

            segments.Reverse();
            return "/" + string.Join("/", segments);
        }

        private static int GetRootIndex(Transform transform)
        {
            int rootIndex = 0;
            foreach (GameObject root in transform.gameObject.scene.GetRootGameObjects())
            {
                if (root.transform == transform)
                {
                    break;
                }

                if (string.Equals(root.name, transform.name, StringComparison.Ordinal))
                {
                    rootIndex++;
                }
            }

            return rootIndex;
        }

        private static int GetSiblingIndex(Transform transform)
        {
            int siblingIndex = 0;
            for (int index = 0; index < transform.parent.childCount; index++)
            {
                Transform sibling = transform.parent.GetChild(index);
                if (sibling == transform)
                {
                    break;
                }

                if (string.Equals(sibling.name, transform.name, StringComparison.Ordinal))
                {
                    siblingIndex++;
                }
            }

            return siblingIndex;
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
        private sealed class ScenePatchSpec
        {
            public int version;
            public ScenePatchOperationSpec[] operations = Array.Empty<ScenePatchOperationSpec>();
        }

        [Serializable]
        private sealed class SceneNodeSpec
        {
            public string name;
            public bool? active;
            public string tag;
            public JToken layer;
            public SceneTransformSpec transform;
            public SceneComponentSpec[] components = Array.Empty<SceneComponentSpec>();
            public SceneNodeSpec[] children = Array.Empty<SceneNodeSpec>();
        }

        [Serializable]
        private sealed class SceneNodeMutationSpec
        {
            public string name;
            public bool? active;
            public string tag;
            public JToken layer;
            public SceneTransformSpec transform;
        }

        [Serializable]
        private sealed class SceneTransformSpec
        {
            public SceneVector3Spec localPosition;
            public SceneVector3Spec localRotationEuler;
            public SceneVector3Spec localScale;
        }

        [Serializable]
        private sealed class SceneVector3Spec
        {
            public float? x;
            public float? y;
            public float? z;
        }

        [Serializable]
        private sealed class SceneComponentSpec
        {
            public string type;
            public JObject values;
        }

        [Serializable]
        private sealed class ScenePatchOperationSpec
        {
            public string op;
            public string parent;
            public string target;
            public SceneNodeSpec node;
            public JToken values;
            public SceneComponentSpec component;
            public string componentType;
            public int? componentIndex;
        }
    }
}
