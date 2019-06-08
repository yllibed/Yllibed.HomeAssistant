using System;
using Newtonsoft.Json;

namespace HomeAssistant.AppStarter.Models.WebsocketCommands
{
    public class SubscribeToEventsCommand : CommandBase
    {
        [JsonProperty("event_type")]
        public string EventType { get; }

        public SubscribeToEventsCommand(EventType eventType)
        {
            EventType = Enum.GetName(typeof(EventType), eventType);
        }

        public override string Type => "subscribe_events";
    }
}
