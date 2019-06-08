using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HomeAssistant.AppStarter.Extensions;
using HomeAssistant.AppStarter.Models;
using HomeAssistant.AppStarter.Models.Events;
using HomeAssistant.AppStarter.Models.WebsocketCommands;
using HomeAssistant.AppStarter.RawModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace HomeAssistant.AppStarter
{
    // TODO: Implement IDisposable to call Stop()
    /// <summary>
    /// Connects to a websocket from Hass to catch events. See https://home-assistant.io/developers/websocket_api/
    /// </summary>
    public class HassAppsRunner
    {
        // Events
        public event EventHandler<LogEventArgs> TraceOutput;
        public event EventHandler<LogEventArgs> DebugOutput;
        public event EventHandler<LogEventArgs> InfoOutput;
        public event EventHandler<LogEventArgs> WarnOutput;
        public event EventHandler<LogEventArgs> ErrorOutput;

        // Fields
        private readonly Uri _hassWebsocketUri;
        private readonly string _accessToken;
        private bool _inited;
        /// <summary>
        /// TODO: This lib needs to be .NET Core
        /// </summary>
        private WebSocket _ws;

        // Props
        public HashSet<string> EncounteredEntityIdsWithoutSubscription { get; set; } = new HashSet<string>();
        private Dictionary<string, List<IHassApp>> _apps;
        private bool _started;

        private CancellationTokenSource _cts = new CancellationTokenSource();

        // Ctor
        /// <param name="hassWebsocketUri">Example: 'ws://192.168.0.168:8123/api/websocket' </param>
        /// <param name="apiPassword"></param>
        public HassAppsRunner(Uri hassWebsocketUri, string accessToken)
        {
            _hassWebsocketUri = hassWebsocketUri;
            _accessToken = accessToken;
        }

        // Public 

        /// <summary>
        /// Start a websocket and connect, subscribe to hass state_changed events
        /// </summary>
        public async Task Start(CancellationToken ct)
        {
            if (!_inited)
                Initialize();
            if (_started)
                throw new InvalidOperationException($"{nameof(HassAppsRunner)} already started!");

            var client = new ClientWebSocket();
            await client.ConnectAsync(_hassWebsocketUri, ct);

            _ws = client;

            StartReceive(_cts.Token);

            //SubscribeToEvents();

            _started = true;
        }

        private async void StartReceive(CancellationToken ct)
        {
            var buffer = WebSocket.CreateClientBuffer(1024 * 16, 1024 * 16);
            while (_ws.State == WebSocketState.Open)
            {
                var result = await _ws.ReceiveAsync(buffer, ct);

                if (result.MessageType == WebSocketMessageType.Close)
                    return;
                if (result.MessageType == WebSocketMessageType.Binary)
                    continue;

                // TODO: check result.EndOfMessage here for big messages

                var msg = Encoding.UTF8.GetString(buffer.Array, buffer.Offset, result.Count);
                await OnMessage(msg, ct);
            }
        }

        /// <summary>
        /// Disconnect and dispose websocket, kill subscriptions and let go of IHassApp instances
        /// </summary>
        public void Stop()
        {
            var ws = Interlocked.Exchange(ref _ws, null);

            if (ws == null)
                return;

            _cts.Cancel();

            ws.Dispose();

            _started = false;
        }

        // Private 

        /// <summary>
        /// * Scans assembly for implementations of <see cref="IHassApp"/>
        /// * Instantiates and connects them by their EntityId ids that they are listening to
        /// </summary>
        private void Initialize()
        {
            var apps = ScanAssemblyForHassApps();

            _apps = new Dictionary<string, List<IHassApp>>();

            foreach (var app in apps)
            {
                // TODO: Replace this to support ctor injection.
                var instance = (IHassApp)Activator.CreateInstance(app);

                // Clean up the filter
                instance.TriggeredByEntities = instance.TriggeredByEntities.ToLowerInvariant().Replace(" ", "");

                // Empty or null not allowed
                if (string.IsNullOrEmpty(instance.TriggeredByEntities))
                    throw new ArgumentNullException($"{nameof(IHassApp.TriggeredByEntities)} must not be null or empty!");

                // Support for comma-delimited entity id's
                var entityIdentifiers = instance.TriggeredByEntities.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var entId in entityIdentifiers)
                {
                    if (!_apps.ContainsKey(entId))
                    {
                        _apps.Add(entId, new List<IHassApp>());
                    }
                    _apps[entId].Add(instance);
                }
            }

            _inited = true;
        }

        private IEnumerable<Type> ScanAssemblyForHassApps()
        {
            var type = typeof(IHassApp);
            var types = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(s => s.GetTypes())
                    .Where(p => type.IsAssignableFrom(p))
                    .Where(p => p.IsClass)
                ;

            return types;
        }


        private async Task SubscribeToEvents(CancellationToken ct)
        {
            await SendWs(new SubscribeToEventsCommand(EventType.state_changed), ct);
            await SendWs(new SubscribeToEventsCommand(EventType.click), ct);
        }

        private static readonly JsonSerializer _serializer = new JsonSerializer();
        private static readonly Encoding _utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private async Task SendWs<T>(T msg, CancellationToken ct)
        {
            using(var stream = new MemoryStream())
            using(var writer = new StreamWriter(stream, _utf8))
            using (var jsonWriter = new JsonTextWriter(writer))
            {
                _serializer.Serialize(jsonWriter, msg);
                jsonWriter.Flush();
                writer.Flush();
                var bytes = stream.ToArray();
                var buffer = new ArraySegment<byte>(bytes);
                await _ws.SendAsync(buffer, WebSocketMessageType.Text, true, ct);
            }
        }

        private async Task OnMessage(string msg, CancellationToken ct)
        {
            if (!msg.IsValidJson())
                return;

            var json = JToken.Parse(msg);

            if (json.IsAuthMessage())
            {
                InfoOutput?.Invoke(this, new LogEventArgs { Text = "Authorization requested. Sending access token..." });
                await SendWs(new AuthCommand(_accessToken), ct);
                return;
            }

            if (json.IsAuthOk())
            {
                InfoOutput?.Invoke(this, new LogEventArgs { Text = "Authorization completed, subscribing to events..." });
                await SubscribeToEvents(ct);
                return;
            }

            if (json.IsResult())
            {
                // Isn't an event, log and exit.
                DebugOutput?.Invoke(this, new LogEventArgs { Text = $"Result message: {msg.ToPrettyJson()}" });
                return;
            }

            if (!json.IsEvent())
            {
                // Isn't an event! And event's are what we're working with.
                WarnOutput?.Invoke(this, new LogEventArgs { Text = $"Unsupported message (not an 'event'): {msg.ToPrettyJson()}" });
                return;
            }

            var entId = json.ExtractEntityId().ToLowerInvariant();
            var matchedApps = _apps.FindApps(entId);

            if (matchedApps.Count == 0)
            {
                // No matched apps, log and exit.
                if (EncounteredEntityIdsWithoutSubscription.Add(entId))
                    TraceOutput?.Invoke(this, new LogEventArgs { Text = $"First time encounter of message with an EntityId that we're not listening on: {entId}" });
                return;
            }

            // Found matched apps! Log and determine which type
            InfoOutput?.Invoke(this, new LogEventArgs { Text = msg.ToPrettyJson() });
            var eventData = new EventData { EntityId = entId };

            if (json.IsClickEvent())
            {
                eventData.ClickData = new Click { ClickType = (string)json["event"]["data"]["click_type"] };
            }

            if (json.IsStateChangeEvent())
            {
                //entity_boolean doesn't have a "last_triggered" attribute.
                if (!entId.Contains("input_boolean."))
                {
                    if (!json.HasNewStateWithLastTriggered())
                        return; // Irrelevant event, we need new states that has "last time triggered" otherwise it might be an event provoked by reloading Hass. Unsure about this.

                }
                if (!json.IsTheMostRelevantStateChangeMessage())
                    return; // Is most probably a 'duped' event, throw it away ..
                if (!json.HasNewState())
                    return; // Irrelevant event, we need new states only ..

                var rawGraph = JsonConvert.DeserializeObject<HassEventRawModel>(msg);
                var stateChange = new StateChanged
                {
                    NewState = rawGraph.@event.data.new_state?.state,
                    OldState = rawGraph.@event.data.old_state?.state,
                    Attributes = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                        (rawGraph.@event.data.new_state ?? rawGraph.@event.data.old_state ?? new StateRaw()).attributes
                        .ToString())
                };
                eventData.StateChangeData = stateChange;
            }

            foreach (var hassApp in matchedApps.Where(p => p.IsExecuting == false))
            {
                hassApp.IsExecuting = true;

                try
                {
                    await hassApp.ExecuteAsync(eventData, msg);
                }
                catch (Exception ex)
                {
                    ErrorOutput?.Invoke(this, new LogEventArgs {Text = ex?.Message, Exception = ex});
                }
                finally
                {
                    hassApp.IsExecuting = false;
                }
            }
        }

    }
}
