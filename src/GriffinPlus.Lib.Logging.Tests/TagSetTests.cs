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
	/// Unit tests targeting the <see cref="TagSet"/> class.
	/// </summary>
	public class TagSetTests
	{
		#region Construction

		public static IEnumerable<object[]> CreateTestData
		{
			get
			{
				// empty tag set
				yield return new object[]
				{
					Array.Empty<string>(),
					Array.Empty<string>()
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
			var tagSet = new TagSet(tags);
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
			var tagSet = new TagSet(tags);
			for (int i = 0; i < tags.Length; i++) Assert.Equal(tags[i], tagSet[i]);
		}

		/// <summary>
		/// Tests whether the indexer fails when passing an index that is out of bounds.
		/// </summary>
		[Theory]
		[InlineData("Tag1", "Tag2", "Tag3", "Tag4", "Tag5")] // already ordered
		public void Indexer_IndexOutOfRange(params string[] tags)
		{
			var tagSet = new TagSet(tags);
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
			var tagSet = new TagSet(tags);
			int i = 0;
			using (IEnumerator<string> enumerator = tagSet.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					Assert.Equal(tags[i++], enumerator.Current);
				}
			}
		}

		#endregion

		#region Operator==

		public static IEnumerable<object[]> OperatorEquality_TestData
		{
			get
			{
				// equal
				yield return new object[] { true, null, null };
				yield return new object[] { true, TagSet.Empty, TagSet.Empty };
				yield return new object[] { true, new TagSet("A"), new TagSet("A") };
				yield return new object[] { true, new TagSet("A", "B"), new TagSet("A", "B") };

				// not equal
				yield return new object[] { false, new TagSet("A"), null };
				yield return new object[] { false, new TagSet("A"), TagSet.Empty };
				yield return new object[] { false, null, new TagSet("A") };
				yield return new object[] { false, TagSet.Empty, new TagSet("A") };
				yield return new object[] { false, new TagSet("A"), new TagSet("B") };
				yield return new object[] { false, new TagSet("A"), new TagSet("A", "B") };
				yield return new object[] { false, new TagSet("A", "B"), new TagSet("A") };
			}
		}

		/// <summary>
		/// Tests whether operator== works properly.
		/// </summary>
		[Theory]
		[MemberData(nameof(OperatorEquality_TestData))]
		public void OperatorEquality(bool expected, TagSet left, TagSet right)
		{
			bool isEqual = left == right;
			Assert.Equal(expected, isEqual);
		}

		#endregion

		#region Operator!=

		public static IEnumerable<object[]> OperatorInequality_TestData
		{
			get
			{
				foreach (object[] data in OperatorEquality_TestData)
				{
					yield return new[] { !(bool)data[0], data[1], data[2] };
				}
			}
		}

		/// <summary>
		/// Tests whether operator!= works properly.
		/// </summary>
		[Theory]
		[MemberData(nameof(OperatorInequality_TestData))]
		public void OperatorInequality(bool expected, TagSet left, TagSet right)
		{
			bool isEqual = left != right;
			Assert.Equal(expected, isEqual);
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
					Array.Empty<string>(),
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
			var left = new TagSet(tags);
			left += right;
			Assert.Equal(expected, left.ToArray());
		}

		/// <summary>
		/// Tests whether operator+ fails, if the right operand is <c>null</c>.
		/// </summary>
		[Fact]
		public void OperatorPlus_WithSingleTag_TagIsNull()
		{
			var left = new TagSet();
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
					Array.Empty<string>(),
					Array.Empty<string>(),
					Array.Empty<string>()
				};

				// add tag to empty tag set
				yield return new object[]
				{
					Array.Empty<string>(),
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
			var left = new TagSet(tags);
			left += right;
			Assert.Equal(expected, left.ToArray());
		}

		/// <summary>
		/// Tests whether operator+ fails, if the right operand is <c>null</c>.
		/// </summary>
		[Fact]
		public void OperatorPlus_WithMultipleTags_TagsIsNull()
		{
			var left = new TagSet();
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
					Array.Empty<string>(),
					"Tag",
					Array.Empty<string>()
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
			var left = new TagSet(tags);
			left -= right;
			Assert.Equal(expected, left.ToArray());
		}

		/// <summary>
		/// Tests whether operator- fails, if the right operand is <c>null</c>.
		/// </summary>
		[Fact]
		public void OperatorMinus_WithSingleTag_TagIsNull()
		{
			var left = new TagSet();
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
					Array.Empty<string>(),
					Array.Empty<string>(),
					Array.Empty<string>()
				};

				// remove tag from empty tag set
				yield return new object[]
				{
					Array.Empty<string>(),
					new[] { "Tag" },
					Array.Empty<string>()
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
			var left = new TagSet(tags);
			left -= right;
			Assert.Equal(expected, left.ToArray());
		}

		/// <summary>
		/// Tests whether operator- fails, if the right operand is <c>null</c>.
		/// </summary>
		[Fact]
		public void OperatorMinus_WithMultipleTags_TagsIsNull()
		{
			var left = new TagSet();
			string[] right = null;
			Assert.Throws<ArgumentNullException>(() => left - right);
		}

		#endregion
	}

}
