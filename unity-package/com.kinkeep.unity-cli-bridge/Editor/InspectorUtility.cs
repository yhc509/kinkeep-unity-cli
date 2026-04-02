#nullable enable
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
