using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityCli.Protocol;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PUC.Editor
{
    internal static class BuiltInAssetCreateProviders
    {
        public static IAssetCreateProvider[] CreateAll()
        {
            return new IAssetCreateProvider[]
            {
                new MaterialAssetCreateProvider(),
                new PhysicsMaterialAssetCreateProvider(),
                new PhysicsMaterial2DAssetCreateProvider(),
                new AnimatorControllerAssetCreateProvider(),
                new AnimatorOverrideControllerAssetCreateProvider(),
                new AnimationClipAssetCreateProvider(),
                new InputActionsAssetCreateProvider(),
                new SceneAssetCreateProvider(),
                new PrefabAssetCreateProvider(),
                new RenderTextureAssetCreateProvider(),
                new AvatarMaskAssetCreateProvider(),
                new VolumeProfileAssetCreateProvider(),
                new ScriptableObjectAssetCreateProvider(),
            };
        }

        private sealed class MaterialAssetCreateProvider : IAssetCreateProvider
        {
            public AssetCreateTypeDescriptor Describe()
            {
                return BuiltInAssetCreateCatalog.GetDescriptor("material");
            }

            public AssetCreateArtifact Create(AssetCreateRequest request)
            {
                MaterialCreateOptions options = request.GetOptions<MaterialCreateOptions>();
                Shader shader = ResolveShader(options.shader);
                var material = new Material(shader)
                {
                    name = Path.GetFileNameWithoutExtension(request.AssetPath),
                };

                return new AssetCreateArtifact(
                    delegate { AssetDatabase.CreateAsset(material, request.AssetPath); },
                    typeof(Material));
            }
        }

        private sealed class PhysicsMaterialAssetCreateProvider : IAssetCreateProvider
        {
            public AssetCreateTypeDescriptor Describe()
            {
                return BuiltInAssetCreateCatalog.GetDescriptor("physics-material");
            }

            public AssetCreateArtifact Create(AssetCreateRequest request)
            {
                var asset = new PhysicsMaterial(Path.GetFileNameWithoutExtension(request.AssetPath));
                return new AssetCreateArtifact(
                    delegate { AssetDatabase.CreateAsset(asset, request.AssetPath); },
                    typeof(PhysicsMaterial));
            }
        }

        private sealed class PhysicsMaterial2DAssetCreateProvider : IAssetCreateProvider
        {
            public AssetCreateTypeDescriptor Describe()
            {
                return BuiltInAssetCreateCatalog.GetDescriptor("physics-material-2d");
            }

            public AssetCreateArtifact Create(AssetCreateRequest request)
            {
                var asset = new PhysicsMaterial2D(Path.GetFileNameWithoutExtension(request.AssetPath));
                return new AssetCreateArtifact(
                    delegate { AssetDatabase.CreateAsset(asset, request.AssetPath); },
                    typeof(PhysicsMaterial2D));
            }
        }

        private sealed class AnimatorControllerAssetCreateProvider : IAssetCreateProvider
        {
            public AssetCreateTypeDescriptor Describe()
            {
                return BuiltInAssetCreateCatalog.GetDescriptor("animator-controller");
            }

            public AssetCreateArtifact Create(AssetCreateRequest request)
            {
                return new AssetCreateArtifact(
                    delegate { AnimatorController.CreateAnimatorControllerAtPath(request.AssetPath); },
                    typeof(AnimatorController));
            }
        }

        private sealed class AnimatorOverrideControllerAssetCreateProvider : IAssetCreateProvider
        {
            public AssetCreateTypeDescriptor Describe()
            {
                return BuiltInAssetCreateCatalog.GetDescriptor("animator-override-controller");
            }

            public AssetCreateArtifact Create(AssetCreateRequest request)
            {
                AnimatorOverrideControllerCreateOptions options = request.GetOptions<AnimatorOverrideControllerCreateOptions>();
                if (string.IsNullOrWhiteSpace(options.baseController))
                {
                    throw new CommandFailureException("ASSET_OPTION_INVALID", "`--base-controller`가 필요합니다.");
                }

                string baseControllerPath = AssetCommandSupport.RequireExistingAssetPath(options.baseController, "asset-create");
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

        private sealed class AnimationClipAssetCreateProvider : IAssetCreateProvider
        {
            public AssetCreateTypeDescriptor Describe()
            {
                return BuiltInAssetCreateCatalog.GetDescriptor("animation-clip");
            }

            public AssetCreateArtifact Create(AssetCreateRequest request)
            {
                AnimationClipCreateOptions options = request.GetOptions<AnimationClipCreateOptions>();
                var clip = new AnimationClip
                {
                    name = Path.GetFileNameWithoutExtension(request.AssetPath),
                    legacy = options.legacy,
                };

                return new AssetCreateArtifact(
                    delegate { AssetDatabase.CreateAsset(clip, request.AssetPath); },
                    typeof(AnimationClip));
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

                if (!string.IsNullOrWhiteSpace(options.initialMap))
                {
                    Type extensionsType = RequireType(
                        "UnityEngine.InputSystem.InputActionSetupExtensions",
                        "ASSET_DEPENDENCY_MISSING",
                        "Input System setup API를 찾지 못했습니다.");

                    MethodInfo addActionMap = extensionsType
                        .GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(method =>
                        {
                            if (method.Name != "AddActionMap")
                            {
                                return false;
                            }

                            ParameterInfo[] parameters = method.GetParameters();
                            return parameters.Length == 2
                                && parameters[0].ParameterType == assetType
                                && parameters[1].ParameterType == typeof(string);
                        });

                    if (addActionMap == null)
                    {
                        throw new CommandFailureException("ASSET_DEPENDENCY_MISSING", "InputActionAsset API를 찾지 못했습니다.");
                    }

                    addActionMap.Invoke(null, new object[] { asset, options.initialMap.Trim() });
                }

                return new AssetCreateArtifact(
                    delegate { SaveInputActionsAsset(request.AssetPath, asset); },
                    assetType);
            }
        }

        private sealed class SceneAssetCreateProvider : IAssetCreateProvider
        {
            public AssetCreateTypeDescriptor Describe()
            {
                return BuiltInAssetCreateCatalog.GetDescriptor("scene");
            }

            public AssetCreateArtifact Create(AssetCreateRequest request)
            {
                return new AssetCreateArtifact(
                    delegate
                    {
                        Scene activeScene = SceneManager.GetActiveScene();
                        bool canCreateSingle = SceneManager.sceneCount == 1
                            && string.IsNullOrWhiteSpace(activeScene.path)
                            && !activeScene.isDirty;
                        Scene scene = EditorSceneManager.NewScene(
                            NewSceneSetup.EmptyScene,
                            canCreateSingle ? NewSceneMode.Single : NewSceneMode.Additive);
                        try
                        {
                            if (!EditorSceneManager.SaveScene(scene, request.AssetPath, false))
                            {
                                throw new CommandFailureException("ASSET_CREATE_FAILED", "scene를 저장하지 못했습니다: " + request.AssetPath);
                            }
                        }
                        finally
                        {
                            if (!canCreateSingle)
                            {
                                EditorSceneManager.CloseScene(scene, true);
                            }
                        }
                    },
                    typeof(SceneAsset));
            }
        }

        private sealed class PrefabAssetCreateProvider : IAssetCreateProvider
        {
            public AssetCreateTypeDescriptor Describe()
            {
                return BuiltInAssetCreateCatalog.GetDescriptor("prefab");
            }

            public AssetCreateArtifact Create(AssetCreateRequest request)
            {
                PrefabCreateOptions options = request.GetOptions<PrefabCreateOptions>();
                string rootName = string.IsNullOrWhiteSpace(options.rootName)
                    ? Path.GetFileNameWithoutExtension(request.AssetPath)
                    : options.rootName.Trim();

                if (string.IsNullOrWhiteSpace(rootName))
                {
                    throw new CommandFailureException("ASSET_OPTION_INVALID", "prefab root 이름이 비어 있습니다.");
                }

                return new AssetCreateArtifact(
                    delegate
                    {
                        var root = new GameObject(rootName);
                        try
                        {
                            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, request.AssetPath);
                            if (saved == null)
                            {
                                throw new CommandFailureException("ASSET_CREATE_FAILED", "prefab을 저장하지 못했습니다: " + request.AssetPath);
                            }
                        }
                        finally
                        {
                            UnityEngine.Object.DestroyImmediate(root);
                        }
                    },
                    typeof(GameObject));
            }
        }

        private sealed class RenderTextureAssetCreateProvider : IAssetCreateProvider
        {
            public AssetCreateTypeDescriptor Describe()
            {
                return BuiltInAssetCreateCatalog.GetDescriptor("render-texture");
            }

            public AssetCreateArtifact Create(AssetCreateRequest request)
            {
                RenderTextureCreateOptions options = request.GetOptions<RenderTextureCreateOptions>();
                int width = options.width <= 0 ? 1024 : options.width;
                int height = options.height <= 0 ? 1024 : options.height;
                int depth = options.depth < 0 ? 24 : options.depth;

                var asset = new RenderTexture(width, height, depth)
                {
                    name = Path.GetFileNameWithoutExtension(request.AssetPath),
                };

                return new AssetCreateArtifact(
                    delegate { AssetDatabase.CreateAsset(asset, request.AssetPath); },
                    typeof(RenderTexture));
            }
        }

        private sealed class AvatarMaskAssetCreateProvider : IAssetCreateProvider
        {
            public AssetCreateTypeDescriptor Describe()
            {
                return BuiltInAssetCreateCatalog.GetDescriptor("avatar-mask");
            }

            public AssetCreateArtifact Create(AssetCreateRequest request)
            {
                var asset = new AvatarMask
                {
                    name = Path.GetFileNameWithoutExtension(request.AssetPath),
                };

                return new AssetCreateArtifact(
                    delegate { AssetDatabase.CreateAsset(asset, request.AssetPath); },
                    typeof(AvatarMask));
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

        private static Shader ResolveShader(string shaderPath)
        {
            if (!string.IsNullOrWhiteSpace(shaderPath))
            {
                Shader shader = Shader.Find(shaderPath.Trim());
                if (shader == null)
                {
                    throw new CommandFailureException("ASSET_OPTION_INVALID", "shader를 찾지 못했습니다: " + shaderPath.Trim());
                }

                return shader;
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit != null)
            {
                return urpLit;
            }

            Shader standard = Shader.Find("Standard");
            if (standard != null)
            {
                return standard;
            }

            throw new CommandFailureException("ASSET_DEPENDENCY_MISSING", "기본 material shader를 찾지 못했습니다.");
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
            List<Type> exactMatches = FindTypes(candidate =>
                IsValidScriptableObjectType(candidate)
                && string.Equals(candidate.FullName, typeName, StringComparison.Ordinal));
            if (exactMatches.Count == 1)
            {
                return exactMatches[0];
            }

            if (exactMatches.Count > 1)
            {
                throw new CommandFailureException("ASSET_TYPE_AMBIGUOUS", "동일한 full name의 타입이 여러 개 있습니다: " + typeName);
            }

            List<Type> shortMatches = FindTypes(candidate =>
                IsValidScriptableObjectType(candidate)
                && string.Equals(candidate.Name, typeName, StringComparison.Ordinal));
            if (shortMatches.Count == 1)
            {
                return shortMatches[0];
            }

            if (shortMatches.Count > 1)
            {
                throw new CommandFailureException("ASSET_TYPE_AMBIGUOUS", "짧은 이름이 같은 타입이 여러 개 있습니다. full name을 사용하세요: " + typeName);
            }

            throw new CommandFailureException("ASSET_TYPE_NOT_FOUND", "타입을 찾지 못했습니다: " + typeName);
        }

        private static bool IsValidScriptableObjectType(Type candidate)
        {
            return typeof(ScriptableObject).IsAssignableFrom(candidate)
                && !candidate.IsAbstract
                && !candidate.ContainsGenericParameters;
        }

        private static List<Type> FindTypes(Func<Type, bool> predicate)
        {
            var results = new List<Type>();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(item => item != null).ToArray();
                }

                foreach (Type type in types)
                {
                    if (type != null && predicate(type))
                    {
                        results.Add(type);
                    }
                }
            }

            return results;
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
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);

            if (type == null)
            {
                throw new CommandFailureException(errorCode, message);
            }

            return type;
        }

        [Serializable]
        private sealed class MaterialCreateOptions
        {
            public string shader = string.Empty;
        }

        [Serializable]
        private sealed class AnimationClipCreateOptions
        {
            public bool legacy;
        }

        [Serializable]
        private sealed class InputActionsCreateOptions
        {
            public string initialMap = string.Empty;
        }

        [Serializable]
        private sealed class PrefabCreateOptions
        {
            public string rootName = string.Empty;
        }

        [Serializable]
        private sealed class RenderTextureCreateOptions
        {
            public int width;
            public int height;
            public int depth = 24;
        }

        [Serializable]
        private sealed class AnimatorOverrideControllerCreateOptions
        {
            public string baseController = string.Empty;
        }
    }
}
