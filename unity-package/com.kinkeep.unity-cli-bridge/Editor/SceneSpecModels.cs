#nullable enable
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KinKeep.UnityCli.Bridge.Editor
{
    [Serializable]
    internal sealed class ScenePatchSpec
    {
        public int Version { get; set; }

        public ScenePatchOperationSpec[] Operations { get; set; } = Array.Empty<ScenePatchOperationSpec>();
    }

    [Serializable]
    internal sealed class SceneNodeSpec
    {
        public string? Name { get; set; }

        [JsonProperty("active")]
        public bool? IsActive { get; set; }

        public string? Tag { get; set; }
        public JToken? Layer { get; set; }
        public SceneTransformSpec? Transform { get; set; }
        public SceneComponentSpec[] Components { get; set; } = Array.Empty<SceneComponentSpec>();
        public SceneNodeSpec[] Children { get; set; } = Array.Empty<SceneNodeSpec>();
    }

    [Serializable]
    internal sealed class SceneNodeMutationSpec
    {
        public string? Name { get; set; }

        [JsonProperty("active")]
        public bool? IsActive { get; set; }

        public string? Tag { get; set; }
        public JToken? Layer { get; set; }
        public SceneTransformSpec? Transform { get; set; }
    }

    [Serializable]
    internal sealed class SceneTransformSpec
    {
        public SceneVector3Spec? LocalPosition { get; set; }
        public SceneVector3Spec? LocalRotationEuler { get; set; }
        public SceneVector3Spec? LocalScale { get; set; }
    }

    [Serializable]
    internal sealed class SceneVector3Spec
    {
        public float? X { get; set; }
        public float? Y { get; set; }
        public float? Z { get; set; }
    }

    [Serializable]
    internal sealed class SceneComponentSpec
    {
        public string Type { get; set; } = string.Empty;
        public JObject? Values { get; set; }
    }

    [Serializable]
    internal sealed class ScenePatchOperationSpec
    {
        [JsonProperty("op")]
        public string Operation { get; set; } = string.Empty;

        public string? Parent { get; set; }
        public string? Target { get; set; }
        public SceneNodeSpec? Node { get; set; }
        public JToken? Values { get; set; }
        public SceneComponentSpec? Component { get; set; }
        public string? ComponentType { get; set; }
        public int? ComponentIndex { get; set; }
    }
}
