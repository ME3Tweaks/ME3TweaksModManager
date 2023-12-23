using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Localization;
using ME3TweaksCore.ME3Tweaks.StarterKit;
using ME3TweaksCore.Services.ThirdPartyModIdentification;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.memoryanalyzer;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.ui;
using MvvmValidation;

namespace ME3TweaksModManager.modmanager.windows
{
    /// <summary>
    /// Interaction logic for StarterKitGeneratorWindow.xaml
    /// </summary>
    public partial class StarterKitGeneratorWindow : ValidatableWindowBase
    {
        public int MaxMountForGame
        {
            get
            {
                switch (Game)
                {
                    case MEGame.ME3:
                        return 4800;
                    case MEGame.ME2:
                        return 2000; //sensible?
                    case MEGame.ME1:
                        return 500; //sensible?

                    // Supports up to 2 billion
                    case MEGame.LE1:
                    case MEGame.LE2:
                    case MEGame.LE3:
                        return 32000;
                }
                return 500;
            }
        }

        public int MinMountForGame
        {
            get
            {
                switch (Game)
                {
                    case MEGame.ME3:
                        return 1005;
                    case MEGame.ME2:
                        return 1; //sensible?
                    case MEGame.ME1:
                        return 1; //sensible?
                    case MEGame.LE1:
                        return 10;
                    case MEGame.LE2:
                        return 2500;
                    case MEGame.LE3:
                        return 4000;
                }
                return 1;
            }
        }

        public ThirdPartyModInfo PreviewTPMI { get; } = new ThirdPartyModInfo();
        public static GridLength VisibleRowHeight { get; } = new GridLength(25);
        public string BusyText { get; set; }
        public bool IsBusy { get; set; }

        public string FilterText { get; set; }
        public void OnFilterTextChanged()
        {
            CustomDLCMountsForGame.Refresh();
        }

        public ObservableCollectionExtended<ThirdPartyModInfo> ListedEntries { get; } = new();
        public ICollectionView CustomDLCMountsForGame => CollectionViewSource.GetDefaultView(ListedEntries);
        private bool FilterTMPIEntries(object obj)
        {
            if (!string.IsNullOrWhiteSpace(FilterText) && obj is ThirdPartyModInfo tpmi)
            {
                return tpmi.StarterKitString.Contains(FilterText, StringComparison.InvariantCultureIgnoreCase);
            }
            return true;
        }


        #region FEATURE FLAGS
        // LE1, Game 2, Game 3
        public bool AddStartupFile { get; set; }
        public bool AddPlotManagerData { get; set; }

        // Game 1
        public ObservableCollectionExtended<Bio2DAOption> Selected2DAs { get; } = new();

        // Game 3
        public bool AddModSettingsMenuData { get; set; } = true; // LE3 only
        public bool AddSquadmateMerge3Garrus { get; set; }
        public bool AddSquadmateMerge3Liara { get; set; }
        public bool AddSquadmateMerge3EDI { get; set; }
        public bool AddSquadmateMerge3James { get; set; }
        public bool AddSquadmateMerge3Javik { get; set; }
        public bool AddSquadmateMerge3Ashley { get; set; }
        public bool AddSquadmateMerge3Kaidan { get; set; }
        public bool AddSquadmateMerge3Tali { get; set; }

        #endregion


        public string ModDescription { get; set; } = "";

        private string _modDeveloper;

        public string ModDeveloper
        {
            get => _modDeveloper;
            set
            {
                SetProperty(ref _modDeveloper, value);
                Validator.Validate(nameof(ModDeveloper));
            }
        }

        private string _modName;
        public string ModName
        {
            get => _modName;
            set
            {
                SetProperty(ref _modName, value);
                PreviewTPMI.modname = _modName;
                Validator.Validate(nameof(ModName));
            }
        }

        private string _modInternalName;
        public string ModInternalName
        {
            get => _modInternalName;
            set
            {
                SetProperty(ref _modInternalName, value);
                Validator.Validate(nameof(ModInternalName));
            }
        }

        private string _modDLCFolderName;
        public string ModDLCFolderName
        {
            get => _modDLCFolderName;
            set
            {
                SetProperty(ref _modDLCFolderName, value);
                Validator.Validate(nameof(ModDLCFolderName));
            }
        }

        private int _modMountPriority;
        public int ModMountPriority
        {
            get => _modMountPriority;
            set
            {
                SetProperty(ref _modMountPriority, value);
                var res = Validator.Validate(nameof(ModMountPriority));
                if (res.IsValid)
                {
                    PreviewTPMI.mountpriority = value.ToString();
                    ListedEntries.SortDescending(x => x.MountPriorityInt);
                    FilterTMPIEntries(null);
                    CustomDLCMountsListBox?.ScrollIntoView(PreviewTPMI);
                }
            }
        }
        private int _modInternalTLKID;
        public int ModInternalTLKID
        {
            get => _modInternalTLKID;
            set
            {
                SetProperty(ref _modInternalTLKID, value);
                Validator.Validate(nameof(ModInternalTLKID));
            }
        }
        private string _modURL;

        public string ModURL
        {
            get => _modURL;
            set
            {
                SetProperty(ref _modURL, value);
                Validator.Validate(nameof(ModURL));
            }
        }

        private int _modDLCModuleNumber;
        public int ModDLCModuleNumber
        {
            get { return _modDLCModuleNumber; }
            set
            {
                SetProperty(ref _modDLCModuleNumber, value);
                Validator.Validate(nameof(ModDLCModuleNumber));
            }
        }

        public ObservableCollectionExtended<MountFlag> DisplayedMountFlags { get; } = new ObservableCollectionExtended<MountFlag>();
        //private readonly List<MountFlag> ME1MountFlags = new List<MountFlag>();
        private readonly List<MountFlag> ME2MountFlags = new List<MountFlag>();
        private readonly List<MountFlag> ME3MountFlags = new List<MountFlag>();

        // Doesn't change so you can bind to this
        public MEGameSelector[] Games { get; init; }

        public StarterKitGeneratorWindow(MEGame Game) : base()
        {
            M3MemoryAnalyzer.AddTrackedMemoryItem(@"Starter Kit Window", this);
            M3Log.Information(@"Opening Starter Kit window");

            PendingGame = Game;
            CustomDLCMountsForGame.Filter = FilterTMPIEntries;


            var flagset2 = Enum.GetValues<EME2MountFileFlag>();
            ME2MountFlags.ReplaceAll(flagset2.Select(x => new MountFlag((int)x, true)));

            var flagset3 = Enum.GetValues<EME3MountFileFlag>();
            ME3MountFlags.ReplaceAll(flagset3.Select(x => new MountFlag((int)x, false)));

            PreviewTPMI.IsPreview = true;
            LoadCommands();
            InitializeComponent();
            Games = MEGameSelector.GetGameSelectors().ToArray();
        }

        public MEGame? PendingGame { get; set; }

        private void SetupValidation()
        {
            #region Validation

            Validator.AddRule(nameof(ModName), () =>
            {
                if (string.IsNullOrWhiteSpace(ModName)) return RuleResult.Invalid(M3L.GetString(M3L.string_modNameCannotBeEmpty));
                var r = new Regex("[A-Z,a-z,0-9,\\-,',., ,|,\",]+"); //do not localize
                if (!r.IsMatch(ModName))
                {
                    return RuleResult.Invalid(M3L.GetString(M3L.string_modNameCanOnlyContain));
                }
                var sanitized = MUtilities.SanitizePath(ModName, true);
                if (sanitized.Length == 0)
                {
                    return RuleResult.Invalid(M3L.GetString(M3L.string_modNameWillNotResolveToAUsableFilesystemPath));
                }
                if (sanitized.Contains(@".."))
                {
                    return RuleResult.Invalid(M3L.GetString(M3L.string_modNameCannotContainDoubleDots));
                }
                //Debug.WriteLine(M3L.GetString(M3L.string_validationOK));
                return RuleResult.Valid();
            });

            Validator.AddRule(nameof(ModMountPriority), () =>
            {
                if (ModMountPriority <= MinMountForGame || ModMountPriority >= MaxMountForGame)
                {
                    return RuleResult.Invalid(M3L.GetString(M3L.string_interp_valueMustBeBetweenXAndY, MinMountForGame, MaxMountForGame));
                }
                return RuleResult.Valid();
            });

            Validator.AddRule(nameof(ModInternalTLKID), () =>
            {
                if (ModInternalTLKID <= 0)
                {
                    return RuleResult.Invalid(M3L.GetString(M3L.string_internalTLKIDMustBeGreaterThan0));
                }
                return RuleResult.Valid();
            });

            Validator.AddRequiredRule(() => ModDeveloper, M3L.GetString(M3L.string_modDeveloperNameIsRequired));
            Validator.AddRequiredRule(() => ModDescription, M3L.GetString(M3L.string_modDescriptionIsRequired));
            Validator.AddRule(nameof(ModDLCFolderName), () =>
            {
                //Debug.WriteLine("MDFN " + ModDLCFolderName);
                if (string.IsNullOrWhiteSpace(ModDLCFolderName))
                    return RuleResult.Invalid(M3L.GetString(M3L.string_dLCFolderNameCannotBeEmpty));
                Regex reg = new Regex("^[A-Za-z0-9_]+$"); //do not localize
                if (!reg.IsMatch(ModDLCFolderName))
                {
                    return RuleResult.Invalid(M3L.GetString(M3L.string_dLCFolderNameCanOnlyConsistOf));
                }

                // 02/02/2022 - Discovered that LE3 rejects DLC foldernames containing case sensitive 'MP'
                if (Game == MEGame.LE3 && ModDLCFolderName.Contains(@"MP"))
                {
                    return RuleResult.Invalid(M3L.GetString(M3L.string_le3dlcFolderNamesMP));
                }

                return RuleResult.Valid();
            });
            Validator.AddRule(nameof(ModURL), () =>
            {
                if (string.IsNullOrWhiteSpace(ModURL)) return RuleResult.Valid(); //Empty URL is OK. Discouraged, but OK
                //Check is http or https
                Uri uriResult;
                bool result = Uri.TryCreate(ModURL, UriKind.Absolute, out uriResult)
                              && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
                if (result)
                {
                    return RuleResult.Valid();
                }
                return RuleResult.Invalid(M3L.GetString(M3L.string_uRLMustBeOfProtocol));
            });
            Validator.AddRule(nameof(ModDLCModuleNumber), () =>
            {
                if (!Game.IsGame2()) return RuleResult.Valid();
                if (ModDLCModuleNumber <= 0 || ModDLCModuleNumber >= ushort.MaxValue)
                {
                    return RuleResult.Invalid(M3L.GetString(M3L.string_valueMustBeBetween0And, ushort.MaxValue.ToString()));
                }
                return RuleResult.Valid();
            });
            Validator.AddRequiredRule(() => ModInternalName, M3L.GetString(M3L.string_internalModNameCannotBeEmpty));
            #endregion        
        }

        public ICommand GenerateStarterKitCommand { get; set; }
        public ICommand Select2DAsCommand { get; set; }
        private void LoadCommands()
        {
            GenerateStarterKitCommand = new GenericCommand(PrecheckStarterKitValues, ValidateInput);
            Select2DAsCommand = new GenericCommand(Select2DAs, CanSelect2DAs);
        }

        private void Select2DAs()
        {
            var selector = new Bio2DAGeneratorSelector(Game);
            selector.SetSelectedOptions(Selected2DAs.ToList());
            selector.ShowDialog();
            Selected2DAs.ReplaceAll(selector.GetSelected2DAs());
        }

        private bool CanSelect2DAs()
        {
            return true;
        }

        private void PrecheckStarterKitValues()
        {
            if (Game.IsGame2())
            {
                //Check Engine Number.
                var sameModuleNumberItems = TPMIService.GetThirdPartyModInfosByModuleNumber(ModDLCModuleNumber, Game);
                if (sameModuleNumberItems.Count > 0)
                {
                    string conflicts = "";
                    sameModuleNumberItems.ForEach(x => conflicts += Environment.NewLine + @" - " + x.modname); //do not localize
                    var result = M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_dialogDLCModuleNumberConflicts, conflicts), M3L.GetString(M3L.string_conflictingDLCModuleNumbers), MessageBoxButton.YesNo, MessageBoxImage.Error, MessageBoxResult.No);
                    if (result == MessageBoxResult.No) return;
                }
            }

            var sameMountPriorityItems = TPMIService.GetThirdPartyModInfosByMountPriority(Game, ModMountPriority);
            if (sameMountPriorityItems.Count > 0)
            {
                string conflicts = "";
                sameMountPriorityItems.ForEach(x => conflicts += Environment.NewLine + @" - " + x.modname); //do not localize
                var result = M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_dialogMountPrioirtyConflicts, conflicts), M3L.GetString(M3L.string_conflictingDLCMountPriorityNumbers), MessageBoxButton.YesNo, MessageBoxImage.Error, MessageBoxResult.No);
                if (result == MessageBoxResult.No) return;
            }


            MountFlag mf = new MountFlag(0, Game.IsGame2());
            var mountFlags = MountSelector.GetSelectedItems().OfType<MountFlag>();
            foreach (var f in mountFlags)
            {
                mf.SetFlagBit(f.FlagValue);
            }

            if ((Game.IsGame2() && ((EME2MountFileFlag)mf.FlagValue).HasFlag(EME2MountFileFlag.SaveFileDependency))
            || (Game.IsGame3() && ((EME3MountFileFlag)mf.FlagValue).HasFlag(EME3MountFileFlag.SaveFileDependency)))
            {
                var result = M3L.ShowDialog(this, M3L.GetString(M3L.string_dialog_discourageUseOfSPSaveRequired), M3L.GetString(M3L.string_undesirableMountFlag), MessageBoxButton.YesNo, MessageBoxImage.Error, MessageBoxResult.No);
                if (result == MessageBoxResult.No) return;
            }

            var outputDirectory = Path.Combine(M3LoadedMods.GetModDirectoryForGame(Game), MUtilities.SanitizePath(ModName));
            if (Directory.Exists(outputDirectory))
            {
                var result = M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_dialogWillDeleteExistingMod, outputDirectory), M3L.GetString(M3L.string_modAlreadyExists), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                if (result == MessageBoxResult.No) return;
                try
                {
                    if (!MUtilities.DeleteFilesAndFoldersRecursively(outputDirectory))
                    {
                        M3Log.Error(@"Could not delete existing output directory.");
                        M3L.ShowDialog(this, M3L.GetString(M3L.string_dialogErrorDeletingExistingMod), M3L.GetString(M3L.string_errorDeletingExistingMod), MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                }
                catch (Exception e)
                {
                    //I don't think this can be triggered but will leave as failsafe anyways.
                    M3Log.Error(@"Error while deleting existing output directory: " + App.FlattenException(e));
                    M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_errorOccuredWhileDeletingExistingModDirectory, e.Message), M3L.GetString(M3L.string_errorDeletingExistingMod), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            RunStarterKitGenerator(mf);
        }

        private void RunStarterKitGenerator(MountFlag mf)
        {

            var sko = new StarterKitOptions
            {
                ModName = ModName,
                ModDescription = ModDescription,
                ModDeveloper = ModDeveloper,
                ModDLCFolderNameSuffix = ModDLCFolderName,
                ModGame = Game,
                ModInternalName = ModInternalName,
                ModInternalTLKID = ModInternalTLKID,
                ModMountFlag = mf,
                ModMountPriority = ModMountPriority,
                ModURL = ModURL,
                ModModuleNumber = ModDLCModuleNumber,

                // FEATURES MAP
                AddStartupFile = AddStartupFile,
                AddPlotManagerData = AddPlotManagerData,
                AddModSettingsMenu = AddModSettingsMenuData,
                Blank2DAsToGenerate = Selected2DAs.ToList(),
                AddAshleySQM = AddSquadmateMerge3Ashley,
                AddGarrusSQM = AddSquadmateMerge3Garrus,
                AddJamesSQM = AddSquadmateMerge3James,
                AddEDISQM = AddSquadmateMerge3EDI,
                AddJavikSQM = AddSquadmateMerge3Javik,
                AddKaidanSQM = AddSquadmateMerge3Kaidan,
                AddLiaraSQM = AddSquadmateMerge3Liara,
                AddTaliSQM = AddSquadmateMerge3Tali,
            };

            M3Log.Information(@"Generating a starter kit mod with the following options:");
            M3Log.Information(sko.ToString());

            IsBusy = true;
            BusyText = LC.GetString(LC.string_generatingMod);
            Task.Run(() =>
            {
                FinishedCallback(CreateStarterKitMod(sko, s => { BusyText = s; }));
            }).ContinueWithOnUIThread(x =>
            {
                if (x.Exception != null)
                {
                    M3Log.Exception(x.Exception, @"Error generating mod:");
                    M3L.ShowDialog(this, x.Exception.Message, M3L.GetString(M3L.string_errorGeneratingMod), MessageBoxButton.OK, MessageBoxImage.Error);
                    IsBusy = false;
                }
            });
        }

        private void FinishedCallback(Mod obj)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {

                IsBusy = false;
                //if (Owner is MainWindow w)
                //{
                M3LoadedMods.Instance.LoadMods(obj, gamesToLoad: new[] { Game });
                //}

                Close();
                if (!Settings.DeveloperMode)
                {
                    var turnOnDevMode = M3L.ShowDialog(Owner, M3L.GetString(M3L.string_dialog_devModeAdvert),
                        M3L.GetString(M3L.string_enableDeveloperFeaturesQuestion), MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    if (turnOnDevMode == MessageBoxResult.Yes)
                    {
                        Settings.DeveloperMode = true;
                        TelemetryInterposer.TrackEvent(@"Turned on developer mode after starter kit");
                        //Settings.Save();
                    }
                }

                M3L.ShowDialog(Owner, M3L.GetString(M3L.string_dialog_warnMustBeDeployedForFullFeatures),
                    M3L.GetString(M3L.string_deploymentInfo), MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        private bool ValidateInput()
        {
            return Validator.ValidateAll().IsValid;
        }

        public MEGame Game { get; private set; }


        private void GameIcon_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fw && fw.DataContext is MEGameSelector gamesel)
            {
                Games.ForEach(x => x.IsSelected = false);
                gamesel.IsSelected = true;
                SetGame(gamesel.Game);
            }
        }

        private void SetGame(MEGame game)
        {
            try
            {
                Game = game;
                if (Game.IsGame1())
                {
                    DisplayedMountFlags.ClearEx();
                    ListedEntries.ReplaceAll(TPMIService.GetThirdPartyModInfos(Game).Values.Where(x => !x.IsOutdated));
                }

                if (Game.IsGame2())
                {
                    DisplayedMountFlags.ReplaceAll(ME2MountFlags);
                    ListedEntries.ReplaceAll(TPMIService.GetThirdPartyModInfos(Game).Values.Where(x => !x.IsOutdated));
                }

                if (Game.IsGame3())
                {
                    DisplayedMountFlags.ReplaceAll(ME3MountFlags);
                    MountSelector.SetSelectedItems(new MountFlag[] { new MountFlag(EME3MountFileFlag.LoadsInSingleplayer) });
                    ListedEntries.ReplaceAll(TPMIService.GetThirdPartyModInfos(Game).Values.Where(x => !x.IsOutdated));
                }

                ListedEntries.Insert(0, PreviewTPMI);
                ListedEntries.SortDescending(x => x.MountPriorityInt);
                FilterTMPIEntries(null);
                CustomDLCMountsListBox.ScrollIntoView(PreviewTPMI);
            }
            catch (Exception e)
            {
                // This happens, somehow, according to telemetry
                M3Log.Error($@"Error setting game in StarterKit: {e.Message}");
            }
        }

        /// <summary>
        /// Generates a DLC mod with starter kit. Can return null if generate moddesc is not specified
        /// </summary>
        /// <param name="skOption"></param>
        /// <param name="UITextCallback"></param>
        /// <returns></returns>
        public static Mod CreateStarterKitMod(StarterKitOptions skOption, Action<string> UITextCallback)
        {
            var modPath = DLCModGenerator.CreateStarterKitMod(skOption.OutputFolderOverride ?? Path.Combine(M3LoadedMods.GetModDirectoryForGame(skOption.ModGame), MUtilities.SanitizePath(skOption.ModName))
                , skOption, UITextCallback, out var moddescAddinDelegates);
            if (skOption.GenerateModdesc)
            {
                var dlcFolderName = $@"DLC_MOD_{skOption.ModDLCFolderNameSuffix}";
                var ini = new DuplicatingIni();
                ini[Mod.MODDESC_HEADERKEY_MODMANAGER][Mod.MODDESC_DESCRIPTOR_MODMANAGER_CMMVER].Value = App.HighestSupportedModDesc.ToString(CultureInfo.InvariantCulture); //prevent commas
                ini[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_GAME].Value = skOption.ModGame.ToString();
                ini[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_NAME].Value = skOption.ModName;
                ini[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_DEVELOPER].Value = skOption.ModDeveloper;
                ini[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_DESCRIPTION].Value = M3Utilities.ConvertNewlineToBr(skOption.ModDescription);
                ini[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_VERSION].Value = 1.0.ToString(CultureInfo.InvariantCulture);
                ini[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_SITE].Value = skOption.ModURL;

                ini[Mod.MODDESC_HEADERKEY_CUSTOMDLC][Mod.MODDESC_DESCRIPTOR_CUSTOMDLC_SOURCEDIRS].Value = dlcFolderName;
                ini[Mod.MODDESC_HEADERKEY_CUSTOMDLC][Mod.MODDESC_DESCRIPTOR_CUSTOMDLC_DESTDIRS].Value = dlcFolderName;

                foreach (var v in moddescAddinDelegates)
                {
                    v.Invoke(ini);
                }

                var modDescPath = Path.Combine(modPath, @"moddesc.ini");
                ini.WriteToFile(modDescPath, new UTF8Encoding(false));
                Mod m = new Mod(modDescPath, skOption.ModGame);
                return m;
            }

            return null;
        }
        
        private void MountPriority_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (int.TryParse(ModMountPriority_TextBox.Text, out var val) && val > MinMountForGame && val < MaxMountForGame)
            {
                PreviewTPMI.mountpriority = val.ToString();
                ListedEntries.SortDescending(x => x.MountPriorityInt);
                FilterTMPIEntries(null);
                CustomDLCMountsListBox?.ScrollIntoView(PreviewTPMI);
            }
            Validator.Validate(nameof(ModMountPriority));
        }

        private void FieldText_Changed(object sender, TextChangedEventArgs e)
        {
            //Debug.WriteLine("Textchanged");
            var textField = (TextBox)sender;
            if (textField == ModModuleNumber_TextBox)
                Validator.Validate(nameof(ModDLCModuleNumber));
            else if (textField == ModDescription_TextBox)
                Validator.Validate(nameof(ModDescription));
            else if (textField == ModInternalName_TextBox)
                Validator.Validate(nameof(ModInternalName));
            else if (textField == ModDeveloper_TextBox)
                Validator.Validate(nameof(ModDeveloper));
            else if (textField == ModName_TextBox)
                Validator.Validate(nameof(ModName));
            else if (textField == ModInternalTLK_TextBox)
                Validator.Validate(nameof(ModInternalTLKID));
            //else if (textField == ModSuffix_TextBox)
            //    Validator.Validate(nameof(ModDLCFolderName));
        }

        private void StarterKit_ContentRendered(object sender, EventArgs e)
        {
            SetupValidation();
            if (PendingGame != null)
            {
                if (Games.Any())
                {
                    Games.FirstOrDefault(x => x.Game == PendingGame).IsSelected = true;
                }
                SetGame(PendingGame.Value);
                PendingGame = null;
            }
        }

        private void OnGameChanged()
        {
            // CLEAR ALL THE FLAGS
            AddStartupFile = false;
            AddModSettingsMenuData = false;
            AddPlotManagerData = false;
            Selected2DAs.Clear();
            AddSquadmateMerge3Ashley = false;
            AddSquadmateMerge3EDI = false;
            AddSquadmateMerge3Garrus = false;
            AddSquadmateMerge3James = false;
            AddSquadmateMerge3Javik = false;
            AddSquadmateMerge3Kaidan = false;
            AddSquadmateMerge3Liara = false;
        }

        private void DebugFill_Click(object sender, RoutedEventArgs e)
        {
#if DEBUG
            ModName = @"Debug Mod Name";
            ModDeveloper = @"DEBUG DEV";
            ModMountPriority = 4567;
            ModInternalName = @"Debug Internal Name";
            ModInternalTLKID = 12345;
            ModDLCFolderName = @"DebugMod";
            ModDescription = @"Debug Description for mod";
            ModDLCModuleNumber = 12345;
#endif
        }
    }
}
