using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Yllibed.HomeAssistantFramework.Extensions;
using Yllibed.HomeAssistantFramework.Models;
using Yllibed.HomeAssistantFramework.Models.Apps;
using Yllibed.HomeAssistantFramework.Models.Events;
using Yllibed.HomeAssistantFramework.Models.WebsocketCommands;

namespace Yllibed.HomeAssistantFramework
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
		private AppRegistration[] _apps;
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
			{
				Initialize();
			}

			if (_started)
			{
				throw new InvalidOperationException($"{nameof(HassAppsRunner)} already started!");
			}

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
				{
					return;
				}

				if (result.MessageType == WebSocketMessageType.Binary)
				{
					continue;
				}

				// TODO: check result.EndOfMessage here for big messages

				var msg = Encoding.UTF8.GetString(buffer.Array, buffer.Offset, result.Count);
				var t = OnMessage(msg, ct);
			}
		}

		/// <summary>
		/// Disconnect and dispose websocket, kill subscriptions and let go of IHassApp instances
		/// </summary>
		public void Stop()
		{
			var ws = Interlocked.Exchange(ref _ws, null);

			if (ws == null)
			{
				return;
			}

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

			_apps = apps
				.Select(AppRegistration.TryCreate)
				.Where(a => a != null)
				.ToArray();

			_inited = true;
		}

		private Type[] ScanAssemblyForHassApps()
		{
			var type = typeof(IHassApp);
			var types = AppDomain.CurrentDomain.GetAssemblies()
					.SelectMany(s => s.GetTypes())
					.Where(p => type.IsAssignableFrom(p))
					.Where(p => p.IsClass)
				;

			return types.ToArray();
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
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream, _utf8))
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
			if (!msg.IsValidJson(out var json))
			{
				return;
			}

			var message = new WebSocketMessage(json);

			switch (message.MessageType)
			{
				case "auth_required":
					LogInfo("Authorization requested. Sending access token...");
					await SendWs(new AuthCommand(_accessToken), ct);
					return;

				case "auth_ok":
					LogInfo("Authorization completed, subscribing to events...");
					await SubscribeToEvents(ct);
					return;

				case "result":
					// Isn't an event, log and exit.
					LogDebug($"Result message: {msg.ToPrettyJson()}");
					return;

				case "event":
					break; // continue processing

				default:
					{
						// Isn't an event! And event's are what we're working with.
						LogWarn($"Unsupported message (not an 'event'): {msg.ToPrettyJson()}");
						return;
					}
			}

			var entId = message.EventEntityId;
			var matchedApps = _apps
				.Where(a => a.MatchEntity(entId))
				.ToArray();

			if (!matchedApps.Any())
			{
				// No matched apps, log and exit.
				if (EncounteredEntityIdsWithoutSubscription.Add(entId))
				{
					LogTrace($"First time encounter of message with an EntityId that we're not listening on: {entId}");
				}

				return;
			}

			// Found matched apps! Log and determine which type
			LogInfo(msg.ToPrettyJson());
			Click clickData = null;
			StateChanged stateChangedData = null;

			var eventType = message.EventType;
			switch (eventType)
			{
				case "click":
					{
						clickData = new Click { ClickType = (string)json["event"]["data"]["click_type"] };
						break;
					}

				case "state_changed":
					{
						//entity_boolean doesn't have a "last_triggered" attribute.
						//if (!entId.Contains("input_boolean."))
						//{
						//    if (!message.HasNewStateWithLastTriggered)
						//    {
						//        return; // Irrelevant event, we need new states that has "last time triggered" otherwise it might be an event provoked by reloading Hass. Unsure about this.
						//    }
						//}
						if (!message.IsTheMostRelevantStateChangeEvent)
						{
							return; // Is most probably a 'duped' event, throw it away ..
						}

						if (!message.HasNewState)
						{
							return; // Irrelevant event, we need new states only ..
						}

						stateChangedData = message.DeserializeStateChanged();
						break;
					}
			}

			var eventData = new EventData(entId, stateChangedData, clickData, msg);

			foreach (var hassApp in matchedApps)
			{
				hassApp.DispatchEvent(eventData);
			}
		}

		private void LogTrace(string log) => TraceOutput?.Invoke(this, new LogEventArgs { Text = log });

		private void LogWarn(string log) => WarnOutput?.Invoke(this, new LogEventArgs { Text = log });

		private void LogDebug(string log) => DebugOutput?.Invoke(this, new LogEventArgs { Text = log });

		private void LogInfo(string log) => InfoOutput?.Invoke(this, new LogEventArgs { Text = log });
	}
}
