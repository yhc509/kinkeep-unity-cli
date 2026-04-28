#nullable enable
using System;
using System.Collections.Generic;

namespace UnityCli.Protocol
{
    internal static class FriendlyKeyAliasCatalog
    {
        private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _aliasesByTypeName =
            new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
            {
                ["UnityEngine.Rigidbody"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    // Unity 2021.3 uses m_Drag/m_AngularDrag; Unity 6+ renamed these serialized fields to m_LinearDamping/m_AngularDamping.
                    ["damping"] = "m_Drag",
                    ["angularDamping"] = "m_AngularDrag",
                    ["constraints"] = "m_Constraints",
                    ["collisionDetection"] = "m_CollisionDetection",
                    ["collisionDetectionMode"] = "m_CollisionDetection",
                    ["isKinematic"] = "m_IsKinematic",
                    ["useGravity"] = "m_UseGravity",
                    ["mass"] = "m_Mass",
                },
                ["UnityEngine.Collider"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["isTrigger"] = "m_IsTrigger",
                    ["material"] = "m_Material",
                    ["contactOffset"] = "m_ContactOffset",
                    ["enabled"] = "m_Enabled",
                },
                ["UnityEngine.BoxCollider"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["size"] = "m_Size",
                    ["center"] = "m_Center",
                },
                ["UnityEngine.SphereCollider"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["radius"] = "m_Radius",
                    ["center"] = "m_Center",
                },
                ["UnityEngine.CapsuleCollider"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["radius"] = "m_Radius",
                    ["height"] = "m_Height",
                    ["direction"] = "m_Direction",
                    ["center"] = "m_Center",
                },
                ["UnityEngine.MeshCollider"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["mesh"] = "m_Mesh",
                    ["convex"] = "m_Convex",
                },
                ["UnityEngine.Renderer"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["materials"] = "m_Materials",
                    ["sharedMaterial[0]"] = "m_Materials.Array.data[0]",
                    ["sharedMaterials[0]"] = "m_Materials.Array.data[0]",
                    ["materials[0]"] = "m_Materials.Array.data[0]",
                    ["receiveShadows"] = "m_ReceiveShadows",
                    ["shadowCastingMode"] = "m_CastShadows",
                    ["lightProbeUsage"] = "m_LightProbeUsage",
                    ["reflectionProbeUsage"] = "m_ReflectionProbeUsage",
                    ["enabled"] = "m_Enabled",
                },
                ["UnityEngine.Light"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["color"] = "m_Color",
                    ["intensity"] = "m_Intensity",
                    ["range"] = "m_Range",
                    ["type"] = "m_Type",
                    ["bounceIntensity"] = "m_BounceIntensity",
                    ["shadows"] = "m_Shadows",
                    ["shadowStrength"] = "m_Shadows.m_Strength",
                },
                ["UnityEngine.Camera"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["fieldOfView"] = "field of view",
                    ["field of view"] = "field of view",
                    ["near"] = "near clip plane",
                    ["nearClipPlane"] = "near clip plane",
                    ["near clip plane"] = "near clip plane",
                    ["far"] = "far clip plane",
                    ["farClipPlane"] = "far clip plane",
                    ["far clip plane"] = "far clip plane",
                    ["backgroundColor"] = "m_BackGroundColor",
                    ["clearFlags"] = "m_ClearFlags",
                    ["depth"] = "m_Depth",
                    ["cullingMask"] = "m_CullingMask",
                    ["orthographic"] = "orthographic",
                    ["orthographicSize"] = "orthographic size",
                    ["orthographic size"] = "orthographic size",
                },
            };

        internal static bool TryGetCanonicalPath(Type componentType, string key, out string canonicalPath)
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

                if (_aliasesByTypeName.TryGetValue(fullName, out IReadOnlyDictionary<string, string>? aliases)
                    && aliases.TryGetValue(key, out string? resolvedPath)
                    && resolvedPath != null)
                {
                    canonicalPath = resolvedPath;
                    return true;
                }

                if (string.Equals(fullName, "UnityEngine.Renderer", StringComparison.Ordinal)
                    && TryGetRendererMaterialElementPath(key, out canonicalPath))
                {
                    return true;
                }
            }

            canonicalPath = string.Empty;
            return false;
        }

        private static bool TryGetRendererMaterialElementPath(string key, out string canonicalPath)
        {
            canonicalPath = string.Empty;

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

            canonicalPath = "m_Materials.Array.data[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
            return true;
        }
    }
}
