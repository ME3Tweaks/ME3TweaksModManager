using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.usercontrols;
using MassEffectModManagerCore.ui;
using Serilog;
using static MassEffectModManagerCore.modmanager.usercontrols.InstallationInformation;

namespace MassEffectModManagerCore.modmanager.objects
{

    public class GameTarget : IEqualityComparer<GameTarget>, INotifyPropertyChanged
    {
        public const uint MEMI_TAG = 0x494D454D;

        private static readonly Color ME1BackgroundColor = Color.FromArgb(80, 181, 181, 181);
        private static readonly Color ME2BackgroundColor = Color.FromArgb(80, 255, 176, 171);
        private static readonly Color ME3BackgroundColor = Color.FromArgb(80, 196, 24, 24);

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
                if (GameSource.Contains(@"Steam")) return @"/images/steam.png";
                if (GameSource.Contains(@"Origin")) return @"/images/origin.png";
                if (GameSource.Contains(@"DVD")) return @"/images/dvd.png";
                return @"/images/unknown.png";
            }
        }
        public bool Supported => GameSource != null;
        public bool IsPolishME1 { get; private set; }
        public Brush BackgroundColor
        {
            get
            {
                if (RegistryActive)
                {
                    switch (Game)
                    {
                        case Mod.MEGame.ME1:
                            return new SolidColorBrush(ME1BackgroundColor);
                        case Mod.MEGame.ME2:
                            return new SolidColorBrush(ME2BackgroundColor);
                        case Mod.MEGame.ME3:
                            return new SolidColorBrush(ME3BackgroundColor);
                    }
                }
                return null;
            }
        }

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
            ReloadGameTarget();
        }

        public void ReloadGameTarget()
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


                    Log.Information(@"Getting game source for target " + TargetPath);
                    var hashCheckResult = VanillaDatabaseService.GetGameSource(this);

                    GameSource = hashCheckResult.result;
                    ExecutableHash = hashCheckResult.hash;
                    if (GameSource == null)
                    {
                        Log.Error(@"Unknown source or illegitimate installation: " + hashCheckResult.hash);
                    }
                    else
                    {
                        Log.Information(@"Source: " + GameSource);
                    }

                    IsPolishME1 = Game == Mod.MEGame.ME1 && File.Exists(Path.Combine(TargetPath, @"BioGame", @"CookedPC", @"Movies", @"niebieska_pl.bik"));
                    if (IsPolishME1)
                    {
                        Log.Information(@"ME1 Polish Edition detected");
                    }

                    if (RegistryActive && Settings.AutoUpdateLODs && oldTMOption != TextureModded)
                    {
                        UpdateLODs();
                    }
                }
                else
                {
                    Log.Error($@"Target is invalid: {TargetPath} does not exist (or is not accessible)");
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
                        short memVersionUsed = fs.ReadInt16();
                        short installerVersionUsed = fs.ReadInt16();
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

                        if (installerVersionUsed >= 10 && installerVersionUsed != perGameFinal4Bytes) //default bytes before 178 MEMI Format
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
            switch (Game)
            {
                case Mod.MEGame.ME1:
                    return Path.Combine(TargetPath, @"BioGame\CookedPC\testVolumeLight_VFX.upk");
                case Mod.MEGame.ME2:
                    return Path.Combine(TargetPath, @"BioGame\CookedPC\BIOC_Materials.pcc");
                case Mod.MEGame.ME3:
                    return Path.Combine(TargetPath, @"BIOGame\CookedPCConsole\adv_combat_tutorial_xbox_D_Int.afc");
                default:
                    throw new Exception(@"Unknown game to find ALOT marker for!");
            }
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
            void failedCallback(string file)
            {
                //todo: Filter out SFARs?
                if (file.EndsWith(@".sfar"))
                {
                    ModifiedSFARFiles.Add(new SFARObject(file, this, restoreSfarConfirmationCallback, notifySFARRestoringCallback, notifyRestoredCallback));
                    return;
                }
                ModifiedBasegameFiles.Add(new ModifiedFileObject(file.Substring(TargetPath.Length + 1), this,
                    restoreBasegamefileConfirmationCallback,
                    notifyFileRestoringCallback,
                    notifyRestoredCallback));
            }
            VanillaDatabaseService.ValidateTargetAgainstVanilla(this, failedCallback);
        }

        /// <summary>
        /// Call this when the modified object lists are no longer necessary
        /// </summary>
        public void DumpModifiedFilesFromMemory()
        {
            ModifiedBasegameFiles.ClearEx();
            ModifiedSFARFiles.ClearEx();
            foreach (var uiInstalledDlcMod in UIInstalledDLCMods)
            {
                uiInstalledDlcMod.ClearHandlers();
            }
            UIInstalledDLCMods.ClearEx();
        }

        public ObservableCollectionExtended<InstalledDLCMod> UIInstalledDLCMods { get; } = new ObservableCollectionExtended<InstalledDLCMod>();

        public void PopulateDLCMods(bool includeDisabled, Func<InstalledDLCMod, bool> deleteConfirmationCallback = null, Action notifyDeleted = null, bool modNamePrefersTPMI = false)
        {
            UIInstalledDLCMods.ClearEx();
            var dlcDir = MEDirectories.DLCPath(this);
            var installedMods = MEDirectories.GetInstalledDLC(this, includeDisabled).Where(x => !MEDirectories.OfficialDLC(Game).Contains(x.TrimStart('x'), StringComparer.InvariantCultureIgnoreCase)).Select(x => new InstalledDLCMod(Path.Combine(dlcDir, x), Game, deleteConfirmationCallback, notifyDeleted, modNamePrefersTPMI)).ToList();
            UIInstalledDLCMods.AddRange(installedMods.OrderBy(x => x.ModName));
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

                    var unpackedFiles = Directory.GetFiles(DLCDirectory, @"*", SearchOption.AllDirectories);
                    // not TOC is due to bug in autotoc
                    if (unpackedFiles.Any(x =>
                        Path.GetExtension(x) == @".bin" &&
                        Path.GetFileNameWithoutExtension(x) != @"PCConsoleTOC") && !Unpacked) Inconsistent = true;
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
                    NamedBackgroundWorker bw = new NamedBackgroundWorker(@"RestoreSFARThread");
                    bw.DoWork += (a, b) =>
                    {
                        var backupFile = Path.Combine(Utilities.GetGameBackupPath(target.Game), FilePath);
                        var targetFile = Path.Combine(target.TargetPath, FilePath);
                        Restoring = true;
                        Log.Information($@"Restoring SFAR from backup: {backupFile} {targetFile}");
                        XCopy.Copy(backupFile, targetFile, true, true,
                            (o, pce) =>
                            {
                                RestoreButtonContent = M3L.GetString(M3L.string_interp_restoringXpercent,
                                    pce.ProgressPercentage.ToString());
                            });
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

                        Utilities.DeleteEmptySubdirectories(DLCDirectory);
                        RestoreButtonContent = M3L.GetString(M3L.string_restored);
                    };
                    bw.RunWorkerCompleted += (a, b) =>
                    {
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
                    bw.RunWorkerAsync();
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
                var backupPath = Utilities.GetGameBackupPath(target.Game);
                canRestoreSfar = backupPath != null && File.Exists(Path.Combine(backupPath, FilePath));
                checkedForBackupFile = true;
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
                    var backupPath = Utilities.GetGameBackupPath(target.Game);
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
                var backupPath = Utilities.GetGameBackupPath(target.Game);
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
    }
}
