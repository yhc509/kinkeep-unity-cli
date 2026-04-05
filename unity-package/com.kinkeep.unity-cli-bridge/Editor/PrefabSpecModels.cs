#nullable enable
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KinKeep.UnityCli.Bridge.Editor
{
    [Serializable]
    internal sealed class PrefabCreateSpec
    {
        public int Version { get; set; }

        public PrefabNodeSpec Root { get; set; } = null!;
    }

    [Serializable]
    internal sealed class PrefabPatchSpec
    {
        public int Version { get; set; }

        public PrefabPatchOperationSpec[] Operations { get; set; } = Array.Empty<PrefabPatchOperationSpec>();
    }

    [Serializable]
    internal sealed class PrefabNodeSpec
    {
        public string Name { get; set; } = string.Empty;

        [JsonProperty("active")]
        public bool? IsActive { get; set; }

        public string? Tag { get; set; }
        public JToken Layer { get; set; } = null!;
        public PrefabTransformSpec Transform { get; set; } = null!;
        public PrefabComponentSpec[] Components { get; set; } = Array.Empty<PrefabComponentSpec>();
        public PrefabNodeSpec[] Children { get; set; } = Array.Empty<PrefabNodeSpec>();
    }

    [Serializable]
    internal sealed class PrefabNodeMutationSpec
    {
        public string? Name { get; set; }

        [JsonProperty("active")]
        public bool? IsActive { get; set; }

        public string? Tag { get; set; }
        public JToken Layer { get; set; } = null!;
        public PrefabTransformSpec Transform { get; set; } = null!;
    }

    [Serializable]
    internal sealed class PrefabTransformSpec
    {
        public PrefabVector3Spec LocalPosition { get; set; } = null!;
        public PrefabVector3Spec LocalRotationEuler { get; set; } = null!;
        public PrefabVector3Spec LocalScale { get; set; } = null!;
    }

    [Serializable]
    internal sealed class PrefabVector3Spec
    {
        public float? X { get; set; }
        public float? Y { get; set; }
        public float? Z { get; set; }
    }

    [Serializable]
    internal sealed class PrefabComponentSpec
    {
        public string Type { get; set; } = string.Empty;
        public JObject Values { get; set; } = null!;
    }

    [Serializable]
    internal sealed class PrefabPatchOperationSpec
    {
        [JsonProperty("op")]
        public string Operation { get; set; } = string.Empty;

        public string Parent { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public PrefabNodeSpec Node { get; set; } = null!;
        public JToken Values { get; set; } = null!;
        public PrefabComponentSpec Component { get; set; } = null!;
        public string ComponentType { get; set; } = string.Empty;
        public int? ComponentIndex { get; set; }
    }

    [Serializable]
    internal sealed class PrefabListComponentsArgs
    {
        public string? path;
        public string? node;
    }

    [Serializable]
    internal sealed class PrefabListComponentsPayload
    {
        public string node = string.Empty;
        public ComponentOperations.ComponentEntry[]? components;
    }
}
