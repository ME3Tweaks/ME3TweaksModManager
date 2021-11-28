using System.ComponentModel;
using System.Windows;
using IniParser.Model;
using ME3TweaksModManager.modmanager.objects;

namespace ME3TweaksModManager.modmanager.usercontrols.moddescinieditor
{
    /// <summary>
    /// Interaction logic for LELauncherEditorControl.xaml
    /// </summary>
    public partial class LELauncherEditorControl : ModdescEditorControlBase, INotifyPropertyChanged
    {
        public LELauncherEditorControl()
        {
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {

        }

        public override void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            if (!HasLoaded)
            {
                var LELauncherJob = EditingMod.GetJob(ModJob.JobHeader.LELAUNCHER);
                FileDir = LELauncherJob?.JobDirectory;
                HasLoaded = true;
            }
        }

        public string FileDir { get; set; }

        public override void Serialize(IniData ini)
        {
            if (!string.IsNullOrWhiteSpace(FileDir))
            {
                ini[ModJob.JobHeader.LELAUNCHER.ToString()][@"moddir"] = FileDir;
            }
        }
    }
}
