///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Diagnostics;
using System.Text;

namespace GriffinPlus.Lib.Logging.Elasticsearch;

partial class BulkResponse
{
	/// <summary>
	/// A proxy for a boolean value that looks onto a sequence of bytes containing
	/// the value in its UTF-8 encoded representation.
	/// </summary>
	private struct BooleanPropertyProxy
	{
		private readonly byte[] mData;
		private readonly int    mIndex;
		private readonly int    mLength;
		private          bool   mHasValue;
		private          bool   mValue;

		/// <summary>
		/// Initializes a new instance of the <see cref="BooleanPropertyProxy"/> struct.
		/// </summary>
		/// <param name="data">Byte array containing the UTF-8 encoded string.</param>
		/// <param name="index">Index in the array where the string starts.</param>
		/// <param name="length">Length of the string in the array (in bytes).</param>
		public BooleanPropertyProxy(byte[] data, int index, int length)
		{
			mData = data;
			mIndex = index;
			mLength = length;
			mHasValue = false;
			mValue = false;
		}

		/// <summary>
		/// Gets the boolean value corresponding to the UTF-8 encoded byte sequence passed during construction.
		/// </summary>
		public bool Value
		{
			get
			{
				Debug.Assert(mData != null);
				if (mHasValue) return mValue;
				string s = Encoding.UTF8.GetString(mData, mIndex, mLength);
				mHasValue = bool.TryParse(s, out mValue);
				Debug.Assert(mHasValue);
				return mValue;
			}
		}
	}
}
