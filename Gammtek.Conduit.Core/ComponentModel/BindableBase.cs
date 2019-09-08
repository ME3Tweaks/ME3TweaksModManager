using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Gammtek.Conduit.Extensions.Linq;

namespace Gammtek.Conduit.ComponentModel
{
	/// <summary>
	///     Implementation of <see cref="INotifyPropertyChanged" /> to simplify models.
	/// </summary>
	[DataContract]
	public class BindableBase : INotifyPropertyChanged
	{
		/// <summary>
		///     Creates an instance of <see cref="BindableBase" />.
		/// </summary>
		public BindableBase()
		{
			IsNotifying = true;
		}

		/// <summary>
		///     Occurs when a property value changes.
		/// </summary>
		public event PropertyChangedEventHandler PropertyChanged = delegate { };

		/// <summary>
		///     Enables/Disables property change notification.
		/// </summary>
		public bool IsNotifying { get; set; }

		/// <summary>
		///     Notifies subscribers of the property change.
		/// </summary>
		/// <param name="propertyName">Name of the property.</param>
		[NotifyPropertyChangedInvocator]
		public virtual void NotifyOfPropertyChange([CallerMemberName] string propertyName = null)
		{
			if (IsNotifying)
			{
				OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
				//Execute.OnUIThread(() => OnPropertyChanged(new PropertyChangedEventArgs(propertyName)));
			}
		}

		/// <summary>
		///     Notifies subscribers of the property change.
		/// </summary>
		/// <typeparam name="TProperty">The type of the property.</typeparam>
		/// <param name="property">The property expression.</param>
		[NotifyPropertyChangedInvocator]
		public void NotifyOfPropertyChange<TProperty>(Expression<Func<TProperty>> property)
		{
			NotifyOfPropertyChange(property.GetMemberInfo().Name);
		}

		/// <summary>
		///     Raises a change notification indicating that all bindings should be refreshed.
		/// </summary>
		public virtual void Refresh()
		{
			NotifyOfPropertyChange(string.Empty);
		}

		/// <summary>
		///     Raises the <see cref="PropertyChanged" /> event directly.
		/// </summary>
		/// <param name="e">The <see cref="PropertyChangedEventArgs" /> instance containing the event data.</param>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[NotifyPropertyChangedInvocator]
		protected void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			var handler = PropertyChanged;

			handler?.Invoke(this, e);
		}

		/// <summary>
		///     Checks if a property already matches a desired value. Sets the property and
		///     notifies listeners only when necessary.
		/// </summary>
		/// <typeparam name="T">Type of the property.</typeparam>
		/// <param name="storage">Reference to a property with both getter and setter.</param>
		/// <param name="value">Desired value for the property.</param>
		/// <param name="propertyName">
		///     Name of the property used to notify listeners. This
		///     value is optional and can be provided automatically when invoked from compilers that
		///     support CallerMemberName.
		/// </param>
		/// <returns>
		///     True if the value was changed, false if the existing value matched the
		///     desired value.
		/// </returns>
		[NotifyPropertyChangedInvocator]
		protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
		{
			if (Equals(storage, value))
			{
				return false;
			}

			storage = value;
			NotifyOfPropertyChange(propertyName);

			return true;
		}
	}
}
