///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;

using Xunit;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Unit tests targeting the <see cref="LogWriter"/> class.
	/// </summary>
	public class LogWriterTests
	{
		/// <summary>
		/// Tests creating a <see cref="LogWriter"/> via the <see cref="Log.GetWriter(string)"/> method.
		/// </summary>
		[Fact]
		public void Creating_New_LogWriter_By_Name()
		{
			string name = Guid.NewGuid().ToString("D");
			var writer = Log.GetWriter(name);
			Assert.Equal(name, writer.Name);
		}

		public static IEnumerable<object[]> LogWriterCreationTestData1
		{
			get
			{
				// NOTE: The generic GetWriter<>() method does not support generic type definitions
				yield return new object[] { typeof(LogWriterTests), "GriffinPlus.Lib.Logging.LogWriterTests" };
				yield return new object[] { typeof(List<int>), "System.Collections.Generic.List<System.Int32>" };
				yield return new object[] { typeof(Dictionary<int, string>), "System.Collections.Generic.Dictionary<System.Int32,System.String>" };
			}
		}

		/// <summary>
		/// Tests creating a <see cref="LogWriter"/> via the <see cref="Log.GetWriter{Type}"/> method.
		/// </summary>
		/// <param name="type">Type to derive the name of the log writer from.</param>
		/// <param name="expectedName">The expected name of the created log writer.</param>
		[Theory]
		[MemberData(nameof(LogWriterCreationTestData1))]
		public void Creating_New_LogWriter_By_Generic_Type_Parameter(Type type, string expectedName)
		{
			var method = typeof(Log)
				.GetMethods()
				.Single(x => x.Name == nameof(Log.GetWriter) && x.IsGenericMethod && x.GetGenericArguments().Length == 1)
				.MakeGenericMethod(type);

			var writer = (LogWriter)method.Invoke(null, null);
			Assert.Equal(expectedName, writer.Name);
		}

		public static IEnumerable<object[]> LogWriterCreationTestData2
		{
			get
			{
				foreach (var item in LogWriterCreationTestData1) yield return item;
				yield return new object[] { typeof(List<>), "System.Collections.Generic.List<>" };
				yield return new object[] { typeof(Dictionary<,>), "System.Collections.Generic.Dictionary<,>" };
			}
		}

		/// <summary>
		/// Tests creating a <see cref="LogWriter"/> via the <see cref="Log.GetWriter(Type)"/> method.
		/// </summary>
		/// <param name="type">Type to derive the name of the log writer from.</param>
		/// <param name="expectedName">The expected name of the created log writer.</param>
		[Theory]
		[MemberData(nameof(LogWriterCreationTestData2))]
		public void Creating_New_LogWriter_By_Type_Parameter(Type type, string expectedName)
		{
			var writer = Log.GetWriter(type);
			Assert.Equal(expectedName, writer.Name);
		}

		/// <summary>
		/// Tests whether calling <see cref="Log.GetWriter(string)"/> twice returns the same <see cref="LogWriter"/> instance.
		/// </summary>
		[Fact]
		public void LogWriters_Should_be_Singleton_Instances()
		{
			string name = Guid.NewGuid().ToString("D");
			var writer1 = Log.GetWriter(name);
			var writer2 = Log.GetWriter(name);
			Assert.Same(writer1, writer2);
		}

		/// <summary>
		/// Tests creating a tagging <see cref="LogWriter"/> via <see cref="LogWriter.WithTag"/> and checks its integrity.
		/// The tag was not assigned before.
		/// </summary>
		[Fact]
		public void WithTag_TagWasNotAssignedBefore()
		{
			string name = Guid.NewGuid().ToString("D");
			string tag = Guid.NewGuid().ToString("D");
			var writer1 = Log.GetWriter(name);
			Assert.Empty(writer1.Tags);
			var writer2 = writer1.WithTag(tag);
			Assert.NotSame(writer1, writer2);
			Assert.Equal(new TagSet(tag), writer2.Tags);
		}

		/// <summary>
		/// Tests creating a tagging <see cref="LogWriter"/> via <see cref="LogWriter.WithTag"/> and checks its integrity.
		/// The tag was assigned before.
		/// </summary>
		[Fact]
		public void WithTag_TagWasAssignedBefore()
		{
			string name = Guid.NewGuid().ToString("D");
			string tag = Guid.NewGuid().ToString("D");
			var writer1 = Log.GetWriter(name).WithTag(tag);
			Assert.Equal(new TagSet(tag), writer1.Tags);
			var writer2 = writer1.WithTag(tag);
			Assert.Same(writer1, writer2);
			Assert.Equal(new TagSet(tag), writer2.Tags);
		}

		/// <summary>
		/// Tests whether <see cref="LogWriter.WithTag"/> returns the same <see cref="LogWriter"/> instance, if no tags are specified.
		/// </summary>
		[Fact]
		public void WithTag_TagIsNull()
		{
			string name = Guid.NewGuid().ToString("D");
			const string tag = null;
			var writer1 = Log.GetWriter(name);
			Assert.Empty(writer1.Tags);
			var writer2 = writer1.WithTag(tag);
			Assert.Same(writer1, writer2);
			Assert.Empty(writer2.Tags);
		}

		/// <summary>
		/// Tests creating a tagging <see cref="LogWriter"/> via <see cref="LogWriter.WithTags"/> and checks its integrity.
		/// The tags were not assigned before.
		/// </summary>
		[Fact]
		public void WithTags_TagsWereNotAssignedBefore()
		{
			string name = Guid.NewGuid().ToString("D");
			string tag1 = Guid.NewGuid().ToString("D");
			string tag2 = Guid.NewGuid().ToString("D");
			var writer1 = Log.GetWriter(name);
			Assert.Empty(writer1.Tags);
			var writer2 = writer1.WithTags(tag1, tag2);
			Assert.NotSame(writer1, writer2);
			Assert.Equal(new TagSet(tag1, tag2), writer2.Tags);
		}

		/// <summary>
		/// Tests creating a tagging <see cref="LogWriter"/> via <see cref="LogWriter.WithTags"/> and checks its integrity.
		/// The tags were assigned before.
		/// </summary>
		[Fact]
		public void WithTags_TagsWereAssignedBefore()
		{
			string name = Guid.NewGuid().ToString("D");
			string tag1 = Guid.NewGuid().ToString("D");
			string tag2 = Guid.NewGuid().ToString("D");
			var writer1 = Log.GetWriter(name).WithTags(tag1, tag2);
			Assert.Equal(new TagSet(tag1, tag2), writer1.Tags);
			var writer2 = writer1.WithTags(tag1, tag2);
			Assert.Same(writer1, writer2);
			Assert.Equal(new TagSet(tag1, tag2), writer2.Tags);
		}

		/// <summary>
		/// Tests whether <see cref="LogWriter.WithTags"/> returns the same <see cref="LogWriter"/> instance, if no tags (<c>null</c>) are specified.
		/// </summary>
		[Fact]
		public void WithTags_TagsIsNull()
		{
			string name = Guid.NewGuid().ToString("D");
			var writer1 = Log.GetWriter(name);
			Assert.Empty(writer1.Tags);
			var writer2 = writer1.WithTags(null);
			Assert.Same(writer1, writer2);
			Assert.Empty(writer2.Tags);
		}
	}

}
