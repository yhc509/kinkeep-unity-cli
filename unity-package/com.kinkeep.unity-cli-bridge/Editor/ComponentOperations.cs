#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace KinKeep.UnityCli.Bridge.Editor
{
    internal static class ComponentOperations
    {
        [Serializable]
        internal sealed class ComponentEntry
        {
            [SerializeField] private string type = string.Empty;
            [SerializeField] private int index;

            public string Type
            {
                get => type;
                set => type = value;
            }

            public int Index
            {
                get => index;
                set => index = value;
            }
        }

        internal static List<ComponentEntry> ListComponents(GameObject gameObject)
        {
            if (gameObject == null)
                throw new ArgumentNullException(nameof(gameObject));

            var components = gameObject.GetComponents<Component>();
            var typeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var entries = new List<ComponentEntry>(components.Length);

            foreach (var component in components)
            {
                if (component == null)
                {
                    continue;
                }

                string typeName = component.GetType().FullName ?? component.GetType().Name;
                if (!typeCounts.TryGetValue(typeName, out int count))
                {
                    count = 0;
                }

                entries.Add(new ComponentEntry
                {
                    Type = typeName,
                    Index = count,
                });
                typeCounts[typeName] = count + 1;
            }

            return entries;
        }
    }
}
