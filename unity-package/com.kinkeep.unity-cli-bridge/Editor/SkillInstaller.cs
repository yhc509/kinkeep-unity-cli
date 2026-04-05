#nullable enable
using System;
using System.IO;
using UnityEngine;
using PackageManagerInfo = UnityEditor.PackageManager.PackageInfo;

namespace KinKeep.UnityCli.Bridge.Editor
{
    internal enum SkillTarget
    {
        ClaudeCode = 0,
        Codex = 1,
    }

    internal static class SkillInstaller
    {
        private const string SkillName = "unity-cli-operator";
        private const string AgentsDirectoryName = "agents";

        internal static void Install(SkillTarget target)
        {
            string templateRoot = GetTemplateRoot();
            string destination = GetDestination(target);
            bool includeAgents = target == SkillTarget.Codex;

            CopyDirectory(templateRoot, destination, includeAgents);

            Debug.Log($"[SkillInstaller] Installed {target} skill to: {destination}");
        }

        internal static string GetDestination(SkillTarget target)
        {
            switch (target)
            {
                case SkillTarget.ClaudeCode:
                    string? projectRoot = Path.GetDirectoryName(Application.dataPath);
                    if (string.IsNullOrWhiteSpace(projectRoot))
                    {
                        throw new InvalidOperationException("Failed to resolve Unity project root.");
                    }

                    return Path.Combine(projectRoot, ".claude", "skills", SkillName);
                case SkillTarget.Codex:
                    string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    if (string.IsNullOrWhiteSpace(homeDirectory))
                    {
                        throw new InvalidOperationException("Failed to resolve user home directory.");
                    }

                    return Path.Combine(homeDirectory, ".codex", "skills", SkillName);
                default:
                    throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported skill target.");
            }
        }

        private static string GetTemplateRoot()
        {
            PackageManagerInfo? packageInfo = PackageManagerInfo.FindForAssembly(typeof(SkillInstaller).Assembly);
            if (packageInfo == null || string.IsNullOrWhiteSpace(packageInfo.resolvedPath))
            {
                throw new InvalidOperationException("Could not resolve the Unity package path.");
            }

            return Path.Combine(packageInfo.resolvedPath, "SkillTemplates~");
        }

        private static void CopyDirectory(string source, string destination, bool includeAgents)
        {
            if (!Directory.Exists(source))
            {
                throw new DirectoryNotFoundException("Skill template not found: " + source);
            }

            Directory.CreateDirectory(destination);

            foreach (string file in Directory.GetFiles(source))
            {
                string destinationFile = Path.Combine(destination, Path.GetFileName(file));
                File.Copy(file, destinationFile, overwrite: true);
            }

            foreach (string directory in Directory.GetDirectories(source))
            {
                string directoryName = Path.GetFileName(directory);
                if (!includeAgents && string.Equals(directoryName, AgentsDirectoryName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                CopyDirectory(directory, Path.Combine(destination, directoryName), includeAgents);
            }
        }
    }
}
