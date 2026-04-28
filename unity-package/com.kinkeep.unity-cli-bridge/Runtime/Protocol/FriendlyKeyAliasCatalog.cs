#nullable enable
using System;
using System.Collections.Generic;

namespace UnityCli.Protocol
{
    internal static class FriendlyKeyAliasCatalog
    {
        private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> _aliasesByTypeName =
            new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
            {
                ["UnityEngine.Rigidbody"] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    // Unity 2021.3: m_Drag; Unity 6: m_LinearDamping.
                    ["damping"] = new[] { "m_Drag", "m_LinearDamping" },
                    // Unity 2021.3: m_AngularDrag; Unity 6: m_AngularDamping.
                    ["angularDamping"] = new[] { "m_AngularDrag", "m_AngularDamping" },
                    ["constraints"] = new[] { "m_Constraints" },
                    ["collisionDetection"] = new[] { "m_CollisionDetection" },
                    ["collisionDetectionMode"] = new[] { "m_CollisionDetection" },
                    ["isKinematic"] = new[] { "m_IsKinematic" },
                    ["useGravity"] = new[] { "m_UseGravity" },
                    ["mass"] = new[] { "m_Mass" },
                },
                ["UnityEngine.Collider"] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["isTrigger"] = new[] { "m_IsTrigger" },
                    ["material"] = new[] { "m_Material" },
                    ["contactOffset"] = new[] { "m_ContactOffset" },
                    ["enabled"] = new[] { "m_Enabled" },
                },
                ["UnityEngine.BoxCollider"] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["size"] = new[] { "m_Size" },
                    ["center"] = new[] { "m_Center" },
                },
                ["UnityEngine.SphereCollider"] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["radius"] = new[] { "m_Radius" },
                    ["center"] = new[] { "m_Center" },
                },
                ["UnityEngine.CapsuleCollider"] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["radius"] = new[] { "m_Radius" },
                    ["height"] = new[] { "m_Height" },
                    ["direction"] = new[] { "m_Direction" },
                    ["center"] = new[] { "m_Center" },
                },
                ["UnityEngine.MeshCollider"] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["mesh"] = new[] { "m_Mesh" },
                    ["convex"] = new[] { "m_Convex" },
                },
                ["UnityEngine.Renderer"] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["materials"] = new[] { "m_Materials" },
                    ["sharedMaterial[0]"] = new[] { "m_Materials.Array.data[0]" },
                    ["sharedMaterials[0]"] = new[] { "m_Materials.Array.data[0]" },
                    ["materials[0]"] = new[] { "m_Materials.Array.data[0]" },
                    ["receiveShadows"] = new[] { "m_ReceiveShadows" },
                    ["shadowCastingMode"] = new[] { "m_CastShadows" },
                    ["lightProbeUsage"] = new[] { "m_LightProbeUsage" },
                    ["reflectionProbeUsage"] = new[] { "m_ReflectionProbeUsage" },
                    ["enabled"] = new[] { "m_Enabled" },
                },
                ["UnityEngine.Light"] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["color"] = new[] { "m_Color" },
                    ["intensity"] = new[] { "m_Intensity" },
                    ["range"] = new[] { "m_Range" },
                    ["type"] = new[] { "m_Type" },
                    ["bounceIntensity"] = new[] { "m_BounceIntensity" },
                    ["shadows"] = new[] { "m_Shadows" },
                    ["shadowStrength"] = new[] { "m_Shadows.m_Strength" },
                },
                ["UnityEngine.Camera"] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["fieldOfView"] = new[] { "field of view" },
                    ["field of view"] = new[] { "field of view" },
                    ["near"] = new[] { "near clip plane" },
                    ["nearClipPlane"] = new[] { "near clip plane" },
                    ["near clip plane"] = new[] { "near clip plane" },
                    ["far"] = new[] { "far clip plane" },
                    ["farClipPlane"] = new[] { "far clip plane" },
                    ["far clip plane"] = new[] { "far clip plane" },
                    ["backgroundColor"] = new[] { "m_BackGroundColor" },
                    ["clearFlags"] = new[] { "m_ClearFlags" },
                    ["depth"] = new[] { "m_Depth" },
                    ["cullingMask"] = new[] { "m_CullingMask" },
                    ["orthographic"] = new[] { "orthographic" },
                    ["orthographicSize"] = new[] { "orthographic size" },
                    ["orthographic size"] = new[] { "orthographic size" },
                },
            };

        internal static bool TryGetCanonicalPaths(Type componentType, string key, out IReadOnlyList<string> canonicalPaths)
        {
            if (componentType == null)
            {
                throw new ArgumentNullException(nameof(componentType));
            }

            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            for (Type? currentType = componentType; currentType != null; currentType = currentType.BaseType)
            {
                string? fullName = currentType.FullName;
                if (fullName == null)
                {
                    continue;
                }

                if (_aliasesByTypeName.TryGetValue(fullName, out IReadOnlyDictionary<string, IReadOnlyList<string>>? aliases)
                    && aliases.TryGetValue(key, out IReadOnlyList<string>? resolvedPaths)
                    && resolvedPaths != null)
                {
                    canonicalPaths = resolvedPaths;
                    return true;
                }

                if (string.Equals(fullName, "UnityEngine.Renderer", StringComparison.Ordinal)
                    && TryGetRendererMaterialElementPath(key, out canonicalPaths))
                {
                    return true;
                }
            }

            canonicalPaths = Array.Empty<string>();
            return false;
        }

        private static bool TryGetRendererMaterialElementPath(string key, out IReadOnlyList<string> canonicalPaths)
        {
            canonicalPaths = Array.Empty<string>();

            int openBracketIndex = key.IndexOf('[');
            if (openBracketIndex <= 0 || key[key.Length - 1] != ']')
            {
                return false;
            }

            string prefix = key.Substring(0, openBracketIndex);
            if (!string.Equals(prefix, "materials", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(prefix, "sharedMaterial", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(prefix, "sharedMaterials", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string indexText = key.Substring(openBracketIndex + 1, key.Length - openBracketIndex - 2);
            if (indexText.Length == 0 || (indexText.Length > 1 && indexText[0] == '0'))
            {
                return false;
            }

            for (int i = 0; i < indexText.Length; i++)
            {
                if (indexText[i] < '0' || indexText[i] > '9')
                {
                    return false;
                }
            }

            if (!int.TryParse(indexText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int index))
            {
                return false;
            }

            canonicalPaths = new[] { "m_Materials.Array.data[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]" };
            return true;
        }
    }
}
