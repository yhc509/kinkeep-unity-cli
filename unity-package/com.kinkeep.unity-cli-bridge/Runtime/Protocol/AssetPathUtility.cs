using System;

namespace UnityCli.Protocol
{
    public static class AssetPathUtility
    {
        public static string Normalize(string? path, bool allowPackages = false)
        {
            string normalizedPath = NormalizeSlashes(path);
            if (IsAssetsPath(normalizedPath) || (allowPackages && IsPackagesPath(normalizedPath)))
            {
                return normalizedPath;
            }

            throw new InvalidOperationException(
                allowPackages
                    ? "asset 경로는 `Assets/...` 또는 `Packages/...` 형식이어야 합니다."
                    : "asset 경로는 `Assets/...` 형식이어야 합니다.");
        }

        public static bool IsAssetsPath(string? path)
        {
            return HasRootPrefix(NormalizeSlashes(path), "Assets");
        }

        public static bool IsPackagesPath(string? path)
        {
            return HasRootPrefix(NormalizeSlashes(path), "Packages");
        }

        private static string NormalizeSlashes(string? path)
        {
            string normalizedPath = path == null ? string.Empty : path.Replace('\\', '/').Trim();
            return normalizedPath.TrimEnd('/');
        }

        private static bool HasRootPrefix(string path, string root)
        {
            return string.Equals(path, root, StringComparison.Ordinal)
                || path.StartsWith(root + "/", StringComparison.Ordinal);
        }
    }
}
