#nullable enable
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityCli.Protocol;
using UnityEngine;

namespace KinKeep.UnityCli.Bridge.Editor
{
    internal static class PrefabInspector
    {
        internal static string BuildInspectPayload(string path, GameObject root, bool withValues)
        {
            JObject payload = new JObject
            {
                ["asset"] = BuildAssetToken(path),
                ["root"] = BuildNodeToken(root, withValues),
            };

            return payload.ToString(Formatting.None);
        }

        internal static GameObject ResolveNode(GameObject root, string? path, string commandName)
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
                var matches = new List<Transform>();
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

        internal static void ApplyNodeState(GameObject target, PrefabNodeSpec? spec, string defaultName, bool allowMissingName)
        {
            if (spec == null)
            {
                throw new CommandFailureException("PREFAB_SPEC_INVALID", "node spec이 비어 있습니다.");
            }

            string name = string.IsNullOrWhiteSpace(spec.Name)
                ? (allowMissingName ? defaultName : RequireNodeName(spec.Name, "prefab"))
                : spec.Name.Trim();
            target.name = name;

            ApplyNodeStateCore(target, spec.IsActive, spec.Tag, spec.Layer, spec.Transform);
        }

        internal static void ApplyNodeState(GameObject target, PrefabNodeMutationSpec? spec)
        {
            if (spec == null)
            {
                throw new CommandFailureException("PREFAB_SPEC_INVALID", "node values가 비어 있습니다.");
            }

            if (spec.Name != null)
            {
                target.name = RequireNodeName(spec.Name, "set-node");
            }

            ApplyNodeStateCore(target, spec.IsActive, spec.Tag, spec.Layer, spec.Transform);
        }

        internal static string RequireNodeName(string? name, string commandName)
        {
            string normalized = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new CommandFailureException("PREFAB_NODE_INVALID", commandName + " node 이름이 비어 있습니다.");
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

        private static JToken BuildLayerToken(int layerIndex)
        {
            string layerName = LayerMask.LayerToName(layerIndex);
            return string.IsNullOrWhiteSpace(layerName)
                ? (JToken)new JValue(layerIndex)
                : new JValue(layerName);
        }

        private static void ApplyNodeStateCore(GameObject target, bool? active, string? tag, JToken? layer, PrefabTransformSpec? transformSpec)
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
