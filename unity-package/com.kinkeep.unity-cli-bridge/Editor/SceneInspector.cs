#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityCli.Protocol;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KinKeep.UnityCli.Bridge.Editor
{
    internal static class SceneInspector
    {
        internal static string BuildInspectPayload(string path, Scene scene, bool withValues, string activeScenePath)
        {
            var roots = new JArray();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                roots.Add(BuildNodeToken(root, withValues));
            }

            var payload = new JObject
            {
                ["asset"] = BuildAssetToken(path),
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
                (string name, int siblingIndex) = ParsePathSegment(segments[index], commandName);
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
                ? (allowMissingName ? defaultName : RequireNodeName(spec.Name, "scene"))
                : spec.Name.Trim();
            target.name = name;

            ApplyNodeStateCore(target, spec.IsActive, spec.Tag, spec.Layer, spec.Transform);
        }

        internal static void ApplyNodeState(GameObject target, SceneNodeMutationSpec? spec)
        {
            if (spec == null)
            {
                throw new CommandFailureException("SCENE_SPEC_INVALID", "node values가 비어 있습니다.");
            }

            if (spec.Name != null)
            {
                target.name = RequireNodeName(spec.Name, "modify-gameobject");
            }

            ApplyNodeStateCore(target, spec.IsActive, spec.Tag, spec.Layer, spec.Transform);
        }

        internal static string RequireNodeName(string? name, string commandName)
        {
            string normalized = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new CommandFailureException("SCENE_NODE_INVALID", commandName + " node 이름이 비어 있습니다.");
            }

            return normalized;
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

        private static void ApplyNodeStateCore(GameObject target, bool? active, string? tag, JToken? layer, SceneTransformSpec? transformSpec)
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

            string? layerName = token.Value<string>();
            if (string.IsNullOrWhiteSpace(layerName))
            {
                throw new CommandFailureException("SCENE_NODE_INVALID", "layer string이 비어 있습니다.");
            }

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
            if (spec.LocalPosition != null)
            {
                Vector3 current = transform.localPosition;
                transform.localPosition = new Vector3(
                    spec.LocalPosition.X ?? current.x,
                    spec.LocalPosition.Y ?? current.y,
                    spec.LocalPosition.Z ?? current.z);
            }

            if (spec.LocalRotationEuler != null)
            {
                Vector3 current = transform.localEulerAngles;
                transform.localEulerAngles = new Vector3(
                    spec.LocalRotationEuler.X ?? current.x,
                    spec.LocalRotationEuler.Y ?? current.y,
                    spec.LocalRotationEuler.Z ?? current.z);
            }

            if (spec.LocalScale != null)
            {
                Vector3 current = transform.localScale;
                transform.localScale = new Vector3(
                    spec.LocalScale.X ?? current.x,
                    spec.LocalScale.Y ?? current.y,
                    spec.LocalScale.Z ?? current.z);
            }
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
    }
}
