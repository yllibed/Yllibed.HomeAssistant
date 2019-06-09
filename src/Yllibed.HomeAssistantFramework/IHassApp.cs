using System.Threading;
using System.Threading.Tasks;
using Yllibed.HomeAssistantFramework.Models.Events;

namespace Yllibed.HomeAssistantFramework
{
	public interface IHassApp
	{
		/// <summary>
		/// Control how your app should react to message received while the
		/// app is executing.
		/// </summary>
		/// <remarks>
		/// When set to false, incoming messages are discarded during current execution.
		/// If you want to ensure to have **all events**, you must enable this feature.
		/// </remarks>
		AppExecutionMode AppExecutionMode { get; }

		/// <summary>
		/// The entity identifier(s) that we listen on. Separate multiple id's with comma, and/or use wildcard *.
		/// Examples: 'automation.wake_up*' or 'automation.wake_up_1, automation.wake_up_2' or 'sensor.my_sensor'
		/// </summary>
		string TriggeredByEntities { get; }

		/// <summary>
		/// Implement this with your own code.
		/// </summary>
		Task ExecuteAsync(EventData eventData, CancellationToken ct);
	}

	public enum AppExecutionMode : byte
	{
		/// <summary>
		/// Never execute the application concurrently.
		/// Incoming messages while executing as discarded.
		/// </summary>
		Sample,

		/// <summary>
		/// The app is execute concurrently on each message
		/// received.
		/// </summary>
		ConcurrentExecution,

		/// <summary>
		/// The message is queue for execution by the app.
		/// </summary>
		/// <remarks>
		/// WARNING: This could lead to big problems (out-of-memory, crash...)
		/// if the rate of processing of message is lower than the
		/// rate of incoming messages.
		/// </remarks>
		Queue,

		// TODO: a mode doing queue per device, but concurrent for different devices.
	}
}
