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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Unit tests targeting the <see cref="TagSet"/> class.
	/// </summary>
	public class TagSetTests
	{
		const string ValidCharSet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.,:;+-#()[]{}<>";

		#region Construction

		public static IEnumerable<object[]> CreateTestData
		{
			get
			{
				// empty tag set
				yield return new object[]
				{
					new string[0],
					new string[0]
				};

				// single element in the tag set
				yield return new object[]
				{
					new[] { "Tag" },
					new[] { "Tag" }
				};

				// mixed set of unordered elements
				yield return new object[]
				{
					new[] { "A", "C", "D", "B", "E", "e", "d", "c", "b", "a" },
					new[] { "A", "a", "B", "b", "C", "c", "D", "d", "E", "e" }
				};

				// mixed set with duplicates
				yield return new object[]
				{
					new[] { "A", "B", "C", "D", "E", "A", "B", "C", "D", "E" },
					new[] { "A", "B", "C", "D", "E" }
				};

			}
		}

		/// <summary>
		/// Tests whether the constructor succeeds creating a tag set with valid parameters.
		/// </summary>
		/// <param name="tags">Tags to pass to the constructor.</param>
		/// <param name="expected">The expected tags in the tag set.</param>
		[Theory]
		[MemberData(nameof(CreateTestData))]
		public void Create_Success(string[] tags, string[] expected)
		{
			TagSet tagSet = new TagSet(tags);
			Assert.Equal(expected.Length, tagSet.Count);
			Assert.Equal(expected, tagSet.ToArray());
		}

		/// <summary>
		/// Tests whether the constructor fails when passing a null reference.
		/// </summary>
		[Fact]
		public void Create_TagsIsNull()
		{
			Assert.Throws<ArgumentNullException>(() => new TagSet(null));
		}

		#endregion

		#region Indexer

		/// <summary>
		/// Tests whether the indexer is working properly.
		/// </summary>
		[Theory]
		[InlineData("Tag1", "Tag2", "Tag3", "Tag4", "Tag5")] // already ordered
		public void Indexer_Success(params string[] tags)
		{
			TagSet tagSet = new TagSet(tags);
			for (int i = 0; i < tags.Length; i++) Assert.Equal(tags[i], tagSet[i]);
		}

		/// <summary>
		/// Tests whether the indexer fails when passing an index that is out of bounds.
		/// </summary>
		[Theory]
		[InlineData("Tag1", "Tag2", "Tag3", "Tag4", "Tag5")] // already ordered
		public void Indexer_IndexOutOfRange(params string[] tags)
		{
			TagSet tagSet = new TagSet(tags);
			Assert.Throws<IndexOutOfRangeException>(() => tagSet[-1]);          // below lower bound
			Assert.Throws<IndexOutOfRangeException>(() => tagSet[tags.Length]); // above upper bound
		}

		#endregion

		#region GetEnumerator()

		/// <summary>
		/// Tests whether the enumerator is working properly.
		/// </summary>
		[Theory]
		[InlineData("Tag1", "Tag2", "Tag3", "Tag4", "Tag5")] // already ordered
		public void GetEnumerator(params string[] tags)
		{
			TagSet tagSet = new TagSet(tags);
			int i = 0;
			using (var enumerator = tagSet.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					Assert.Equal(tags[i++], enumerator.Current);
				}
			}
		}

		#endregion

		#region CheckTag()

		/// <summary>
		/// Generates a random set of valid tags and tests whether all tags pass the <see cref="TagSet.CheckTag"/> method.
		/// </summary>
		[Fact]
		public void CheckTag_Valid()
		{
			StringBuilder builder = new StringBuilder();
			Random random = new Random(0);
			for (int sample = 0; sample < 10000; sample++)
			{
				builder.Clear();

				// generate a valid tag
				int length = random.Next(1, 50);
				for (int i = 0; i < length; i++)
				{
					int j = random.Next(0, ValidCharSet.Length - 1);
					builder.Append(ValidCharSet[j]);
				}

				// check the tag
				string tag = builder.ToString();
				TagSet.CheckTag(tag);
			}
		}

		/// <summary>
		/// Generates a random set of invalid tags and tests whether all tags let the <see cref="TagSet.CheckTag"/> method
		/// throw an exception.
		/// </summary>
		[Fact]
		public void CheckTag_Invalid()
		{
			StringBuilder builder = new StringBuilder();
			Random random = new Random(0);
			for (int sample = 0; sample < 10000; sample++)
			{
				builder.Clear();

				// generate a valid tag
				int length = random.Next(1, 50);
				for (int i = 0; i < length; i++)
				{
					int j = random.Next(0, ValidCharSet.Length - 1);
					builder.Append(ValidCharSet[j]);
				}

				// find a character that is not valid and inject it into the tag
				char c;
				do { c = (char)random.Next(0, 65535); } while (ValidCharSet.Contains(c));
				int index = random.Next(0, builder.Length-1);
				builder.Insert(index, c);

				// check the tag
				string tag = builder.ToString();
				Assert.Throws<ArgumentException>(() => TagSet.CheckTag(tag));
			}
		}
		
		#endregion

		#region Operator+

		public static IEnumerable<object[]> OperatorPlus_WithSingleTag_TestData
		{
			get
			{
				// add tag to empty tag set
				yield return new object[]
				{
					new string[0],
					"Tag",
					new[] { "Tag" }
				};

				// add tag to non-empty tag set
				yield return new object[]
				{
					new[] { "A", "B", "C", "E", "F", "G" },
					"D",
					new[] { "A", "B", "C", "D", "E", "F", "G" }
				};
			}
		}

		/// <summary>
		/// Tests whether operator+ works properly with a single tag on the right side.
		/// </summary>
		[Theory]
		[MemberData(nameof(OperatorPlus_WithSingleTag_TestData))]
		public void OperatorPlus_WithSingleTag_Success(string[] tags, string right, string[] expected)
		{
			TagSet left = new TagSet(tags);
			left = left + right;
			Assert.Equal(expected, left.ToArray());
		}

		/// <summary>
		/// Tests whether operator+ fails, if the right operand is null.
		/// </summary>
		[Fact]
		public void OperatorPlus_WithSingleTag_TagIsNull()
		{
			TagSet left = new TagSet();
			string right = null;
			Assert.Throws<ArgumentNullException>(() => left + right);
		}

		public static IEnumerable<object[]> OperatorPlus_WithMultipleTags_TestData
		{
			get
			{
				// add no tags to empty tag set
				yield return new object[]
				{
					new string[0],
					new string[0],
					new string[0],
				};

				// add tag to empty tag set
				yield return new object[]
				{
					new string[0],
					new[] { "Tag" },
					new[] { "Tag" }
				};

				// add tag to non-empty tag set
				yield return new object[]
				{
					new[] { "A", "B", "C", "E", "F", "G" },
					new[] { "D" },
					new[] { "A", "B", "C", "D", "E", "F", "G" }
				};

				// add tags to non-empty tag set
				yield return new object[]
				{
					new[] { "A", "B", "C", "E", "F", "G" },
					new[] { "D", "H" },
					new[] { "A", "B", "C", "D", "E", "F", "G", "H" }
				};
			}
		}

		/// <summary>
		/// Tests whether operator+ works properly with a a tag set on the right side.
		/// </summary>
		[Theory]
		[MemberData(nameof(OperatorPlus_WithMultipleTags_TestData))]
		public void OperatorPlus_WithMultipleTags_Success(string[] tags, string[] right, string[] expected)
		{
			TagSet left = new TagSet(tags);
			left = left + right;
			Assert.Equal(expected, left.ToArray());
		}

		/// <summary>
		/// Tests whether operator+ fails, if the right operand is null.
		/// </summary>
		[Fact]
		public void OperatorPlus_WithMultipleTags_TagsIsNull()
		{
			TagSet left = new TagSet();
			string[] right = null;
			Assert.Throws<ArgumentNullException>(() => left + right);
		}

		#endregion

		#region Operator-

		public static IEnumerable<object[]> OperatorMinus_WithSingleTag_TestData
		{
			get
			{
				// remove tag from empty tag set
				yield return new object[]
				{
					new string[0],
					"Tag",
					new string[0]
				};

				// remove tag from non-empty tag set
				yield return new object[]
				{
					new[] { "A", "B", "C", "D", "E", "F" },
					"C",
					new[] { "A", "B", "D", "E", "F" }
				};
			}
		}

		/// <summary>
		/// Tests whether operator- works properly with a single tag on the right side.
		/// </summary>
		[Theory]
		[MemberData(nameof(OperatorMinus_WithSingleTag_TestData))]
		public void OperatorMinus_WithSingleTag_Success(string[] tags, string right, string[] expected)
		{
			TagSet left = new TagSet(tags);
			left = left - right;
			Assert.Equal(expected, left.ToArray());
		}

		/// <summary>
		/// Tests whether operator- fails, if the right operand is null.
		/// </summary>
		[Fact]
		public void OperatorMinus_WithSingleTag_TagIsNull()
		{
			TagSet left = new TagSet();
			string right = null;
			Assert.Throws<ArgumentNullException>(() => left - right);
		}

		public static IEnumerable<object[]> OperatorMinus_WithMultipleTags_TestData
		{
			get
			{
				// remove no tags from empty tag set
				yield return new object[]
				{
					new string[0],
					new string[0],
					new string[0]
				};

				// remove tag from empty tag set
				yield return new object[]
				{
					new string[0],
					new[] { "Tag" },
					new string[0]
				};

				// remove tag from non-empty tag set
				yield return new object[]
				{
					new[] { "A", "B", "C", "E", "F", "G" },
					new[] { "D" },
					new[] { "A", "B", "C", "E", "F", "G" }
				};

				// remove multiple tags from non-empty tag set
				yield return new object[]
				{
					new[] { "A", "B", "C", "D", "E", "F", "G" },
					new[] { "C", "D" },
					new[] { "A", "B", "E", "F", "G" }
				};
			}
		}

		/// <summary>
		/// Tests whether operator- works properly with a a tag set on the right side.
		/// </summary>
		[Theory]
		[MemberData(nameof(OperatorMinus_WithMultipleTags_TestData))]
		public void OperatorMinus_WithMultipleTags_Success(string[] tags, string[] right, string[] expected)
		{
			TagSet left = new TagSet(tags);
			left = left - right;
			Assert.Equal(expected, left.ToArray());
		}

		/// <summary>
		/// Tests whether operator- fails, if the right operand is null.
		/// </summary>
		[Fact]
		public void OperatorMinus_WithMultipleTags_TagsIsNull()
		{
			TagSet left = new TagSet();
			string[] right = null;
			Assert.Throws<ArgumentNullException>(() => left - right);
		}

		#endregion
	}
}