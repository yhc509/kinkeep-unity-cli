#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityCli.Protocol;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KinKeep.UnityCli.Bridge.Editor
{
    internal sealed partial class SceneCommandHandler
    {
        private static bool HasDestructiveOperation(ScenePatchSpec spec)
        {
            return spec.Operations.Any(operation =>
                operation != null
                && !string.IsNullOrWhiteSpace(operation.Operation)
                && (string.Equals(operation.Operation, "delete-gameobject", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(operation.Operation, "remove-component", StringComparison.OrdinalIgnoreCase)));
        }

        private static ScenePatchApplyResult ApplyPatchOperations(Scene scene, ScenePatchOperationSpec[] operations)
        {
            var result = new ScenePatchApplyResult();
            var createdRoots = new List<GameObject>();
            foreach (ScenePatchOperationSpec operation in operations)
            {
                if (operation == null || string.IsNullOrWhiteSpace(operation.Operation))
                {
                    throw new CommandFailureException("SCENE_SPEC_INVALID", "patch operation `op`가 비어 있습니다.");
                }

                switch (operation.Operation.Trim().ToLowerInvariant())
                {
                    case "add-gameobject":
                    {
                        Transform? parent = SceneInspector.ResolveParent(scene, operation.Parent, "add-gameobject");
                        if (operation.Node == null)
                        {
                            throw new CommandFailureException("SCENE_SPEC_INVALID", "`add-gameobject`에는 `node`가 필요합니다.");
                        }

                        createdRoots.Add(AddNode(scene, parent, operation.Node, "add-gameobject"));
                        result.Patched = true;
                        break;
                    }
                    case "modify-gameobject":
                    {
                        GameObject target = SceneInspector.ResolveNode(scene, operation.Target, "modify-gameobject");
                        JObject? rawValues = operation.Values as JObject;
                        SceneNodeMutationSpec? values = operation.Values == null
                            ? null
                            : operation.Values.ToObject<SceneNodeMutationSpec>(_serializer);
                        if (values == null)
                        {
                            throw new CommandFailureException("SCENE_SPEC_INVALID", "`modify-gameobject`에는 `values`가 필요합니다.");
                        }

                        if (rawValues == null)
                        {
                            SceneInspector.ApplyNodeState(target, values);
                            result.Patched = true;
                            break;
                        }

                        NodeMutationAnalysis analysis = InspectorUtility.AnalyzeNodeMutationValues(rawValues);
                        result.Warnings.AddRange(analysis.Warnings);
                        if (!analysis.HasRecognizedKeys)
                        {
                            break;
                        }

                        SceneInspector.ApplyNodeState(target, values);
                        result.Patched = true;
                        break;
                    }
                    case "delete-gameobject":
                    {
                        GameObject target = SceneInspector.ResolveNode(scene, operation.Target, "delete-gameobject");
                        UnityEngine.Object.DestroyImmediate(target);
                        result.Patched = true;
                        break;
                    }
                    case "add-component":
                    {
                        GameObject target = SceneInspector.ResolveNode(scene, operation.Target, "add-component");
                        if (operation.Component == null)
                        {
                            throw new CommandFailureException("SCENE_SPEC_INVALID", "`add-component`에는 `component`가 필요합니다.");
                        }

                        AddComponent(target, operation.Component, "add-component");
                        result.Patched = true;
                        break;
                    }
                    case "modify-component":
                    {
                        GameObject target = SceneInspector.ResolveNode(scene, operation.Target, "modify-component");
                        Component component = ResolveComponent(target, operation.ComponentType, operation.ComponentIndex, "modify-component");
                        if (operation.Values == null || operation.Values.Type != JTokenType.Object)
                        {
                            throw new CommandFailureException("SCENE_SPEC_INVALID", "`modify-component`에는 object 형태의 `values`가 필요합니다.");
                        }

                        SerializedValueApplier.Apply(component, (JObject)operation.Values);
                        result.Patched = true;
                        break;
                    }
                    case "remove-component":
                    {
                        GameObject target = SceneInspector.ResolveNode(scene, operation.Target, "remove-component");
                        Component component = ResolveComponent(target, operation.ComponentType, operation.ComponentIndex, "remove-component");
                        if (component is Transform)
                        {
                            throw new CommandFailureException("SCENE_COMPONENT_INVALID", "Transform은 제거할 수 없습니다.");
                        }

                        UnityEngine.Object.DestroyImmediate(component, true);
                        result.Patched = true;
                        break;
                    }
                    default:
                        throw new CommandFailureException("SCENE_SPEC_INVALID", "지원하지 않는 patch operation입니다: " + operation.Operation);
                }
            }

            GameObject[] survivingCreatedRoots = createdRoots
                .Where(gameObject => gameObject != null)
                .ToArray();

            result.CreatedPath = survivingCreatedRoots.Length == 1
                ? SceneInspector.BuildNodePath(survivingCreatedRoots[0])
                : null;

            return result;
        }

        private static void AddNodes(Scene scene, Transform? parent, SceneNodeSpec[]? children, string commandName)
        {
            if (children == null)
            {
                return;
            }

            foreach (SceneNodeSpec childSpec in children)
            {
                AddNode(scene, parent, childSpec, commandName);
            }
        }

        private static GameObject AddNode(Scene scene, Transform? parent, SceneNodeSpec? childSpec, string commandName)
        {
            if (childSpec == null)
            {
                throw new CommandFailureException("SCENE_SPEC_INVALID", commandName + " node spec이 비어 있습니다.");
            }

            string childName = InspectorUtility.RequireNodeName(childSpec.Name, commandName, "SCENE");
            GameObject child = CreateSceneObject(childName, childSpec.Primitive, commandName);
            SceneManager.MoveGameObjectToScene(child, scene);
            if (parent != null)
            {
                child.transform.SetParent(parent, false);
            }

            SceneInspector.ApplyNodeState(child, childSpec, childName, allowMissingName: false);
            AddComponents(child, childSpec.Components, commandName);
            AddNodes(scene, child.transform, childSpec.Children, commandName);
            return child;
        }

        private static void AddComponents(GameObject target, SceneComponentSpec[]? components, string commandName)
        {
            if (components == null)
            {
                return;
            }

            foreach (SceneComponentSpec componentSpec in components)
            {
                AddComponent(target, componentSpec, commandName);
            }
        }

        private static Component AddComponent(GameObject target, SceneComponentSpec? componentSpec, string commandName)
        {
            if (componentSpec == null || string.IsNullOrWhiteSpace(componentSpec.Type))
            {
                throw new CommandFailureException("SCENE_COMPONENT_INVALID", commandName + " component type이 비어 있습니다.");
            }

            Type componentType = ResolveComponentType(componentSpec.Type, commandName);
            if (componentType == typeof(Transform))
            {
                throw new CommandFailureException("SCENE_COMPONENT_INVALID", "Transform은 직접 추가할 수 없습니다.");
            }

            Component component;
            try
            {
                component = target.AddComponent(componentType);
            }
            catch (Exception exception)
            {
                throw new CommandFailureException("SCENE_COMPONENT_INVALID", "component를 추가하지 못했습니다: " + componentSpec.Type, exception.Message);
            }

            if (componentSpec.Values != null)
            {
                SerializedValueApplier.Apply(component, componentSpec.Values);
            }

            return component;
        }

        private static GameObject CreateSceneObject(string name, string? primitive, string commandName)
        {
            if (string.IsNullOrWhiteSpace(primitive))
            {
                return new GameObject(name);
            }

            string normalizedPrimitive = ProtocolConstants.NormalizeScenePrimitive(primitive);
            if (string.IsNullOrWhiteSpace(normalizedPrimitive))
            {
                throw new CommandFailureException(
                    "SCENE_SPEC_INVALID",
                    commandName + " primitive type을 지원하지 않습니다: " + primitive + " (지원: " + string.Join(", ", ProtocolConstants.SupportedScenePrimitiveNames) + ")");
            }

            GameObject gameObject = GameObject.CreatePrimitive(ResolvePrimitiveType(normalizedPrimitive));
            gameObject.name = name;
            return gameObject;
        }

        private static PrimitiveType ResolvePrimitiveType(string normalizedPrimitive)
        {
            switch (normalizedPrimitive)
            {
                case "Cube":
                    return PrimitiveType.Cube;
                case "Sphere":
                    return PrimitiveType.Sphere;
                case "Capsule":
                    return PrimitiveType.Capsule;
                case "Cylinder":
                    return PrimitiveType.Cylinder;
                case "Plane":
                    return PrimitiveType.Plane;
                case "Quad":
                    return PrimitiveType.Quad;
                default:
                    throw new InvalidOperationException("지원하지 않는 primitive type입니다: " + normalizedPrimitive);
            }
        }

        private static Component ResolveComponent(GameObject target, string? componentTypeName, int? componentIndex, string commandName)
        {
            Type componentType = ResolveComponentType(componentTypeName, commandName);
            Component[] matches = target.GetComponents<Component>()
                .Where(component => component != null && componentType.IsAssignableFrom(component.GetType()))
                .ToArray();
            if (matches.Length == 0)
            {
                throw new CommandFailureException("SCENE_COMPONENT_NOT_FOUND", commandName + " 대상 component를 찾지 못했습니다: " + componentTypeName);
            }

            int index = componentIndex ?? 0;
            if (index < 0 || index >= matches.Length)
            {
                throw new CommandFailureException("SCENE_COMPONENT_NOT_FOUND", commandName + " component index가 범위를 벗어났습니다: " + index);
            }

            return matches[index];
        }

        private static Type ResolveComponentType(string? typeName, string commandName)
        {
            string normalized = string.IsNullOrWhiteSpace(typeName) ? string.Empty : typeName.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new CommandFailureException("SCENE_COMPONENT_INVALID", commandName + " component type이 비어 있습니다.");
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
                throw new CommandFailureException("SCENE_COMPONENT_INVALID", "동일한 full name의 component 타입이 여러 개 있습니다: " + normalized);
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
                throw new CommandFailureException("SCENE_COMPONENT_INVALID", "짧은 이름이 같은 component 타입이 여러 개 있습니다. full name을 사용하세요: " + normalized);
            }

            throw new CommandFailureException("SCENE_COMPONENT_INVALID", "component 타입을 찾지 못했습니다: " + normalized);
        }

        private sealed class ScenePatchApplyResult
        {
            internal string? CreatedPath { get; set; }

            internal bool Patched { get; set; }

            internal List<string> Warnings { get; } = new List<string>();
        }
    }
}
