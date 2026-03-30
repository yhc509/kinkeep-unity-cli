using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace KinKeep.UnityCli.Bridge.Editor
{
    internal static class BridgeJsonSettings
    {
        internal static readonly JsonSerializerSettings CamelCaseIgnoreNull = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
        };
    }
}
