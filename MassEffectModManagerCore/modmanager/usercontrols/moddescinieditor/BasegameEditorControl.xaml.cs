using System.ComponentModel;
using System.Windows;
using IniParser.Model;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.modmanager.objects.mod;
using MassEffectModManagerCore.ui;

namespace MassEffectModManagerCore.modmanager.usercontrols.moddescinieditor
{
    /// <summary>
    /// Interaction logic for BasegameEditorControl.xaml
    /// </summary>
    public partial class BasegameEditorControl : ModdescEditorControlBase, INotifyPropertyChanged
    {
        public BasegameEditorControl()
        {
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {
            AddBasegameTaskCommand = new GenericCommand(AddBasegameTask, () => BasegameJob == null);
        }

        public override void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            if (!HasLoaded)
            {
                BasegameJob = EditingMod.GetJob(ModJob.JobHeader.BASEGAME);
                BasegameJob?.BuildParameterMap(EditingMod);
                //basegame_multilists_editor.OnLoaded(EditingMod);
                //basegame_alternatefiles_editor.OnLoaded(EditingMod);
                HasLoaded = true;
            }
        }

        public ModJob BasegameJob { get; set; }

        private void AddBasegameTask()
        {
            BasegameJob = new ModJob(ModJob.JobHeader.BASEGAME);
            BasegameJob.BuildParameterMap(EditingMod);
            EditingMod.InstallationJobs.Add(BasegameJob);
        }

        public GenericCommand AddBasegameTaskCommand { get; set; }

        public override void Serialize(IniData ini)
        {

        }
    }
}
