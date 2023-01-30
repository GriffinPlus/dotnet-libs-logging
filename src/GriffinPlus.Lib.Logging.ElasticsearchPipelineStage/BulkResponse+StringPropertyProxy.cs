///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Text;

namespace GriffinPlus.Lib.Logging.Elasticsearch
{

	partial class BulkResponse
	{
		/// <summary>
		/// A proxy for a string property that looks onto a sequence of bytes containing
		/// the value in its UTF-8 encoded representation.
		/// </summary>
		private struct StringPropertyProxy
		{
			private readonly BulkResponsePool mPool;
			private          byte[]           mData;
			private          int              mIndex;
			private          int              mLength;
			private          bool             mCache;
			private          string           mString;

			/// <summary>
			/// Initializes a new instance of the <see cref="StringPropertyProxy"/> struct.
			/// </summary>
			public StringPropertyProxy(BulkResponsePool pool)
			{
				mPool = pool;
				mData = null;
				mIndex = 0;
				mLength = 0;
				mCache = false;
				mString = null;
			}

			/// <summary>
			/// Updates the proxy.
			/// </summary>
			/// <param name="data">Byte array containing the UTF-8 encoded string.</param>
			/// <param name="index">Index in the array where the string starts.</param>
			/// <param name="length">Length of the string in the array (in bytes).</param>
			/// <param name="cache">
			/// <c>true</c> to cache the string in the <see cref="BulkResponsePool"/>;<br/>
			/// otherwise <c>false</c>.
			/// </param>
			public void Update(
				byte[] data,
				int    index,
				int    length,
				bool   cache)
			{
				mData = data;
				mIndex = index;
				mLength = length;
				mCache = cache;
				mString = null;
			}

			/// <summary>
			/// Gets the string corresponding to the UTF-8 encoded byte sequence passed during construction.
			/// </summary>
			public string Value
			{
				get
				{
					if (mData == null) return null;
					if (mString != null) return mString;
					return mString = mCache
						                 ? mPool.GetStringFromUtf8(new ReadOnlySpan<byte>(mData, mIndex, mLength))
						                 : Encoding.UTF8.GetString(mData, mIndex, mLength);
				}
			}
		}
	}

}
