using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MassEffectModManagerCore.ui;
using ME3ExplorerCore.Gammtek.Extensions.Collections.Generic;

namespace MassEffectModManagerCore.modmanager.usercontrols.moddescinieditor
{
    /// <summary>
    /// Interaction logic for DropdownSelectorDialog.xaml
    /// </summary>
    public partial class DropdownSelectorDialog : Window, INotifyPropertyChanged
    {
        public string DirectionsText { get; set; }
        public string DirectionsText2 { get; set; }
        public object SelectedItem { get; set; }
        public string DialogTitle { get; set; }
        public ObservableCollection<object> DropdownItems { get; } = new ObservableCollection<object>();

        private DropdownSelectorDialog()
        {
            LoadCommands();
            InitializeComponent();
        }

        public static object GetSelection<T>(Window owner, string title, IEnumerable<T> dropdownObjects, string directionsText, string directionsText2)
        {
            DropdownSelectorDialog dss = new DropdownSelectorDialog()
            {
                DialogTitle = title,
                DirectionsText = directionsText,
                DirectionsText2 = directionsText2,
                Owner = owner
            };
            dss.DropdownItems.ReplaceAll(dropdownObjects.Select(x => (object)x));
            dss.ShowDialog();
            return dss.SelectedItem;
        }

        private void LoadCommands()
        {
            OKCommand = new GenericCommand(OK);
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

        //Fody uses this property on weaving
#pragma warning disable 0067
public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore 0067
    }
}
