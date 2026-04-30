using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace KinKeep.UnityCli.Bridge.Editor
{
    [InitializeOnLoad]
    internal static class TypeDiscoveryUtility
    {
        private static readonly Type[] _emptyTypes = Array.Empty<Type>();
        private static Type[] _allTypes = Array.Empty<Type>();
        private static Dictionary<string, Type[]> _typesByFullName = new Dictionary<string, Type[]>(StringComparer.Ordinal);
        private static Dictionary<string, Type[]> _typesByShortName = new Dictionary<string, Type[]>(StringComparer.Ordinal);

        static TypeDiscoveryUtility()
        {
            RebuildCache();
        }

        internal static List<Type> FindTypes(Func<Type, bool> predicate)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            var results = new List<Type>();
            Type[] allTypes = _allTypes;
            for (int i = 0; i < allTypes.Length; i++)
            {
                Type type = allTypes[i];
                if (predicate(type))
                {
                    results.Add(type);
                }
            }

            return results;
        }

        internal static IReadOnlyList<Type> FindTypesByFullName(string typeName)
        {
            return FindIndexedTypes(_typesByFullName, typeName);
        }

        internal static IReadOnlyList<Type> FindTypesByShortName(string typeName)
        {
            return FindIndexedTypes(_typesByShortName, typeName);
        }

        private static IReadOnlyList<Type> FindIndexedTypes(Dictionary<string, Type[]> index, string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return _emptyTypes;
            }

            if (index.TryGetValue(typeName, out Type[] matches))
            {
                return matches;
            }

            return _emptyTypes;
        }

        private static void RebuildCache()
        {
            var allTypes = new List<Type>();
            var typesByFullName = new Dictionary<string, List<Type>>(StringComparer.Ordinal);
            var typesByShortName = new Dictionary<string, List<Type>>(StringComparer.Ordinal);
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
            {
                Type[] types;
                try
                {
                    types = assemblies[assemblyIndex].GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types;
                }

                for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    Type type = types[typeIndex];
                    if (type == null)
                    {
                        continue;
                    }

                    allTypes.Add(type);
                    AddTypeToIndex(typesByFullName, type.FullName, type);
                    AddTypeToIndex(typesByShortName, type.Name, type);
                }
            }

            _allTypes = allTypes.ToArray();
            _typesByFullName = FreezeIndex(typesByFullName);
            _typesByShortName = FreezeIndex(typesByShortName);
        }

        private static void AddTypeToIndex(Dictionary<string, List<Type>> index, string key, Type type)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (!index.TryGetValue(key, out List<Type> matches))
            {
                matches = new List<Type>();
                index.Add(key, matches);
            }

            matches.Add(type);
        }

        private static Dictionary<string, Type[]> FreezeIndex(Dictionary<string, List<Type>> source)
        {
            var result = new Dictionary<string, Type[]>(source.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<Type>> pair in source)
            {
                result.Add(pair.Key, pair.Value.ToArray());
            }

            return result;
        }
    }
}
