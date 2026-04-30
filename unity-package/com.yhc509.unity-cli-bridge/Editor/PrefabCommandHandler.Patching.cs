#nullable enable
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityCliBridge.Bridge.Editor
{
    internal sealed partial class PrefabCommandHandler
    {
        // Reused to avoid GC allocations. Safe because all callers run on the
        // main thread and no method using this buffer calls another that also uses it.
        private static readonly List<Component> _componentBuffer = new List<Component>(8);

        private static void AddChildren(Transform parent, PrefabNodeSpec[] children, string commandName)
        {
            if (children == null)
            {
                return;
            }

            foreach (PrefabNodeSpec childSpec in children)
            {
                string childName = InspectorPathParserUtility.RequireNodeName(childSpec == null ? null : childSpec.Name, commandName, "PREFAB");
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

            Component? component;
            try
            {
                component = target.AddComponent(componentType);
            }
            catch (Exception exception)
            {
                throw new CommandFailureException("PREFAB_COMPONENT_INVALID", "component를 추가하지 못했습니다: " + componentSpec.Type, exception.Message);
            }

            if (component == null)
            {
                throw new CommandFailureException(
                    "PREFAB_COMPONENT_ADD_FAILED",
                    "component를 추가하지 못했습니다: " + componentSpec.Type + " @ " + PrefabInspector.BuildNodePath(target.transform));
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

                switch (operation.Operation)
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
                        if (operation.Values is not JObject rawValues)
                        {
                            throw new CommandFailureException("PREFAB_SPEC_INVALID", "`set-node`에는 `values`가 필요합니다.");
                        }

                        NodeMutationAnalysis analysis = InspectorMutationReaderUtility.AnalyzeNodeMutationValues(rawValues);
                        result.Warnings.AddRange(analysis.Warnings);
                        if (!analysis.HasRecognizedKeys)
                        {
                            break;
                        }

                        PrefabInspector.ApplyNodeState(target, rawValues, "set-node");
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

        private static void NormalizeOperationNames(PrefabPatchOperationSpec[] operations)
        {
            for (int index = 0; index < operations.Length; index++)
            {
                PrefabPatchOperationSpec operation = operations[index];
                if (operation == null)
                {
                    continue;
                }

                operation.Operation = PatchUtility.NormalizeOperationName(operation.Operation);
            }
        }

        private static Component ResolveComponent(GameObject target, string componentTypeName, int? componentIndex, string commandName)
        {
            Type componentType = ResolveComponentType(componentTypeName, commandName);
            int requestedIndex = componentIndex ?? 0;
            if (requestedIndex < 0)
            {
                throw new CommandFailureException("PREFAB_COMPONENT_NOT_FOUND", commandName + " component index가 범위를 벗어났습니다: " + requestedIndex);
            }

            _componentBuffer.Clear();
            target.GetComponents(_componentBuffer);

            int matchedCount = 0;
            for (int index = 0; index < _componentBuffer.Count; index++)
            {
                Component component = _componentBuffer[index];
                if (component == null || !componentType.IsAssignableFrom(component.GetType()))
                {
                    continue;
                }

                if (matchedCount == requestedIndex)
                {
                    return component;
                }

                matchedCount++;
            }

            if (matchedCount == 0)
            {
                throw new CommandFailureException("PREFAB_COMPONENT_NOT_FOUND", commandName + " 대상 component를 찾지 못했습니다: " + componentTypeName);
            }

            throw new CommandFailureException("PREFAB_COMPONENT_NOT_FOUND", commandName + " component index가 범위를 벗어났습니다: " + requestedIndex);
        }

        private static Type ResolveComponentType(string typeName, string commandName)
        {
            string normalized = string.IsNullOrWhiteSpace(typeName) ? string.Empty : typeName.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new CommandFailureException("PREFAB_COMPONENT_INVALID", commandName + " component type이 비어 있습니다.");
            }

            Type? exactMatch = null;
            int exactMatchCount = 0;
            IReadOnlyList<Type> exactCandidates = TypeDiscoveryUtility.FindTypesByFullName(normalized);
            for (int index = 0; index < exactCandidates.Count; index++)
            {
                Type candidate = exactCandidates[index];
                if (!IsResolvableComponentType(candidate))
                {
                    continue;
                }

                exactMatch = candidate;
                exactMatchCount++;
                if (exactMatchCount > 1)
                {
                    break;
                }
            }

            if (exactMatchCount == 1 && exactMatch != null)
            {
                return exactMatch;
            }

            if (exactMatchCount > 1)
            {
                throw new CommandFailureException("PREFAB_COMPONENT_INVALID", "동일한 full name의 component 타입이 여러 개 있습니다: " + normalized);
            }

            Type? shortMatch = null;
            int shortMatchCount = 0;
            IReadOnlyList<Type> shortCandidates = TypeDiscoveryUtility.FindTypesByShortName(normalized);
            for (int index = 0; index < shortCandidates.Count; index++)
            {
                Type candidate = shortCandidates[index];
                if (!IsResolvableComponentType(candidate))
                {
                    continue;
                }

                shortMatch = candidate;
                shortMatchCount++;
                if (shortMatchCount > 1)
                {
                    break;
                }
            }

            if (shortMatchCount == 1 && shortMatch != null)
            {
                return shortMatch;
            }

            if (shortMatchCount > 1)
            {
                throw new CommandFailureException("PREFAB_COMPONENT_INVALID", "짧은 이름이 같은 component 타입이 여러 개 있습니다. full name을 사용하세요: " + normalized);
            }

            throw new CommandFailureException("PREFAB_COMPONENT_INVALID", "component 타입을 찾지 못했습니다: " + normalized);
        }

        private static bool IsResolvableComponentType(Type type)
        {
            return typeof(Component).IsAssignableFrom(type)
                && !type.IsAbstract
                && !type.ContainsGenericParameters;
        }

        private sealed class PrefabPatchApplyResult
        {
            internal bool Patched { get; set; }

            internal List<string> Warnings { get; } = new List<string>();
        }
    }
}
