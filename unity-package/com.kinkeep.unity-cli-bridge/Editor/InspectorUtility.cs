#nullable enable
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityCli.Protocol;
using UnityEngine;

namespace KinKeep.UnityCli.Bridge.Editor
{
    internal static class InspectorUtility
    {
        internal static JObject BuildAssetToken(string path)
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

        internal static JToken BuildLayerToken(int layerIndex)
        {
            string layerName = LayerMask.LayerToName(layerIndex);
            return string.IsNullOrWhiteSpace(layerName)
                ? (JToken)new JValue(layerIndex)
                : new JValue(layerName);
        }

        internal static Vector3? MergeVector3(Vector3 current, float? x, float? y, float? z)
        {
            if (!x.HasValue && !y.HasValue && !z.HasValue)
            {
                return null;
            }

            return new Vector3(
                x ?? current.x,
                y ?? current.y,
                z ?? current.z);
        }

        internal static JObject BuildVector3Token(Vector3 vector)
        {
            return new JObject
            {
                ["x"] = vector.x,
                ["y"] = vector.y,
                ["z"] = vector.z,
            };
        }

        internal static JObject BuildTransformToken(Transform transform, bool omitDefaults)
        {
            var transformToken = new JObject();
            if (!omitDefaults || !IsExactlyZero(transform.localPosition))
            {
                transformToken["localPosition"] = BuildVector3Token(transform.localPosition);
            }

            if (!omitDefaults || !IsExactlyIdentity(transform.localRotation))
            {
                transformToken["localRotationEuler"] = BuildVector3Token(transform.localEulerAngles);
            }

            if (!omitDefaults || !IsExactlyOne(transform.localScale))
            {
                transformToken["localScale"] = BuildVector3Token(transform.localScale);
            }

            return transformToken;
        }

        internal static JArray BuildChildStubTokens(Transform parent, Func<Transform, string> buildPath)
        {
            var children = new JArray();
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                children.Add(new JObject
                {
                    ["name"] = child.name,
                    ["path"] = buildPath(child),
                    ["childCount"] = child.childCount,
                });
            }

            return children;
        }

        internal static int? ParseOptionalMaxDepth(string? argumentsJson, string errorCode)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                return null;
            }

            JObject arguments = JObject.Parse(argumentsJson);
            JToken? token = arguments["maxDepth"];
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
            {
                return null;
            }

            if (token.Type != JTokenType.Integer)
            {
                throw new CommandFailureException(errorCode, "`--max-depth`는 정수여야 합니다.");
            }

            return token.Value<int>();
        }

        internal static void ApplyNodeDefaultOmissions(JObject node, GameObject gameObject)
        {
            if (gameObject.activeSelf)
            {
                node.Remove("active");
            }

            if (string.Equals(gameObject.tag, "Untagged", System.StringComparison.Ordinal))
            {
                node.Remove("tag");
            }

            if (gameObject.layer == 0)
            {
                node.Remove("layer");
            }

            RemoveIfEmptyArray(node, "components");
            RemoveIfEmptyArray(node, "children");

            JToken? transform = node["transform"];
            if (transform is JObject transformObject && !transformObject.HasValues)
            {
                node.Remove("transform");
            }
        }

        internal static void PruneDefaultInspectableValues(JObject values)
        {
            foreach (JProperty property in values.Properties().ToArray())
            {
                if (ShouldOmitInspectableValue(property.Value))
                {
                    property.Remove();
                }
            }
        }

        private static bool ShouldOmitInspectableValue(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Null:
                case JTokenType.Undefined:
                    return true;
                case JTokenType.Integer:
                    return token.Value<long>() == 0L;
                case JTokenType.Float:
                    // Strict equality intentional — only exact 0.0 is omitted, not near-zero values.
                    return token.Value<double>() == 0d;
                case JTokenType.Boolean:
                    return !token.Value<bool>();
                case JTokenType.String:
                    return string.IsNullOrEmpty(token.Value<string>());
                case JTokenType.Object:
                {
                    var obj = (JObject)token;
                    PruneDefaultInspectableValues(obj);
                    return !obj.HasValues;
                }
                case JTokenType.Array:
                {
                    var array = (JArray)token;
                    foreach (JToken item in array)
                    {
                        if (item is JObject itemObject)
                        {
                            PruneDefaultInspectableValues(itemObject);
                        }
                        else if (item is JArray itemArray)
                        {
                            PruneDefaultInspectableArray(itemArray);
                        }
                    }

                    return array.Count == 0;
                }
                default:
                    return false;
            }
        }

        private static void PruneDefaultInspectableArray(JArray values)
        {
            foreach (JToken value in values)
            {
                if (value is JObject obj)
                {
                    PruneDefaultInspectableValues(obj);
                }
                else if (value is JArray array)
                {
                    PruneDefaultInspectableArray(array);
                }
            }
        }

        private static void RemoveIfEmptyArray(JObject node, string propertyName)
        {
            JToken? token = node[propertyName];
            if (token is JArray array && array.Count == 0)
            {
                node.Remove(propertyName);
            }
        }

        private static bool IsExactlyZero(Vector3 value)
        {
            return value.x == 0f && value.y == 0f && value.z == 0f;
        }

        private static bool IsExactlyOne(Vector3 value)
        {
            return value.x == 1f && value.y == 1f && value.z == 1f;
        }

        private static bool IsExactlyIdentity(Quaternion value)
        {
            return value.x == 0f && value.y == 0f && value.z == 0f && value.w == 1f;
        }

        internal static string RequireNodeName(string? name, string commandName, string errorPrefix)
        {
            string normalized = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new CommandFailureException(errorPrefix + "_NODE_INVALID", commandName + " node 이름이 비어 있습니다.");
            }

            return normalized;
        }

        internal static (string name, int index) ParsePathSegment(string segment, string commandName, string errorPrefix)
        {
            int bracketIndex = segment.LastIndexOf('[');
            if (bracketIndex > 0 && segment.EndsWith("]", System.StringComparison.Ordinal))
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
                throw new CommandFailureException(errorPrefix + "_NODE_INVALID", commandName + " path segment가 비어 있습니다.");
            }

            return (segment, 0);
        }

        internal static int ResolveLayer(JToken token, string errorPrefix)
        {
            if (token.Type == JTokenType.Integer)
            {
                int layerIndex = token.Value<int>();
                if (layerIndex < 0 || layerIndex > 31)
                {
                    throw new CommandFailureException(errorPrefix + "_NODE_INVALID", "layer index가 범위를 벗어났습니다: " + layerIndex);
                }

                return layerIndex;
            }

            if (token.Type != JTokenType.String)
            {
                throw new CommandFailureException(errorPrefix + "_NODE_INVALID", "layer는 string 또는 int여야 합니다.");
            }

            string? layerName = token.Value<string>();
            if (string.IsNullOrWhiteSpace(layerName))
            {
                throw new CommandFailureException(errorPrefix + "_NODE_INVALID", "layer string이 비어 있습니다.");
            }

            if (int.TryParse(layerName, out int numericLayer))
            {
                return ResolveLayer(new JValue(numericLayer), errorPrefix);
            }

            int resolvedLayer = LayerMask.NameToLayer(layerName);
            if (resolvedLayer < 0)
            {
                throw new CommandFailureException(errorPrefix + "_NODE_INVALID", "layer를 찾지 못했습니다: " + layerName);
            }

            return resolvedLayer;
        }

        internal static void ApplyTransformSpec(Transform transform, Vector3? position, Vector3? rotation, Vector3? scale)
        {
            if (position.HasValue)
            {
                transform.localPosition = position.Value;
            }

            if (rotation.HasValue)
            {
                transform.localEulerAngles = rotation.Value;
            }

            if (scale.HasValue)
            {
                transform.localScale = scale.Value;
            }
        }

        internal static void ApplyNodeStateCore(
            GameObject target,
            bool? active,
            string? tag,
            JToken? layer,
            Vector3? position,
            Vector3? rotation,
            Vector3? scale,
            string errorPrefix)
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
                    throw new CommandFailureException(errorPrefix + "_NODE_INVALID", "tag가 비어 있습니다.");
                }

                target.tag = normalizedTag;
            }

            if (layer != null)
            {
                target.layer = ResolveLayer(layer, errorPrefix);
            }

            ApplyTransformSpec(target.transform, position, rotation, scale);
        }
    }
}
