using MassEffectModManager.ui;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Windows.Shapes;

namespace MassEffectModManager.modmanager.windows
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class FailedModsWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollectionExtended<Mod> FailedMods { get; } = new ObservableCollectionExtended<Mod>();
        public Mod SelectedMod { get; set; }
        public FailedModsWindow(List<Mod> FailedMods)
        {
            DataContext = this;
            this.FailedMods.ReplaceAll(FailedMods);
            InitializeComponent();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void ModsList_ListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                SelectedMod = (Mod)e.AddedItems[0];
            }
            else
            {
                SelectedMod = null;
            }
        }

        private void Close_Clicked(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
