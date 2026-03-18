#nullable enable
using System;

namespace UnityCli.Protocol
{
    [Serializable]
    public sealed class PingPayload
    {
        public string message = string.Empty;
        public string timestampUtc = string.Empty;
    }

    [Serializable]
    public sealed class StatusPayload
    {
        public string projectRoot = string.Empty;
        public string projectHash = string.Empty;
        public string projectName = string.Empty;
        public string unityVersion = string.Empty;
        public bool isPlaying;
        public bool isPaused;
        public bool isCompiling;
        public bool isUpdating;
        public string activeScenePath = string.Empty;
        public string pipeName = string.Empty;
    }

    [Serializable]
    public sealed class MessagePayload
    {
        public string message = string.Empty;
    }

    [Serializable]
    public sealed class PlayStatePayload
    {
        public bool isPlaying;
    }

    [Serializable]
    public sealed class PauseStatePayload
    {
        public bool isPaused;
    }

    [Serializable]
    public sealed class StopStatePayload
    {
        public bool isPlaying;
        public bool isPaused;
    }

    [Serializable]
    public sealed class ExecuteMenuArgs
    {
        public string path = string.Empty;
    }

    [Serializable]
    public sealed class ExecuteMenuPayload
    {
        public string path = string.Empty;
        public bool executed;
    }

    [Serializable]
    public sealed class ReadConsoleArgs
    {
        public int limit = ProtocolConstants.DefaultConsoleLimit;
        public string? type;
    }

    [Serializable]
    public sealed class ReadConsolePayload
    {
        public ConsoleLogEntry[] entries = Array.Empty<ConsoleLogEntry>();
    }

    [Serializable]
    public sealed class AssetFindArgs
    {
        public string name = string.Empty;
        public string? type;
        public string? folder;
        public int limit = ProtocolConstants.DefaultAssetFindLimit;
    }

    [Serializable]
    public sealed class AssetInfoArgs
    {
        public string? path;
        public string? guid;
    }

    [Serializable]
    public sealed class AssetTypesPayload
    {
        public AssetCreateTypeDescriptor[] types = Array.Empty<AssetCreateTypeDescriptor>();
    }

    [Serializable]
    public sealed class AssetPathArgs
    {
        public string path = string.Empty;
    }

    [Serializable]
    public sealed class AssetMoveArgs
    {
        public string from = string.Empty;
        public string to = string.Empty;
        public bool force;
    }

    [Serializable]
    public sealed class AssetRenameArgs
    {
        public string path = string.Empty;
        public string name = string.Empty;
        public bool force;
    }

    [Serializable]
    public sealed class AssetCreateArgs
    {
        public string type = string.Empty;
        public string path = string.Empty;
        public bool force;
        public string? script;
        public string? typeName;
        public string? dataJson;
        public string? optionsJson;
    }

    [Serializable]
    public sealed class PrefabInspectArgs
    {
        public string path = string.Empty;
        public bool withValues;
    }

    [Serializable]
    public sealed class PrefabCreateArgs
    {
        public string path = string.Empty;
        public bool force;
        public string specJson = string.Empty;
    }

    [Serializable]
    public sealed class PrefabPatchArgs
    {
        public string path = string.Empty;
        public string specJson = string.Empty;
    }

    [Serializable]
    public sealed class AssetCreateTypeDescriptor
    {
        public string typeId = string.Empty;
        public string displayName = string.Empty;
        public string defaultExtension = string.Empty;
        public string origin = string.Empty;
        public bool supportsDataPatch;
        public string[] requiredOptions = Array.Empty<string>();
        public string[] optionalOptions = Array.Empty<string>();
    }

    [Serializable]
    public sealed class AssetRecord
    {
        public string path = string.Empty;
        public string guid = string.Empty;
        public string assetName = string.Empty;
        public string mainType = string.Empty;
        public bool isFolder;
        public bool exists;
    }

    [Serializable]
    public sealed class AssetFindPayload
    {
        public AssetRecord[] results = Array.Empty<AssetRecord>();
    }

    [Serializable]
    public sealed class AssetMutationPayload
    {
        public AssetRecord asset = new AssetRecord();
        public bool created;
        public bool deleted;
        public bool reimported;
        public bool overwritten;
        public string previousPath = string.Empty;
    }

    [Serializable]
    public sealed class AssetCreatePayload
    {
        public AssetRecord asset = new AssetRecord();
        public string createdType = string.Empty;
        public bool overwritten;
    }

    [Serializable]
    public sealed class PrefabMutationPayload
    {
        public AssetRecord asset = new AssetRecord();
        public bool created;
        public bool patched;
        public bool overwritten;
    }

    [Serializable]
    public sealed class ConsoleLogEntry
    {
        public string timestampUtc = string.Empty;
        public string type = string.Empty;
        public string message = string.Empty;
        public string stackTrace = string.Empty;
    }
}
