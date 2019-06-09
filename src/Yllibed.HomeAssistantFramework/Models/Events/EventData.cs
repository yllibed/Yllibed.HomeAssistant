namespace Yllibed.HomeAssistantFramework.Models.Events
{
    public class EventData
    {
        public EventData(string entityId, StateChanged stateChangeData, Click clickData, string originalMessage)
        {
            EntityId = entityId;
            StateChangeData = stateChangeData;
            ClickData = clickData;
            OriginalMessage = originalMessage;
        }

        public string EntityId { get; }
        public Click ClickData { get; }
        public StateChanged StateChangeData { get; }

        public string OriginalMessage { get; }
    }
}
