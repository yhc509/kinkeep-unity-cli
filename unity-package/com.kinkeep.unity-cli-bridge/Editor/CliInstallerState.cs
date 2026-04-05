#nullable enable
using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using PackageManagerInfo = UnityEditor.PackageManager.PackageInfo;

namespace KinKeep.UnityCli.Bridge.Editor
{
    public enum CliInstallStatus
    {
        NotInstalled,
        UpToDate,
        UpdateRequired,
    }

    public static class CliInstallerState
    {
        private const string InstalledVersionEditorPrefsKey = "KinKeep.CLI.InstalledVersion";
        private const string PackageJsonFileName = "package.json";
        private const string RepositoryUrl = "https://github.com/yhc509/kinkeep-unity-cli";
        private const string ReleaseDownloadUrlPattern = RepositoryUrl + "/releases/download/v{0}/unity-cli-{1}.{2}";
        private const string ReleasePageUrlPattern = RepositoryUrl + "/releases/tag/v{0}";
        private const string InstallRootDirectoryName = ".kinkeep";
        private const string InstallDirectoryName = "unity-cli";
        private const string MacExecutableName = "unity-cli";
        private const string WindowsExecutableName = "unity-cli.exe";
        private const string MacPlatformAssetName = "osx-arm64";
        private const string WindowsPlatformAssetName = "win-x64";
        private const string MacArchiveExtension = "tar.gz";
        private const string WindowsArchiveExtension = "zip";
        private const string MacPlatformDisplayName = "macOS arm64";
        private const string WindowsPlatformDisplayName = "Windows x64";

        public static bool IsInstalled => File.Exists(GetExecutablePath());

        public static string GetInstallDirectory()
        {
            string userProfileDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(userProfileDirectory))
            {
                throw new InvalidOperationException("Failed to resolve user home directory.");
            }

            return Path.Combine(userProfileDirectory, InstallRootDirectoryName, InstallDirectoryName);
        }

        public static string GetExecutablePath()
        {
            return Path.Combine(GetInstallDirectory(), GetExecutableFileName());
        }

        public static string? GetInstalledVersion()
        {
            if (!IsInstalled)
            {
                return null;
            }

            string installedVersion = EditorPrefs.GetString(InstalledVersionEditorPrefsKey, string.Empty).Trim();
            return installedVersion.Length == 0 ? null : installedVersion;
        }

        public static string GetPackageVersion()
        {
            string packageJsonPath = Path.Combine(GetPackageDirectory(), PackageJsonFileName);
            if (!File.Exists(packageJsonPath))
            {
                throw new FileNotFoundException("Could not find package.json for the Unity package.", packageJsonPath);
            }

            JObject packageJson = JObject.Parse(File.ReadAllText(packageJsonPath));
            string? packageVersion = packageJson["version"]?.Value<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(packageVersion))
            {
                throw new InvalidOperationException("package.json does not contain a version field.");
            }

            return packageVersion;
        }

        public static string GetDownloadUrl()
        {
            string packageVersion = GetPackageVersion();
            GetPlatformAssetInfo(out string platformAssetName, out string archiveExtension);
            return string.Format(
                ReleaseDownloadUrlPattern,
                packageVersion,
                platformAssetName,
                archiveExtension);
        }

        public static string GetRepositoryUrl()
        {
            return RepositoryUrl;
        }

        public static string GetReleasePageUrl()
        {
            return string.Format(ReleasePageUrlPattern, GetPackageVersion());
        }

        public static string GetPlatformDisplayName()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.OSXEditor:
                    return MacPlatformDisplayName;
                case RuntimePlatform.WindowsEditor:
                    return WindowsPlatformDisplayName;
                default:
                    throw new PlatformNotSupportedException("CLI Installer only supports macOS arm64 and Windows x64 editors.");
            }
        }

        public static CliInstallStatus GetStatus()
        {
            if (!IsInstalled)
            {
                return CliInstallStatus.NotInstalled;
            }

            string? installedVersion = GetInstalledVersion();
            if (string.IsNullOrWhiteSpace(installedVersion))
            {
                return CliInstallStatus.UpdateRequired;
            }

            string packageVersion = GetPackageVersion();
            return CompareVersions(installedVersion, packageVersion) >= 0
                ? CliInstallStatus.UpToDate
                : CliInstallStatus.UpdateRequired;
        }

        public static void SetInstalledVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException("Installed version value is required.", nameof(version));
            }

            EditorPrefs.SetString(InstalledVersionEditorPrefsKey, version.Trim());
        }

        private static string GetPackageDirectory()
        {
            PackageManagerInfo? packageInfo = PackageManagerInfo.FindForAssembly(typeof(CliInstallerState).Assembly);
            if (packageInfo == null || string.IsNullOrWhiteSpace(packageInfo.resolvedPath))
            {
                throw new InvalidOperationException("Could not resolve the Unity package path.");
            }

            return packageInfo.resolvedPath;
        }

        private static string GetExecutableFileName()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.OSXEditor:
                    return MacExecutableName;
                case RuntimePlatform.WindowsEditor:
                    return WindowsExecutableName;
                default:
                    throw new PlatformNotSupportedException("CLI Installer only supports macOS arm64 and Windows x64 editors.");
            }
        }

        private static int CompareVersions(string leftVersion, string rightVersion)
        {
            return ParseVersion(leftVersion).CompareTo(ParseVersion(rightVersion));
        }

        private static Version ParseVersion(string version)
        {
            string normalizedVersion = version.Trim().TrimStart('v', 'V');
            return Version.Parse(normalizedVersion);
        }

        private static void GetPlatformAssetInfo(out string platformAssetName, out string archiveExtension)
        {
            switch (Application.platform)
            {
                case RuntimePlatform.OSXEditor:
                    platformAssetName = MacPlatformAssetName;
                    archiveExtension = MacArchiveExtension;
                    return;
                case RuntimePlatform.WindowsEditor:
                    platformAssetName = WindowsPlatformAssetName;
                    archiveExtension = WindowsArchiveExtension;
                    return;
                default:
                    throw new PlatformNotSupportedException("CLI Installer only supports macOS arm64 and Windows x64 editors.");
            }
        }
    }
}
