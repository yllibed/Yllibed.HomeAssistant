using System.Threading;
using Newtonsoft.Json;

namespace Yllibed.HomeAssistantFramework.Models.WebsocketCommands
{
    internal abstract class CommandBase
    {
        private static long _nextId = 0;

        [JsonProperty("id")]
        public long Id { get; } = Interlocked.Increment(ref _nextId);

        [JsonProperty("type")]
        public abstract string Type { get; }
    }
}
