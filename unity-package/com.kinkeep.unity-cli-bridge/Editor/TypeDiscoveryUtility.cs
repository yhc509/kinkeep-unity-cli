using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace KinKeep.UnityCli.Bridge.Editor
{
    internal static class TypeDiscoveryUtility
    {
        internal static List<Type> FindTypes(Func<Type, bool> predicate)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            var results = new List<Type>();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                IEnumerable<Type> types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types.OfType<Type>();
                }

                foreach (Type type in types)
                {
                    if (predicate(type))
                    {
                        results.Add(type);
                    }
                }
            }

            return results;
        }
    }
}
