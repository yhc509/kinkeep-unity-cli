#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KinKeep.UnityCli.Bridge.Editor
{
    internal static class SceneInspector
    {
        internal static string BuildInspectPayload(string path, Scene scene, bool withValues, int? maxDepth, bool omitDefaults, string activeScenePath)
        {
            var roots = new JArray();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                roots.Add(BuildNodeToken(root, withValues, maxDepth, omitDefaults, 0));
            }

            var payload = new JObject
            {
                ["asset"] = InspectorUtility.BuildAssetToken(path),
                ["activeScenePath"] = activeScenePath,
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
        }

        internal static Transform? ResolveParent(Scene scene, string? path, string commandName)
        {
            string normalizedPath = string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();
            if (normalizedPath == "/")
            {
                return null;
            }

            return ResolveNode(scene, normalizedPath, commandName).transform;
        }

        internal static GameObject ResolveNode(Scene scene, string? path, string commandName)
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
                (string name, int siblingIndex) = InspectorUtility.ParsePathSegment(segments[index], commandName, "SCENE");
                var matches = new List<Transform>();
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

        internal static void ApplyNodeState(GameObject target, SceneNodeSpec? spec, string defaultName, bool allowMissingName)
        {
            if (spec == null)
            {
                throw new CommandFailureException("SCENE_SPEC_INVALID", "node spec이 비어 있습니다.");
            }

            string name = string.IsNullOrWhiteSpace(spec.Name)
                ? (allowMissingName ? defaultName : InspectorUtility.RequireNodeName(spec.Name, "scene", "SCENE"))
                : spec.Name.Trim();
            target.name = name;

            SceneTransformSpec? transformSpec = spec.Transform;
            Transform transform = target.transform;
            InspectorUtility.ApplyNodeStateCore(
                target,
                spec.IsActive,
                spec.Tag,
                spec.Layer,
                InspectorUtility.MergeVector3(transform.localPosition, transformSpec?.LocalPosition?.X, transformSpec?.LocalPosition?.Y, transformSpec?.LocalPosition?.Z),
                InspectorUtility.MergeVector3(transform.localEulerAngles, transformSpec?.LocalRotationEuler?.X, transformSpec?.LocalRotationEuler?.Y, transformSpec?.LocalRotationEuler?.Z),
                InspectorUtility.MergeVector3(transform.localScale, transformSpec?.LocalScale?.X, transformSpec?.LocalScale?.Y, transformSpec?.LocalScale?.Z),
                "SCENE");
        }

        internal static void ApplyNodeState(GameObject target, SceneNodeMutationSpec? spec)
        {
            if (spec == null)
            {
                throw new CommandFailureException("SCENE_SPEC_INVALID", "node values가 비어 있습니다.");
            }

            if (spec.Name != null)
            {
                target.name = InspectorUtility.RequireNodeName(spec.Name, "modify-gameobject", "SCENE");
            }

            SceneTransformSpec? transformSpec = spec.Transform;
            Transform transform = target.transform;
            InspectorUtility.ApplyNodeStateCore(
                target,
                spec.IsActive,
                spec.Tag,
                spec.Layer,
                InspectorUtility.MergeVector3(transform.localPosition, transformSpec?.LocalPosition?.X, transformSpec?.LocalPosition?.Y, transformSpec?.LocalPosition?.Z),
                InspectorUtility.MergeVector3(transform.localEulerAngles, transformSpec?.LocalRotationEuler?.X, transformSpec?.LocalRotationEuler?.Y, transformSpec?.LocalRotationEuler?.Z),
                InspectorUtility.MergeVector3(transform.localScale, transformSpec?.LocalScale?.X, transformSpec?.LocalScale?.Y, transformSpec?.LocalScale?.Z),
                "SCENE");
        }

        private static JObject BuildNodeToken(GameObject gameObject, bool withValues, int? maxDepth, bool omitDefaults, int currentDepth)
        {
            var node = new JObject
            {
                ["path"] = BuildNodePath(gameObject),
                ["name"] = gameObject.name,
                ["active"] = gameObject.activeSelf,
                ["tag"] = gameObject.tag,
                ["layer"] = InspectorUtility.BuildLayerToken(gameObject.layer),
                ["transform"] = InspectorUtility.BuildTransformToken(gameObject.transform, omitDefaults),
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
                    JObject values = SerializedValueApplier.BuildInspectableValues(component);
                    if (omitDefaults)
                    {
                        InspectorUtility.PruneDefaultInspectableValues(values);
                    }

                    if (!omitDefaults || values.HasValues)
                    {
                        componentToken["values"] = values;
                    }
                }

                components.Add(componentToken);
            }

            node["components"] = components;

            JArray children;
            if (maxDepth.HasValue && currentDepth >= maxDepth.Value)
            {
                children = InspectorUtility.BuildChildStubTokens(gameObject.transform, BuildNodePath);
            }
            else
            {
                children = new JArray();
                for (int index = 0; index < gameObject.transform.childCount; index++)
                {
                    children.Add(BuildNodeToken(gameObject.transform.GetChild(index).gameObject, withValues, maxDepth, omitDefaults, currentDepth + 1));
                }
            }
            node["children"] = children;

            if (omitDefaults)
            {
                InspectorUtility.ApplyNodeDefaultOmissions(node, gameObject);
            }

            return node;
        }

        private static string BuildNodePath(GameObject gameObject)
        {
            return BuildNodePath(gameObject.transform);
        }

        private static string BuildNodePath(Transform transform)
        {
            var segments = new List<string>();
            Transform current = transform;
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

        private static GameObject ResolveRoot(Scene scene, string segment, string normalizedPath, string commandName)
        {
            (string name, int rootIndex) = InspectorUtility.ParsePathSegment(segment, commandName, "SCENE");
            GameObject[] matches = scene.GetRootGameObjects()
                .Where(gameObject => string.Equals(gameObject.name, name, StringComparison.Ordinal))
                .ToArray();
            if (rootIndex < 0 || rootIndex >= matches.Length)
            {
                throw new CommandFailureException("SCENE_NODE_NOT_FOUND", commandName + " path를 찾지 못했습니다: " + normalizedPath);
            }

            return matches[rootIndex];
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
    }
}
