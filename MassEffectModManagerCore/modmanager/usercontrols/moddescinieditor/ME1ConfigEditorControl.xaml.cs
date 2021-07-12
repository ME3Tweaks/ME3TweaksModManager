using System.ComponentModel;
using System.Linq;
using System.Windows;
using IniParser.Model;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.modmanager.objects.mod.editor;
using MassEffectModManagerCore.ui;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;

namespace MassEffectModManagerCore.modmanager.usercontrols.moddescinieditor
{
    /// <summary>
    /// Interaction logic for ME1ConfigEditorControl.xaml
    /// </summary>
    public partial class ME1ConfigEditorControl : ModdescEditorControlBase, INotifyPropertyChanged
    {
        public ME1ConfigEditorControl()
        {
            LoadCommands();
            InitializeComponent();
        }

        public ModJob ConfigJob { get; set; }
        public string ModDir { get; set; }
        public ObservableCollectionExtended<MDParameter> Files { get; } = new ObservableCollectionExtended<MDParameter>();

        private void LoadCommands()
        {
            AddME1ConfigTaskCommand = new GenericCommand(AddME1ConfigTask, () => ConfigJob == null);
            AddConfigFileCommand = new GenericCommand(AddConfigFile, CanAddConfigFile);
        }

        public GenericCommand AddME1ConfigTaskCommand { get; set; }

        private void AddME1ConfigTask()
        {
            ModDir = "";
            ConfigJob = new ModJob(ModJob.JobHeader.ME1_CONFIG, EditingMod);
            EditingMod.InstallationJobs.Add(ConfigJob);
        }

        public GenericCommand AddConfigFileCommand { get; set; }

        private void AddConfigFile()
        {
            Files.Add(new MDParameter(@"string", M3L.GetString(M3L.string_configFile), @""));
        }

        private bool CanAddConfigFile()
        {
            return ConfigJob != null && !string.IsNullOrWhiteSpace(ModDir) && (!Files.Any() || !string.IsNullOrWhiteSpace(Files.Last().Value));
        }

        public override void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (HasLoaded) return;
            if (EditingMod.Game == MEGame.ME1)
            {
                ConfigJob = EditingMod.GetJob(ModJob.JobHeader.ME1_CONFIG);
                if (ConfigJob != null)
                {
                    ModDir = ConfigJob.JobDirectory;
                    Files.ReplaceAll(ConfigJob.ConfigFilesRaw.Split(';')
                        .Select(x => new MDParameter(@"string", M3L.GetString(M3L.string_configFile), x)));
                }
            }

            HasLoaded = true;
        }

        public override void Serialize(IniData ini)
        {
            if (ConfigJob != null)
            {
                if (Files.Any())
                {
                    ini[ConfigJob.Header.ToString()][@"configfiles"] = string.Join(';', Files.Where(x => !string.IsNullOrWhiteSpace(x.Value)).Select(x => x.Value));
                }

                if (!string.IsNullOrWhiteSpace(ModDir))
                {
                    ini[ConfigJob.Header.ToString()][@"moddir"] = ModDir;
                }
            }
        }
    }
}
