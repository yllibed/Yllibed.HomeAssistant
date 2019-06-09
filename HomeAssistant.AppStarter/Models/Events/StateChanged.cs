using System.Collections.Generic;
using System.Linq;
using HomeAssistant.AppStarter.RawModels;
using Newtonsoft.Json.Linq;

namespace HomeAssistant.AppStarter.Models.Events
{
    public class StateChanged
    {
        private readonly JToken _json;
        private readonly HassEventRawModel _rawGraph;

        internal StateChanged(JToken json)
        {
            _json = json;
            _rawGraph = _json.ToObject<HassEventRawModel>();
        }

        public string NewState => _rawGraph?.eventNode?.data?.new_state?.state;
        public string OldState => _rawGraph?.eventNode?.data?.old_state?.state;

        public IReadOnlyDictionary<string, object> NewStateAttributes => _rawGraph?.eventNode?.data?.new_state?.attributes;
        public IReadOnlyDictionary<string, object> OldStateAttributes => _rawGraph?.eventNode?.data?.old_state?.attributes;

        public IReadOnlyDictionary<string, object> Attributes => NewStateAttributes ?? OldStateAttributes;
    }
}
