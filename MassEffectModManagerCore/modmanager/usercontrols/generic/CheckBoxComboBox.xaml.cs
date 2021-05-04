using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MassEffectModManagerCore.modmanager.usercontrols.moddescinieditor;
using ME3ExplorerCore.Gammtek.Extensions.Collections.Generic;
using ME3ExplorerCore.Misc;
using PropertyChanged;
using WinCopies.Util;

namespace MassEffectModManagerCore.modmanager.usercontrols.generic
{
    /// <summary>
    /// Interaction logic for CheckBoxComboBox.xaml
    /// </summary>
    public partial class CheckBoxComboBox : UserControl, INotifyPropertyChanged
    {
        public class CBCBPair
        {
            public CBCBPair(object item, bool isChecked)
            {
                Item = item;
                IsChecked = isChecked;
            }

            public object Item { get; set; }
            public bool IsChecked { get; set; }
            public override string ToString() => Item?.ToString();
        }

        #region Dependency Properties
        #region NoItemsSelectedText
        public string NoItemsSelectedText
        {
            get => (string)GetValue(NoItemsSelectedTextProperty);
            set => SetValue(NoItemsSelectedTextProperty, value);
        }

        public static readonly DependencyProperty NoItemsSelectedTextProperty =
            DependencyProperty.Register(@"NoItemsSelectedText", typeof(string), typeof(CheckBoxComboBox), new PropertyMetadata());

        #endregion

        #region DefaultCheckState
        public bool DefaultCheckState
        {
            get => (bool)GetValue(DefaultCheckStateProperty);
            set => SetValue(DefaultCheckStateProperty, value);
        }

        public static readonly DependencyProperty DefaultCheckStateProperty =
            DependencyProperty.Register(@"DefaultCheckState", typeof(bool), typeof(CheckBoxComboBox), new PropertyMetadata(false));

        #endregion

        #region ItemsSource
        public ICollection ItemsSource
        {
            get => (ICollection)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(@"ItemsSource", typeof(ICollection), typeof(CheckBoxComboBox), new PropertyMetadata(new PropertyChangedCallback(OnItemsSourcePropertyChanged)));

        [SuppressPropertyChangedWarnings]
        private static void OnItemsSourcePropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var control = sender as CheckBoxComboBox;
            control?.OnItemsSourceChanged((ICollection)e.OldValue, (ICollection)e.NewValue);
        }

        [SuppressPropertyChangedWarnings]
        private void OnItemsSourceChanged(ICollection oldValue, ICollection newValue)
        {
            // Remove handler for oldValue.CollectionChanged

            if (oldValue is INotifyCollectionChanged oldValueINotifyCollectionChanged)
            {
                oldValueINotifyCollectionChanged.CollectionChanged -= new NotifyCollectionChangedEventHandler(newValueINotifyCollectionChanged_CollectionChanged);
            }
            // Add handler for newValue.CollectionChanged (if possible)
            if (newValue is INotifyCollectionChanged newValueINotifyCollectionChanged)
            {
                newValueINotifyCollectionChanged.CollectionChanged += new NotifyCollectionChangedEventHandler(newValueINotifyCollectionChanged_CollectionChanged);
            }

            // ICollection doesn't have linq support so just kinda old school it
            List<CBCBPair> pairs = new List<CBCBPair>();
            foreach (var v in newValue)
            {
                pairs.Add(new CBCBPair(v, DefaultCheckState));
            }

            InternalItemsSource.ReplaceAll(pairs);
            CBSelector.SelectedItem = pairs.FirstOrDefault();
        }

        private void newValueINotifyCollectionChanged_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {

        }
        #endregion

        #endregion

        #region Properties

        // Maybe make 'JoinString' property?
        public string SelectedItemsString { get; set; }
        private void UpdateSelectedItemString()
        {
            var selectedItems = InternalItemsSource.Where(x => x.IsChecked).ToList();
            if (selectedItems.Any())
                SelectedItemsString = string.Join(',', selectedItems.Select(x => x.ToString()));
            else
                SelectedItemsString = NoItemsSelectedText;
        }
        #endregion


        public ObservableCollectionExtended<CBCBPair> InternalItemsSource { get; } = new ObservableCollectionExtended<CBCBPair>();

        /// <summary>
        /// Sets the list of selected items
        /// </summary>
        /// <param name="selectedItems"></param>
        public void SetSelectedItems(IEnumerable<object> selectedItems)
        {
            var sic = selectedItems.ToList();
            foreach (var v in InternalItemsSource)
            {
                v.IsChecked = sic.Contains(v.Item);
            }
            UpdateSelectedItemString();
        }

        public void ClearAllSelectedItems()
        {
            foreach (var v in InternalItemsSource)
            {
                v.IsChecked = false;
            }
            UpdateSelectedItemString();
        }

        public CheckBoxComboBox()
        {
            InitializeComponent();
            UpdateSelectedItemString();
        }


        private void CheckChanged(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb)
            {
                //if (cb.IsChecked.HasValue && cb.IsChecked.Value)
                //{
                //    SelectedItems.Add(cb.DataContext);
                //}
                //else
                //{
                //    SelectedItems.Remove(cb.DataContext);
                //}
                UpdateSelectedItemString();
            }
        }

#pragma warning disable
        public event PropertyChangedEventHandler? PropertyChanged;

        private void CB_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ComboBox cb && cb.IsDropDownOpen)
            {
                e.Handled = true; // Do not auto close
            }
        }

        private void CBCB_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is CheckBox cb)
            {
                cb.IsChecked = !cb.IsChecked;
                e.Handled = true;
            }
        }
#pragma warning restore
        public IEnumerable<object> GetSelectedItems()
        {
            return InternalItemsSource.Where(x => x.IsChecked).Select(x => x.Item);
        }
    }
}
