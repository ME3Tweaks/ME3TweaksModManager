using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using MassEffectModManagerCore.modmanager.asi;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.usercontrols;
using MassEffectModManagerCore.ui;
using LegendaryExplorerCore.GameFilesystem;
using Serilog;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using static MassEffectModManagerCore.modmanager.usercontrols.InstallationInformation;
using MemoryAnalyzer = MassEffectModManagerCore.modmanager.memoryanalyzer.MemoryAnalyzer;

namespace MassEffectModManagerCore.modmanager.objects
{
    [DebuggerDisplay("GameTarget {Game} {TargetPath}")]
    public class GameTarget : IEqualityComparer<GameTarget>, INotifyPropertyChanged
    {
        public const uint MEMI_TAG = 0x494D454D;

        //Fody uses this property on weaving
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore

        public MEGame Game { get; }
        public string TargetPath { get; }
        public bool RegistryActive { get; set; }
        public string GameSource { get; private set; }
        public string ExecutableHash { get; private set; }

        public string TargetBootIcon
        {
            get
            {
                if (GameSource == null) return @"/images/unknown.png";
                if (GameSource.Contains(@"Steam")) return @"/images/steam.png"; //higher priority than Origin in icon will make the Steam/Origin mix work
                if (GameSource.Contains(@"Origin")) return @"/images/origin.png";
                if (GameSource.Contains(@"DVD")) return @"/images/dvd.png";
                return @"/images/unknown.png";
            }
        }
        public bool Supported => GameSource != null;
        public bool IsPolishME1 { get; private set; }

        /// <summary>
        /// Determines if this gametarget can be chosen in dropdowns
        /// </summary>
        public bool Selectable { get; internal set; } = true;
        public string ALOTVersion { get; private set; }
        /// <summary>
        /// Indicates that this is a custom, abnormal game object. It may be used only for UI purposes, but it depends on the context.
        /// </summary>
        public bool IsCustomOption { get; set; } = false;
        public GameTarget(MEGame game, string targetRootPath, bool currentRegistryActive, bool isCustomOption = false, bool isTest = false, bool skipInit = false)
        {
            this.Game = game;
            this.RegistryActive = currentRegistryActive;
            this.IsCustomOption = isCustomOption;
            this.TargetPath = targetRootPath.TrimEnd('\\');
            MemoryAnalyzer.AddTrackedMemoryItem($@"{game} GameTarget {TargetPath} - IsCustomOption: {isCustomOption}", new WeakReference(this));
            ReloadGameTarget(isTest, skipInit: skipInit);
        }

        public void ReloadGameTarget(bool lodUpdateAndLogging = true, bool forceLodUpdate = false, bool reverseME1Executable = true, bool skipInit = false)
        {
            // Unknown = 
            if (!IsCustomOption && !skipInit)
            {
                if (Directory.Exists(TargetPath))
                {
                    CLog.Information(@"Getting game source for target " + TargetPath, lodUpdateAndLogging);
                    var hashCheckResult = VanillaDatabaseService.GetGameSource(this, reverseME1Executable);

                    GameSource = hashCheckResult.result;
                    ExecutableHash = hashCheckResult.hash;

                    if (ExecutableHash.Length != 32)
                    {
                        CLog.Error($@"Issue getting game source: {ExecutableHash}", lodUpdateAndLogging);
                    }
                    else
                    {

                        if (GameSource == null)
                        {
                            // No source is listed
                            CLog.Error(@"Unknown source or illegitimate installation: " + hashCheckResult.hash,
                                lodUpdateAndLogging);
                        }
                        else
                        {
                            if (GameSource.Contains(@"Origin") && (Game is MEGame.ME3 or MEGame.LELauncher || Game.IsLEGame()))
                            {
                                // Check for steam
                                var testPath = Game == MEGame.ME3 ? TargetPath : Directory.GetParent(TargetPath).FullName;
                                if (Game != MEGame.ME3)
                                {
                                    testPath = Directory.GetParent(testPath).FullName;
                                }
                                if (Directory.Exists(Path.Combine(testPath, @"__overlay")))
                                {
                                    GameSource += @" (Steam version)";
                                }
                            }

                            CLog.Information(@"Source: " + GameSource, lodUpdateAndLogging);
                        }
                    }

                    if (Game != MEGame.LELauncher)
                    {
                        // Actual game
                        var oldTMOption = TextureModded;
                        var alotInfo = GetInstalledALOTInfo();
                        if (alotInfo != null)
                        {
                            TextureModded = true;
                            ALOTVersion = alotInfo.ToString();
                            if (alotInfo.MEUITMVER > 0)
                            {
                                MEUITMInstalled = true;
                                MEUITMVersion = alotInfo.MEUITMVER;
                            }
                        }
                        else
                        {
                            TextureModded = false;
                            ALOTVersion = null;
                            MEUITMInstalled = false;
                            MEUITMVersion = 0;
                        }

                        IsPolishME1 = Game == MEGame.ME1 && File.Exists(Path.Combine(TargetPath, @"BioGame", @"CookedPC", @"Movies", @"niebieska_pl.bik"));
                        if (IsPolishME1)
                        {
                            CLog.Information(@"ME1 Polish Edition detected", lodUpdateAndLogging);
                        }

                        if (RegistryActive && (Settings.AutoUpdateLODs2K || Settings.AutoUpdateLODs4K) &&
                            oldTMOption != TextureModded && forceLodUpdate)
                        {
                            UpdateLODs(Settings.AutoUpdateLODs2K);
                        }
                    }
                    else
                    {
                        // LELAUNCHER
                        IsValid = true; //set to false if target becomes invalid
                    }
                }
                else
                {
                    Log.Error($@"Target is invalid: {TargetPath} does not exist (or is not accessible)");
                    IsValid = false;
                }
            }
            else
            {
                // Custom Option
                IsValid = true;
            }
        }

        public void UpdateLODs(bool twoK)
        {
            if (Game.IsLEGame())
                return; // Do not update LE LODs for now.

            if (!TextureModded)
            {
                Utilities.SetLODs(this, false, false, false);
            }
            else
            {
                if (Game == MEGame.ME1)
                {
                    if (MEUITMInstalled)
                    {
                        //detect soft shadows/meuitm
                        var branchingPCFCommon =
                            Path.Combine(TargetPath, @"Engine", @"Shaders", @"BranchingPCFCommon.usf");
                        if (File.Exists(branchingPCFCommon))
                        {
                            var md5 = Utilities.CalculateMD5(branchingPCFCommon);
                            Utilities.SetLODs(this, true, twoK, md5 == @"10db76cb98c21d3e90d4f0ffed55d424");
                            return;
                        }
                    }
                }
                else if (Game.IsOTGame())
                {
                    //me2/3
                    Utilities.SetLODs(this, true, twoK, false);
                }
            }
        }

        public bool Equals(GameTarget x, GameTarget y)
        {
            return x.TargetPath == y.TargetPath && x.Game == y.Game;
        }

        public int GetHashCode(GameTarget obj)
        {
            return obj.TargetPath.GetHashCode();
        }

        public bool TextureModded { get; private set; }


        private TextureModInstallationInfo cachedTextureModInfo;

        /// <summary>
        /// Gets the installed texture mod info. If startpos is not defined (<0) the latest version is used from the end of the file.
        /// </summary>
        /// <param name="startpos"></param>
        /// <returns></returns>
        public TextureModInstallationInfo GetInstalledALOTInfo(int startPos = -1, bool allowCached = true)
        {
            if (allowCached && cachedTextureModInfo != null && startPos == -1) return cachedTextureModInfo;

            string gamePath = getALOTMarkerFilePath();
            if (gamePath != null && File.Exists(gamePath))
            {
                try
                {
                    using FileStream fs = new FileStream(gamePath, System.IO.FileMode.Open, FileAccess.Read);
                    if (startPos < 0)
                    {
                        fs.SeekEnd();
                    }
                    else
                    {
                        fs.Seek(startPos, SeekOrigin.Begin);
                    }

                    long endPos = fs.Position;
                    fs.Position = endPos - 4;
                    uint memi = fs.ReadUInt32();

                    if (memi == MEMI_TAG)
                    {
                        long markerStartOffset = fs.Position;
                        //MEM has been run on this installation
                        fs.Position = endPos - 8;
                        short installerVersionUsed = fs.ReadInt16();
                        short memVersionUsed = fs.ReadInt16();
                        fs.Position -= 4; //roll back so we can read this whole thing as 4 bytes
                        int preMemi4Bytes = fs.ReadInt32();
                        int perGameFinal4Bytes = -20;
                        switch (Game)
                        {
                            case MEGame.ME1:
                                perGameFinal4Bytes = 0;
                                break;
                            case MEGame.ME2:
                                perGameFinal4Bytes = 4352;
                                break;
                            case MEGame.ME3:
                                perGameFinal4Bytes = 16777472;
                                break;
                        }

                        // Note: If MEMI v1 is written after any other MEMI marker, it will not work as we cannot differentiate v1 to v2+

                        if (preMemi4Bytes != perGameFinal4Bytes) //default bytes before 178 MEMI Format (MEMI v1)
                        {
                            // MEMI v3 (and technically also v2 but values will be wrong)
                            fs.Position = endPos - 12;
                            short ALOTVER = fs.ReadInt16();
                            byte ALOTUPDATEVER = (byte)fs.ReadByte();
                            byte ALOTHOTFIXVER = (byte)fs.ReadByte();

                            //unused for now
                            fs.Position = endPos - 16;

                            markerStartOffset = fs.Position;
                            int MEUITMVER = fs.ReadInt32();

                            var tmii = new TextureModInstallationInfo(ALOTVER, ALOTUPDATEVER, ALOTHOTFIXVER, MEUITMVER, memVersionUsed, installerVersionUsed);
                            tmii.MarkerExtendedVersion = 0x03; // detected memi v3
                            tmii.MarkerStartPosition = (int)markerStartOffset;

                            // MEMI v4 DETECTION
                            fs.Position = endPos - 20;
                            if (fs.ReadUInt32() == TextureModInstallationInfo.TEXTURE_MOD_MARKER_VERSIONING_MAGIC)
                            {
                                // It's MEMI v4 (or higher)
                                var memiExtendedEndPos = endPos - 24; // Sanity check should make reading end here
                                fs.Position = memiExtendedEndPos;
                                fs.Position = fs.ReadInt32(); // Go to start of MEMI extended marker
                                tmii.MarkerStartPosition = (int)fs.Position;
                                tmii.MarkerExtendedVersion = fs.ReadInt32();
                                // Extensions to memi format go here

                                if (tmii.MarkerExtendedVersion == 0x04)
                                {
                                    tmii.InstallerVersionFullName = fs.ReadUnrealString();
                                    tmii.InstallationTimestamp = DateTime.FromBinary(fs.ReadInt64());
                                    var fileCount = fs.ReadInt32();
                                    for (int i = 0; i < fileCount; i++)
                                    {
                                        tmii.InstalledTextureMods.Add(new TextureModInstallationInfo.InstalledTextureMod(fs, tmii.MarkerExtendedVersion));
                                    }
                                }

                                if (fs.Position != memiExtendedEndPos)
                                {
                                    Log.Warning($@"Sanity check for MEMI extended marker failed. We did not read data until the marker info offset. Should be at 0x{memiExtendedEndPos:X6}, but ended at 0x{fs.Position:X6}");
                                }
                            }
                            if (startPos == -1) cachedTextureModInfo = tmii;
                            return tmii;
                        }

                        var info = new TextureModInstallationInfo(0, 0, 0, 0)
                        {
                            MarkerStartPosition = (int)markerStartOffset,
                            MarkerExtendedVersion = 0x01
                        }; //MEMI tag but no info we know of

                        if (startPos == -1) cachedTextureModInfo = info;
                        return info;
                    }
                }
                catch (Exception e)
                {
                    Log.Error($@"Error reading texture mod marker file for {Game}. Installed info will be returned as null (nothing installed). " + e.Message);
                    return null;
                }
            }

            return null;
            //Debug. Force ALOT always on
            //return new ALOTVersionInfo(9, 0, 0, 0); //MEMI tag but no info we know of
        }

        private string getALOTMarkerFilePath()
        {
            // this used to be shared method
            return M3Directories.GetTextureMarkerPath(this);
        }

        public ui.ObservableCollectionExtended<ModifiedFileObject> ModifiedBasegameFiles { get; } = new ui.ObservableCollectionExtended<ModifiedFileObject>();
        public ui.ObservableCollectionExtended<SFARObject> ModifiedSFARFiles { get; } = new ui.ObservableCollectionExtended<SFARObject>();
        public ICollectionView ModifiedBasegameFilesView => CollectionViewSource.GetDefaultView(ModifiedBasegameFiles);

        public void PopulateModifiedBasegameFiles(Func<string, bool> restoreBasegamefileConfirmationCallback,
            Func<string, bool> restoreSfarConfirmationCallback,
            Action notifySFARRestoringCallback,
            Action notifyFileRestoringCallback,
            Action<object> notifyRestoredCallback)
        {
            ModifiedBasegameFiles.ClearEx();
            ModifiedSFARFiles.ClearEx();

            List<string> modifiedSfars = new List<string>();
            List<string> modifiedFiles = new List<string>();
            void failedCallback(string file)
            {
                if (file.EndsWith(@".sfar"))
                {
                    modifiedSfars.Add(file);
                    return;
                }

                if (this.Game != MEGame.LELauncher && file == getALOTMarkerFilePath())
                {
                    return; //Do not report this file as modified or user will desync game state with texture state
                }
                modifiedFiles.Add(file);
            }
            VanillaDatabaseService.ValidateTargetAgainstVanilla(this, failedCallback);

            List<string> inconsistentDLC = new List<string>();
            VanillaDatabaseService.ValidateTargetDLCConsistency(this, x => inconsistentDLC.Add(x));

            modifiedSfars.AddRange(inconsistentDLC.Select(x => Path.Combine(x, @"CookedPCConsole", @"Default.sfar")));
            modifiedSfars = modifiedSfars.Distinct().ToList(); //filter out if modified + inconsistent

            ModifiedSFARFiles.AddRange(modifiedSfars.Select(file => new SFARObject(file, this, restoreSfarConfirmationCallback, notifySFARRestoringCallback, notifyRestoredCallback)));
            ModifiedBasegameFiles.AddRange(modifiedFiles.Select(file => new ModifiedFileObject(file.Substring(TargetPath.Length + 1), this,
                restoreBasegamefileConfirmationCallback,
                notifyFileRestoringCallback,
                notifyRestoredCallback)));
        }

        /// <summary>
        /// Call this when the modified object lists are no longer necessary
        /// </summary>
        public void DumpModifiedFilesFromMemory()
        {
            //Some commands are made from a background thread, which means this might not be called from dispatcher
            App.Current.Dispatcher.Invoke(delegate
            {
                ModifiedBasegameFiles.ClearEx();
                ModifiedSFARFiles.ClearEx();
                foreach (var uiInstalledDlcMod in UIInstalledDLCMods)
                {
                    uiInstalledDlcMod.ClearHandlers();
                }

                UIInstalledDLCMods.ClearEx();
            });
        }

        public ui.ObservableCollectionExtended<InstallationInformation.InstalledDLCMod> UIInstalledDLCMods { get; } = new ui.ObservableCollectionExtended<InstallationInformation.InstalledDLCMod>();
        public ui.ObservableCollectionExtended<InstalledOfficialDLC> UIInstalledOfficialDLC { get; } = new ui.ObservableCollectionExtended<InstalledOfficialDLC>();

        public void PopulateDLCMods(bool includeDisabled, Func<InstallationInformation.InstalledDLCMod, bool> deleteConfirmationCallback = null, Action notifyDeleted = null, Action notifyToggled = null, bool modNamePrefersTPMI = false)
        {
            if (Game == MEGame.LELauncher) return; // LE Launcher doesn't have DLC mods
            var dlcDir = M3Directories.GetDLCPath(this);
            var allOfficialDLCforGame = MEDirectories.OfficialDLC(Game);
            var installedDLC = M3Directories.GetInstalledDLC(this, includeDisabled);
            var installedMods = installedDLC.Where(x => !allOfficialDLCforGame.Contains(x.TrimStart('x'), StringComparer.InvariantCultureIgnoreCase));

            // Also populate official DLC
            var installedOfficialDLC = installedDLC.Where(x => allOfficialDLCforGame.Contains(x, StringComparer.InvariantCultureIgnoreCase));
            var notInstalledOfficialDLC = allOfficialDLCforGame.Where(x => !installedOfficialDLC.Contains(x));

            var officialDLC = installedOfficialDLC.Select(x => new InstalledOfficialDLC(x, true, Game)).ToList();
            officialDLC.AddRange(notInstalledOfficialDLC.Select(x => new InstalledOfficialDLC(x, false, Game)));
            officialDLC = officialDLC.OrderBy(x => x.HumanName).ToList();

            //Must run on UI thread
            Application.Current.Dispatcher.Invoke(delegate
            {
                UIInstalledDLCMods.ReplaceAll(installedMods.Select(x => new InstallationInformation.InstalledDLCMod(Path.Combine(dlcDir, x), Game, deleteConfirmationCallback, notifyDeleted, notifyToggled, modNamePrefersTPMI)).ToList().OrderBy(x => x.ModName));
                UIInstalledOfficialDLC.ReplaceAll(officialDLC);
            });
        }

        public bool IsTargetWritable()
        {
            if (Game == MEGame.LELauncher)
            {
                return Utilities.IsDirectoryWritable(TargetPath) && Utilities.IsDirectoryWritable(Path.Combine(TargetPath, @"Content"));
            }
            return Utilities.IsDirectoryWritable(TargetPath) && Utilities.IsDirectoryWritable(Path.Combine(TargetPath, @"Binaries"));
        }

        public bool IsValid { get; set; }

        /// <summary>
        /// Validates a game directory by checking for multiple things that should be present in a working game.
        /// </summary>
        /// <param name="ignoreCmmVanilla">Ignore the check for a cmm_vanilla file. Presence of this file will cause validation to fail</param>
        /// <returns>String of failure reason, null if OK</returns>
        public string ValidateTarget(bool ignoreCmmVanilla = false)
        {
            if (!Selectable)
            {
                return null;
            }
            IsValid = false; //set to invalid at first/s
            string[] validationFiles = null;
            switch (Game)
            {
                case MEGame.ME1:
                    validationFiles = new[]
                    {
                        Path.Combine(TargetPath, @"Binaries", @"MassEffect.exe"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPC", @"Maps", @"EntryMenu.SFM"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPC", @"BIOC_Base.u"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPC", @"Packages", @"Textures", @"BIOA_GLO_00_A_Opening_FlyBy_T.upk"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPC", @"Maps", @"WAR", @"LAY", @"BIOA_WAR20_05_LAY.SFM"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPC", @"Movies", @"MEvisionSEQ3.bik")
                    };
                    break;
                case MEGame.ME2:
                    validationFiles = new[]
                    {
                        Path.Combine(TargetPath, @"Binaries", @"MassEffect2.exe"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPC", @"BioA_BchLmL.pcc"),
                        Path.Combine(TargetPath, @"BioGame", @"Config", @"PC", @"Cooked", @"Coalesced.ini"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPC", @"Wwise_Jack_Loy_Music.afc"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPC", @"WwiseAudio.pcc"),
                        Path.Combine(TargetPath, @"BioGame", @"Movies", @"Crit03_CollectArrive_Part2_1.bik")
                    };
                    break;
                case MEGame.ME3:
                    validationFiles = new[]
                    {
                        Path.Combine(TargetPath, @"Binaries", @"Win32", @"MassEffect3.exe"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPCConsole", @"Textures.tfc"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPCConsole", @"Startup.pcc"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPCConsole", @"Coalesced.bin"),
                        Path.Combine(TargetPath, @"BioGame", @"Patches", @"PCConsole", @"Patch_001.sfar"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPCConsole", @"Textures.tfc"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPCConsole", @"citwrd_rp1_bailey_m_D_Int.afc")
                    };
                    break;
                case MEGame.LE1:
                    validationFiles = new[]
                    {
                        Path.Combine(TargetPath, @"Binaries", @"Win64", @"MassEffect1.exe"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPCConsole", @"Textures5.tfc"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPCConsole", @"Startup_INT.pcc"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPCConsole", @"Coalesced_INT.bin"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPCConsole", @"Textures.tfc"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPCConsole", @"PlotManagerAutoDLC_UNC.pcc")
                    };
                    break;
                case MEGame.LE2:
                    validationFiles = new[]
                    {
                        Path.Combine(TargetPath, @"Binaries", @"Win64", @"MassEffect2.exe"),
                        Path.Combine(TargetPath, @"BioGame", @"PCConsoleTOC.bin"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPCConsole", @"Startup_INT.pcc"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPCConsole", @"Coalesced_INT.bin"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPCConsole", @"BioD_QuaTlL_505LifeBoat_LOC_INT.pcc"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPCConsole", @"cithub_ad_low_a_S_INT.afc"),
                        Path.Combine(TargetPath, @"BioGame", @"DLC", @"DLC_METR_Patch01", @"CookedPCConsole", @"BioA_Nor_103aGalaxyMap.pcc")
                    };
                    break;
                case MEGame.LE3:
                    validationFiles = new[]
                    {
                        Path.Combine(TargetPath, @"Binaries", @"Win64", @"MassEffect3.exe"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPCConsole", @"Textures1.tfc"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPCConsole", @"Startup.pcc"),
                        Path.Combine(TargetPath, @"BioGame", @"DLC", @"DLC_CON_PRO3", @"CookedPCConsole", @"DLC_CON_PRO3_INT.tlk"),
                        Path.Combine(TargetPath, @"BioGame", @"DLC", @"DLC_CON_END", @"CookedPCConsole", @"BioD_End001_910RaceToConduit.pcc"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPCConsole", @"citwrd_rp1_bailey_m_D_Int.afc")
                    };
                    break;
                case MEGame.LELauncher: // LELAUNCHER
                    validationFiles = new[]
                    {
                        Path.Combine(TargetPath, @"MassEffectLauncher.exe"),
                        Path.Combine(TargetPath, @"Content", @"EulaUI.swf"),
                        Path.Combine(TargetPath, @"Content", @"click.wav"),
                        Path.Combine(TargetPath, @"Content", @"LauncherUI.swf"),
                        Path.Combine(TargetPath, @"Content", @"Xbox_ControllerIcons.swf"),
                        Path.Combine(TargetPath, @"Content", @"Sounds", @"mus_gui_menu_looping_quad.wav"),
                    };
                    break;
            }

            if (validationFiles == null) return null; //Invalid game.
            foreach (var f in validationFiles)
            {
                if (!File.Exists(f))
                {
                    return M3L.GetString(M3L.string_interp_invalidTargetMissingFile, Path.GetFileName(f));
                }
            }

            // Check exe on first file
            var exeInfo = FileVersionInfo.GetVersionInfo(validationFiles[0]);
            switch (Game)
            {
                /*
                MassEffect.exe 1.2.20608.0
                MassEffect2.exe 1.2.1604.0 (File Version)
                ME2Game.exe is the same
                MassEffect3.exe 1.5.5427.124
                */
                case MEGame.ME1:
                    if (exeInfo.FileVersion != @"1.2.20608.0")
                    {
                        // NOT SUPPORTED
                        return M3L.GetString(M3L.string_interp_unsupportedME1Version, exeInfo.FileVersion);
                    }
                    break;
                case MEGame.ME2:
                    if (exeInfo.FileVersion != @"1.2.1604.0" && exeInfo.FileVersion != @"01604.00") // Steam and Origin exes have different FileVersion for some reason
                    {
                        // NOT SUPPORTED
                        return M3L.GetString(M3L.string_interp_unsupportedME2Version, exeInfo.FileVersion);
                    }
                    break;
                case MEGame.ME3:
                    if (exeInfo.FileVersion != @"05427.124") // not really sure what's going on here
                    {
                        // NOT SUPPORTED
                        return M3L.GetString(M3L.string_interp_unsupportedME3Version, exeInfo.FileVersion);
                    }
                    break;

                    // No check for Legendary Edition games right now until patch cycle ends
            }

            if (!ignoreCmmVanilla)
            {
                if (File.Exists(Path.Combine(TargetPath, @"cmm_vanilla"))) return M3L.GetString(M3L.string_invalidTargetProtectedByCmmvanilla);
            }

            IsValid = true;
            return null;
        }

        protected bool Equals(GameTarget other)
        {
            return TargetPath == other.TargetPath;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((GameTarget)obj);
        }

        public string RemoveTargetTooltipText
        {
            get
            {
                if (RegistryActive) return M3L.GetString(M3L.string_dialog_cannotRemoveActiveTarget);
                return M3L.GetString(M3L.string_tooltip_removeTargetFromM3);
            }
        }

        public bool MEUITMInstalled { get; private set; }
        public int MEUITMVersion { get; private set; }

        public override int GetHashCode()
        {
            return (TargetPath != null ? TargetPath.GetHashCode() : 0);
        }

        private Queue<SFARObject> SFARRestoreQueue = new Queue<SFARObject>();

        public class SFARObject : INotifyPropertyChanged
        {
            public SFARObject(string file, GameTarget target, Func<string, bool> restoreSFARCallback,
                Action startingRestoreCallback, Action<object> notifyNoLongerModifiedCallback)
            {
                RestoreConfirmationCallback = restoreSFARCallback;
                IsModified = true;
                this.startingRestoreCallback = startingRestoreCallback;
                this.notifyNoLongerModified = notifyNoLongerModifiedCallback;
                this.target = target;
                Unpacked = new FileInfo(file).Length == 32;
                DLCDirectory = Directory.GetParent(Directory.GetParent(file).FullName).FullName;
                FilePath = file.Substring(target.TargetPath.Length + 1);
                if (Path.GetFileName(file) == @"Patch_001.sfar")
                {
                    UIString = @"TestPatch";
                    IsMPSFAR = true;
                    IsSPSFAR = true;
                }
                else
                {
                    var dlcFoldername = Directory.GetParent(Directory.GetParent(file).FullName).FullName;
                    if (dlcFoldername.Contains(@"DLC_UPD") || dlcFoldername.Contains(@"DLC_CON_MP"))
                    {
                        IsMPSFAR = true;
                    }
                    else
                    {
                        IsSPSFAR = true;
                    }

                    ME3Directory.OfficialDLCNames.TryGetValue(Path.GetFileName(dlcFoldername), out var name);
                    UIString = name;
                    if (Unpacked)
                    {
                        UIString += @" - " + M3L.GetString(M3L.string_unpacked);
                    }

                    if (!Unpacked)
                    {
                        var filesInSfarDir = Directory.EnumerateFiles(DLCDirectory, @"*.*", SearchOption.AllDirectories).ToList();
                        if (filesInSfarDir.Any(d =>
                            !Path.GetFileName(d).Equals(@"PCConsoleTOC.bin", StringComparison.InvariantCultureIgnoreCase) && //pcconsoletoc will be produced for all folders even with autotoc asi even if its not needed
                            VanillaDatabaseService.UnpackedFileExtensions.Contains(Path.GetExtension(d.ToLower()))))
                        {
                            Inconsistent = true;
                        }
                    }
                }

                RestoreCommand = new GenericCommand(RestoreSFARWrapper, CanRestoreSFAR);
            }

            public bool IsSPSFAR { get; private set; }
            public bool IsMPSFAR { get; private set; }

            public bool RevalidateIsModified(bool notify = true)
            {
                bool _isModified = IsModified;
                IsModified = !VanillaDatabaseService.IsFileVanilla(target, Path.Combine(target.TargetPath, FilePath));
                if (!IsModified && _isModified && notify)
                {
                    //Debug.WriteLine("Notifying that " + FilePath + " is no longer modified.");
                    notifyNoLongerModified?.Invoke(this);
                }

                return IsModified;
            }

            public bool IsModified { get; set; }

            private Action<object> notifyNoLongerModified;

            private void RestoreSFARWrapper()
            {
                RestoreSFAR(false);
            }

            public void RestoreSFAR(bool batchRestore, Action signalRestoreCompleted = null)
            {
                bool? restore = batchRestore;

                if (!restore.Value) restore = RestoreConfirmationCallback?.Invoke(FilePath);
                if (restore.HasValue && restore.Value)
                {
                    NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"RestoreSFARThread");
                    nbw.DoWork += (a, b) =>
                    {
                        var bup = BackupService.GetGameBackupPath(target.Game);
                        if (bup != null)
                        {
                            var backupFile = Path.Combine(bup, FilePath);
                            var targetFile = Path.Combine(target.TargetPath, FilePath);
                            Restoring = true;

                            var unpackedFiles = Directory.GetFiles(DLCDirectory, @"*", SearchOption.AllDirectories);
                            RestoreButtonContent = M3L.GetString(M3L.string_cleaningUp);
                            foreach (var file in unpackedFiles)
                            {
                                if (!file.EndsWith(@".sfar"))
                                {
                                    Log.Information(@"Deleting unpacked file: " + file);
                                    File.Delete(file);
                                }
                            }

                            // Check if we actually need to restore SFAR
                            if (new FileInfo(targetFile).Length == 32 || !VanillaDatabaseService.IsFileVanilla(target, targetFile, false))
                            {
                                Log.Information($@"Restoring SFAR from backup: {backupFile} -> {targetFile}");
                                XCopy.Copy(backupFile, targetFile, true, true,
                                    (o, pce) =>
                                    {
                                        RestoreButtonContent = M3L.GetString(M3L.string_interp_restoringXpercent,
                                            pce.ProgressPercentage.ToString());
                                    });
                            }

                            Utilities.DeleteEmptySubdirectories(DLCDirectory);
                            RestoreButtonContent = M3L.GetString(M3L.string_restored);
                        }
                        else
                        {
                            Restoring = false;
                        }
                    };
                    nbw.RunWorkerCompleted += (a, b) =>
                    {
                        if (b.Error != null)
                        {
                            Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
                        }
                        //File.Copy(backupFile, targetFile, true);
                        //if (!batchRestore)
                        //{
                        RevalidateIsModified();
                        //restoreCompletedCallback?.Invoke();
                        //}
                        Restoring = false;
                        signalRestoreCompleted?.Invoke();
                    };
                    startingRestoreCallback?.Invoke();
                    nbw.RunWorkerAsync();
                }
            }


            public static bool HasUnpackedFiles(string sfarFile)
            {
                var unpackedFiles =
                    Directory.GetFiles(Directory.GetParent(Directory.GetParent(sfarFile).FullName).FullName, @"*",
                        SearchOption.AllDirectories);
                return (unpackedFiles.Any(x =>
                    Path.GetExtension(x) == @".bin" && Path.GetFileNameWithoutExtension(x) != @"PCConsoleTOC"));
            }

            private bool checkedForBackupFile;
            private bool canRestoreSfar;
            public bool Restoring { get; set; }
            public bool OtherSFARBeingRestored { get; set; }

            private bool CanRestoreSFAR()
            {
                if (Restoring) return false;
                if (OtherSFARBeingRestored) return false;
                if (checkedForBackupFile) return canRestoreSfar;
                var backupPath = BackupService.GetGameBackupPath(target.Game);
                canRestoreSfar = backupPath != null && File.Exists(Path.Combine(backupPath, FilePath));
                checkedForBackupFile = true;
                if (!canRestoreSfar)
                {
                    RestoreButtonContent = M3L.GetString(M3L.string_noBackup);
                }
                return canRestoreSfar;
            }

            private Func<string, bool> RestoreConfirmationCallback;

            public string RestoreButtonContent { get; set; } = M3L.GetString(M3L.string_restore);

            private Action startingRestoreCallback;
            private readonly GameTarget target;
            private readonly bool Unpacked;

            public string DLCDirectory { get; }

            //Fody uses this property on weaving
#pragma warning disable
            public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore

            public ICommand RestoreCommand { get; }

            public string FilePath { get; }
            public string UIString { get; }
            public bool Inconsistent { get; }

        }

        public class ModifiedFileObject : INotifyPropertyChanged
        {
            private bool canRestoreFile;
            private bool checkedForBackupFile;
            public string FilePath { get; }
            private GameTarget target;
            private Action<object> notifyRestoredCallback;
            private Action notifyRestoringCallback;
            private Func<string, bool> restoreBasegamefileConfirmationCallback;

            //Fody uses this property on weaving
#pragma warning disable
            public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore

            public ICommand RestoreCommand { get; }
            public bool Restoring { get; set; }

            public ModifiedFileObject(string filePath, GameTarget target,
                Func<string, bool> restoreBasegamefileConfirmationCallback,
                Action notifyRestoringFileCallback,
                Action<object> notifyRestoredCallback)
            {
                this.FilePath = filePath;
                this.target = target;
                this.notifyRestoredCallback = notifyRestoredCallback;
                this.restoreBasegamefileConfirmationCallback = restoreBasegamefileConfirmationCallback;
                this.notifyRestoringCallback = notifyRestoringFileCallback;
                RestoreCommand = new GenericCommand(RestoreFileWrapper, CanRestoreFile);
            }

            public string ModificationSource { get; set; }

            /// <summary>
            /// Uses the basegame file DB to attempt to determine the source mod of this file. This method should be run on a background thread.
            /// </summary>
            public void DetermineSource()
            {
                var fullpath = Path.Combine(target.TargetPath, FilePath);
                if (File.Exists(fullpath))
                {
                    //if (FilePath.Equals(Utilities.Getth, StringComparison.InvariantCultureIgnoreCase)) return; //don't report this file
                    var info = BasegameFileIdentificationService.GetBasegameFileSource(target, fullpath);
                    if (info != null)
                    {
                        ModificationSource = info.source;
                    }
                    // TODO: MAKE LOCAL DB??
#if DEBUG
                    else
                    {
                        ModificationSource = Utilities.CalculateMD5(fullpath);
                    }
#endif
                }
            }

            private void RestoreFileWrapper()
            {
                RestoreFile(false);
            }
            public void RestoreFile(bool batchRestore)
            {
                bool? restore = batchRestore;
                if (!restore.Value) restore = restoreBasegamefileConfirmationCallback?.Invoke(FilePath);
                if (restore.HasValue && restore.Value && internalCanRestoreFile(batchRestore))
                {
                    //Todo: Background thread this maybe?
                    var backupPath = BackupService.GetGameBackupPath(target.Game);
                    var backupFile = Path.Combine(backupPath, FilePath);
                    var targetFile = Path.Combine(target.TargetPath, FilePath);
                    try
                    {
                        Restoring = true;
                        Log.Information(@"Restoring basegame file: " + targetFile);
                        notifyRestoringCallback?.Invoke();
                        var tfi = new FileInfo(targetFile);
                        if (tfi.IsReadOnly)
                        {
                            tfi.IsReadOnly = false;
                        }
                        File.Copy(backupFile, targetFile, true);
                        notifyRestoredCallback?.Invoke(this);
                    }
                    catch (Exception e)
                    {
                        Restoring = false;
                        notifyRestoredCallback?.Invoke(this);
                        Log.Error($@"Error restoring file {targetFile}: " + e.Message);
                    }
                }
            }

            //might need to make this more efficient...
            public string RestoreButtonText => Restoring ? M3L.GetString(M3L.string_restoring) : (CanRestoreFile() ? M3L.GetString(M3L.string_restore) : M3L.GetString(M3L.string_noBackup));

            public bool CanRestoreFile()
            {
                return internalCanRestoreFile(false);
            }

            private bool internalCanRestoreFile(bool batchMode)
            {
                if (Restoring && !batchMode) return false;
                if (checkedForBackupFile) return canRestoreFile;
                var backupPath = BackupService.GetGameBackupPath(target.Game);
                canRestoreFile = backupPath != null && File.Exists(Path.Combine(backupPath, FilePath));
                checkedForBackupFile = true;
                return canRestoreFile;
            }
        }

        public ui.ObservableCollectionExtended<TextureModInstallationInfo> TextureInstallHistory { get; } = new ui.ObservableCollectionExtended<TextureModInstallationInfo>();

        public void PopulateTextureInstallHistory()
        {
            TextureInstallHistory.ReplaceAll(GetTextureModInstallationHistory());
        }

        public List<TextureModInstallationInfo> GetTextureModInstallationHistory()
        {
            var alotInfos = new List<TextureModInstallationInfo>();
            if (Game == MEGame.LELauncher) return alotInfos;
            int startPos = -1;
            while (GetInstalledALOTInfo(startPos, false) != null)
            {
                var info = GetInstalledALOTInfo(startPos, false);
                alotInfos.Add(info);
                startPos = info.MarkerStartPosition;
            }

            return alotInfos;
        }

        internal void StampDebugALOTInfo()
        {
#if DEBUG
            // Writes a MEMI v4 marker
            Random r = new Random();
            TextureModInstallationInfo tmii = new TextureModInstallationInfo((short)(r.Next(10) + 1), 1, 0, 3);
            tmii.MarkerExtendedVersion = TextureModInstallationInfo.LATEST_TEXTURE_MOD_MARKER_VERSION;
            tmii.InstallationTimestamp = DateTime.Now;
            var ran = new Random();
            int i = 10;
            var fileset = new List<TextureModInstallationInfo.InstalledTextureMod>();
            string[] authors = { @"Mgamerz", @"Scottina", @"Sil", @"Audemus", @"Jack", @"ThisGuy", @"KitFisto" };
            string[] modnames =
            {
                @"Spicy Italian Meatballs", @"HD window textures", @"Zebra Stripes", @"Everything is an advertisement",
                @"Todd Howard's Face", @"Kai Lame", @"Downscaled then upscaled", @"1990s phones", @"Are these even texture mods?", @"Dusty countertops",
                @"Dirty shoes", @"Cotton Candy Clothes", @"4K glowy things", @"Christmas in August", @"Cyber warfare", @"Shibuya but it's really hot all the time",
                @"HD Manhole covers", @"Priority Earth but retextured to look like Detroit"
            };
            while (i > 0)
            {
                fileset.Add(new TextureModInstallationInfo.InstalledTextureMod()
                {
                    ModName = modnames.RandomElement(),
                    AuthorName = authors.RandomElement(),
                    ModType = r.Next(6) == 0 ? TextureModInstallationInfo.InstalledTextureMod.InstalledTextureModType.USERFILE : TextureModInstallationInfo.InstalledTextureMod.InstalledTextureModType.MANIFESTFILE
                });
                i--;
            }
            tmii.InstalledTextureMods.AddRange(fileset);
            StampTextureModificationInfo(tmii);
#endif
        }

        /// <summary>
        /// Stamps the TextureModInstallationInfo object into the game. This method only works in Debug mode
        /// as M3 is not a texture installer
        /// </summary>
        /// <param name="tmii"></param>
        public void StampTextureModificationInfo(TextureModInstallationInfo tmii)
        {
#if DEBUG
            var markerPath = getALOTMarkerFilePath();
            try
            {
                using (FileStream fs = new FileStream(markerPath, FileMode.Open, FileAccess.ReadWrite))
                {
                    // MARKER FILE FORMAT
                    // When writing marker, the end of the file is appended with the following data.
                    // Programs that read this marker must read the file IN REVERSE as the MEMI marker
                    // file is appended to prevent data corruption of an existing game file

                    // MEMI v1 - ALOT support
                    // This version only indicated that a texture mod (alot) had been installed
                    // BYTE "MEMI" ASCII

                    // MEMI v2 - MEUITM support (2018):
                    // This version supported versioning of main ALOT and MEUITM. On ME2/3, the MEUITM field would be 0
                    // INT MEUITM VERSION
                    // INT ALOT VERSION (major only)
                    // SHORT MEM VERSION USED
                    // SHORT INSTALLER VERSION USED
                    // <MEMI v1>

                    // MEMI v3 - ALOT subversioning support (2018):
                    // This version split the ALOT int into a short and 2 bytes. The size did not change.
                    // As a result it is not possible to distinguish v2 and v3, and code should just assume v3.
                    // INT MEUITM Version
                    // SHORT ALOT Version
                    // BYTE ALOT Update Version
                    // BYTE ALOT Hotfix Version (not used)
                    // SHORT MEM VERSION USED
                    // SHORT INSTALLER VERSION USED
                    // <MEMI v1>

                    // MEMI v4 - Extended (2020+):
                    // INT MEMI EXTENDED VERSION                         <---------------------------------------------
                    // UNREALSTRING Installer Version Info Extended                                                   |
                    // LONG BINARY DATESTAMP OF STAMPING TIME                                                         |
                    // INT INSTALLED FILE COUNT - ONLY COUNTS TEXTURE MODS, PREINSTALL MODS ARE NOT COUNTED           |
                    // FOR <INSTALLED FILE COUNT>                                                                     |
                    //     BYTE INSTALLED FILE TYPE                                                                   |
                    //         0 = USER FILE                                                                          |
                    //         1 = MANIFEST FILE                                                                      |
                    //     UNREALSTRING Installed File Name (INT LEN (negative for unicode), STR DATA)                |
                    //     [IF MANIFESTFILE] UNREALSTRING Author Name                                                 |
                    // INT MEMI Extended Marker Data Start Offset -----------------------------------------------------
                    // INT MEMI Extended Magic (0xDEADBEEF)
                    // <MEMI v3>

                    fs.SeekEnd();

                    // Write MEMI v4 - Installer full name, date, List of installed files
                    var memiExtensionStartPos = fs.Position;
                    fs.WriteInt32(TextureModInstallationInfo.LATEST_TEXTURE_MOD_MARKER_VERSION); // THIS MUST BE INCREMENTED EVERY TIME MARKER FORMAT CHANGES!! OR IT WILL BREAK OTHER APPS
                    fs.WriteUnrealStringUnicode($@"ME3Tweaks Installer {App.BuildNumber}");
                    fs.WriteInt64(DateTime.Now.ToBinary()); // DATESTAMP
                    fs.WriteInt32(tmii.InstalledTextureMods.Count); // NUMBER OF FILE ENTRIES TO FOLLOW. Count must be here
                    foreach (var fi in tmii.InstalledTextureMods)
                    {
                        fi.WriteToMarker(fs);
                    }
                    fs.WriteInt32((int)memiExtensionStartPos); // Start of memi extended data
                    fs.WriteUInt32(TextureModInstallationInfo.TEXTURE_MOD_MARKER_VERSIONING_MAGIC); // Magic that can be used to tell if this has the v3 extended marker offset preceding it

                    // Write MEMI v3
                    fs.WriteInt32(tmii.MEUITMVER); //meuitm
                    fs.WriteInt16(tmii.ALOTVER); //major
                    fs.WriteByte(tmii.ALOTUPDATEVER); //minor
                    fs.WriteByte(tmii.ALOTHOTFIXVER); //hotfix

                    // MEMI v2 is not used

                    // Write MEMI v1
                    fs.WriteInt16(tmii.ALOT_INSTALLER_VERSION_USED); //Installer Version (Build)
                    fs.WriteInt16(tmii.MEM_VERSION_USED); //Backend MEM version
                    fs.WriteUInt32(MEMI_TAG);
                }

            }
            catch (Exception e)
            {
                Log.Error($@"Error writing debug texture mod installation marker file: {e.Message}");
            }
#endif
        }

        internal void StripALOTInfo()
        {
#if DEBUG
            var markerPath = getALOTMarkerFilePath();

            try
            {
                using (FileStream fs = new FileStream(markerPath, System.IO.FileMode.Open, FileAccess.ReadWrite))
                {
                    fs.SeekEnd();
                    fs.WriteUInt32(1234); //erase memi tag
                }
                Log.Information(@"Changed MEMI Tag for game to 1234.");
            }
            catch (Exception e)
            {
                Log.Error($@"Error stripping debug ALOT marker file for {Game}. {e.Message}");
            }
#endif
        }

        public bool HasALOTOrMEUITM()
        {
            var alotInfo = GetInstalledALOTInfo();
            return alotInfo != null && (alotInfo.ALOTVER > 0 || alotInfo.MEUITMVER > 0);
        }

        private bool RestoringSFAR;

        private void SignalSFARRestore()
        {
            if (SFARRestoreQueue.TryDequeue(out var sfar))
            {
                RestoringSFAR = true;
                sfar.RestoreSFAR(true, SFARRestoreCompleted);
            }
        }

        private void SFARRestoreCompleted()
        {
            RestoringSFAR = false;
            SignalSFARRestore(); //try next item
        }

        /// <summary>
        /// Queues an sfar for restoration.
        /// </summary>
        /// <param name="sfar">SFAR to restore</param>
        /// <param name="batchMode">If this is part of a batch mode, and thus should not show dialogs</param>
        public void RestoreSFAR(SFARObject sfar, bool batchMode)
        {
            sfar.Restoring = true;
            sfar.RestoreButtonContent = M3L.GetString(M3L.string_restoring);
            SFARRestoreQueue.Enqueue(sfar);
            if (!RestoringSFAR)
            {
                SignalSFARRestore();
            }
        }

        // We set it to false on RestoringSFAR because ModifiedSFARFiles will be modified. A race condition may occur.
        public bool HasModifiedMPSFAR() => !RestoringSFAR && ModifiedSFARFiles.Any(x => x.IsMPSFAR);
        public bool HasModifiedSPSFAR() => !RestoringSFAR && ModifiedSFARFiles.Any(x => x.IsSPSFAR);


        public class InstalledExtraFile
        {
            private Action<InstalledExtraFile> notifyDeleted;
            private MEGame game;
            public ICommand DeleteCommand { get; }
            public string DisplayName { get; }
            public enum EFileType
            {
                DLL
            }

            public EFileType FileType { get; }
            public InstalledExtraFile(string filepath, EFileType type, MEGame game, Action<InstalledExtraFile> notifyDeleted = null)
            {
                this.game = game;
                this.notifyDeleted = notifyDeleted;
                FilePath = filepath;
                FileName = Path.GetFileName(filepath);
                FileType = type;
                DisplayName = FileName;
                switch (type)
                {
                    case EFileType.DLL:
                        var info = FileVersionInfo.GetVersionInfo(FilePath);
                        if (!string.IsNullOrWhiteSpace(info.ProductName))
                        {
                            DisplayName += $@" ({info.ProductName.Trim()})";
                        }
                        break;
                }
                DeleteCommand = new GenericCommand(DeleteExtraFile, CanDeleteFile);
            }

            private bool CanDeleteFile() => !Utilities.IsGameRunning(game);

            private void DeleteExtraFile()
            {
                if (!Utilities.IsGameRunning(game))
                {
                    try
                    {
                        File.Delete(FilePath);
                        notifyDeleted?.Invoke(this);
                    }
                    catch (Exception e)
                    {
                        Log.Error($@"Error deleting extra file {FilePath}: {e.Message}");
                    }
                }
            }

            public string FileName { get; set; }

            public string FilePath { get; set; }
        }
        public ui.ObservableCollectionExtended<InstalledExtraFile> ExtraFiles { get; } = new ui.ObservableCollectionExtended<InstalledExtraFile>();
        /// <summary>
        /// Populates list of 'extra' items for the game. This includes things like dlls, and for ME1, config files
        /// </summary>
        public void PopulateExtras()
        {
            try
            {
                var exeDir = M3Directories.GetExecutableDirectory(this);
                var dlls = Directory.GetFiles(exeDir, @"*.dll").Select(x => Path.GetFileName(x));
                var expectedDlls = MEDirectories.VanillaDlls(this.Game);
                var extraDlls = dlls.Except(expectedDlls, StringComparer.InvariantCultureIgnoreCase);

                void notifyExtraFileDeleted(InstalledExtraFile ief)
                {
                    ExtraFiles.Remove(ief);
                }

                ExtraFiles.ReplaceAll(extraDlls.Select(x => new InstalledExtraFile(Path.Combine(exeDir, x), InstalledExtraFile.EFileType.DLL, Game, notifyExtraFileDeleted)));
            }
            catch (Exception e)
            {
                Log.Error($@"Error populating extras for target {TargetPath}: " + e.Message);
            }
        }

        public string NumASIModsInstalledText { get; private set; }
        public void PopulateASIInfo()
        {
            if (Game == MEGame.LELauncher) return;
            var installedASIs = GetInstalledASIs();
            if (installedASIs.Any())
            {
                NumASIModsInstalledText = M3L.GetString(M3L.string_interp_asiStatus, installedASIs.Count);
            }
            else
            {
                NumASIModsInstalledText = M3L.GetString(M3L.string_thisInstallationHasNoASIModsInstalled);
            }
        }

        public string Binkw32StatusText { get; private set; }
        public void PopulateBinkInfo()
        {
            if (Game != MEGame.ME1)
            {
                Binkw32StatusText = Utilities.CheckIfBinkw32ASIIsInstalled(this) ? M3L.GetString(M3L.string_bypassInstalledASIAndDLCModsWillBeAbleToLoad) : M3L.GetString(M3L.string_bypassNotInstalledASIAndDLCModsWillBeUnableToLoad);
            }
            else
            {
                Binkw32StatusText = Utilities.CheckIfBinkw32ASIIsInstalled(this) ? M3L.GetString(M3L.string_bypassInstalledASIModsWillBeAbleToLoad) : M3L.GetString(M3L.string_bypassNotInstalledASIModsWillBeUnableToLoad);
            }
        }

        public List<InstalledASIMod> GetInstalledASIs()
        {
            List<InstalledASIMod> installedASIs = new List<InstalledASIMod>();
            try
            {
                string asiDirectory = M3Directories.GetASIPath(this);
                if (asiDirectory != null && Directory.Exists(TargetPath))
                {
                    if (!Directory.Exists(asiDirectory))
                    {
                        Directory.CreateDirectory(asiDirectory); //Create it, but we don't need it
                        return installedASIs; //It won't have anything in it if we are creating it
                    }

                    var asiFiles = Directory.GetFiles(asiDirectory, @"*.asi");
                    foreach (var asiFile in asiFiles)
                    {
                        var hash = Utilities.CalculateMD5(asiFile);
                        var matchingManifestASI = ASIManager.GetASIVersionByHash(hash, Game);
                        if (matchingManifestASI != null)
                        {
                            installedASIs.Add(new KnownInstalledASIMod(asiFile, hash, Game, matchingManifestASI));
                        }
                        else
                        {
                            installedASIs.Add(new UnknownInstalledASIMod(asiFile, hash, Game));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(@"Error fetching list of installed ASIs: " + e.Message);
            }

            return installedASIs;
        }

        public bool SupportsLODUpdates() => Game is MEGame.ME1 or MEGame.ME2 or MEGame.ME3; // update to remove ME3 in future

    }
}
