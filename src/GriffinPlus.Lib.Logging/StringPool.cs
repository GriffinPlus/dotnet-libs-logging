///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// String pool interning frequently used strings to avoid keeping multiple instances of equivalent strings.
	/// Although <seealso cref="string.Intern"/> provides a similar functionality, the advantage of the pool is that
	/// pooled strings can be collected by releasing the pool. Strings interned by the runtime are kept alive until
	/// the runtime terminates.
	/// </summary>
	public class StringPool
	{
		private readonly string[][] mTable;

		/// <summary>
		/// Initializes a new instance of the <see cref="StringPool"/> class.
		/// </summary>
		public StringPool()
		{
			mTable = new string[10000][];
		}

		/// <summary>
		/// Keeps the specified string into the pool, if the string is not in the pool, yet.
		/// Returns a previously interned string, if the string is already in the pool.
		/// </summary>
		/// <param name="s">String to intern.</param>
		/// <returns>The interned string.</returns>
		public string Intern(string s)
		{
			uint hash = (uint)s.GetHashCode();
			uint index = hash % (uint)mTable.Length;
			string[] bucket = mTable[index];
			if (bucket != null)
			{
				// ReSharper disable once ForCanBeConvertedToForeach
				for (int i = 0; i < bucket.Length; i++)
				{
					string other = bucket[i];
					if (s.Equals(other)) return other;
				}

				mTable[index] = new string[bucket.Length + 1];
				Array.Copy(bucket, mTable[index], bucket.Length);
				mTable[index][bucket.Length] = s;
				return s;
			}

			bucket = new string[1];
			bucket[0] = s;
			mTable[index] = bucket;
			return s;
		}
	}

}
