///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;

namespace GriffinPlus.Lib.Logging.Collections
{

	public partial class FileBackedLogMessageCollection
	{
		/// <summary>
		/// Enumerator iterating over a <see cref="FileBackedLogMessageCollection"/>.
		/// </summary>
		private class Enumerator : IEnumerator<LogMessage>
		{
			private readonly FileBackedLogMessageCollection mCollection;
			private readonly int                            mCollectionChangeCounter;
			private          long                           mIndex;

			/// <summary>
			/// Creates a new instance of the <see cref="Enumerator"/> class.
			/// </summary>
			/// <param name="collection">Collection the enumerator will iterate over.</param>
			public Enumerator(FileBackedLogMessageCollection collection)
			{
				mCollection = collection;
				mCollectionChangeCounter = collection.mChangeCounter;
				mIndex = -1;
			}

			/// <summary>
			/// Gets the current log message.
			/// </summary>
			public LogMessage Current => mCollection[mIndex];

			/// <summary>
			/// Gets the current log message.
			/// </summary>
			object IEnumerator.Current => mCollection[mIndex];

			/// <summary>
			/// Disposes the enumerator.
			/// </summary>
			public void Dispose() { }

			/// <summary>
			/// Advances the enumerator to the next element of the collection
			/// </summary>
			/// <returns>
			/// <c>true</c> if the enumerator was successfully advanced to the next log message;<br/>
			/// <c>false</c> if the enumerator has passed the end of the collection.
			/// </returns>
			/// <exception cref="InvalidOperationException">The collection was modified after the enumerator was created.</exception>
			public bool MoveNext()
			{
				if (mCollectionChangeCounter != mCollection.mChangeCounter)
				{
					throw new InvalidOperationException("The collection was modified after the enumerator was created.");
				}

				if (mIndex < mCollection.Count)
				{
					mIndex++;
					return mIndex < mCollection.Count;
				}

				return false;
			}

			/// <summary>
			/// Sets the enumerator to its initial position.
			/// </summary>
			/// <exception cref="InvalidOperationException">The collection was modified after the enumerator was created.</exception>
			public void Reset()
			{
				if (mCollectionChangeCounter != mCollection.mChangeCounter)
				{
					throw new InvalidOperationException("The collection was modified after the enumerator was created.");
				}

				mIndex = -1;
			}
		}
	}

}
