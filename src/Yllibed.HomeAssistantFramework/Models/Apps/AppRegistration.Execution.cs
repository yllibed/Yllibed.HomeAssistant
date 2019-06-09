using System.Collections.Concurrent;
using System.Threading;
using Yllibed.HomeAssistantFramework.Extensions;
using Yllibed.HomeAssistantFramework.Models.Events;

namespace Yllibed.HomeAssistantFramework.Models.Apps
{
	partial class AppRegistration
	{
		internal void DispatchEvent(EventData data)
		{
			// TODO: handle CT correctly

			switch (App.AppExecutionMode)
			{
				case AppExecutionMode.ConcurrentExecution:
					DispatchConcurrently(data);
					break;
				case AppExecutionMode.Queue:
					DispatchToQueue(data);
					break;
				case AppExecutionMode.Sample:
					DispatchSample(data);
					break;
			}
		}

		private void DispatchConcurrently(EventData data)
		{
			// Simply call the execution here,
			// full concurrency supported by the app itself.
			var t = App.ExecuteAsync(data, CancellationToken.None);
		}

		private ConcurrentQueue<EventData> _concurrentQueue;
		private ManualResetEvent _event;

		private void DispatchToQueue(EventData data)
		{
			if (_concurrentQueue == null)
			{
				var queue = new ConcurrentQueue<EventData>();
				var previous = Interlocked.CompareExchange(ref _concurrentQueue, queue, null);
				if (previous == null)
				{
					StartQueue();
				}
			}

			_concurrentQueue.Enqueue(data);
			_event.Set();
		}

		private async void StartQueue()
		{
			_event = new ManualResetEvent(false);

			while (true)
			{
				_event.Reset();

				if (_concurrentQueue.IsEmpty)
				{
					await _event.AsTask();
				}

				if (_concurrentQueue.TryDequeue(out var data))
				{
					try
					{
						await App.ExecuteAsync(data, CancellationToken.None);
					}
					catch
					{
						// TODO: log this somewhere...
					}
				}
			}
		}

		private int _isExecuting = 0;

		private void DispatchSample(EventData data)
		{
			if (Interlocked.CompareExchange(ref _isExecuting, 1, 0) != 0)
			{
				return; // Already executing... so we discard this event.
			}

			try
			{
				App.ExecuteAsync(data, CancellationToken.None)
					.ContinueWith(t => _isExecuting = 0);
			}
			catch
			{
				_isExecuting = 0;
			}
		}
	}
}
