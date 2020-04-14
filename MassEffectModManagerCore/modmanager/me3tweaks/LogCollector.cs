

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
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.helpers;
using NickStrupat;
using Polly;
using SlavaGu.ConsoleAppLauncher;

namespace MassEffectModManagerCore.modmanager.me3tweaks
{
    [Localizable(false)]
    class LogCollector
    {
        public static string CollectLogs(string logfile)
        {
            Log.Information("Shutting down logger to allow application to pull log file.");
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
                Log.Error("Could not read log file! " + e.Message);
                return null;
            }
        }

        internal static void CreateLogger()
        {
            Log.Logger = new LoggerConfiguration().WriteTo.SizeRollingFile(Path.Combine(App.LogDir, "modmanagerlog.txt"),
                                    retainedFileDurationLimit: TimeSpan.FromDays(14),
                                    fileSizeLimitBytes: 1024 * 1024 * 10) // 10MB  
#if DEBUG
                .WriteTo.Debug()
#endif
                .CreateLogger();
        }

        internal static string CollectLatestLog(bool restartLogger)
        {
            Log.Information("Shutting down logger to allow application to pull log file.");
            Log.CloseAndFlush();
            var logFile = new DirectoryInfo(App.LogDir)
                                             .GetFiles("*.txt")
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
            SECTION
        }
        private static int GetPartitionDiskBackingType(string partitionLetter)
        {
            using (var partitionSearcher = new ManagementObjectSearcher(
                @"\\localhost\ROOT\Microsoft\Windows\Storage",
                $"SELECT DiskNumber FROM MSFT_Partition WHERE DriveLetter='{partitionLetter}'"))
            {
                try
                {
                    var partition = partitionSearcher.Get().Cast<ManagementBaseObject>().Single();
                    using (var physicalDiskSearcher = new ManagementObjectSearcher(
                        @"\\localhost\ROOT\Microsoft\Windows\Storage",
                        $"SELECT Size, Model, MediaType FROM MSFT_PhysicalDisk WHERE DeviceID='{ partition["DiskNumber"] }'"))
                    {
                        var physicalDisk = physicalDiskSearcher.Get().Cast<ManagementBaseObject>().Single();
                        return
                            (UInt16)physicalDisk["MediaType"];/*||
                        SSDModelSubstrings.Any(substring => result.Model.ToLower().Contains(substring)); ;*/


                    }
                }
                catch (Exception e)
                {
                    Log.Error($"Error reading partition type on {partitionLetter}: {e.Message}");
                    return -1;
                }
            }
        }

        public static string GetProcessorInformationForDiag()
        {
            string str = "";
            try
            {
                ManagementObjectSearcher mosProcessor = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");

                foreach (ManagementObject moProcessor in mosProcessor.Get())
                {
                    if (str != "")
                    {
                        str += "\n";
                    }

                    if (moProcessor["name"] != null)
                    {
                        str += moProcessor["name"].ToString();
                        str += "\n";
                    }
                    if (moProcessor["maxclockspeed"] != null)
                    {
                        str += "Maximum reported clock speed: ";
                        str += moProcessor["maxclockspeed"].ToString();
                        str += " Mhz\n";
                    }
                    if (moProcessor["numberofcores"] != null)
                    {
                        str += "Cores: ";

                        str += moProcessor["numberofcores"].ToString();
                        str += "\n";
                    }
                    if (moProcessor["numberoflogicalprocessors"] != null)
                    {
                        str += "Logical processors: ";
                        str += moProcessor["numberoflogicalprocessors"].ToString();
                        str += "\n";
                    }

                }
                return str
                   .Replace("(TM)", "™")
                   .Replace("(tm)", "™")
                   .Replace("(R)", "®")
                   .Replace("(r)", "®")
                   .Replace("(C)", "©")
                   .Replace("(c)", "©")
                   .Replace("    ", " ")
                   .Replace("  ", " ").Trim();
            }
            catch (Exception e)
            {
                return $"Error getting processor information: {e.Message}\n";
            }
        }

        private static void runMassEffectModderNoGuiIPC(string exe, string args, object lockObject, Action<int?> setExitCodeCallback = null, Action<string, string> ipcCallback = null)
        {
            Log.Information($"Running Mass Effect Modder No GUI w/ IPC: {exe} {args}");
            var memProcess = new ConsoleApp(exe, args);
            memProcess.ConsoleOutput += (o, args2) =>
            {
                string str = args2.Line;
                if (str.StartsWith("[IPC]", StringComparison.Ordinal))
                {
                    string command = str.Substring(5);
                    int endOfCommand = command.IndexOf(' ');
                    if (endOfCommand >= 0)
                    {
                        command = command.Substring(0, endOfCommand);
                    }

                    string param = str.Substring(endOfCommand + 5).Trim();
                    ipcCallback?.Invoke(command, param);
                }
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

            /*switch (command)
            {
                case "ERROR_REMOVED_FILE":
                    if (MEMI_FOUND)
                    {
                        BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Add("DIAG ERROR: File was removed after textures scan: " + param);
                    }
                    else
                    {
                        BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Add("File was removed after textures scan: " + param);
                    }
                    break;
                case "ERROR_ADDED_FILE":
                    if (MEMI_FOUND)
                    {
                        AddedFiles.Add(param.ToLower());
                        BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Add("DIAG ERROR: File was added after textures scan: " + param + " " + File.GetCreationTimeUtc(gamePath + param));
                    }
                    else
                    {
                        BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Add("File was added after textures scan: " + param + " " + File.GetCreationTimeUtc(gamePath + param));
                    }
                    break;
                case "ERROR_REFERENCED_TFC_NOT_FOUND":
                    BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Add("A referenced TFC was not found: " + param + ". See the next diagnostic message for additional info");
                    break;
                case "LODLINE":
                    int eqIndex = param.IndexOf('=');
                    string lodSetting = param.Substring(0, eqIndex);
                    string lodValue = "";
                    // if (eqIndex + 1 < param.Length - 1)
                    //{
                    lodValue = param.Substring(eqIndex + 1, param.Length - 1 - eqIndex); //not blank
                                                                                         //}
                                                                                         // param.Substring(eqIndex + 1, param.Length - 1);
                    LODS_INFO.Add(new KeyValuePair<string, string>(lodSetting, lodValue));
                    break;
                case "ERROR_VANILLA_MOD_FILE":
                    if (MEMI_FOUND)
                    {
                        string subpath = param;
                        if (!AddedFiles.Contains(subpath.ToLower()))
                        {
                            BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Add("File missing MEM/MEMNOGUI marker was found: " + subpath);
                        }
                    }
                    break;
                case "MOD":
                    BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Add("Detected mod: " + param);
                    break;
                case "TASK_PROGRESS":
                    int percentInt = Convert.ToInt32(param);
                    if (Context == CONTEXT_FULLMIPMAP_SCAN)
                    {
                        worker.ReportProgress(0, new ThreadCommand(SET_FULLSCAN_PROGRESS, percentInt));
                    }
                    else if (Context == CONTEXT_REPLACEDFILE_SCAN || Context == CONTEXT_FILEMARKER_SCAN)
                    {
                        worker.ReportProgress(0, new ThreadCommand(SET_REPLACEDFILE_PROGRESS, percentInt));
                    }
                    break;
                case "PROCESSING_FILE":
                    worker.ReportProgress(0, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, param));
                    break;
                case "ERROR_FILEMARKER_FOUND":
                    Log.Error("File that has ALOT modification marker was found: " + param);
                    BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Add("File has been previously modified by ALOT: " + param);
                    break;
                case "ERROR":
                    //will remove context switch if ERROR_FILEMARKER_FOUND is implemented
                    if (Context == CONTEXT_FILEMARKER_SCAN)
                    {
                        Log.Error("File that has ALOT modification marker was found: " + param);
                        BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Add("File has been previously modified by ALOT: " + param);
                    }
                    else
                    {
                        Log.Error("IPC ERROR: " + param);
                        BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Add(param);
                    }
                    break;
                case "ERROR_TEXTURE_SCAN_DIAGNOSTIC":
                    BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Add(param);
                    break;
                case "ERROR_MIPMAPS_NOT_REMOVED":
                    if (MEMI_FOUND)
                    {
                        BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Add(param);
                    }
                    break;
                case "ERROR_FILE_NOT_COMPATIBLE":
                    Log.Error("MEM reporting file is not compatible: " + param);
                    BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Add(param);
                    break;
                default:
                    Log.Information("Unknown IPC command: " + command);
                    break;
            }*/
        }

        public static string PerformDiagnostic(GameTarget selectedDiagnosticTarget, Action<string> updateStatusCallback = null)
        {
            updateStatusCallback?.Invoke("Preparing to collect diagnostic info");

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
            void setPercentDone(int pd) => updateStatusCallback?.Invoke($"Preparing MEM No GUI {pd}%");

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

            updateStatusCallback?.Invoke("Collecting game information");
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
                        diagStringBuilder.Append($@"~~~{message}");
                        break;
                    case Severity.ERROR:
                        diagStringBuilder.Append($@"[ERROR]{message}");
                        break;
                    case Severity.FATAL:
                        diagStringBuilder.Append($@"[FATAL]{message}");
                        break;
                    case Severity.SECTION:
                        diagStringBuilder.Append($@"==={message}");
                        break;
                    case Severity.GOOD:
                        diagStringBuilder.Append($@"$$${message}");
                        break;
                }
                diagStringBuilder.Append("\n");
            }


            //todo: massage this alot code to work with M3
            string gamePath = selectedDiagnosticTarget.TargetPath;
            addDiagLine($"ME3Tweaks Mod Manager {App.AppVersionHR} Game Diagnostic");
            addDiagLine($"Diagnostic for {Utilities.GetGameName(selectedDiagnosticTarget.Game)}");
            addDiagLine($"Diagnostic generated on {DateTime.Now.ToShortDateString()}");
            if (hasMEM)
            {
                var versInfo = FileVersionInfo.GetVersionInfo(mempath);
                int fileVersion = versInfo.FileMajorPart;
                addDiagLine($"Diagnostic MassEffectModderNoGui version: {fileVersion}");
            }
            else
            {
                addDiagLine("Mass Effect Modder No Gui was not available for use when this diagnostic was generated.", Severity.ERROR);
            }

            addDiagLine($"System culture: {CultureInfo.InstalledUICulture.Name}");

            updateStatusCallback?.Invoke("Collecting game information");
            addDiagLine("Basic game information", Severity.SECTION);
            addDiagLine($"Game is installed at {gamePath}");


            string pathroot = Path.GetPathRoot(gamePath);
            pathroot = pathroot.Substring(0, 1);
            if (pathroot == @"\")
            {
                addDiagLine("Installation appears to be on a network drive (first character in path is \\)", Severity.WARN);
            }
            else
            {
                if (Utilities.IsWindows10OrNewer())
                {
                    int backingType = GetPartitionDiskBackingType(pathroot);
                    string type = "Unknown type";
                    switch (backingType)
                    {
                        case 3: type = "Hard disk drive"; break;
                        case 4: type = "Solid state drive"; break;
                        default: type += ": " + backingType; break;
                    }
                    addDiagLine("Installed on disk type: " + type);
                }
            }

            try
            {
                ALOTVersionInfo avi = selectedDiagnosticTarget.GetInstalledALOTInfo();
                var texturesInstalled = avi != null;

                string exePath = MEDirectories.ExecutablePath(selectedDiagnosticTarget);
                if (File.Exists(exePath))
                {

                    var versInfo = FileVersionInfo.GetVersionInfo(exePath);
                    addDiagLine("Version: " + versInfo.FileMajorPart + "." + versInfo.FileMinorPart + "." + versInfo.FileBuildPart + "." + versInfo.FilePrivatePart);
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
                        addDiagLine($"Game source: {selectedDiagnosticTarget.GameSource}", Severity.GOOD);
                    }
                    else
                    {
                        addDiagLine($"Game source: Unknown/Unsupported - {selectedDiagnosticTarget.ExecutableHash}", Severity.FATAL);

                        //Authenticode
                        //var info = new FileInspector(exePath);
                        //var certOK = info.Validate();
                        //if (certOK == SignatureCheckResult.NoSignature)
                        //{
                        //    addDiagLine("[ERROR]This executable is not signed");
                        //}
                        //else
                        //{
                        //    if (certOK == SignatureCheckResult.BadDigest)
                        //    {
                        //        if (DIAGNOSTICS_GAME == 1 && versInfo.ProductName == "Mass_Effect")
                        //        {
                        //            //Check if this Mass_Effect
                        //            addDiagLine("Signature check for this executable skipped as MEM has modified this exe");
                        //        }
                        //        else
                        //        {
                        //            addDiagLine("[ERROR]The signature for this executable is not valid. The executable has been modified");
                        //            diagPrintSignatures(info);
                        //        }
                        //    }
                        //    else
                        //    {
                        //        addDiagLine("Signature check for this executable: " + certOK.ToString());
                        //        diagPrintSignatures(info);
                        //    }
                        //}
                    }

                    var exeDir = Path.GetDirectoryName(exePath);
                    var d3d9file = Path.Combine(exeDir, "d3d9.dll");
                    if (File.Exists(d3d9file))
                    {
                        addDiagLine("d3d9.dll exists - a dll is hooking the process (reshade?), may cause stability issues", Severity.WARN);
                    }
                    var fpscounter = Path.Combine(exeDir, @"fpscounter\fpscounter.dll");
                    if (File.Exists(fpscounter))
                    {
                        addDiagLine("fpscounter.dll exists - FPS Counter plugin detected, may cause stability issues", Severity.WARN);
                    }
                    var dinput8 = Path.Combine(exeDir, "dinput8.dll");
                    if (File.Exists(dinput8))
                    {
                        addDiagLine("dinput8.dll exists - a dll is hooking the process, may cause stability issues", Severity.WARN);
                    }
                }

                updateStatusCallback?.Invoke("Collecting system information");

                addDiagLine("System information", Severity.SECTION);
                OperatingSystem os = Environment.OSVersion;
                Version osBuildVersion = os.Version;

                //Windows 10 only
                string releaseId = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ReleaseId", "").ToString();
                string productName = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName", "").ToString();
                string verLine = "Running " + productName;
                if (osBuildVersion.Major == 10)
                {
                    verLine += " " + releaseId;
                }

                if (os.Version < App.MIN_SUPPORTED_OS)
                {
                    addDiagLine("This operating system is not supported", Severity.FATAL);
                    addDiagLine("Upgrade to a supported operating system if you want support", Severity.FATAL);
                }

                addDiagLine(verLine, os.Version < App.MIN_SUPPORTED_OS ? Severity.ERROR : Severity.INFO);
                addDiagLine("Version " + osBuildVersion, os.Version < App.MIN_SUPPORTED_OS ? Severity.ERROR : Severity.INFO);

                addDiagLine();
                addDiagLine("Processors");
                var computerInfo = new ComputerInfo();
                addDiagLine(GetProcessorInformationForDiag());
                long ramInBytes = (long)computerInfo.TotalPhysicalMemory;
                addDiagLine("System Memory: " + ByteSize.FromKiloBytes(ramInBytes));
                if (ramInBytes == 0)
                {
                    addDiagLine("Unable to get the read amount of physically installed ram. This may be a sign of impending hardware failure in the SMBIOS", Severity.WARN);
                }
                ManagementObjectSearcher objvide = new ManagementObjectSearcher("select * from Win32_VideoController");
                int vidCardIndex = 1;
                foreach (ManagementObject obj in objvide.Get())
                {
                    addDiagLine();
                    addDiagLine("Video Card " + vidCardIndex);
                    addDiagLine("Name: " + obj["Name"]);

                    //Get Memory
                    string vidKey = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\";
                    vidKey += (vidCardIndex - 1).ToString().PadLeft(4, '0');
                    object returnvalue = null;
                    try
                    {
                        returnvalue = Registry.GetValue(vidKey, "HardwareInformation.qwMemorySize", 0L);
                    }
                    catch (Exception ex)
                    {
                        addDiagLine($"Unable to read memory size from registry. Reading from WMI instead ({ex.GetType()})", Severity.WARN);
                    }

                    string displayVal;
                    if (returnvalue != null && (long)returnvalue != 0)
                    {
                        displayVal = ByteSize.FromBytes((long)returnvalue).ToString();
                    }
                    else
                    {
                        try
                        {
                            UInt32 wmiValue = (UInt32)obj["AdapterRam"];
                            displayVal = ByteSize.FromBytes((long)wmiValue).ToString();
                            if (displayVal == "4GB" || displayVal == "4 GB")
                            {
                                displayVal += " (possibly more, variable is 32-bit unsigned)";
                            }
                        }
                        catch (Exception)
                        {
                            displayVal = "Unable to read value from registry/WMI";

                        }
                    }
                    addDiagLine("Memory: " + displayVal);
                    addDiagLine("DriverVersion: " + obj["DriverVersion"]);
                    vidCardIndex++;
                }

                addDiagLine("Texture mod information", Severity.SECTION);
                if (avi == null)
                {
                    addDiagLine("The texture mod installation marker was not detected. No texture mods appear to be installed");
                }
                else
                {
                    if (avi.ALOTVER > 0 || avi.MEUITMVER > 0)
                    {
                        addDiagLine("ALOT Version: " + avi.ALOTVER + "." + avi.ALOTUPDATEVER + "." + avi.ALOTHOTFIXVER);
                        if (selectedDiagnosticTarget.Game == Mod.MEGame.ME1 && avi.MEUITMVER != 0)
                        {
                            addDiagLine("MEUITM version: " + avi.MEUITMVER);
                        }
                    }
                    else
                    {
                        addDiagLine("This installation has been texture modded, but ALOT and/or MEUITM has not been installed");
                    }

                    if (avi.ALOT_INSTALLER_VERSION_USED > 0)
                    {
                        addDiagLine("Latest installation was from ALOT Installer v" + avi.ALOT_INSTALLER_VERSION_USED);
                    }
                    addDiagLine("Latest installation used MEM v" + avi.MEM_VERSION_USED);
                }


                //Start diagnostics
                var gameID = selectedDiagnosticTarget.Game.ToString().Substring(2);

                if (hasMEM)
                {
                    string args = $"--check-game-data-mismatch --gameid {gameID} --ipc";
                    if (selectedDiagnosticTarget.TextureModded)
                    {
                        bool textureMapFileExists = File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + $@"\MassEffectModder\me{gameID}map.bin");
                        if (textureMapFileExists)
                        {
                            // check for replaced files (file size changes)
                            updateStatusCallback?.Invoke("Checking texture map <-> game consistency");
                            int? exitcode = null;
                            object memFinishedLock = new object();
                            List<string> removedFiles = new List<string>();
                            List<string> addedFiles = new List<string>();
                            List<string> replacedFiles = new List<string>();
                            void setExitCode(int? value) => exitcode = value;
                            void handleIPC(string command, string param)
                            {
                                switch (command)
                                {
                                    case "ERROR_REMOVED_FILE":
                                        removedFiles.Add($" - File removed after textures were installed: {param}");
                                        break;
                                    default:
                                        Debug.WriteLine("oof?");
                                        break;
                                }
                            }
                            runMassEffectModderNoGuiIPC(mempath, args, memFinishedLock, setExitCode, handleIPC);
                            lock (memFinishedLock)
                            {
                                Monitor.Wait(memFinishedLock);
                            }

                            if (removedFiles.Any())
                            {
                                addDiagLine("The following problems were detected checking game consistency with the texture map file:", Severity.ERROR);
                                foreach (var error in removedFiles)
                                {
                                    addDiagLine(error, Severity.ERROR);
                                }
                            }

                            addDiagLine("Files added or removed after texture mods were installed", Severity.SECTION);

                            //if (BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Count > 0)
                            //{
                            //    if (MEMI_FOUND)
                            //    {
                            //        addDiagLine("Diagnostic reports some files appear to have been added or removed since texture scan took place:");
                            //    }

                            //    foreach (String str in BACKGROUND_MEM_PROCESS_PARSED_ERRORS)
                            //    {
                            //        addDiagLine(" - " + str);
                            //    }


                            //}
                            //else
                            //{
                            //    addDiagLine("Diagnostic reports no files appear to have been added or removed since texture scan took place.");
                            //}

                            //diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_GREEN, Image_DataMismatch));
                        }
                        else
                        {
                            //addDiagLine("===Files added (or removed) after ALOT" + (DIAGNOSTICS_GAME == 1 ? "/MEUITM" : "") + " install");
                            //if (avi == null)
                            //{
                            //    addDiagLine("Texture map file is not present: me" + DIAGNOSTICS_GAME + "map.bin - MEMI tag missing so this is OK");

                            //}
                            //else
                            //{
                            //    addDiagLine("[ERROR] -  Texture map file is missing: me" + DIAGNOSTICS_GAME + "map.bin but MEMI tag is present - was game migrated to new system or on different user account?");
                            //}

                            //diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_RED, Image_DataMismatch));
                        }
                    }
                    //}
                    //else
                    //{
                    //    addDiagLine("===Files added (or removed) after ALOT" + (DIAGNOSTICS_GAME == 1 ? "/MEUITM" : "") + " install");
                    //    addDiagLine("MEMI tag was not found - ALOT/MEUITM not installed, skipping this check.");
                    //    diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_GREEN, Image_DataMismatch));
                    //}

                    /*if (!TextureCheck || MEMI_FOUND)
                    {
                        if (MEMI_FOUND)
                        {
                            args = "--check-game-data-after --gameid " + DIAGNOSTICS_GAME + " --ipc";
                        }
                        else
                        {
                            args = "--check-for-markers --gameid " + DIAGNOSTICS_GAME + " --ipc";
                        }

                        diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_WORKING, Image_DataAfter));
                        Context = MEMI_FOUND ? CONTEXT_REPLACEDFILE_SCAN : CONTEXT_FILEMARKER_SCAN;
                        runMEM_Diagnostics(exe, args, diagnosticsWorker);
                        WaitForMEM();
                        if (MEMI_FOUND)
                        {
                            addDiagLine("===Replaced files scan (after textures were installed)");
                            addDiagLine("This check will detect if files were replaced after textures were installed in an unsupported manner.");
                            addDiagLine("");
                        }
                        else
                        {
                            addDiagLine("===Preinstallation file scan");
                            addDiagLine("This check will make sure all files can be opened for reading and that files that were previously modified by ALOT are not installed.");
                            addDiagLine("");
                        }

                        if (BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Count > 0)
                        {

                            if (MEMI_FOUND)
                            {
                                addDiagLine("[ERROR]Diagnostic reports some files appear to have been added or replaced after ALOT was installed, or could not be read:");
                            }
                            else
                            {
                                addDiagLine("[ERROR]The following files did not pass the modification marker check, or could not be read:");
                            }

                            int numSoFar = 0;
                            foreach (String str in BACKGROUND_MEM_PROCESS_PARSED_ERRORS)
                            {
                                addDiagLine("[ERROR] - " + str);
                                numSoFar++;
                                if (numSoFar == 10 && BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Count() > 10)
                                {
                                    addDiagLine("[SUB]");
                                }
                            }

                            if (numSoFar > 10)
                            {
                                addDiagLine("[/SUB]");
                            }

                            if (MEMI_FOUND)
                            {
                                addDiagLine("[ERROR]Files added or replaced after ALOT has been installed is not supported due to the way the Unreal Engine 3 works.");
                            }
                            else
                            {
                                addDiagLine("[ERROR]Files that were previously modified by ALOT are most times broken or leftover from a previous ALOT failed installation that did not complete and set the ALOT installation marker.");
                                addDiagLine("[ERROR]Delete your game installation and reinstall the game, or restore from your backup in the ALOT settings.");
                            }

                            if (BACKGROUND_MEM_PROCESS.ExitCode == null || BACKGROUND_MEM_PROCESS.ExitCode != 0)
                            {
                                pairLog = true;
                                addDiagLine("[ERROR]MEMNoGui returned non zero exit code, or null (crash) during --check-game-data-after. Some data was returned. The return code was: " + BACKGROUND_MEM_PROCESS.ExitCode);
                            }
                        }
                        else
                        {
                            if (BACKGROUND_MEM_PROCESS.ExitCode != null && BACKGROUND_MEM_PROCESS.ExitCode == 0)
                            {
                                if (MEMI_FOUND)
                                {
                                    addDiagLine("Diagnostic did not find any files that were added or replaced after ALOT installation or have issues reading files.");
                                }
                                else
                                {
                                    addDiagLine("Diagnostic did not find any files from previous installations of ALOT or have issues reading files.");
                                }
                            }
                            else
                            {
                                pairLog = true;
                                addDiagLine("[ERROR]MEMNoGui returned non zero exit code, or null (crash) during --check-game-data-after: " + BACKGROUND_MEM_PROCESS.ExitCode);
                            }
                        }

                        diagnosticsWorker.ReportProgress(0, new ThreadCommand(RESET_REPLACEFILE_TEXT));
                        diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_GREEN, Image_DataAfter));
                    }

                    Context = CONTEXT_NORMAL;

                    //FULL CHECK
                    if (TextureCheck)
                    {
                        addDiagLine("===Full Textures Check");
                        diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_WORKING, Image_FullCheck));
                        diagnosticsWorker.ReportProgress(0, new ThreadCommand(TURN_ON_TASKBAR_PROGRESS));
                        args = "--check-game-data-textures --gameid " + DIAGNOSTICS_GAME + " --ipc";
                        Context = CONTEXT_FULLMIPMAP_SCAN;
                        runMEM_Diagnostics(exe, args, diagnosticsWorker);
                        WaitForMEM();
                        Context = CONTEXT_NORMAL;

                        if (BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Count > 0)
                        {
                            addDiagLine("Full texture check reported errors:");

                            int numSoFar = 0;
                            foreach (string str in BACKGROUND_MEM_PROCESS_PARSED_ERRORS)
                            {
                                addDiagLine("[ERROR] -  " + str);
                                numSoFar++;
                                if (numSoFar == 10 && BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Count() > 10)
                                {
                                    addDiagLine("[SUB]");
                                }
                            }

                            if (numSoFar > 10)
                            {
                                addDiagLine("[/SUB]");
                            }

                            if (BACKGROUND_MEM_PROCESS.ExitCode == null || BACKGROUND_MEM_PROCESS.ExitCode != 0)
                            {
                                pairLog = true;
                                addDiagLine("[ERROR]MEMNoGui returned non zero exit code, or null (crash) during --check-game-data-textures. Some data was returned. The return code was: " + BACKGROUND_MEM_PROCESS.ExitCode);
                            }
                        }
                        else
                        {
                            if (BACKGROUND_MEM_PROCESS.ExitCode != null && BACKGROUND_MEM_PROCESS.ExitCode == 0)
                            {
                                addDiagLine("Diagnostics textures check (full) did not find any issues.");
                            }
                            else
                            {
                                pairLog = true;
                                addDiagLine("[ERROR]MEMNoGui returned non zero exit code, or null (crash) during --check-game-data-textures: " + BACKGROUND_MEM_PROCESS.ExitCode);
                            }
                        }

                        diagnosticsWorker.ReportProgress(0, new ThreadCommand(TURN_OFF_TASKBAR_PROGRESS));
                        diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_GREEN, Image_FullCheck));
                    }*/
                }

                addDiagLine("Basegame changes", Severity.SECTION);

                updateStatusCallback?.Invoke("Collecting basegame file modifications");
                List<string> modifiedFiles = new List<string>();
                void failedCallback(string file)
                {
                    modifiedFiles.Add(file);
                }

                var isVanilla = VanillaDatabaseService.ValidateTargetAgainstVanilla(selectedDiagnosticTarget, failedCallback);
                if (isVanilla)
                {
                    addDiagLine("No modified basegame files were found.");
                }
                else
                {
                    if (!selectedDiagnosticTarget.TextureModded)
                    {
                        addDiagLine("The following basegame files have been modified:");
                        var cookedPath = MEDirectories.CookedPath(selectedDiagnosticTarget);
                        foreach (var mf in modifiedFiles)
                        {
                            if (mf.StartsWith(cookedPath, StringComparison.InvariantCultureIgnoreCase))
                            {
                                var info = BasegameFileIdentificationService.GetBasegameFileSource(selectedDiagnosticTarget, mf);
                                if (info != null)
                                {
                                    addDiagLine($" - {mf.Substring(cookedPath.Length + 1)} - {info.source}");
                                }
                                else
                                {
                                    addDiagLine($" - {mf.Substring(cookedPath.Length + 1)}");
                                }
                            }
                        }
                    }
                    else
                    {
                        //Check MEMI markers?

                    }
                }

                //Thread.Sleep(1000000);
                /*
                //addDiagLine("If ALOT was installed, detection of mods in this block means you installed items after ALOT was installed, which will break the game.");

                args = "--detect-mods --gameid " + DIAGNOSTICS_GAME + " --ipc";
                diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_WORKING, Image_DataBasegamemods));
                runMEM_Diagnostics(exe, args, diagnosticsWorker);
                WaitForMEM();
                addDiagLine("");

                string prefix = "";
                if (MEMI_FOUND)
                {
                    prefix = "[ERROR] -  ";
                }
                if (BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Count > 0)
                {
                    if (MEMI_FOUND)
                    {
                        addDiagLine("[ERROR]The following basegame mods were detected:");
                    }
                    else
                    {
                        addDiagLine("The following basegame mods were detected:");
                    }
                    foreach (String str in BACKGROUND_MEM_PROCESS_PARSED_ERRORS)
                    {
                        addDiagLine(prefix + " - " + str);
                    }
                    if (MEMI_FOUND)
                    {
                        addDiagLine("[ERROR]These mods appear to be installed after ALOT. This will break the game. Follow the directions for ALOT to avoid this issue.");
                    }
                }
                else
                {
                    addDiagLine("Diagnostics did not detect any known basegame mods (--detect-mods).");
                }

                args = "--detect-bad-mods --gameid " + DIAGNOSTICS_GAME + " --ipc";
                runMEM_Diagnostics(exe, args, diagnosticsWorker);
                WaitForMEM();
                if (BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Count > 0)
                {
                    addDiagLine("Diagnostic reports the following incompatible mods are installed:");
                    foreach (String str in BACKGROUND_MEM_PROCESS_PARSED_ERRORS)
                    {
                        addDiagLine("[ERROR] -  " + str);
                    }
                }
                else
                {
                    addDiagLine("Diagnostic did not detect any known incompatible mods.");
                }
                */
                //Get DLCs
                updateStatusCallback?.Invoke("Collecting DLC information");

                var installedDLCs = MEDirectories.GetInstalledDLC(selectedDiagnosticTarget);

                addDiagLine("Installed DLC", Severity.SECTION);
                addDiagLine("The following DLC is installed:");

                bool metadataPresent = false;
                bool hasUIMod = false;
                bool compatPatchInstalled = false;
                bool hasNonUIDLCMod = false;
                Dictionary<int, string> priorities = new Dictionary<int, string>();
                foreach (string dlc in installedDLCs)
                {
                    addDiagLine(" - " + dlc);
                }
                /*


                bool isCompatPatch = false;
                    string value = Path.GetFileName(dir);
                    if (value == "__metadata")
                    {
                        metadataPresent = true;
                        continue;
                    }
                    long sfarsize = 0;
                    long propersize = 32L;
                    long me3expUnpackedSize = GetME3ExplorerUnpackedSFARSize(value);
                    bool hasSfarSizeErrorMemiFound = false;
                    string duplicatePriorityStr = "";
                    if (DIAGNOSTICS_GAME == 3)
                    {
                        //check for ISM/Controller patch
                        int mountpriority = GetDLCPriority(dir);
                        if (mountpriority != -1)
                        {
                            if (priorities.ContainsKey(mountpriority))
                            {
                                duplicatePriorityStr = priorities[mountpriority];
                            }
                            else
                            {
                                priorities[mountpriority] = value;
                            }
                        }
                        if (mountpriority == 31050)
                        {
                            compatPatchInstalled = isCompatPatch = true;
                        }
                        if (value != "DLC_CON_XBX" && value != "DLC_CON_UIScaling" && value != "DLC_CON_UIScaling_Shared" && InteralGetDLCName(value) == null)
                        {
                            hasNonUIDLCMod = true;
                        }
                        if (value == "DLC_CON_XBX" || value == "DLC_CON_UIScaling" || value == "DLC_CON_UIScaling_Shared")
                        {
                            hasUIMod = true;
                        }

                        //Check for SFAR size not being 32 bytes
                        string sfar = Path.Combine(dir, "CookedPCConsole", "Default.sfar");
                        string unpackedDir = Path.Combine(dir, "CookedPCConsole");
                        if (File.Exists(sfar))
                        {
                            FileInfo fi = new FileInfo(sfar);
                            sfarsize = fi.Length;
                            hasSfarSizeErrorMemiFound = sfarsize != propersize;

                            var filesInSfarDir = Directory.EnumerateFiles(unpackedDir).ToList();
                            var hasUnpackedFiles = filesInSfarDir.Any(d => unpackedFileExtensions.Contains(Path.GetExtension(d.ToLower())));
                            var officialPackedSize = GetPackedSFARSize(value);
                            if (hasSfarSizeErrorMemiFound && MEMI_FOUND)
                            {
                                if (me3expUnpackedSize == sfarsize)
                                {
                                    addDiagLine("~~~" + GetDLCDisplayString(value, isCompatPatch ? "[MOD] UI Mod Compatibilty Pack" : null) + " - ME3Explorer mainline unpacked");
                                    addDiagLine("[ERROR]      SFAR has been unpacked with ME3Explorer. SFAR unpacking with ME3Explorer is extremely slow and prone to failure. Do not unpack your DLC with ME3Explorer.");
                                    if (HASH_SUPPORTED)
                                    {
                                        addDiagLine("[ERROR]      If you used ME3Explorer for AutoTOC, you can use the one in ALOT Installer by going to Settings -> Game Utilities -> AutoTOC.");
                                    }
                                }
                                else if (sfarsize >= officialPackedSize && officialPackedSize != 0 && hasUnpackedFiles)
                                {
                                    addDiagLine("[FATAL]" + GetDLCDisplayString(value, isCompatPatch ? "[MOD] UI Mod Compatibilty Pack" : null) + " - Packed with unpacked files");
                                    addDiagLine("[ERROR]      SFAR is not unpacked, but directory contains unpacked files. This DLC was unpacked and then restored.");
                                    addDiagLine("[ERROR]      This DLC is in an inconsistent state. The game must be restored from backup or deleted. Once reinstalled, ALOT must be installed again.");
                                }
                                else if (officialPackedSize == sfarsize)
                                {
                                    addDiagLine("[FATAL]" + GetDLCDisplayString(value, isCompatPatch ? "[MOD] UI Mod Compatibilty Pack" : null) + " - Packed");
                                    addDiagLine("[ERROR]      SFAR is not unpacked. This DLC was either installed after ALOT was installed or was attempted to be repaired by Origin.");
                                    addDiagLine("[ERROR]      The game must be restored from backup or deleted. Once reinstalled, ALOT must be installed again.");
                                }
                                else
                                {
                                    addDiagLine("~~~" + GetDLCDisplayString(value, isCompatPatch ? "[MOD] UI Mod Compatibilty Pack" : null) + " - Unknown SFAR size");
                                    addDiagLine("[ERROR]      SFAR is not the MEM unpacked size, ME3Explorer unpacked size, or packed size. This SFAR is " + ByteSize.FromBytes(sfarsize) + " bytes.");
                                }
                            }
                            else
                            {
                                //ALOT not detected
                                if (me3expUnpackedSize == sfarsize)
                                {
                                    addDiagLine("~~~" + GetDLCDisplayString(value, isCompatPatch ? "[MOD] UI Mod Compatibilty Pack" : null) + " - ME3Explorer mainline unpacked");
                                    addDiagLine("[ERROR]      SFAR has been unpacked with ME3Explorer. SFAR unpacking with ME3Explorer is extremely slow and prone to failure. Do not unpack your DLC with ME3Explorer.");
                                    if (HASH_SUPPORTED)
                                    {
                                        addDiagLine("[ERROR]      If you used ME3Explorer for AutoTOC, you can use the one in ALOT Installer by going to Settings -> Game Utilities -> AutoTOC.");
                                    }
                                }
                                else if (sfarsize >= officialPackedSize && officialPackedSize != 0 && hasUnpackedFiles)
                                {
                                    addDiagLine("[FATAL]" + GetDLCDisplayString(value, isCompatPatch ? "[MOD] UI Mod Compatibilty Pack" : null) + " - Packed with unpacked files");
                                    addDiagLine("[ERROR]      SFAR is not unpacked, but directory contains unpacked files. This DLC was unpacked and then restored.");
                                    addDiagLine("[ERROR]      This DLC is in an inconsistent state. The game must be restored from backup or deleted. Once reinstalled, ALOT must be installed again.");
                                }
                                else if (sfarsize >= officialPackedSize && officialPackedSize != 0)
                                {
                                    addDiagLine(GetDLCDisplayString(value) + " - Packed");
                                }
                                else
                                {
                                    addDiagLine(GetDLCDisplayString(value, isCompatPatch ? "[MOD] UI Mod Compatibility Pack" : null));
                                }
                            }
                        }
                        else
                        {
                            //SFAR MISSING
                            addDiagLine("~~~" + GetDLCDisplayString(value) + " - SFAR missing");
                        }
                    }
                    else
                    {
                        addDiagLine(GetDLCDisplayString(value));
                    }

                    if (duplicatePriorityStr != "")
                    {
                        addDiagLine("[ERROR] - This DLC has the same mount priority as another DLC: " + duplicatePriorityStr);
                        addDiagLine("[ERROR]   These conflicting DLCs will likely encounter issues as the game will not know which files should be used");
                    }
                }

                /*
                if (DIAGNOSTICS_GAME == 3)
                {
                    if (hasUIMod && hasNonUIDLCMod && compatPatchInstalled)
                    {
                        addDiagLine("This installation requires a UI compatibility patch. This patch appears to be installed.");
                    }
                    else if (hasUIMod && hasNonUIDLCMod && !compatPatchInstalled)
                    {
                        addDiagLine("~~~This installation may require a UI compatibility patch from Mass Effect 3 Mod Manager due to installation of a UI mod with other mods.");
                        addDiagLine("~~~In Mass Effect 3 Mod Manager use Mod Management > Check for Custom DLC conflicts to see if you need one.");
                    }
                    else if (!hasUIMod && compatPatchInstalled)
                    {
                        addDiagLine("[ERROR] -  This installation does not require a UI compatibilty patch but one is installed. This may lead to game crashing.");
                    }

                    if (metadataPresent)
                    {
                        addDiagLine("__metadata folder is present");
                    }
                    else
                    {
                        addDiagLine("~~~__metadata folder is missing");
                    }
                }
            }
            else
            {
                if (DIAGNOSTICS_GAME == 3)
                {
                    addDiagLine("[ERROR]DLC directory is missing: " + dlcPath + ". Mass Effect 3 always has a DLC folder so this should not be missing.");
                }
                else
                {
                    addDiagLine("DLC directory is missing: " + dlcPath + ". If no DLC is installed, this folder will be missing.");
                }
            }*/

                if (selectedDiagnosticTarget.Game > Mod.MEGame.ME1)
                {
                    updateStatusCallback?.Invoke("Collecting TFC file information");

                    addDiagLine("Texture File Cache (TFC) files", Severity.SECTION);
                    addDiagLine("The following TFC files are present in the game directory.");
                    var bgPath = MEDirectories.BioGamePath(selectedDiagnosticTarget);
                    string[] tfcFiles = Directory.GetFiles(bgPath, "*.tfc", SearchOption.AllDirectories);
                    if (tfcFiles.Any())
                    {
                        foreach (string tfc in tfcFiles)
                        {
                            FileInfo fi = new FileInfo(tfc);
                            long tfcSize = fi.Length;
                            string tfcPath = tfc.Substring(bgPath.Length + 1);
                            addDiagLine($" - {tfcPath}, {ByteSize.FromBytes(tfcSize)}");
                        }
                    }
                    else
                    {
                        addDiagLine("No TFC files were found - is this installation broken?", Severity.ERROR);
                    }
                }

                updateStatusCallback?.Invoke("Collecting ASI file information");

                string asidir = MEDirectories.ASIPath(selectedDiagnosticTarget);
                addDiagLine("Installed ASI mods", Severity.SECTION);
                if (Directory.Exists(asidir))
                {
                    addDiagLine("The follow files are located in the ASI directory:");
                    string[] files = Directory.GetFiles(asidir, "*.asi");
                    if (!files.Any())
                    {
                        addDiagLine("ASI directory is empty. No ASI mods are installed.");
                    }
                    else
                    {
                        foreach (string f in files)
                        {
                            addDiagLine(" - " + Path.GetFileName(f));
                        }
                    }
                }
                else
                {
                    addDiagLine("ASI directory does not exist. No ASI mods are installed.");
                }
                //TOC SIZE CHECK
                if (selectedDiagnosticTarget.Game == Mod.MEGame.ME3)
                {
                    updateStatusCallback?.Invoke("Collecting TOC file information");

                    addDiagLine("File Table of Contents (TOC) size check", Severity.SECTION);
                    addDiagLine("PCConsoleTOC.bin files list the size of each file the game can load.");
                    addDiagLine("If the size is smaller than the actual file, the game will not allocate enough memory to load the file.");
                    addDiagLine("These hangs typically occur at loading screens and are the result of manually modifying files without running AutoTOC afterwards.");
                    bool hadTocError = false;
                    string[] tocs = Directory.GetFiles(Path.Combine(gamePath, "BIOGame"), "PCConsoleTOC.bin", SearchOption.AllDirectories);
                    string markerfile = MEDirectories.ALOTMarkerPath(selectedDiagnosticTarget);
                    foreach (string toc in tocs)
                    {
                        TOCBinFile tbf = new TOCBinFile(toc);
                        foreach (TOCBinFile.Entry ent in tbf.Entries)
                        {
                            //Console.WriteLine(index + "\t0x" + ent.offset.ToString("X6") + "\t" + ent.size + "\t" + ent.name);
                            string filepath = Path.Combine(gamePath, ent.name);
                            if (File.Exists(filepath) && !filepath.Equals(markerfile, StringComparison.InvariantCultureIgnoreCase) && !filepath.ToLower().EndsWith("pcconsoletoc.bin"))
                            {
                                FileInfo fi = new FileInfo(filepath);
                                long size = fi.Length;
                                if (ent.size < size)
                                {
                                    addDiagLine("-  " + filepath + " size is " + size + ", but TOC lists " + ent.size + " (" + (ent.size - size) + " bytes)", Severity.ERROR);
                                    hadTocError = true;
                                }
                            }
                        }
                    }
                    if (!hadTocError)
                    {
                        addDiagLine("All TOC files passed check. No files have a size larger than the TOC size.");
                    }
                    else
                    {
                        addDiagLine("Some files are larger than the listed TOC size. This typically won't happen unless you manually installed some files or an ALOT installation failed.", Severity.ERROR);
                        addDiagLine("The game will always hang while loading these files." + (selectedDiagnosticTarget.Supported ? " You can regenerate the TOC files by using AutoTOC from the tools menu. If installation failed due a crash, this won't fix it." : ""));
                    }
                }

                //Get LODs
                if (hasMEM)
                {
                    updateStatusCallback?.Invoke("Collecting LOD settings");
                    string args = $"--print-lods --gameid {gameID} --ipc";
                    int? exitCode = null;
                    object memFinishedLock = new object();
                    var lods = new Dictionary<string, string>();
                    void handleIPC(string command, string param)
                    {
                        switch (command)
                        {
                            case "LODLINE":
                                var lodSplit = param.Split("=");
                                lods[lodSplit[0]] = param.Substring(lodSplit[0].Length + 1);
                                break;
                            default:
                                Debug.WriteLine("oof?");
                                break;
                        }
                    }
                    runMassEffectModderNoGuiIPC(mempath, args, memFinishedLock, i => exitCode = i, handleIPC);
                    lock (memFinishedLock)
                    {
                        Monitor.Wait(memFinishedLock);
                    }

                    addLODStatusToDiag(selectedDiagnosticTarget, lods, addDiagLine);
                }

                //ME1: LOGS
                if (selectedDiagnosticTarget.Game == Mod.MEGame.ME1)
                {
                    updateStatusCallback?.Invoke("Collecting ME1 application logs");

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
                                if (line.Contains("Critical: appError called"))
                                {
                                    crashLineNumber = currentLineNumber;
                                    reason = "Log file indicates crash occured";
                                    Log.Information("Found crash in ME1 log " + file.Name + " on line " + currentLineNumber);
                                    break;
                                }
                                currentLineNumber++;
                            }

                            if (crashLineNumber >= 0)
                            {
                                crashLineNumber = Math.Max(0, crashLineNumber - 10); //show last 10 lines of log leading up to the crash
                                //this log has a crash
                                addDiagLine("Mass Effect game log " + file.Name, Severity.SECTION);
                                if (reason != "") addDiagLine(reason);
                                if (crashLineNumber > 0)
                                {
                                    addDiagLine("[CRASHLOG]...");
                                }
                                for (int i = crashLineNumber; i < logLines.Length; i++)
                                {
                                    addDiagLine("[CRASHLOG]" + logLines[i]);
                                }
                            }
                        }
                    }
                }

                //EVENT LOGS
                updateStatusCallback?.Invoke("Collecting event logs");
                StringBuilder crashLogs = new StringBuilder();
                var sevenDaysAgo = DateTime.Now.AddDays(-3);

                //Get event logs
                EventLog ev = new EventLog("Application");
                List<EventLogEntry> entries = ev.Entries
                    .Cast<EventLogEntry>()
                    .Where(z => z.InstanceId == 1001 && z.TimeGenerated > sevenDaysAgo && (GenerateEventLogString(z).ContainsAny(MEDirectories.ExecutableNames(selectedDiagnosticTarget.Game), StringComparison.InvariantCultureIgnoreCase)))
                    .ToList();

                addDiagLine($"{Utilities.GetGameName(selectedDiagnosticTarget.Game)} crash logs found in Event Viewer", Severity.SECTION);
                if (entries.Any())
                {
                    foreach (var entry in entries)
                    {
                        string str = string.Join("\n", GenerateEventLogString(entry).Split('\n').ToList().Take(17).ToList());
                        addDiagLine($"{Utilities.GetGameName(selectedDiagnosticTarget.Game)} Event {entry.TimeGenerated}\n{str}"); // !!! ?
                    }

                }
                else
                {
                    addDiagLine("No crash events found in Event Viewer");
                }

                //    return crashLogs.ToString();


                //}


                if (selectedDiagnosticTarget.Game == Mod.MEGame.ME3)
                {
                    string me3logfilepath = Path.Combine(Directory.GetParent(MEDirectories.ExecutablePath(selectedDiagnosticTarget)).FullName, "me3log.txt");
                    if (File.Exists(me3logfilepath))
                    {

                        FileInfo fi = new FileInfo(me3logfilepath);
                        if (fi.Length < 10000)
                        {
                            addDiagLine("Mass Effect 3 last session log", Severity.SECTION);
                            addDiagLine("Last session log has modification date of " + fi.LastWriteTimeUtc.ToShortDateString());
                            addDiagLine();
                            var log = Utilities.WriteSafeReadAllLines(me3logfilepath); //try catch needed?
                            foreach (string line in log)
                            {
                                addDiagLine(line);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                addDiagLine("Exception occured while running diagnostic.", Severity.ERROR);
                addDiagLine(App.FlattenException(ex));
                return diagStringBuilder.ToString();

            }

            return diagStringBuilder.ToString();
        }

        private static string GenerateEventLogString(EventLogEntry entry) => $"Event type: {entry.EntryType}\nEvent Message: {entry.Message + entry}\nEvent Time: {entry.TimeGenerated.ToShortTimeString()}\nEvent {entry.UserName}\n";

        private static void addLODStatusToDiag(GameTarget selectedDiagnosticTarget, Dictionary<string, string> lods, Action<string, Severity> addDiagLine)
        {
            addDiagLine("Texture Level of Detail (LOD) settings", Severity.SECTION);
            string iniPath = MEDirectories.LODConfigFile(selectedDiagnosticTarget.Game);
            if (!File.Exists(iniPath))
            {
                addDiagLine($"Game config file is missing: {iniPath}", Severity.ERROR);
                return;
            }

            foreach (KeyValuePair<string, string> kvp in lods)
            {
                addDiagLine($@"{kvp.Key}={kvp.Value}", Severity.INFO);
            }
            var textureChar1024 = lods.FirstOrDefault(x => x.Key == "TEXTUREGROUP_Character_1024");
            if (string.IsNullOrWhiteSpace(textureChar1024.Key)) //does this work for ME2/ME3??
            {
                //not found
                addDiagLine("Could not find TEXTUREGROUP_Character_1024 in config file for checking LOD settings", Severity.ERROR);
                return;
            }

            try
            {
                int maxLodSize = 0;
                if (!string.IsNullOrWhiteSpace(textureChar1024.Value))
                {
                    //ME2,3 default to blank
                    maxLodSize = int.Parse(StringStructParser.GetCommaSplitValues(textureChar1024.Value)[selectedDiagnosticTarget.Game == Mod.MEGame.ME1 ? "MinLODSize" : "MaxLODSize"]);
                }

                var HQLine = "High quality texture LOD settings appear to be set";
                var HQSettingsMissingLine = "High quality texture LOD settings appear to be missing, but a high resolution texture mod appears to be installed. The game will not use these new high quality assets - config file was probably deleted or texture quality settings were changed in game";
                var HQVanillaLine = "High quality LOD settings are not set and no high quality texture mod is installed";
                switch (selectedDiagnosticTarget.Game)
                {
                    case Mod.MEGame.ME1:
                        if (maxLodSize != 1024) //ME1 Default
                        {
                            //Not Default
                            if (selectedDiagnosticTarget.TextureModded)
                            {
                                addDiagLine(HQVanillaLine, Severity.INFO);
                            }
                            else if (maxLodSize > 1024)
                            {
                                addDiagLine("Texture LOD settings appear to have been raised, but this installation has not been texture modded - game will likely have unused mip crashes.", Severity.FATAL);
                                //log = ShowBadLODDialog(log);
                            }
                        }
                        else
                        {
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
                                    addDiagLine("LOD quality settings: 4K textures", Severity.INFO);
                                }
                                else if (maxLodSize == 2048)
                                {
                                    addDiagLine("LOD quality settings: 2K textures", Severity.INFO);
                                }
                            }
                            else //not vanilla, but no MEM/MEUITM
                            {
                                addDiagLine(HQSettingsMissingLine, Severity.ERROR);

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
                Log.Error("Error checking LOD settings: " + e.Message);
                addDiagLine($"Error checking LOD settings: {e.Message}", Severity.INFO);
            }
        }
    }
}
