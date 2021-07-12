using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using MassEffectModManagerCore.modmanager.objects.mod;
using MassEffectModManagerCore.ui;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Misc;

namespace MassEffectModManagerCore.modmanager.windows
{
    /// <summary>
    /// Interaction logic for ModSelectorDialog.xaml
    /// </summary>
    public partial class ModSelectorDialog : Window, INotifyPropertyChanged
    {
        public ObservableCollectionExtended<Mod> AvailableMods { get; } = new ObservableCollectionExtended<Mod>();
        public List<Mod> SelectedMods { get; } = new List<Mod>();

        public ModSelectorDialog(Window owner, List<Mod> shownMods)
        {
            Owner = owner;
            AvailableMods.ReplaceAll(shownMods);
            LoadCommands();
            InitializeComponent();
        }

        public GenericCommand CommitModsCommand { get; set; }
        public GenericCommand CancelCommand { get; set; }

        private void LoadCommands()
        {
            CommitModsCommand = new GenericCommand(CommitMods, CanCommitMods);
            CancelCommand = new GenericCommand(Cancel);
        }

        private void Cancel()
        {
            DialogResult = false;
            Close();
        }


        private bool CanCommitMods() => ModListBox != null && ModListBox.SelectedItems.Count > 0;

        private void CommitMods()
        {
            SelectedMods.ReplaceAll(ModListBox.SelectedItems.Cast<Mod>());
            DialogResult = true;
            Close();
        }

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore

    }
}
