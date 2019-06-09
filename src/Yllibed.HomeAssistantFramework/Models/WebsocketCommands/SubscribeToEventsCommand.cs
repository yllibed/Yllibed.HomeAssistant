using System;
using Newtonsoft.Json;

namespace Yllibed.HomeAssistantFramework.Models.WebsocketCommands
{
	internal class SubscribeToEventsCommand : CommandBase
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
