///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;

using Xunit;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Unit tests targeting the <see cref="TagSet"/> class.
/// </summary>
public class TagSetTests
{
	#region Construction

	/// <summary>
	/// Test data for creating TagSet instances (input tags => expected normalized tags).
	/// </summary>
	public static TheoryData<string[], string[]> CreateTestData
	{
		get
		{
			var data = new TheoryData<string[], string[]>();

			// empty tag set
			data.Add([], []);

			// single element in the tag set
			data.Add(["Tag"], ["Tag"]);

			// mixed set of unordered elements
			data.Add(
				["A", "C", "D", "B", "E", "e", "d", "c", "b", "a"],
				["A", "a", "B", "b", "C", "c", "D", "d", "E", "e"]);

			// mixed set with duplicates
			data.Add(
				["A", "B", "C", "D", "E", "A", "B", "C", "D", "E"],
				["A", "B", "C", "D", "E"]);

			return data;
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
		using IEnumerator<string> enumerator = tagSet.GetEnumerator();
		while (enumerator.MoveNext())
		{
			Assert.Equal(tags[i++], enumerator.Current);
		}
	}

	#endregion

	#region Operator==

	/// <summary>
	/// Test data for TagSet equality operator (==).
	/// </summary>
	public static TheoryData<bool, TagSet, TagSet> OperatorEquality_TestData
	{
		get
		{
			var data = new TheoryData<bool, TagSet, TagSet>();

			// equal
			data.Add(true, null, null);
			data.Add(true, TagSet.Empty, TagSet.Empty);
			data.Add(true, new TagSet("A"), new TagSet("A"));
			data.Add(true, new TagSet("A", "B"), new TagSet("A", "B"));

			// not equal
			data.Add(false, new TagSet("A"), null);
			data.Add(false, new TagSet("A"), TagSet.Empty);
			data.Add(false, null, new TagSet("A"));
			data.Add(false, TagSet.Empty, new TagSet("A"));
			data.Add(false, new TagSet("A"), new TagSet("B"));
			data.Add(false, new TagSet("A"), new TagSet("A", "B"));
			data.Add(false, new TagSet("A", "B"), new TagSet("A"));

			return data;
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

	/// <summary>
	/// Test data for TagSet inequality operator (!=) derived from equality cases.
	/// </summary>
	public static TheoryData<bool, TagSet, TagSet> OperatorInequality_TestData
	{
		get
		{
			var data = new TheoryData<bool, TagSet, TagSet>();

			// OperatorEquality_TestData enumerates as object[]; keep the original pattern but with strong output types.
			foreach (object[] entry in OperatorEquality_TestData)
			{
				bool areEqual = (bool)entry[0]!;
				var left = (TagSet)entry[1];
				var right = (TagSet)entry[2];

				data.Add(!areEqual, left, right);
			}

			return data;
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

	/// <summary>
	/// Test data for TagSet plus operator with a single tag (base + tagToAdd = expected).
	/// </summary>
	public static TheoryData<string[], string, string[]> OperatorPlus_WithSingleTag_TestData
	{
		get
		{
			var data = new TheoryData<string[], string, string[]>();

			// add tag to empty tag set
			data.Add([], "Tag", ["Tag"]);

			// add tag to non-empty tag set
			data.Add(["A", "B", "C", "E", "F", "G"], "D", ["A", "B", "C", "D", "E", "F", "G"]);

			return data;
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
	/// Tests whether operator+ fails, if the right operand is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void OperatorPlus_WithSingleTag_TagIsNull()
	{
		var left = new TagSet();
		string right = null;
		Assert.Throws<ArgumentNullException>(() => left + right);
	}

	/// <summary>
	/// Test data for TagSet plus operator with multiple tags (base + tagsToAdd = expected).
	/// </summary>
	public static TheoryData<string[], string[], string[]> OperatorPlus_WithMultipleTags_TestData
	{
		get
		{
			var data = new TheoryData<string[], string[], string[]>();

			// add no tags to empty tag set
			data.Add([], [], []);

			// add tag to empty tag set
			data.Add([], ["Tag"], ["Tag"]);

			// add tag to non-empty tag set
			data.Add(["A", "B", "C", "E", "F", "G"], ["D"], ["A", "B", "C", "D", "E", "F", "G"]);

			// add tags to non-empty tag set
			data.Add(["A", "B", "C", "E", "F", "G"], ["D", "H"], ["A", "B", "C", "D", "E", "F", "G", "H"]);

			return data;
		}
	}


	/// <summary>
	/// Tests whether operator+ works properly with a tag set on the right side.
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
	/// Tests whether operator+ fails, if the right operand is <see langword="null"/>.
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

	/// <summary>
	/// Test data for TagSet minus operator with a single tag (base - tagToRemove = expected).
	/// </summary>
	public static TheoryData<string[], string, string[]> OperatorMinus_WithSingleTag_TestData
	{
		get
		{
			var data = new TheoryData<string[], string, string[]>();

			// remove tag from empty tag set
			data.Add([], "Tag", []);

			// remove tag from non-empty tag set
			data.Add(["A", "B", "C", "D", "E", "F"], "C", ["A", "B", "D", "E", "F"]);

			return data;
		}
	}

	/// <summary>
	/// Tests whether 'operator-' works properly with a single tag on the right side.
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
	/// Tests whether 'operator-' fails, if the right operand is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void OperatorMinus_WithSingleTag_TagIsNull()
	{
		var left = new TagSet();
		string right = null;
		Assert.Throws<ArgumentNullException>(() => left - right);
	}

	/// <summary>
	/// Test data for TagSet minus operator with multiple tags (base - tagsToRemove = expected).
	/// </summary>
	public static TheoryData<string[], string[], string[]> OperatorMinus_WithMultipleTags_TestData
	{
		get
		{
			var data = new TheoryData<string[], string[], string[]>();

			// remove no tags from empty tag set
			data.Add([], [], []);

			// remove tag from empty tag set
			data.Add([], ["Tag"], []);

			// remove tag from non-empty tag set
			data.Add(["A", "B", "C", "E", "F", "G"], ["D"], ["A", "B", "C", "E", "F", "G"]);

			// remove multiple tags from non-empty tag set
			data.Add(["A", "B", "C", "D", "E", "F", "G"], ["C", "D"], ["A", "B", "E", "F", "G"]);

			return data;
		}
	}

	/// <summary>
	/// Tests whether 'operator-' works properly with a tag set on the right side.
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
	/// Tests whether 'operator-' fails, if the right operand is <see langword="null"/>.
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
