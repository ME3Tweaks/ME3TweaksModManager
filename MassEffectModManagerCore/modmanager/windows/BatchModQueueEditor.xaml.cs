using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MassEffectModManagerCore.modmanager.usercontrols;
using MassEffectModManagerCore.ui;

namespace MassEffectModManagerCore.modmanager.windows
{
    /// <summary>
    /// Interaction logic for BatchModQueueEditor.xaml
    /// </summary>
    public partial class BatchModQueueEditor : Window, INotifyPropertyChanged
    {
        private List<Mod> allMods;
        public ObservableCollectionExtended<Mod> VisibleFilteredMods { get; } = new ObservableCollectionExtended<Mod>();
        public ObservableCollectionExtended<Mod> ModsInGroup { get; } = new ObservableCollectionExtended<Mod>();
        public string GroupName { get; set; }
        public string GroupDescription { get; set; }
        public BatchModQueueEditor(List<Mod> allMods, Window owner = null, BatchLibraryInstallQueue queueToEdit = null)
        {
            Owner = owner;
            DataContext = this;
            this.allMods = allMods;
            LoadCommands();
            InitializeComponent();
            if (queueToEdit != null)
            {
                switch (queueToEdit.Game)
                {
                    case Mod.MEGame.ME1:
                        ME1_RadioButton.IsChecked = true;
                        break;
                    case Mod.MEGame.ME2:
                        ME2_RadioButton.IsChecked = true;
                        break;
                    case Mod.MEGame.ME3:
                        ME3_RadioButton.IsChecked = true;
                        break;
                }
                SelectedGame = queueToEdit.Game;
                GroupName = queueToEdit.QueueName;
                GroupDescription = queueToEdit.QueueDescription;
                ModsInGroup.ReplaceAll(queueToEdit.ModsToInstall);
                VisibleFilteredMods.RemoveRange(queueToEdit.ModsToInstall);
            }
        }

        public ICommand CancelCommand { get; set; }
        public ICommand SaveAndCloseCommand { get; set; }

        private void LoadCommands()
        {
            CancelCommand = new GenericCommand(CancelEditing);
            SaveAndCloseCommand = new GenericCommand(SaveAndClose, CanSave);
        }

        private void SaveAndClose()
        {

        }

        private bool CanSave()
        {
            return false;
        }

        private void CancelEditing()
        {
            Close();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public Mod.MEGame SelectedGame { get; set; }

        public void OnSelectedGameChanged()
        {
            if (SelectedGame != Mod.MEGame.Unknown)
            {
                VisibleFilteredMods.ReplaceAll(allMods.Where(x => x.Game == SelectedGame));
            }
            else
            {
                VisibleFilteredMods.ClearEx();
            }
        }

        private void ME1_Clicked(object sender, RoutedEventArgs e)
        {
            SelectedGame = Mod.MEGame.ME1;
        }
        private void ME2_Clicked(object sender, RoutedEventArgs e)
        {
            SelectedGame = Mod.MEGame.ME2;
        }
        private void ME3_Clicked(object sender, RoutedEventArgs e)
        {
            SelectedGame = Mod.MEGame.ME3;
        }
    }
}
