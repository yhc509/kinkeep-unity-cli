#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace KinKeep.UnityCli.Bridge.Editor
{
    internal static class ComponentOperations
    {
        internal sealed class ComponentEntry
        {
            public string type { get; set; } = string.Empty;
            public int index { get; set; }
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

                string typeName = component.GetType().Name;
                if (!typeCounts.TryGetValue(typeName, out int count))
                {
                    count = 0;
                }

                entries.Add(new ComponentEntry
                {
                    type = typeName,
                    index = count,
                });
                typeCounts[typeName] = count + 1;
            }

            return entries;
        }
    }
}
