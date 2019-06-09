using Newtonsoft.Json.Linq;

namespace Yllibed.HomeAssistantFramework.Models.Events
{
    internal class WebSocketMessage
    {
        private readonly JToken _json;
        private readonly JToken _eventData;

        public WebSocketMessage(JToken json)
        {
            _json = json;

            MessageType = json?["type"]?.ToString();
            EventType = json?["event"]?["event_type"]?.ToString();
            _eventData = json?["event"]?["data"];
            EventEntityId = _eventData?["entity_id"]?.ToString().ToLowerInvariant();
        }

        public string MessageType { get; }

        public string EventType { get; }

        public bool IsEvent => MessageType == "event" && EventType != null;

        public string EventEntityId { get; }

        public bool HasNewState
        {
            get
            {
                var newState = _eventData?["new_state"];
                return newState != null
                       && newState.Type != JTokenType.Null;
            }
        }

        public bool HasNewStateWithLastTriggered
        {
            get
            {
                if (!HasNewState)
                {
                    return false;
                }
                var lastTriggered = _eventData?["new_state"]?["attributes"]?["last_triggered"];
                return lastTriggered != null
                       && lastTriggered.Type != JTokenType.Null;
            }
        }

        public bool IsTheMostRelevantStateChangeEvent
        {
            get
            {
                if (EventEntityId == null)
                {
                    return false;
                }

                var entId = EventEntityId;

                var newStateEntityId = _eventData?["new_state"]?["entity_id"]?.ToString();
                var oldStateEntityId = _eventData?["old_state"]?["entity_id"]?.ToString();

                var deeperEntId = newStateEntityId ?? oldStateEntityId;

                // Outer entity_id must be same as inner because otherwise it's a (for us) irrelevant message due to some internal Hass workings. 
                // Example: we can get several messages, one that has entity id: light.mylight and also light.mylight_2. Dunno why.
                return entId == deeperEntId;
            }
        }

        public StateChanged DeserializeStateChanged()
        {
            return new StateChanged(_json);
        }
    }
}
