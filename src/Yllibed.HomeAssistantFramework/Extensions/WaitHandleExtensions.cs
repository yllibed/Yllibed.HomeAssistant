using System;
using System.Threading;
using System.Threading.Tasks;

namespace Yllibed.HomeAssistantFramework.Extensions
{
	public static class WaitHandleExtensions
	{
		public static Task AsTask(this WaitHandle handle)
		{
			return AsTask(handle, Timeout.InfiniteTimeSpan);
		}

		public static Task AsTask(this WaitHandle handle, TimeSpan timeout)
		{
			var tcs = new TaskCompletionSource<object>();

			void CallBack(object state, bool timedOut)
			{
				var localTcs = (TaskCompletionSource<object>)state;
				if (timedOut)
				{
					localTcs.TrySetCanceled();
				}
				else
				{
					localTcs.TrySetResult(null);
				}
			}

			var registration = ThreadPool
				.RegisterWaitForSingleObject(handle, CallBack, tcs, timeout, executeOnlyOnce: true);

			tcs.Task
				.ContinueWith((_, state) => ((RegisteredWaitHandle)state).Unregister(null), registration, TaskScheduler.Default);

			return tcs.Task;
		}
	}
}
