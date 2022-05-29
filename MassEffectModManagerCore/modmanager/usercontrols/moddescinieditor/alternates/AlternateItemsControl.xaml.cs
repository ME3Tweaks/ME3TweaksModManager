using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.alternates;
using PropertyChanged;

namespace ME3TweaksModManager.modmanager.usercontrols.moddescinieditor.alternates
{
    /// <summary>
    /// User control that allows editing a list of AlternateOptions.
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class AlternateItemsControl : UserControl
    {
        public AlternateItemsControl()
        {
            LoadCommands();
            InitializeComponent();
        }

        public RelayCommand MoveAlternateDownCommand { get; set; }
        public RelayCommand MoveAlternateUpCommand { get; set; }
        public RelayCommand DeleteAlternateCommand { get; set; }

        private void LoadCommands()
        {
            DeleteAlternateCommand = new RelayCommand(RemoveAlternate, x => true);
            MoveAlternateUpCommand = new RelayCommand(MoveAlternateUp, CanMoveAlternateUp);
            MoveAlternateDownCommand = new RelayCommand(MoveAlternateDown, CanMoveAlternateDown);
        }

        private void MoveAlternateUp(object obj)
        {
            if (obj is AlternateOption option && DataContext is AlternateBuilderBaseControl baseControl)
            {
                var startingIndex = baseControl.Alternates.IndexOf(option);
                baseControl.Alternates.RemoveAt(startingIndex); // e.g. Remove from position 3
                baseControl.Alternates.Insert(startingIndex - 1, option);
            }
        }

        private void MoveAlternateDown(object obj)
        {
            if (obj is AlternateOption option && DataContext is AlternateBuilderBaseControl baseControl)
            {
                var startingIndex = baseControl.Alternates.IndexOf(option);
                baseControl.Alternates.RemoveAt(startingIndex); // e.g. Remove from position 3
                baseControl.Alternates.Insert(startingIndex + 1, option);
            }
        }

        private bool CanMoveAlternateDown(object obj)
        {
            if (obj is AlternateOption option && DataContext is AlternateBuilderBaseControl baseControl)
            {
                return baseControl.Alternates.IndexOf(option) < baseControl.Alternates.Count - 1; // -1 for 0 indexing. Less than covers the next -1.
            }
            return false;
        }

        private bool CanMoveAlternateUp(object obj)
        {
            if (obj is AlternateOption option && DataContext is AlternateBuilderBaseControl baseControl)
            {
                return baseControl.Alternates.IndexOf(option) > 0;
            }
            return false;
        }

        private void RemoveAlternate(object obj)
        {
            if (obj is AlternateOption option && DataContext is AlternateBuilderBaseControl baseControl)
            {
                var deleteAlternate = M3L.ShowDialog(Window.GetWindow(this), M3L.GetString(M3L.string_mde_deleteAlternateNamed, option.FriendlyName), M3L.GetString(M3L.string_confirmDeletion), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                if (deleteAlternate == MessageBoxResult.Yes)
                {
                    baseControl.Alternates.Remove(option);
                }
            }
        }


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
