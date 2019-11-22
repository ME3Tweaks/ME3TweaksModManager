using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;

using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.helpers;
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
        public string GameSource { get; }
        public bool Supported => GameSource != null;
        public bool IsPolishME1 { get; }
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

        public bool Selectable { get; internal set; } = true;
        public string ALOTVersion { get; private set; }
        public bool IsCustomOption { get; set; } = false;
        public GameTarget(Mod.MEGame game, string target, bool currentRegistryActive, bool isCustomOption = false)
        {
            this.Game = game;
            this.RegistryActive = currentRegistryActive;
            this.IsCustomOption = isCustomOption;
            this.TargetPath = target.TrimEnd('\\');
            if (game != Mod.MEGame.Unknown && !IsCustomOption)
            {
                var alotInfo = GetInstalledALOTInfo();
                if (alotInfo != null)
                {
                    ALOTInstalled = true;
                    ALOTVersion = alotInfo.ToString();
                }
                Log.Information("Getting game source for target " + TargetPath);
                var hashCheckResult = VanillaDatabaseService.GetGameSource(this);

                GameSource = hashCheckResult.result;
                if (GameSource == null)
                {
                    Log.Error("Unknown source or illegitimate installation: " + hashCheckResult.hash);
                }
                else
                {
                    Log.Information("Source: " + GameSource);
                }
                IsPolishME1 = game == Mod.MEGame.ME1 && File.Exists(Path.Combine(target, "BioGame", "CookedPC", "Movies", "niebieska_pl.bik"));
                if (IsPolishME1)
                {
                    Log.Information("ME1 Polish Edition detected");
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

        public bool ALOTInstalled { get; private set; }

        public ALOTVersionInfo GetInstalledALOTInfo()
        {
            string gamePath = getALOTMarkerFilePath();
            if (gamePath != null && File.Exists(gamePath))
            {
                try
                {
                    using (FileStream fs = new FileStream(gamePath, System.IO.FileMode.Open, FileAccess.Read))
                    {
                        fs.SeekEnd();
                        long endPos = fs.Position;
                        fs.Position = endPos - 4;
                        uint memi = fs.ReadUInt32();

                        if (memi == MEMI_TAG)
                        {
                            //ALOT has been installed
                            fs.Position = endPos - 8;
                            int installerVersionUsed = fs.ReadInt32();
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

                                return new ALOTVersionInfo(ALOTVER, ALOTUPDATEVER, ALOTHOTFIXVER, MEUITMVER);
                            }
                            else
                            {
                                return new ALOTVersionInfo(0, 0, 0, 0); //MEMI tag but no info we know of
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error($"Error reading ALOT marker file for {Game}. ALOT Info will be returned as null (nothing installed). " + e.Message);
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
                    throw new Exception("Unknown game to find ALOT marker for!");
            }
        }

        public ObservableCollectionExtended<ModifiedFileObject> ModifiedBasegameFiles { get; } = new ObservableCollectionExtended<ModifiedFileObject>();
        public ObservableCollectionExtended<SFARObject> ModifiedSFARFiles { get; } = new ObservableCollectionExtended<SFARObject>();

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
                if (file.EndsWith(".sfar"))
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

        public ObservableCollectionExtended<InstalledDLCMod> UIInstalledDLCMods { get; } = new ObservableCollectionExtended<InstalledDLCMod>();

        public void PopulateDLCMods(Func<InstalledDLCMod, bool> deleteConfirmationCallback, Action notifyDeleted)
        {
            UIInstalledDLCMods.ClearEx();
            var dlcDir = MEDirectories.DLCPath(this);
            var installedMods = MEDirectories.GetInstalledDLC(this).Where(x => !MEDirectories.OfficialDLC(Game).Contains(x, StringComparer.InvariantCultureIgnoreCase)).Select(x => new InstalledDLCMod(Path.Combine(dlcDir, x), Game, deleteConfirmationCallback, notifyDeleted)).ToList();
            UIInstalledDLCMods.AddRange(installedMods);
        }

        public bool IsTargetWritable()
        {
            return Utilities.IsDirectoryWritable(TargetPath) && Utilities.IsDirectoryWritable(Path.Combine(TargetPath, "Binaries"));
        }

        public string ALOTStatusString
        {
            get
            {
                if (ALOTInstalled)
                {
                    return "A Lot Of Textures (ALOT) is installed\nVersion " + ALOTVersion;
                }
                else
                {
                    return "A Lot Of Textures (ALOT)\nNot Installed";
                }
            }
        }

        public bool IsValid { get; set; }

        /// <summary>
        /// Validates a game directory by checking for multiple things that should be present in a working game.
        /// </summary>
        /// <param name="target">Game target to check</param>
        /// <returns>String of failure reason, null if OK</returns>
        public string ValidateTarget()
        {
            if (!Selectable)
            {
                return null;
            }
            IsValid = false; //set to invalid at first/s
            switch (Game)
            {
                case Mod.MEGame.ME1:
                    if (!File.Exists(Path.Combine(TargetPath, "Binaries", "MassEffect.exe"))) return "Invalid game directory: Game EXE not found";
                    if (!File.Exists(Path.Combine(TargetPath, "BioGame", "CookedPC", "Maps", "EntryMenu.SFM"))) return "Invalid game directory: Entrymenu.sfm not found";
                    if (!File.Exists(Path.Combine(TargetPath, "BioGame", "CookedPC", "BIOC_Base.u"))) return "Invalid game directory: BIOC_Base.u not found";
                    if (!File.Exists(Path.Combine(TargetPath, "BioGame", "CookedPC", "Packages", "Textures", "BIOA_GLO_00_A_Opening_FlyBy_T.upk"))) return "Invalid game directory: BIOA_GLO_00_A_Opening_FlyBy_T.upk not found";
                    if (!File.Exists(Path.Combine(TargetPath, "BioGame", "CookedPC", "Maps", "WAR", "LAY", "BIOA_WAR20_05_LAY.SFM"))) return "Invalid game directory: Entrymenu.sfm not found";
                    if (!File.Exists(Path.Combine(TargetPath, "BioGame", "CookedPC", "Movies", "MEvisionSEQ3.bik"))) return "Invalid game directory: MEvisionSEQ3.bik not found";
                    break;
                case Mod.MEGame.ME2:
                    if (!File.Exists(Path.Combine(TargetPath, "Binaries", "MassEffect2.exe"))) return "Invalid game directory: Game EXE not found";
                    if (!File.Exists(Path.Combine(TargetPath, "BioGame", "CookedPC", "BioA_BchLmL.pcc"))) return "Invalid game directory: BioA_BchLmL.pcc not found";
                    if (!File.Exists(Path.Combine(TargetPath, "BioGame", "Config", "PC", "Cooked", "Coalesced.ini"))) return "Invalid game directory: Coalesced.ini not found";
                    if (!File.Exists(Path.Combine(TargetPath, "BioGame", "CookedPC", "Wwise_Jack_Loy_Music.afc"))) return "Invalid game directory: Wwise_Jack_Loy_Music.afc not found";
                    if (!File.Exists(Path.Combine(TargetPath, "BioGame", "CookedPC", "WwiseAudio.pcc"))) return "Invalid game directory: WwiseAudio.pcc not found";
                    if (!File.Exists(Path.Combine(TargetPath, "BioGame", "Movies", "Crit03_CollectArrive_Part2_1.bik"))) return "Invalid game directory: Crit03_CollectArrive_Part2_1.bik not found";
                    break;
                case Mod.MEGame.ME3:
                    if (!File.Exists(Path.Combine(TargetPath, "Binaries", "win32", "MassEffect3.exe"))) return "Invalid game directory: Game EXE not found";
                    if (!File.Exists(Path.Combine(TargetPath, "BioGame", "CookedPCConsole", "Textures.tfc"))) return "Invalid game directory: Textures.tfc not found";
                    if (!File.Exists(Path.Combine(TargetPath, "BioGame", "CookedPCConsole", "Startup.pcc"))) return "Invalid game directory: Startup.pcc not found";
                    if (!File.Exists(Path.Combine(TargetPath, "BioGame", "CookedPCConsole", "Coalesced.bin"))) return "Invalid game directory: Coalesced.bin not found";
                    if (!File.Exists(Path.Combine(TargetPath, "BioGame", "Patches", "PCConsole", "Patch_001.sfar"))) return "Invalid game directory: Patch_001.sfar not found";
                    if (!File.Exists(Path.Combine(TargetPath, "BioGame", "CookedPCConsole", "Textures.tfc"))) return "Invalid game directory: Textures.tfc not found";
                    if (!File.Exists(Path.Combine(TargetPath, "BioGame", "CookedPCConsole", "citwrd_rp1_bailey_m_D_Int.afc"))) return "Invalid game directory: citwrd_rp1_bailey_m_D_Int.afc not found";
                    break;
            }
            if (File.Exists(Path.Combine(TargetPath, "cmm_vanilla"))) return "Invalid target: Marked as backup protected with cmm_vanilla file";

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
            return Equals((GameTarget) obj);
        }

        public string RemoveTargetTooltipText
        {
            get
            {
                if (RegistryActive) return "Cannot remove a target that is currently the active boot target.\nChange the active target in the main window dropdown to remove allow removing this target";
                return "Removes this target from Mod Manager's list of available targets for modding";
            }
        }

        public override int GetHashCode()
        {
            return (TargetPath != null ? TargetPath.GetHashCode() : 0);
        }

        public class SFARObject : INotifyPropertyChanged
        {
            public SFARObject(string file, GameTarget target, Func<string, bool> restoreSFARCallback, Action startingRestoreCallback, Action<object> notifyNoLongerModifiedCallback)
            {
                RestoreConfirmationCallback = restoreSFARCallback;
                IsModified = true;
                this.startingRestoreCallback = startingRestoreCallback;
                this.notifyNoLongerModified = notifyNoLongerModifiedCallback;
                this.target = target;
                Unpacked = new FileInfo(file).Length == 32;
                DLCDirectory = Directory.GetParent(Directory.GetParent(file).FullName).FullName;
                FilePath = file.Substring(target.TargetPath.Length + 1);
                if (Path.GetFileName(file) == "Patch_001.sfar")
                {
                    UIString = "TestPatch";
                }
                else
                {
                    ME3Directory.OfficialDLCNames.TryGetValue(Path.GetFileName(Directory.GetParent(Directory.GetParent(file).FullName).FullName), out var name);
                    UIString = name;
                    if (Unpacked)
                    {
                        UIString += " - Unpacked";
                    }
                    var unpackedFiles = Directory.GetFiles(DLCDirectory, "*", SearchOption.AllDirectories);
                    if (unpackedFiles.Any(x => Path.GetExtension(x) == ".bin") && !Unpacked) Inconsistent = true;
                }
                RestoreCommand = new GenericCommand(RestoreSFARWrapper, CanRestoreSFAR);
            }

            public bool RevalidateIsModified(bool notify = true)
            {
                bool _isModified = IsModified;
                IsModified = !VanillaDatabaseService.IsFileVanilla(target, Path.Combine(target.TargetPath, FilePath));
                if (!IsModified && _isModified && notify)
                {
                    Debug.WriteLine("Notifying that " + FilePath + " is no longer modified.");
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

            public void RestoreSFAR(bool batchRestore)
            {
                bool? restore = batchRestore;
                if (!restore.Value) restore = RestoreConfirmationCallback?.Invoke(FilePath);
                if (restore.HasValue && restore.Value)
                {
                    //Todo: Background thread this maybe?
                    NamedBackgroundWorker bw = new NamedBackgroundWorker("RestoreSFARThread");
                    bw.DoWork += (a, b) =>
                    {
                        var backupFile = Path.Combine(Utilities.GetGameBackupPath(target.Game), FilePath);
                        var targetFile = Path.Combine(target.TargetPath, FilePath);
                        Restoring = true;
                        Log.Information("Restoring SFAR from backup: " + backupFile + " => " + targetFile);
                        XCopy.Copy(backupFile, targetFile, true, true, (o, pce) => { RestoreButtonContent = $"Restoring {pce.ProgressPercentage}%"; });
                        var unpackedFiles = Directory.GetFiles(DLCDirectory, "*", SearchOption.AllDirectories);
                        RestoreButtonContent = $"Cleaning up";
                        foreach (var file in unpackedFiles)
                        {
                            if (!file.EndsWith(".sfar"))
                            {
                                Log.Information("Deleting unpacked file: " + file);
                                File.Delete(file);
                            }
                        }
                        Utilities.DeleteEmptySubdirectories(DLCDirectory);
                        RestoreButtonContent = "Restored";
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
                    };
                    startingRestoreCallback?.Invoke();
                    bw.RunWorkerAsync();
                }
            }


            public static bool HasUnpackedFiles(string sfarFile)
            {
                var unpackedFiles = Directory.GetFiles(Directory.GetParent(Directory.GetParent(sfarFile).FullName).FullName, "*", SearchOption.AllDirectories);
                return (unpackedFiles.Any(x => Path.GetExtension(x) == ".bin"));
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

            public string RestoreButtonContent { get; set; } = "Restore";

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
            public bool Restoring { get;  set; }

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
                if (restore.HasValue && restore.Value)
                {
                    //Todo: Background thread this maybe?
                    var backupFile = Path.Combine(Utilities.GetGameBackupPath(target.Game), FilePath);
                    var targetFile = Path.Combine(target.TargetPath, FilePath);
                    try
                    {
                        Restoring = true;
                        Log.Information("Restoring basegame file: " + targetFile);
                        notifyRestoringCallback?.Invoke();
                        File.Copy(backupFile, targetFile, true);
                        notifyRestoredCallback?.Invoke(this);
                    }
                    catch (Exception e)
                    {
                        Restoring = false;
                        notifyRestoredCallback?.Invoke(this);
                        Log.Error($"Error restoring file {targetFile}: " + e.Message);
                    }
                }
            }

            public string RestoreButtonText => Restoring ? "Restoring" : (CanRestoreFile() ? "Restore" : "No backup");

            private bool CanRestoreFile()
            {
                if (Restoring) return false;
                if (checkedForBackupFile) return canRestoreFile;
                var backupPath = Utilities.GetGameBackupPath(target.Game);
                canRestoreFile = backupPath != null && File.Exists(Path.Combine(backupPath, FilePath));
                checkedForBackupFile = true;
                return canRestoreFile;
            }
        }
    }
}
