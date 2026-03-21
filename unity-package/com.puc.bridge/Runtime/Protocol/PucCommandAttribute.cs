#nullable enable
using System;

namespace UnityCli.Protocol
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class PucCommandAttribute : Attribute
    {
        public PucCommandAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
