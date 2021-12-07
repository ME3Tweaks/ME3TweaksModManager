using System.ComponentModel;
using System.Linq;
using System.Windows;
using IniParser.Model;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Services.ThirdPartyModIdentification;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.mod.editor;
using ME3TweaksModManager.modmanager.windows;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols.moddescinieditor
{
    /// <summary>
    /// Interaction logic for LocalizationTaskEditorControl.xaml
    /// </summary>
    public partial class LocalizationTaskEditorControl : ModdescEditorControlBase, INotifyPropertyChanged
    {
        public LocalizationTaskEditorControl()
        {
            LoadCommands();
            InitializeComponent();
        }


        public ModJob LocalizationJob { get; set; }
        public ObservableCollectionExtended<MDParameter> Files { get; } = new ObservableCollectionExtended<MDParameter>();

        private void LoadCommands()
        {
            SetToLocalizationModCommand = new GenericCommand(ConvertModToLocalizationMod, CanConvertModToLocalizationMod);
            AddLocalizationFileCommand = new GenericCommand(AddTLK, CanAddTLK);
        }

        public GenericCommand AddLocalizationFileCommand { get; set; }

        private void AddTLK()
        {
            Files.Add(new MDParameter(@"string", M3L.GetString(M3L.string_tlkFilename), ""));
        }

        private bool CanAddTLK()
        {
            return LocalizationJob != null && (!Files.Any() || !string.IsNullOrWhiteSpace(Files.Last().Value));
        }

        private void ConvertModToLocalizationMod()
        {
            if (Window.GetWindow(this) is ModDescEditor ed)
            {
                ed.ConvertModToLocalizationMod();
            }

            EditingMod.InstallationJobs.Clear();
            TargetMod = "";
            LocalizationJob = new ModJob(ModJob.JobHeader.LOCALIZATION, EditingMod);
            EditingMod.InstallationJobs.Add(LocalizationJob);
        }

        private bool CanConvertModToLocalizationMod() => LocalizationJob == null;

        public GenericCommand SetToLocalizationModCommand { get; set; }

        public string TargetMod { get; set; }
        public string TPMIModName { get; set; }

        public void OnTargetModChanged()
        {
            var tpmi = TPMIService.GetThirdPartyModInfo(TargetMod, EditingMod.Game);
            TPMIModName = tpmi?.modname;
        }


        public string SetToLocalizationModText { get; set; }

        public override void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (HasLoaded) return;
            if (EditingMod.Game >= MEGame.ME2)
            {
                TargetMod = EditingMod?.RequiredDLC.FirstOrDefault();
                LocalizationJob = EditingMod?.GetJob(ModJob.JobHeader.LOCALIZATION);
                if (LocalizationJob != null)
                {
                    SetToLocalizationModText = M3L.GetString(M3L.string_modAlreadyLocalizationMod);
                    Files.ReplaceAll(LocalizationJob.LocalizationFilesStrRaw.Split(';').Select(x => new MDParameter(@"string", M3L.GetString(M3L.string_tlkFilename), x)));
                }
                else
                {
                    SetToLocalizationModText = M3L.GetString(M3L.string_setToLocalizationMod);
                    Files.ClearEx();
                }
            }

            HasLoaded = true;
        }


        public override void Serialize(IniData ini)
        {
            if (LocalizationJob != null)
            {
                if (Files.Any())
                {
                    ini[LocalizationJob.Header.ToString()][@"files"] = string.Join(';', Files.Select(x => x.Value));
                }

                if (!string.IsNullOrWhiteSpace(TargetMod))
                {
                    ini[LocalizationJob.Header.ToString()][@"dlcname"] = TargetMod;
                }
            }
        }
    }
}
