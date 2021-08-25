///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Text;

using GriffinPlus.Lib.Collections;

// ReSharper disable ForCanBeConvertedToForeach

namespace GriffinPlus.Lib.Logging.Elasticsearch
{

	/// <summary>
	/// A pool of <see cref="BulkResponse"/> objects (and objects they consist of).
	/// </summary>
	class BulkResponsePool
	{
		#region BulkResponse

		private readonly Stack<BulkResponse> mBulkResponses = new Stack<BulkResponse>();

		/// <summary>
		/// Gets a pooled instance of the <see cref="BulkResponse"/> class (creates a new instance, if the pool is empty).
		/// </summary>
		/// <returns>The requested <see cref="BulkResponse"/> instance.</returns>
		public BulkResponse GetBulkResponse()
		{
			if (mBulkResponses.Count > 0)
				return mBulkResponses.Pop();

			return new BulkResponse(this);
		}

		/// <summary>
		/// Returns the specified <see cref="BulkResponse"/> instance to the pool.
		/// </summary>
		/// <param name="response">The instance to return to the pool.</param>
		public void Return(BulkResponse response)
		{
			if (response == null) return;
			response.Reset();
			mBulkResponses.Push(response);
		}

		#endregion

		#region List<BulkResponse.Item>

		private readonly Stack<List<BulkResponse.Item>> mBulkResponseItemLists = new Stack<List<BulkResponse.Item>>();

		/// <summary>
		/// Gets a pooled list of <see cref="BulkResponse.Item"/> (creates a new instance, if the pool is empty).
		/// </summary>
		/// <returns>The requested list.</returns>
		public List<BulkResponse.Item> GetListOfBulkResponseItems()
		{
			if (mBulkResponseItemLists.Count > 0)
				return mBulkResponseItemLists.Pop();

			return new List<BulkResponse.Item>();
		}

		/// <summary>
		/// Returns the specified list of <see cref="BulkResponse.Item"/> to the pool.
		/// </summary>
		/// <param name="list">The instance to return to the pool.</param>
		public void Return(List<BulkResponse.Item> list)
		{
			if (list == null) return;
			for (int i = 0; i < list.Count; i++) Return(list[i]);
			list.Clear();
			mBulkResponseItemLists.Push(list);
		}

		#endregion

		#region BulkResponse.Item

		private readonly Stack<BulkResponse.Item> mBulkResponseItems = new Stack<BulkResponse.Item>();

		/// <summary>
		/// Gets a pooled instance of the <see cref="BulkResponse.Item"/> class (creates a new instance, if the pool is empty).
		/// </summary>
		/// <returns>The requested <see cref="BulkResponse.Item"/> instance.</returns>
		public BulkResponse.Item GetBulkResponseItem()
		{
			if (mBulkResponseItems.Count > 0)
				return mBulkResponseItems.Pop();

			return new BulkResponse.Item(this);
		}

		/// <summary>
		/// Returns the specified <see cref="BulkResponse.Item"/> instance to the pool.
		/// </summary>
		/// <param name="item">The instance to return to the pool.</param>
		public void Return(BulkResponse.Item item)
		{
			if (item == null) return;
			item.Reset();
			mBulkResponseItems.Push(item);
		}

		#endregion

		#region BulkResponse.Item_Index

		private readonly Stack<BulkResponse.Item_Index> mBulkResponseItemIndices = new Stack<BulkResponse.Item_Index>();

		/// <summary>
		/// Gets a pooled instance of the <see cref="BulkResponse.Item_Index"/> class (creates a new instance, if the pool is empty).
		/// </summary>
		/// <returns>The requested <see cref="BulkResponse.Item_Index"/> instance.</returns>
		public BulkResponse.Item_Index GetBulkResponseItemIndex()
		{
			if (mBulkResponseItemIndices.Count > 0)
				return mBulkResponseItemIndices.Pop();

			return new BulkResponse.Item_Index(this);
		}

		/// <summary>
		/// Returns the specified <see cref="BulkResponse.Item_Index"/> instance to the pool.
		/// </summary>
		/// <param name="item">The instance to return to the pool.</param>
		public void Return(BulkResponse.Item_Index item)
		{
			if (item == null) return;
			item.Reset();
			mBulkResponseItemIndices.Push(item);
		}

		#endregion

		#region BulkResponse.Item_Index_Shards

		private readonly Stack<BulkResponse.Item_Index_Shards> mBulkResponseItemIndexShards = new Stack<BulkResponse.Item_Index_Shards>();

		/// <summary>
		/// Gets a pooled instance of the <see cref="BulkResponse.Item_Index_Shards"/> class (creates a new instance, if the pool is empty).
		/// </summary>
		/// <returns>The requested <see cref="BulkResponse.Item_Index"/> instance.</returns>
		public BulkResponse.Item_Index_Shards GetBulkResponseItemIndexShards()
		{
			if (mBulkResponseItemIndexShards.Count > 0)
				return mBulkResponseItemIndexShards.Pop();

			return new BulkResponse.Item_Index_Shards(this);
		}

		/// <summary>
		/// Returns the specified <see cref="BulkResponse.Item_Index_Shards"/> instance to the pool.
		/// </summary>
		/// <param name="item">The instance to return to the pool.</param>
		public void Return(BulkResponse.Item_Index_Shards item)
		{
			if (item == null) return;
			item.Reset();
			mBulkResponseItemIndexShards.Push(item);
		}

		#endregion

		#region BulkResponse.Item_Index_Error

		private readonly Stack<BulkResponse.Item_Index_Error> mBulkResponseItemIndexErrors = new Stack<BulkResponse.Item_Index_Error>();

		/// <summary>
		/// Gets a pooled instance of the <see cref="BulkResponse.Item_Index_Error"/> class (creates a new instance, if the pool is empty).
		/// </summary>
		/// <returns>The requested <see cref="BulkResponse.Item_Index"/> instance.</returns>
		public BulkResponse.Item_Index_Error GetBulkResponseItemIndexError()
		{
			if (mBulkResponseItemIndexErrors.Count > 0)
				return mBulkResponseItemIndexErrors.Pop();

			return new BulkResponse.Item_Index_Error(this);
		}

		/// <summary>
		/// Returns the specified <see cref="BulkResponse.Item_Index_Error"/> instance to the pool.
		/// </summary>
		/// <param name="item">The instance to return to the pool.</param>
		public void Return(BulkResponse.Item_Index_Error item)
		{
			if (item == null) return;
			item.Reset();
			mBulkResponseItemIndexErrors.Push(item);
		}

		#endregion

		#region String Caching

		private readonly ByteSequenceKeyedDictionary<string> mUtf8ToStringMap = new ByteSequenceKeyedDictionary<string>();

		/// <summary>
		/// Converts the specified UTF-8 byte sequence to a string and caches it to speed up following queries and
		/// reduce GC pressure.
		/// </summary>
		/// <param name="data">UTF-8 bytes sequence to convert.</param>
		/// <returns>The string corresponding to the UTF-8 byte sequence.</returns>
		/// <exception cref="ArgumentNullException">The specified byte sequence is <c>null</c>.</exception>
		/// <exception cref="ArgumentException">The byte sequence contains invalid Unicode code points.</exception>
		public unsafe string GetStringFromUtf8(ReadOnlySpan<byte> data)
		{
			if (data == null) throw new ArgumentNullException(nameof(data));

			if (!mUtf8ToStringMap.TryGetValue(data, out string s))
			{
				fixed (byte* pData = data)
				{
					s = Encoding.UTF8.GetString(pData, data.Length);
					mUtf8ToStringMap.Add(data, s);
				}
			}

			return s;
		}

		#endregion
	}

}
