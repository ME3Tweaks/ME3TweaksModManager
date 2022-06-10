using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using System.Xml.XPath;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Objects;
using ME3TweaksCore.Services.Backup;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.gameini;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.ui;
using Microsoft.AppCenter.Analytics;
using PropertyChanged;
using Path = System.IO.Path;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ConsoleKeybindingPanel.xaml
    /// </summary>
    public partial class ConsoleKeybindingPanel : MMBusyPanelBase
    {
        public bool IsListeningForKey { get; set; }

        public void OnIsListeningForKeyChanged()
        {
            foreach (var game in Games)
            {
                game.NotifyKeyListeningState(IsListeningForKey);
            }
        }

        public ObservableCollectionExtended<KeybindingGame> Games { get; } = new();

        #region Key texts

        #endregion
        public ConsoleKeybindingPanel()
        {
            DataContext = this;
            LoadCommands();
        }

        /// <summary>
        /// Game-specific instance the keybinding logic for UI binding
        /// </summary>
        [AddINotifyPropertyChangedInterface]
        public class KeybindingGame
        {
            public void NotifyKeyListeningState(bool isListening)
            {
                SharedIsListeningForKey = isListening;
            }

            public bool SharedIsListeningForKey { get; set; }
            public MEGame Game { get; }
            public GameTargetWPF SelectedTarget { get; set; }
            public ICommand DefaultCommand { get; }
            public ICommand ChangeMiniKeyCommand { get; }
            public ICommand ChangeFullKeyCommand { get; }

            public string FullConsoleKeyText { get; private set; }
            public string MiniConsoleKeyText { get; private set; }
            public ObservableCollectionExtended<GameTargetWPF> Targets { get; } = new();
            private bool OperationInProgress;

            public string WhereKeysAreDefinedText { get; }
            public KeybindingGame(MEGame game, IEnumerable<GameTargetWPF> targets, Action<Action<string>, string> beginListeningForKey)
            {
                BeginListeningForKeyDelegate = beginListeningForKey;
                Game = game;
                WhereKeysAreDefinedText = M3L.GetString(M3L.string_interp_gameConsoleKeysAreDefinedPerGame, Game.ToGameName());
                DefaultCommand = new GenericCommand(ResetKeybinds, CanResetKeybinds);
                ChangeMiniKeyCommand = new GenericCommand(SetMiniKey, CanResetKeybinds);
                ChangeFullKeyCommand = new GenericCommand(SetFullKey, CanResetKeybinds);
                Targets.AddRange(targets);
                SelectedTarget = Targets.FirstOrDefault();
                LoadKeys();
            }

            public Action<Action<string>, string> BeginListeningForKeyDelegate { get; }

            public string GameName => Game.ToGameName();

            private void SetFullKey()
            {
                // We do not have a 1:1 compiler for ME3/LE3
                // ME1 uses ini that doesn't get backed up
                // ME2 uses ini that is 1:1
                // LE1/LE2 use coalesced ini that is 1:1
                if (Game.IsGame3() && !BackupService.GetBackupStatus(Game).BackedUp)
                {
                    var result = M3L.ShowDialog(Application.Current.MainWindow, $"There is no backup of {Game} available. If you continue, you will be unable to take a game backup as the game will be modified. Are you sure you want to do this?", "Backup warning", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.No;
                    if (!result)
                        return; // Don't proceed
                }

                void keyPressed(string key)
                {
                    if (key != null)
                        SetKeyWithThread(consoleKeyStr: key);
                }
                BeginListeningForKeyDelegate?.Invoke(keyPressed, M3L.GetString(M3L.string_interp_gameNameFullConsole, Game.ToGameName()));
            }

            private void SetMiniKey()
            {
                // We do not have a 1:1 compiler for ME3/LE3
                // ME1 uses ini that doesn't get backed up
                // ME2 uses ini that is 1:1
                // LE1/LE2 use coalesced ini that is 1:1
                if (Game.IsGame3() && !BackupService.GetBackupStatus(Game).BackedUp)
                {
                    var result = M3L.ShowDialog(Application.Current.MainWindow, $"There is no backup of {Game} available. If you continue, you will be unable to take a game backup as the game will be modified. Are you sure you want to do this?", "Backup warning", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.No;
                    if (!result)
                        return; // Don't proceed
                }

                void keyPressed(string key)
                {
                    if (key != null)
                        SetKeyWithThread(typeKeyStr: key);
                }
                BeginListeningForKeyDelegate?.Invoke(keyPressed, M3L.GetString(M3L.string_interp_gameNameMiniConsole, Game.ToGameName()));
            }

            private void ResetKeybinds()
            {
                // We do not have a 1:1 compiler for ME3/LE3
                // ME1 uses ini that doesn't get backed up
                // ME2 uses ini that is 1:1
                // LE1/LE2 use coalesced ini that is 1:1
                if (Game.IsGame3() && !BackupService.GetBackupStatus(Game).BackedUp)
                {
                    var result = M3L.ShowDialog(Application.Current.MainWindow, $"There is no backup of {Game} available. If you continue, you will be unable to take a game backup as the game will be modified. Are you sure you want to do this?", "Backup warning", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.No;
                    if (!result)
                        return; // Don't proceed
                }
                SetKeyWithThread(@"Tilde", @"Tab", true);
            }

            private bool CanResetKeybinds() => !SharedIsListeningForKey && !OperationInProgress;


            #region LOAD KEYS
            private void LoadKeys()
            {
                Task.Run(() =>
                {
                    if (Game == MEGame.ME1)
                        LoadME1Keys();
                    else
                        LoadEmbeddedKeys();
                });
            }

            private void LoadEmbeddedKeys()
            {
                if (SelectedTarget == null)
                {
                    FullConsoleKeyText = M3L.GetString(M3L.string_noInstallsOfGameManagedByModManager);
                    MiniConsoleKeyText = "";
                    return; // Can't load keys
                }

                if (Game == MEGame.ME2)
                {
                    #region ME2

                    ME2Coalesced me2c = null;
                    try
                    {
                        me2c = ME2Coalesced.OpenFromTarget(SelectedTarget, true);
                    }
                    catch (Exception e)
                    {
                        Application.Current.Dispatcher.Invoke(delegate { M3L.ShowDialog(null, M3L.GetString(M3L.string_interp_cannotOpenMassEffect2CoalescediniMessage, e.Message), M3L.GetString(M3L.string_errorReadingCoalescedini), MessageBoxButton.OK, MessageBoxImage.Error); });
                        MiniConsoleKeyText = M3L.GetString(M3L.string_errorReadingCoalescedini);
                        MiniConsoleKeyText = M3L.GetString(M3L.string_errorReadingCoalescedini);
                        return;
                    }

                    var bioinput = me2c.Inis.FirstOrDefault(x => Path.GetFileName(x.Key).Equals(@"BioInput.ini", StringComparison.InvariantCultureIgnoreCase));
                    var engineConsole = bioinput.Value.GetSection(@"Engine.Console");
                    if (engineConsole != null)
                    {
                        var consoleKey = engineConsole.GetValue(@"ConsoleKey");
                        if (consoleKey == null)
                        {
                            FullConsoleKeyText = M3L.GetString(M3L.string_fullConsoleNotBoundToAKey);
                        }
                        else
                        {
                            FullConsoleKeyText = M3L.GetString(M3L.string_interp_fullConsoleBoundToX, consoleKey.Value);
                        }

                        var typeKey = engineConsole.GetValue(@"TypeKey");
                        if (typeKey == null)
                        {
                            MiniConsoleKeyText = M3L.GetString(M3L.string_miniConsoleNotBoundToAKey);
                        }
                        else
                        {
                            MiniConsoleKeyText = M3L.GetString(M3L.string_interp_miniConsoleBoundToX, typeKey.Value);
                        }
                    }

                    #endregion
                }
                else if (Game.IsGame3())
                {
                    #region Game 3

                    try
                    {
                        var coalPath = Path.Combine(SelectedTarget.TargetPath, @"BioGame", @"CookedPCConsole", @"Coalesced.bin");
                        Dictionary<string, string> coalescedFilemapping = null;
                        if (File.Exists(coalPath))
                        {
                            using FileStream fs = new FileStream(coalPath, FileMode.Open);
                            coalescedFilemapping = CoalescedConverter.DecompileGame3ToMemory(fs);
                        }
                        else
                        {
                            M3Log.Error(
                                @"Could not get file data for coalesced chunk BASEGAME as Coalesced.bin file was missing");
                            return;
                        }

                        var bioinputText = coalescedFilemapping[@"BioInput.xml"];
                        var coalFileDoc = XDocument.Parse(bioinputText);
                        var consolekey = coalFileDoc.XPathSelectElement(
                            @"/CoalesceAsset/Sections/Section[@name='engine.console']/Property[@name='consolekey']");
                        var typekey =
                            coalFileDoc.XPathSelectElement(
                                @"/CoalesceAsset/Sections/Section[@name='engine.console']/Property[@name='typekey']");
                        if (consolekey != null)
                        {
                            FullConsoleKeyText = M3L.GetString(M3L.string_interp_fullConsoleBoundToX, consolekey.Value);
                        }
                        else
                        {
                            FullConsoleKeyText = M3L.GetString(M3L.string_fullConsoleNotBoundToAKey);
                        }

                        if (typekey != null)
                        {
                            MiniConsoleKeyText = M3L.GetString(M3L.string_interp_miniConsoleBoundToX, typekey.Value);
                        }
                        else
                        {
                            MiniConsoleKeyText = M3L.GetString(M3L.string_miniConsoleNotBoundToAKey);
                        }
                    }
                    catch (Exception e)
                    {
                        M3Log.Error(@"Error reading keybinds: " + e.Message);
                        M3L.ShowDialog(null, M3L.GetString(M3L.string_interp_cannotReadME3Keybinds, e.Message), M3L.GetString(M3L.string_errorReadingKeybinds), MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    #endregion
                }
                else if (Game is MEGame.LE1 or MEGame.LE2)
                {
                    #region LE1/LE2

                    // We use INT as base for keybinder
                    var coalPath = Path.Combine(SelectedTarget.TargetPath, @"BioGame", @"CookedPCConsole", @"Coalesced_INT.bin");
                    Dictionary<string, LegendaryExplorerCore.Misc.DuplicatingIni> coalescedFilemapping = null;
                    if (File.Exists(coalPath))
                    {
                        using FileStream fs = new FileStream(coalPath, FileMode.Open);
                        coalescedFilemapping = CoalescedConverter.DecompileLE1LE2ToMemory(fs, @"Coalesced_INT.pcc");
                    }
                    else
                    {
                        M3Log.Error(
                            @"Could not get file data for coalesced chunk BASEGAME as Coalesced.bin file was missing");
                        return;
                    }

                    var bioInputIni = coalescedFilemapping[@"BIOInput.ini"];
                    var engineSection = bioInputIni.GetSection(@"Engine.Console");
                    if (engineSection != null)
                    {
                        var consolekey = engineSection.GetValue(@"ConsoleKey");
                        if (consolekey != null)
                        {
                            FullConsoleKeyText = M3L.GetString(M3L.string_interp_fullConsoleBoundToX, consolekey.Value);
                        }
                        else
                        {
                            FullConsoleKeyText = M3L.GetString(M3L.string_fullConsoleNotBoundToAKey);
                        }

                        var typekey = engineSection.GetValue(@"TypeKey");
                        if (typekey != null)
                        {
                            MiniConsoleKeyText = M3L.GetString(M3L.string_interp_miniConsoleBoundToX, typekey.Value);
                        }
                        else
                        {
                            MiniConsoleKeyText = M3L.GetString(M3L.string_miniConsoleNotBoundToAKey);
                        }

                        #endregion
                    }
                }
            }

            // KEY LOADERS
            private void LoadME1Keys()
            {
                var iniFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"BioWare", @"Mass Effect", @"Config", @"BIOInput.ini");
                if (File.Exists(iniFile))
                {
                    var ini = DuplicatingIni.LoadIni(iniFile);
                    var engineConsole = ini.Sections.FirstOrDefault(x => x.Header == @"Engine.Console");
                    if (engineConsole != null)
                    {
                        var consoleKey = engineConsole.Entries.FirstOrDefault(x => x.Key == @"ConsoleKey");
                        if (consoleKey == null)
                        {
                            FullConsoleKeyText = M3L.GetString(M3L.string_fullConsoleNotBoundToAKey);
                        }
                        else
                        {
                            FullConsoleKeyText = M3L.GetString(M3L.string_interp_fullConsoleBoundToX, consoleKey.Value);
                        }

                        var typeKey = engineConsole.Entries.FirstOrDefault(x => x.Key == @"TypeKey");
                        if (typeKey == null)
                        {
                            MiniConsoleKeyText = M3L.GetString(M3L.string_miniConsoleNotBoundToAKey);
                        }
                        else
                        {
                            MiniConsoleKeyText = M3L.GetString(M3L.string_interp_miniConsoleBoundToX, typeKey.Value);
                        }
                    }
                }
                else
                {
                    //HasME1Install = false;
                    FullConsoleKeyText = M3L.GetString(M3L.string_bioInputiniFileIsMissing);
                    MiniConsoleKeyText = M3L.GetString(M3L.string_runGameToGenerateFile);
                }
            }

            #endregion

            #region SET KEYS
            private static void SetIniBasedKeybinds(DuplicatingIni bioinput, string consoleKeyStr, string typeKeyStr, bool wipeTypeKey = false)
            {
                var engineConsole = bioinput.GetSection(@"Engine.Console");
                if (engineConsole != null)
                {
                    if (consoleKeyStr != null)
                    {
                        var consoleKey = engineConsole.GetValue(@"ConsoleKey");
                        if (consoleKey != null)
                        {
                            consoleKey.Value = consoleKeyStr;
                        }
                        else
                        {
                            engineConsole.Entries.Add(new DuplicatingIni.IniEntry(@"ConsoleKey", typeKeyStr));
                        }
                    }
                    var typeKey = engineConsole.GetValue(@"TypeKey");
                    if (wipeTypeKey && typeKey != null)
                    {
                        engineConsole.Entries.Remove(typeKey);
                    }
                    if (typeKeyStr != null)
                    {
                        if (typeKey != null)
                        {
                            typeKey.Value = typeKeyStr;
                        }
                        else
                        {
                            //Create Typekey
                            engineConsole.Entries.Add(new DuplicatingIni.IniEntry(@"TypeKey", typeKeyStr));
                        }
                    }
                }
            }

            private void SetME1ConsoleKeybinds(string consoleKeyStr, string typeKeyStr, bool wipeTypeKey = false)
            {
                var iniFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"BioWare", @"Mass Effect", @"Config", @"BIOInput.ini");
                if (File.Exists(iniFile))
                {
                    var ini = DuplicatingIni.LoadIni(iniFile);
                    SetIniBasedKeybinds(ini, consoleKeyStr, typeKeyStr, wipeTypeKey);
                    var wasReadOnly = M3Utilities.ClearReadOnly(iniFile);
                    File.WriteAllText(iniFile, ini.ToString());
                    if (wasReadOnly)
                    {
                        M3Utilities.SetReadOnly(iniFile);
                    }
                }
            }
            private void SetKeyWithThread(string consoleKeyStr = null, string typeKeyStr = null, bool wipeTypeKey = false)
            {
                OperationInProgress = true;
                FullConsoleKeyText = M3L.GetString(M3L.string_updatingKeybindsPleaseWait);
                MiniConsoleKeyText = "";
                NamedBackgroundWorker nbw = new NamedBackgroundWorker($@"{Game}-ConsoleKeySetterThread");
                nbw.DoWork += (a, b) =>
                {
                    if (Game == MEGame.ME1) SetME1ConsoleKeybinds(consoleKeyStr, typeKeyStr, wipeTypeKey);
                    if (Game == MEGame.ME2) SetME2ConsoleKeybinds(consoleKeyStr, typeKeyStr);
                    if (Game.IsGame3()) SetME3ConsoleKeybinds(consoleKeyStr, typeKeyStr);
                    if (Game is MEGame.LE1 or MEGame.LE2) SetLE1LE2ConsoleKeybinds(consoleKeyStr, typeKeyStr);

                    Analytics.TrackEvent($@"Set {Game} Console Keys", new Dictionary<string, string>()
                    {
                        {@"Full Console Key", consoleKeyStr },
                        {@"Mini Console Key", typeKeyStr }
                    });
                    LoadKeys();
                };
                nbw.RunWorkerCompleted += (a, b) =>
                {
                    OperationInProgress = false;
                    CommandManager.InvalidateRequerySuggested();
                };
                nbw.RunWorkerAsync();
            }

            private void SetLE1LE2ConsoleKeybinds(string consoleKeyStr, string typeKeyStr)
            {
                var cookedPath = M3Directories.GetCookedPath(SelectedTarget);
                var langs = GameLanguage.GetLanguagesForGame(Game);
                foreach (var lang in langs)
                {
                    var fname = Path.Combine(cookedPath, $@"Coalesced_{lang.FileCode}.bin");
                    if (File.Exists(fname))
                    {
                        using var fs = File.OpenRead(fname);
                        var decomp = CoalescedConverter.DecompileLE1LE2ToMemory(fs, "");
                        fs.Dispose();
                        SetIniBasedKeybinds(decomp[@"BIOInput.ini"], consoleKeyStr, typeKeyStr);
                        CoalescedConverter.CompileLE1LE2FromMemory(decomp).WriteToFile(fname);
                    }
                }
            }

            private void SetME2ConsoleKeybinds(string consoleKeyStr, string typeKeyStr)
            {
                var me2c = ME2Coalesced.OpenFromTarget(SelectedTarget);
                var bioinput = me2c.Inis.FirstOrDefault(x => Path.GetFileName(x.Key).Equals(@"BioInput.ini", StringComparison.InvariantCultureIgnoreCase));
                SetIniBasedKeybinds(bioinput.Value, consoleKeyStr, typeKeyStr);
                me2c.Serialize();
            }

            /// <summary>
            /// Sets the TypeKey and ConsoleKey values for an ME3 game target. This method is synchronous.
            /// </summary>
            /// <param name="target"></param>
            /// <param name="consoleKeyStr"></param>
            /// <param name="typeKeyStr"></param>
            private void SetME3ConsoleKeybinds(string consoleKeyStr = null, string typeKeyStr = null)
            {
                var coalPath = Path.Combine(SelectedTarget.TargetPath, @"BIOGame", @"CookedPCConsole", @"Coalesced.bin");
                Dictionary<string, string> coalescedFilemapping = null;
                if (File.Exists(coalPath))
                {
                    using FileStream fs = new FileStream(coalPath, FileMode.Open);
                    coalescedFilemapping = CoalescedConverter.DecompileGame3ToMemory(fs);
                }
                else
                {
                    M3Log.Error(@"Could not get file data for Coalesced.bin, file was missing");
                    return;
                }

                var bioinputText = coalescedFilemapping[@"BioInput.xml"];
                var coalFileDoc = XDocument.Parse(bioinputText);
                var consolekey = coalFileDoc.XPathSelectElement(@"/CoalesceAsset/Sections/Section[@name='engine.console']/Property[@name='consolekey']");
                var typekey = coalFileDoc.XPathSelectElement(@"/CoalesceAsset/Sections/Section[@name='engine.console']/Property[@name='typekey']");
                if (consolekey != null && consoleKeyStr != null)
                {
                    consolekey.Value = consoleKeyStr;
                }
                else
                {
                    var consoleElement = coalFileDoc.XPathSelectElement(@"/CoalesceAsset/Sections/Section[@name='engine.console']");
                    var consoleKeyElement = new XElement(@"Property", consoleKeyStr);
                    consoleKeyElement.SetAttributeValue(@"name", @"consolekey");
                    consoleElement.Add(consoleKeyElement);
                }

                if (typekey != null && typeKeyStr != null)
                {
                    typekey.Value = typeKeyStr;
                }
                else
                {
                    var consoleElement = coalFileDoc.XPathSelectElement(@"/CoalesceAsset/Sections/Section[@name='engine.console']");
                    var consoleKeyElement = new XElement(@"Property", typeKeyStr);
                    consoleKeyElement.SetAttributeValue(@"name", @"typekey");
                    consoleElement.Add(consoleKeyElement);
                }

                coalescedFilemapping[@"BioInput.xml"] = coalFileDoc.ToString();
                var recompiled = CoalescedConverter.CompileFromMemory(coalescedFilemapping);
                recompiled.WriteToFile(coalPath);
                AutoTOC.RunTOCOnGameTarget(SelectedTarget);
            }

            #endregion
        }

        public ICommand CloseCommand { get; set; }
        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(() => OnClosing(DataEventArgs.Empty), NotListeningForKey);
        }

        //private void SetME3MiniKeyCallback(string unrealKeyStr)
        //{
        //    SetME3KeyWithThread(SelectedME3Target, typeKeyStr: unrealKeyStr);
        //}
        //private void ResetME3Keys()
        //{
        //    SetME3KeyWithThread(SelectedME3Target, consoleKeyStr: @"Tilde", typeKeyStr: @"Tab");
        //}


        private void ListenForKey(Action<string> setKeyCallback, string listeningForKeyName)
        {
            KeyBeingAssigned = listeningForKeyName;
            IsListeningForKey = true;
            OnKeyPressed = setKeyCallback;
        }

        private bool NotListeningForKey() => !IsListeningForKey;

        public string KeyBeingAssigned { get; set; }

        public Action<string> OnKeyPressed;


        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (IsListeningForKey)
            {
                if (e.Key == Key.Escape)
                {
                    // Can't bind escape.
                    IsListeningForKey = false;
                    OnKeyPressed?.Invoke(null);
                    OnKeyPressed = null;
                    return;
                }

                // Handle key if possible
                var unrealString = ConvertToUnrealKeyString(e.Key == Key.System ? e.SystemKey : e.Key);
                if (unrealString != null)
                {
                    IsListeningForKey = false;
                    OnKeyPressed?.Invoke(unrealString);
                    OnKeyPressed = null;
                }
            }
            else if (e.Key == Key.Escape)
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
            foreach (var game in Enum.GetValues<MEGame>())
            {
                if ((game.IsOTGame() || game.IsLEGame()) && game.IsEnabledGeneration())
                {
                    Games.Add(new KeybindingGame(game, mainwindow.InstallationTargets.Where(x => x.Game == game), ListenForKey));
                }
            }
        }

        private void CancelKeyAssignment_Click(object sender, RoutedEventArgs e)
        {
            IsListeningForKey = false;
            OnKeyPressed = null;
        }

        [Localizable(false)]
        private string ConvertToUnrealKeyString(Key key)
        {
            //Oh god...
            switch (key.ToString())
            {
                case @"F1":
                    return @"F1";
                case @"F2":
                    return @"F2";
                case @"F3":
                    return @"F3";
                case @"F4":
                    return @"F4";
                case @"F5":
                    return @"F5";
                case @"F6":
                    return @"F6";
                case @"F7":
                    return @"F7";
                case @"F8":
                    return @"F8";
                case @"F9":
                    return @"F9";
                case @"F10":
                    return @"F10";
                case @"F11":
                    return @"F11";
                case @"F12":
                    return @"F12";
                case @"A":
                    return @"A";
                case @"B":
                    return @"B";
                case @"C":
                    return @"C";
                case @"D":
                    return @"D";
                case @"E":
                    return @"E";
                case @"F":
                    return @"F";
                case @"G":
                    return @"G";
                case @"H":
                    return @"H";
                case @"I":
                    return @"I";
                case @"J":
                    return @"J";
                case @"K":
                    return @"K";
                case @"L":
                    return @"L";
                case @"M":
                    return @"M";
                case @"N":
                    return @"N";
                case @"O":
                    return @"O";
                case @"P":
                    return @"P";
                case @"Q":
                    return @"Q";
                case @"R":
                    return @"R";
                case @"S":
                    return @"S";
                case @"T":
                    return @"T";
                case @"U":
                    return @"U";
                case @"V":
                    return @"V";
                case @"W":
                    return @"W";
                case @"X":
                    return @"X";
                case @"Y":
                    return @"Y";
                case @"Z":
                    return @"Z";
                case @"Escape":
                    return @"Escape";
                case @"Tab":
                    return @"Tab";
                case @"Tilde":
                    return @"Tilde";
                case @"ScrollLock":
                    return @"ScrollLock";
                case @"Pause":
                    return @"Pause";
                case @"D1":
                    return @"one";
                case @"D2":
                    return @"two";
                case @"D3":
                    return @"three";
                case @"D4":
                    return @"four";
                case @"D5":
                    return @"five";
                case @"D6":
                    return @"six";
                case @"D7":
                    return @"seven";
                case @"D8":
                    return @"eight";
                case @"D9":
                    return @"nine";
                case @"D0":
                    return @"zero";
                case @"Underscore":
                    return @"Underscore";
                case @"Equals":
                    return @"Equals";
                case @"Backslash":
                    return @"Backslash";
                case @"LeftBracket":
                    return @"LeftBracket";
                case @"RightBracket":
                    return @"RightBracket";
                case @"Enter":
                case @"Return":
                    return @"Enter";
                case @"CapsLock":
                    return @"CapsLock";
                case @"Semicolon":
                    return @"Semicolon";
                case @"Quote":
                    return @"Quote";
                //case @"LeftShift":
                //    return @"LeftShift";
                case @"Comma":
                    return @"Comma";
                case @"Period":
                    return @"Period";
                case @"Slash":
                    return @"Slash";
                //case @"RightShift":
                //    return @"RightShift";
                case @"LeftControl":
                    return @"LeftControl";
                case @"LeftAlt":
                    return @"LeftAlt";
                case @"Space":
                    return @"SpaceBar";
                case @"RightAlt":
                    return @"RightAlt";
                case @"RightControl":
                    return @"RightControl";
                case @"Left":
                    return @"Left";
                case @"Up":
                    return @"Up";
                case @"Down":
                    return @"Down";
                case @"Right":
                    return @"Right";
                case @"Home":
                    return @"Home";
                case @"End":
                    return @"End";
                case @"Insert":
                    return @"Insert";
                case @"PageUp":
                    return @"PageUp";
                case @"Delete":
                    return @"Delete";
                case @"PageDown":
                    return @"PageDown";
                case @"NumLock": //why?
                    return @"NumLock";
                case @"Divide": //needs to be numpad
                    return @"Divide";
                case @"Multiply": //needs to be numpad
                    return @"Multiply";
                case @"Subtract": //needs to be numpad
                    return @"Subtract";
                case @"Add": //needs to be numpad
                    return @"Add";
                case @"NumPadOne":
                    return @"NumPadOne";
                case @"NumPadTwo":
                    return @"NumPadTwo";
                case @"NumPadThree":
                    return @"NumPadThree";
                case @"NumPadFour":
                    return @"NumPadFour";
                case @"NumPadFive":
                    return @"NumPadFive";
                case @"NumPadSix":
                    return @"NumPadSix";
                case @"NumPadSeven":
                    return @"NumPadSeven";
                case @"NumPadEight":
                    return @"NumPadEight";
                case @"NumPadNine":
                    return @"NumPadNine";
                case @"NumPadZero":
                    return @"NumPadZero";
                case @"Decimal":
                    return @"Decimal";
                case @"Oem3":
                    return "Tilde"; //?
                default:
                    Debug.WriteLine(@"Unknown key: " + key);
                    break;
            }

            return null; //Not usable
        }
    }
}
