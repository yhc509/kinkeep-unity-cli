using System;
using System.IO;
using UnityCli.Protocol;
using UnityEditor;
using UnityEngine;

namespace PUC.Editor
{
    internal static class AssetCommandSupport
    {
        public static AssetRecord BuildRecordFromGuid(string guid)
        {
            string normalizedGuid = string.IsNullOrWhiteSpace(guid) ? string.Empty : guid.Trim();
            string path = string.IsNullOrWhiteSpace(normalizedGuid) ? string.Empty : AssetDatabase.GUIDToAssetPath(normalizedGuid);
            AssetRecord record = string.IsNullOrWhiteSpace(path)
                ? new AssetRecord
                {
                    guid = normalizedGuid,
                    exists = false,
                }
                : BuildRecordFromPath(path);

            if (string.IsNullOrWhiteSpace(record.guid))
            {
                record.guid = normalizedGuid;
            }

            return record;
        }

        public static AssetRecord BuildRecordFromPath(string path)
        {
            string normalizedPath = NormalizeAssetPath(path);
            bool isFolder = AssetDatabase.IsValidFolder(normalizedPath);
            string guid = AssetDatabase.AssetPathToGUID(normalizedPath);
            UnityEngine.Object mainAsset = isFolder ? null : AssetDatabase.LoadMainAssetAtPath(normalizedPath);
            bool exists = DoesAssetPathExistOnDisk(normalizedPath) || isFolder || mainAsset != null;

            string assetName;
            if (mainAsset != null)
            {
                assetName = mainAsset.name;
            }
            else if (isFolder)
            {
                assetName = Path.GetFileName(normalizedPath);
            }
            else
            {
                assetName = Path.GetFileNameWithoutExtension(normalizedPath);
            }

            Type mainType = AssetDatabase.GetMainAssetTypeAtPath(normalizedPath);
            return new AssetRecord
            {
                path = normalizedPath,
                guid = guid ?? string.Empty,
                assetName = assetName ?? string.Empty,
                mainType = mainType == null ? string.Empty : mainType.Name,
                isFolder = isFolder,
                exists = exists,
            };
        }

        public static bool AssetExists(string path)
        {
            string normalizedPath = NormalizeAssetPath(path);
            return DoesAssetPathExistOnDisk(normalizedPath)
                || AssetDatabase.IsValidFolder(normalizedPath)
                || AssetDatabase.LoadMainAssetAtPath(normalizedPath) != null;
        }

        public static string RequireExistingAssetPath(string path, string commandName)
        {
            string normalizedPath = NormalizeAssetPath(path);
            if (!AssetExists(normalizedPath))
            {
                throw new InvalidOperationException(commandName + " 대상 asset이 없습니다: " + normalizedPath);
            }

            return normalizedPath;
        }

        public static string NormalizeAssetPath(string path)
        {
            string normalizedPath = path == null ? string.Empty : path.Replace('\\', '/').Trim();
            normalizedPath = normalizedPath.TrimEnd('/');

            if (normalizedPath == "Assets" || normalizedPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return normalizedPath;
            }

            throw new InvalidOperationException("asset 경로는 `Assets/...` 형식이어야 합니다.");
        }

        public static void EnsureParentFolderExists(string path)
        {
            string parentPath = GetParentFolder(path);
            if (!AssetDatabase.IsValidFolder(parentPath))
            {
                throw new CommandFailureException("ASSET_CREATE_FAILED", "대상 폴더가 없습니다: " + parentPath);
            }
        }

        public static bool DeleteIfTargetExists(string path, bool force, string commandName)
        {
            if (!AssetExists(path))
            {
                return false;
            }

            if (!force)
            {
                throw new InvalidOperationException(commandName + " 대상 경로가 이미 있습니다. 덮어쓰려면 --force를 사용하세요: " + path);
            }

            if (!AssetDatabase.DeleteAsset(path))
            {
                throw new InvalidOperationException("기존 asset를 지우지 못했습니다: " + path);
            }

            return true;
        }

        public static string BuildRenamedPath(string path, string newName)
        {
            string folder = GetParentFolder(path);
            string extension = AssetDatabase.IsValidFolder(path) ? string.Empty : Path.GetExtension(path);
            return folder + "/" + newName + extension;
        }

        public static string GetParentFolder(string path)
        {
            int separatorIndex = path.LastIndexOf('/');
            if (separatorIndex <= 0)
            {
                return "Assets";
            }

            return path.Substring(0, separatorIndex);
        }

        public static string GetPhysicalPath(string assetPath)
        {
            string normalizedPath = NormalizeAssetPath(assetPath);
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new InvalidOperationException("Unity 프로젝트 루트를 찾지 못했습니다.");
            }

            string relativePath = normalizedPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(projectRoot, relativePath));
        }

        private static bool DoesAssetPathExistOnDisk(string assetPath)
        {
            string physicalPath = GetPhysicalPath(assetPath);
            return File.Exists(physicalPath) || Directory.Exists(physicalPath);
        }
    }
}
