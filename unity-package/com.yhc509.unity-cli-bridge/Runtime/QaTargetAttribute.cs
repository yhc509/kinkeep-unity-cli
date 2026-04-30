#nullable enable
using System;

namespace UnityCliBridge.Bridge
{
    /// <summary>
    /// Marks a serialized field as a QA test target, discoverable by the CLI qa commands.
    /// Metadata-only attribute; production builds carry only the marker without editor-side scanning/runtime hooks.
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
