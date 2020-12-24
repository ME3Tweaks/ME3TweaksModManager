using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MassEffectModManagerCore.modmanager.usercontrols.moddescinieditor.alternates
{
    /// <summary>
    /// Interaction logic for AlternateItemsControl.xaml
    /// </summary>
    public partial class AlternateItemsControl : UserControl, INotifyPropertyChanged
    {
        public AlternateItemsControl()
        {
            InitializeComponent();
        }

        //Fody uses this property on weaving
#pragma warning disable 67
public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore 67

        private void HandleMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            // This forces scrolling to bubble up
            // cause expander eats it
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
                eventArg.RoutedEvent = UIElement.MouseWheelEvent;
                eventArg.Source = sender;
                var parent = (((Control)sender).TemplatedParent ?? ((Control)sender).Parent) as UIElement;
                parent.RaiseEvent(eventArg);
            }
        }
    }
}
