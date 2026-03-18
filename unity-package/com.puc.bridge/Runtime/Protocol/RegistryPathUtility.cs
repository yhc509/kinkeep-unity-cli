using System;
using System.IO;

namespace UnityCli.Protocol
{
    public static class RegistryPathUtility
    {
        public static string GetRegistryFilePath()
        {
            var overridePath = Environment.GetEnvironmentVariable("UNITY_CLI_REGISTRY_PATH");
            if (!string.IsNullOrWhiteSpace(overridePath))
            {
                return EnsureDirectoryAndReturn(overridePath);
            }

            var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(homeDirectory))
            {
                var macLibraryDirectory = Path.Combine(homeDirectory, "Library");
                if (Directory.Exists(macLibraryDirectory))
                {
                    return EnsureDirectoryAndReturn(Path.Combine(macLibraryDirectory, "Application Support", ProtocolConstants.AppName, "instances.json"));
                }
            }

            var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                baseDirectory = Path.Combine(homeDirectory, ".config");
            }

            return EnsureDirectoryAndReturn(Path.Combine(baseDirectory, ProtocolConstants.AppName, "instances.json"));
        }

        private static string EnsureDirectoryAndReturn(string filePath)
        {
            var fullPath = Path.GetFullPath(filePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return fullPath;
        }
    }
}
