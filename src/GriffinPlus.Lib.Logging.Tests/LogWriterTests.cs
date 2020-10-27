///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2018-2020 Sascha Falk <sascha@falk-online.eu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Linq;
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
			LogWriter writer = Log.GetWriter(name);
			Assert.Equal(name, writer.Name);
		}

		[Fact]
		public void Creating_New_LogWriter_By_Type_Parameter()
		{
			LogWriter writer = Log.GetWriter(typeof(LogWriterTests));
			Assert.Equal(typeof(LogWriterTests).FullName, writer.Name);
		}

		[Fact]
		public void Creating_New_LogWriter_By_Generic_Type_Parameter()
		{
			LogWriter writer = Log.GetWriter<LogWriterTests>();
			Assert.Equal(typeof(LogWriterTests).FullName, writer.Name);
		}

		[Fact]
		public void LogWriters_Should_be_Singleton_Instances()
		{
			string name = Guid.NewGuid().ToString("D");
			LogWriter writer1 = Log.GetWriter(name);
			LogWriter writer2 = Log.GetWriter(name);
			Assert.Same(writer1, writer2);
		}

		[Fact]
		public void WithTag_TagWasNotAssignedBefore()
		{
			string name = Guid.NewGuid().ToString("D");
			string tag = Guid.NewGuid().ToString("D");
			LogWriter writer1 = Log.GetWriter(name);
			Assert.Empty(writer1.Tags);
			LogWriter writer2 = writer1.WithTag(tag);
			Assert.NotSame(writer1, writer2);
			Assert.Equal(new TagSet(tag), writer2.Tags);
		}

		[Fact]
		public void WithTag_TagWasAssignedBefore()
		{
			string name = Guid.NewGuid().ToString("D");
			string tag = Guid.NewGuid().ToString("D");
			LogWriter writer1 = Log.GetWriter(name).WithTag(tag);
			Assert.Equal(new TagSet(tag), writer1.Tags);
			LogWriter writer2 = writer1.WithTag(tag);
			Assert.Same(writer1, writer2);
			Assert.Equal(new TagSet(tag), writer2.Tags);
		}

		[Fact]
		public void WithTag_TagIsNull()
		{
			string name = Guid.NewGuid().ToString("D");
			const string tag = null;
			LogWriter writer1 = Log.GetWriter(name);
			Assert.Empty(writer1.Tags);
			LogWriter writer2 = writer1.WithTag(tag);
			Assert.Same(writer1, writer2);
			Assert.Empty(writer2.Tags);
		}

		[Fact]
		public void WithTags_TagsWereNotAssignedBefore()
		{
			string name = Guid.NewGuid().ToString("D");
			string tag1 = Guid.NewGuid().ToString("D");
			string tag2 = Guid.NewGuid().ToString("D");
			LogWriter writer1 = Log.GetWriter(name);
			Assert.Empty(writer1.Tags);
			LogWriter writer2 = writer1.WithTags(tag1, tag2);
			Assert.NotSame(writer1, writer2);
			Assert.Equal(new TagSet(tag1, tag2), writer2.Tags);
		}

		[Fact]
		public void WithTags_TagsWereAssignedBefore()
		{
			string name = Guid.NewGuid().ToString("D");
			string tag1 = Guid.NewGuid().ToString("D");
			string tag2 = Guid.NewGuid().ToString("D");
			LogWriter writer1 = Log.GetWriter(name).WithTags(tag1, tag2);
			Assert.Equal(new TagSet(tag1, tag2), writer1.Tags);
			LogWriter writer2 = writer1.WithTags(tag1, tag2);
			Assert.Same(writer1, writer2);
			Assert.Equal(new TagSet(tag1, tag2), writer2.Tags);
		}

		[Fact]
		public void WithTags_TagsIsNull()
		{
			string name = Guid.NewGuid().ToString("D");
			LogWriter writer1 = Log.GetWriter(name);
			Assert.Empty(writer1.Tags);
			LogWriter writer2 = writer1.WithTags(null);
			Assert.Same(writer1, writer2);
			Assert.Empty(writer2.Tags);
		}

	}
}


