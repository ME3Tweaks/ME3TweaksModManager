using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;

using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using Serilog;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for OfficialDLCToggler.xaml
    /// </summary>
    public partial class OfficialDLCToggler : MMBusyPanelBase
    {
        public OfficialDLCToggler()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }

        public ObservableCollectionExtended<GameTarget> AvailableTargets { get; } = new ObservableCollectionExtended<GameTarget>();
        public ObservableCollectionExtended<InstalledDLC> InstalledDLCs { get; } = new ObservableCollectionExtended<InstalledDLC>();
        public GameTarget SelectedTarget { get; set; }
        public ICommand CloseCommand { get; set; }

        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(ClosePanel);
        }

        private void ClosePanel()
        {
            OnClosing(DataEventArgs.Empty);
        }


        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
        }

        public void OnSelectedTargetChanged()
        {
            InstalledDLCs.ClearEx();
            if (SelectedTarget != null)
            {
                // maps DLC folder name -> mount number
                var installedDlc = VanillaDatabaseService.GetInstalledOfficialDLC(SelectedTarget, true);
                foreach (var dlc in installedDlc)
                {
                    Debug.WriteLine(dlc);
                    var foldername = dlc.TrimStart('x');
                    InstalledDLCs.Add(new InstalledDLC()
                    {
                        target = SelectedTarget,
                        DLCFolderName = dlc,
                        UIDLCFolderName = foldername,
                        Enabled = !dlc.StartsWith('x'),
                        HumanName = ThirdPartyServices.GetThirdPartyModInfo(foldername, SelectedTarget.Game).modname
                    });
                }


            }
        }

        public override void OnPanelVisible()
        {
            AvailableTargets.ReplaceAll(mainwindow.InstallationTargets.Where(x => x.Selectable));
            SelectedTarget = AvailableTargets.FirstOrDefault();
        }

        public class InstalledDLC : INotifyPropertyChanged
        {
            public GameTarget target { get; set; }
            /// <summary>
            /// Current DLC Folder Name
            /// </summary>
            public string DLCFolderName { get; set; }
            /// <summary>
            /// Doesn't show 'disabled' X, the always enabled one
            /// </summary>
            public string UIDLCFolderName { get; set; }
            public bool Enabled { get; set; }
            public string HumanName { get; set; }
            public string ToggleText => Enabled ? M3L.GetString(M3L.string_toggleOff) : M3L.GetString(M3L.string_toggleOn);
            //Fody uses this property on weaving
#pragma warning disable 0169
public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore 0169

            public ICommand ToggleCommand { get; }
            public InstalledDLC()
            {
                ToggleCommand = new GenericCommand(ToggleDLC);
            }

            private void ToggleDLC()
            {
                try
                {
                    var dlcFPath = M3Directories.GetDLCPath(target);
                    var currentDLCPath = Path.Combine(dlcFPath, DLCFolderName);
                    string destPath = Path.Combine(dlcFPath, Enabled ? @"x" + UIDLCFolderName : UIDLCFolderName);
                    Directory.Move(currentDLCPath, destPath);
                    Enabled = !Enabled;
                    DLCFolderName = Enabled ? UIDLCFolderName : @"x" + UIDLCFolderName;
                }
                catch (Exception e)
                {
                    Log.Error($@"Error toggling DLC {DLCFolderName}: {e.Message}");
                    M3L.ShowDialog(Application.Current?.MainWindow, M3L.GetString(M3L.string_interp_errorTogglingDLC, e.Message), M3L.GetString(M3L.string_error), MessageBoxButton.OK, MessageBoxImage.Error); //this needs updated to be better
                }
            }
        }
    }
}
