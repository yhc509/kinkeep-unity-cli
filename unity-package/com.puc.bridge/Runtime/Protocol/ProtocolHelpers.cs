using System;

namespace UnityCli.Protocol
{
    public static class ProtocolHelpers
    {
        private static readonly string[] SupportedCommands =
        {
            ProtocolConstants.CommandPing,
            ProtocolConstants.CommandStatus,
            ProtocolConstants.CommandRefresh,
            ProtocolConstants.CommandCompile,
            ProtocolConstants.CommandPlay,
            ProtocolConstants.CommandPause,
            ProtocolConstants.CommandStop,
            ProtocolConstants.CommandExecuteMenu,
            ProtocolConstants.CommandReadConsole,
            ProtocolConstants.CommandAssetFind,
            ProtocolConstants.CommandAssetTypes,
            ProtocolConstants.CommandAssetInfo,
            ProtocolConstants.CommandAssetReimport,
            ProtocolConstants.CommandAssetMkdir,
            ProtocolConstants.CommandAssetMove,
            ProtocolConstants.CommandAssetRename,
            ProtocolConstants.CommandAssetDelete,
            ProtocolConstants.CommandAssetCreate,
            ProtocolConstants.CommandPrefabInspect,
            ProtocolConstants.CommandPrefabCreate,
            ProtocolConstants.CommandPrefabPatch,
        };

        public static string[] GetSupportedCommands()
        {
            return (string[])SupportedCommands.Clone();
        }

        public static bool IsCommandAllowedWhileBusy(string command)
        {
            return string.Equals(command, ProtocolConstants.CommandPing, StringComparison.Ordinal)
                || string.Equals(command, ProtocolConstants.CommandStatus, StringComparison.Ordinal)
                || string.Equals(command, ProtocolConstants.CommandReadConsole, StringComparison.Ordinal)
                || string.Equals(command, ProtocolConstants.CommandAssetFind, StringComparison.Ordinal)
                || string.Equals(command, ProtocolConstants.CommandAssetTypes, StringComparison.Ordinal)
                || string.Equals(command, ProtocolConstants.CommandAssetInfo, StringComparison.Ordinal)
                || string.Equals(command, ProtocolConstants.CommandPrefabInspect, StringComparison.Ordinal);
        }

        public static bool IsAssetCommand(string command)
        {
            return string.Equals(command, ProtocolConstants.CommandAssetFind, StringComparison.Ordinal)
                || string.Equals(command, ProtocolConstants.CommandAssetTypes, StringComparison.Ordinal)
                || string.Equals(command, ProtocolConstants.CommandAssetInfo, StringComparison.Ordinal)
                || string.Equals(command, ProtocolConstants.CommandAssetReimport, StringComparison.Ordinal)
                || string.Equals(command, ProtocolConstants.CommandAssetMkdir, StringComparison.Ordinal)
                || string.Equals(command, ProtocolConstants.CommandAssetMove, StringComparison.Ordinal)
                || string.Equals(command, ProtocolConstants.CommandAssetRename, StringComparison.Ordinal)
                || string.Equals(command, ProtocolConstants.CommandAssetDelete, StringComparison.Ordinal)
                || string.Equals(command, ProtocolConstants.CommandAssetCreate, StringComparison.Ordinal);
        }

        public static bool IsPrefabCommand(string command)
        {
            return string.Equals(command, ProtocolConstants.CommandPrefabInspect, StringComparison.Ordinal)
                || string.Equals(command, ProtocolConstants.CommandPrefabCreate, StringComparison.Ordinal)
                || string.Equals(command, ProtocolConstants.CommandPrefabPatch, StringComparison.Ordinal);
        }
    }
}
