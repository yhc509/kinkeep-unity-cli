#nullable enable
using System;

namespace UnityCliBridge.Bridge.Editor
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
