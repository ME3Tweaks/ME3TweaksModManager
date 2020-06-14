using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ByteSizeLib;
using FontAwesome.WPF;
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.gamefileformats.unreal;
using MassEffectModManagerCore.modmanager.windows;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using SevenZip;
using Brushes = System.Windows.Media.Brushes;
using UserControl = System.Windows.Controls.UserControl;
using Microsoft.Win32;
using MassEffectModManagerCore.gamefileformats;
using MassEffectModManagerCore.modmanager.localizations;
using Serilog;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ArchiveDeployment.xaml
    /// </summary>
    public partial class ArchiveDeployment : MMBusyPanelBase
    {
        public Mod ModBeingDeployed { get; }
        public string Header { get; set; } = M3L.GetString(M3L.string_prepareModForDistribution);
        public bool MultithreadedCompression { get; set; } = true;
        public ArchiveDeployment(Mod mod)
        {
            Analytics.TrackEvent(@"Started deployment panel for mod", new Dictionary<string, string>()
            {
                { @"Mod name" , $@"{mod.ModName} {mod.ParsedModVersion}"}
            });
            DataContext = this;
            ModBeingDeployed = mod;
            string versionString = mod.ParsedModVersion != null ? mod.ParsedModVersion.ToString(Utilities.GetDisplayableVersionFieldCount(mod.ParsedModVersion)) : mod.ModVersionString;
            string versionFormat = mod.ModDescTargetVersion < 6 ? @"X.X" : @"X.X[.X[.X]]";
            string checklistItemText = mod.ParsedModVersion != null ? M3L.GetString(M3L.string_verifyModVersion) : M3L.GetString(M3L.string_recommendedVersionFormatNotFollowed, versionFormat);
            DeploymentChecklistItems.Add(new DeploymentChecklistItem() { ItemText = $@"{checklistItemText}: {versionString}", ValidationFunction = ManualValidation });
            DeploymentChecklistItems.Add(new DeploymentChecklistItem()
            {
                ItemText = $@"{M3L.GetString(M3L.string_verifyURLIsCorrect)} {mod.ModWebsite}",
                ValidationFunction = URLValidation,
                ErrorsMessage = M3L.GetString(M3L.string_validation_badUrl),
                ErrorsTitle = M3L.GetString(M3L.string_modUrlErrorsWereFound)
            });
            DeploymentChecklistItems.Add(new DeploymentChecklistItem()
            {
                ItemText = M3L.GetString(M3L.string_verifyModDescription),
                ValidationFunction = ManualValidation

            });
            if (mod.Game == Mod.MEGame.ME3)
            {
                DeploymentChecklistItems.Add(new DeploymentChecklistItem()
                {
                    ItemText = M3L.GetString(M3L.string_sfarFilesCheck),
                    ModToValidateAgainst = mod,
                    ErrorsMessage = M3L.GetString(M3L.string_invalidSfarSize),
                    ErrorsTitle = M3L.GetString(M3L.string_wrongSfarSizesFound),
                    ValidationFunction = CheckModSFARs
                });
            }
            var customDLCJob = mod.GetJob(ModJob.JobHeader.CUSTOMDLC);
            if (customDLCJob != null)
            {
                //Todo: Implement this for ME1, ME2
                if (mod.Game == Mod.MEGame.ME3)
                {
                    var customDLCFolders = customDLCJob.CustomDLCFolderMapping.Keys.ToList();
                    customDLCFolders.AddRange(customDLCJob.AlternateDLCs.Where(x => x.Operation == AlternateDLC.AltDLCOperation.OP_ADD_CUSTOMDLC).Select(x => x.AlternateDLCFolder));

                    if (customDLCFolders.Count > 0)
                    {
                        DeploymentChecklistItems.Add(new DeploymentChecklistItem()
                        {
                            ItemText = M3L.GetString(M3L.string_languageSupportCheck),
                            ModToValidateAgainst = mod,
                            ValidationFunction = CheckLocalizationsME3,
                            ErrorsMessage = M3L.GetString(M3L.string_languageSupportCheckDetectedFollowingIssues),
                            ErrorsTitle = M3L.GetString(M3L.string_languageSupportIssuesDetectedInMod)
                        });
                    }
                }
            }
            if (mod.Game >= Mod.MEGame.ME2)
            {
                DeploymentChecklistItems.Add(new DeploymentChecklistItem()
                {
                    ItemText = M3L.GetString(M3L.string_audioCheck),
                    ModToValidateAgainst = mod,
                    ValidationFunction = CheckModForAFCCompactability,
                    ErrorsMessage = M3L.GetString(M3L.string_audioCheckDetectedErrors),
                    ErrorsTitle = M3L.GetString(M3L.string_audioIssuesDetectedInMod)
                });
            }
            DeploymentChecklistItems.Add(new DeploymentChecklistItem()
            {
                ItemText = M3L.GetString(M3L.string_texturesCheck),
                ModToValidateAgainst = mod,
                ErrorsMessage = M3L.GetString(M3L.string_texturesCheckDetectedErrors),
                ErrorsTitle = M3L.GetString(M3L.string_textureErrorsInMod),
                ValidationFunction = CheckModForTFCCompactability
            });
            DeploymentChecklistItems.Add(new DeploymentChecklistItem()
            {
                ItemText = M3L.GetString(M3L.string_miscellaneousChecks),
                ModToValidateAgainst = mod,
                ErrorsMessage = M3L.GetString(M3L.string_atLeastOneMiscellaneousCheckFailed),
                ErrorsTitle = M3L.GetString(M3L.string_detectedMiscellaneousIssues),
                ValidationFunction = CheckModForMiscellaneousIssues
            });
            LoadCommands();
            InitializeComponent();

        }

        private void CheckModForMiscellaneousIssues(DeploymentChecklistItem item)
        {
            bool hasError = false;
            item.HasError = false;
            item.ItemText = M3L.GetString(M3L.string_checkingForMiscellaneousIssues);
            var referencedFiles = ModBeingDeployed.GetAllRelativeReferences();

            var metacmms = referencedFiles.Where(x => Path.GetFileName(x) == @"_metacmm.txt").ToList();

            if (metacmms.Any())
            {
                item.HasError = true;
                foreach (var m in metacmms)
                {
                    //Mods cannot include metacmm files
                    item.Errors.Add(M3L.GetString(M3L.string_interp_modReferencesMetaCmm, m));
                }
            }

            //end setup
            if (!item.Errors.Any())
            {
                item.Foreground = Brushes.Green;
                item.Icon = FontAwesomeIcon.CheckCircle;
                item.ItemText = M3L.GetString(M3L.string_noMiscellaneousIssuesDetected);
                item.ToolTip = M3L.GetString(M3L.string_validationOK);
            }
            else
            {
                item.ItemText = M3L.GetString(M3L.string_detectedMiscellaneousIssues);
                item.ToolTip = M3L.GetString(M3L.string_tooltip_deploymentChecksFoundMiscIssues);
                item.Foreground = Brushes.Red;
                item.Icon = FontAwesomeIcon.TimesCircle;
            }

            item.HasError = item.Errors.Any();

        }

        private void URLValidation(DeploymentChecklistItem obj)
        {
            bool OK = Uri.TryCreate(ModBeingDeployed.ModWebsite, UriKind.Absolute, out var uriResult)
                      && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
            if (!OK)
            {
                obj.HasError = true;
                obj.Icon = FontAwesomeIcon.TimesCircle;
                obj.Foreground = Brushes.Red;
                string url = ModBeingDeployed.ModWebsite ?? @"null";
                obj.Errors = new List<string>(new[] { M3L.GetString(M3L.string_interp_urlIsNotValid, url) });
                obj.ItemText = M3L.GetString(M3L.string_emptyOrInvalidModUrl);
                obj.ToolTip = M3L.GetString(M3L.string_validationFailed);

            }
            else
            {
                if (ModBeingDeployed.ModWebsite == Mod.DefaultWebsite)
                {
                    obj.Icon = FontAwesomeIcon.TimesCircle;
                    obj.Foreground = Brushes.Red;
                    obj.ItemText = M3L.GetString(M3L.string_moddescMissingModsite);
                    obj.Spinning = false;
                    obj.HasError = true;
                    obj.Errors.Add(M3L.GetString(M3L.string_noModWebsiteSet));
                }
                else
                {
                    obj.Icon = FontAwesomeIcon.CheckCircle;
                    obj.Foreground = Brushes.Green;
                    obj.ItemText = M3L.GetString(M3L.string_interp_modURLOK, ModBeingDeployed.ModWebsite);
                    obj.ToolTip = M3L.GetString(M3L.string_validationOK);
                }
            }
        }

        private void CheckLocalizationsME3(DeploymentChecklistItem obj)
        {
            var customDLCJob = ModBeingDeployed.GetJob(ModJob.JobHeader.CUSTOMDLC);
            var customDLCFolders = customDLCJob.CustomDLCFolderMapping.Keys.ToList();
            customDLCFolders.AddRange(customDLCJob.AlternateDLCs.Where(x => x.Operation == AlternateDLC.AltDLCOperation.OP_ADD_CUSTOMDLC).Select(x => x.AlternateDLCFolder));
            var languages = StarterKitGeneratorWindow.me3languages;
            List<string> errors = new List<string>();
            obj.ItemText = M3L.GetString(M3L.string_languageCheckInProgress);
            foreach (var customDLC in customDLCFolders)
            {
                if (_closed) return;
                var tlkBasePath = Path.Combine(ModBeingDeployed.ModPath, customDLC, @"CookedPCConsole", customDLC);
                Dictionary<string, List<TalkFileME1.TLKStringRef>> tlkMappings = new Dictionary<string, List<TalkFileME1.TLKStringRef>>();
                foreach (var language in languages)
                {
                    if (_closed) return;
                    var tlkLangPath = tlkBasePath + @"_" + language.filecode + @".tlk";
                    if (File.Exists(tlkLangPath))
                    {
                        //inspect
                        TalkFileME2ME3 tf = new TalkFileME2ME3();
                        tf.LoadTlkData(tlkLangPath);
                        tlkMappings[language.filecode] = tf.StringRefs;
                    }
                    else
                    {
                        errors.Add(M3L.GetString(M3L.string_interp_customDLCMissingLocalizedTLK, customDLC, language.filecode)); ;
                    }
                }
                if (tlkMappings.Count > 1)
                {
                    //find TLK with most entries
                    //var tlkCounts = tlkMappings.Select(x => (x.Key, x.Value.Count));
                    double numLoops = Math.Pow(tlkMappings.Count - 1, tlkMappings.Count - 1);
                    int numDone = 0;
                    foreach (var mapping1 in tlkMappings)
                    {
                        foreach (var mapping2 in tlkMappings)
                        {
                            if (mapping1.Equals(mapping2))
                            {
                                continue;
                            }

                            var differences = mapping1.Value.Select(x => x.StringID).Except(mapping2.Value.Select(x => x.StringID));
                            foreach (var difference in differences)
                            {
                                var str = mapping1.Value.FirstOrDefault(x => x.StringID == difference)?.Data ?? M3L.GetString(M3L.string_errorFindingString);
                                errors.Add(M3L.GetString(M3L.string_interp_tlkDifference, difference.ToString(), mapping1.Key, mapping2.Key, str));
                            }

                            numDone++;
                            double percent = (numDone * 100.0) / numLoops;
                            obj.ItemText = $@"{M3L.GetString(M3L.string_languageCheckInProgress)} {percent:0.00}%";
                        }
                    }
                    //use INT as master. Not sure if any mods are not-english based
                    //TODO
                }
            }
            if (errors.Count > 0)
            {
                obj.HasError = true;
                obj.Icon = FontAwesomeIcon.Warning;
                obj.Foreground = Brushes.Orange;
                obj.Errors = errors;
                obj.ItemText = M3L.GetString(M3L.string_languageCheckDetectedIssues);
                obj.ToolTip = M3L.GetString(M3L.string_validationFailed);

            }
            else
            {
                obj.Icon = FontAwesomeIcon.CheckCircle;
                obj.Foreground = Brushes.Green;
                obj.ItemText = M3L.GetString(M3L.string_noLanguageIssuesDetected);
                obj.ToolTip = M3L.GetString(M3L.string_validationOK);
            }
        }

        private void CheckModSFARs(DeploymentChecklistItem item)
        {
            bool hasError = false;
            item.HasError = false;
            item.ItemText = M3L.GetString(M3L.string_checkingSFARFilesSizes);
            var referencedFiles = ModBeingDeployed.GetAllRelativeReferences().Select(x => Path.Combine(ModBeingDeployed.ModPath, x)).ToList();
            int numChecked = 0;
            GameTarget validationTarget = mainwindow.InstallationTargets.FirstOrDefault(x => x.Game == ModBeingDeployed.Game);
            List<string> gameFiles = MEDirectories.EnumerateGameFiles(validationTarget.Game, validationTarget.TargetPath);

            var errors = new List<string>();
            Dictionary<string, MemoryStream> cachedAudio = new Dictionary<string, MemoryStream>();

            bool hasSFARs = false;
            foreach (var f in referencedFiles)
            {
                if (_closed) return;
                if (Path.GetExtension(f) == @".sfar")
                {
                    hasSFARs = true;
                    if (new FileInfo(f).Length != 32)
                    {
                        {
                            hasError = true;
                            item.Icon = FontAwesomeIcon.TimesCircle;
                            item.Foreground = Brushes.Red;
                            item.Spinning = false;
                            errors.Add(f);
                        }
                    }
                }
            }

            if (!hasSFARs)
            {
                item.Foreground = Brushes.Green;
                item.Icon = FontAwesomeIcon.CheckCircle;
                item.ItemText = M3L.GetString(M3L.string_modDoesNotUseSFARs);
                item.ToolTip = M3L.GetString(M3L.string_validationOK);
            }
            else
            {
                if (!hasError)
                {
                    item.Foreground = Brushes.Green;
                    item.Icon = FontAwesomeIcon.CheckCircle;
                    item.ItemText = M3L.GetString(M3L.string_noSFARSizeIssuesWereDetected);
                    item.ToolTip = M3L.GetString(M3L.string_validationOK);
                }
                else
                {
                    item.Errors = errors;
                    item.ItemText = M3L.GetString(M3L.string_someSFARSizesAreTheIncorrectSize);
                    item.ToolTip = M3L.GetString(M3L.string_validationFailed);
                }

                item.HasError = hasError;
            }
        }

        private void ManualValidation(DeploymentChecklistItem item)
        {
            item.Foreground = Brushes.Gray;
            item.Icon = FontAwesomeIcon.CheckCircle;
            item.ToolTip = M3L.GetString(M3L.string_thisItemMustBeManuallyCheckedByYou);
        }

        private void CheckModForAFCCompactability(DeploymentChecklistItem item)
        {
            bool hasError = false;
            item.HasError = false;
            item.ItemText = M3L.GetString(M3L.string_checkingAudioReferencesInMod);
            var referencedFiles = ModBeingDeployed.GetAllRelativeReferences().Select(x => Path.Combine(ModBeingDeployed.ModPath, x)).ToList();
            int numChecked = 0;
            GameTarget validationTarget = mainwindow.InstallationTargets.FirstOrDefault(x => x.Game == ModBeingDeployed.Game);
            List<string> gameFiles = MEDirectories.EnumerateGameFiles(validationTarget.Game, validationTarget.TargetPath);

            var errors = new List<string>();
            Dictionary<string, MemoryStream> cachedAudio = new Dictionary<string, MemoryStream>();

            foreach (var f in referencedFiles)
            {
                if (_closed) return;
                numChecked++;
                item.ItemText = $@"{M3L.GetString(M3L.string_checkingAudioReferencesInMod)} [{numChecked}/{referencedFiles.Count}]";
                if (f.RepresentsPackageFilePath())
                {
                    var package = MEPackageHandler.OpenMEPackage(f);
                    var wwiseStreams = package.Exports.Where(x => x.ClassName == @"WwiseStream" && !x.IsDefaultObject).ToList();
                    foreach (var wwisestream in wwiseStreams)
                    {
                        if (_closed) return;
                        //Check each reference.
                        var afcNameProp = wwisestream.GetProperty<NameProperty>(@"Filename");
                        if (afcNameProp != null)
                        {
                            string afcNameWithExtension = afcNameProp + @".afc";
                            int audioSize = BitConverter.ToInt32(wwisestream.Data, wwisestream.Data.Length - 8);
                            int audioOffset = BitConverter.ToInt32(wwisestream.Data, wwisestream.Data.Length - 4);

                            string afcPath = null;
                            Stream audioStream = null;
                            var localDirectoryAFCPath = Path.Combine(Path.GetDirectoryName(wwisestream.FileRef.FilePath), afcNameWithExtension);
                            bool isInOfficialArea = false;
                            if (File.Exists(localDirectoryAFCPath))
                            {
                                //local afc
                                afcPath = localDirectoryAFCPath;
                            }
                            else if (referencedFiles.Any(x => Path.GetFileName(x).Equals(afcNameWithExtension, StringComparison.InvariantCultureIgnoreCase)))
                            {
                                //found afc in mod.
                                //if there is multiple same-named AFCs in the mod, this might fail.
                                afcPath = Path.Combine(ModBeingDeployed.ModPath, referencedFiles.FirstOrDefault(x => Path.GetFileName(x).Equals(afcNameWithExtension, StringComparison.InvariantCultureIgnoreCase)));
                                if (!File.Exists(afcPath))
                                {
                                    Debugger.Break();
                                }
                            }
                            else
                            {
                                //Check game
                                var fullPath = gameFiles.FirstOrDefault(x => Path.GetFileName(x).Equals(afcNameWithExtension, StringComparison.InvariantCultureIgnoreCase));
                                if (fullPath != null)
                                {
                                    afcPath = fullPath;
                                    isInOfficialArea = MEDirectories.IsInBasegame(afcPath, validationTarget) || MEDirectories.IsInOfficialDLC(afcPath, validationTarget);
                                }
                                else if (cachedAudio.TryGetValue(afcNameProp.Value.Name, out var cachedAudioStream))
                                {
                                    audioStream = cachedAudioStream;
                                    //isInOfficialArea = true; //cached from vanilla SFAR
                                }
                                else if (MEDirectories.OfficialDLC(validationTarget.Game).Any(x => afcNameProp.Value.Name.StartsWith(x)))
                                {
                                    var dlcName = afcNameProp.Value.Name.Substring(0, afcNameProp.Value.Name.LastIndexOf(@"_", StringComparison.InvariantCultureIgnoreCase));
                                    var audio = VanillaDatabaseService.FetchFileFromVanillaSFAR(dlcName, afcNameWithExtension /*, validationTarget*/);
                                    if (audio != null)
                                    {
                                        cachedAudio[afcNameProp.Value.Name] = audio;
                                    }

                                    audioStream = audio;
                                    //isInOfficialArea = true; as this is in a vanilla SFAR we don't test against this since it will be correct.
                                    continue;
                                }
                                else
                                {
                                    hasError = true;
                                    item.Icon = FontAwesomeIcon.TimesCircle;
                                    item.Foreground = Brushes.Red;
                                    item.Spinning = false;
                                    errors.Add(M3L.GetString(M3L.string_interp_couldNotFindReferencedAFC, Path.GetFileName(wwisestream.FileRef.FilePath), wwisestream.GetInstancedFullPath, afcNameProp.ToString()));
                                    continue;
                                }
                            }

                            if (afcPath != null)
                            {
                                audioStream = new FileStream(afcPath, FileMode.Open);
                            }

                            try
                            {
                                audioStream.Seek(audioOffset, SeekOrigin.Begin);
                                if (audioStream.Position > audioStream.Length - 4)
                                {
                                    hasError = true;
                                    item.Icon = FontAwesomeIcon.TimesCircle;
                                    item.Foreground = Brushes.Red;
                                    item.Spinning = false;
                                    errors.Add(M3L.GetString(M3L.string_interp_invalidAudioPointerOutsideAFC, Path.GetFileName(wwisestream.FileRef.FilePath), wwisestream.UIndex, wwisestream.ObjectName, audioOffset, afcPath, audioStream.Length));
                                    if (audioStream is FileStream) audioStream.Close();
                                    continue;
                                }

                                if (audioStream.ReadStringASCIINull(4) != @"RIFF")
                                {
                                    hasError = true;
                                    item.Icon = FontAwesomeIcon.TimesCircle;
                                    item.Foreground = Brushes.Red;
                                    item.Spinning = false;
                                    errors.Add(M3L.GetString(M3L.string_interp_invalidAudioPointer, Path.GetFileName(wwisestream.FileRef.FilePath), wwisestream.GetInstancedFullPath));
                                    if (audioStream is FileStream) audioStream.Close();
                                    continue;
                                }

                                //attempt to seek audio length.
                                audioStream.Seek(audioSize + 4, SeekOrigin.Current);

                                //Check if this file is in basegame
                                if (isInOfficialArea)
                                {
                                    //Verify offset is not greater than vanilla size
                                    var vanillaInfo = VanillaDatabaseService.GetVanillaFileInfo(validationTarget, afcPath.Substring(validationTarget.TargetPath.Length + 1));
                                    if (vanillaInfo == null)
                                    {
                                        Crashes.TrackError(new Exception($@"Vanilla information was null when performing vanilla file check for {afcPath.Substring(validationTarget.TargetPath.Length + 1)}"));
                                    }
                                    if (audioOffset >= vanillaInfo[0].size)
                                    {
                                        hasError = true;
                                        item.Icon = FontAwesomeIcon.TimesCircle;
                                        item.Foreground = Brushes.Red;
                                        item.Spinning = false;
                                        errors.Add(M3L.GetString(M3L.string_interp_audioStoredInOfficialAFC, wwisestream.FileRef.FilePath, wwisestream.GetInstancedFullPath));
                                    }
                                }
                                if (audioStream is FileStream) audioStream.Close();
                            }
                            catch (Exception e)
                            {
                                hasError = true;
                                item.Icon = FontAwesomeIcon.TimesCircle;
                                item.Foreground = Brushes.Red;
                                item.Spinning = false;
                                if (audioStream is FileStream) audioStream.Close();
                                errors.Add(M3L.GetString(M3L.string_errorValidatingAudioReference, wwisestream.FileRef.FilePath, wwisestream.GetInstancedFullPath, e.Message));
                                continue;
                            }
                        }
                    }
                }
            }


            if (!hasError)
            {
                item.Foreground = Brushes.Green;
                item.Icon = FontAwesomeIcon.CheckCircle;
                item.ItemText = M3L.GetString(M3L.string_noAudioIssuesWereDetected);
                item.ToolTip = M3L.GetString(M3L.string_validationOK);
            }
            else
            {
                item.Errors = errors;
                item.ItemText = M3L.GetString(M3L.string_audioIssuesWereDetected);
                item.ToolTip = M3L.GetString(M3L.string_validationFailed);
            }
            item.HasError = hasError;
            cachedAudio.Clear();
        }

        private void CheckModForTFCCompactability(DeploymentChecklistItem item)
        {
            // if (ModBeingDeployed.Game >= Mod.MEGame.ME2)
            //{
            bool hasError = false;
            item.HasError = false;
            item.ItemText = M3L.GetString(M3L.string_checkingTexturesInMod);
            var referencedFiles = ModBeingDeployed.GetAllRelativeReferences().Select(x => Path.Combine(ModBeingDeployed.ModPath, x)).ToList();
            var allTFCs = referencedFiles.Where(x => Path.GetExtension(x) == @".tfc").ToList();
            int numChecked = 0;
            GameTarget validationTarget = mainwindow.InstallationTargets.FirstOrDefault(x => x.Game == ModBeingDeployed.Game);
            var errors = new List<string>();
            foreach (var f in referencedFiles)
            {
                if (_closed) return;
                numChecked++;
                item.ItemText = $@"{M3L.GetString(M3L.string_checkingTexturesInMod)} [{numChecked}/{referencedFiles.Count}]";
                if (f.RepresentsPackageFilePath())
                {
                    Log.Information(@"Checking file for broken textures: " + f);
                    var package = MEPackageHandler.OpenMEPackage(f);
                    var textures = package.Exports.Where(x => x.IsTexture()).ToList();
                    foreach (var texture in textures)
                    {
                        if (_closed) return;

                        if (package.Game > Mod.MEGame.ME1)
                        {
                            var cache = texture.GetProperty<NameProperty>(@"TextureFileCacheName");
                            if (cache != null)
                            {
                                if (!VanillaDatabaseService.IsBasegameTFCName(cache.Value, ModBeingDeployed.Game))
                                {
                                    //var mips = Texture2D.GetTexture2DMipInfos(texture, cache.Value);
                                    Texture2D tex = new Texture2D(texture);
                                    try
                                    {
                                        tex.GetImageBytesForMip(tex.GetTopMip(), validationTarget, false, allTFCs); //use active target
                                    }
                                    catch (Exception e)
                                    {
                                        Log.Information(@"Found broken texture: " + texture.GetInstancedFullPath);
                                        hasError = true;
                                        item.Icon = FontAwesomeIcon.TimesCircle;
                                        item.Foreground = Brushes.Red;
                                        item.Spinning = false;
                                        errors.Add(M3L.GetString(M3L.string_interp_couldNotLoadTextureData, texture.FileRef.FilePath, texture.GetInstancedFullPath, e.Message));
                                    }
                                }
                            }
                        }
                        else
                        {
                            Texture2D tex = new Texture2D(texture);
                            var cachename = tex.GetTopMip().TextureCacheName;
                            if (cachename != null)
                            {
                                foreach (var mip in tex.Mips)
                                {
                                    try
                                    {
                                        tex.GetImageBytesForMip(mip, validationTarget, false);
                                    }
                                    catch (Exception e)
                                    {
                                        Log.Information(@"Found broken texture: " + texture.GetInstancedFullPath);
                                        hasError = true;
                                        item.Icon = FontAwesomeIcon.TimesCircle;
                                        item.Foreground = Brushes.Red;
                                        item.Spinning = false;
                                        errors.Add(M3L.GetString(M3L.string_interp_couldNotLoadTextureData, texture.FileRef.FilePath, texture.GetInstancedFullPath, e.Message));
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (!hasError)
            {
                item.Foreground = Brushes.Green;
                item.Icon = FontAwesomeIcon.CheckCircle;
                item.ItemText = M3L.GetString(M3L.string_noBrokenTexturesWereFound);
                item.ToolTip = M3L.GetString(M3L.string_validationOK);
            }
            else
            {
                item.Errors = errors;
                item.ItemText = M3L.GetString(M3L.string_textureIssuesWereDetected);
                item.ToolTip = M3L.GetString(M3L.string_validationFailed);
            }
            item.HasError = hasError;
        }

        public ICommand DeployCommand { get; set; }
        public ICommand CloseCommand { get; set; }
        private void LoadCommands()
        {
            DeployCommand = new GenericCommand(StartDeployment, CanDeploy);
            CloseCommand = new GenericCommand(ClosePanel, CanClose);
        }

        private void ClosePanel()
        {
            _closed = true;
            OnClosing(DataEventArgs.Empty);
        }

        private bool CanClose() => !DeploymentInProgress;

        private void StartDeployment()
        {
            SaveFileDialog d = new SaveFileDialog
            {
                Filter = $@"{M3L.GetString(M3L.string_7zipArchiveFile)}|*.7z",
                FileName = Utilities.SanitizePath($@"{ModBeingDeployed.ModName}_{ModBeingDeployed.ModVersionString}".Replace(@" ", ""), true)
            };
            var result = d.ShowDialog();
            if (result.HasValue && result.Value)
            {
                NamedBackgroundWorker bw = new NamedBackgroundWorker(@"ModDeploymentThread");
                bw.DoWork += Deployment_BackgroundThread;
                bw.RunWorkerCompleted += (a, b) =>
                {
                    DeploymentInProgress = false;
                    CommandManager.InvalidateRequerySuggested();
                };
                bw.RunWorkerAsync(d.FileName);
                DeploymentInProgress = true;
            }
        }

        private void Deployment_BackgroundThread(object sender, DoWorkEventArgs e)
        {
            var referencedFiles = ModBeingDeployed.GetAllRelativeReferences();

            string archivePath = e.Argument as string;
            //Key is in-archive path, value is on disk path
            var archiveMapping = new Dictionary<string, string>();
            SortedSet<string> directories = new SortedSet<string>();
            foreach (var file in referencedFiles)
            {
                var path = Path.Combine(ModBeingDeployed.ModPath, file);
                var directory = Directory.GetParent(path).FullName;
                if (directory.Length <= ModBeingDeployed.ModPath.Length) continue; //root file or directory.
                directory = directory.Substring(ModBeingDeployed.ModPath.Length + 1);

                //nested folders with no folders
                var relativeFolders = directory.Split('\\');
                string buildingFolderList = "";
                foreach (var relativeFolder in relativeFolders)
                {
                    if (buildingFolderList != "")
                    {
                        buildingFolderList += @"\";
                    }
                    buildingFolderList += relativeFolder;
                    if (directories.Add(buildingFolderList))
                    {
                        archiveMapping[buildingFolderList] = null;
                    }
                }

            }

            archiveMapping.AddRange(referencedFiles.ToDictionary(x => x, x => Path.Combine(ModBeingDeployed.ModPath, x)));

            var compressor = new SevenZip.SevenZipCompressor();
            //compressor.CompressionLevel = CompressionLevel.Ultra;
            compressor.CustomParameters.Add(@"s", @"on");
            if (!MultithreadedCompression)
            {
                compressor.CustomParameters.Add(@"mt", @"off");
            }
            else
            {
                var foldersize = Utilities.GetSizeOfDirectory(ModBeingDeployed.ModPath);
                if (foldersize > ByteSize.BytesInGigaByte * 1.25)
                {
                    //cap threads to prevent huge memory
                    var cores = Environment.ProcessorCount;
                    cores = Math.Min(cores, 5);
                    compressor.CustomParameters.Add(@"mt", cores.ToString());
                }
            }


            compressor.CustomParameters.Add(@"yx", @"9");
            //compressor.CustomParameters.Add("x", "9");
            compressor.CustomParameters.Add(@"d", @"28");
            string currentDeploymentStep = M3L.GetString(M3L.string_mod);
            compressor.Progressing += (a, b) =>
            {
                //Debug.WriteLine(b.AmountCompleted + "/" + b.TotalAmount);
                ProgressMax = b.TotalAmount;
                ProgressValue = b.AmountCompleted;
                var now = DateTime.Now;
                if ((now - lastPercentUpdateTime).Milliseconds > ModInstaller.PERCENT_REFRESH_COOLDOWN)
                {
                    //Don't update UI too often. Once per second is enough.
                    string percent = (ProgressValue * 100.0 / ProgressMax).ToString(@"0.00");
                    OperationText = $@"[{currentDeploymentStep}] {M3L.GetString(M3L.string_deploymentInProgress)} {percent}%";
                    lastPercentUpdateTime = now;
                }
                //Debug.WriteLine(ProgressValue + "/" + ProgressMax);
            };
            compressor.FileCompressionStarted += (a, b) => { Debug.WriteLine(b.FileName); };
            compressor.CompressFileDictionary(archiveMapping, archivePath);
            compressor.CustomParameters.Clear(); //remove custom params as it seems to force LZMA
            compressor.CompressionMode = CompressionMode.Append;
            compressor.CompressionLevel = CompressionLevel.None;
            currentDeploymentStep = @"moddesc.ini";
            compressor.CompressFiles(archivePath, new string[]
            {
                Path.Combine(ModBeingDeployed.ModPath, @"moddesc.ini")
            });
            OperationText = M3L.GetString(M3L.string_deploymentSucceeded);
            Utilities.HighlightInExplorer(archivePath);
        }

        private bool CanDeploy()
        {
            return PrecheckCompleted && !DeploymentInProgress;
        }

        public ObservableCollectionExtended<DeploymentChecklistItem> DeploymentChecklistItems { get; } = new ObservableCollectionExtended<DeploymentChecklistItem>();
        public bool PrecheckCompleted { get; private set; }
        public bool DeploymentInProgress { get; private set; }
        public ulong ProgressMax { get; set; } = 100;
        public ulong ProgressValue { get; set; } = 0;
        public string OperationText { get; set; } = M3L.GetString(M3L.string_checkingModBeforeDeployment);
        //M3L.GetString(M3L.string_verifyAboveItemsBeforeDeployment);

        private DateTime lastPercentUpdateTime;
        private bool _closed;

        public class DeploymentChecklistItem : INotifyPropertyChanged
        {
            public string ItemText { get; set; }
            public SolidColorBrush Foreground { get; set; }
            public FontAwesomeIcon Icon { get; set; }
            public bool Spinning { get; set; }

            public Action<DeploymentChecklistItem> ValidationFunction;
            public Mod ModToValidateAgainst;
            internal string ErrorsMessage;
            internal string ErrorsTitle;

            public event PropertyChangedEventHandler PropertyChanged;

            public string ToolTip { get; set; }
            public bool HasError { get; internal set; }
            public List<string> Errors { get; internal set; } = new List<string>();

            //public DeploymentChecklistItem(string initialDisplayText, Mod m, Action<DeploymentChecklistItem> validationFunction)
            //{
            //    this.ItemText = initialDisplayText;
            //    this.ModToValidateAgainst = m;
            //    this.ValidationFunction = validationFunction;
            //    Icon = FontAwesomeIcon.Spinner;
            //    Spinning = true;
            //    Foreground = Brushes.Gray;
            //    ToolTip = "Validation in progress...";
            //}

            public DeploymentChecklistItem()
            {
                Icon = FontAwesomeIcon.Spinner;
                Spinning = true;
                Foreground = Application.Current.FindResource(AdonisUI.Brushes.DisabledAccentForegroundBrush) as SolidColorBrush;
                ToolTip = M3L.GetString(M3L.string_validationInProgress);
            }

            public void ExecuteValidationFunction()
            {
                Foreground = Application.Current.FindResource(AdonisUI.Brushes.HyperlinkBrush) as SolidColorBrush;
                ValidationFunction?.Invoke(this);
                //Debug.WriteLine("Invoke finished");
                Spinning = false;
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            if (sender is Hyperlink hl && hl.DataContext is DeploymentChecklistItem dcli)
            {
                ListDialog ld = new ListDialog(dcli.Errors, dcli.ErrorsTitle, dcli.ErrorsMessage, Window.GetWindow(hl));
                ld.ShowDialog();
            }
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && CanClose())
            {
                ClosePanel();
            }
        }

        public override void OnPanelVisible()
        {
            lastPercentUpdateTime = DateTime.Now;
            NamedBackgroundWorker bw = new NamedBackgroundWorker(@"DeploymentValidation");
            bw.DoWork += (a, b) =>
            {
                ProgressIndeterminate = true;
                foreach (var checkItem in DeploymentChecklistItems)
                {
                    checkItem.ExecuteValidationFunction();
                }
            };
            bw.RunWorkerCompleted += (a, b) =>
            {
                PrecheckCompleted = true;
                ProgressIndeterminate = false;
                OperationText = M3L.GetString(M3L.string_verifyAboveItemsBeforeDeployment);
                CommandManager.InvalidateRequerySuggested();
            };
            bw.RunWorkerAsync();
        }

        public bool ProgressIndeterminate { get; set; }
    }
}
