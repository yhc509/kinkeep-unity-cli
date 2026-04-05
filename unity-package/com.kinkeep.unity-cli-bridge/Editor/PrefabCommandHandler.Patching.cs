using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace KinKeep.UnityCli.Bridge.Editor
{
    internal sealed partial class PrefabCommandHandler
    {
        private static void AddChildren(Transform parent, PrefabNodeSpec[] children, string commandName)
        {
            if (children == null)
            {
                return;
            }

            foreach (PrefabNodeSpec childSpec in children)
            {
                string childName = InspectorUtility.RequireNodeName(childSpec == null ? null : childSpec.Name, commandName, "PREFAB");
                var child = new GameObject(childName);
                child.transform.SetParent(parent, false);
                PrefabInspector.ApplyNodeState(child, childSpec, childName, allowMissingName: false);
                AddComponents(child, childSpec.Components, commandName);
                AddChildren(child.transform, childSpec.Children, commandName);
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
            if (componentSpec == null || string.IsNullOrWhiteSpace(componentSpec.Type))
            {
                throw new CommandFailureException("PREFAB_COMPONENT_INVALID", commandName + " component type이 비어 있습니다.");
            }

            Type componentType = ResolveComponentType(componentSpec.Type, commandName);
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
                throw new CommandFailureException("PREFAB_COMPONENT_INVALID", "component를 추가하지 못했습니다: " + componentSpec.Type, exception.Message);
            }

            if (componentSpec.Values != null)
            {
                SerializedValueApplier.Apply(component, componentSpec.Values);
            }

            return component;
        }

        private static PrefabPatchApplyResult ApplyPatchOperations(GameObject root, PrefabPatchOperationSpec[] operations)
        {
            var result = new PrefabPatchApplyResult();
            foreach (PrefabPatchOperationSpec operation in operations)
            {
                if (operation == null || string.IsNullOrWhiteSpace(operation.Operation))
                {
                    throw new CommandFailureException("PREFAB_SPEC_INVALID", "patch operation `op`가 비어 있습니다.");
                }

                switch (operation.Operation.Trim().ToLowerInvariant())
                {
                    case "add-child":
                    {
                        GameObject parent = PrefabInspector.ResolveNode(root, operation.Parent, "add-child");
                        if (operation.Node == null)
                        {
                            throw new CommandFailureException("PREFAB_SPEC_INVALID", "`add-child`에는 `node`가 필요합니다.");
                        }

                        AddChildren(parent.transform, new[] { operation.Node }, "add-child");
                        result.Patched = true;
                        break;
                    }
                    case "remove-node":
                    {
                        GameObject target = PrefabInspector.ResolveNode(root, operation.Target, "remove-node");
                        if (target == root)
                        {
                            throw new CommandFailureException("PREFAB_NODE_INVALID", "루트 오브젝트는 삭제할 수 없습니다.");
                        }

                        UnityEngine.Object.DestroyImmediate(target);
                        result.Patched = true;
                        break;
                    }
                    case "set-node":
                    {
                        GameObject target = PrefabInspector.ResolveNode(root, operation.Target, "set-node");
                        JObject rawValues = operation.Values as JObject;
                        PrefabNodeMutationSpec values = operation.Values == null
                            ? null
                            : operation.Values.ToObject<PrefabNodeMutationSpec>(_serializer);
                        if (values == null)
                        {
                            throw new CommandFailureException("PREFAB_SPEC_INVALID", "`set-node`에는 `values`가 필요합니다.");
                        }

                        if (rawValues == null)
                        {
                            PrefabInspector.ApplyNodeState(target, values);
                            result.Patched = true;
                            break;
                        }

                        NodeMutationAnalysis analysis = InspectorUtility.AnalyzeNodeMutationValues(rawValues);
                        result.Warnings.AddRange(analysis.Warnings);
                        if (!analysis.HasRecognizedKeys)
                        {
                            break;
                        }

                        PrefabInspector.ApplyNodeState(target, values);
                        result.Patched = true;
                        break;
                    }
                    case "add-component":
                    {
                        GameObject target = PrefabInspector.ResolveNode(root, operation.Target, "add-component");
                        if (operation.Component == null)
                        {
                            throw new CommandFailureException("PREFAB_SPEC_INVALID", "`add-component`에는 `component`가 필요합니다.");
                        }

                        AddComponent(target, operation.Component, "add-component");
                        result.Patched = true;
                        break;
                    }
                    case "remove-component":
                    {
                        GameObject target = PrefabInspector.ResolveNode(root, operation.Target, "remove-component");
                        Component component = ResolveComponent(target, operation.ComponentType, operation.ComponentIndex, "remove-component");
                        if (component is Transform)
                        {
                            throw new CommandFailureException("PREFAB_COMPONENT_INVALID", "Transform은 제거할 수 없습니다.");
                        }

                        UnityEngine.Object.DestroyImmediate(component, true);
                        result.Patched = true;
                        break;
                    }
                    case "set-component-values":
                    {
                        GameObject target = PrefabInspector.ResolveNode(root, operation.Target, "set-component-values");
                        Component component = ResolveComponent(target, operation.ComponentType, operation.ComponentIndex, "set-component-values");
                        if (operation.Values == null || operation.Values.Type != JTokenType.Object)
                        {
                            throw new CommandFailureException("PREFAB_SPEC_INVALID", "`set-component-values`에는 object 형태의 `values`가 필요합니다.");
                        }

                        SerializedValueApplier.Apply(component, (JObject)operation.Values);
                        result.Patched = true;
                        break;
                    }
                    default:
                        throw new CommandFailureException("PREFAB_SPEC_INVALID", "지원하지 않는 patch operation입니다: " + operation.Operation);
                }
            }

            return result;
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

            List<Type> exactMatches = TypeDiscoveryUtility.FindTypes(type =>
                typeof(Component).IsAssignableFrom(type)
                && !type.IsAbstract
                && !type.ContainsGenericParameters
                && string.Equals(type.FullName, normalized, StringComparison.Ordinal));
            if (exactMatches.Count == 1)
            {
                return exactMatches[0];
            }

            if (exactMatches.Count > 1)
            {
                throw new CommandFailureException("PREFAB_COMPONENT_INVALID", "동일한 full name의 component 타입이 여러 개 있습니다: " + normalized);
            }

            List<Type> shortMatches = TypeDiscoveryUtility.FindTypes(type =>
                typeof(Component).IsAssignableFrom(type)
                && !type.IsAbstract
                && !type.ContainsGenericParameters
                && string.Equals(type.Name, normalized, StringComparison.Ordinal));
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

        private sealed class PrefabPatchApplyResult
        {
            internal bool Patched { get; set; }

            internal List<string> Warnings { get; } = new List<string>();
        }
    }
}
