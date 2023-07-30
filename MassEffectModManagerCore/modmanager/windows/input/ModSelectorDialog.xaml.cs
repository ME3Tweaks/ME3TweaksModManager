using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.VisualStyles;
using Dark.Net;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Misc;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.windows
{
    /// <summary>
    /// Interaction logic for ModSelectorDialog.xaml
    /// </summary>
    public partial class ModSelectorDialog : Window, INotifyPropertyChanged
    {
        public ObservableCollectionExtended<Mod> AvailableMods { get; } = new ObservableCollectionExtended<Mod>();
        public List<Mod> SelectedMods { get; } = new List<Mod>();
        public string DialogCaption { get; set; }
        public string AcceptButtonText { get; set; }

        public ModSelectorDialog(Window owner, List<Mod> shownMods, string windowTitle, string selectorCaption, string acceptButtonText)
        {
            Title = windowTitle;
            DialogCaption = selectorCaption;
            AcceptButtonText = acceptButtonText;
            Owner = owner;
            AvailableMods.ReplaceAll(shownMods);
            LoadCommands();
            InitializeComponent();
            this.ApplyDarkNetWindowTheme();
        }



        public GenericCommand CommitModsCommand { get; set; }
        public GenericCommand CancelCommand { get; set; }
        public SelectionMode SelectionMode { get; set; }

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
