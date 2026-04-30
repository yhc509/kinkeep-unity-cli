#nullable enable
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using PackageManagerInfo = UnityEditor.PackageManager.PackageInfo;

namespace KinKeep.UnityCli.Bridge.Editor
{
    internal enum SkillTarget
    {
        [InspectorName("Claude Code")]
        ClaudeCode = 0,
        [InspectorName("Codex")]
        Codex = 1,
    }

    internal static class SkillInstaller
    {
        private const string SkillName = "kinkeep-unity-cli";
        private const string AgentsDirectoryName = "agents";
        private const string MetaFileExtension = ".meta";

        internal static void Install(SkillTarget target)
        {
            string templateRoot = GetTemplateRoot();
            string destination = GetDestination(target);
            bool includeAgents = target == SkillTarget.Codex;

            if (Directory.Exists(destination))
            {
                bool shouldOverwrite = EditorUtility.DisplayDialog(
                    "Overwrite Skill?",
                    "기존 스킬이 이미 설치되어 있습니다: " + destination + "\n덮어쓰시겠습니까?",
                    "Overwrite",
                    "Cancel");
                if (!shouldOverwrite)
                {
                    return;
                }

                Directory.Delete(destination, true);
            }

            Directory.CreateDirectory(destination);
            CopyDirectory(templateRoot, destination, includeAgents);

            Debug.Log($"[SkillInstaller] Installed {target} skill to: {destination}");
        }

        internal static string GetDestination(SkillTarget target)
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(home))
            {
                throw new InvalidOperationException("Failed to resolve user home directory.");
            }

            switch (target)
            {
                case SkillTarget.ClaudeCode:
                    return Path.Combine(home, ".claude", "skills", SkillName);
                case SkillTarget.Codex:
                    return Path.Combine(home, ".codex", "skills", SkillName);
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
                if (string.Equals(Path.GetExtension(file), MetaFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

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
