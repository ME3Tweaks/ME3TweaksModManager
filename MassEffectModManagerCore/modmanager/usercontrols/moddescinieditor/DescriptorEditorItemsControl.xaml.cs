using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using PropertyChanged;

namespace MassEffectModManagerCore.modmanager.usercontrols.moddescinieditor
{
    /// <summary>
    /// Interaction logic for AlternatesItemsControl.xaml
    /// </summary>
    [DebuggerDisplay(@"DescriptorEditorItemsControl Header={HeaderText} ItemCount={GetItemCount()}")]
    public partial class DescriptorEditorItemsControl : UserControl, INotifyPropertyChanged
    {
        public string HeaderText
        {
            get => (string)GetValue(HeaderTextProperty);
            set => SetValue(HeaderTextProperty, value);
        }

        public static readonly DependencyProperty HeaderTextProperty =
            DependencyProperty.Register(@"HeaderText", typeof(string), typeof(DescriptorEditorItemsControl));

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(@"Description", typeof(string), typeof(DescriptorEditorItemsControl));

        public ICollection ItemsSource
        {
            get => (ICollection)GetValue(ItemsSourceProperty);
            set
            {
                SetValue(ItemsSourceProperty, value);
            }
        }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(@"ItemsSource", typeof(ICollection), typeof(DescriptorEditorItemsControl), new PropertyMetadata(new PropertyChangedCallback(OnItemsSourcePropertyChanged)));

        [SuppressPropertyChangedWarnings]
        private static void OnItemsSourcePropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var control = sender as DescriptorEditorItemsControl;
            control?.OnItemsSourceChanged((ICollection)e.OldValue, (ICollection)e.NewValue);
        }

        [SuppressPropertyChangedWarnings]
        private void OnItemsSourceChanged(ICollection oldValue, ICollection newValue)
        {
            // Remove handler for oldValue.CollectionChanged
            var oldValueINotifyCollectionChanged = oldValue as INotifyCollectionChanged;

            if (null != oldValueINotifyCollectionChanged)
            {
                oldValueINotifyCollectionChanged.CollectionChanged -= new NotifyCollectionChangedEventHandler(newValueINotifyCollectionChanged_CollectionChanged);
            }
            // Add handler for newValue.CollectionChanged (if possible)
            var newValueINotifyCollectionChanged = newValue as INotifyCollectionChanged;
            if (null != newValueINotifyCollectionChanged)
            {
                newValueINotifyCollectionChanged.CollectionChanged += new NotifyCollectionChangedEventHandler(newValueINotifyCollectionChanged_CollectionChanged);
            }
        }

        void newValueINotifyCollectionChanged_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
        }

#if DEBUG
        public string GetItemCount()
        {
            return ItemsSource != null ? $@"{ItemsSource.Count} items" : @"ItemsSource is null";
        }
#endif

        public DescriptorEditorItemsControl()
        {
            InitializeComponent();
        }

        //Fody uses this property on weaving
#pragma warning disable
public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }
}
