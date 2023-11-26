using Newtonsoft.Json;

namespace iRacingRPC.Configuration
{
    public struct ConfigJson
    {
        [JsonProperty("appid")]
        public string AppId { get; private set; }
    }
}
