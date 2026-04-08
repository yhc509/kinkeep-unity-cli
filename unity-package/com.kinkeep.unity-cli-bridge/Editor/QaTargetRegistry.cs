#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using KinKeep.UnityCli.Bridge;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KinKeep.UnityCli.Bridge.Editor
{
    /// <summary>
    /// Scans loaded scenes for fields marked with [QaTarget] and caches the resolved GameObject targets.
    /// </summary>
    internal sealed class QaTargetRegistry
    {
        private static readonly Dictionary<string, GameObject> QaIdCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, GameObject> PathCache = new(StringComparer.Ordinal);
        private static bool _isSubscribed;
        private static bool _isDirty = true;

        private QaTargetRegistry()
        {
        }

        public static void EnsureSubscribed()
        {
            if (_isSubscribed)
            {
                return;
            }

            _isSubscribed = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static bool TryResolve(string qaId, out GameObject? target)
        {
            target = null;
            if (string.IsNullOrWhiteSpace(qaId))
            {
                return false;
            }

            EnsureSubscribed();
            EnsureCacheCurrent();
            return TryGetCachedTarget(QaIdCache, qaId, out target);
        }

        public static bool TryResolvePath(string path, out GameObject? target)
        {
            target = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            EnsureSubscribed();
            EnsureCacheCurrent();
            return TryGetCachedTarget(PathCache, NormalizePath(path), out target);
        }

        public static void Rebuild()
        {
            if (!_isDirty)
            {
                return;
            }

            QaIdCache.Clear();
            PathCache.Clear();

#if UNITY_2022_2_OR_NEWER
            Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
#else
            Transform[] transforms = UnityEngine.Object.FindObjectsOfType<Transform>();
#endif
            foreach (Transform transform in transforms)
            {
                if (transform == null || transform.parent != null || !transform.gameObject.activeInHierarchy)
                {
                    continue;
                }

                CacheHierarchy(transform, string.Empty);
            }

#if UNITY_2022_2_OR_NEWER
            MonoBehaviour[] monoBehaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
#else
            MonoBehaviour[] monoBehaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
#endif
            foreach (MonoBehaviour monoBehaviour in monoBehaviours)
            {
                if (monoBehaviour == null || !monoBehaviour.gameObject.activeInHierarchy)
                {
                    continue;
                }

                ScanFields(monoBehaviour);
            }

            _isDirty = false;
        }

        private static void ScanFields(MonoBehaviour monoBehaviour)
        {
            Type? type = monoBehaviour.GetType();
            while (type != null && type != typeof(MonoBehaviour) && type != typeof(Behaviour) && type != typeof(Component))
            {
                FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                foreach (FieldInfo field in fields)
                {
                    QaTargetAttribute? attribute = field.GetCustomAttribute<QaTargetAttribute>();
                    if (attribute == null || string.IsNullOrWhiteSpace(attribute.Id))
                    {
                        continue;
                    }

                    object? value = field.GetValue(monoBehaviour);
                    GameObject? gameObject = value switch
                    {
                        GameObject go => go,
                        Component component => component != null ? component.gameObject : null,
                        _ => null,
                    };

                    if (gameObject == null)
                    {
                        continue;
                    }

                    if (QaIdCache.ContainsKey(attribute.Id))
                    {
                        Debug.LogWarning($"[QaTargetRegistry] Duplicate QA ID '{attribute.Id}' found on '{gameObject.name}'. Keeping the first match.");
                        continue;
                    }

                    QaIdCache[attribute.Id] = gameObject;
                }

                type = type.BaseType;
            }
        }

        private static void EnsureCacheCurrent()
        {
            if (_isDirty)
            {
                Rebuild();
            }
        }

        private static bool TryGetCachedTarget(
            Dictionary<string, GameObject> cache,
            string key,
            out GameObject? target)
        {
            target = null;
            if (!cache.TryGetValue(key, out GameObject cached) || cached == null || !cached.activeInHierarchy)
            {
                return false;
            }

            target = cached;
            return true;
        }

        private static void CacheHierarchy(Transform transform, string parentPath)
        {
            string currentPath = string.IsNullOrEmpty(parentPath)
                ? "/" + transform.name
                : parentPath + "/" + transform.name;

            if (!PathCache.ContainsKey(currentPath))
            {
                PathCache[currentPath] = transform.gameObject;
            }

            int childCount = transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == null || !child.gameObject.activeInHierarchy)
                {
                    continue;
                }

                CacheHierarchy(child, currentPath);
            }
        }

        private static string NormalizePath(string path)
        {
            string normalized = path.Replace('\\', '/').Trim();
            normalized = normalized.TrimEnd('/');
            if (normalized.Length == 0)
            {
                return "/";
            }

            return normalized.StartsWith("/", StringComparison.Ordinal)
                ? normalized
                : "/" + normalized.TrimStart('/');
        }

        private static void Invalidate()
        {
            _isDirty = true;
            QaIdCache.Clear();
            PathCache.Clear();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Invalidate();
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            Invalidate();
        }

        private static void OnHierarchyChanged()
        {
            Invalidate();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            Invalidate();
        }
    }
}
