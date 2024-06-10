///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace GriffinPlus.Lib.Logging.Collections;

partial class SelectableLogMessageFilterBase<TMessage, TUnfilteredCollection>
{
	/// <summary>
	/// Base class for more specific filters.
	/// </summary>
	public abstract class FilterBase : ISelectableLogMessageFilter_FilterBase
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="FilterBase"/> class.
		/// </summary>
		/// <param name="parent">The <see cref="SelectableLogMessageFilterBase{TMessage,TUnfilteredCollection}"/> the filter belongs to.</param>
		protected FilterBase(SelectableLogMessageFilterBase<TMessage, TUnfilteredCollection> parent)
		{
			Parent = parent;
		}

		#region PropertyChanged Event

		private readonly HashSet<string> mChangedProperties = new();
		private          int             mPropertyChangedSuspendedCounter;

		/// <summary>
		/// Occurs when one of the properties has changed.
		/// </summary>
		public event PropertyChangedEventHandler PropertyChanged;

		/// <summary>
		/// Raises the <see cref="PropertyChanged"/> event.
		/// </summary>
		/// <param name="propertyName">Name of the property that has changed.</param>
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			if (mPropertyChangedSuspendedCounter == 0)
			{
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
			}
			else
			{
				mChangedProperties.Add(propertyName);
			}
		}

		/// <summary>
		/// Suspends raising the <see cref="PropertyChanged"/> event.
		/// </summary>
		protected internal void SuspendPropertyChanged()
		{
			if (mPropertyChangedSuspendedCounter++ == 0)
			{
				Contract.Assert(
					mChangedProperties.Count == 0,
					"The list of suspended property changed notifications should be empty when starting suspension.");
			}
		}

		/// <summary>
		/// Resumes raising the <see cref="PropertyChanged"/> event firing the event for properties
		/// that have changed meanwhile.
		/// </summary>
		protected internal void ResumePropertyChanged()
		{
			Contract.Assert(
				mPropertyChangedSuspendedCounter > 0,
				"The suspension counter should always be greater than 0 when resuming.");

			if (--mPropertyChangedSuspendedCounter == 0)
			{
				try
				{
					if (mChangedProperties.Contains(null) || mChangedProperties.Contains(string.Empty))
					{
						// some operation invalidated all properties
						// => it is sufficient to notify this one only (more specific property changes are covered by it)
						PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
					}
					else
					{
						// operations have invalidated single properties only
						// => notify for each of them
						foreach (string propertyName in mChangedProperties)
						{
							try
							{
								PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
							}
							catch (Exception ex)
							{
								Debug.Fail("PropertyChanged handler threw an unhandled exception.", ex.ToString());
								throw;
							}
						}
					}
				}
				finally
				{
					mChangedProperties.Clear();
				}
			}
		}

		#endregion

		#region Parent

		/// <summary>
		/// The <see cref="SelectableLogMessageFilter{TMessage}"/> the filter belongs to.
		/// </summary>
		protected SelectableLogMessageFilterBase<TMessage, TUnfilteredCollection> Parent;

		#endregion

		#region Enabled

		private bool mEnabled;

		/// <summary>
		/// Gets or sets a value indicating whether the filter is enabled.
		/// </summary>
		public bool Enabled
		{
			get => mEnabled;
			set
			{
				if (mEnabled != value)
				{
					mEnabled = value;
					OnPropertyChanged();
					Parent.OnFilterChanged(true);
				}
			}
		}

		#endregion

		#region Resetting

		/// <summary>
		/// Resets the filter.
		/// </summary>
		protected internal virtual void Reset()
		{
			Enabled = false;
		}

		#endregion
	}
}
