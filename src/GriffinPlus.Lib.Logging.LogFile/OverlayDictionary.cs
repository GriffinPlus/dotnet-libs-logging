///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Collections;
using System.Collections.Generic;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// A dictionary with overlay support.
	/// Changes can be committed and discarded to restore the last committed state.
	/// </summary>
	class OverlayDictionary<TKey, TValue> : IDictionary<TKey, TValue>
	{
		private readonly Dictionary<TKey, TValue> mDictionary;
		private readonly Dictionary<TKey, TValue> mOverlay;

		/// <summary>
		/// Initializes a new instance of the <see cref="OverlayDictionary{TKey,TValue}" /> class.
		/// </summary>
		/// <param name="comparer">Comparer to use to checks keys for equality (may be null to use the default comparer).</param>
		public OverlayDictionary(IEqualityComparer<TKey> comparer = null)
		{
			comparer = comparer ?? EqualityComparer<TKey>.Default;
			mOverlay = new Dictionary<TKey, TValue>(comparer);
			mDictionary = new Dictionary<TKey, TValue>(comparer);
		}

		/// <summary>
		/// Gets a value indicating whether the overlay dictionary contains changes.
		/// </summary>
		public bool Dirty { get; private set; }

		/// <summary>
		/// Gets an enumerator that iterates over the dictionary.
		/// </summary>
		/// <returns>Enumerator that iterates over the dictionary.</returns>
		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
		{
			return mOverlay.GetEnumerator();
		}

		/// <summary>
		/// Gets an enumerator that iterates over the dictionary.
		/// </summary>
		/// <returns>Enumerator that iterates over the dictionary.</returns>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		/// <summary>
		/// Adds the specified item to the dictionary.
		/// </summary>
		/// <param name="item">Item to add.</param>
		void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
		{
			(mOverlay as ICollection<KeyValuePair<TKey, TValue>>).Add(item);
			Dirty = true;
		}

		/// <summary>
		/// Removes all keys and values from the dictionary.
		/// </summary>
		public void Clear()
		{
			if (mOverlay.Count > 0)
			{
				mOverlay.Clear();
				Dirty = true;
			}
		}

		/// <summary>
		/// Determines whether the collection contains the specified item.
		/// </summary>
		/// <param name="item">Item to check.</param>
		/// <returns></returns>
		bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
		{
			return (mOverlay as ICollection<KeyValuePair<TKey, TValue>>).Contains(item);
		}

		/// <summary>
		/// Copies the elements of the dictionary to the specified array, starting at a particular index.
		/// </summary>
		/// <param name="array">Array to copy the elements to.</param>
		/// <param name="arrayIndex">Index in the array to start at.</param>
		void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
		{
			(mOverlay as ICollection<KeyValuePair<TKey, TValue>>).CopyTo(array, arrayIndex);
		}

		/// <summary>
		/// Removes the first occurrence of a specific object from the dictionary.
		/// </summary>
		/// <param name="item">Item to remove.</param>
		/// <returns></returns>
		bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
		{
			bool removed = (mOverlay as ICollection<KeyValuePair<TKey, TValue>>).Remove(item);
			if (removed) Dirty = true;
			return removed;
		}

		/// <summary>
		/// Gets a value indicating whether the dictionary is read-only (always false).
		/// </summary>
		bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

		/// <summary>
		/// Gets the number of items in the dictionary.
		/// </summary>
		public int Count => mOverlay.Count;

		/// <summary>
		/// Determines whether the dictionary contains the specified key.
		/// </summary>
		/// <param name="key">The key to locate in the dictionary.</param>
		/// <returns>true, if the dictionary contains the specified key; otherwise false.</returns>
		public bool ContainsKey(TKey key)
		{
			return mOverlay.ContainsKey(key);
		}

		/// <summary>
		/// Adds the specified key and value to the dictionary.
		/// </summary>
		/// <param name="key">The key of the element to add.</param>
		/// <param name="value">The value of the element to add.</param>
		public void Add(TKey key, TValue value)
		{
			mOverlay.Add(key, value);
			Dirty = true;
		}

		/// <summary>
		/// Removes the value with the specified key from the dictionary.
		/// </summary>
		/// <param name="key">The key of the element to remove.</param>
		/// <returns>
		/// true, if the element with the specified key was successfully removed;
		/// false, if the element does not exist.
		/// </returns>
		public bool Remove(TKey key)
		{
			bool removed = mOverlay.Remove(key);
			if (removed) Dirty = true;
			return removed;
		}

		/// <summary>
		/// Gets the value associated with the specified key.
		/// </summary>
		/// <param name="key">Key of the value to get.</param>
		/// <param name="value">Receives the value with the specified key.</param>
		/// <returns>
		/// true, if the dictionary contains an element with the specified key;
		/// otherwise false.
		/// </returns>
		public bool TryGetValue(TKey key, out TValue value)
		{
			return mOverlay.TryGetValue(key, out value);
		}

		/// <summary>
		/// Gets or sets the element with the specified key.
		/// </summary>
		/// <param name="key">Key of the value to get/set.</param>
		/// <returns>The value of the element with the specified key.</returns>
		public TValue this[TKey key]
		{
			get => mOverlay[key];
			set
			{
				// check whether the dictionary already contains this value
				if (mOverlay.TryGetValue(key, out var oldValue))
				{
					if (Equals(oldValue, value))
					{
						// set object although the objects are equal to preserve the identity
						mOverlay[key] = value;
						return;
					}
				}

				mOverlay[key] = value;
				Dirty = true;
			}
		}

		/// <summary>
		/// Gets a collection containing the keys in the dictionary.
		/// </summary>
		public ICollection<TKey> Keys => mOverlay.Keys;

		/// <summary>
		/// Gets a collection containing the values in the dictionary.
		/// </summary>
		public ICollection<TValue> Values => mOverlay.Values;

		/// <summary>
		/// Commits changes to the underlying dictionary.
		/// </summary>
		public void Commit()
		{
			if (Dirty)
			{
				mDictionary.Clear();
				foreach (var kvp in mOverlay) mDictionary.Add(kvp.Key, kvp.Value);
				Dirty = false;
			}
		}

		/// <summary>
		/// Discards changes in the overlay.
		/// </summary>
		public void Discard()
		{
			if (Dirty)
			{
				mOverlay.Clear();
				foreach (var kvp in mDictionary) mOverlay.Add(kvp.Key, kvp.Value);
				Dirty = false;
			}
		}
	}

}
