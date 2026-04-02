#nullable enable
using System;

namespace KinKeep.UnityCli.Bridge
{
    /// <summary>
    /// Marks a serialized field as a QA test target, discoverable by the CLI qa commands.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class QaTargetAttribute : Attribute
    {
        public string Id { get; }

        public QaTargetAttribute(string id)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
        }
    }
}
