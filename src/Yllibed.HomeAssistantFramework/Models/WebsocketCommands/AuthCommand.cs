using Newtonsoft.Json;

namespace Yllibed.HomeAssistantFramework.Models.WebsocketCommands
{
    internal class AuthCommand
    {
        [JsonProperty("type")]
        public string Type => "auth";

        [JsonProperty("access_token")]
        public string Token { get; }

        public AuthCommand(string token)
        {
            Token = token;
        }
    }
}
