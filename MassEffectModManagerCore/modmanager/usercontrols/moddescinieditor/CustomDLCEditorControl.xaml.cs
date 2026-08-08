using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using IniParser.Model;
using LegendaryExplorerCore.Misc;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.exceptions;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.objects.mod.editor;

namespace ME3TweaksModManager.modmanager.usercontrols.moddescinieditor
{
    /// <summary>
    /// Interaction logic for CustomDLCEditorControl.xaml
    /// </summary>
    public partial class CustomDLCEditorControl : ModdescEditorControlBase, INotifyPropertyChanged
    {
        public override void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            if (!HasLoaded)
            {
                var job = EditingMod.GetJob(ModJob.JobHeader.CUSTOMDLC);
                job?.BuildParameterMap(EditingMod);
                CustomDLCJob = EditingMod.GetJob(ModJob.JobHeader.CUSTOMDLC);
                if (CustomDLCJob != null)
                {
                    CustomDLCJob.BuildParameterMap(EditingMod);
                    foreach (var v in CustomDLCJob.CustomDLCFolderMapping)
                    {
                        EditingMod.HumanReadableCustomDLCNames.TryGetValue(v.Value, out var hrName);
                        var cdp = new MDCustomDLCParameter
                        {
                            SourcePath = v.Key,
                            DestDLCName = v.Value,
                            HumanReadableName = hrName
                        };
                        cdp.PropertyChanged += CustomDLCPropertyChanged;
                        CustomDLCParameters.Add(cdp);
                    }

                    this.outdated_dlc_editor.LoadFromList(EditingMod.OutdatedCustomDLC);
                    this.incompatible_dlc_editor.LoadFromList(EditingMod.IncompatibleDLC);

                    // Build filtered descriptor parameters (excluding outdated/incompatible which are handled by DLCListDescriptorControl)
                    //08/03/2026 - there is no remaining parameters since splitting out the dlc lists, but 
                    // this is left here in case we eventually add more.
                    BuildRemainingDescriptorParameters();
                }

                HasLoaded = true;
            }

            //customdlc_multilists_editor.OnLoaded(newMod);
        }

        public CustomDLCEditorControl()
        {
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {
            AddCustomDLCCommand = new GenericCommand(AddCustomDLC, CanAddCustomDLC);
        }

        private bool CanAddCustomDLC() => CustomDLCJob == null || !CustomDLCParameters.Any() ||
                                          (!string.IsNullOrWhiteSpace(CustomDLCParameters.Last().SourcePath)
                                           && (!string.IsNullOrWhiteSpace(CustomDLCParameters.Last().DestDLCName) && CustomDLCParameters.Last().DestDLCName.StartsWith(@"DLC_")));

        private void AddCustomDLC()
        {
            if (CustomDLCJob == null)
            {
                // Generate the job
                CustomDLCJob = new ModJob(ModJob.JobHeader.CUSTOMDLC);
                CustomDLCJob.BuildParameterMap(EditingMod);
                EditingMod.InstallationJobs.Add(CustomDLCJob);
            }

            var job = CustomDLCJob;
            CustomDLCJob = null;
            CustomDLCJob = job; // Rebind??/s

            var cdp = new MDCustomDLCParameter();
            cdp.PropertyChanged += CustomDLCPropertyChanged;
            CustomDLCParameters.Add(cdp); //empty data
        }

        private void CustomDLCPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MDCustomDLCParameter.SourcePath) || e.PropertyName == nameof(MDCustomDLCParameter.DestDLCName))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public GenericCommand AddCustomDLCCommand { get; set; }

        public ModJob CustomDLCJob { get; set; }

        public ObservableCollectionExtended<MDCustomDLCParameter> CustomDLCParameters { get; } = new ObservableCollectionExtended<MDCustomDLCParameter>();

        public ObservableCollectionExtended<MDParameter> RemainingDescriptorParameters { get; } = new ObservableCollectionExtended<MDParameter>();

        private void BuildRemainingDescriptorParameters()
        {
            RemainingDescriptorParameters.ClearEx();

            if (CustomDLCJob?.ParameterMap == null)
                return;

            // Only include descriptors that are NOT handled by DLCListDescriptorControl
            // (i.e., exclude outdatedcustomdlc and incompatiblecustomdlc)
            var excludedKeys = new HashSet<string>
            {
                Mod.MODDESC_DESCRIPTOR_CUSTOMDLC_OUTDATEDDLC,
                Mod.MODDESC_DESCRIPTOR_CUSTOMDLC_INCOMPATIBLEDLC,
                Mod.MODDESC_DESCRIPTOR_CUSTOMDLC_SOURCEDIRS,
                Mod.MODDESC_DESCRIPTOR_CUSTOMDLC_DESTDIRS
            };

            foreach (var param in CustomDLCJob.ParameterMap)
            {
                if (!excludedKeys.Contains(param.Key))
                {
                    RemainingDescriptorParameters.Add(new MDParameter
                    {
                        Key = param.Key,
                        Value = param.Value
                    });
                }
            }
        }

        public override void Serialize(IniData ini)
        {
            if (CustomDLCJob != null)
            {
                // Not the best implementation but it'll work
                var sourceFoldersDup = CustomDLCParameters.GroupBy(x => x.SourcePath).Any(group => group.Count() > 1);
                var destFoldersDup = CustomDLCParameters.GroupBy(x => x.DestDLCName).Any(group => group.Count() > 1);

                if (sourceFoldersDup || destFoldersDup)
                {
                    // This will be handled in serializer
                    throw new ModDescSerializerException(M3L.GetString(M3L.string_mde_validation_customDLCUniqueNames));
                }

                var srcDirs = CustomDLCParameters.ToDictionary(x => x.SourcePath, x => x.DestDLCName);

                if (srcDirs.Any())
                {
                    ini[Mod.MODDESC_HEADERKEY_CUSTOMDLC][Mod.MODDESC_DESCRIPTOR_CUSTOMDLC_SOURCEDIRS] = string.Join(';', srcDirs.Keys);
                    ini[Mod.MODDESC_HEADERKEY_CUSTOMDLC][Mod.MODDESC_DESCRIPTOR_CUSTOMDLC_DESTDIRS] = string.Join(';', srcDirs.Values);

                    foreach (var v in CustomDLCParameters.Where(x => !string.IsNullOrWhiteSpace(x.HumanReadableName)))
                    {
                        ini[Mod.MODDESC_HEADERKEY_CUSTOMDLC][v.DestDLCName] = v.HumanReadableName;
                    }
                }

                outdated_dlc_editor.Serialize(Mod.MODDESC_HEADERKEY_CUSTOMDLC, ini);
                incompatible_dlc_editor.Serialize(Mod.MODDESC_HEADERKEY_CUSTOMDLC, ini);
                customdlc_multilists_editor.Serialize(ini);

                // Serialize all descriptor parameters from ParameterMap
                // The DLCListDescriptorControl instances have already updated the ParameterMap
                //foreach (var p in CustomDLCJob.ParameterMap)
                //{
                //    // sourcedirs and destdirs was serialized above
                //    // Add any extra keys here that are not sourcedirs or destdirs that need serialized
                //    if (!string.IsNullOrWhiteSpace(p.Value) && 
                //        (p.Key == Mod.MODDESC_DESCRIPTOR_CUSTOMDLC_INCOMPATIBLEDLC || 
                //         p.Key == Mod.MODDESC_DESCRIPTOR_CUSTOMDLC_REQUIREDDLC || 
                //         p.Key == Mod.MODDESC_DESCRIPTOR_CUSTOMDLC_OUTDATEDDLC))
                //    {
                //        ini[Mod.MODDESC_HEADERKEY_CUSTOMDLC][p.Key] = p.Value;
                //    }
                //}
            }
        }
    }

    public class MDCustomDLCParameter : INotifyPropertyChanged
    {
        public string HumanReadableName { get; set; } = "";
        public string DestDLCName { get; set; } = "";
        public string SourcePath { get; set; } = "";
        //Fody uses this property on weaving
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }
}
