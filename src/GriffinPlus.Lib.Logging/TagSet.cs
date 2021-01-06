///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

// ReSharper disable UnusedMember.Global
// ReSharper disable PossibleMultipleEnumeration

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// A sorted collection of tags (immutable, thread-safe).
	/// 
	/// Valid tags may consist of the following characters only:
	/// - alphanumeric characters: [a-z], [A-Z], [0-9]
	/// - extra characters: [. , : ; + - #]
	/// - brackets: (), [], {}, &lt;&gt;
	///
	/// Asterisk(*) and quotation mark (?) are not supported as these characters are used to implement pattern matching with wildcards.
	/// Caret(^) and dollar sign ($) are not supported as these characters are used to implement the detection of regex strings.
	/// </summary>
	public sealed class TagSet : IReadOnlyList<string>, IEquatable<TagSet>
	{
		private static readonly Regex sValidTagNameRegex = new Regex(
			@"^[a-zA-Z0-9\.\,\:\;\+\-\#\(\)\[\]\{\}\<\>]+$",
			RegexOptions.Compiled | RegexOptions.Singleline);

		private static readonly List<string> sEmpty = new List<string>();
		private readonly        List<string> mTags;
		private readonly        int          mHashCode;

		/// <summary>
		/// Gets an empty tag set.
		/// </summary>
		public static TagSet Empty { get; } = new TagSet();

		/// <summary>
		/// Initializes a new empty instance of the <see cref="TagSet"/> class.
		/// </summary>
		public TagSet()
		{
			mTags = sEmpty;
			mHashCode = CalculateHashCode(mTags);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TagSet"/> class with the specified tags.
		/// </summary>
		/// <param name="tags">Tags to keep in the collection.</param>
		public TagSet(params string[] tags) :
			this((IEnumerable<string>)tags)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TagSet"/> class with the specified tags.
		/// </summary>
		/// <param name="tags">Tags to keep in the collection.</param>
		public TagSet(IEnumerable<string> tags)
		{
			if (tags == null) throw new ArgumentNullException(nameof(tags));
			foreach (string tag in tags) CheckTag(tag);
			mTags = new List<string>(new HashSet<string>(tags, StringComparer.Ordinal));
			mTags.Sort(StringComparer.OrdinalIgnoreCase);
			mHashCode = CalculateHashCode(mTags);
		}

		/// <summary>
		/// Initializes a new empty instance of the <see cref="TagSet"/> class (for internal use only).
		/// </summary>
		private TagSet(List<string> tags)
		{
			mTags = tags ?? throw new ArgumentNullException(nameof(tags));
			mTags.Sort(StringComparer.OrdinalIgnoreCase);
			mHashCode = CalculateHashCode(mTags);
		}

		/// <summary>
		/// Gets the tag at the specified index.
		/// </summary>
		/// <param name="index">Index of the tag to get.</param>
		/// <returns>The tag at the specified index.</returns>
		public string this[int index]
		{
			get
			{
				if (index < 0 || index >= mTags.Count) throw new IndexOutOfRangeException("The specified index is out of bounds.");
				return mTags[index];
			}
		}

		/// <summary>
		/// Gets the number of tags in the collection.
		/// </summary>
		public int Count => mTags.Count;

		/// <summary>
		/// Determines whether the left tag set and the right tag set are equal.
		/// </summary>
		/// <param name="left">Left tag set.</param>
		/// <param name="right">Right tag set.</param>
		/// <returns>true, if the specified tag sets are equal; otherwise false.</returns>
		public static bool operator ==(TagSet left, TagSet right)
		{
			if (ReferenceEquals(left, null) && ReferenceEquals(right, null)) return true;
			if (ReferenceEquals(left, null)) return false;
			return left.Equals(right);
		}

		/// <summary>
		/// Determines whether the left tag set and the right tag set are not equal.
		/// </summary>
		/// <param name="left">Left tag set.</param>
		/// <param name="right">Right tag set.</param>
		/// <returns>true, if the specified tag sets are not equal; otherwise false.</returns>
		public static bool operator !=(TagSet left, TagSet right)
		{
			if (ReferenceEquals(left, null) && ReferenceEquals(right, null)) return false;
			if (ReferenceEquals(left, null)) return true;
			return !left.Equals(right);
		}

		/// <summary>
		/// Adds the specified tag to the set returning a new set.
		/// </summary>
		/// <param name="left">The set to add the specified tag to.</param>
		/// <param name="right">Tag to add to the set.</param>
		/// <returns>The resulting set.</returns>
		public static TagSet operator +(TagSet left, string right)
		{
			if (left == null) throw new ArgumentNullException(nameof(left));
			if (right == null) throw new ArgumentNullException(nameof(right));
			CheckTag(right);

			var newSet = new HashSet<string>(left, StringComparer.Ordinal) { right };
			if (newSet.Count == left.Count) return left;
			var tags = new List<string>(newSet);
			return new TagSet(tags);
		}

		/// <summary>
		/// Adds the specified tags  to the set returning a new set.
		/// </summary>
		/// <param name="left">The set to add the specified tags to.</param>
		/// <param name="right">Tags to add to the set.</param>
		/// <returns>The resulting set.</returns>
		public static TagSet operator +(TagSet left, IEnumerable<string> right)
		{
			if (left == null) throw new ArgumentNullException(nameof(left));
			if (right == null) throw new ArgumentNullException(nameof(right));
			foreach (string tag in right) CheckTag(tag);

			var newSet = new HashSet<string>(left, StringComparer.Ordinal);
			newSet.UnionWith(right);
			if (newSet.Count == left.Count) return left;
			var tags = new List<string>(newSet);
			return new TagSet(tags);
		}

		/// <summary>
		/// Removes the specified tag from the set returning a new set.
		/// </summary>
		/// <param name="left">The set to remove the specified tag from.</param>
		/// <param name="right">Tag to remove from the set.</param>
		/// <returns>The resulting set.</returns>
		public static TagSet operator -(TagSet left, string right)
		{
			if (left == null) throw new ArgumentNullException(nameof(left));
			if (right == null) throw new ArgumentNullException(nameof(right));
			CheckTag(right);

			var newSet = new HashSet<string>(left, StringComparer.Ordinal);
			newSet.Remove(right);
			if (newSet.Count == left.Count) return left;
			var tags = new List<string>(newSet);
			return new TagSet(tags);
		}

		/// <summary>
		/// Removes the specified tags from the set returning a new set.
		/// </summary>
		/// <param name="left">The set to remove the specified tags from.</param>
		/// <param name="right">Tags to remove from the set.</param>
		/// <returns>The resulting set.</returns>
		public static TagSet operator -(TagSet left, IEnumerable<string> right)
		{
			if (left == null) throw new ArgumentNullException(nameof(left));
			if (right == null) throw new ArgumentNullException(nameof(right));
			foreach (string tag in right) CheckTag(tag);

			var newSet = new HashSet<string>(left, StringComparer.Ordinal);
			newSet.ExceptWith(right);
			if (newSet.Count == left.Count) return left;
			var tags = new List<string>(newSet);
			return new TagSet(tags);
		}

		/// <summary>
		/// Gets an enumerator iterating over the tags.
		/// </summary>
		/// <returns>An enumerator iterating over the tags.</returns>
		public IEnumerator<string> GetEnumerator()
		{
			return mTags.GetEnumerator();
		}

		/// <summary>
		/// Gets an enumerator iterating over the tags.
		/// </summary>
		/// <returns>An enumerator iterating over the tags.</returns>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return mTags.GetEnumerator();
		}

		/// <summary>
		/// Checks whether the specified tag set equals the current one.
		/// </summary>
		/// <param name="other">Tag set to compare with.</param>
		/// <returns>
		/// true, if the specified tag set equals the current one;
		/// otherwise false.
		/// </returns>
		public bool Equals(TagSet other)
		{
			if (other == null) return false;
			return mTags.SequenceEqual(other.mTags, StringComparer.Ordinal);
		}

		/// <summary>
		/// Checks whether the specified tag set equals the current one.
		/// </summary>
		/// <param name="obj">Tag set to compare with.</param>
		/// <returns>
		/// true, if the specified tag set equals the current one;
		/// otherwise false.
		/// </returns>
		public override bool Equals(object obj)
		{
			return ReferenceEquals(this, obj) || obj is TagSet other && Equals(other);
		}

		/// <summary>
		/// Gets the hash code of the tag set.
		/// </summary>
		/// <returns>Hash code of the tag set.</returns>
		public override int GetHashCode()
		{
			return mHashCode;
		}

		/// <summary>
		/// Checks whether the specified string is a valid tag.
		/// </summary>
		/// <param name="tag">Tag to check.</param>
		public static void CheckTag(string tag)
		{
			if (tag == null) throw new ArgumentNullException(nameof(tag));
			if (!sValidTagNameRegex.IsMatch(tag)) throw new ArgumentException($"The specified tag ({tag}) is invalid.");
		}

		/// <summary>
		/// Calculates the hash code of the specified tags.
		/// </summary>
		/// <param name="tags">Tags to hash.</param>
		/// <returns>The hash code of the tags.</returns>
		private static int CalculateHashCode(IEnumerable<string> tags)
		{
			unchecked
			{
				int hash = 17;
				foreach (string tag in tags) hash = hash * 23 + tag.GetHashCode();
				return hash;
			}
		}
	}

}
