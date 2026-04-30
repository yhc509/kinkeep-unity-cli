using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityCli.Protocol;
using UnityEditor;
using UnityEngine;

namespace KinKeep.UnityCli.Bridge.Editor
{
    internal static partial class BuiltInAssetCreateProviders
    {
        private sealed class AnimatorOverrideControllerAssetCreateProvider : IAssetCreateProvider
        {
            public AssetCreateTypeDescriptor Describe()
            {
                return BuiltInAssetCreateCatalog.GetDescriptor("animator-override-controller");
            }

            public AssetCreateArtifact Create(AssetCreateRequest request)
            {
                AnimatorOverrideControllerCreateOptions options = request.GetOptions<AnimatorOverrideControllerCreateOptions>();
                if (string.IsNullOrWhiteSpace(options.BaseController))
                {
                    throw new CommandFailureException("ASSET_OPTION_INVALID", "`--base-controller`가 필요합니다.");
                }

                string baseControllerPath = AssetCommandSupport.RequireExistingAssetPath(options.BaseController, "asset-create");
                RuntimeAnimatorController baseController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(baseControllerPath);
                if (baseController == null)
                {
                    throw new CommandFailureException("ASSET_OPTION_INVALID", "유효한 RuntimeAnimatorController가 아닙니다: " + baseControllerPath);
                }

                var asset = new AnimatorOverrideController
                {
                    name = Path.GetFileNameWithoutExtension(request.AssetPath),
                    runtimeAnimatorController = baseController,
                };

                return new AssetCreateArtifact(
                    delegate { AssetDatabase.CreateAsset(asset, request.AssetPath); },
                    typeof(AnimatorOverrideController));
            }
        }

        private sealed class InputActionsAssetCreateProvider : IAssetCreateProvider
        {
            public AssetCreateTypeDescriptor Describe()
            {
                return BuiltInAssetCreateCatalog.GetDescriptor("input-actions");
            }

            public AssetCreateArtifact Create(AssetCreateRequest request)
            {
                InputActionsCreateOptions options = request.GetOptions<InputActionsCreateOptions>();
                Type assetType = RequireType(
                    "UnityEngine.InputSystem.InputActionAsset",
                    "ASSET_DEPENDENCY_MISSING",
                    "Input System 패키지가 없어서 InputActionAsset을 만들 수 없습니다.");

                ScriptableObject asset = ScriptableObject.CreateInstance(assetType);
                if (asset == null)
                {
                    throw new CommandFailureException("ASSET_CREATE_FAILED", "InputActionAsset 인스턴스를 만들지 못했습니다.");
                }

                asset.name = Path.GetFileNameWithoutExtension(request.AssetPath);

                if (!string.IsNullOrWhiteSpace(options.InitialMap))
                {
                    Type extensionsType = RequireType(
                        "UnityEngine.InputSystem.InputActionSetupExtensions",
                        "ASSET_DEPENDENCY_MISSING",
                        "Input System setup API를 찾지 못했습니다.");

                    MethodInfo addActionMap = null;
                    MethodInfo[] methods = extensionsType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                    for (int methodIndex = 0; methodIndex < methods.Length; methodIndex++)
                    {
                        MethodInfo method = methods[methodIndex];
                        if (method.Name != "AddActionMap")
                        {
                            continue;
                        }

                        ParameterInfo[] parameters = method.GetParameters();
                        if (parameters.Length == 2
                            && parameters[0].ParameterType == assetType
                            && parameters[1].ParameterType == typeof(string))
                        {
                            addActionMap = method;
                            break;
                        }
                    }

                    if (addActionMap == null)
                    {
                        throw new CommandFailureException("ASSET_DEPENDENCY_MISSING", "InputActionAsset API를 찾지 못했습니다.");
                    }

                    addActionMap.Invoke(null, new object[] { asset, options.InitialMap.Trim() });
                }

                return new AssetCreateArtifact(
                    delegate { SaveInputActionsAsset(request.AssetPath, asset); },
                    assetType);
            }
        }

        private sealed class VolumeProfileAssetCreateProvider : IAssetCreateProvider
        {
            public AssetCreateTypeDescriptor Describe()
            {
                return BuiltInAssetCreateCatalog.GetDescriptor("volume-profile");
            }

            public AssetCreateArtifact Create(AssetCreateRequest request)
            {
                Type assetType = RequireType(
                    "UnityEngine.Rendering.VolumeProfile",
                    "ASSET_DEPENDENCY_MISSING",
                    "Render Pipelines Core 패키지가 없어서 VolumeProfile을 만들 수 없습니다.");

                ScriptableObject asset = ScriptableObject.CreateInstance(assetType);
                if (asset == null)
                {
                    throw new CommandFailureException("ASSET_CREATE_FAILED", "VolumeProfile 인스턴스를 만들지 못했습니다.");
                }

                asset.name = Path.GetFileNameWithoutExtension(request.AssetPath);
                return new AssetCreateArtifact(
                    delegate { AssetDatabase.CreateAsset(asset, request.AssetPath); },
                    assetType);
            }
        }

        private sealed class ScriptableObjectAssetCreateProvider : IAssetCreateProvider
        {
            public AssetCreateTypeDescriptor Describe()
            {
                return BuiltInAssetCreateCatalog.GetDescriptor("scriptable-object");
            }

            public AssetCreateArtifact Create(AssetCreateRequest request)
            {
                Type scriptType = ResolveScriptableObjectType(request.ScriptPath, request.TypeName);
                ScriptableObject asset = ScriptableObject.CreateInstance(scriptType);
                if (asset == null)
                {
                    throw new CommandFailureException("ASSET_CREATE_FAILED", "ScriptableObject 인스턴스를 만들지 못했습니다: " + scriptType.FullName);
                }

                asset.name = Path.GetFileNameWithoutExtension(request.AssetPath);
                return new AssetCreateArtifact(
                    delegate { AssetDatabase.CreateAsset(asset, request.AssetPath); },
                    scriptType,
                    asset);
            }
        }

        private static void SaveInputActionsAsset(string assetPath, ScriptableObject inputActionsAsset)
        {
            MethodInfo toJson = inputActionsAsset.GetType().GetMethod("ToJson", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (toJson == null || toJson.ReturnType != typeof(string))
            {
                throw new CommandFailureException("ASSET_DEPENDENCY_MISSING", "InputActionAsset JSON 저장 API를 찾지 못했습니다.");
            }

            string json = toJson.Invoke(inputActionsAsset, null) as string;
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new CommandFailureException("ASSET_CREATE_FAILED", "InputActionAsset JSON을 만들지 못했습니다.");
            }

            string physicalPath = AssetCommandSupport.GetPhysicalPath(assetPath);

            try
            {
                File.WriteAllText(physicalPath, json);
            }
            catch (Exception exception)
            {
                throw new CommandFailureException(
                    "ASSET_CREATE_FAILED",
                    "InputActionAsset 파일을 쓰지 못했습니다: " + assetPath,
                    exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inputActionsAsset);
            }
        }

        private static Type ResolveScriptableObjectType(string scriptPath, string typeName)
        {
            bool hasScriptPath = !string.IsNullOrWhiteSpace(scriptPath);
            bool hasTypeName = !string.IsNullOrWhiteSpace(typeName);
            if (!hasScriptPath && !hasTypeName)
            {
                throw new CommandFailureException("ASSET_TYPE_NOT_FOUND", "`--script` 또는 `--type-name`이 필요합니다.");
            }

            Type typeFromName = hasTypeName ? ResolveTypeByName(typeName.Trim()) : null;
            Type typeFromScript = hasScriptPath ? ResolveTypeFromScript(scriptPath.Trim()) : null;

            Type resolvedType;
            if (typeFromName != null && typeFromScript != null)
            {
                if (typeFromName != typeFromScript && !IsNestedWithin(typeFromName, typeFromScript))
                {
                    throw new CommandFailureException("ASSET_TYPE_NOT_FOUND", "`--script`와 `--type-name`이 같은 타입을 가리키지 않습니다.");
                }

                resolvedType = typeFromName;
            }
            else
            {
                resolvedType = typeFromName ?? typeFromScript;
            }

            if (resolvedType == null)
            {
                throw new CommandFailureException("ASSET_TYPE_NOT_FOUND", "ScriptableObject 타입을 찾지 못했습니다.");
            }

            if (!IsValidScriptableObjectType(resolvedType))
            {
                throw new CommandFailureException("ASSET_SCRIPT_INVALID", "유효한 ScriptableObject 타입이 아닙니다: " + resolvedType.FullName);
            }

            return resolvedType;
        }

        private static Type ResolveTypeFromScript(string scriptPath)
        {
            string normalizedScriptPath = AssetCommandSupport.NormalizeAssetPath(scriptPath);
            MonoScript monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(normalizedScriptPath);
            if (monoScript == null)
            {
                throw new CommandFailureException("ASSET_SCRIPT_NOT_FOUND", "script를 찾지 못했습니다: " + normalizedScriptPath);
            }

            Type scriptType = monoScript.GetClass();
            if (scriptType == null)
            {
                throw new CommandFailureException("ASSET_SCRIPT_INVALID", "스크립트 타입을 확인하지 못했습니다: " + normalizedScriptPath);
            }

            return scriptType;
        }

        private static Type ResolveTypeByName(string typeName)
        {
            Type exactMatch = FindSingleScriptableObjectType(
                TypeDiscoveryUtility.FindTypesByFullName(typeName),
                "ASSET_TYPE_AMBIGUOUS",
                "동일한 full name의 타입이 여러 개 있습니다: " + typeName);
            if (exactMatch != null)
            {
                return exactMatch;
            }

            Type shortMatch = FindSingleScriptableObjectType(
                TypeDiscoveryUtility.FindTypesByShortName(typeName),
                "ASSET_TYPE_AMBIGUOUS",
                "짧은 이름이 같은 타입이 여러 개 있습니다. full name을 사용하세요: " + typeName);
            if (shortMatch != null)
            {
                return shortMatch;
            }

            throw new CommandFailureException("ASSET_TYPE_NOT_FOUND", "타입을 찾지 못했습니다: " + typeName);
        }

        private static Type FindSingleScriptableObjectType(
            IReadOnlyList<Type> candidates,
            string errorCode,
            string ambiguousMessage)
        {
            Type match = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                Type candidate = candidates[i];
                if (!IsValidScriptableObjectType(candidate))
                {
                    continue;
                }

                if (match != null)
                {
                    throw new CommandFailureException(errorCode, ambiguousMessage);
                }

                match = candidate;
            }

            return match;
        }

        private static bool IsValidScriptableObjectType(Type candidate)
        {
            return typeof(ScriptableObject).IsAssignableFrom(candidate)
                && !candidate.IsAbstract
                && !candidate.ContainsGenericParameters;
        }

        private static bool IsNestedWithin(Type type, Type outerType)
        {
            Type current = type;
            while (current != null)
            {
                if (current == outerType)
                {
                    return true;
                }

                current = current.DeclaringType;
            }

            return false;
        }

        private static Type RequireType(string fullName, string errorCode, string message)
        {
            IReadOnlyList<Type> matches = TypeDiscoveryUtility.FindTypesByFullName(fullName);
            if (matches.Count == 0)
            {
                throw new CommandFailureException(errorCode, message);
            }

            return matches[0];
        }

        [Serializable]
        private sealed class InputActionsCreateOptions
        {
            public string InitialMap { get; set; } = string.Empty;
        }

        [Serializable]
        private sealed class AnimatorOverrideControllerCreateOptions
        {
            public string BaseController { get; set; } = string.Empty;
        }
    }
}
