using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassEffectModManagerCore.ui
{
    /// <summary>
    /// Pair for itemsource binding of a checkbox.
    /// </summary>
    public class CheckBoxSelectionPair : INotifyPropertyChanged
    {

        public CheckBoxSelectionPair(object item, bool isChecked, Action<CheckBoxSelectionPair> checkChangedDelegate)
        {
            Item = item;
            IsChecked = isChecked;
            _checkChangedDelegate = checkChangedDelegate;
        }

        public object Item { get; set; }
        public bool IsChecked { get; set; }
        public bool IsEnabled { get; set; } = true;

        private readonly Action<CheckBoxSelectionPair> _checkChangedDelegate;

        private void OnIsCheckedChanged()
        {
            _checkChangedDelegate?.Invoke(this);
        }
        public override string ToString() => Item?.ToString();
#pragma warning disable
        public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore
    }
}
