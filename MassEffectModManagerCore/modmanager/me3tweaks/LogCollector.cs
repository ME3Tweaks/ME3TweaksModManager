

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
            lock (memEnsuredSignaler)
            {
                Monitor.Wait(memEnsuredSignaler);
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
            bool pairLog = false;
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
                        addDiagLine($"Game source: {selectedDiagnosticTarget.GameSource} - {selectedDiagnosticTarget.ExecutableHash}", Severity.GOOD);
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
                        addDiagLine("~~~fpscounter.dll exists - FPS Counter plugin detected, may cause stability issues", Severity.WARN);
                    }
                    var dinput8 = Path.Combine(exeDir, "dinput8.dll");
                    if (File.Exists(dinput8))
                    {
                        addDiagLine("dinput8.dll exists - a dll is hooking the process, may cause stability issues", Severity.WARN);
                    }
                }
                

                addDiagLine("===System information");
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
                /*
                if (os.Version < App.MIN_SUPPORTED_OS)
                {
                    addDiagLine("[FATAL]This operating system is not supported");
                    addDiagLine("[FATAL]Upgrade to a supported operating system if you want support");
                }

                addDiagLine(os.Version < App.MIN_SUPPORTED_OS ? "[ERROR]" + verLine : verLine);
                if (os.Version < App.MIN_SUPPORTED_OS)
                {
                    addDiagLine("[ERROR]Version " + osBuildVersion);
                }
                else
                {
                    addDiagLine("Version " + osBuildVersion);
                }
                addDiagLine("");
                addDiagLine("Processors");
                addDiagLine(Utilities.GetCPUString());
                long ramInBytes = Utilities.GetInstalledRamAmount(); // use https://github.com/NickStrupat/ComputerInfo
                addDiagLine("System Memory: " + ByteSize.FromKiloBytes(ramInBytes));
                if (ramInBytes == 0)
                {
                    addDiagLine("~~~Unable to get the read amount of physically installed ram. This may be a sign of impending hardware failure in the SMBIOS");
                }
                ManagementObjectSearcher objvide = new ManagementObjectSearcher("select * from Win32_VideoController");
                int vidCardIndex = 1;
                foreach (ManagementObject obj in objvide.Get())
                {
                    addDiagLine("");
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
                        addDiagLine("~~~Warning: Unable to read memory size from registry. Reading from WMI instead (" + ex.GetType().ToString() + ")");
                    }
                    string displayVal = "Unable to read value from registry";
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


                /*
                addDiagLine("===Latest MEMI Marker Information");
                if (avi == null)
                {
                    if (DIAGNOSTICS_GAME != 1)
                    {
                        addDiagLine("The ALOT installation marker was not detected. ALOT is not installed.");
                    }
                    else
                    {
                        addDiagLine("The ALOT installation marker was not detected. ALOT and MEUITM are not installed.");
                    }
                }
                else
                {
                    addDiagLine("ALOT Version: " + avi.ALOTVER + "." + avi.ALOTUPDATEVER + "." + avi.ALOTHOTFIXVER);
                    if (DIAGNOSTICS_GAME == 1)
                    {
                        addDiagLine("MEUITM: " + avi.MEUITMVER);
                    }
                    addDiagLine("Latest installation used MEM v" + avi.MEM_VERSION_USED);
                }


                //Start diagnostics
                string exe = BINARY_DIRECTORY + MEM_EXE_NAME;
                string args = "--check-game-data-mismatch --gameid " + DIAGNOSTICS_GAME + " --ipc";
                if (MEMI_FOUND)
                {
                    bool textureMapFileExists = File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\MassEffectModder\me" + DIAGNOSTICS_GAME + "map.bin");
                    if (textureMapFileExists)
                    {
                        diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_WORKING, Image_DataMismatch));
                        runMEM_Diagnostics(exe, args, diagnosticsWorker);
                        WaitForMEM();
                        addDiagLine("===Files added (or removed) after ALOT" + (DIAGNOSTICS_GAME == 1 ? "/MEUITM" : "") + " install");

                        if (BACKGROUND_MEM_PROCESS_PARSED_ERRORS.Count > 0)
                        {
                            if (MEMI_FOUND)
                            {
                                addDiagLine("Diagnostic reports some files appear to have been added or removed since texture scan took place:");
                            }
                            foreach (String str in BACKGROUND_MEM_PROCESS_PARSED_ERRORS)
                            {
                                addDiagLine(" - " + str);
                            }


                        }
                        else
                        {
                            addDiagLine("Diagnostic reports no files appear to have been added or removed since texture scan took place.");
                        }
                        diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_GREEN, Image_DataMismatch));
                    }
                    else
                    {
                        addDiagLine("===Files added (or removed) after ALOT" + (DIAGNOSTICS_GAME == 1 ? "/MEUITM" : "") + " install");
                        if (avi == null)
                        {
                            addDiagLine("Texture map file is not present: me" + DIAGNOSTICS_GAME + "map.bin - MEMI tag missing so this is OK");

                        }
                        else
                        {
                            addDiagLine("[ERROR] -  Texture map file is missing: me" + DIAGNOSTICS_GAME + "map.bin but MEMI tag is present - was game migrated to new system or on different user account?");
                        }
                        diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_RED, Image_DataMismatch));
                    }
                }
                //}
                //else
                //{
                //    addDiagLine("===Files added (or removed) after ALOT" + (DIAGNOSTICS_GAME == 1 ? "/MEUITM" : "") + " install");
                //    addDiagLine("MEMI tag was not found - ALOT/MEUITM not installed, skipping this check.");
                //    diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_GREEN, Image_DataMismatch));
                //}

                if (!TextureCheck || MEMI_FOUND)
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
                }

                addDiagLine("===Basegame mods");
                addDiagLine("Items in this block are only accurate if ALOT is not installed or items have been installed after ALOT.");
                addDiagLine("If ALOT was installed, detection of mods in this block means you installed items after ALOT was installed, which will break the game.");

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

                //Get DLCs
                var dlcPath = gamePath;
                switch (DIAGNOSTICS_GAME)
                {
                    case 1:
                        dlcPath = Path.Combine(dlcPath, "DLC");
                        break;
                    case 2:
                    case 3:
                        dlcPath = Path.Combine(dlcPath, "BIOGame", "DLC");
                        break;
                }

                addDiagLine("===Installed DLC");
                addDiagLine("The following folders are present in the DLC directory:");
                var unpackedFileExtensions = new List<string>() { ".pcc", ".tlk", ".bin", ".dlc" };

                if (Directory.Exists(dlcPath))
                {

                    var directories = Directory.EnumerateDirectories(dlcPath);
                    bool metadataPresent = false;
                    bool hasUIMod = false;
                    bool compatPatchInstalled = false;
                    bool hasNonUIDLCMod = false;
                    Dictionary<int, string> priorities = new Dictionary<int, string>();
                    foreach (string dir in directories)
                    {
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
                }

                if (DIAGNOSTICS_GAME == 2 || DIAGNOSTICS_GAME == 3)
                {
                    addDiagLine("===Texture File Cache (TFC) files");
                    addDiagLine("The following unpacked TFC files are present in the game directory.");
                    string[] tfcFiles = Directory.GetFiles(gamePath + "\\BIOGame", "*.tfc", SearchOption.AllDirectories);
                    if (tfcFiles.Count() > 0)
                    {
                        int strOffset = (gamePath + "\\BIOGame").Length;
                        foreach (string tfc in tfcFiles)
                        {
                            FileInfo fi = new FileInfo(tfc);
                            long tfcSize = fi.Length;
                            string tfcPath = tfc.Substring(strOffset);
                            addDiagLine(" - " + tfcPath + ", " + ByteSize.FromBytes(tfcSize));
                        }
                    }
                    else
                    {
                        addDiagLine("[ERROR]      No TFC files were found - is this installation broken?");
                    }
                }

                string asidir = Path.Combine(Directory.GetParent(Utilities.GetGameEXEPath(DIAGNOSTICS_GAME)).ToString(), "asi");
                addDiagLine("===Installed ASI mods");
                if (Directory.Exists(asidir))
                {
                    addDiagLine("The follow files are located in the ASI directory:");
                    string[] files = Directory.GetFiles(asidir, "*.asi");
                    if (files.Count() == 0)
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
                if (DIAGNOSTICS_GAME == 3)
                {
                    addDiagLine("===File Table of Contents (TOC) size check");
                    addDiagLine("PCConsoleTOC.bin files list the size of each file the game can load.");
                    addDiagLine("If the size is smaller than the actual file, the game will not allocate enough memory to load the file and will hang or crash, typically at loading screens.");
                    bool hadTocError = false;
                    string[] tocs = Directory.GetFiles(Path.Combine(gamePath, "BIOGame"), "PCConsoleTOC.bin", SearchOption.AllDirectories);
                    string markerfile = Utilities.GetALOTMarkerFilePath(3);
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
                                    addDiagLine("[ERROR] -  " + filepath + " size is " + size + ", but TOC lists " + ent.size + " (" + (ent.size - size) + " bytes)");
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
                        addDiagLine("[ERROR]Some files are larger than the listed TOC size. This typically won't happen unless you manually installed some files or an ALOT installation failed.");
                        addDiagLine("[ERROR]The game will always hang while loading these files." + (HASH_SUPPORTED ? " You can regenerate the TOC files by using AutoTOC. If installation failed due a crash, this won't fix it." : ""));
                        if (HASH_SUPPORTED) { addDiagLine("[ERROR]You can run AutoTOC in ALOT Installer by going to Settings -> Game Utilities -> AutoTOC."); }
                    }
                }

                //Get LODs
                args = "--print-lods --gameid " + DIAGNOSTICS_GAME + " --ipc";
                LODS_INFO.Clear();
                runMEM_Diagnostics(exe, args, diagnosticsWorker);
                WaitForMEM();

                String lodStr = GetLODStr(DIAGNOSTICS_GAME, avi);
                addDiagLine("===LOD Information");
                addDiagLine(lodStr);

                diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_GREEN, Image_DataBasegamemods));
                diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_WORKING, Image_Upload));

                //ME1: LOGS
                if (HASH_SUPPORTED && DIAGNOSTICS_GAME == 1)
                {
                    string dsound = gamePath += "\\Binaries\\dsound.dll";
                    bool dSoundExists = File.Exists(dsound);

                    //GET LOGS
                    string logsdir = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\BioWare\Mass Effect\Logs";
                    if (Directory.Exists(logsdir))
                    {
                        DirectoryInfo info = new DirectoryInfo(logsdir);
                        FileInfo[] files = info.GetFiles().Where(f => f.LastWriteTime > DateTime.Now.AddDays(-7)).OrderByDescending(p => p.LastWriteTime).ToArray();
                        DateTime threeDaysAgo = DateTime.Now.AddDays(-3);
                        Console.WriteLine("---");
                        foreach (FileInfo file in files)
                        {
                            Console.WriteLine(file.Name + " " + file.LastWriteTime);
                            var logLines = File.ReadAllLines(file.FullName);
                            int crashIndex = -1;
                            int index = 0;
                            string reason = "";
                            foreach (string line in logLines)
                            {

                                if (line.Contains("Critical: appError called"))
                                {
                                    crashIndex = index;
                                    reason = "Log file indicates crash occured";
                                    Log.Information("Found crash in ME1 log " + file.Name + " on line " + index);
                                    break;
                                }
                                if (line.Contains("Uninitialized: Log file closed"))
                                {
                                    crashIndex = index;
                                    reason = "~~~Additional log to provide context for log analysis";
                                    Log.Information("Found possibly relevant item in ME1 log " + file.Name + " on line " + index);
                                    break;
                                }
                                index++;
                            }

                            if (crashIndex >= 0)
                            {
                                crashIndex = Math.Max(0, crashIndex - 10);
                                //this log has a crash
                                addDiagLine("===Mass Effect game log " + file.Name);
                                if (reason != "") addDiagLine(reason);
                                if (crashIndex > 0)
                                {
                                    addDiagLine("[CRASHLOG]...");
                                }
                                for (int i = crashIndex; i < logLines.Length; i++)
                                {
                                    addDiagLine("[CRASHLOG]" + logLines[i]);
                                }
                            }
                        }
                    }
                }
                
                if (DIAGNOSTICS_GAME == 3)
                {
                    string me3logfilepath = Path.Combine(Directory.GetParent(Utilities.GetGameEXEPath(3)).ToString(), "me3log.txt");
                    if (File.Exists(me3logfilepath))
                    {

                        FileInfo fi = new FileInfo(me3logfilepath);
                        if (fi.Length < 10000)
                        {
                            addDiagLine("===Mass Effect 3 last session log");
                            addDiagLine("Last session log has modification date of " + fi.LastWriteTimeUtc.ToShortDateString());
                            addDiagLine();
                            var log = File.ReadAllLines(me3logfilepath);
                            foreach (string line in log)
                            {
                                addDiagLine(line);
                            }
                        }
                    }
                }
                if (pairLog)
                {
                    //program has had issue and log should be linked
                    EventWaitHandle waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
                    diagnosticsWorker.ReportProgress(0, new ThreadCommand(UPLOAD_LINKED_LOG, waitHandle));
                    waitHandle.WaitOne();
                    if (LINKEDLOGURL != null)
                    {
                        Log.Information("Linked log for this diagnostic: " + LINKEDLOGURL);
                        addDiagLine("[LINKEDLOG]" + LINKEDLOGURL);
                    }
                }
                */

            }
            catch (Exception ex)
            {
                addDiagLine("[ERROR]Exception occured while running diagnostic.");
                addDiagLine(App.FlattenException(ex));
                return diagStringBuilder.ToString();

            }

            return diagStringBuilder.ToString();
        }
    }
}
