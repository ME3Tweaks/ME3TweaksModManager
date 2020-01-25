using MassEffectModManagerCore.ui;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for FailedModsPanel.xaml
    /// </summary>
    public partial class FailedModsPanel : MMBusyPanelBase
    {
        public FailedModsPanel(List<Mod> FailedMods)
        {
            DataContext = this;
            this.FailedMods.ReplaceAll(FailedMods);
            LoadCommands();
            InitializeComponent();
        }

        public Mod SelectedMod { get; set; }
        public ICommand RestoreSelectedModCommand { get; set; }
        public ICommand DebugReloadCommand { get; set; }
        private void LoadCommands()
        {
            RestoreSelectedModCommand = new GenericCommand(CloseToRestoreMod, CanRestoreMod);
            DebugReloadCommand = new GenericCommand(DebugReloadMod, CanDebugReload);
        }

        private void DebugReloadMod()
        {
#if DEBUG
            Mod m = new Mod(SelectedMod.ModDescPath, Mod.MEGame.Unknown);
            Debug.WriteLine("Is valid: " + m.ValidMod);
#endif
        }

        private bool CanRestoreMod()
        {
            return SelectedMod != null && SelectedMod.IsME3TweaksUpdatable;
        }

        private bool CanDebugReload()
        {
#if DEBUG
            return SelectedMod != null;
#else
            return false;
#endif
        }

        private void CloseToRestoreMod()
        {
            OnClosing(new DataEventArgs(SelectedMod));
        }

        public ObservableCollectionExtended<Mod> FailedMods { get; } = new ObservableCollectionExtended<Mod>();

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
            OnClosing(DataEventArgs.Empty);
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        public override void OnPanelVisible()
        {

        }
    }
}
