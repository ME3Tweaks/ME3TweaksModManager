using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.ME3Tweaks.ModManager.Interfaces;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.extensions;

namespace ME3TweaksModManager.modmanager.windows
{
    /// <summary>
    /// Interaction logic for ModSelectorDialog.xaml
    /// </summary>
    public partial class ModSelectorDialog : Window, INotifyPropertyChanged
    {
        public ObservableCollectionExtended<IDisplayableMod> AvailableMods { get; } = new ObservableCollectionExtended<IDisplayableMod>();
        public List<IDisplayableMod> SelectedMods { get; } = new List<IDisplayableMod>();
        public string DialogCaption { get; set; }
        public string AcceptButtonText { get; set; }

        public ModSelectorDialog(Window owner, List<IDisplayableMod> shownMods, string windowTitle, string selectorCaption, string acceptButtonText)
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
            SelectedMods.ReplaceAll(ModListBox.SelectedItems.Cast<IDisplayableMod>());
            DialogResult = true;
            Close();
        }

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore

    }
}
