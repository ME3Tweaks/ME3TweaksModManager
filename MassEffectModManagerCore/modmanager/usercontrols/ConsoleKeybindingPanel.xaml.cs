using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;
using System.Xml.XPath;
using MassEffectModManagerCore.modmanager.gameini;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using Microsoft.AppCenter.Analytics;
using Serilog;
using Path = System.IO.Path;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ConsoleKeybindingPanel.xaml
    /// </summary>
    public partial class ConsoleKeybindingPanel : MMBusyPanelBase
    {
        public bool OperationInProgress { get; set; }
        public bool IsListeningForKey { get; set; }

        #region Key texts
        public string ME1FullConsoleKeyText { get; set; }
        public string ME1MiniConsoleKeyText { get; set; }
        public string ME2FullConsoleKeyText { get; set; }
        public string ME2MiniConsoleKeyText { get; set; }
        public string ME3FullConsoleKeyText { get; set; }
        public string ME3MiniConsoleKeyText { get; set; }
        #endregion
        public ConsoleKeybindingPanel()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }

        public ICommand ChangeME1MiniCommand { get; set; }
        public ICommand ChangeME1FullCommand { get; set; }
        public ICommand DefaultME1KeysCommand { get; set; }
        public ICommand ChangeME2MiniCommand { get; set; }
        public ICommand ChangeME2FullCommand { get; set; }
        public ICommand DefaultME2KeysCommand { get; set; }
        public ICommand ChangeME3MiniCommand { get; set; }
        public ICommand ChangeME3FullCommand { get; set; }
        public ICommand DefaultME3KeysCommand { get; set; }
        public ICommand CloseCommand { get; set; }
        private void LoadCommands()
        {
            ChangeME1FullCommand = new GenericCommand(SetME1FullKey, NotListeningForKey);
            ChangeME1MiniCommand = new GenericCommand(SetME1MiniKey, NotListeningForKey);
            DefaultME1KeysCommand = new GenericCommand(ResetME1Keys, NotListeningForKey);
            ChangeME2FullCommand = new GenericCommand(SetME2FullKey, NotListeningForKey);
            ChangeME2MiniCommand = new GenericCommand(SetME2MiniKey, NotListeningForKey);
            DefaultME2KeysCommand = new GenericCommand(ResetME2Keys, NotListeningForKey);
            ChangeME3FullCommand = new GenericCommand(SetME3FullKey, NotListeningForKey);
            ChangeME3MiniCommand = new GenericCommand(SetME3MiniKey, NotListeningForKey);
            DefaultME3KeysCommand = new GenericCommand(ResetME3Keys, NotListeningForKey);
            CloseCommand = new GenericCommand(() => OnClosing(DataEventArgs.Empty), NotListeningForKey);
        }

        #region Set Mini/Full/Reset and callbacks

        private void SetME3MiniKey()
        {
            KeyBeingAssigned = M3L.GetString(M3L.string_massEffect3MiniConsole);
            IsListeningForKey = true;
            OnKeyPressed = SetME3MiniKeyCallback;
        }

        private void SetME3FullKey()
        {
            KeyBeingAssigned = M3L.GetString(M3L.string_massEffect3FullConsole);
            IsListeningForKey = true;
            OnKeyPressed = SetME3FullKeyCallback;
        }

        private void SetME3FullKeyCallback(string consoleKeyStr)
        {
            SetME3KeyWithThread(SelectedME3Target, consoleKeyStr: consoleKeyStr);
        }

        private void SetME3MiniKeyCallback(string unrealKeyStr)
        {
            SetME3KeyWithThread(SelectedME3Target, typeKeyStr: unrealKeyStr);
        }
        private void ResetME3Keys()
        {
            SetME3KeyWithThread(SelectedME3Target, consoleKeyStr: @"Tilde", typeKeyStr: @"Tab");
        }

        private void SetME1KeyWithThread(string consoleKeyStr = null, string typeKeyStr = null, bool wipeTypeKey = false)
        {
            OperationInProgress = true;
            ME1FullConsoleKeyText = M3L.GetString(M3L.string_updatingKeybindsPleaseWait);
            ME1MiniConsoleKeyText = "";
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"ME2-ConsoleKeySetterThread");
            nbw.DoWork += (a, b) =>
            {
                SetME1ConsoleKeybinds(consoleKeyStr, typeKeyStr, wipeTypeKey);
                LoadME1Keys();
                Analytics.TrackEvent(@"Set ME1 Console Keys", new Dictionary<string, string>()
                {
                    {@"Full Console Key", consoleKeyStr },
                    {@"Mini Console Key", typeKeyStr }
                });
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
                }
                OperationInProgress = false;
                CommandManager.InvalidateRequerySuggested();
            };
            nbw.RunWorkerAsync();
        }

        private void SetME1ConsoleKeybinds(string consoleKeyStr, string typeKeyStr, bool wipeTypeKey = false)
        {
            var iniFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"BioWare", @"Mass Effect", @"Config", @"BIOInput.ini");
            if (File.Exists(iniFile))
            {
                var ini = DuplicatingIni.LoadIni(iniFile);
                SetIniBasedKeybinds(ini, consoleKeyStr, typeKeyStr, wipeTypeKey);
                var wasReadOnly = Utilities.ClearReadOnly(iniFile);
                File.WriteAllText(iniFile, ini.ToString());
                if (wasReadOnly)
                {
                    Utilities.SetReadOnly(iniFile);
                }
            }
        }

        private void SetME2KeyWithThread(GameTarget target, string consoleKeyStr = null, string typeKeyStr = null)
        {
            OperationInProgress = true;
            ME2FullConsoleKeyText = M3L.GetString(M3L.string_updatingKeybindsPleaseWait);
            ME2MiniConsoleKeyText = "";
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"ME2-ConsoleKeySetterThread");
            nbw.DoWork += (a, b) =>
            {
                SetME2ConsoleKeybinds(SelectedME2Target, consoleKeyStr, typeKeyStr);
                LoadME2Keys(SelectedME2Target);
                Analytics.TrackEvent(@"Set ME2 Console Keys", new Dictionary<string, string>()
                {
                    {@"Full Console Key", consoleKeyStr },
                    {@"Mini Console Key", typeKeyStr }
                });
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
                }
                OperationInProgress = false;
                CommandManager.InvalidateRequerySuggested();
            };
            nbw.RunWorkerAsync();
        }

        public static void SetME2ConsoleKeybinds(GameTarget target, string consoleKeyStr, string typeKeyStr)
        {
            if (target.Game != Mod.MEGame.ME2) throw new Exception(@"Cannot set ME2 keybind for non-ME2 target");
            var me2c = ME2Coalesced.OpenFromTarget(target);
            var bioinput = me2c.Inis.FirstOrDefault(x => Path.GetFileName(x.Key).Equals(@"BioInput.ini", StringComparison.InvariantCultureIgnoreCase));
            SetIniBasedKeybinds(bioinput.Value, consoleKeyStr, typeKeyStr);
            me2c.Serialize();
        }

        private static void SetIniBasedKeybinds(DuplicatingIni bioinput, string consoleKeyStr, string typeKeyStr, bool wipeTypeKey = false)
        {
            var engineConsole = bioinput.Sections.FirstOrDefault(x => x.Header == @"Engine.Console");
            if (engineConsole != null)
            {
                if (consoleKeyStr != null)
                {
                    var consoleKey = engineConsole.Entries.FirstOrDefault(x => x.Key == @"ConsoleKey");
                    if (consoleKey != null)
                    {
                        consoleKey.Value = consoleKeyStr;
                    }
                    else
                    {
                        engineConsole.Entries.Add(new DuplicatingIni.IniEntry(@"ConsoleKey=" + typeKeyStr));
                    }
                }
                var typeKey = engineConsole.Entries.FirstOrDefault(x => x.Key == @"TypeKey");
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
                        engineConsole.Entries.Add(new DuplicatingIni.IniEntry(@"TypeKey=" + typeKeyStr));
                    }
                }
            }
        }

        private void SetME3KeyWithThread(GameTarget target, string consoleKeyStr = null, string typeKeyStr = null)
        {
            OperationInProgress = true;
            ME3FullConsoleKeyText = M3L.GetString(M3L.string_updatingKeybindsPleaseWait);
            ME3MiniConsoleKeyText = "";
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"ME3-ConsoleKeySetterThread");
            nbw.DoWork += (a, b) =>
            {
                SetME3ConsoleKeybinds(SelectedME3Target, consoleKeyStr, typeKeyStr);
                LoadME3Keys(SelectedME3Target);
                Analytics.TrackEvent(@"Set ME3 Console Keys", new Dictionary<string, string>()
                {
                    {@"Full Console Key", consoleKeyStr },
                    {@"Mini Console Key", typeKeyStr }
                });
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
                }
                OperationInProgress = false;
                CommandManager.InvalidateRequerySuggested();
            };
            nbw.RunWorkerAsync();
        }

        /// <summary>
        /// Sets the TypeKey and ConsoleKey values for an ME3 game target. This method is synchronous.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="consoleKeyStr"></param>
        /// <param name="typeKeyStr"></param>
        public static void SetME3ConsoleKeybinds(GameTarget target, string consoleKeyStr = null, string typeKeyStr = null)
        {
            var coalPath = Path.Combine(target.TargetPath, @"BioGame", @"CookedPCConsole", @"Coalesced.bin");
            Dictionary<string, string> coalescedFilemapping = null;
            if (File.Exists(coalPath))
            {
                using FileStream fs = new FileStream(coalPath, FileMode.Open);
                coalescedFilemapping = MassEffect3.Coalesce.Converter.DecompileToMemory(fs);
            }
            else
            {
                Log.Error(@"Could not get file data for coalesced chunk BASEGAME as Coalesced.bin file was missing");
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
            var recompiled = MassEffect3.Coalesce.Converter.CompileFromMemory(coalescedFilemapping);
            recompiled.WriteToFile(coalPath);
            AutoTOC.RunTOCOnGameTarget(target);
        }

        private void SetME2MiniKey()
        {
            KeyBeingAssigned = M3L.GetString(M3L.string_massEffect2MiniConsole);
            IsListeningForKey = true;
            OnKeyPressed = SetME2MiniKeyCallback;
        }

        private void SetME2FullKey()
        {
            KeyBeingAssigned = M3L.GetString(M3L.string_massEffect2FullConsole);
            IsListeningForKey = true;
            OnKeyPressed = SetME2FullKeyCallback;
        }

        private void SetME2FullKeyCallback(string unrealKeyStr)
        {
            SetME2KeyWithThread(SelectedME2Target, unrealKeyStr, null);
        }

        private void SetME2MiniKeyCallback(string unrealKeyStr)
        {
            SetME2KeyWithThread(SelectedME2Target, null, unrealKeyStr);
        }

        private void ResetME2Keys()
        {
            SetME2KeyWithThread(SelectedME2Target, @"Tilde", @"Tab");
        }


        private void SetME1FullKey()
        {
            KeyBeingAssigned = M3L.GetString(M3L.string_massEffectFullConsole);
            IsListeningForKey = true;
            OnKeyPressed = SetME1FullKeyCallback;
        }

        private void SetME1MiniKey()
        {
            KeyBeingAssigned = M3L.GetString(M3L.string_massEffectMiniConsole);
            IsListeningForKey = true;
            OnKeyPressed = SetME1MiniKeyCallback;
        }

        private void SetME1FullKeyCallback(string unrealKeyStr)
        {
            SetME1KeyWithThread(unrealKeyStr, null);
        }

        private void SetME1MiniKeyCallback(string unrealKeyStr)
        {
            SetME1KeyWithThread(null, unrealKeyStr);

        }

        private void ResetME1Keys()
        {
            SetME1KeyWithThread(@"Tilde", null, true);
        }

        #endregion
        private bool NotListeningForKey() => !IsListeningForKey && !OperationInProgress;
        public bool UIEnabled => !IsListeningForKey && !OperationInProgress;

        public string KeyBeingAssigned { get; set; }


        public Action<string> OnKeyPressed;

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (OperationInProgress) return; //do not handle key
            if (IsListeningForKey)
            {
                var unrealString = ConvertToUnrealKeyString(e.Key == Key.System ? e.SystemKey : e.Key);
                if (unrealString != null)
                {
                    IsListeningForKey = false;
                    OnKeyPressed?.Invoke(unrealString);
                    OnKeyPressed = null;
                    //e.Handled = true;
                }
            }
            else if (e.Key == Key.Escape)
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        public GameTarget SelectedME2Target { get; set; }
        public GameTarget SelectedME3Target { get; set; }
        public ObservableCollectionExtended<GameTarget> ME2Targets { get; } = new ObservableCollectionExtended<GameTarget>();
        public ObservableCollectionExtended<GameTarget> ME3Targets { get; } = new ObservableCollectionExtended<GameTarget>();

        public override void OnPanelVisible()
        {
            ME2Targets.ReplaceAll(mainwindow.InstallationTargets.Where(x => x.Game == Mod.MEGame.ME2));
            ME3Targets.ReplaceAll(mainwindow.InstallationTargets.Where(x => x.Game == Mod.MEGame.ME3));
            SelectedME2Target = ME2Targets.FirstOrDefault();
            SelectedME3Target = ME3Targets.FirstOrDefault();
            LoadME1Keys();
        }

        public bool HasME1Install { get; set; } = true;
        private void LoadME1Keys()
        {
            if (mainwindow.InstallationTargets.Where(x => x.Selectable).Any())
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
                            ME1FullConsoleKeyText = M3L.GetString(M3L.string_fullConsoleNotBoundToAKey);
                        }
                        else
                        {
                            ME1FullConsoleKeyText = M3L.GetString(M3L.string_interp_fullConsoleBoundToX, consoleKey.Value);
                        }

                        var typeKey = engineConsole.Entries.FirstOrDefault(x => x.Key == @"TypeKey");
                        if (typeKey == null)
                        {
                            ME1MiniConsoleKeyText = M3L.GetString(M3L.string_miniConsoleNotBoundToAKey);
                        }
                        else
                        {
                            ME1MiniConsoleKeyText = M3L.GetString(M3L.string_interp_miniConsoleBoundToX, typeKey.Value);
                        }
                    }
                }
                else
                {
                    HasME1Install = false;
                    ME1FullConsoleKeyText = M3L.GetString(M3L.string_bioInputiniFileIsMissing);
                    ME1MiniConsoleKeyText = M3L.GetString(M3L.string_runGameToGenerateFile);
                }
            }
            else
            {
                HasME1Install = false;
                ME1FullConsoleKeyText = M3L.GetString(M3L.string_noInstallsOfGameManagedByModManager);
                ME1MiniConsoleKeyText = "";
            }
        }

        public void OnSelectedME2TargetChanged()
        {
            if (SelectedME2Target != null)
            {
                LoadME2Keys(SelectedME2Target);
            }
            else
            {
                ME2FullConsoleKeyText = M3L.GetString(M3L.string_noInstallsOfGameManagedByModManager);
                ME2MiniConsoleKeyText = "";
            }
        }

        public void OnSelectedME3TargetChanged()
        {
            if (SelectedME3Target != null)
            {
                LoadME3Keys(SelectedME3Target);
            }
            else
            {
                ME3FullConsoleKeyText = M3L.GetString(M3L.string_noInstallsOfGameManagedByModManager);
                ME3MiniConsoleKeyText = "";
            }
        }

        private void LoadME2Keys(GameTarget target)
        {
            if (target.Game != Mod.MEGame.ME2) throw new Exception(@"Cannot load ME2 keys from target that is not ME2");
            ME2Coalesced me2c = null;

            try
            {
                me2c = ME2Coalesced.OpenFromTarget(target, true);
            }
            catch (Exception e)
            {
                Application.Current.Dispatcher.Invoke(delegate { M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_cannotOpenMassEffect2CoalescediniMessage, e.Message), M3L.GetString(M3L.string_errorReadingCoalescedini), MessageBoxButton.OK, MessageBoxImage.Error); });
                ME2MiniConsoleKeyText = M3L.GetString(M3L.string_errorReadingCoalescedini);
                ME2MiniConsoleKeyText = M3L.GetString(M3L.string_errorReadingCoalescedini);
                return;
            }

            var bioinput = me2c.Inis.FirstOrDefault(x => Path.GetFileName(x.Key).Equals(@"BioInput.ini", StringComparison.InvariantCultureIgnoreCase));
            var engineConsole = bioinput.Value.Sections.FirstOrDefault(x => x.Header == @"Engine.Console");
            if (engineConsole != null)
            {
                var consoleKey = engineConsole.Entries.FirstOrDefault(x => x.Key == @"ConsoleKey");
                if (consoleKey == null)
                {
                    ME2FullConsoleKeyText = M3L.GetString(M3L.string_fullConsoleNotBoundToAKey);
                }
                else
                {
                    ME2FullConsoleKeyText = M3L.GetString(M3L.string_interp_fullConsoleBoundToX, consoleKey.Value);
                }

                var typeKey = engineConsole.Entries.FirstOrDefault(x => x.Key == @"TypeKey");
                if (typeKey == null)
                {
                    ME2MiniConsoleKeyText = M3L.GetString(M3L.string_miniConsoleNotBoundToAKey);
                }
                else
                {
                    ME2MiniConsoleKeyText = M3L.GetString(M3L.string_interp_miniConsoleBoundToX, typeKey.Value);
                }
            }
        }

        private void LoadME3Keys(GameTarget target)
        {
            if (target.Game != Mod.MEGame.ME3) throw new Exception(@"Cannot load ME3 keys from target that is not ME3");
            try
            {
                var coalPath = Path.Combine(target.TargetPath, @"BioGame", @"CookedPCConsole", @"Coalesced.bin");
                Dictionary<string, string> coalescedFilemapping = null;
                if (File.Exists(coalPath))
                {
                    using FileStream fs = new FileStream(coalPath, FileMode.Open);
                    coalescedFilemapping = MassEffect3.Coalesce.Converter.DecompileToMemory(fs);
                }
                else
                {
                    Log.Error(
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
                    ME3FullConsoleKeyText = M3L.GetString(M3L.string_interp_fullConsoleBoundToX, consolekey.Value);
                }
                else
                {
                    ME3FullConsoleKeyText = M3L.GetString(M3L.string_fullConsoleNotBoundToAKey);
                }

                if (typekey != null)
                {
                    ME3MiniConsoleKeyText = M3L.GetString(M3L.string_interp_miniConsoleBoundToX, typekey.Value);
                }
                else
                {
                    ME3MiniConsoleKeyText = M3L.GetString(M3L.string_miniConsoleNotBoundToAKey);
                }
            }
            catch (Exception e)
            {
                Log.Error(@"Error reading keybinds: " + e.Message);
                M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_cannotReadME3Keybinds, e.Message), M3L.GetString(M3L.string_errorReadingKeybinds), MessageBoxButton.OK, MessageBoxImage.Error);
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
                case @"LeftShift":
                    return @"LeftShift";
                case @"Comma":
                    return @"Comma";
                case @"Period":
                    return @"Period";
                case @"Slash":
                    return @"Slash";
                case @"RightShift":
                    return @"RightShift";
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
                default:
                    Debug.WriteLine(@"Unknown key: " + key);
                    break;
            }

            return null; //Not usable
        }
    }
}
