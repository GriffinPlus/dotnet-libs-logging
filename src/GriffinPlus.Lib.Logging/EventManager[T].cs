﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable StaticMemberInGenericType

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Event manager that administrates event handlers in a central place.
	/// Made for events of the <see cref="EventHandler{TEventArgs}" /> type.
	/// Objects firing events do not need to implement own event add/remove logic, especially when it comes to firing
	/// events asynchronously.
	/// </summary>
	/// <typeparam name="T">Type of the event arguments of the event.</typeparam>
	static class EventManager<T> where T : EventArgs
	{
		/// <summary>
		/// An event handler item in the event manager.
		/// </summary>
		private readonly struct Item
		{
			public readonly SynchronizationContext SynchronizationContext;
			public readonly EventHandler<T>        Handler;
			public readonly bool                   ScheduleAlways;

			public Item(SynchronizationContext context, EventHandler<T> handler, bool scheduleAlways)
			{
				SynchronizationContext = context;
				Handler = handler;
				ScheduleAlways = scheduleAlways;
			}
		}

		private static readonly ConditionalWeakTable<object, Dictionary<string, Item[]>> sItemsByObject =
			new ConditionalWeakTable<object, Dictionary<string, Item[]>>();

		private static readonly object sSync = new object();

		/// <summary>
		/// Registers an event handler for an event associated with the specified object.
		/// </summary>
		/// <param name="obj">Object providing the event.</param>
		/// <param name="eventName">Name of the event.</param>
		/// <param name="handler">Event handler to register.</param>
		/// <param name="context">Synchronization context to use when calling the event handler (may be null).</param>
		/// <param name="scheduleAlways">
		/// If <paramref name="context" /> is set:
		/// <c>true</c> to always schedule the event handler in the specified synchronization context,
		/// <c>false</c> to schedule the event handler in the specified context only, if the thread firing the event has some other synchronization context.
		/// If <paramref name="context" /> is <c>null</c>:
		/// <c>true</c> to always schedule the event handler in a worker thread,
		/// <c>false</c> to invoke the event handler in the thread that is firing the event (direct call).
		/// </param>
		/// <returns>Total number of registered event handlers (including the specified event handler).</returns>
		public static int RegisterEventHandler(
			object                 obj,
			string                 eventName,
			EventHandler<T>        handler,
			SynchronizationContext context,
			bool                   scheduleAlways)
		{
			return RegisterEventHandler(
				obj,
				eventName,
				handler,
				context,
				scheduleAlways,
				false,
				null,
				null);
		}

		/// <summary>
		/// Registers an event handler for an event associated with the specified object and optionally fires the
		/// event handler after registration.
		/// </summary>
		/// <param name="obj">Object providing the event.</param>
		/// <param name="eventName">Name of the event.</param>
		/// <param name="handler">Event handler to register.</param>
		/// <param name="context">
		/// Synchronization context to use when calling the event handler
		/// (<c>null</c> to execute the event handler in the context of the thread firing the event).
		/// </param>
		/// <param name="scheduleAlways">
		/// If <paramref name="context" /> is set:
		/// <c>true</c> to always schedule the event handler in the specified synchronization context,
		/// <c>false</c> to schedule the event handler in the specified context only, if the thread firing the event has some other synchronization context.
		/// If <paramref name="context" /> is <c>null</c>:
		/// <c>true</c> to always schedule the event handler in a worker thread,
		/// <c>false</c> to invoke the event handler in the thread that is firing the event (direct call).
		/// </param>
		/// <param name="fireImmediately">
		/// true to register and fire the event handler immediately after registration;
		/// false to register the event handler only.
		/// </param>
		/// <param name="sender">Sender object to pass to the event handler that is fired immediately.</param>
		/// <param name="e">Event arguments to pass to the event handler that is fired immediately.</param>
		/// <returns>Total number of registered event handlers (including the specified event handler).</returns>
		public static int RegisterEventHandler(
			object                 obj,
			string                 eventName,
			EventHandler<T>        handler,
			SynchronizationContext context,
			bool                   scheduleAlways,
			bool                   fireImmediately,
			object                 sender,
			T                      e)
		{
			if (handler == null) throw new ArgumentNullException(nameof(handler));

			Item[] newItems;

			lock (sSync)
			{
				if (!sItemsByObject.TryGetValue(obj, out var itemsByName))
				{
					itemsByName = new Dictionary<string, Item[]>(1);
					sItemsByObject.Add(obj, itemsByName);
				}

				if (itemsByName.TryGetValue(eventName, out var items))
				{
					newItems = new Item[items.Length + 1];
					Array.Copy(items, newItems, items.Length);
					newItems[items.Length] = new Item(context, handler, scheduleAlways);
					itemsByName[eventName] = newItems;
				}
				else
				{
					newItems = new Item[1];
					newItems[0] = new Item(context, handler, scheduleAlways);
					itemsByName[eventName] = newItems;
				}
			}

			if (fireImmediately)
			{
				if (context != null)
				{
					if (scheduleAlways) context.Post(_ => handler(sender, e), null);
					else handler(sender, e);
				}
				else
				{
					if (scheduleAlways) Task.Run(() => handler(sender, e));
					else handler(sender, e);
				}
			}

			return newItems.Length;
		}

		/// <summary>
		/// Unregisters an event handler from the specified event.
		/// </summary>
		/// <param name="obj">Object providing the event.</param>
		/// <param name="eventName">Name of the event.</param>
		/// <param name="handler">Event handler to unregister.</param>
		/// <returns>
		/// Total number of registered event handlers after the specified handler has been removed;
		/// -1, if the specified event handler is not registered.
		/// </returns>
		public static int UnregisterEventHandler(object obj, string eventName, EventHandler<T> handler)
		{
			if (handler == null) throw new ArgumentNullException(nameof(handler));

			lock (sSync)
			{
				if (!sItemsByObject.TryGetValue(obj, out var itemsByName) || !itemsByName.TryGetValue(eventName, out var items))
					return -1; // specified event handler was not registered

				for (int i = 0; i < items.Length; i++)
				{
					var registeredHandler = items[i].Handler;
					if (registeredHandler == handler)
					{
						var newItems = new Item[items.Length - 1];
						for (int j = 0, k = 0; j < items.Length; j++)
						{
							if (j != i) newItems[k++] = items[j];
						}

						if (newItems.Length > 0)
						{
							itemsByName[eventName] = newItems;
							return newItems.Length;
						}

						itemsByName.Remove(eventName);
						if (itemsByName.Count == 0) sItemsByObject.Remove(obj);

						return 0;
					}
				}

				// specified event handler was not registered
				return -1;
			}
		}

		/// <summary>
		/// Unregisters all event handlers from the specified event.
		/// </summary>
		/// <param name="obj">Object providing the event.</param>
		/// <param name="eventName">Name of the event (<c>null</c> to remove all handlers attached to events of the specified object).</param>
		/// <returns>
		/// true, if a least one event handler has been removed;
		/// false, if no event handler was registered.
		/// </returns>
		public static bool UnregisterEventHandlers(object obj, string eventName)
		{
			lock (sSync)
			{
				if (eventName != null)
				{
					// abort, if there is no event handler attached to the specified event
					if (!sItemsByObject.TryGetValue(obj, out var itemsByName) || !itemsByName.TryGetValue(eventName, out _))
						return false;

					// remove all handlers attached to the specified event
					bool removed = itemsByName.Remove(eventName);
					if (itemsByName.Count == 0) sItemsByObject.Remove(obj);
					return removed;
				}

				// remove all handlers attached to events of the specified object
				return sItemsByObject.Remove(obj);
			}
		}

		/// <summary>
		/// Checks whether the specified event has event handlers attached to it.
		/// </summary>
		/// <param name="obj">Object providing the event.</param>
		/// <param name="eventName">Name of the event.</param>
		/// <returns>true, if the specified event has event handlers attached; otherwise false.</returns>
		public static bool IsHandlerRegistered(object obj, string eventName)
		{
			lock (sSync)
			{
				if (!sItemsByObject.TryGetValue(obj, out var itemsByName) || !itemsByName.TryGetValue(eventName, out _))
					return false;

				return true;
			}
		}

		/// <summary>
		/// Checks whether the specified event handler is registered for the specified event.
		/// </summary>
		/// <param name="obj">Object providing the event.</param>
		/// <param name="eventName">Name of the event.</param>
		/// <param name="handler">Event handler to check for.</param>
		/// <returns>true, if the specified event handler is attached to the event; otherwise false.</returns>
		public static bool IsHandlerRegistered(object obj, string eventName, EventHandler<T> handler)
		{
			lock (sSync)
			{
				if (!sItemsByObject.TryGetValue(obj, out var itemsByName) || !itemsByName.TryGetValue(eventName, out var items))
					return false;

				for (int i = 0; i < items.Length; i++)
				{
					if (items[i].Handler == handler)
						return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Fires an event invoking all event handlers that are attached to it (event handlers that are associated
		/// with a synchronization context are executed in the thread the synchronization context belongs to).
		/// </summary>
		/// <param name="obj">Object providing the event.</param>
		/// <param name="eventName">Name of the event.</param>
		/// <param name="sender">Sender object to pass to invoked event handlers.</param>
		/// <param name="e">Event arguments to pass to invoked event handlers.</param>
		public static void FireEvent(
			object obj,
			string eventName,
			object sender,
			T      e)
		{
			Item[] items;

			lock (sSync)
			{
				if (!sItemsByObject.TryGetValue(obj, out var itemsByName)) return;
				if (!itemsByName.TryGetValue(eventName, out items)) return;
			}

			foreach (var item in items)
			{
				if (item.SynchronizationContext != null)
				{
					// synchronization context was specified at registration
					// => invoke the handler directly, if the current context is the same as the context at registration and scheduling is not enforced;
					//    otherwise schedule the handler using the context specified at registration
					if (!item.ScheduleAlways && ReferenceEquals(SynchronizationContext.Current, item.SynchronizationContext)) item.Handler(sender, e);
					else item.SynchronizationContext.Post(x => ((Item)x).Handler(sender, e), item);
				}
				else
				{
					// synchronization context was not specified at registration
					// => schedule handler in worker thread or invoke it directly
					if (item.ScheduleAlways) Task.Run(() => item.Handler(sender, e));
					else item.Handler(sender, e);
				}
			}
		}

		/// <summary>
		/// Gets a multicast delegate that calls all event handlers that are attached to the specified event
		/// (event handlers that are associated with a synchronization context are executed in the thread the
		/// synchronization context belongs to).
		/// </summary>
		/// <param name="obj">Object providing the event.</param>
		/// <param name="eventName">Name of the event.</param>
		/// <returns>Event callers.</returns>
		public static EventHandler<T> GetEventCallers(object obj, string eventName)
		{
			Item[] items;

			lock (sSync)
			{
				if (!sItemsByObject.TryGetValue(obj, out var itemsByName)) return null;
				if (!itemsByName.TryGetValue(eventName, out items)) return null;
			}

			EventHandler<T> handlers = null;

			foreach (var item in items)
			{
				if (item.SynchronizationContext != null)
				{
					handlers += (sender, e) =>
					{
						// synchronization context was specified at registration
						// => invoke the handler directly, if the current context is the same as the context at registration and scheduling is not enforced;
						//    otherwise schedule the handler using the context specified at registration
						if (!item.ScheduleAlways && ReferenceEquals(SynchronizationContext.Current, item.SynchronizationContext)) item.Handler(sender, e);
						else item.SynchronizationContext.Post(x => ((Item)x).Handler(sender, e), item);
					};
				}
				else
				{
					var itemCopy = item;
					handlers += (sender, e) =>
					{
						// synchronization context was not specified at registration
						// => schedule handler in worker thread or invoke it directly
						if (itemCopy.ScheduleAlways) Task.Run(() => itemCopy.Handler(sender, e));
						else itemCopy.Handler(sender, e);
					};
				}
			}

			return handlers;
		}
	}

}
