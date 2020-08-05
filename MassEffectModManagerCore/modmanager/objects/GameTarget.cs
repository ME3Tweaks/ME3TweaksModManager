using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.asi;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.memoryanalyzer;
using MassEffectModManagerCore.modmanager.usercontrols;
using MassEffectModManagerCore.ui;
using Serilog;
using static MassEffectModManagerCore.modmanager.usercontrols.InstallationInformation;
using Path = System.IO.Path;

namespace MassEffectModManagerCore.modmanager.objects
{

    public class GameTarget : IEqualityComparer<GameTarget>, INotifyPropertyChanged
    {
        public const uint MEMI_TAG = 0x494D454D;

        public event PropertyChangedEventHandler PropertyChanged;

        public Mod.MEGame Game { get; }
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
        public GameTarget(Mod.MEGame game, string targetRootPath, bool currentRegistryActive, bool isCustomOption = false)
        {
            this.Game = game;
            this.RegistryActive = currentRegistryActive;
            this.IsCustomOption = isCustomOption;
            this.TargetPath = targetRootPath.TrimEnd('\\');
            MemoryAnalyzer.AddTrackedMemoryItem($@"{game} GameTarget {TargetPath} - IsCustomOption: {isCustomOption}", new WeakReference(this));
            ReloadGameTarget();
        }

        public void ReloadGameTarget(bool lodUpdateAndLogging = true, bool forceLodUpdate = false)
        {
            if (Game != Mod.MEGame.Unknown && !IsCustomOption)
            {
                if (Directory.Exists(TargetPath))
                {
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

                    CLog.Information(@"Getting game source for target " + TargetPath, lodUpdateAndLogging);
                    var hashCheckResult = VanillaDatabaseService.GetGameSource(this);

                    GameSource = hashCheckResult.result;
                    ExecutableHash = hashCheckResult.hash;
                    if (GameSource == null)
                    {
                        CLog.Error(@"Unknown source or illegitimate installation: " + hashCheckResult.hash, lodUpdateAndLogging);

                    }
                    else
                    {
                        if (GameSource.Contains(@"Origin") && Game == Mod.MEGame.ME3)
                        {
                            // Check for steam
                            if (Directory.Exists(Path.Combine(TargetPath, @"__overlay")))
                            {
                                GameSource += @" (Steam version)";
                            }
                        }
                        CLog.Information(@"Source: " + GameSource, lodUpdateAndLogging);
                    }

                    IsPolishME1 = Game == Mod.MEGame.ME1 && File.Exists(Path.Combine(TargetPath, @"BioGame", @"CookedPC", @"Movies", @"niebieska_pl.bik"));
                    if (IsPolishME1)
                    {
                        CLog.Information(@"ME1 Polish Edition detected", lodUpdateAndLogging);
                    }

                    if (RegistryActive && Settings.AutoUpdateLODs && oldTMOption != TextureModded && (lodUpdateAndLogging || forceLodUpdate))
                    {
                        UpdateLODs();
                    }
                }
                else
                {
                    Log.Error($@"Target is invalid: {TargetPath} does not exist (or is not accessible)");
                    IsValid = false; //set to false if target becomes invalid
                }
            }
        }

        public void UpdateLODs(bool me12k = false)
        {
            if (!TextureModded)
            {
                Utilities.SetLODs(this, false, false, false);
            }
            else
            {
                if (Game == Mod.MEGame.ME1)
                {
                    if (MEUITMInstalled)
                    {
                        //detect soft shadows/meuitm
                        var branchingPCFCommon = Path.Combine(TargetPath, @"Engine", @"Shaders", @"BranchingPCFCommon.usf");
                        if (File.Exists(branchingPCFCommon))
                        {
                            var md5 = Utilities.CalculateMD5(branchingPCFCommon);
                            Utilities.SetLODs(this, true, me12k, md5 == @"10db76cb98c21d3e90d4f0ffed55d424");
                            return;
                        }
                    }

                    //set default HQ lod
                    Utilities.SetLODs(this, true, me12k, false);
                }
                else
                {
                    //me2/3
                    Utilities.SetLODs(this, true, false, false);
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

        public ALOTVersionInfo GetInstalledALOTInfo()
        {
            string gamePath = getALOTMarkerFilePath();
            if (gamePath != null && File.Exists(gamePath))
            {
                try
                {
                    using FileStream fs = new FileStream(gamePath, System.IO.FileMode.Open, FileAccess.Read);
                    fs.SeekEnd();
                    long endPos = fs.Position;
                    fs.Position = endPos - 4;
                    uint memi = fs.ReadUInt32();

                    if (memi == MEMI_TAG)
                    {
                        //ALOT has been installed
                        fs.Position = endPos - 8;
                        short installerVersionUsed = fs.ReadInt16();
                        short memVersionUsed = fs.ReadInt16();
                        fs.Position -= 4; //roll back so we can read this whole thing as 4 bytes
                        int preMemi4Bytes = fs.ReadInt32();
                        int perGameFinal4Bytes = -20;
                        switch (Game)
                        {
                            case Mod.MEGame.ME1:
                                perGameFinal4Bytes = 0;
                                break;
                            case Mod.MEGame.ME2:
                                perGameFinal4Bytes = 4352;
                                break;
                            case Mod.MEGame.ME3:
                                perGameFinal4Bytes = 16777472;
                                break;
                        }

                        if (preMemi4Bytes != perGameFinal4Bytes) //default bytes before 178 MEMI Format
                        {
                            fs.Position = endPos - 12;
                            short ALOTVER = fs.ReadInt16();
                            byte ALOTUPDATEVER = (byte)fs.ReadByte();
                            byte ALOTHOTFIXVER = (byte)fs.ReadByte();

                            //unused for now
                            fs.Position = endPos - 16;
                            int MEUITMVER = fs.ReadInt32();

                            return new ALOTVersionInfo(ALOTVER, ALOTUPDATEVER, ALOTHOTFIXVER, MEUITMVER, installerVersionUsed, memVersionUsed);
                        }
                        else
                        {
                            return new ALOTVersionInfo(0, 0, 0, 0, 0, 0); //MEMI tag but no info we know of
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error($@"Error reading ALOT marker file for {Game}. ALOT Info will be returned as null (nothing installed). " + e.Message);
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
            return MEDirectories.ALOTMarkerPath(this);
        }

        public ObservableCollectionExtended<ModifiedFileObject> ModifiedBasegameFiles { get; } = new ObservableCollectionExtended<ModifiedFileObject>();
        public ObservableCollectionExtended<SFARObject> ModifiedSFARFiles { get; } = new ObservableCollectionExtended<SFARObject>();
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

                if (file == getALOTMarkerFilePath())
                {
                    return; //Do not report this file as modified or user will desync game state with texture state
                }
                modifiedFiles.Add(file);
            }
            VanillaDatabaseService.ValidateTargetAgainstVanilla(this, failedCallback);

            List<string> inconsistentDLC = new List<string>();
            VanillaDatabaseService.ValidateTargetDLCConsistency(this, x => inconsistentDLC.Add(x));

            modifiedSfars.AddRange(inconsistentDLC.Select(x => Path.Combine(x, "CookedPCConsole", "Default.sfar")));
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

        public ObservableCollectionExtended<InstalledDLCMod> UIInstalledDLCMods { get; } = new ObservableCollectionExtended<InstalledDLCMod>();

        public void PopulateDLCMods(bool includeDisabled, Func<InstalledDLCMod, bool> deleteConfirmationCallback = null, Action notifyDeleted = null, bool modNamePrefersTPMI = false)
        {
            var dlcDir = MEDirectories.DLCPath(this);
            var installedMods = MEDirectories.GetInstalledDLC(this, includeDisabled).Where(x => !MEDirectories.OfficialDLC(Game).Contains(x.TrimStart('x'), StringComparer.InvariantCultureIgnoreCase));
            //Must run on UI thread
            Application.Current.Dispatcher.Invoke(delegate
            {
                UIInstalledDLCMods.ClearEx();
                UIInstalledDLCMods.AddRange(installedMods.Select(x => new InstalledDLCMod(Path.Combine(dlcDir, x), Game, deleteConfirmationCallback, notifyDeleted, modNamePrefersTPMI)).ToList().OrderBy(x => x.ModName));
            });
        }

        public bool IsTargetWritable()
        {
            return Utilities.IsDirectoryWritable(TargetPath) && Utilities.IsDirectoryWritable(Path.Combine(TargetPath, @"Binaries"));
        }

        public string ALOTStatusString
        {
            get
            {
                if (TextureModded)
                {
                    return M3L.GetString(M3L.string_interp_ui_alotInstalledVersion, ALOTVersion);
                }
                else
                {
                    return M3L.GetString(M3L.string_ui_alotNotInstalled);
                }
            }
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
                case Mod.MEGame.ME1:
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
                case Mod.MEGame.ME2:
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
                case Mod.MEGame.ME3:
                    validationFiles = new[]
                    {
                        Path.Combine(TargetPath, @"Binaries", @"win32", @"MassEffect3.exe"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPCConsole", @"Textures.tfc"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPCConsole", @"Startup.pcc"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPCConsole", @"Coalesced.bin"),
                        Path.Combine(TargetPath, @"BioGame", @"Patches", @"PCConsole", @"Patch_001.sfar"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPCConsole", @"Textures.tfc"),
                        Path.Combine(TargetPath, @"BioGame", @"CookedPCConsole", @"citwrd_rp1_bailey_m_D_Int.afc")
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
                        var filesInSfarDir = Directory.EnumerateFiles(DLCDirectory, "*.*", SearchOption.AllDirectories).ToList();
                        if (filesInSfarDir.Any(d =>
                            !Path.GetFileName(d).Equals("PCConsoleTOC.bin", StringComparison.InvariantCultureIgnoreCase) && //pcconsoletoc will be produced for all folders even with autotoc asi even if its not needed
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
                        var backupFile = Path.Combine(BackupService.GetGameBackupPath(target.Game), FilePath);
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
                        if (!VanillaDatabaseService.IsFileVanilla(target, targetFile, false))
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
                    };
                    nbw.RunWorkerCompleted += (a, b) =>
                    {
                        if (b.Error != null)
                        {
                            Log.Error($@"Exception occured in {nbw.Name} thread: {b.Error.Message}");
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

            private Action restoreCompletedCallback, startingRestoreCallback;
            private readonly GameTarget target;
            private readonly bool Unpacked;

            public string DLCDirectory { get; }

            public event PropertyChangedEventHandler PropertyChanged;

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

            public event PropertyChangedEventHandler PropertyChanged;

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

        internal void StampDebugALOTInfo()
        {
#if DEBUG
            var markerPAth = getALOTMarkerFilePath();
            try
            {
                using (FileStream fs = new FileStream(markerPAth, System.IO.FileMode.Open, FileAccess.ReadWrite))
                {
                    fs.SeekEnd();
                    fs.WriteInt32(0); //meuitm
                    fs.WriteUInt16(0); //major
                    fs.WriteByte(0); //minor
                    fs.WriteByte(0); //hotfix
                                     //fs.WriteByte(0); //unused
                    fs.WriteInt32(100); //installer version
                    fs.WriteUInt32(MEMI_TAG);
                }
                Log.Information(@"Stamped ALOT for game. Installer 100, v 6.8, MEUITM 4");
            }
            catch (Exception e)
            {
                Log.Error($@"Error writing debug ALOT marker file for {Game}. {e.Message}");
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
                    fs.Position -= 4;
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

        public bool HasModifiedMPSFAR() => ModifiedSFARFiles.Any(x => x.IsMPSFAR);
        public bool HasModifiedSPSFAR() => ModifiedSFARFiles.Any(x => x.IsSPSFAR);


        public class InstalledExtraFile
        {
            private Action<InstalledExtraFile> notifyDeleted;
            private Mod.MEGame game;
            public ICommand DeleteCommand { get; }
            public string DisplayName { get; }
            public enum EFileType
            {
                DLL
            }

            public EFileType FileType { get; }
            public InstalledExtraFile(string filepath, EFileType type, Mod.MEGame game, Action<InstalledExtraFile> notifyDeleted = null)
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
        public ObservableCollectionExtended<InstalledExtraFile> ExtraFiles { get; } = new ObservableCollectionExtended<InstalledExtraFile>();
        /// <summary>
        /// Populates list of 'extra' items for the game. This includes things like dlls, and for ME1, config files
        /// </summary>
        public void PopulateExtras()
        {
            try
            {
                var exeDir = MEDirectories.ExecutableDirectory(this);
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
            var asi = new ASIGame(this);
            var installedASIs = asi.GetInstalledASIMods(Game);
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
            if (Game != Mod.MEGame.ME1)
            {
                Binkw32StatusText = Utilities.CheckIfBinkw32ASIIsInstalled(this) ? M3L.GetString(M3L.string_bypassInstalledASIAndDLCModsWillBeAbleToLoad) : M3L.GetString(M3L.string_bypassNotInstalledASIAndDLCModsWillBeUnableToLoad);
            }
            else
            {
                Binkw32StatusText = Utilities.CheckIfBinkw32ASIIsInstalled(this) ? M3L.GetString(M3L.string_bypassInstalledASIModsWillBeAbleToLoad) : M3L.GetString(M3L.string_bypassNotInstalledASIModsWillBeUnableToLoad);
            }
        }
    }
}
