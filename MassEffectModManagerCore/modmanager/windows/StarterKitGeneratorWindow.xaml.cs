using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IniParser;
using IniParser.Model;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.memoryanalyzer;
using MassEffectModManagerCore.ui;
using ME3ExplorerCore.GameFilesystem;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.TLK.ME1;
using Microsoft.AppCenter.Analytics;
using MvvmValidation;
using Serilog;
using static MassEffectModManagerCore.modmanager.me3tweaks.ThirdPartyServices;

namespace MassEffectModManagerCore.modmanager.windows
{
    /// <summary>
    /// Interaction logic for StarterKitGeneratorWindow.xaml
    /// </summary>
    public partial class StarterKitGeneratorWindow : ValidatableWindowBase
    {
        public static (string filecode, string langcode)[] me3languages = { (@"INT", @"en-us"), (@"ESN", @"es-es"), (@"DEU", @"de-de"), (@"RUS", @"ru-ru"), (@"FRA", @"fr-fr"), (@"ITA", @"it-it"), (@"POL", @"pl-pl"), (@"JPN", @"jp-jp") };
        public static (string filecode, string langcode)[] me2languages = { (@"INT", @"en-us"), (@"ESN", @"es-es"), (@"DEU", @"de-de"), (@"RUS", @"ru-ru"), (@"FRA", @"fr-fr"), (@"ITA", @"it-it"), (@"POL", @"pl-pl"), (@"HUN", @"hu-hu"), (@"CZE", @"cs-cz") };

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
                }
                return 500;
            }
        }

        public ThirdPartyModInfo PreviewTPMI { get; } = new ThirdPartyModInfo();
        public static GridLength VisibleRowHeight { get; } = new GridLength(25);
        public string BusyText { get; set; }
        public bool IsBusy { get; set; }
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
                    CustomDLCMountsForGame.SortDescending(x => x.MountPriorityInt);
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

        public UIMountFlag ModMountFlag { get; set; }
        public ObservableCollectionExtended<UIMountFlag> DisplayedMountFlags { get; } = new ObservableCollectionExtended<UIMountFlag>();
        private readonly List<UIMountFlag> ME1MountFlags = new List<UIMountFlag>();
        private readonly List<UIMountFlag> ME2MountFlags = new List<UIMountFlag>();
        private readonly List<UIMountFlag> ME3MountFlags = new List<UIMountFlag>();
        public ObservableCollectionExtended<ThirdPartyModInfo> CustomDLCMountsForGame { get; } = new ObservableCollectionExtended<ThirdPartyModInfo>();

        public StarterKitGeneratorWindow(MEGame Game) : base()
        {
            MemoryAnalyzer.AddTrackedMemoryItem(@"Starter Kit Window", new WeakReference(this));
            Log.Information(@"Opening Starter Kit window");
            DataContext = this;
            ME1MountFlags.Add(new UIMountFlag(EMountFileFlag.ME1_NoSaveFileDependency, M3L.GetString(M3L.string_noSaveFileDependencyOnDLC)));
            ME1MountFlags.Add(new UIMountFlag(EMountFileFlag.ME1_SaveFileDependency, M3L.GetString(M3L.string_saveFileDependencyOnDLC)));

            ME2MountFlags.Add(new UIMountFlag(EMountFileFlag.ME2_NoSaveFileDependency, @"0x00 | No save file dependency on DLC"));
            ME2MountFlags.Add(new UIMountFlag(EMountFileFlag.ME2_SaveFileDependency, @"0x02 | Save file dependency on DLC"));

            ME3MountFlags.Add(new UIMountFlag(EMountFileFlag.ME3_SPOnly_NoSaveFileDependency, @"0x08 - SP only | No file dependency on DLC"));
            ME3MountFlags.Add(new UIMountFlag(EMountFileFlag.ME3_SPOnly_SaveFileDependency, @"0x09 - SP only | Save file dependency on DLC"));
            ME3MountFlags.Add(new UIMountFlag(EMountFileFlag.ME3_SPMP_SaveFileDependency, @"0x1C - SP & MP | No save file dependency on DLC"));
            ME3MountFlags.Add(new UIMountFlag(EMountFileFlag.ME3_MPOnly_Patch, @"0x0C - MP only | Loads in MP (PATCH)"));
            ME3MountFlags.Add(new UIMountFlag(EMountFileFlag.ME3_MPOnly_1, @"0x14 - MP only | Loads in MP"));
            ME3MountFlags.Add(new UIMountFlag(EMountFileFlag.ME3_MPOnly_2, @"0x34 - MP only | Loads in MP"));
            PreviewTPMI.IsPreview = true;
            DisplayedMountFlags.Add(new UIMountFlag(EMountFileFlag.ME1_NoSaveFileDependency, M3L.GetString(M3L.string_loadingPlaceholder)));
            SetupValidation();

            LoadCommands();

            //#if DEBUG
            //            ModName = "Debug Test Mod";
            //            ModDeveloper = "Developer";
            //            ModInternalName = "StarterKit Mod";
            //            //ModDLCFolderName = "DLC_MOD_StarterKitMod";
            //            ModMountPriority = 3678;
            //            ModDLCModuleNumber = 150;
            //            ModURL = "https://example.com";
            //            ModInternalTLKID = 277346578;
            //            ModDescription = "This is a starter kit debug testing mod.\n\nHerp a derp flerp.";
            //#endif
            InitializeComponent();
            SetGame(Game);
        }

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
                var sanitized = Utilities.SanitizePath(ModName, true);
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
                if (ModMountPriority <= 0 || ModMountPriority >= GetGameSpecificMountLimit())
                {
                    return RuleResult.Invalid(M3L.GetString(M3L.string_interp_valueMustBeBetween0AndX, GetGameSpecificMountLimit().ToString()));
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
                Regex reg = new Regex("[A-Za-z0-9_]+$"); //do not localize
                if (!reg.IsMatch(ModDLCFolderName))
                {
                    return RuleResult.Invalid(M3L.GetString(M3L.string_dLCFolderNameCanOnlyConsistOf));

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
                if (Game != MEGame.ME2) return RuleResult.Valid();
                if (ModDLCModuleNumber <= 0 || ModDLCModuleNumber >= ushort.MaxValue)
                {
                    return RuleResult.Invalid(M3L.GetString(M3L.string_interp_valueMustBeBetween0AndX, ushort.MaxValue.ToString()));
                }
                return RuleResult.Valid();
            });
            Validator.AddRequiredRule(() => ModInternalName, M3L.GetString(M3L.string_internalModNameCannotBeEmpty));

            #endregion        
        }

        public ICommand GenerateStarterKitCommand { get; set; }
        private void LoadCommands()
        {
            GenerateStarterKitCommand = new GenericCommand(PrecheckStarterKitValues, ValidateInput);
        }

        private void PrecheckStarterKitValues()
        {
            if (Game == MEGame.ME2)
            {
                //Check Engine Number.
                var sameModuleNumberItems = ThirdPartyServices.GetThirdPartyModInfosByModuleNumber(ModDLCModuleNumber);
                if (sameModuleNumberItems.Count > 0)
                {
                    string conflicts = "";
                    sameModuleNumberItems.ForEach(x => conflicts += Environment.NewLine + @" - " + x.modname); //do not localize
                    var result = M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_dialogDLCModuleNumberConflicts, conflicts), M3L.GetString(M3L.string_conflictingDLCModuleNumbers), MessageBoxButton.YesNo, MessageBoxImage.Error, MessageBoxResult.No);
                    if (result == MessageBoxResult.No) return;
                }
            }

            var sameMountPriorityItems = ThirdPartyServices.GetThirdPartyModInfosByMountPriority(Game, ModMountPriority);
            if (sameMountPriorityItems.Count > 0)
            {
                string conflicts = "";
                sameMountPriorityItems.ForEach(x => conflicts += Environment.NewLine + @" - " + x.modname); //do not localize
                var result = M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_dialogMountPrioirtyConflicts, conflicts), M3L.GetString(M3L.string_conflictingDLCMountPriorityNumbers), MessageBoxButton.YesNo, MessageBoxImage.Error, MessageBoxResult.No);
                if (result == MessageBoxResult.No) return;
            }

            if (ModMountFlag.Flag == EMountFileFlag.ME1_SaveFileDependency || ModMountFlag.Flag == EMountFileFlag.ME2_SaveFileDependency || ModMountFlag.Flag == EMountFileFlag.ME3_SPMP_SaveFileDependency || ModMountFlag.Flag == EMountFileFlag.ME3_SPMP_SaveFileDependency)
            {
                var result = M3L.ShowDialog(this, M3L.GetString(M3L.string_dialog_discourageUseOfSPSaveRequired), M3L.GetString(M3L.string_undesirableMountFlag), MessageBoxButton.YesNo, MessageBoxImage.Error, MessageBoxResult.No);
                if (result == MessageBoxResult.No) return;
            }

            var outputDirectory = Path.Combine(Utilities.GetModDirectoryForGame(Game), Utilities.SanitizePath(ModName));
            if (Directory.Exists(outputDirectory))
            {
                var result = M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_dialogWillDeleteExistingMod, outputDirectory), M3L.GetString(M3L.string_modAlreadyExists), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                if (result == MessageBoxResult.No) return;
                try
                {
                    if (!Utilities.DeleteFilesAndFoldersRecursively(outputDirectory))
                    {
                        Log.Error(@"Could not delete existing output directory.");
                        M3L.ShowDialog(this, M3L.GetString(M3L.string_dialogErrorDeletingExistingMod), M3L.GetString(M3L.string_errorDeletingExistingMod), MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                }
                catch (Exception e)
                {
                    //I don't think this can be triggered but will leave as failsafe anyways.
                    Log.Error(@"Error while deleting existing output directory: " + App.FlattenException(e));
                    M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_errorOccuredWhileDeletingExistingModDirectory, e.Message), M3L.GetString(M3L.string_errorDeletingExistingMod), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            RunStarterKitGenerator();
        }

        private void RunStarterKitGenerator()
        {

            StarterKitOptions sko = new StarterKitOptions
            {
                ModName = ModName,
                ModDescription = ModDescription,
                ModDeveloper = ModDeveloper,
                ModDLCFolderName = ModDLCFolderName,
                ModGame = Game,
                ModInternalName = ModInternalName,
                ModInternalTLKID = ModInternalTLKID,
                ModMountFlag = ModMountFlag.Flag,
                ModMountPriority = ModMountPriority,
                ModURL = ModURL,
                ModModuleNumber = ModDLCModuleNumber
            };

            Log.Information(@"Generating a starter kit mod with the following options:");
            Log.Information(sko.ToString());

            IsBusy = true;
            BusyText = M3L.GetString(M3L.string_generatingMod);
            CreateStarterKitMod(sko, s => { BusyText = s; }, FinishedCallback);
        }

        private void FinishedCallback(objects.mod.Mod obj)
        {
            IsBusy = false;
            if (Owner is MainWindow w)
            {
                w.LoadMods(obj);
            }
            Close();
            if (!Settings.DeveloperMode)
            {
                var turnOnDevMode = M3L.ShowDialog(Owner, M3L.GetString(M3L.string_dialog_devModeAdvert), M3L.GetString(M3L.string_enableDeveloperFeaturesQuestion), MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (turnOnDevMode == MessageBoxResult.Yes)
                {
                    Settings.DeveloperMode = true;
                    Analytics.TrackEvent(@"Turned on developer mode after starter kit");
                    //Settings.Save();
                }
            }

        }

        private bool ValidateInput()
        {
            return Validator.ValidateAll().IsValid;
        }

        public MEGame Game { get; private set; }


        private void RadioButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender == ME1_RadioButton)
            {
                SetGame(MEGame.ME1);
            }

            if (sender == ME2_RadioButton)
            {
                SetGame(MEGame.ME2);
            }

            if (sender == ME3_RadioButton)
            {
                SetGame(MEGame.ME3);
            }
        }

        private void SetGame(MEGame game)
        {
            try
            {
                Game = game;
                if (Game == MEGame.ME1)
                {
                    DisplayedMountFlags.ReplaceAll(ME1MountFlags);
                    CustomDLCMountsForGame.ReplaceAll(App.ThirdPartyIdentificationService[MEGame.ME1.ToString()].Values
                        .Where(x => !x.IsOutdated));
                    ME1_RadioButton.IsChecked = true;
                }

                if (Game == MEGame.ME2)
                {
                    DisplayedMountFlags.ReplaceAll(ME2MountFlags);
                    CustomDLCMountsForGame.ReplaceAll(App.ThirdPartyIdentificationService[MEGame.ME2.ToString()].Values
                        .Where(x => !x.IsOutdated));
                    ME2_RadioButton.IsChecked = true;
                }

                if (Game == MEGame.ME3)
                {
                    DisplayedMountFlags.ReplaceAll(ME3MountFlags);
                    CustomDLCMountsForGame.ReplaceAll(App.ThirdPartyIdentificationService[MEGame.ME3.ToString()].Values
                        .Where(x => !x.IsOutdated));
                    ME3_RadioButton.IsChecked = true;
                }

                CustomDLCMountsForGame.Insert(0, PreviewTPMI);
                DisplayedMountFlags[0].Selected = true;
                CustomDLCMountsForGame.SortDescending(x => x.MountPriorityInt);
                CustomDLCMountsListBox.ScrollIntoView(PreviewTPMI);
            }
            catch (Exception e)
            {
                // This happens, somehow, according to telemetry
                Log.Error($@"Error setting game in StarterKit: {e.Message}");
            }
        }

        public class UIMountFlag
        {
            public UIMountFlag(EMountFileFlag flag, string displayString)
            {
                this.Flag = flag;
                this.DisplayString = displayString;
            }

            public EMountFileFlag Flag { get; }
            public string DisplayString { get; }
            public bool Selected { get; set; }
        }

        public static void CreateStarterKitMod(StarterKitOptions options, Action<string> UITextCallback, Action<objects.mod.Mod> finishedCallback)
        {
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"StarterKitThread");
            nbw.DoWork += (sender, args) =>
            {
                var skOption = args.Argument as StarterKitOptions;

                var dlcFolderName = $@"DLC_MOD_{skOption.ModDLCFolderName}";
                var modsDirectory = Utilities.GetModDirectoryForGame(skOption.ModGame);
                var modPath = Path.Combine(modsDirectory, Utilities.SanitizePath(skOption.ModName));
                if (Directory.Exists(modPath))
                {
                    Utilities.DeleteFilesAndFoldersRecursively(modPath);
                }

                Directory.CreateDirectory(modPath);

                //Creating DLC directories
                Log.Information(@"Creating starter kit folders");
                var contentDirectory = Directory.CreateDirectory(Path.Combine(modPath, dlcFolderName)).FullName;
                var cookedDir = Directory.CreateDirectory(Path.Combine(contentDirectory, skOption.ModGame == MEGame.ME3 ? @"CookedPCConsole" : @"CookedPC")).FullName;
                if (skOption.ModGame == MEGame.ME1)
                {
                    //AutoLoad.ini
                    IniData autoload = new IniData();
                    autoload[@"Packages"][@"GlobalTalkTable1"] = $@"{dlcFolderName}_GlobalTlk.GlobalTlk_tlk";

                    autoload[@"GUI"][@"NameStrRef"] = skOption.ModInternalTLKID.ToString();

                    autoload[@"ME1DLCMOUNT"][@"ModName"] = skOption.ModName;
                    autoload[@"ME1DLCMOUNT"][@"ModMount"] = skOption.ModMountPriority.ToString();
                    Log.Information(@"Saving autoload.ini for ME1 mod");
                    new FileIniDataParser().WriteFile(Path.Combine(contentDirectory, @"AutoLoad.ini"), autoload);

                    //TLK
                    var dialogdir = Directory.CreateDirectory(Path.Combine(cookedDir, @"Packages", @"Dialog")).FullName;
                    var tlkGlobalFile = Path.Combine(dialogdir, $@"{dlcFolderName}_GlobalTlk.upk");
                    Utilities.ExtractInternalFile(@"MassEffectModManagerCore.modmanager.starterkit.BlankTlkFile.upk", tlkGlobalFile, true);
                    var tlkFile = MEPackageHandler.OpenMEPackage(tlkGlobalFile);
                    var tlk1 = new ME1TalkFile(tlkFile.GetUExport(1));
                    var tlk2 = new ME1TalkFile(tlkFile.GetUExport(2));

                    tlk1.StringRefs[0].StringID = skOption.ModInternalTLKID;
                    tlk2.StringRefs[0].StringID = skOption.ModInternalTLKID;

                    tlk1.StringRefs[0].Data = skOption.ModInternalName;
                    tlk2.StringRefs[0].Data = skOption.ModInternalName;

                    var huff = new HuffmanCompression();
                    huff.LoadInputData(tlk1.StringRefs.ToList());
                    huff.serializeTalkfileToExport(tlkFile.GetUExport(1));

                    huff = new HuffmanCompression();
                    huff.LoadInputData(tlk2.StringRefs.ToList());
                    huff.serializeTalkfileToExport(tlkFile.GetUExport(2));
                    Log.Information(@"Saving ME1 TLK package");
                    tlkFile.Save();
                }
                else
                {
                    //ME2, ME3
                    MountFile mf = new MountFile();
                    mf.IsME2 = skOption.ModGame == MEGame.ME2;
                    mf.MountFlag = skOption.ModMountFlag;
                    mf.ME2Only_DLCFolderName = dlcFolderName;
                    mf.ME2Only_DLCHumanName = skOption.ModName;
                    mf.MountPriority = (ushort)skOption.ModMountPriority;
                    mf.TLKID = skOption.ModInternalTLKID;
                    Log.Information(@"Saving mount.dlc file for mod");
                    mf.WriteMountFile(Path.Combine(cookedDir, @"Mount.dlc"));

                    if (skOption.ModGame == MEGame.ME3)
                    {
                        //Extract Default.Sfar
                        Utilities.ExtractInternalFile(@"MassEffectModManagerCore.modmanager.starterkit.Default.sfar", Path.Combine(cookedDir, @"Default.sfar"), true);

                        //Generate Coalesced.bin for mod
                        var memory = Utilities.ExtractInternalFileToStream(@"MassEffectModManagerCore.modmanager.starterkit.Default_DLC_MOD_StarterKit.bin");
                        var files = MassEffect3.Coalesce.Converter.DecompileToMemory(memory);
                        //Modify coal files for this mod.
                        files[@"BioEngine.xml"] = files[@"BioEngine.xml"].Replace(@"StarterKit", skOption.ModDLCFolderName); //update bioengine

                        var newMemory = MassEffect3.Coalesce.Converter.CompileFromMemory(files);
                        var outpath = Path.Combine(cookedDir, $@"Default_{dlcFolderName}.bin");
                        Log.Information(@"Saving new starterkit coalesced file");
                        File.WriteAllBytes(outpath, newMemory.ToArray());
                    }
                    else
                    {
                        //ME2
                        IniData bioEngineIni = new IniData();
                        bioEngineIni.Configuration.AssigmentSpacer = ""; //no spacer.
                        bioEngineIni[@"Core.System"][@"!CookPaths"] = @"CLEAR";
                        bioEngineIni[@"Core.System"][@"+SeekFreePCPaths"] = $@"..\BIOGame\DLC\{dlcFolderName}\CookedPC";

                        //bioEngineIni["Engine.PackagesToAlwaysCook"]["!Package"] = "CLEAR";
                        //bioEngineIni["Engine.PackagesToAlwaysCook"]["!SeekFreePackage"] = "CLEAR";

                        //Todo: Find way to tell user what this is for and how to pick one. Used to determine TLK filename
                        bioEngineIni[@"Engine.DLCModules"][dlcFolderName] = skOption.ModModuleNumber.ToString();

                        bioEngineIni[@"DLCInfo"][@"Version"] = 0.ToString(); //unknown
                        bioEngineIni[@"DLCInfo"][@"Flags"] = ((int)skOption.ModMountFlag).ToString(); //unknown
                        bioEngineIni[@"DLCInfo"][@"Name"] = skOption.ModInternalTLKID.ToString();
                        Log.Information(@"Saving BioEngine file");
                        new FileIniDataParser().WriteFile(Path.Combine(cookedDir, @"BIOEngine.ini"), bioEngineIni, new UTF8Encoding(false));
                    }

                    var tlkFilePrefix = skOption.ModGame == MEGame.ME3 ? dlcFolderName : $@"DLC_{skOption.ModModuleNumber}";
                    var languages = skOption.ModGame == MEGame.ME2 ? me2languages : me3languages;
                    foreach (var lang in languages)
                    {
                        List<ME1TalkFile.TLKStringRef> strs = new List<ME1TalkFile.TLKStringRef>();
                        strs.Add(new ME1TalkFile.TLKStringRef(skOption.ModInternalTLKID, 0, skOption.ModInternalName));
                        if (skOption.ModGame == MEGame.ME2)
                        {
                            strs.Add(new ME1TalkFile.TLKStringRef(skOption.ModInternalTLKID + 1, 1, @"DLC_" + skOption.ModModuleNumber));
                        }
                        else
                        {
                            strs.Add(new ME1TalkFile.TLKStringRef(skOption.ModInternalTLKID + 1, 1, @"DLC_MOD_" + skOption.ModDLCFolderName));
                        }

                        strs.Add(new ME1TalkFile.TLKStringRef(skOption.ModInternalTLKID + 2, 2, lang.langcode));
                        strs.Add(new ME1TalkFile.TLKStringRef(skOption.ModInternalTLKID + 3, 3, @"Male"));
                        strs.Add(new ME1TalkFile.TLKStringRef(skOption.ModInternalTLKID + 3, 4, @"Female"));

                        foreach (var str in strs)
                        {
                            str.Data += '\0';
                        }

                        var tlk = Path.Combine(cookedDir, $@"{tlkFilePrefix}_{lang.filecode}.tlk");
                        Log.Information(@"Saving TLK file: " + tlk);
                        ME3ExplorerCore.TLK.ME2ME3.HuffmanCompression.SaveToTlkFile(tlk, strs);
                    }
                }

                IniData ini = new IniData();
                ini[@"ModManager"][@"cmmver"] = App.HighestSupportedModDesc.ToString(CultureInfo.InvariantCulture); //prevent commas
                ini[@"ModInfo"][@"game"] = skOption.ModGame.ToString();
                ini[@"ModInfo"][@"modname"] = skOption.ModName;
                ini[@"ModInfo"][@"moddev"] = skOption.ModDeveloper;
                ini[@"ModInfo"][@"moddesc"] = Utilities.ConvertNewlineToBr(skOption.ModDescription);
                ini[@"ModInfo"][@"modver"] = 1.0.ToString(CultureInfo.InvariantCulture);
                ini[@"ModInfo"][@"modsite"] = skOption.ModURL;

                ini[@"CUSTOMDLC"][@"sourcedirs"] = dlcFolderName;
                ini[@"CUSTOMDLC"][@"destdirs"] = dlcFolderName;


                var modDescPath = Path.Combine(modPath, @"moddesc.ini");
                new FileIniDataParser().WriteFile(modDescPath, ini, new UTF8Encoding(false));
                objects.mod.Mod m = new objects.mod.Mod(modDescPath, skOption.ModGame);
                args.Result = m;
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
                }
                finishedCallback(b.Result as objects.mod.Mod);
            };
            nbw.RunWorkerAsync(options);
        }

        public class StarterKitOptions
        {
            public string ModDescription;
            public string ModDeveloper;
            public string ModName;
            public string ModInternalName;
            public string ModDLCFolderName;
            public int ModMountPriority;
            public int ModInternalTLKID;
            public string ModURL;
            public EMountFileFlag ModMountFlag;
            public MEGame ModGame;

            public int ModModuleNumber;
            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(@"Game: " + ModGame);
                sb.AppendLine(@"ModName: " + ModName);
                sb.AppendLine(@"ModDescription: " + ModDescription);
                sb.AppendLine(@"ModDeveloper: " + ModDLCFolderName);
                sb.AppendLine(@"ModDLCFolderName: " + ModDLCFolderName);
                sb.AppendLine(@"ModInternalTLKID: " + ModInternalTLKID);
                sb.AppendLine(@"ModMountPriority: " + ModMountPriority);
                sb.AppendLine(@"ModModuleNumber: " + ModModuleNumber);
                sb.AppendLine(@"ModURL: " + ModURL);
                sb.AppendLine(@"Mount flag: " + ModMountFlag);
                return sb.ToString();
            }
        }

        private void MountPriority_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (int.TryParse(ModMountPriority_TextBox.Text, out var val) && val > 0 && val < GetGameSpecificMountLimit())
            {
                PreviewTPMI.mountpriority = val.ToString();
                CustomDLCMountsForGame.SortDescending(x => x.MountPriorityInt);
                CustomDLCMountsListBox?.ScrollIntoView(PreviewTPMI);
            }
            Validator.Validate(nameof(ModMountPriority));
        }

        private int GetGameSpecificMountLimit() => MaxMountForGame;

        private void FieldText_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
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
        }
    }
}
