using System.ComponentModel;
using System.Windows;
using IniParser.Model;
using LegendaryExplorerCore.Packages;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols.moddescinieditor
{
    /// <summary>
    /// Interaction logic for BalanceChangesTaskEditorControl.xaml
    /// </summary>
    public partial class BalanceChangesTaskEditorControl : ModdescEditorControlBase, INotifyPropertyChanged
    {
        public BalanceChangesTaskEditorControl()
        {
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {
            AddBalanceChangesJobCommand = new GenericCommand(AddBalanceChangesJob, ()=> BalanceChangesJob == null);
        }

        public GenericCommand AddBalanceChangesJobCommand { get; set; }

        private void AddBalanceChangesJob()
        {
            BalanceChangesJob = new ModJob(ModJob.JobHeader.BALANCE_CHANGES, EditingMod);
            BalanceChangesJob.BuildParameterMap(EditingMod);
            EditingMod.InstallationJobs.Add(BalanceChangesJob);
        }

        public ModJob BalanceChangesJob { get; set; }

        public override void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (HasLoaded) return;
            if (EditingMod.Game == MEGame.ME3)
            {
                BalanceChangesJob = EditingMod.GetJob(ModJob.JobHeader.BALANCE_CHANGES);
                BalanceChangesJob?.BuildParameterMap(EditingMod);
            }
            HasLoaded = true;
        }

        public override void Serialize(IniData ini)
        {
            if (BalanceChangesJob != null)
            {
                foreach (var p in BalanceChangesJob.ParameterMap)
                {
                    if (!string.IsNullOrEmpty(p.Value))
                    {
                        ini[BalanceChangesJob.Header.ToString()][p.Key] = p.Value;
                    }
                }
            }
        }
    }
}
