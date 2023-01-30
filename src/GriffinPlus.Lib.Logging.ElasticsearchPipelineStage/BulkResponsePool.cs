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

		private readonly Stack<BulkResponse> mBulkResponseInstances = new Stack<BulkResponse>();

		/// <summary>
		/// Gets a pooled instance of the <see cref="BulkResponse"/> class (creates a new instance, if the pool is empty).
		/// </summary>
		/// <returns>The requested <see cref="BulkResponse"/> instance.</returns>
		public BulkResponse GetBulkResponse()
		{
			return mBulkResponseInstances.Count > 0
				       ? mBulkResponseInstances.Pop()
				       : new BulkResponse(this);
		}

		/// <summary>
		/// Returns the specified <see cref="BulkResponse"/> instance to the pool.
		/// </summary>
		/// <param name="response">The instance to return to the pool.</param>
		public void Return(BulkResponse response)
		{
			if (response == null) return;
			response.Reset();
			mBulkResponseInstances.Push(response);
		}

		#endregion

		#region List<BulkResponse.Item>

		private readonly Stack<List<BulkResponse.Item>> mBulkResponseItemListInstances = new Stack<List<BulkResponse.Item>>();

		/// <summary>
		/// Gets a pooled list of <see cref="BulkResponse.Item"/> (creates a new instance, if the pool is empty).
		/// </summary>
		/// <returns>The requested list.</returns>
		public List<BulkResponse.Item> GetListOfBulkResponseItems()
		{
			return mBulkResponseItemListInstances.Count > 0
				       ? mBulkResponseItemListInstances.Pop()
				       : new List<BulkResponse.Item>();
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
			mBulkResponseItemListInstances.Push(list);
		}

		#endregion

		#region BulkResponse.Item

		private readonly Stack<BulkResponse.Item> mBulkResponseItemInstances = new Stack<BulkResponse.Item>();

		/// <summary>
		/// Gets a pooled instance of the <see cref="BulkResponse.Item"/> class (creates a new instance, if the pool is empty).
		/// </summary>
		/// <returns>The requested <see cref="BulkResponse.Item"/> instance.</returns>
		public BulkResponse.Item GetBulkResponseItem()
		{
			return mBulkResponseItemInstances.Count > 0
				       ? mBulkResponseItemInstances.Pop()
				       : new BulkResponse.Item(this);
		}

		/// <summary>
		/// Returns the specified <see cref="BulkResponse.Item"/> instance to the pool.
		/// </summary>
		/// <param name="item">The instance to return to the pool.</param>
		public void Return(BulkResponse.Item item)
		{
			if (item == null) return;
			item.Reset();
			mBulkResponseItemInstances.Push(item);
		}

		#endregion

		#region BulkResponse.Item_Create

		private readonly Stack<BulkResponse.Item_Create> mBulkResponseItemCreateInstances = new Stack<BulkResponse.Item_Create>();

		/// <summary>
		/// Gets a pooled instance of the <see cref="BulkResponse.Item_Create"/> class (creates a new instance, if the pool is empty).
		/// </summary>
		/// <returns>The requested <see cref="BulkResponse.Item_Create"/> instance.</returns>
		public BulkResponse.Item_Create GetBulkResponseItemCreate()
		{
			return mBulkResponseItemCreateInstances.Count > 0
				       ? mBulkResponseItemCreateInstances.Pop()
				       : new BulkResponse.Item_Create(this);
		}

		/// <summary>
		/// Returns the specified <see cref="BulkResponse.Item_Create"/> instance to the pool.
		/// </summary>
		/// <param name="item">The instance to return to the pool.</param>
		public void Return(BulkResponse.Item_Create item)
		{
			if (item == null) return;
			item.Reset();
			mBulkResponseItemCreateInstances.Push(item);
		}

		#endregion

		#region BulkResponse.Item_Create_Shards

		private readonly Stack<BulkResponse.Item_Create_Shards> mBulkResponseItemCreateShardInstances = new Stack<BulkResponse.Item_Create_Shards>();

		/// <summary>
		/// Gets a pooled instance of the <see cref="BulkResponse.Item_Create_Shards"/> class (creates a new instance, if the pool is empty).
		/// </summary>
		/// <returns>The requested <see cref="BulkResponse.Item_Create"/> instance.</returns>
		public BulkResponse.Item_Create_Shards GetBulkResponseItemIndexShards()
		{
			return mBulkResponseItemCreateShardInstances.Count > 0
				       ? mBulkResponseItemCreateShardInstances.Pop()
				       : new BulkResponse.Item_Create_Shards(this);
		}

		/// <summary>
		/// Returns the specified <see cref="BulkResponse.Item_Create_Shards"/> instance to the pool.
		/// </summary>
		/// <param name="item">The instance to return to the pool.</param>
		public void Return(BulkResponse.Item_Create_Shards item)
		{
			if (item == null) return;
			item.Reset();
			mBulkResponseItemCreateShardInstances.Push(item);
		}

		#endregion

		#region BulkResponse.Item_Create_Error

		private readonly Stack<BulkResponse.Item_Create_Error> mBulkResponseItemCreateErrorInstances = new Stack<BulkResponse.Item_Create_Error>();

		/// <summary>
		/// Gets a pooled instance of the <see cref="BulkResponse.Item_Create_Error"/> class (creates a new instance, if the pool is empty).
		/// </summary>
		/// <returns>The requested <see cref="BulkResponse.Item_Create"/> instance.</returns>
		public BulkResponse.Item_Create_Error GetBulkResponseItemIndexError()
		{
			return mBulkResponseItemCreateErrorInstances.Count > 0
				       ? mBulkResponseItemCreateErrorInstances.Pop()
				       : new BulkResponse.Item_Create_Error(this);
		}

		/// <summary>
		/// Returns the specified <see cref="BulkResponse.Item_Create_Error"/> instance to the pool.
		/// </summary>
		/// <param name="item">The instance to return to the pool.</param>
		public void Return(BulkResponse.Item_Create_Error item)
		{
			if (item == null) return;
			item.Reset();
			mBulkResponseItemCreateErrorInstances.Push(item);
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
