#nullable enable
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace KinKeep.UnityCli.Bridge.Editor
{
    internal static class PrefabInspector
    {
        internal static string BuildInspectPayload(string path, GameObject root, bool withValues, int? maxDepth, bool omitDefaults)
        {
            JObject payload = new JObject
            {
                ["asset"] = InspectorUtility.BuildAssetToken(path),
                ["root"] = BuildNodeToken(root, withValues, maxDepth, omitDefaults, 0),
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
                (string name, int index) = InspectorUtility.ParsePathSegment(segment, commandName, "PREFAB");
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
                ? (allowMissingName ? defaultName : InspectorUtility.RequireNodeName(spec.Name, "prefab", "PREFAB"))
                : spec.Name.Trim();
            target.name = name;

            PrefabTransformSpec? transformSpec = spec.Transform;
            Transform transform = target.transform;
            InspectorUtility.ApplyNodeStateCore(
                target,
                spec.IsActive,
                spec.Tag,
                spec.Layer,
                InspectorUtility.MergeVector3(transform.localPosition, transformSpec?.LocalPosition?.X, transformSpec?.LocalPosition?.Y, transformSpec?.LocalPosition?.Z),
                InspectorUtility.MergeVector3(transform.localEulerAngles, transformSpec?.LocalRotationEuler?.X, transformSpec?.LocalRotationEuler?.Y, transformSpec?.LocalRotationEuler?.Z),
                InspectorUtility.MergeVector3(transform.localScale, transformSpec?.LocalScale?.X, transformSpec?.LocalScale?.Y, transformSpec?.LocalScale?.Z),
                "PREFAB");
        }

        internal static void ApplyNodeState(GameObject target, PrefabNodeMutationSpec? spec)
        {
            if (spec == null)
            {
                throw new CommandFailureException("PREFAB_SPEC_INVALID", "node values가 비어 있습니다.");
            }

            if (spec.Name != null)
            {
                target.name = InspectorUtility.RequireNodeName(spec.Name, "set-node", "PREFAB");
            }

            PrefabTransformSpec? transformSpec = spec.Transform;
            Transform transform = target.transform;
            InspectorUtility.ApplyNodeStateCore(
                target,
                spec.IsActive,
                spec.Tag,
                spec.Layer,
                InspectorUtility.MergeVector3(transform.localPosition, transformSpec?.LocalPosition?.X, transformSpec?.LocalPosition?.Y, transformSpec?.LocalPosition?.Z),
                InspectorUtility.MergeVector3(transform.localEulerAngles, transformSpec?.LocalRotationEuler?.X, transformSpec?.LocalRotationEuler?.Y, transformSpec?.LocalRotationEuler?.Z),
                InspectorUtility.MergeVector3(transform.localScale, transformSpec?.LocalScale?.X, transformSpec?.LocalScale?.Y, transformSpec?.LocalScale?.Z),
                "PREFAB");
        }

        private static JObject BuildNodeToken(GameObject gameObject, bool withValues, int? maxDepth, bool omitDefaults, int currentDepth)
        {
            var node = new JObject
            {
                ["path"] = BuildNodePath(gameObject.transform),
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

        internal static string BuildNodePath(Transform transform)
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
    }
}
