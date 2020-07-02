using Serilog;
using Serilog.Sinks.RollingFile.Extension;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Windows;
using AuthenticodeExaminer;
using ByteSizeLib;
using MassEffectModManagerCore.gamefileformats;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.modmanager.usercontrols;
using MassEffectModManagerCore.ui;
using Microsoft.AppCenter.Analytics;
using Microsoft.Win32;
using Octokit;
using SevenZip;
using ProgressEventArgs = System.Management.ProgressEventArgs;
using System.Threading.Tasks;
using IniParser;
using IniParser.Model;
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.helpers;
using Microsoft.WindowsAPICodePack.Taskbar;
using NickStrupat;
using Polly;
using SlavaGu.ConsoleAppLauncher;
using System.Windows.Shell;

namespace MassEffectModManagerCore.modmanager.me3tweaks
{
    class LogCollector
    {
        public static string CollectLogs(string logfile)
        {
            Log.Information(@"Shutting down logger to allow application to pull log file.");
            Log.CloseAndFlush();
            try
            {
                string log = File.ReadAllText(logfile);
                CreateLogger();
                return log;
            }
            catch (Exception e)
            {
                CreateLogger();
                Log.Error(@"Could not read log file! " + e.Message);
                return null;
            }
        }

        internal static void CreateLogger()
        {
            Log.Logger = new LoggerConfiguration().WriteTo.SizeRollingFile(Path.Combine(App.LogDir, @"modmanagerlog.txt"),
                                    retainedFileDurationLimit: TimeSpan.FromDays(14),
                                    fileSizeLimitBytes: 1024 * 1024 * 10) // 10MB  
#if DEBUG
                .WriteTo.Debug()
#endif
                .CreateLogger();
        }

        internal static string CollectLatestLog(bool restartLogger)
        {
            Log.Information(@"Shutting down logger to allow application to pull log file.");
            Log.CloseAndFlush();
            var logFile = new DirectoryInfo(App.LogDir)
                                             .GetFiles(@"*.txt")
                                             .OrderByDescending(f => f.LastWriteTime)
                                             .FirstOrDefault();
            string logText = null;
            if (logFile != null && File.Exists(logFile.FullName))
            {
                logText = File.ReadAllText(logFile.FullName);
            }

            if (restartLogger)
            {
                CreateLogger();
            }
            return logText;
        }

        private enum Severity
        {
            INFO,
            WARN,
            ERROR,
            FATAL,
            GOOD,
            DIAGSECTION,
            BOLD,
            DLC,
            GAMEID,
            OFFICIALDLC,
            TPMI,
            SUB
        }
        private static int GetPartitionDiskBackingType(string partitionLetter)
        {
            using (var partitionSearcher = new ManagementObjectSearcher(
                @"\\localhost\ROOT\Microsoft\Windows\Storage",
                $@"SELECT DiskNumber FROM MSFT_Partition WHERE DriveLetter='{partitionLetter}'"))
            {
                try
                {
                    var partition = partitionSearcher.Get().Cast<ManagementBaseObject>().Single();
                    using (var physicalDiskSearcher = new ManagementObjectSearcher(
                        @"\\localhost\ROOT\Microsoft\Windows\Storage",
                        $@"SELECT Size, Model, MediaType FROM MSFT_PhysicalDisk WHERE DeviceID='{ partition[@"DiskNumber"] }'")) //do not localize
                    {
                        var physicalDisk = physicalDiskSearcher.Get().Cast<ManagementBaseObject>().Single();
                        return
                            (UInt16)physicalDisk[@"MediaType"];/*||
                        SSDModelSubstrings.Any(substring => result.Model.ToLower().Contains(substring)); ;*/


                    }
                }
                catch (Exception e)
                {
                    Log.Error($@"Error reading partition type on {partitionLetter}: {e.Message}");
                    return -1;
                }
            }
        }

        public static string GetProcessorInformationForDiag()
        {
            string str = "";
            try
            {
                ManagementObjectSearcher mosProcessor = new ManagementObjectSearcher(@"SELECT * FROM Win32_Processor");

                foreach (ManagementObject moProcessor in mosProcessor.Get())
                {
                    if (str != "")
                    {
                        str += "\n"; //do not localize
                    }

                    if (moProcessor[@"name"] != null)
                    {
                        str += moProcessor[@"name"].ToString();
                        str += "\n"; //do not localize
                    }
                    if (moProcessor[@"maxclockspeed"] != null)
                    {
                        str += @"Maximum reported clock speed: ";
                        str += moProcessor[@"maxclockspeed"].ToString();
                        str += " Mhz\n"; //do not localize
                    }
                    if (moProcessor[@"numberofcores"] != null)
                    {
                        str += @"Cores: ";

                        str += moProcessor[@"numberofcores"].ToString();
                        str += "\n"; //do not localize
                    }
                    if (moProcessor[@"numberoflogicalprocessors"] != null)
                    {
                        str += @"Logical processors: ";
                        str += moProcessor[@"numberoflogicalprocessors"].ToString();
                        str += "\n"; //do not localize
                    }

                }
                return str
                   .Replace(@"(TM)", @"™")
                   .Replace(@"(tm)", @"™")
                   .Replace(@"(R)", @"®")
                   .Replace(@"(r)", @"®")
                   .Replace(@"(C)", @"©")
                   .Replace(@"(c)", @"©")
                   .Replace(@"    ", @" ")
                   .Replace(@"  ", @" ").Trim();
            }
            catch (Exception e)
            {
                Log.Error($@"Error getting processor information: {e.Message}");
                return $"Error getting processor information: {e.Message}\n"; //do not localize
            }
        }

        private static void runMassEffectModderNoGuiIPC(string operationName, string exe, string args, object lockObject, Action<string, string> exceptionOccuredCallback, Action<int?> setExitCodeCallback = null, Action<string, string> ipcCallback = null)
        {
            Log.Information($@"Running Mass Effect Modder No GUI w/ IPC: {exe} {args}");
            var memProcess = new ConsoleApp(exe, args);
            bool hasExceptionOccured = false;
            memProcess.ConsoleOutput += (o, args2) =>
            {
                string str = args2.Line;
                if (hasExceptionOccured)
                {
                    Log.Fatal(@"MassEffectModderNoGui.exe: " + str);
                }
                if (str.StartsWith(@"[IPC]", StringComparison.Ordinal))
                {
                    string command = str.Substring(5);
                    int endOfCommand = command.IndexOf(' ');
                    if (endOfCommand >= 0)
                    {
                        command = command.Substring(0, endOfCommand);
                    }

                    string param = str.Substring(endOfCommand + 5).Trim();
                    if (command == @"EXCEPTION_OCCURRED")
                    {
                        hasExceptionOccured = true;
                        exceptionOccuredCallback?.Invoke(operationName, param);
                        return; //don't process this command further, nothing handles it.
                    }

                    ipcCallback?.Invoke(command, param);
                }
                //Debug.WriteLine(args2.Line);
            };
            memProcess.Exited += (a, b) =>
            {
                setExitCodeCallback?.Invoke(memProcess.ExitCode);
                lock (lockObject)
                {
                    Monitor.Pulse(lockObject);
                }
            };
            memProcess.Run();
        }

        public static string PerformDiagnostic(GameTarget selectedDiagnosticTarget, bool textureCheck, Action<string> updateStatusCallback = null, Action<int> updateProgressCallback = null, Action<TaskbarItemProgressState> updateTaskbarState = null)
        {
            updateStatusCallback?.Invoke(M3L.GetString(M3L.string_preparingToCollectDiagnosticInfo));
            updateTaskbarState?.Invoke(TaskbarItemProgressState.Indeterminate);

            #region MEM No Gui Fetch
            object memEnsuredSignaler = new object();
            // It is here we say a little prayer
            // to keep the bugs away from this monsterous code
            //    /_/\/\
            //    \_\  /
            //    /_/  \
            //    \_\/\ \
            //      \_\/

            bool hasMEM = false;
            string mempath = null;
            #region MEM Fetch Callbacks
            void failedToDownload()
            {
                Thread.Sleep(100); //try to stop deadlock
                Log.Error(@"Failed to acquire MEM for diagnostics.");
                hasMEM = false;
                lock (memEnsuredSignaler)
                {
                    Monitor.Pulse(memEnsuredSignaler);
                }
            }
            void readyToLaunch(string exe)
            {
                Thread.Sleep(100); //try to stop deadlock
                hasMEM = true;
                mempath = exe;
                lock (memEnsuredSignaler)
                {
                    Monitor.Pulse(memEnsuredSignaler);
                }
            };
            void failedToExtractMEM(Exception e, string message, string caption)
            {
                Thread.Sleep(100); //try to stop deadlock
                hasMEM = false;
                lock (memEnsuredSignaler)
                {
                    Monitor.Pulse(memEnsuredSignaler);
                }
            }
            void currentTaskCallback(string s) => updateStatusCallback?.Invoke(s);
            void setPercentDone(int pd) => updateStatusCallback?.Invoke(M3L.GetString(M3L.string_interp_preparingMEMNoGUIX, pd));

            #endregion
            // Ensure MEM NOGUI
            ExternalToolLauncher.FetchAndLaunchTool(ExternalToolLauncher.MEM_CMD, currentTaskCallback, null, setPercentDone, readyToLaunch, failedToDownload, failedToExtractMEM);

            //wait for tool fetch
            if (!hasMEM)
            {
                lock (memEnsuredSignaler)
                {
                    Monitor.Wait(memEnsuredSignaler, new TimeSpan(0, 0, 25));
                }
            }
            #endregion

            #region Diagnostic setup and diag header
            updateStatusCallback?.Invoke(M3L.GetString(M3L.string_collectingGameInformation));
            var diagStringBuilder = new StringBuilder();

            void addDiagLine(string message = "", Severity sev = Severity.INFO)
            {
                if (diagStringBuilder == null)
                {
                    diagStringBuilder = new StringBuilder();
                }

                switch (sev)
                {
                    case Severity.INFO:
                        diagStringBuilder.Append(message);
                        break;
                    case Severity.WARN:
                        diagStringBuilder.Append($@"[WARN]{message}");
                        break;
                    case Severity.ERROR:
                        diagStringBuilder.Append($@"[ERROR]{message}");
                        break;
                    case Severity.FATAL:
                        diagStringBuilder.Append($@"[FATAL]{message}");
                        break;
                    case Severity.DIAGSECTION:
                        diagStringBuilder.Append($@"[DIAGSECTION]{message}");
                        break;
                    case Severity.GOOD:
                        diagStringBuilder.Append($@"[GREEN]{message}");
                        break;
                    case Severity.BOLD:
                        diagStringBuilder.Append($@"[BOLD]{message}");
                        break;
                    case Severity.DLC:
                        diagStringBuilder.Append($@"[DLC]{message}");
                        break;
                    case Severity.OFFICIALDLC:
                        diagStringBuilder.Append($@"[OFFICIALDLC]{message}");
                        break;
                    case Severity.GAMEID:
                        diagStringBuilder.Append($@"[GAMEID]{message}");
                        break;
                    case Severity.TPMI:
                        diagStringBuilder.Append($@"[TPMI]{message}");
                        break;
                    case Severity.SUB:
                        diagStringBuilder.Append($@"[SUB]{message}");
                        break;
                    default:
                        Debugger.Break();
                        break;
                }
                diagStringBuilder.Append("\n"); //do not localize
            }


            string gamePath = selectedDiagnosticTarget.TargetPath;
            var gameID = selectedDiagnosticTarget.Game.ToString().Substring(2);

            addDiagLine(gameID, Severity.GAMEID);
            addDiagLine($@"{App.AppVersionHR} Game Diagnostic");
            addDiagLine($@"Diagnostic for {Utilities.GetGameName(selectedDiagnosticTarget.Game)}");
            addDiagLine($@"Diagnostic generated on {DateTime.Now.ToShortDateString()}");
            #endregion

            #region MEM Setup
            //vars
            string args = null;
            object memFinishedLock = new object();
            int? exitcode = null;

            //paths
            string oldMemGamePath = null;
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"MassEffectModder");
            string _iniPath = Path.Combine(path, @"MassEffectModder.ini");
            if (hasMEM)
            {
                // Set INI path to target

                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                if (!File.Exists(_iniPath))
                {
                    File.Create(_iniPath).Close();
                }

                IniData ini = new FileIniDataParser().ReadFile(_iniPath);
                oldMemGamePath = ini[@"GameDataPath"][selectedDiagnosticTarget.Game.ToString()];
                ini[@"GameDataPath"][selectedDiagnosticTarget.Game.ToString()] = gamePath;
                File.WriteAllText(_iniPath, ini.ToString());

                var versInfo = FileVersionInfo.GetVersionInfo(mempath);
                int fileVersion = versInfo.FileMajorPart;
                addDiagLine($@"Diagnostic MassEffectModderNoGui version: {fileVersion}");
            }
            else
            {
                addDiagLine(@"Mass Effect Modder No Gui was not available for use when this diagnostic was generated.", Severity.ERROR);
            }
            #endregion

            addDiagLine($@"System culture: {CultureInfo.InstalledUICulture.Name}");
            try
            {
                #region Game Information

                updateStatusCallback?.Invoke(M3L.GetString(M3L.string_collectingGameInformation));
                addDiagLine(@"Basic game information", Severity.DIAGSECTION);
                addDiagLine($@"Game is installed at {gamePath}");


                string pathroot = Path.GetPathRoot(gamePath);
                pathroot = pathroot.Substring(0, 1);
                if (pathroot == @"\")
                {
                    addDiagLine(@"Installation appears to be on a network drive (first character in path is \)", Severity.WARN);
                }
                else
                {
                    if (Utilities.IsWindows10OrNewer())
                    {
                        int backingType = GetPartitionDiskBackingType(pathroot);
                        string type = @"Unknown type";
                        switch (backingType)
                        {
                            case 3:
                                type = @"Hard disk drive";
                                break;
                            case 4:
                                type = @"Solid state drive";
                                break;
                            default:
                                type += @": " + backingType;
                                break;
                        }

                        addDiagLine(@"Installed on disk type: " + type);
                    }
                }

                selectedDiagnosticTarget.ReloadGameTarget(false); //reload vars
                ALOTVersionInfo avi = selectedDiagnosticTarget.GetInstalledALOTInfo();

                string exePath = MEDirectories.ExecutablePath(selectedDiagnosticTarget);
                if (File.Exists(exePath))
                {

                    var versInfo = FileVersionInfo.GetVersionInfo(exePath);
                    addDiagLine($@"Version: {versInfo.FileMajorPart}.{versInfo.FileMinorPart}.{versInfo.FileBuildPart}.{versInfo.FilePrivatePart}");
                    if (selectedDiagnosticTarget.Game == Mod.MEGame.ME1)
                    {
                        //bool me1LAAEnabled = Utilities.GetME1LAAEnabled();
                        //if (texturesInstalled && !me1LAAEnabled)
                        //{
                        //    addDiagLine("[ERROR] -  Large Address Aware: " + me1LAAEnabled + " - ALOT/MEUITM is installed - this being false will almost certainly cause crashes");
                        //}
                        //else
                        //{
                        //    addDiagLine("Large Address Aware: " + me1LAAEnabled);
                        //}
                    }

                    if (selectedDiagnosticTarget.Supported)
                    {
                        addDiagLine($@"Game source: {selectedDiagnosticTarget.GameSource}", Severity.GOOD);
                    }
                    else
                    {
                        addDiagLine($@"Game source: Unknown/Unsupported - {selectedDiagnosticTarget.ExecutableHash}", Severity.FATAL);
                    }

                    //Executable signatures
                    var info = new FileInspector(exePath);
                    var certOK = info.Validate();
                    if (certOK == SignatureCheckResult.NoSignature)
                    {
                        addDiagLine(@"This executable is not signed", Severity.ERROR);
                    }
                    else
                    {
                        if (certOK == SignatureCheckResult.BadDigest)
                        {
                            if (selectedDiagnosticTarget.Game == Mod.MEGame.ME1 && versInfo.ProductName == @"Mass_Effect")
                            {
                                //Check if this Mass_Effect
                                addDiagLine(@"Signature check for this executable was skipped as MEM modified this exe");
                            }
                            else
                            {
                                addDiagLine(@"The signature for this executable is not valid. The executable has been modified", Severity.ERROR);
                                diagPrintSignatures(info, addDiagLine);
                            }
                        }
                        else
                        {
                            addDiagLine(@"Signature check for this executable: " + certOK.ToString());
                            diagPrintSignatures(info, addDiagLine);
                        }
                    }

                    //BINK
                    if (Utilities.CheckIfBinkw32ASIIsInstalled(selectedDiagnosticTarget))
                    {
                        addDiagLine(@"binkw32 ASI bypass is installed");
                    }
                    else
                    {
                        addDiagLine(@"binkw32 ASI bypass is not installed. DLC mods, ASI mods, and modified DLC will not load", Severity.WARN);
                    }

                    var exeDir = Path.GetDirectoryName(exePath);
                    var d3d9file = Path.Combine(exeDir, @"d3d9.dll");
                    if (File.Exists(d3d9file))
                    {
                        FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(d3d9file);
                        string d3d9message = @"Product name on dll not set";
                        if (!string.IsNullOrEmpty(fvi.ProductName))
                        {
                            d3d9message = fvi.ProductName;
                        }

                        addDiagLine(@"d3d9.dll exists - " + d3d9message, Severity.WARN);
                    }

                    var fpscounter = Path.Combine(exeDir, @"fpscounter\fpscounter.dll");
                    if (File.Exists(fpscounter))
                    {
                        addDiagLine(@"fpscounter.dll exists - FPS Counter plugin detected, may cause stability issues. If using to fix AMD lighting issues, consider the Black Blobs Fix mod instead", Severity.WARN);
                    }

                    var dinput8 = Path.Combine(exeDir, @"dinput8.dll");
                    if (File.Exists(dinput8))
                    {
                        addDiagLine(@"dinput8.dll exists - a dll is hooking the process, may cause stability issues", Severity.WARN);
                    }
                }

                #endregion

                #region System Information

                updateStatusCallback?.Invoke(M3L.GetString(M3L.string_collectingSystemInformation));

                addDiagLine(@"System information", Severity.DIAGSECTION);
                OperatingSystem os = Environment.OSVersion;
                Version osBuildVersion = os.Version;

                //Windows 10 only
                string releaseId = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", @"ReleaseId", "").ToString();
                string productName = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", @"ProductName", "").ToString();
                string verLine = @"Running " + productName;
                if (osBuildVersion.Major == 10)
                {
                    verLine += @" " + releaseId;
                }

                if (os.Version < App.MIN_SUPPORTED_OS)
                {
                    addDiagLine(@"This operating system is not supported", Severity.FATAL);
                    addDiagLine(@"Upgrade to a supported operating system if you want support", Severity.FATAL);
                }

                addDiagLine(verLine, os.Version < App.MIN_SUPPORTED_OS ? Severity.ERROR : Severity.INFO);
                addDiagLine(@"Version " + osBuildVersion, os.Version < App.MIN_SUPPORTED_OS ? Severity.ERROR : Severity.INFO);

                addDiagLine();
                addDiagLine(@"System Memory", Severity.BOLD);
                var computerInfo = new ComputerInfo();
                long ramInBytes = (long)computerInfo.TotalPhysicalMemory;
                addDiagLine(@"Total memory available: " + ByteSize.FromBytes(ramInBytes).GibiBytes.ToString(@"#.##") + @"GB");
                addDiagLine(@"Processors", Severity.BOLD);
                addDiagLine(GetProcessorInformationForDiag());
                if (ramInBytes == 0)
                {
                    addDiagLine(@"Unable to get the read amount of physically installed ram. This may be a sign of impending hardware failure in the SMBIOS", Severity.WARN);
                }

                ManagementObjectSearcher objvide = new ManagementObjectSearcher(@"select * from Win32_VideoController");
                int vidCardIndex = 1;
                foreach (ManagementObject obj in objvide.Get())
                {
                    addDiagLine();
                    addDiagLine(@"Video Card " + vidCardIndex, Severity.BOLD);
                    addDiagLine(@"Name: " + obj[@"Name"]);

                    //Get Memory
                    string vidKey = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\";
                    vidKey += (vidCardIndex - 1).ToString().PadLeft(4, '0');
                    object returnvalue = null;
                    try
                    {
                        returnvalue = Registry.GetValue(vidKey, @"HardwareInformation.qwMemorySize", 0L);
                    }
                    catch (Exception ex)
                    {
                        addDiagLine($@"Unable to read memory size from registry. Reading from WMI instead ({ex.GetType()})", Severity.WARN);
                    }

                    string displayVal;
                    if (returnvalue != null && (long)returnvalue != 0)
                    {
                        displayVal = ByteSize.FromBytes((long)returnvalue).GibiBytes.ToString(@"#.##");
                    }
                    else
                    {
                        try
                        {
                            UInt32 wmiValue = (UInt32)obj[@"AdapterRam"];
                            var numBytes = ByteSize.FromBytes((long)wmiValue);
                            displayVal = numBytes.MebiBytes.ToString(@"#.##") + @" MB";
                            if (numBytes.MebiBytes == 4095)
                            {
                                displayVal += @" (possibly more, variable is 32-bit unsigned)";
                            }
                        }
                        catch (Exception)
                        {
                            displayVal = @"Unable to read value from registry/WMI";

                        }
                    }

                    addDiagLine(@"Memory: " + displayVal);
                    addDiagLine(@"DriverVersion: " + obj[@"DriverVersion"]);
                    vidCardIndex++;
                }

                #endregion

                #region Texture mod information
                updateStatusCallback?.Invoke(@"Getting texture mod installation info");

                addDiagLine(@"Texture mod information", Severity.DIAGSECTION);
                if (avi == null)
                {
                    addDiagLine(@"The texture mod installation marker was not detected. No texture mods appear to be installed");
                }
                else
                {
                    if (avi.ALOTVER > 0 || avi.MEUITMVER > 0)
                    {
                        addDiagLine(@"ALOT Version: " + avi.ALOTVER + @"." + avi.ALOTUPDATEVER + @"." + avi.ALOTHOTFIXVER);
                        if (selectedDiagnosticTarget.Game == Mod.MEGame.ME1 && avi.MEUITMVER != 0)
                        {
                            addDiagLine(@"MEUITM version: " + avi.MEUITMVER);
                        }
                    }
                    else
                    {
                        addDiagLine(@"This installation has been texture modded, but ALOT and/or MEUITM has not been installed");
                    }

                    if (avi.ALOT_INSTALLER_VERSION_USED > 0)
                    {
                        addDiagLine(@"Latest installation was from ALOT Installer v" + avi.ALOT_INSTALLER_VERSION_USED);
                    }

                    addDiagLine(@"Latest installation used MEM v" + avi.MEM_VERSION_USED);
                }

                #endregion



                #region Basegame file changes

                addDiagLine(@"Basegame changes", Severity.DIAGSECTION);

                updateStatusCallback?.Invoke(@"Collecting basegame file modifications");
                List<string> modifiedFiles = new List<string>();

                void failedCallback(string file)
                {
                    modifiedFiles.Add(file);
                }

                var isVanilla = VanillaDatabaseService.ValidateTargetAgainstVanilla(selectedDiagnosticTarget, failedCallback);
                if (isVanilla)
                {
                    addDiagLine(@"No modified basegame files were found.");
                }
                else
                {
                    if (!selectedDiagnosticTarget.TextureModded)
                    {
                        addDiagLine(@"The following basegame files have been modified:");
                        var cookedPath = MEDirectories.CookedPath(selectedDiagnosticTarget);
                        var markerPath = MEDirectories.ALOTMarkerPath(selectedDiagnosticTarget);
                        foreach (var mf in modifiedFiles)
                        {
                            if (mf.StartsWith(cookedPath, StringComparison.InvariantCultureIgnoreCase))
                            {
                                if (mf.Equals(markerPath, StringComparison.InvariantCultureIgnoreCase)) continue; //don't report this file
                                var info = BasegameFileIdentificationService.GetBasegameFileSource(selectedDiagnosticTarget, mf);
                                if (info != null)
                                {
                                    addDiagLine($@" - {mf.Substring(cookedPath.Length + 1)} - {info.source}");
                                }
                                else
                                {
                                    addDiagLine($@" - {mf.Substring(cookedPath.Length + 1)}");
                                }
                            }
                        }
                    }
                    else
                    {
                        //Check MEMI markers?
                        addDiagLine(@"Basegame changes check skipped as this installation has been texture modded");
                    }
                }

                #endregion

                #region Blacklisted mods check

                void memExceptionOccured(string operation, string line)
                {
                    addDiagLine($@"An exception occured performing operation '{operation}': {line}", Severity.ERROR);
                    addDiagLine(@"Check the Mod Manager application log for more information.", Severity.ERROR);
                    addDiagLine(@"Report this to ALOT or ME3Tweaks Discord for further assistance.", Severity.ERROR);
                }

                if (hasMEM)
                {
                    updateStatusCallback?.Invoke(@"Checking for blacklisted mods");
                    args = $@"--detect-bad-mods --gameid {gameID} --ipc";
                    exitcode = null;
                    var blacklistedMods = new List<string>();
                    runMassEffectModderNoGuiIPC(@"Detect Blacklisted Mods", mempath, args, memFinishedLock, memExceptionOccured, i => exitcode = i, (string command, string param) =>
                    {
                        switch (command)
                        {
                            case @"ERROR":
                                blacklistedMods.Add(param);
                                break;
                            default:
                                Debug.WriteLine(@"oof?");
                                break;
                        }
                    });
                    lock (memFinishedLock)
                    {
                        Monitor.Wait(memFinishedLock);
                    }

                    if (blacklistedMods.Any())
                    {
                        addDiagLine(@"The following blacklisted mods were found:", Severity.ERROR);
                        foreach (var str in blacklistedMods)
                        {
                            addDiagLine(@" - " + str);
                        }

                        addDiagLine(@"These mods have been blacklisted by modding tools because of known issues they cause. Do not use these mods", Severity.ERROR);
                    }
                    else
                    {
                        addDiagLine(@"No blacklisted mods were found installed");
                    }
                }
                else
                {
                    addDiagLine(@"MEM not available, skipped blacklisted mods check", Severity.WARN);

                }

                #endregion

                #region Installed DLCs

                //Get DLCs
                updateStatusCallback?.Invoke(M3L.GetString(M3L.string_collectingDLCInformation));

                var installedDLCs = MEDirectories.GetMetaMappedInstalledDLC(selectedDiagnosticTarget);

                addDiagLine(@"Installed DLC", Severity.DIAGSECTION);
                addDiagLine(@"The following DLC is installed:");

                bool metadataPresent = false;
                bool hasUIMod = false;
                bool compatPatchInstalled = false;
                bool hasNonUIDLCMod = false;
                Dictionary<int, string> priorities = new Dictionary<int, string>();
                var officialDLC = MEDirectories.OfficialDLC(selectedDiagnosticTarget.Game);
                foreach (var dlc in installedDLCs)
                {
                    string dlctext = dlc.Key;
                    if (!officialDLC.Contains(dlc.Key, StringComparer.InvariantCultureIgnoreCase))
                    {
                        dlctext += @";;";
                        if (dlc.Value != null)
                        {
                            if (int.TryParse(dlc.Value.InstalledBy, out var _))
                            {
                                dlctext += @"Installed by Mod Manager Build " + dlc.Value.InstalledBy;
                            }
                            else
                            {
                                dlctext += @"Installed by " + dlc.Value.InstalledBy;
                            }
                            if (dlc.Value.Version != null)
                            {
                                dlctext += @";;" + dlc.Value.Version;
                            }
                        }
                        else
                        {
                            dlctext += @"Not installed by managed installer";
                        }
                    }

                    addDiagLine(dlctext, officialDLC.Contains(dlc.Key, StringComparer.InvariantCultureIgnoreCase) ? Severity.OFFICIALDLC : Severity.DLC);
                }

                var supercedanceList = MEDirectories.GetFileSupercedances(selectedDiagnosticTarget).Where(x => x.Value.Count > 1).ToList();
                if (supercedanceList.Any())
                {
                    addDiagLine();
                    addDiagLine(@"Superceding files", Severity.BOLD);
                    addDiagLine(@"The following mod files supercede others due to same-named files. This may mean the mods are incompatible, or that these files are compatilibity patches. This information is for developer use only - DO NOT MODIFY YOUR GAME DIRECTORY MANUALLY.");

                    bool isFirst = true;
                    addDiagLine(@"Click to view list", Severity.SUB);
                    foreach (var sl in supercedanceList)
                    {
                        if (isFirst)
                            isFirst = false;
                        else
                            addDiagLine();

                        addDiagLine(sl.Key);
                        foreach (var dlc in sl.Value)
                        {
                            addDiagLine(dlc, Severity.TPMI);
                        }
                    }
                    addDiagLine(@"[/SUB]");
                }
                #endregion

                #region Get list of TFCs

                if (selectedDiagnosticTarget.Game > Mod.MEGame.ME1)
                {
                    updateStatusCallback?.Invoke(M3L.GetString(M3L.string_collectingTFCFileInformation));

                    addDiagLine(@"Texture File Cache (TFC) files", Severity.DIAGSECTION);
                    addDiagLine(@"The following TFC files are present in the game directory.");
                    var bgPath = MEDirectories.BioGamePath(selectedDiagnosticTarget);
                    string[] tfcFiles = Directory.GetFiles(bgPath, @"*.tfc", SearchOption.AllDirectories);
                    if (tfcFiles.Any())
                    {
                        foreach (string tfc in tfcFiles)
                        {
                            FileInfo fi = new FileInfo(tfc);
                            long tfcSize = fi.Length;
                            string tfcPath = tfc.Substring(bgPath.Length + 1);
                            addDiagLine($@" - {tfcPath}, {ByteSize.FromBytes(tfcSize).MebiBytes.ToString(@"#.##")} MB"); //do not localize
                        }
                    }
                    else
                    {
                        addDiagLine(@"No TFC files were found - is this installation broken?", Severity.ERROR);
                    }
                }

                #endregion

                if (hasMEM)
                {
                    #region Files added or removed after texture install

                    args = $@"--check-game-data-mismatch --gameid {gameID} --ipc";
                    if (selectedDiagnosticTarget.TextureModded)
                    {
                        bool textureMapFileExists = File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + $@"\MassEffectModder\me{gameID}map.bin");
                        addDiagLine(@"Files added or removed after texture mods were installed", Severity.DIAGSECTION);

                        if (textureMapFileExists)
                        {
                            // check for replaced files (file size changes)
                            updateStatusCallback?.Invoke(M3L.GetString(M3L.string_checkingTextureMapGameConsistency));
                            List<string> removedFiles = new List<string>();
                            List<string> addedFiles = new List<string>();
                            List<string> replacedFiles = new List<string>();
                            runMassEffectModderNoGuiIPC(@"Texture map check", mempath, args, memFinishedLock, memExceptionOccured, i => exitcode = i, (string command, string param) =>
                            {
                                switch (command)
                                {
                                    case @"ERROR_REMOVED_FILE":
                                        //.Add($" - File removed after textures were installed: {param}");
                                        removedFiles.Add(param);
                                        break;
                                    case @"ERROR_ADDED_FILE":
                                        //addedFiles.Add($"File was added after textures were installed" + param + " " + File.GetCreationTimeUtc(Path.Combine(gamePath, param));
                                        addedFiles.Add(param);
                                        break;
                                    case @"ERROR_VANILLA_MOD_FILE":
                                        if (!addedFiles.Contains(param))
                                        {
                                            replacedFiles.Add(param);
                                        }
                                        break;
                                    default:
                                        Debug.WriteLine(@"oof?");
                                        break;
                                }
                            });
                            lock (memFinishedLock)
                            {
                                Monitor.Wait(memFinishedLock);
                            }


                            if (removedFiles.Any())
                            {
                                addDiagLine(@"The following problems were detected checking game consistency with the texture map file:", Severity.ERROR);
                                foreach (var error in removedFiles)
                                {
                                    addDiagLine(@" - " + error, Severity.ERROR);
                                }
                            }

                            if (addedFiles.Any())
                            {
                                addDiagLine(@"The following files were added after textures were installed:", Severity.ERROR);
                                foreach (var error in addedFiles)
                                {
                                    addDiagLine(@" - " + error, Severity.ERROR);
                                }
                            }

                            if (replacedFiles.Any())
                            {
                                addDiagLine(@"The following files were replaced after textures were installed:", Severity.ERROR);
                                foreach (var error in replacedFiles)
                                {
                                    addDiagLine(@" - " + error, Severity.ERROR);
                                }
                            }

                            if (replacedFiles.Any() || addedFiles.Any() || removedFiles.Any())
                            {
                                addDiagLine(@"Diagnostic detected that some files were added, removed or replaced after textures were installed.", Severity.ERROR);
                                addDiagLine(@"Package files cannot be installed after a texture mod is installed - the texture pointers will be wrong.", Severity.ERROR);
                            }
                            else
                            {
                                addDiagLine(@"Diagnostic reports no files appear to have been added or removed since texture scan took place.");
                            }

                        }
                        else
                        {
                            addDiagLine($@"Texture map file is missing: {selectedDiagnosticTarget.Game.ToString().ToLower()}map.bin - was game migrated to new system or are you M3 on a different user account?");
                        }
                    }

                    #endregion

                    #region Textures check... unknown?

                    //what the hell is this?
                    //if (textureCheck)
                    //{
                    //    //if (MEMI_FOUND)
                    //    //{
                    //    args = $"--check-game-data-after --gameid {gameID} --ipc";
                    //    //}
                    //    //else // full texture check without mods installed. might allow this
                    //    //{
                    //    //    args = "--check-for-markers --gameid " + DIAGNOSTICS_GAME + " --ipc";
                    //    //}
                    //    exitcode = null;
                    //    runMassEffectModderNoGuiIPC(mempath, args, memFinishedLock, i => exitcode = i, (string command, string param) =>
                    //    {

                    //    });
                    //    lock (memFinishedLock)
                    //    {
                    //        Monitor.Wait(memFinishedLock);
                    //    }
                    //    if (MEMI_FOUND)
                    //    {
                    //        addDiagLine("===Replaced files scan (after textures were installed)");
                    //        addDiagLine("This check will detect if files were replaced after textures were installed in an unsupported manner.");
                    //        addDiagLine("");
                    //    }
                    //    else
                    //    {
                    //        addDiagLine("===Preinstallation file scan");
                    //        addDiagLine("This check will make sure all files can be opened for reading and that files that were previously modified by ALOT are not installed.");
                    //        addDiagLine("");
                    //    }

                    //    if (BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Count > 0)
                    //    {

                    //        if (MEMI_FOUND)
                    //        {
                    //            addDiagLine("[ERROR]Diagnostic reports some files appear to have been added or replaced after ALOT was installed, or could not be read:");
                    //        }
                    //        else
                    //        {
                    //            addDiagLine("[ERROR]The following files did not pass the modification marker check, or could not be read:");
                    //        }

                    //        int numSoFar = 0;
                    //        foreach (String str in BACKGROUND_MEM_PROCESS_PARSED_ERRORS)
                    //        {
                    //            addDiagLine("[ERROR] - " + str);
                    //            numSoFar++;
                    //            if (numSoFar == 10 && BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Count() > 10)
                    //            {
                    //                addDiagLine("[SUB]");
                    //            }
                    //        }

                    //        if (numSoFar > 10)
                    //        {
                    //            addDiagLine("[/SUB]");
                    //        }

                    //        if (MEMI_FOUND)
                    //        {
                    //            addDiagLine("[ERROR]Files added or replaced after ALOT has been installed is not supported due to the way the Unreal Engine 3 works.");
                    //        }
                    //        else
                    //        {
                    //            addDiagLine("[ERROR]Files that were previously modified by ALOT are most times broken or leftover from a previous ALOT failed installation that did not complete and set the ALOT installation marker.");
                    //            addDiagLine("[ERROR]Delete your game installation and reinstall the game, or restore from your backup in the ALOT settings.");
                    //        }

                    //        if (BACKGROUND_MEM_PROCESS.ExitCode == null || BACKGROUND_MEM_PROCESS.ExitCode != 0)
                    //        {
                    //            pairLog = true;
                    //            addDiagLine("[ERROR]MEMNoGui returned non zero exit code, or null (crash) during --check-game-data-after. Some data was returned. The return code was: " + BACKGROUND_MEM_PROCESS.ExitCode);
                    //        }
                    //    }
                    //    else
                    //    {
                    //        if (BACKGROUND_MEM_PROCESS.ExitCode != null && BACKGROUND_MEM_PROCESS.ExitCode == 0)
                    //        {
                    //            if (MEMI_FOUND)
                    //            {
                    //                addDiagLine("Diagnostic did not find any files that were added or replaced after ALOT installation or have issues reading files.");
                    //            }
                    //            else
                    //            {
                    //                addDiagLine("Diagnostic did not find any files from previous installations of ALOT or have issues reading files.");
                    //            }
                    //        }
                    //        else
                    //        {
                    //            pairLog = true;
                    //            addDiagLine("[ERROR]MEMNoGui returned non zero exit code, or null (crash) during --check-game-data-after: " + BACKGROUND_MEM_PROCESS.ExitCode);
                    //        }
                    //    }

                    //    diagnosticsWorker.ReportProgress(0, new ThreadCommand(RESET_REPLACEFILE_TEXT));
                    //    diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_GREEN, Image_DataAfter));
                    //}

                    //Context = CONTEXT_NORMAL;
                    #endregion

                    #region Textures - full check
                    //FULL CHECK
                    if (textureCheck)
                    {
                        var param = 0;
                        updateStatusCallback?.Invoke(M3L.GetString(M3L.string_interp_performingFullTexturesCheckX, param)); //done this way to save a string in localization
                        addDiagLine(@"Full Textures Check", Severity.DIAGSECTION);
                        args = $@"--check-game-data-textures --gameid {gameID} --ipc";
                        exitcode = null;
                        var emptyMipsNotRemoved = new List<string>();
                        var badTFCReferences = new List<string>();
                        var scanErrors = new List<string>();
                        string lastMissingTFC = null;
                        updateProgressCallback?.Invoke(0);
                        updateTaskbarState?.Invoke(TaskbarItemProgressState.Normal);

                        runMassEffectModderNoGuiIPC(@"Full textures check", mempath, args, memFinishedLock, memExceptionOccured, i => exitcode = i, (string command, string param) =>
                        {
                            switch (command)
                            {
                                case @"ERROR_MIPMAPS_NOT_REMOVED":
                                    if (selectedDiagnosticTarget.TextureModded)
                                    {
                                        //only matters when game is texture modded
                                        emptyMipsNotRemoved.Add(param);
                                    }
                                    break;
                                case @"TASK_PROGRESS":
                                    if (int.TryParse(param, out var progress))
                                    {
                                        updateProgressCallback?.Invoke(progress);
                                    }
                                    updateStatusCallback?.Invoke($"Performing full textures check {param}%");
                                    break;
                                case @"PROCESSING_FILE":
                                    //Don't think there's anything to do with this right now
                                    break;
                                case @"ERROR_REFERENCED_TFC_NOT_FOUND":
                                    //badTFCReferences.Add(param);
                                    lastMissingTFC = param;
                                    break;
                                case @"ERROR_TEXTURE_SCAN_DIAGNOSTIC":
                                    if (lastMissingTFC != null)
                                    {
                                        if (lastMissingTFC.StartsWith(@"Textures_"))
                                        {
                                            var foldername = Path.GetFileNameWithoutExtension(lastMissingTFC).Substring(@"Textures_".Length);
                                            if (MEDirectories.OfficialDLC(selectedDiagnosticTarget.Game)
                                                .Contains(foldername))
                                            {
                                                break; //dlc is packed still
                                            }
                                        }
                                        badTFCReferences.Add(lastMissingTFC + @", " + param);
                                    }
                                    else
                                    {
                                        scanErrors.Add(param);
                                    }
                                    lastMissingTFC = null; //reset
                                    break;
                                default:
                                    Debug.WriteLine($@"{command} {param}");
                                    break;
                            }
                        });
                        lock (memFinishedLock)
                        {
                            Monitor.Wait(memFinishedLock);
                        }
                        updateProgressCallback?.Invoke(0);
                        updateTaskbarState?.Invoke(TaskbarItemProgressState.Indeterminate);


                        if (emptyMipsNotRemoved.Any() || badTFCReferences.Any() || scanErrors.Any())
                        {
                            addDiagLine(@"Texture check reported errors", Severity.ERROR);
                            if (emptyMipsNotRemoved.Any())
                            {
                                addDiagLine();
                                addDiagLine(@"The following textures contain empty mips, which typically means files were installed after texture mods were installed.:", Severity.ERROR);
                                foreach (var em in emptyMipsNotRemoved)
                                {
                                    addDiagLine(@" - " + em, Severity.ERROR);
                                }
                            }

                            if (badTFCReferences.Any())
                            {
                                addDiagLine();
                                addDiagLine(@"The following textures have bad TFC references, which means the mods were built wrong, dependent DLC is missing, or the mod was installed wrong:", Severity.ERROR);
                                foreach (var br in badTFCReferences)
                                {
                                    addDiagLine(@" - " + br, Severity.ERROR);
                                }
                            }

                            if (scanErrors.Any())
                            {
                                addDiagLine();
                                addDiagLine(@"The following textures failed to scan:", Severity.ERROR);
                                foreach (var fts in scanErrors)
                                {
                                    addDiagLine(@" - " + fts, Severity.ERROR);
                                }
                            }
                        }
                        else
                        {
                            addDiagLine(@"Texture check did not find any texture issues in this installation");
                        }


                        //int numSoFar = 0;
                        //foreach (string str in BACKGROUND_MEM_PROCESS_PARSED_ERRORS)
                        //{
                        //    addDiagLine("[ERROR] -  " + str);
                        //    numSoFar++;
                        //    if (numSoFar == 10 && BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Count() > 10)
                        //    {
                        //        addDiagLine("[SUB]");
                        //    }
                        //}

                        //if (numSoFar > 10)
                        //{
                        //    addDiagLine("[/SUB]");
                        //}

                        //if (BACKGROUND_MEM_PROCESS.ExitCode == null || BACKGROUND_MEM_PROCESS.ExitCode != 0)
                        //{
                        //    pairLog = true;
                        //    addDiagLine("[ERROR]MEMNoGui returned non zero exit code, or null (crash) during --check-game-data-textures. Some data was returned. The return code was: " + BACKGROUND_MEM_PROCESS.ExitCode);
                        //}
                    }
                    else
                    {
                        //if (BACKGROUND_MEM_PROCESS.ExitCode != null && BACKGROUND_MEM_PROCESS.ExitCode == 0)
                        //{
                        //    addDiagLine("Diagnostics textures check (full) did not find any issues.");
                        //}
                        //else
                        //{
                        //    pairLog = true;
                        //    addDiagLine("[ERROR]MEMNoGui returned non zero exit code, or null (crash) during --check-game-data-textures: " + BACKGROUND_MEM_PROCESS.ExitCode);
                        //}
                    }
                    #endregion


                    #region Texture LODs

                    updateStatusCallback?.Invoke(@"Collecting LOD settings");
                    args = $@"--print-lods --gameid {gameID} --ipc";
                    var lods = new Dictionary<string, string>();
                    runMassEffectModderNoGuiIPC(@"MEM - Fetch LODS", mempath, args, memFinishedLock, memExceptionOccured, i => exitcode = i, (string command, string param) =>
                    {
                        switch (command)
                        {
                            case @"LODLINE":
                                var lodSplit = param.Split(@"=");
                                lods[lodSplit[0]] = param.Substring(lodSplit[0].Length + 1);
                                break;
                            default:
                                Debug.WriteLine(@"oof?");
                                break;
                        }
                    });
                    lock (memFinishedLock)
                    {
                        Monitor.Wait(memFinishedLock);
                    }

                    addLODStatusToDiag(selectedDiagnosticTarget, lods, addDiagLine);

                    #endregion
                }
                else
                {
                    addDiagLine(@"Texture checks skipped", Severity.DIAGSECTION);
                    addDiagLine(@"Mass Effect Modder No Gui was not available for use when this diagnostic was run.", Severity.WARN);
                    addDiagLine(@"The following checks were skipped:", Severity.WARN);
                    addDiagLine(@" - Files added or removed after texture install", Severity.WARN);
                    addDiagLine(@" - Blacklisted mods check", Severity.WARN);
                    addDiagLine(@" - Textures check", Severity.WARN);
                    addDiagLine(@" - Texture LODs check", Severity.WARN);
                }

                #region ASI mods

                updateStatusCallback?.Invoke(M3L.GetString(M3L.string_collectingASIFileInformation));

                string asidir = MEDirectories.ASIPath(selectedDiagnosticTarget);
                addDiagLine(@"Installed ASI mods", Severity.DIAGSECTION);
                if (Directory.Exists(asidir))
                {
                    addDiagLine(@"The following ASI files are located in the ASI directory:");
                    string[] files = Directory.GetFiles(asidir, @"*.asi");
                    if (!files.Any())
                    {
                        addDiagLine(@"ASI directory is empty. No ASI mods are installed.");
                    }
                    else
                    {
                        foreach (string f in files)
                        {
                            addDiagLine(@" - " + Path.GetFileName(f));
                        }
                        addDiagLine();
                        addDiagLine(@"Ensure that only one version of an ASI is installed. If multiple copies of the same one are installed, the game may crash on startup.");
                    }
                }
                else
                {
                    addDiagLine(@"ASI directory does not exist. No ASI mods are installed.");
                }

                #endregion

                #region ME3: TOC check

                //TOC SIZE CHECK
                if (selectedDiagnosticTarget.Game == Mod.MEGame.ME3)
                {
                    updateStatusCallback?.Invoke(@"Collecting TOC file information");

                    addDiagLine(@"File Table of Contents (TOC) size check", Severity.DIAGSECTION);
                    addDiagLine(@"PCConsoleTOC.bin files list the size of each file the game can load.");
                    addDiagLine(@"If the size is smaller than the actual file, the game will not allocate enough memory to load the file.");
                    addDiagLine(@"These hangs typically occur at loading screens and are the result of manually modifying files without running AutoTOC afterwards.");
                    bool hadTocError = false;
                    string[] tocs = Directory.GetFiles(Path.Combine(gamePath, @"BIOGame"), @"PCConsoleTOC.bin", SearchOption.AllDirectories);
                    string markerfile = MEDirectories.ALOTMarkerPath(selectedDiagnosticTarget);
                    foreach (string toc in tocs)
                    {
                        TOCBinFile tbf = new TOCBinFile(toc);
                        foreach (TOCBinFile.Entry ent in tbf.Entries)
                        {
                            //Console.WriteLine(index + "\t0x" + ent.offset.ToString("X6") + "\t" + ent.size + "\t" + ent.name);
                            string filepath = Path.Combine(gamePath, ent.name);
                            if (File.Exists(filepath) && !filepath.Equals(markerfile, StringComparison.InvariantCultureIgnoreCase) && !filepath.ToLower().EndsWith(@"pcconsoletoc.bin"))
                            {
                                FileInfo fi = new FileInfo(filepath);
                                long size = fi.Length;
                                if (ent.size < size)
                                {
                                    addDiagLine($@" - {filepath} size is {size}, but TOC lists {ent.size} ({ent.size - size} bytes)", Severity.ERROR);
                                    hadTocError = true;
                                }
                            }
                        }
                    }

                    if (!hadTocError)
                    {
                        addDiagLine(@"All TOC files passed check. No files have a size larger than the TOC size.");
                    }
                    else
                    {
                        addDiagLine(@"Some files are larger than the listed TOC size. This typically won't happen unless you manually installed some files or an ALOT installation failed.", Severity.ERROR);
                        addDiagLine(@"The game will always hang while loading these files." + (selectedDiagnosticTarget.Supported ? @" You can regenerate the TOC files by using AutoTOC from the tools menu. If installation failed due a crash, this won't fix it." : ""));
                    }
                }

                #endregion

                #region Mass Effect (1) log files

                //ME1: LOGS
                if (selectedDiagnosticTarget.Game == Mod.MEGame.ME1)
                {
                    updateStatusCallback?.Invoke(M3L.GetString(M3L.string_collectingME1ApplicationLogs));

                    //GET LOGS
                    string logsdir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), @"BioWare\Mass Effect\Logs");
                    if (Directory.Exists(logsdir))
                    {
                        DirectoryInfo info = new DirectoryInfo(logsdir);
                        FileInfo[] files = info.GetFiles().Where(f => f.LastWriteTime > DateTime.Now.AddDays(-3)).OrderByDescending(p => p.LastWriteTime).ToArray();
                        DateTime threeDaysAgo = DateTime.Now.AddDays(-3);
                        foreach (FileInfo file in files)
                        {
                            //Console.WriteLine(file.Name + " " + file.LastWriteTime);
                            var logLines = File.ReadAllLines(file.FullName);
                            int crashLineNumber = -1;
                            int currentLineNumber = -1;
                            string reason = "";
                            foreach (string line in logLines)
                            {
                                if (line.Contains(@"Critical: appError called"))
                                {
                                    crashLineNumber = currentLineNumber;
                                    reason = @"Log file indicates crash occured";
                                    Log.Information(@"Found crash in ME1 log " + file.Name + @" on line " + currentLineNumber);
                                    break;
                                }

                                currentLineNumber++;
                            }

                            if (crashLineNumber >= 0)
                            {
                                crashLineNumber = Math.Max(0, crashLineNumber - 10); //show last 10 lines of log leading up to the crash
                                                                                     //this log has a crash
                                addDiagLine(@"Mass Effect game log " + file.Name, Severity.DIAGSECTION);
                                if (reason != "") addDiagLine(reason);
                                if (crashLineNumber > 0)
                                {
                                    addDiagLine(@"[CRASHLOG]...");
                                }

                                for (int i = crashLineNumber; i < logLines.Length; i++)
                                {
                                    addDiagLine(@"[CRASHLOG]" + logLines[i]);
                                }
                            }
                        }
                    }
                }

                #endregion

                #region Event logs for crashes

                //EVENT LOGS
                updateStatusCallback?.Invoke(M3L.GetString(M3L.string_collectingEventLogs));
                StringBuilder crashLogs = new StringBuilder();
                var sevenDaysAgo = DateTime.Now.AddDays(-3);

                //Get event logs
                EventLog ev = new EventLog(@"Application");
                List<EventLogEntry> entries = ev.Entries
                    .Cast<EventLogEntry>()
                    .Where(z => z.InstanceId == 1001 && z.TimeGenerated > sevenDaysAgo && (GenerateEventLogString(z).ContainsAny(MEDirectories.ExecutableNames(selectedDiagnosticTarget.Game), StringComparison.InvariantCultureIgnoreCase)))
                    .ToList();

                addDiagLine($@"{Utilities.GetGameName(selectedDiagnosticTarget.Game)} crash logs found in Event Viewer", Severity.DIAGSECTION);
                if (entries.Any())
                {
                    foreach (var entry in entries)
                    {
                        string str = string.Join("\n", GenerateEventLogString(entry).Split('\n').ToList().Take(17).ToList()); //do not localize
                        addDiagLine($@"{Utilities.GetGameName(selectedDiagnosticTarget.Game)} Event {entry.TimeGenerated}\n{str}"); // !!! ?
                    }

                }
                else
                {
                    addDiagLine(@"No crash events found in Event Viewer");
                }

                #endregion

                #region Mass Effect 3 me3logger log

                if (selectedDiagnosticTarget.Game == Mod.MEGame.ME3)
                {
                    updateStatusCallback?.Invoke(M3L.GetString(M3L.string_collectingME3SessionLog));
                    string me3logfilepath = Path.Combine(Directory.GetParent(MEDirectories.ExecutablePath(selectedDiagnosticTarget)).FullName, @"me3log.txt");
                    if (File.Exists(me3logfilepath))
                    {

                        FileInfo fi = new FileInfo(me3logfilepath);
                        if (fi.Length < 10000)
                        {
                            addDiagLine(@"Mass Effect 3 last session log", Severity.DIAGSECTION);
                            addDiagLine(@"Last session log has modification date of " + fi.LastWriteTimeUtc.ToShortDateString());
                            addDiagLine();
                            var log = Utilities.WriteSafeReadAllLines(me3logfilepath); //try catch needed?
                            foreach (string line in log)
                            {
                                addDiagLine(line);
                            }
                        }
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                addDiagLine(@"Exception occured while running diagnostic.", Severity.ERROR);
                addDiagLine(App.FlattenException(ex), Severity.ERROR);
                return diagStringBuilder.ToString();
            }
            finally
            {
                //restore MEM setting
                if (hasMEM)
                {
                    if (File.Exists(_iniPath))
                    {
                        IniData ini = new FileIniDataParser().ReadFile(_iniPath);
                        ini[@"GameDataPath"][selectedDiagnosticTarget.Game.ToString()] = oldMemGamePath;
                        File.WriteAllText(_iniPath, ini.ToString());
                    }
                }
            }

            return diagStringBuilder.ToString();
        }

        private static void diagPrintSignatures(FileInspector info, Action<string, Severity> addDiagLine)
        {
            foreach (var sig in info.GetSignatures())
            {
                var signingTime = sig.TimestampSignatures.FirstOrDefault()?.TimestampDateTime?.UtcDateTime;
                addDiagLine(@" > Signed on " + signingTime, Severity.INFO);

                foreach (var signChain in sig.AdditionalCertificates)
                {
                    try
                    {
                        var outStr = signChain.Subject.Substring(3); //remove CN=
                        outStr = outStr.Substring(0, outStr.IndexOf(','));
                        addDiagLine(@" >> Signed by " + outStr, Severity.INFO);
                    }
                    catch
                    {
                        addDiagLine(@"  >> Signed by " + signChain.Subject, Severity.INFO);
                    }
                }
            }
        }

        private static string GenerateEventLogString(EventLogEntry entry) => $"Event type: {entry.EntryType}\nEvent Message: {entry.Message + entry}\nEvent Time: {entry.TimeGenerated.ToShortTimeString()}\nEvent {entry.UserName}\n"; //do not localize

        private static void addLODStatusToDiag(GameTarget selectedDiagnosticTarget, Dictionary<string, string> lods, Action<string, Severity> addDiagLine)
        {
            addDiagLine(@"Texture Level of Detail (LOD) settings", Severity.DIAGSECTION);
            string iniPath = MEDirectories.LODConfigFile(selectedDiagnosticTarget.Game);
            if (!File.Exists(iniPath))
            {
                addDiagLine($@"Game config file is missing: {iniPath}", Severity.ERROR);
                return;
            }

            foreach (KeyValuePair<string, string> kvp in lods)
            {
                addDiagLine($@"{kvp.Key}={kvp.Value}", Severity.INFO);
            }
            var textureChar1024 = lods.FirstOrDefault(x => x.Key == @"TEXTUREGROUP_Character_1024");
            if (string.IsNullOrWhiteSpace(textureChar1024.Key)) //does this work for ME2/ME3??
            {
                //not found
                addDiagLine(@"Could not find TEXTUREGROUP_Character_1024 in config file for checking LOD settings", Severity.ERROR);
                return;
            }

            try
            {
                int maxLodSize = 0;
                if (!string.IsNullOrWhiteSpace(textureChar1024.Value))
                {
                    //ME2,3 default to blank
                    maxLodSize = int.Parse(StringStructParser.GetCommaSplitValues(textureChar1024.Value)[selectedDiagnosticTarget.Game == Mod.MEGame.ME1 ? @"MinLODSize" : @"MaxLODSize"]);
                }

                // Texture mod installed, HQ LODs
                var HQLine = @"High quality texture LOD settings appear to be set";

                // Texture mod installed, missing HQ LODs
                var HQSettingsMissingLine = @"High quality texture LOD settings appear to be missing, but a high resolution texture mod appears to be installed.\n[ERROR]The game will not use these new high quality assets - config file was probably deleted or texture quality settings were changed in game"; //do not localize

                // No texture mod, no HQ LODs
                var HQVanillaLine = @"High quality LOD settings are not set and no high quality texture mod is installed";
                switch (selectedDiagnosticTarget.Game)
                {
                    case Mod.MEGame.ME1:
                        if (maxLodSize != 1024) //ME1 Default
                        {
                            //LODS MODIFIED!
                            if (maxLodSize == 4096)
                            {
                                addDiagLine(@"LOD quality settings: 4K textures", Severity.INFO);
                            }
                            else if (maxLodSize == 2048)
                            {
                                addDiagLine(@"LOD quality settings: 2K textures", Severity.INFO);
                            }

                            //Not Default
                            if (selectedDiagnosticTarget.TextureModded)
                            {
                                addDiagLine(@"This installation appears to have a texture mod installed, so unused/empty mips are already removed", Severity.INFO);
                            }
                            else if (maxLodSize > 1024)
                            {
                                addDiagLine(@"Texture LOD settings appear to have been raised, but this installation has not been texture modded - game will likely have unused mip crashes.", Severity.FATAL);
                            }
                        }
                        else
                        {
                            //Default ME1 LODs
                            if (selectedDiagnosticTarget.TextureModded && selectedDiagnosticTarget.HasALOTOrMEUITM())
                            {
                                addDiagLine(HQSettingsMissingLine, Severity.ERROR);
                            }
                            else
                            {
                                addDiagLine(HQVanillaLine, Severity.INFO);
                            }
                        }

                        break;
                    case Mod.MEGame.ME2:
                    case Mod.MEGame.ME3:
                        if (maxLodSize != 0)
                        {
                            //Not vanilla, alot/meuitm
                            if (selectedDiagnosticTarget.TextureModded && selectedDiagnosticTarget.HasALOTOrMEUITM())
                            {
                                addDiagLine(HQVanillaLine, Severity.INFO);
                                if (maxLodSize == 4096)
                                {
                                    addDiagLine(@"LOD quality settings: 4K textures", Severity.INFO);
                                }
                                else if (maxLodSize == 2048)
                                {
                                    addDiagLine(@"LOD quality settings: 2K textures", Severity.INFO);
                                }
                            }
                            else
                            {
                                //else if (selectedDiagnosticTarget.TextureModded) //not vanilla, but no MEM/MEUITM
                                //{
                                if (maxLodSize == 4096)
                                {
                                    addDiagLine(@"LOD quality settings: 4K textures (no high res mod installed)", Severity.WARN);
                                }
                                else if (maxLodSize == 2048)
                                {
                                    addDiagLine(@"LOD quality settings: 2K textures (no high res mod installed)", Severity.INFO);
                                }

                                //}
                                if (!selectedDiagnosticTarget.TextureModded)
                                {
                                    //no texture mod, but has set LODs
                                    addDiagLine(@"LODs have been explicitly set, but a texture mod is not installed - game may have black textures as empty mips may not be removed", Severity.WARN);
                                }
                            }
                        }
                        else //default
                        {
                            //alot/meuitm, but vanilla settings.
                            if (selectedDiagnosticTarget.TextureModded && selectedDiagnosticTarget.HasALOTOrMEUITM())
                            {
                                addDiagLine(HQSettingsMissingLine, Severity.ERROR);
                            }
                            else //no alot/meuitm, vanilla setting.
                            {
                                addDiagLine(HQVanillaLine, Severity.INFO);
                            }
                        }

                        break;
                }
            }
            catch (Exception e)
            {
                Log.Error(@"Error checking LOD settings: " + e.Message);
                addDiagLine($@"Error checking LOD settings: {e.Message}", Severity.INFO);
            }
        }
    }
}
