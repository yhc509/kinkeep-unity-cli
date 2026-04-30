using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace UnityCliBridge.Bridge.Editor
{
    internal static class BridgeJsonSettings
    {
        internal static readonly JsonSerializerSettings CamelCaseIgnoreNull = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
        };

        internal static readonly JsonSerializer CamelCaseIgnoreNullSerializer = JsonSerializer.Create(CamelCaseIgnoreNull);
    }
}
