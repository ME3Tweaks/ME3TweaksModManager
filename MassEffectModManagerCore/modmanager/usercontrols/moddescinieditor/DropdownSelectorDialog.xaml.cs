using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Dark.Net;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols.moddescinieditor
{
    /// <summary>
    /// Interaction logic for DropdownSelectorDialog.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class DropdownSelectorDialog : Window
    {
        public string TopDirectionsText { get; set; }
        public string BottomDirectionsText { get; set; }
        public object SelectedItem { get; set; }
        public ObservableCollection<object> DropdownItems { get; } = new ObservableCollection<object>();

        private DropdownSelectorDialog()
        {
            LoadCommands();
            InitializeComponent();
            this.ApplyDarkNetWindowTheme();
        }

        public static object GetSelection<T>(Window owner,
            string title,
            IEnumerable<T> dropdownObjects,
            string topDirectionsText,
            string bottomDirectionsText = null,
            bool canSelectNothing = false)
        {
            DropdownSelectorDialog dss = new DropdownSelectorDialog()
            {
                Title = title,
                TopDirectionsText = topDirectionsText,
                BottomDirectionsText = bottomDirectionsText,
                CanSelectNothing = canSelectNothing,
                Owner = owner
            };
            dss.DropdownItems.ReplaceAll(dropdownObjects.Select(x => (object)x));
            dss.ShowDialog();
            return dss.SelectedItem;
        }

        /// <summary>
        /// If the blank option is valid input or not
        /// </summary>
        public bool CanSelectNothing { get; set; }

        private void LoadCommands()
        {
            OKCommand = new GenericCommand(OK, CanSelectOK);
        }

        private bool CanSelectOK()
        {
            if (SelectedItem == null)
                return CanSelectNothing;
            return true;
        }

        private void OK()
        {
            Close();
        }

        public GenericCommand OKCommand { get; set; }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedItem = null;
            Close();
        }

        private void EntrySelector_ComboBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && OKCommand.CanExecute(null))
            {
                OKCommand.Execute(null);
            }
        }
    }
}
