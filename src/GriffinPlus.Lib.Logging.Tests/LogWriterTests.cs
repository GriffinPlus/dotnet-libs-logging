﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

using Xunit;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Unit tests targeting the <see cref="LogWriter"/> class.
	/// </summary>
	public class LogWriterTests
	{
		[Fact]
		public void Creating_New_LogWriter_By_Name()
		{
			string name = Guid.NewGuid().ToString("D");
			var writer = Log.GetWriter(name);
			Assert.Equal(name, writer.Name);
		}

		[Fact]
		public void Creating_New_LogWriter_By_Type_Parameter()
		{
			var writer = Log.GetWriter(typeof(LogWriterTests));
			Assert.Equal(typeof(LogWriterTests).FullName, writer.Name);
		}

		[Fact]
		public void Creating_New_LogWriter_By_Generic_Type_Parameter()
		{
			var writer = Log.GetWriter<LogWriterTests>();
			Assert.Equal(typeof(LogWriterTests).FullName, writer.Name);
		}

		[Fact]
		public void LogWriters_Should_be_Singleton_Instances()
		{
			string name = Guid.NewGuid().ToString("D");
			var writer1 = Log.GetWriter(name);
			var writer2 = Log.GetWriter(name);
			Assert.Same(writer1, writer2);
		}

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
