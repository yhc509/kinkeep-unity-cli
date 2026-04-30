#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityCliBridge.Bridge.Editor
{
    internal static class ComponentOperations
    {
        // Reused to avoid GC allocations. Safe because all callers run on the
        // main thread and no method using these buffers calls another that also uses them.
        private static readonly List<Component> _componentBuffer = new List<Component>(8);
        private static readonly Dictionary<Type, int> _typeCounts = new Dictionary<Type, int>();

        [Serializable]
        internal struct ComponentEntry
        {
            [SerializeField] private string type;
            [SerializeField] private int index;

            public ComponentEntry(string type, int index)
            {
                this.type = type ?? string.Empty;
                this.index = index;
            }

            public string Type
            {
                get => type ?? string.Empty;
                set => type = value ?? string.Empty;
            }

            public int Index
            {
                get => index;
                set => index = value;
            }
        }

        internal static ComponentEntry[] ListComponentEntries(GameObject gameObject)
        {
            if (gameObject == null)
            {
                throw new ArgumentNullException(nameof(gameObject));
            }

            _componentBuffer.Clear();
            _typeCounts.Clear();
            gameObject.GetComponents(_componentBuffer);

            int entryCount = CountNonNullComponents(_componentBuffer);
            if (entryCount == 0)
            {
                _componentBuffer.Clear();
                return Array.Empty<ComponentEntry>();
            }

            var entries = new ComponentEntry[entryCount];
            int entryIndex = 0;
            for (int componentIndex = 0; componentIndex < _componentBuffer.Count; componentIndex++)
            {
                Component component = _componentBuffer[componentIndex];
                if (component == null)
                {
                    continue;
                }

                Type componentType = component.GetType();
                if (!_typeCounts.TryGetValue(componentType, out int count))
                {
                    count = 0;
                }

                string typeName = componentType.FullName ?? componentType.Name;
                entries[entryIndex] = new ComponentEntry(typeName, count);
                _typeCounts[componentType] = count + 1;
                entryIndex++;
            }

            _typeCounts.Clear();
            _componentBuffer.Clear();
            return entries;
        }

        private static int CountNonNullComponents(List<Component> components)
        {
            int count = 0;
            for (int index = 0; index < components.Count; index++)
            {
                if (components[index] != null)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
