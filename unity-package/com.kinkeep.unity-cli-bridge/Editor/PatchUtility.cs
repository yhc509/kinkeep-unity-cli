#nullable enable
using System;

namespace KinKeep.UnityCli.Bridge.Editor
{
    internal static class PatchUtility
    {
        internal static string NormalizeOperationName(string? operation)
        {
            return string.IsNullOrWhiteSpace(operation)
                ? string.Empty
                : operation.Trim().ToLowerInvariant();
        }
    }
}
