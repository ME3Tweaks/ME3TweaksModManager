using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using IniParser.Model;
using LegendaryExplorerCore.Misc;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.mod.editor;

namespace ME3TweaksModManager.modmanager.usercontrols.moddescinieditor
{
    /// <summary>
    /// Interaction logic for MetadataEditorControl.xaml
    /// </summary>
    public partial class MetadataEditorControl : ModdescEditorControlBase, INotifyPropertyChanged
    {
        public MetadataEditorControl()
        {
            InitializeComponent();
        }

        public ObservableCollectionExtended<MDParameter> ModManagerParameterMap { get; } = new ObservableCollectionExtended<MDParameter>();
        public ObservableCollectionExtended<MDParameter> ModInfoParameterMap { get; } = new ObservableCollectionExtended<MDParameter>();
        public ObservableCollectionExtended<MDParameter> UPDATESParameterMap { get; } = new ObservableCollectionExtended<MDParameter>();

        public override void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            if (!HasLoaded)
            {
                EditingMod.BuildParameterMap(EditingMod);
                ModManagerParameterMap.ReplaceAll(EditingMod.ParameterMap.Where(x => x.Header == @"ModManager"));
                ModInfoParameterMap.ReplaceAll(EditingMod.ParameterMap.Where(x => x.Header == @"ModInfo"));
                UPDATESParameterMap.ReplaceAll(EditingMod.ParameterMap.Where(x => x.Header == @"UPDATES"));
                HasLoaded = true;
            }
        }

        public override void Serialize(IniData ini)
        {
            foreach (var v in EditingMod.ParameterMap) //references will still be same
            {
                if (v.Header == @"ModInfo" && v.Key== @"requireddlc" && EditingMod.GetJob(ModJob.JobHeader.LOCALIZATION) != null)
                {
                    // Do not store RequiredDLC in localization mod.
                    continue;
                }

                if (v.Key == @"cmmver" && v.Header == @"ModManager")
                {
                    // Editor only can write latest version format
                    v.Value = App.HighestSupportedModDesc.ToString(CultureInfo.InvariantCulture);
                }


                if (!string.IsNullOrWhiteSpace(v.Value))
                {
                    if (v.Key == @"moddesc" && v.Header == @"ModInfo")
                    {
                        // Convert what's written into moddesc
                        ini[v.Header][v.Key] = M3Utilities.ConvertNewlineToBr(v.Value);
                    }
                    else
                    {
                        ini[v.Header][v.Key] = v.Value;

                    }
                }
            }
        }
    }
}
