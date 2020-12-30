///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using GriffinPlus.Lib.Threading;

using Xunit;

using static GriffinPlus.Lib.Expressions.LambdaExpressionInliners;

// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local
// ReSharper disable UnusedParameter.Local
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Unit tests targeting the <see cref="LogMessage" /> class.
	/// </summary>
	public class LogMessageTests : IDisposable
	{
		#region Test Setup/Teardown

		private AsyncContextThread mThread = new AsyncContextThread();

		/// <summary>
		/// Cleans up.
		/// </summary>
		public void Dispose()
		{
			if (mThread != null)
			{
				mThread.Dispose();
				mThread = null;
			}
		}

		#endregion

		#region Construction

		/// <summary>
		/// Tests creating a new log message using the default constructor.
		/// </summary>
		[Fact]
		private void Create_Default()
		{
			var message = new LogMessage();
			CheckDefaultState(message);
		}

		/// <summary>
		/// Tests creating a new log message and associating it with a pool.
		/// </summary>
		[Fact]
		private void Create_Pooled()
		{
			var pool = new LogMessagePool();
			var message = new LogMessage(pool);
			CheckDefaultState(message, pool);
		}

		#endregion

		#region Message Properties

		public static IEnumerable<object[]> MessagePropertyTestData
		{
			get
			{
				foreach (bool protect in new[] { false, true })
				{
					yield return new object[] { EXPR<LogMessage, object>(x => x.Id), -1L, 1L, protect };
					yield return new object[] { EXPR<LogMessage, object>(x => x.LostMessageCount), 0, 1, protect };
					yield return new object[] { EXPR<LogMessage, object>(x => x.Timestamp), default(DateTimeOffset), DateTimeOffset.Now, protect };
					yield return new object[] { EXPR<LogMessage, object>(x => x.HighPrecisionTimestamp), 0L, 1L, protect };
					yield return new object[] { EXPR<LogMessage, object>(x => x.LogWriterName), null, "A Log Writer", protect };
					yield return new object[] { EXPR<LogMessage, object>(x => x.LogLevelName), null, "A Log Level", protect };
					yield return new object[] { EXPR<LogMessage, object>(x => x.Tags), null, new TagSet("Tag"), protect };
					yield return new object[] { EXPR<LogMessage, object>(x => x.ApplicationName), null, "Application Name", protect };
					yield return new object[] { EXPR<LogMessage, object>(x => x.ProcessName), null, "Process Name", protect };
					yield return new object[] { EXPR<LogMessage, object>(x => x.ProcessId), -1, 1, protect };
					yield return new object[] { EXPR<LogMessage, object>(x => x.Text), null, "Some Text", protect };
				}

				// the IsReadOnly property is tested as part of the other test cases with protect = true
				// as Protect() sets this property to true and raises the PropertyChanged event.
			}
		}

		/// <summary>
		/// Tests getting message specific properties.
		/// </summary>
		[Theory]
		[MemberData(nameof(MessagePropertyTestData))]
		private void MessageProperty_Get(
			Expression<Func<LogMessage, object>> property,
			object                               defaultValue,
			object                               newValue,
			bool                                 protect)
		{
			TestPropertyGetter(property, defaultValue, protect);
		}

		/// <summary>
		/// Tests setting message specific properties without registering an
		/// </summary>
		[Theory]
		[MemberData(nameof(MessagePropertyTestData))]
		private void MessageProperty_Set_WithoutPropertyChanged(
			Expression<Func<LogMessage, object>> property,
			object                               defaultValue,
			object                               newValue,
			bool                                 protect)
		{
			TestPropertySetter_WithoutPropertyChanged(property, defaultValue, newValue, protect);
		}

		/// <summary>
		/// Tests setting message specific properties.
		/// </summary>
		[Theory]
		[MemberData(nameof(MessagePropertyTestData))]
		private async Task MessageProperty_Set_WithPropertyChanged_ChangeInSameThread(
			Expression<Func<LogMessage, object>> property,
			object                               defaultValue,
			object                               newValue,
			bool                                 protect)
		{
			await TestPropertySetter_WithPropertyChanged(property, defaultValue, newValue, protect, true);
		}

		/// <summary>
		/// Tests setting message specific properties.
		/// </summary>
		[Theory]
		[MemberData(nameof(MessagePropertyTestData))]
		private async Task MessageProperty_Set_WithPropertyChanged_ChangeInDifferentThread(
			Expression<Func<LogMessage, object>> property,
			object                               defaultValue,
			object                               newValue,
			bool                                 protect)
		{
			await TestPropertySetter_WithPropertyChanged(property, defaultValue, newValue, protect, false);
		}

		/// <summary>
		/// Tests getting a property of the <see cref="LogMessage" /> class.
		/// </summary>
		/// <param name="property">Property to test.</param>
		/// <param name="expectedDefaultValue">Expected default value of the property.</param>
		/// <param name="protect">true to protect the log message before setting the property; otherwise false.</param>
		private static void TestPropertyGetter(
			Expression<Func<LogMessage, object>> property,
			object                               expectedDefaultValue,
			bool                                 protect)
		{
			var message = new LogMessage();

			// extract information about the property to work with
			PropertyInfo propertyInfo;
			switch (property.Body.NodeType)
			{
				case ExpressionType.MemberAccess:
					propertyInfo = (PropertyInfo)((MemberExpression)property.Body).Member;
					break;

				case ExpressionType.Convert:
					propertyInfo = (PropertyInfo)((MemberExpression)((UnaryExpression)property.Body).Operand).Member;
					break;

				default:
					throw new NotImplementedException();
			}

			// invoke the getter of the property and compare to the expected default value
			var value = propertyInfo.GetValue(message);
			Assert.Equal(expectedDefaultValue, value);
		}

		/// <summary>
		/// Tests setting a property of the <see cref="LogMessage" /> class.
		/// </summary>
		/// <param name="property">Property to test.</param>
		/// <param name="expectedDefaultValue">Expected default value of the property.</param>
		/// <param name="valueToSet">Value of the property after setting it.</param>
		/// <param name="protect">true to protect the log message before setting the property; otherwise false.</param>
		private static void TestPropertySetter_WithoutPropertyChanged(
			Expression<Func<LogMessage, object>> property,
			object                               expectedDefaultValue,
			object                               valueToSet,
			bool                                 protect)
		{
			var message = new LogMessage();

			// extract information about the property to work with
			PropertyInfo propertyInfo;
			switch (property.Body.NodeType)
			{
				case ExpressionType.MemberAccess:
					propertyInfo = (PropertyInfo)((MemberExpression)property.Body).Member;
					break;

				case ExpressionType.Convert:
					propertyInfo = (PropertyInfo)((MemberExpression)((UnaryExpression)property.Body).Operand).Member;
					break;

				default:
					throw new NotImplementedException();
			}

			var value = propertyInfo.GetValue(message);
			Assert.Equal(expectedDefaultValue, value);

			if (protect)
			{
				message.Protect();

				var ex = Assert.Throws<TargetInvocationException>(() => propertyInfo.SetValue(message, valueToSet, null));
				Assert.IsType<NotSupportedException>(ex.InnerException);
				value = propertyInfo.GetValue(message);
				Assert.Equal(expectedDefaultValue, value);
			}
			else
			{
				propertyInfo.SetValue(message, valueToSet, null);
				value = propertyInfo.GetValue(message);
				if (propertyInfo.PropertyType.IsValueType) Assert.Equal(valueToSet, value);
				else Assert.Same(valueToSet, value);
			}
		}

		/// <summary>
		/// Tests setting a property of the <see cref="LogMessage" /> class.
		/// </summary>
		/// <param name="property">Property to test.</param>
		/// <param name="expectedDefaultValue">Expected default value of the property.</param>
		/// <param name="valueToSet">Value of the property after setting it.</param>
		/// <param name="protect">true to protect the log message before setting the property; otherwise false.</param>
		/// <param name="changeInSameThread">
		/// true to change the property in the thread that registers the event;
		/// false to change the property and raise the event in some other thread.
		/// </param>
		private async Task TestPropertySetter_WithPropertyChanged(
			Expression<Func<LogMessage, object>> property,
			object                               expectedDefaultValue,
			object                               valueToSet,
			bool                                 protect,
			bool                                 changeInSameThread)
		{
			var message = new LogMessage();

			// extract information about the property to work with
			PropertyInfo propertyInfo;
			switch (property.Body.NodeType)
			{
				case ExpressionType.MemberAccess:
					propertyInfo = (PropertyInfo)((MemberExpression)property.Body).Member;
					break;

				case ExpressionType.Convert:
					propertyInfo = (PropertyInfo)((MemberExpression)((UnaryExpression)property.Body).Operand).Member;
					break;

				default:
					throw new NotImplementedException();
			}

			// check default value of the property
			var value = propertyInfo.GetValue(message);
			Assert.Equal(expectedDefaultValue, value);

			// prepare data pulling some information out of the event handler
			SynchronizationContext handlerThreadSynchronizationContext = null;
			var changedPropertyNames = new List<string>();
			var handlerCalledEvent = new ManualResetEventSlim(false);

			// the handler that is expected to be called on changes
			void PropertyChangedHandler(object sender, PropertyChangedEventArgs e)
			{
				handlerThreadSynchronizationContext = SynchronizationContext.Current;
				changedPropertyNames.Add(e.PropertyName);
				handlerCalledEvent.Set();
			}

			// run test in a separate thread that provides a synchronization context that allows to
			// marshal calls into that thread
			await mThread.Factory.Run(() => { Assert.NotNull(SynchronizationContext.Current); });

			// register the PropertyChanged event
			await mThread.Factory.Run(() => { message.PropertyChanged += PropertyChangedHandler; });

			// set property
			if (changeInSameThread)
			{
				// registering the event and changing the property is done in the same thread
				// => the event handler should be called directly

				await mThread.Factory.Run(
					() =>
					{
						if (protect)
						{
							// protect the message
							// => changes the IsReadOnly property to true
							// => event handler is called directly
							message.Protect();
							Assert.True(handlerCalledEvent.IsSet, "The event handler should have been called directly.");
							Assert.Same(SynchronizationContext.Current, handlerThreadSynchronizationContext);
							Assert.Equal(new[] { "IsReadOnly" }, changedPropertyNames.ToArray());
							handlerCalledEvent.Reset();

							// the message is protected and setting a property should throw an exception
							var ex = Assert.Throws<TargetInvocationException>(() => propertyInfo.SetValue(message, valueToSet, null));
							Assert.IsType<NotSupportedException>(ex.InnerException);

							// the property value should not have changed
							value = propertyInfo.GetValue(message);
							Assert.Equal(expectedDefaultValue, value);

							// the event handler should not have been called directly
							Assert.False(handlerCalledEvent.IsSet);
						}
						else
						{
							// set property
							propertyInfo.SetValue(message, valueToSet, null);

							// the property value should not be the same as the set value 
							value = propertyInfo.GetValue(message);
							if (propertyInfo.PropertyType.IsValueType) Assert.Equal(valueToSet, value);
							else Assert.Same(valueToSet, value);

							// the event handler should have been called directly
							Assert.True(handlerCalledEvent.IsSet);
							Assert.Same(SynchronizationContext.Current, handlerThreadSynchronizationContext);
							Assert.Equal(new[] { propertyInfo.Name }, changedPropertyNames.ToArray());
						}
					});
			}
			else
			{
				// the thread registering the event is different from the thread changing the property and raising the event
				// => the event handler should be scheduled to run on the thread that has registered the event

				if (protect)
				{
					// protect the message
					// => changes the IsReadOnly property to true
					// => event handler should have been scheduled to run
					message.Protect();
					Assert.True(handlerCalledEvent.Wait(1000));
					Assert.Same(mThread.Context.SynchronizationContext, handlerThreadSynchronizationContext);
					Assert.Equal(new[] { "IsReadOnly" }, changedPropertyNames.ToArray());
					handlerCalledEvent.Reset();

					// the message is protected and setting a property should throw an exception
					var ex = Assert.Throws<TargetInvocationException>(() => propertyInfo.SetValue(message, valueToSet, null));
					Assert.IsType<NotSupportedException>(ex.InnerException);

					// the property value should not have changed
					value = propertyInfo.GetValue(message);
					Assert.Equal(expectedDefaultValue, value);

					// the event handler should neither have been called directly nor should it be scheduled to run
					Assert.False(handlerCalledEvent.Wait(1000));
				}
				else
				{
					// set property
					propertyInfo.SetValue(message, valueToSet, null);

					// the property value should not be the same as the set value 
					value = propertyInfo.GetValue(message);
					if (propertyInfo.PropertyType.IsValueType) Assert.Equal(valueToSet, value);
					else Assert.Same(valueToSet, value);

					// the event handler should run in the context of the thread that registered the event
					Assert.True(handlerCalledEvent.Wait(1000));
					Assert.Same(mThread.Context.SynchronizationContext, handlerThreadSynchronizationContext);
					Assert.Equal(new[] { propertyInfo.Name }, changedPropertyNames.ToArray());
				}
			}

			// unregister the PropertyChanged event
			await mThread.Factory.Run(() => { message.PropertyChanged -= PropertyChangedHandler; });
		}

		#endregion

		#region InitWith()

		/// <summary>
		/// Tests <see cref="LogMessage.InitWith" />.
		/// </summary>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		private void InitWith(bool withPropertyChanged)
		{
			// create a new message
			var message = new LogMessage();
			Assert.False(message.IsReadOnly);

			// prepare data pulling some information out of the event handler
			var changedPropertyNames = new List<string>();

			// the handler that is expected to be called on changes
			void PropertyChangedHandler(object sender, PropertyChangedEventArgs e)
			{
				changedPropertyNames.Add(e.PropertyName);
			}

			// register event handler, if required
			if (withPropertyChanged) message.PropertyChanged += PropertyChangedHandler;

			// init the message
			message.InitWith(
				1000,
				DateTimeOffset.Now,
				1000,
				1000,
				"My Log Writer",
				"My Log Level",
				new TagSet("ABC"),
				"My Application",
				"My Process",
				42,
				"Some Text");

			// the event handler should have been called only once
			// (the event handler is always called directly, if the registering thread is the same as the thread raising the event)
			if (withPropertyChanged)
			{
				Assert.Equal(new string[] { null }, changedPropertyNames.ToArray()); // null => all properties
			}
		}

		#endregion

		#region Reset to Defaults

		/// <summary>
		/// Tests resetting a log message to defaults using <see cref="LogMessage.Reset" />.
		/// Used internally by the pool.
		/// </summary>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		private void Reset(bool withPropertyChanged)
		{
			var message = new LogMessage
			{
				Id = 0,
				LostMessageCount = 0,
				Timestamp = DateTimeOffset.Parse("2020-01-01T12:00:00+01:00"),
				HighPrecisionTimestamp = 0,
				LogWriterName = "Log Writer",
				LogLevelName = "Log Level",
				Tags = new TagSet("Tag"),
				ApplicationName = "Application",
				ProcessName = "Process",
				ProcessId = 0,
				Text = "Text"
			};

			// prepare data pulling some information out of the event handler
			var handlerCalledEvent = new ManualResetEventSlim(false);

			// the handler that is expected to be called on changes
			void PropertyChangedHandler(object sender, PropertyChangedEventArgs e)
			{
				handlerCalledEvent.Set();
			}

			// register event handler, if required
			if (withPropertyChanged)
			{
				message.PropertyChanged += PropertyChangedHandler;

				// check whether the corresponding event manager reflects the registration	
				Assert.True(PropertyChangedEventManager.IsHandlerRegistered(message));
			}

			// reset the log message
			message.Reset();

			// the event handler should not have been called although properties have been changed
			// (the event handler is always called directly, if the registering thread is the same as the thread raising the event)
			if (withPropertyChanged)
			{
				Assert.False(handlerCalledEvent.IsSet);

				// resetting the message should also remove any event handler
				Assert.False(PropertyChangedEventManager.IsHandlerRegistered(message));
			}

			// the log message should be in the same state as a freshly created one now
			CheckDefaultState(message);
		}

		#endregion

		#region Write Protection

		/// <summary>
		/// Tests whether <see cref="LogMessage.Protect" /> sets the <see cref="LogMessage.IsReadOnly" /> property to <c>true</c>.
		/// </summary>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		private void Protect(bool withPropertyChanged)
		{
			// create a new message
			var message = new LogMessage();
			Assert.False(message.IsReadOnly);

			// prepare data pulling some information out of the event handler
			var changedPropertyNames = new List<string>();
			var handlerCalledEvent = new ManualResetEventSlim(false);

			// the handler that is expected to be called on changes
			void PropertyChangedHandler(object sender, PropertyChangedEventArgs e)
			{
				changedPropertyNames.Add(e.PropertyName);
				handlerCalledEvent.Set();
			}

			// register event handler, if required
			if (withPropertyChanged) message.PropertyChanged += PropertyChangedHandler;

			// protect the message
			message.Protect();

			// the IsReadOnly property should be true now
			Assert.True(message.IsReadOnly);

			// the event handler should have been called
			// (the event handler is always called directly, if the registering thread is the same as the thread raising the event)
			if (withPropertyChanged)
			{
				Assert.True(handlerCalledEvent.IsSet);
				Assert.Equal(new[] { "IsReadOnly" }, changedPropertyNames.ToArray());
			}
		}

		// NOTE: the effect of protecting the log message is tested in the tests for property getters/setters above.

		#endregion

		#region GetHashCode() / Equals()

		public static IEnumerable<object[]> EqualityTestData
		{
			get
			{
				var message = new LogMessage
				{
					Id = 0,
					LostMessageCount = 0,
					Timestamp = DateTimeOffset.Parse("2020-01-01T12:00:00+01:00"),
					HighPrecisionTimestamp = 0,
					LogWriterName = "Log Writer",
					LogLevelName = "Log Level",
					Tags = new TagSet("Tag"),
					ApplicationName = "Application",
					ProcessName = "Process",
					ProcessId = 0,
					Text = "Text"
				};

				yield return new object[] { message, EXPR<LogMessage, object>(x => x.Id), -10L };
				yield return new object[] { message, EXPR<LogMessage, object>(x => x.Id), 11L };
				yield return new object[] { message, EXPR<LogMessage, object>(x => x.LostMessageCount), -10 };
				yield return new object[] { message, EXPR<LogMessage, object>(x => x.LostMessageCount), 10 };
				yield return new object[] { message, EXPR<LogMessage, object>(x => x.Timestamp), DateTimeOffset.Parse("2020-01-02T12:00:00+01:00") };
				yield return new object[] { message, EXPR<LogMessage, object>(x => x.HighPrecisionTimestamp), -10L };
				yield return new object[] { message, EXPR<LogMessage, object>(x => x.HighPrecisionTimestamp), 10L };
				yield return new object[] { message, EXPR<LogMessage, object>(x => x.LogWriterName), "Another Log Writer" };
				yield return new object[] { message, EXPR<LogMessage, object>(x => x.LogWriterName), null };
				yield return new object[] { message, EXPR<LogMessage, object>(x => x.LogLevelName), "Another Log Level" };
				yield return new object[] { message, EXPR<LogMessage, object>(x => x.LogLevelName), null };
				yield return new object[] { message, EXPR<LogMessage, object>(x => x.Tags), new TagSet("AnotherTag") };
				yield return new object[] { message, EXPR<LogMessage, object>(x => x.Tags), null };
				yield return new object[] { message, EXPR<LogMessage, object>(x => x.ApplicationName), "Another Application" };
				yield return new object[] { message, EXPR<LogMessage, object>(x => x.ApplicationName), null };
				yield return new object[] { message, EXPR<LogMessage, object>(x => x.ProcessName), "Another Process Name" };
				yield return new object[] { message, EXPR<LogMessage, object>(x => x.ProcessName), null };
				yield return new object[] { message, EXPR<LogMessage, object>(x => x.ProcessId), -10 };
				yield return new object[] { message, EXPR<LogMessage, object>(x => x.ProcessId), 10 };
				yield return new object[] { message, EXPR<LogMessage, object>(x => x.Text), "Some other Text" };
				yield return new object[] { message, EXPR<LogMessage, object>(x => x.Text), null };
			}
		}

		/// <summary>
		/// Tests <see cref="LogMessage.GetHashCode" /> by comparing the hash code of the specified message with
		/// a copy of the message with a certain property set to the specified value.
		/// </summary>
		/// <param name="message">Message to work with.</param>
		/// <param name="property">Property to set.</param>
		/// <param name="valueToSet">Value to set the property to.</param>
		[Theory]
		[MemberData(nameof(EqualityTestData))]
		private void GetHashCode_DifferenceOnChangedProperty(
			LogMessage                           message,
			Expression<Func<LogMessage, object>> property,
			object                               valueToSet)
		{
			// create a copy of the message using the copy constructor
			// => the copy should have the same hash code
			int messageHashCode = message.GetHashCode();
			var otherMessage = new LogMessage(message);
			int otherMessageHashCode = otherMessage.GetHashCode();
			Assert.Equal(messageHashCode, otherMessageHashCode);

			// set property on the message copy
			SetProperty(otherMessage, property, valueToSet);

			// the hash code should be different now
			otherMessageHashCode = otherMessage.GetHashCode();
			Assert.NotEqual(messageHashCode, otherMessageHashCode);
		}

		/// <summary>
		/// Tests <see cref="LogMessage.Equals(GriffinPlus.Lib.Logging.ILogMessage)" /> by comparing the specified message with a copy of the message
		/// with a certain property set to the specified value.
		/// </summary>
		/// <param name="message">Message to work with.</param>
		/// <param name="property">Property to set.</param>
		/// <param name="valueToSet">Value to set the property to.</param>
		[Theory]
		[MemberData(nameof(EqualityTestData))]
		private void Equals_DifferenceOnPropertyChange(
			LogMessage                           message,
			Expression<Func<LogMessage, object>> property,
			object                               valueToSet)
		{
			// create a copy of the message using the copy constructor
			// => the copy should equal the message
			var otherMessage = new LogMessage(message);
			bool isEqual = message.Equals(otherMessage);
			Assert.True(isEqual);

			// set property on the message copy
			SetProperty(otherMessage, property, valueToSet);

			// the messages should be different now

			// check using Equals(LogMessage other)
			isEqual = message.Equals(otherMessage);
			Assert.False(isEqual);

			// check using Equals(ILogMessage other)
			isEqual = message.Equals((ILogMessage)otherMessage);
			Assert.False(isEqual);
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Tests whether the specified log message has the expected default state.
		/// </summary>
		/// <param name="message">Log message to check.</param>
		/// <param name="pool">Pool the log message belongs to (null, if the message is not pooled).</param>
		private static void CheckDefaultState(LogMessage message, LogMessagePool pool = null)
		{
			Assert.Equal(-1, message.Id);
			Assert.Equal(0, message.LostMessageCount);
			Assert.Equal(default, message.Timestamp);
			Assert.Equal(0, message.HighPrecisionTimestamp);
			Assert.Null(message.LogWriterName);
			Assert.Null(message.LogLevelName);
			Assert.Null(message.Tags);
			Assert.Null(message.ApplicationName);
			Assert.Null(message.ProcessName);
			Assert.Equal(-1, message.ProcessId);
			Assert.Null(message.Text);
			Assert.False(message.IsReadOnly);
			Assert.Same(pool, message.Pool);
			Assert.Equal(1, message.RefCount);
		}

		/// <summary>
		/// Sets the specified property of the <see cref="LogMessage" /> class to the specified value.
		/// </summary>
		/// <param name="message">Log message to modify.</param>
		/// <param name="property">Property to test.</param>
		/// <param name="valueToSet">Value of the property after setting it.</param>
		private static void SetProperty(
			LogMessage                           message,
			Expression<Func<LogMessage, object>> property,
			object                               valueToSet)
		{
			// extract information about the property to work with
			PropertyInfo propertyInfo;
			switch (property.Body.NodeType)
			{
				case ExpressionType.MemberAccess:
					propertyInfo = (PropertyInfo)((MemberExpression)property.Body).Member;
					break;

				case ExpressionType.Convert:
					propertyInfo = (PropertyInfo)((MemberExpression)((UnaryExpression)property.Body).Operand).Member;
					break;

				default:
					throw new NotImplementedException();
			}

			// set property
			propertyInfo.SetValue(message, valueToSet, null);
		}

		#endregion
	}

}
