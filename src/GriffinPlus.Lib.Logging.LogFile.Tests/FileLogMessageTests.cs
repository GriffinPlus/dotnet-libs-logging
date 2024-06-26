﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
// ReSharper disable UseCollectionExpression

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Unit tests targeting the <see cref="LogFileMessage"/> class.
/// </summary>
public class FileLogMessageTests : IDisposable
{
	#region Test Setup/Teardown

	private AsyncContextThread mThread = new();

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
		var message = new LogFileMessage();
		CheckDefaultState(message);
	}

	#endregion

	#region CreateWithAsyncInit()

	/// <summary>
	/// Tests creating a log message that must be initialized asynchronously, but does not initialize it.
	/// </summary>
	/// <param name="readOnly">
	/// <c>true</c> to create a read-only message;<br/>
	/// <c>false</c> to create a regular message.
	/// </param>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	private void CreateWithAsyncInit_CreateOnly(bool readOnly)
	{
		// create a new message with asynchronous initializer
		var message = LogFileMessage.CreateWithAsyncInit(readOnly, out IFileLogMessageInitializer _);
		CheckDefaultState(message, false, readOnly);

		// check that the message is marked for asynchronous initialization
		Assert.True(message.IsAsyncInitPending);

		// check whether the message reflects the desired read-only state
		Assert.Equal(readOnly, message.IsReadOnly);
	}

	/// <summary>
	/// Test data for <see cref="CreateWithAsyncInit_FollowedByInitialize"/>.
	/// </summary>
	public static IEnumerable<object[]> CreateWithAsyncInitTestData_FollowedByInitialize
	{
		get
		{
			return from readOnly in new[] { false, true }
			       from initInSameThread in new[] { false, true }
			       from withPropertyChanged in new[] { false, true }
			       select (object[])[readOnly, initInSameThread, withPropertyChanged];
		}
	}

	/// <summary>
	/// Tests creating a log message that must be initialized asynchronously abd initializes it.
	/// </summary>
	/// <param name="readOnly">
	/// <c>true</c> to create a read-only message;<br/>
	/// <c>false</c> to create a regular message.
	/// </param>
	/// <param name="initInSameThread">
	/// <c>true</c> to initialize the message in the thread that registers the event;<br/>
	/// <c>false</c> to initialize the message and raise the event in some other thread.
	/// </param>
	/// <param name="withPropertyChanged">
	/// <c>true</c> to register the <see cref="INotifyPropertyChanged.PropertyChanged"/> event and check whether it is fired correctly;<br/>
	/// otherwise <c>false</c>.
	/// </param>
	[Theory]
	[MemberData(nameof(CreateWithAsyncInitTestData_FollowedByInitialize))]
	private async Task CreateWithAsyncInit_FollowedByInitialize(bool readOnly, bool initInSameThread, bool withPropertyChanged)
	{
		// create a new message with asynchronous initializer
		var message = LogFileMessage.CreateWithAsyncInit(readOnly, out IFileLogMessageInitializer initializer);
		CheckDefaultState(message, false, readOnly); // checks IsInitialized

		// check that the message is marked for asynchronous initialization
		Assert.True(message.IsAsyncInitPending);

		// prepare data pulling some information out of the event handler
		SynchronizationContext handlerThreadSynchronizationContext = null;
		var changedPropertyNames = new List<string>();
		var handlerCalledEvent = new ManualResetEventSlim(false);

		// run test in a separate thread that provides a synchronization context that allows to
		// marshal calls into that thread
		await mThread.Factory.Run(() => { Assert.NotNull(SynchronizationContext.Current); });

		// register the PropertyChanged event
		if (withPropertyChanged)
		{
			await mThread.Factory.Run(() => { message.PropertyChanged += PropertyChangedHandler; });
		}

		// initialize the message either in the context of the thread that registered the handler
		// or in a different - the current - thread
		if (initInSameThread) await mThread.Factory.Run(InitializeTest);
		else InitializeTest();

		if (!initInSameThread)
		{
			// the thread registering the event and the thread initializing the message are different
			if (withPropertyChanged)
			{
				// event handler should run in the context of the thread that registered it
				Assert.True(handlerCalledEvent.Wait(1000));
				Assert.Same(mThread.Context.SynchronizationContext, handlerThreadSynchronizationContext);
				Assert.Equal([null], changedPropertyNames.ToArray());
			}
			else
			{
				// the event handler should not have been called
				Assert.False(handlerCalledEvent.Wait(1000));
			}
		}

		return;

		// callback that initializes the message
		void InitializeTest()
		{
			initializer.Initialize(
				1,
				DateTimeOffset.Parse("2020-01-01T12:00:00+01:00"),
				2,
				3,
				"Log Writer",
				"Log Level",
				new TagSet("Tag"),
				"Application",
				"Process",
				42,
				"Some text");

			// check administrative properties
			Assert.True(message.IsInitialized);
			Assert.False(message.IsAsyncInitPending);

			// check message properties
			Assert.Equal(1, message.Id);
			Assert.Equal(DateTimeOffset.Parse("2020-01-01T12:00:00+01:00"), message.Timestamp);
			Assert.Equal(2, message.HighPrecisionTimestamp);
			Assert.Equal(3, message.LostMessageCount);
			Assert.Equal("Log Writer", message.LogWriterName);
			Assert.Equal("Log Level", message.LogLevelName);
			Assert.Equal(new TagSet("Tag"), message.Tags);
			Assert.Equal("Application", message.ApplicationName);
			Assert.Equal("Process", message.ProcessName);
			Assert.Equal(42, message.ProcessId);
			Assert.Equal("Some text", message.Text);

			if (initInSameThread)
			{
				if (withPropertyChanged)
				{
					// the event handler should have been called only once in the same thread
					// (the event handler is called directly as the registering thread is the same as the thread raising the event)
					Assert.True(handlerCalledEvent.IsSet);
					Assert.Equal([null], changedPropertyNames.ToArray()); // null => all properties
				}
				else
				{
					// the event handler should not have been called
					Assert.False(handlerCalledEvent.IsSet);
				}
			}
		}

		// the handler that is expected to be called on changes
		void PropertyChangedHandler(object sender, PropertyChangedEventArgs e)
		{
			handlerThreadSynchronizationContext = SynchronizationContext.Current;
			changedPropertyNames.Add(e.PropertyName);
			handlerCalledEvent.Set();
		}
	}

	#endregion

	#region InitWith()

	/// <summary>
	/// Test data for <see cref="InitWith"/>.
	/// </summary>
	public static IEnumerable<object[]> InitWithTestData
	{
		get
		{
			return from initInSameThread in new[] { false, true }
			       from withPropertyChanged in new[] { false, true }
			       select (object[])[initInSameThread, withPropertyChanged];
		}
	}

	/// <summary>
	/// Tests
	/// <see cref="LogFileMessage.InitWith(long,System.DateTimeOffset,long,int,string,string,GriffinPlus.Lib.Logging.TagSet,string,string,int,string)"/>.
	/// </summary>
	/// <param name="initInSameThread">
	/// <c>true</c> to initialize the message in the thread that registers the event;<br/>
	/// <c>false</c> to initialize the message and raise the event in some other thread.
	/// </param>
	/// <param name="withPropertyChanged">
	/// <c>true</c> to register the <see cref="INotifyPropertyChanged.PropertyChanged"/> event and check whether it is fired correctly;<br/>
	/// otherwise <c>false</c>.
	/// </param>
	[Theory]
	[MemberData(nameof(InitWithTestData))]
	private async Task InitWith(bool initInSameThread, bool withPropertyChanged)
	{
		// create a new message
		var message = new LogFileMessage();
		Assert.False(message.IsReadOnly);
		CheckDefaultState(message, true, false);

		// ensure that the message is not marked for asynchronous initialization
		Assert.False(message.IsAsyncInitPending);

		// prepare data pulling some information out of the event handler
		SynchronizationContext handlerThreadSynchronizationContext = null;
		var changedPropertyNames = new List<string>();
		var handlerCalledEvent = new ManualResetEventSlim(false);

		// run test in a separate thread that provides a synchronization context that allows to
		// marshal calls into that thread
		await mThread.Factory.Run(() => { Assert.NotNull(SynchronizationContext.Current); });

		// register the PropertyChanged event
		if (withPropertyChanged)
		{
			await mThread.Factory.Run(() => { message.PropertyChanged += PropertyChangedHandler; });
		}

		// initialize the message either in the context of the thread that registered the handler
		// or in a different - the current - thread
		if (initInSameThread) await mThread.Factory.Run(InitializeTest);
		else InitializeTest();

		if (!initInSameThread)
		{
			// the thread registering the event and the thread initializing the message are different
			if (withPropertyChanged)
			{
				// event handler should run in the context of the thread that registered it
				Assert.True(handlerCalledEvent.Wait(1000));
				Assert.Same(mThread.Context.SynchronizationContext, handlerThreadSynchronizationContext);
				Assert.Equal([null], changedPropertyNames.ToArray());
			}
			else
			{
				// the event handler should not have been called
				Assert.False(handlerCalledEvent.Wait(1000));
			}
		}

		return;

		// callback that initializes the message
		void InitializeTest()
		{
			// init the message
			message.InitWith(
				1,
				DateTimeOffset.Parse("2020-01-01T12:00:00+01:00"),
				2,
				3,
				"Log Writer",
				"Log Level",
				new TagSet("Tag"),
				"Application",
				"Process",
				42,
				"Some Text");

			// check administrative properties
			Assert.True(message.IsInitialized);
			Assert.False(message.IsAsyncInitPending);

			// check message properties
			Assert.Equal(1, message.Id);
			Assert.Equal(DateTimeOffset.Parse("2020-01-01T12:00:00+01:00"), message.Timestamp);
			Assert.Equal(2, message.HighPrecisionTimestamp);
			Assert.Equal(3, message.LostMessageCount);
			Assert.Equal("Log Writer", message.LogWriterName);
			Assert.Equal("Log Level", message.LogLevelName);
			Assert.Equal(new TagSet("Tag"), message.Tags);
			Assert.Equal("Application", message.ApplicationName);
			Assert.Equal("Process", message.ProcessName);
			Assert.Equal(42, message.ProcessId);
			Assert.Equal("Some Text", message.Text);

			if (initInSameThread)
			{
				if (withPropertyChanged)
				{
					// the event handler should have been called only once in the same thread
					// (the event handler is called directly as the registering thread is the same as the thread raising the event)
					Assert.True(handlerCalledEvent.IsSet);
					Assert.Equal([null], changedPropertyNames.ToArray()); // null => all properties
				}
				else
				{
					// the event handler should not have been called
					Assert.False(handlerCalledEvent.IsSet);
				}
			}
		}

		// the handler that is expected to be called on changes
		void PropertyChangedHandler(object sender, PropertyChangedEventArgs e)
		{
			handlerThreadSynchronizationContext = SynchronizationContext.Current;
			changedPropertyNames.Add(e.PropertyName);
			handlerCalledEvent.Set();
		}
	}

	#endregion

	#region Message Properties

	public static IEnumerable<object[]> MessagePropertyTestData
	{
		get
		{
			foreach (bool protect in new[] { false, true })
			{
				yield return [EXPR<LogFileMessage, object>(x => x.Id), -1L, 1L, protect];
				yield return [EXPR<LogFileMessage, object>(x => x.LostMessageCount), 0, 1, protect];
				yield return [EXPR<LogFileMessage, object>(x => x.Timestamp), default(DateTimeOffset), DateTimeOffset.Now, protect];
				yield return [EXPR<LogFileMessage, object>(x => x.HighPrecisionTimestamp), 0L, 1L, protect];
				yield return [EXPR<LogFileMessage, object>(x => x.LogWriterName), null, "A Log Writer", protect];
				yield return [EXPR<LogFileMessage, object>(x => x.LogLevelName), null, "A Log Level", protect];
				yield return [EXPR<LogFileMessage, object>(x => x.Tags), null, new TagSet("Tag"), protect];
				yield return [EXPR<LogFileMessage, object>(x => x.ApplicationName), null, "Application Name", protect];
				yield return [EXPR<LogFileMessage, object>(x => x.ProcessName), null, "Process Name", protect];
				yield return [EXPR<LogFileMessage, object>(x => x.ProcessId), -1, 1, protect];
				yield return [EXPR<LogFileMessage, object>(x => x.Text), null, "Some Text", protect];
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
		Expression<Func<LogFileMessage, object>> property,
		object                                   defaultValue,
		object                                   newValue,
		bool                                     protect)
	{
		TestPropertyGetter(property, defaultValue, protect);
	}

	/// <summary>
	/// Tests setting message specific properties without registering an
	/// </summary>
	[Theory]
	[MemberData(nameof(MessagePropertyTestData))]
	private void MessageProperty_Set_WithoutPropertyChanged(
		Expression<Func<LogFileMessage, object>> property,
		object                                   defaultValue,
		object                                   newValue,
		bool                                     protect)
	{
		TestPropertySetter_WithoutPropertyChanged(property, defaultValue, newValue, protect);
	}

	/// <summary>
	/// Tests setting message specific properties.
	/// </summary>
	[Theory]
	[MemberData(nameof(MessagePropertyTestData))]
	private async Task MessageProperty_Set_WithPropertyChanged_ChangeInSameThread(
		Expression<Func<LogFileMessage, object>> property,
		object                                   defaultValue,
		object                                   newValue,
		bool                                     protect)
	{
		await TestPropertySetter_WithPropertyChanged(property, defaultValue, newValue, protect, true);
	}

	/// <summary>
	/// Tests setting message specific properties.
	/// </summary>
	[Theory]
	[MemberData(nameof(MessagePropertyTestData))]
	private async Task MessageProperty_Set_WithPropertyChanged_ChangeInDifferentThread(
		Expression<Func<LogFileMessage, object>> property,
		object                                   defaultValue,
		object                                   newValue,
		bool                                     protect)
	{
		await TestPropertySetter_WithPropertyChanged(property, defaultValue, newValue, protect, false);
	}

	/// <summary>
	/// Tests getting a property of the <see cref="LogMessage"/> class.
	/// </summary>
	/// <param name="property">Property to test.</param>
	/// <param name="expectedDefaultValue">Expected default value of the property.</param>
	/// <param name="protect">
	/// <c>true</c> to protect the log message before setting the property;<br/>
	/// otherwise <c>false</c>.
	/// </param>
	private static void TestPropertyGetter(
		Expression<Func<LogFileMessage, object>> property,
		object                                   expectedDefaultValue,
		bool                                     protect)
	{
		var message = new LogFileMessage();

		// extract information about the property to work with
		// ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
		PropertyInfo propertyInfo = property.Body.NodeType switch
		{
			ExpressionType.MemberAccess => (PropertyInfo)((MemberExpression)property.Body).Member,
			ExpressionType.Convert      => (PropertyInfo)((MemberExpression)((UnaryExpression)property.Body).Operand).Member,
			var _                       => throw new NotImplementedException()
		};

		// invoke the getter of the property and compare to the expected default value
		object value = propertyInfo.GetValue(message);
		Assert.Equal(expectedDefaultValue, value);
	}

	/// <summary>
	/// Tests setting a property of the <see cref="LogFileMessage"/> class.
	/// </summary>
	/// <param name="property">Property to test.</param>
	/// <param name="expectedDefaultValue">Expected default value of the property.</param>
	/// <param name="valueToSet">Value of the property after setting it.</param>
	/// <param name="protect">
	/// <c>true</c> to protect the log message before setting the property;<br/>
	/// otherwise <c>false</c>.
	/// </param>
	private static void TestPropertySetter_WithoutPropertyChanged(
		Expression<Func<LogFileMessage, object>> property,
		object                                   expectedDefaultValue,
		object                                   valueToSet,
		bool                                     protect)
	{
		var message = new LogFileMessage();

		// extract information about the property to work with
		// ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
		PropertyInfo propertyInfo = property.Body.NodeType switch
		{
			ExpressionType.MemberAccess => (PropertyInfo)((MemberExpression)property.Body).Member,
			ExpressionType.Convert      => (PropertyInfo)((MemberExpression)((UnaryExpression)property.Body).Operand).Member,
			var _                       => throw new NotImplementedException()
		};

		object value = propertyInfo.GetValue(message);
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
	/// Tests setting a property of the <see cref="LogFileMessage"/> class.
	/// </summary>
	/// <param name="property">Property to test.</param>
	/// <param name="expectedDefaultValue">Expected default value of the property.</param>
	/// <param name="valueToSet">Value of the property after setting it.</param>
	/// <param name="protect">
	/// <c>true</c> to protect the log message before setting the property;<br/>
	/// otherwise <c>false</c>.
	/// </param>
	/// <param name="changeInSameThread">
	/// <c>true</c> to change the property in the thread that registers the event;<br/>
	/// <c>false</c> to change the property and raise the event in some other thread.
	/// </param>
	private async Task TestPropertySetter_WithPropertyChanged(
		Expression<Func<LogFileMessage, object>> property,
		object                                   expectedDefaultValue,
		object                                   valueToSet,
		bool                                     protect,
		bool                                     changeInSameThread)
	{
		var message = new LogFileMessage();

		// extract information about the property to work with
		// ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
		PropertyInfo propertyInfo = property.Body.NodeType switch
		{
			ExpressionType.MemberAccess => (PropertyInfo)((MemberExpression)property.Body).Member,
			ExpressionType.Convert      => (PropertyInfo)((MemberExpression)((UnaryExpression)property.Body).Operand).Member,
			var _                       => throw new NotImplementedException()
		};

		// check default value of the property
		object value = propertyInfo.GetValue(message);
		Assert.Equal(expectedDefaultValue, value);

		// prepare data pulling some information out of the event handler
		SynchronizationContext handlerThreadSynchronizationContext = null;
		var changedPropertyNames = new List<string>();
		var handlerCalledEvent = new ManualResetEventSlim(false);

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

		return;

		// the handler that is expected to be called on changes
		void PropertyChangedHandler(object sender, PropertyChangedEventArgs e)
		{
			handlerThreadSynchronizationContext = SynchronizationContext.Current;
			changedPropertyNames.Add(e.PropertyName);
			handlerCalledEvent.Set();
		}
	}

	#endregion

	#region Write Protection

	/// <summary>
	/// Test data for <see cref="Protect"/>.
	/// </summary>
	public static IEnumerable<object[]> ProtectTestData
	{
		get
		{
			return from initInSameThread in new[] { false, true }
			       from withPropertyChanged in new[] { false, true }
			       select (object[])[initInSameThread, withPropertyChanged];
		}
	}

	/// <summary>
	/// Tests whether <see cref="LogMessage.Protect"/> sets the <see cref="LogMessage.IsReadOnly"/> property to <c>true</c>.
	/// </summary>
	/// <param name="protectInSameThread">
	/// <c>true</c> to protect the message in the thread that registers the event;<br/>
	/// <c>false</c> to protect the message and raise the event in some other thread.
	/// </param>
	/// <param name="withPropertyChanged">
	/// <c>true</c> to register the <see cref="LogMessage.PropertyChanged"/> event and check whether it is fired correctly;<br/>
	/// otherwise <c>false</c>.
	/// </param>
	[Theory]
	[MemberData(nameof(InitWithTestData))]
	private async Task Protect(bool protectInSameThread, bool withPropertyChanged)
	{
		// create a new message
		var message = new LogFileMessage();
		Assert.False(message.IsReadOnly);
		CheckDefaultState(message, true, false);

		// ensure that the message is not marked for asynchronous initialization
		Assert.False(message.IsAsyncInitPending);

		// prepare data pulling some information out of the event handler
		SynchronizationContext handlerThreadSynchronizationContext = null;
		var changedPropertyNames = new List<string>();
		var handlerCalledEvent = new ManualResetEventSlim(false);

		// run test in a separate thread that provides a synchronization context that allows to
		// marshal calls into that thread
		await mThread.Factory.Run(() => { Assert.NotNull(SynchronizationContext.Current); });

		// register the PropertyChanged event
		if (withPropertyChanged)
		{
			await mThread.Factory.Run(() => { message.PropertyChanged += PropertyChangedHandler; });
		}

		// protect the message either in the context of the thread that registered the handler
		// or in a different - the current - thread
		if (protectInSameThread) await mThread.Factory.Run(ProtectTest);
		else ProtectTest();

		if (!protectInSameThread)
		{
			// the thread registering the event and the thread protecting the message are different
			if (withPropertyChanged)
			{
				// event handler should run in the context of the thread that registered it
				Assert.True(handlerCalledEvent.Wait(1000));
				Assert.Same(mThread.Context.SynchronizationContext, handlerThreadSynchronizationContext);
				Assert.Equal(new[] { "IsReadOnly" }, changedPropertyNames.ToArray());
			}
			else
			{
				// the event handler should not have been called
				Assert.False(handlerCalledEvent.Wait(1000));
			}
		}

		return;

		// callback that initializes the message
		void ProtectTest()
		{
			// protect the message
			message.Protect();

			// check administrative properties
			Assert.True(message.IsInitialized);       // unchanged
			Assert.False(message.IsAsyncInitPending); // unchanged
			Assert.True(message.IsReadOnly);

			if (protectInSameThread)
			{
				if (withPropertyChanged)
				{
					// the event handler should have been called only once in the same thread
					// (the event handler is called directly as the registering thread is the same as the thread raising the event)
					Assert.True(handlerCalledEvent.IsSet);
					Assert.Equal(new[] { "IsReadOnly" }, changedPropertyNames.ToArray());
				}
				else
				{
					// the event handler should not have been called
					Assert.False(handlerCalledEvent.IsSet);
				}
			}
		}

		// the handler that is expected to be called on changes
		void PropertyChangedHandler(object sender, PropertyChangedEventArgs e)
		{
			handlerThreadSynchronizationContext = SynchronizationContext.Current;
			changedPropertyNames.Add(e.PropertyName);
			handlerCalledEvent.Set();
		}
	}

	// NOTE: the effect of protecting the log message is tested in the tests for property getters/setters above.

	#endregion

	#region GetHashCode() / Equals()

	public static IEnumerable<object[]> EqualityTestData
	{
		get
		{
			var message = new LogFileMessage
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

			yield return [message, EXPR<LogFileMessage, object>(x => x.Id), -10L];
			yield return [message, EXPR<LogFileMessage, object>(x => x.Id), 11L];
			yield return [message, EXPR<LogFileMessage, object>(x => x.LostMessageCount), -10];
			yield return [message, EXPR<LogFileMessage, object>(x => x.LostMessageCount), 10];
			yield return [message, EXPR<LogFileMessage, object>(x => x.Timestamp), DateTimeOffset.Parse("2020-01-02T12:00:00+01:00")];
			yield return [message, EXPR<LogFileMessage, object>(x => x.HighPrecisionTimestamp), -10L];
			yield return [message, EXPR<LogFileMessage, object>(x => x.HighPrecisionTimestamp), 10L];
			yield return [message, EXPR<LogFileMessage, object>(x => x.LogWriterName), "Another Log Writer"];
			yield return [message, EXPR<LogFileMessage, object>(x => x.LogWriterName), null];
			yield return [message, EXPR<LogFileMessage, object>(x => x.LogLevelName), "Another Log Level"];
			yield return [message, EXPR<LogFileMessage, object>(x => x.LogLevelName), null];
			yield return [message, EXPR<LogFileMessage, object>(x => x.Tags), new TagSet("AnotherTag")];
			yield return [message, EXPR<LogFileMessage, object>(x => x.Tags), null];
			yield return [message, EXPR<LogFileMessage, object>(x => x.ApplicationName), "Another Application"];
			yield return [message, EXPR<LogFileMessage, object>(x => x.ApplicationName), null];
			yield return [message, EXPR<LogFileMessage, object>(x => x.ProcessName), "Another Process Name"];
			yield return [message, EXPR<LogFileMessage, object>(x => x.ProcessName), null];
			yield return [message, EXPR<LogFileMessage, object>(x => x.ProcessId), -10];
			yield return [message, EXPR<LogFileMessage, object>(x => x.ProcessId), 10];
			yield return [message, EXPR<LogFileMessage, object>(x => x.Text), "Some other Text"];
			yield return [message, EXPR<LogFileMessage, object>(x => x.Text), null];
		}
	}

	/// <summary>
	/// Tests <see cref="LogMessage.GetHashCode"/> by comparing the hash code of the specified message with
	/// a copy of the message with a certain property set to the specified value.
	/// </summary>
	/// <param name="message">Message to work with.</param>
	/// <param name="property">Property to set.</param>
	/// <param name="valueToSet">Value to set the property to.</param>
	[Theory]
	[MemberData(nameof(EqualityTestData))]
	private void GetHashCode_DifferenceOnChangedProperty(
		LogFileMessage                           message,
		Expression<Func<LogFileMessage, object>> property,
		object                                   valueToSet)
	{
		// create a copy of the message using the copy constructor
		// => the copy should have the same hash code
		int messageHashCode = message.GetHashCode();
		var otherMessage = new LogFileMessage(message);
		int otherMessageHashCode = otherMessage.GetHashCode();
		Assert.Equal(messageHashCode, otherMessageHashCode);

		// set property on the message copy
		SetProperty(otherMessage, property, valueToSet);

		// the hash code should be different now
		otherMessageHashCode = otherMessage.GetHashCode();
		Assert.NotEqual(messageHashCode, otherMessageHashCode);
	}

	/// <summary>
	/// Tests <see cref="LogMessage.Equals(GriffinPlus.Lib.Logging.ILogMessage)"/> by comparing the specified message with a copy of the message
	/// with a certain property set to the specified value.
	/// </summary>
	/// <param name="message">Message to work with.</param>
	/// <param name="property">Property to set.</param>
	/// <param name="valueToSet">Value to set the property to.</param>
	[Theory]
	[MemberData(nameof(EqualityTestData))]
	private void Equals_DifferenceOnPropertyChange(
		LogFileMessage                           message,
		Expression<Func<LogFileMessage, object>> property,
		object                                   valueToSet)
	{
		// create a copy of the message using the copy constructor
		// => the copy should equal the message
		var otherMessage = new LogFileMessage(message);
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
	/// <param name="inited">
	/// <c>true</c> if the message is initialized;<br/>
	/// otherwise <c>false</c>.
	/// </param>
	/// <param name="readOnly">
	/// <c>true</c> if the message is read-only;<br/>
	/// otherwise <c>false</c>.
	/// </param>
	private static void CheckDefaultState(
		LogFileMessage message,
		bool           inited   = true,
		bool           readOnly = false)
	{
		// check administrative properties
		Assert.Equal(inited, message.IsInitialized);
		Assert.Equal(readOnly, message.IsReadOnly);

		// check message specific properties
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
	}

	/// <summary>
	/// Sets the specified property of the <see cref="LogFileMessage"/> class to the specified value.
	/// </summary>
	/// <param name="message">Log message to modify.</param>
	/// <param name="property">Property to test.</param>
	/// <param name="valueToSet">Value of the property after setting it.</param>
	private static void SetProperty(
		LogFileMessage                           message,
		Expression<Func<LogFileMessage, object>> property,
		object                                   valueToSet)
	{
		// extract information about the property to work with
		// ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
		PropertyInfo propertyInfo = property.Body.NodeType switch
		{
			ExpressionType.MemberAccess => (PropertyInfo)((MemberExpression)property.Body).Member,
			ExpressionType.Convert      => (PropertyInfo)((MemberExpression)((UnaryExpression)property.Body).Operand).Member,
			var _                       => throw new NotImplementedException()
		};

		// set property
		propertyInfo.SetValue(message, valueToSet, null);
	}

	#endregion
}
