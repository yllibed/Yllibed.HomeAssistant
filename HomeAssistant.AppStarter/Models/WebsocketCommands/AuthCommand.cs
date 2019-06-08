using Newtonsoft.Json;

namespace HomeAssistant.AppStarter.Models.WebsocketCommands
{
    public class AuthCommand
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
