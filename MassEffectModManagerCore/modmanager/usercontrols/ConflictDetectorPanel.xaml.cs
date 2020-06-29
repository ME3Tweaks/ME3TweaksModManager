using System;
using System.Linq;
using System.Windows.Input;
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.memoryanalyzer;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ConflictDetectorPanel.xaml
    /// </summary>
    public partial class ConflictDetectorPanel : MMBusyPanelBase
    {
        public ObservableCollectionExtended<GameTarget> ConflictTargets { get; } = new ObservableCollectionExtended<GameTarget>();
        public ObservableCollectionExtended<SupercedanceList> Supercedances { get; } = new ObservableCollectionExtended<SupercedanceList>();
        public GameTarget SelectedTarget { get; set; }
        public ConflictDetectorPanel()
        {
            MemoryAnalyzer.AddTrackedMemoryItem(@"Custom DLC Conflict Detector Panel", new WeakReference(this));
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }
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
            //throw new NotImplementedException();
        }

        public void OnSelectedTargetChanged()
        {
            Supercedances.ClearEx();
            if (SelectedTarget != null)
            {
                // maps DLC folder name -> mount number
                var mountpriorities = MEDirectories.GetMountPriorities(SelectedTarget);
                //maps filename to list of DLC in order of precedence
                var supercedances = MEDirectories.GetFileSupercedances(SelectedTarget).Where(x => x.Value.Count > 1).ToList();
                foreach (var supercedance in supercedances)
                {
                    SupercedanceList sl = new SupercedanceList()
                    {
                        Filename = supercedance.Key,
                        WinningFile = new SupercedanceFile()
                        {
                            DLCName = supercedance.Value.First(),
                            MountPriority = mountpriorities[supercedance.Value.First()],
                            Game = SelectedTarget.Game
                        },
                        Game = SelectedTarget.Game
                    };
                    sl.LosingFiles.ReplaceAll(supercedance.Value.Skip(1).Take(supercedance.Value.Count - 1).Select(x => new SupercedanceFile()
                    {
                        DLCName = x,
                        MountPriority = mountpriorities[x],
                        Game = SelectedTarget.Game
                    }));

                    Supercedances.Add(sl);
                    //var dlcname = supercedance.Value
                    //SupercedanceFile winningFile = new SupercedanceFile()
                    //{
                    //    Fi
                    //    MountPriority = mountpriorities[],
                    //    DLCName = supercedance.Value.First()
                    //};
                }
            }
        }

        public override void OnPanelVisible()
        {
            ConflictTargets.ReplaceAll(mainwindow.InstallationTargets.Where(x => x.Selectable));
            SelectedTarget = ConflictTargets.FirstOrDefault();
        }

        public class SupercedanceList
        {
            public Mod.MEGame Game;
            public string UIMountPriorityString => M3L.GetString(M3L.string_interp_mountPriorityX, WinningFile.MountPriority);
            public string UINumSupercededString => M3L.GetString(M3L.string_interp_XModsSuperceded, LosingFiles.Count);
            private string tpminame;
            public string UISourceString
            {
                get
                {
                    if (tpminame != null) return $@"{tpminame} ({WinningFile.DLCName})";
                    var tpmi = ThirdPartyServices.GetThirdPartyModInfo(WinningFile.DLCName, Game);
                    if (tpmi != null)
                    {
                        tpminame = tpmi.modname;
                        return $@"{tpminame} ({WinningFile.DLCName})";
                    }

                    //no TPMI
                    return WinningFile.DLCName;
                }
            }

            public string Filename { get; set; }
            public SupercedanceFile WinningFile { get; set; }

            public ObservableCollectionExtended<SupercedanceFile> LosingFiles { get; } = new ObservableCollectionExtended<SupercedanceFile>();
        }
        public class SupercedanceFile
        {
            public Mod.MEGame Game;
            public string UIMountPriorityString => M3L.GetString(M3L.string_interp_mountPriorityX, MountPriority);
            private string tpminame;
            public string UISourceString
            {
                get
                {
                    if (tpminame != null) return $@"{tpminame} ({DLCName})";
                    var tpmi = ThirdPartyServices.GetThirdPartyModInfo(DLCName, Game);
                    if (tpmi != null)
                    {
                        tpminame = tpmi.modname;
                        return $@"{tpminame} ({DLCName})";
                    }

                    //no TPMI
                    return DLCName;
                }
            }
            public int MountPriority;
            public string DLCName { get; set; }
        }
    }
}
