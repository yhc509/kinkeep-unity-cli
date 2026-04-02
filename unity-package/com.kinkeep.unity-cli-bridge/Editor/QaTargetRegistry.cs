#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using KinKeep.UnityCli.Bridge;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KinKeep.UnityCli.Bridge.Editor
{
    /// <summary>
    /// Scans loaded scenes for fields marked with [QaTarget] and caches the resolved GameObject targets.
    /// </summary>
    internal sealed class QaTargetRegistry
    {
        private static readonly Dictionary<string, GameObject> Cache = new(StringComparer.OrdinalIgnoreCase);
        private static bool _isSubscribed;

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
        }

        public static bool TryResolve(string qaId, out GameObject? target)
        {
            target = null;
            if (string.IsNullOrWhiteSpace(qaId))
            {
                return false;
            }

            EnsureSubscribed();

            if (Cache.Count == 0)
            {
                Rebuild();
            }

            if (Cache.TryGetValue(qaId, out GameObject cached) && cached != null)
            {
                target = cached;
                return true;
            }

            Rebuild();
            if (Cache.TryGetValue(qaId, out cached) && cached != null)
            {
                target = cached;
                return true;
            }

            return false;
        }

        public static void Rebuild()
        {
            Cache.Clear();

            MonoBehaviour[] monoBehaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (MonoBehaviour monoBehaviour in monoBehaviours)
            {
                if (monoBehaviour == null)
                {
                    continue;
                }

                ScanFields(monoBehaviour);
            }
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

                    if (Cache.ContainsKey(attribute.Id))
                    {
                        Debug.LogWarning($"[QaTargetRegistry] Duplicate QA ID '{attribute.Id}' found on '{gameObject.name}'. Keeping the first match.");
                        continue;
                    }

                    Cache[attribute.Id] = gameObject;
                }

                type = type.BaseType;
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Rebuild();
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            Rebuild();
        }
    }
}
