///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2020 Sascha Falk <sascha@falk-online.eu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable RedundantAssignment

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// An auto-reset event with async/await capability.
	/// This implementation is tailored to its use in the logging subsystem.
	/// </summary>
	internal class AsyncAutoResetEvent
	{
		#region TaskNode

		private sealed class TaskNode : TaskCompletionSource<bool>
		{
			internal TaskNode Prev;
			internal TaskNode Next;
		}

		#endregion

		private static readonly Task<bool> sTrueTask = Task.FromResult(true);

		private bool mSet;
		private TaskNode mAsyncHead;
		private TaskNode mAsyncTail;
		private readonly object mSync = new object();

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncAutoResetEvent"/> with the specified initial state.
		/// </summary>
		/// <param name="set">
		/// true to set the event initially;
		/// otherwise false.
		/// </param>
		public AsyncAutoResetEvent(bool set)
		{
			mSet = set;
		}

		/// <summary>
		/// Sets the event.
		/// </summary>
		public void Set()
		{
			lock (mSync)
			{
				mSet = true;

				// Now signal to any asynchronous waiters, if there are any. While we've already
				// signaled the synchronous waiters, we still hold the lock, and thus
				// they won't have had an opportunity to acquire this yet. So, when releasing
				// asynchronous waiters, we assume that all synchronous waiters will eventually
				// acquire the event. That could be a faulty assumption if those synchronous
				// waits are canceled, but the wait code path will handle that.
				if (mAsyncHead != null)
				{
					Contract.Assert(mAsyncTail != null, "tail should not be null if head isn't null");

					// Get the next async waiter to release and queue it to be completed
					var waiterTask = mAsyncHead;
					RemoveAsyncWaiter(waiterTask); // ensures waiterTask.Next/Prev are null
					bool ok = waiterTask.TrySetResult(true);
					Debug.Assert(ok);
					mSet = false;
				}
			}
		}

		/// <summary>
		/// Asynchronously waits for the event to be set, with timeout and option to cancel the operation.
		/// </summary>
		/// <param name="timeout">
		/// The number of milliseconds to wait, or <see cref="Timeout.Infinite"/>(-1) to wait indefinitely.
		/// </param>
		/// <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken"/> to observe.</param>
		/// <returns>
		/// A task that will complete with a result of true if the event has been set within the specified time,
		/// otherwise with a result of false.
		/// </returns>
		public Task<bool> WaitAsync(int timeout, CancellationToken cancellationToken)
		{
			if (timeout < -1)
				throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "The timeout is out of valid bounds");

			// Check for cancellation
			if (cancellationToken.IsCancellationRequested)
				return Task.FromCanceled<bool>(cancellationToken);

			lock (mSync)
			{
				// Allow this waiter to proceed, if the event is set
				if (mSet)
				{
					mSet = false;
					return sTrueTask;
				}

				// If the event is not set, create and return a task to the caller
				var asyncWaiter = CreateAndAddAsyncWaiter();
				if (timeout == Timeout.Infinite && !cancellationToken.CanBeCanceled) {
					return asyncWaiter.Task;
				}

				cancellationToken.Register(WaitCancellationCallback, asyncWaiter, false);
				return WaitUntilCountOrTimeoutAsync(asyncWaiter, timeout, cancellationToken);
			}
		}

		/// <summary>
		/// Callback that is invoked when an asynchronous wait operation is canceled.
		/// </summary>
		/// <param name="state">The <see cref="TaskNode"/> associated with the wait operation.</param>
		private static void WaitCancellationCallback(object state)
		{
			TaskNode node = (TaskNode) state;
			bool success = node.TrySetCanceled();
			Contract.Assert(success);
		}

		/// <summary>
		/// Creates a new task and stores it into the async waiters list.
		/// </summary>
		/// <returns>The created task.</returns>
		private TaskNode CreateAndAddAsyncWaiter()
		{
			Contract.Assert(Monitor.IsEntered(mSync), "Requires the lock be held");

			var task = new TaskNode();

			if (mAsyncHead == null)
			{
				Contract.Assert(mAsyncTail == null, "If head is null, so too should be tail");
				mAsyncHead = task;
				mAsyncTail = task;
			}
			else
			{
				Contract.Assert(mAsyncTail != null, "If head is not null, neither should be tail");
				mAsyncTail.Next = task;
				task.Prev = mAsyncTail;
				mAsyncTail = task;
			}

			return task;
		}

		/// <summary>
		/// Removes the waiter task from the linked list.
		/// </summary>
		/// <param name="task">The task to remove.</param>
		/// <returns>true if the waiter was in the list; otherwise, false.</returns>
		private bool RemoveAsyncWaiter(TaskNode task)
		{
			Contract.Requires(task != null, "Expected non-null task");
			Contract.Assert(Monitor.IsEntered(mSync), "Requires the lock be held");

			// Check whether the task is in the list
			// (to be in the list, either it's the head or it has a predecessor that's in the list)
			bool wasInList = mAsyncHead == task || task.Prev != null;

			// remove it from the linked list
			if (task.Next != null) task.Next.Prev = task.Prev;
			if (task.Prev != null) task.Prev.Next = task.Next;
			if (mAsyncHead == task) mAsyncHead = task.Next;
			if (mAsyncTail == task) mAsyncTail = task.Prev;
			Contract.Assert((mAsyncHead == null) == (mAsyncTail == null), "Head is null if tail is null");

			// Make sure not to leak
			task.Next = task.Prev = null;

			// Return whether the task was in the list
			return wasInList;
		}

		/// <summary>
		/// Performs the asynchronous wait.
		/// </summary>
		/// <param name="asyncWaiter"></param>
		/// <param name="timeout">The timeout.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The task to return to the caller.</returns>
		private async Task<bool> WaitUntilCountOrTimeoutAsync(TaskNode asyncWaiter, int timeout, CancellationToken cancellationToken)
		{
			Contract.Assert(asyncWaiter != null, "Waiter should have been constructed");
			Contract.Assert(Monitor.IsEntered(mSync), "Requires the lock be held");

			// Wait until either the task is completed, timeout occurs, or cancellation is requested.
			// We need to ensure that the Task.Delay task is appropriately cleaned up if the await completes due to the
			// asyncWaiter completing, so we use our own token that we can explicitly cancel, and we chain the caller's
			// supplied token into it.
			using (var cts = cancellationToken.CanBeCanceled
				? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, default(CancellationToken))
				: new CancellationTokenSource())
			{
				var waitCompleted = Task.WhenAny(asyncWaiter.Task, Task.Delay(timeout, cts.Token));
				if (asyncWaiter.Task == await waitCompleted.ConfigureAwait(false))
				{
					cts.Cancel(); // ensure that the Task.Delay task is cleaned up
					return true;  // successfully acquired
				}
			}

			// If we get here, the wait has timed out or been canceled.

			// If the await completed synchronously, we still hold the lock.
			// If it didn't, we no longer hold the lock. As such, acquire it.
			lock (mSync)
			{
				// Remove the task from the list.  If we're successful in doing so, we know that no one else has tried to
				// complete this waiter yet, so we can safely cancel or timeout.
				if (RemoveAsyncWaiter(asyncWaiter))
				{
					cancellationToken.ThrowIfCancellationRequested(); // cancellation occurred
					return false; // timeout occurred
				}
			}

			// The waiter had already been removed, which means it's already completed or is about to complete, so let it,
			// and don't return until it does.
			return await asyncWaiter.Task.ConfigureAwait(false);
		}
	}
}
