using System;
using System.Linq;
using UnityCli.Protocol;
using UnityEditor;

namespace PUC.Editor
{
    internal sealed class AssetCommandHandler
    {
        private readonly AssetCreateHandler _assetCreateHandler = new AssetCreateHandler();

        public bool CanHandle(string command)
        {
            return ProtocolHelpers.IsAssetCommand(command);
        }

        public string Handle(string command, string argumentsJson)
        {
            switch (command)
            {
                case ProtocolConstants.CommandAssetFind:
                    return HandleFind(argumentsJson);
                case ProtocolConstants.CommandAssetTypes:
                    return _assetCreateHandler.HandleTypes();
                case ProtocolConstants.CommandAssetInfo:
                    return HandleInfo(argumentsJson);
                case ProtocolConstants.CommandAssetReimport:
                    return HandleReimport(argumentsJson);
                case ProtocolConstants.CommandAssetMkdir:
                    return HandleMkdir(argumentsJson);
                case ProtocolConstants.CommandAssetMove:
                    return HandleMove(argumentsJson);
                case ProtocolConstants.CommandAssetRename:
                    return HandleRename(argumentsJson);
                case ProtocolConstants.CommandAssetDelete:
                    return HandleDelete(argumentsJson);
                case ProtocolConstants.CommandAssetCreate:
                    return _assetCreateHandler.Handle(argumentsJson);
                default:
                    throw new InvalidOperationException("지원하지 않는 asset 명령입니다: " + command);
            }
        }

        private static string HandleFind(string argumentsJson)
        {
            AssetFindArgs args = ProtocolJson.Deserialize<AssetFindArgs>(argumentsJson) ?? new AssetFindArgs();
            if (string.IsNullOrWhiteSpace(args.name))
            {
                throw new InvalidOperationException("asset-find에는 name이 필요합니다.");
            }

            int limit = args.limit <= 0 ? ProtocolConstants.DefaultAssetFindLimit : args.limit;
            string filter = args.name.Trim();
            if (!string.IsNullOrWhiteSpace(args.type))
            {
                filter += " t:" + args.type.Trim();
            }

            string[] guids = string.IsNullOrWhiteSpace(args.folder)
                ? AssetDatabase.FindAssets(filter)
                : AssetDatabase.FindAssets(filter, new[] { AssetCommandSupport.NormalizeAssetPath(args.folder) });

            AssetRecord[] results = guids
                .Select(AssetCommandSupport.BuildRecordFromGuid)
                .Where(record => record.exists)
                .OrderBy(record => record.path, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .ToArray();

            return ProtocolJson.Serialize(new AssetFindPayload { results = results });
        }

        private static string HandleInfo(string argumentsJson)
        {
            AssetInfoArgs args = ProtocolJson.Deserialize<AssetInfoArgs>(argumentsJson) ?? new AssetInfoArgs();
            bool hasPath = !string.IsNullOrWhiteSpace(args.path);
            bool hasGuid = !string.IsNullOrWhiteSpace(args.guid);
            if (hasPath == hasGuid)
            {
                throw new InvalidOperationException("asset-info에는 path 또는 guid 중 하나만 필요합니다.");
            }

            AssetRecord payload = hasPath
                ? AssetCommandSupport.BuildRecordFromPath(AssetCommandSupport.NormalizeAssetPath(args.path))
                : AssetCommandSupport.BuildRecordFromGuid(args.guid.Trim());

            return ProtocolJson.Serialize(payload);
        }

        private static string HandleReimport(string argumentsJson)
        {
            AssetPathArgs args = ProtocolJson.Deserialize<AssetPathArgs>(argumentsJson) ?? new AssetPathArgs();
            string path = AssetCommandSupport.RequireExistingAssetPath(args.path, "asset-reimport");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            return ProtocolJson.Serialize(new AssetMutationPayload
            {
                asset = AssetCommandSupport.BuildRecordFromPath(path),
                reimported = true,
            });
        }

        private static string HandleMkdir(string argumentsJson)
        {
            AssetPathArgs args = ProtocolJson.Deserialize<AssetPathArgs>(argumentsJson) ?? new AssetPathArgs();
            string path = AssetCommandSupport.NormalizeAssetPath(args.path);
            if (string.Equals(path, "Assets", StringComparison.Ordinal))
            {
                return ProtocolJson.Serialize(new AssetMutationPayload
                {
                    asset = AssetCommandSupport.BuildRecordFromPath(path),
                    created = false,
                });
            }

            if (AssetCommandSupport.AssetExists(path))
            {
                if (!AssetDatabase.IsValidFolder(path))
                {
                    throw new InvalidOperationException("같은 경로에 폴더가 아닌 asset이 이미 있습니다: " + path);
                }

                return ProtocolJson.Serialize(new AssetMutationPayload
                {
                    asset = AssetCommandSupport.BuildRecordFromPath(path),
                    created = false,
                });
            }

            bool created = false;
            string current = "Assets";
            string[] segments = path.Split('/');
            for (int index = 1; index < segments.Length; index++)
            {
                string segment = segments[index];
                if (string.IsNullOrWhiteSpace(segment))
                {
                    throw new InvalidOperationException("비어 있는 폴더 이름은 만들 수 없습니다.");
                }

                string next = current + "/" + segment;
                if (AssetCommandSupport.AssetExists(next))
                {
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        throw new InvalidOperationException("폴더 경로 중간에 asset 파일이 있습니다: " + next);
                    }
                }
                else
                {
                    string guid = AssetDatabase.CreateFolder(current, segment);
                    if (string.IsNullOrWhiteSpace(guid))
                    {
                        throw new InvalidOperationException("폴더를 만들지 못했습니다: " + next);
                    }

                    created = true;
                }

                current = next;
            }

            AssetDatabase.SaveAssets();
            return ProtocolJson.Serialize(new AssetMutationPayload
            {
                asset = AssetCommandSupport.BuildRecordFromPath(path),
                created = created,
            });
        }

        private static string HandleMove(string argumentsJson)
        {
            AssetMoveArgs args = ProtocolJson.Deserialize<AssetMoveArgs>(argumentsJson) ?? new AssetMoveArgs();
            string from = AssetCommandSupport.RequireExistingAssetPath(args.from, "asset-move");
            string to = AssetCommandSupport.NormalizeAssetPath(args.to);

            if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            {
                return ProtocolJson.Serialize(new AssetMutationPayload
                {
                    asset = AssetCommandSupport.BuildRecordFromPath(from),
                    previousPath = from,
                });
            }

            AssetCommandSupport.EnsureParentFolderExists(to);
            bool overwritten = AssetCommandSupport.DeleteIfTargetExists(to, args.force, "asset-move");

            string error = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrWhiteSpace(error))
            {
                throw new InvalidOperationException(error);
            }

            AssetDatabase.SaveAssets();
            return ProtocolJson.Serialize(new AssetMutationPayload
            {
                asset = AssetCommandSupport.BuildRecordFromPath(to),
                previousPath = from,
                overwritten = overwritten,
            });
        }

        private static string HandleRename(string argumentsJson)
        {
            AssetRenameArgs args = ProtocolJson.Deserialize<AssetRenameArgs>(argumentsJson) ?? new AssetRenameArgs();
            string path = AssetCommandSupport.RequireExistingAssetPath(args.path, "asset-rename");
            string newName = args.name == null ? string.Empty : args.name.Trim();
            if (string.IsNullOrWhiteSpace(newName))
            {
                throw new InvalidOperationException("asset-rename에는 name이 필요합니다.");
            }

            if (newName.Contains("/") || newName.Contains("\\"))
            {
                throw new InvalidOperationException("asset-rename의 name에는 경로 구분자를 넣을 수 없습니다.");
            }

            string destination = AssetCommandSupport.BuildRenamedPath(path, newName);
            if (string.Equals(path, destination, StringComparison.OrdinalIgnoreCase))
            {
                return ProtocolJson.Serialize(new AssetMutationPayload
                {
                    asset = AssetCommandSupport.BuildRecordFromPath(path),
                    previousPath = path,
                });
            }

            bool overwritten = AssetCommandSupport.DeleteIfTargetExists(destination, args.force, "asset-rename");
            string error = AssetDatabase.MoveAsset(path, destination);
            if (!string.IsNullOrWhiteSpace(error))
            {
                throw new InvalidOperationException(error);
            }

            AssetDatabase.SaveAssets();
            return ProtocolJson.Serialize(new AssetMutationPayload
            {
                asset = AssetCommandSupport.BuildRecordFromPath(destination),
                previousPath = path,
                overwritten = overwritten,
            });
        }

        private static string HandleDelete(string argumentsJson)
        {
            AssetPathArgs args = ProtocolJson.Deserialize<AssetPathArgs>(argumentsJson) ?? new AssetPathArgs();
            string path = AssetCommandSupport.RequireExistingAssetPath(args.path, "asset-delete");
            AssetRecord beforeDelete = AssetCommandSupport.BuildRecordFromPath(path);
            if (!AssetDatabase.DeleteAsset(path))
            {
                throw new InvalidOperationException("asset를 삭제하지 못했습니다: " + path);
            }

            AssetDatabase.SaveAssets();
            beforeDelete.exists = false;

            return ProtocolJson.Serialize(new AssetMutationPayload
            {
                asset = beforeDelete,
                deleted = true,
            });
        }
    }
}
