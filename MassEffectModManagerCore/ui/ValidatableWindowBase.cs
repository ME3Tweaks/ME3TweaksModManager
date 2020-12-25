using MvvmValidation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using PropertyChanged;

namespace MassEffectModManagerCore.ui
{
    public abstract class ValidatableWindowBase : Window, IValidatable, INotifyDataErrorInfo, INotifyPropertyChanged
    {
        protected ValidationHelper Validator { get; }

        private NotifyDataErrorInfoAdapter NotifyDataErrorInfoAdapter { get; }

        protected ValidatableWindowBase()
        {
            Validator = new ValidationHelper();

            NotifyDataErrorInfoAdapter = new NotifyDataErrorInfoAdapter(Validator);
            NotifyDataErrorInfoAdapter.ErrorsChanged += OnErrorsChanged;
        }

        [SuppressPropertyChangedWarnings]
        private void OnErrorsChanged(object sender, DataErrorsChangedEventArgs e)
        {
            //Debug.WriteLine(@"Error changing");
            // Notify the UI that the property has changed so that the validation error gets displayed (or removed).
            OnPropertyChanged(e.PropertyName);
        }

        Task<ValidationResult> IValidatable.Validate()
        {
            return Validator.ValidateAllAsync();
        }

        #region Implementation of INotifyDataErrorInfo

        public IEnumerable GetErrors(string propertyName)
        {
            return NotifyDataErrorInfoAdapter.GetErrors(propertyName);
        }

        public bool HasErrors => NotifyDataErrorInfoAdapter.HasErrors;

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged
        {
            add { NotifyDataErrorInfoAdapter.ErrorsChanged += value; }
            remove { NotifyDataErrorInfoAdapter.ErrorsChanged -= value; }
        }


        #region INotifyPropertyChanged helpers (required to work with validation, can't use fody)
        //Fody uses this property on weaving
#pragma warning disable
public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore

        /// <summary>
        /// Notifies listeners when given property is updated.
        /// </summary>
        /// <param name="propertyname">Name of property to give notification for. If called in property, argument can be ignored as it will be default.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyname = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }

        /// <summary>
        /// Sets given property and notifies listeners of its change. IGNORES setting the property to same value.
        /// Should be called in property setters.
        /// </summary>
        /// <typeparam name="T">Type of given property.</typeparam>
        /// <param name="field">Backing field to update.</param>
        /// <param name="value">New value of property.</param>
        /// <param name="propertyName">Name of property.</param>
        /// <returns>True if success, false if backing field and new value aren't compatible.</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        #endregion

        #endregion
    }
}