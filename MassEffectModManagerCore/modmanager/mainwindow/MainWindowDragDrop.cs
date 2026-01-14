using CliWrap;
using CliWrap.EventStream;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.TLK.ME2ME3;
using LegendaryExplorerCore.Unreal;
using ME3TweaksCore.Helpers;
using ME3TweaksModManager.modmanager;
using ME3TweaksModManager.modmanager.importer;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.objects.mod.merge;
using ME3TweaksModManager.modmanager.objects.tlk;
using ME3TweaksModManager.modmanager.usercontrols;
using LegendaryExplorerCore.Helpers;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using ME3TweaksCore.TextureOverride;
using Microsoft.Win32;
using ME3TweaksCore.Objects;

namespace ME3TweaksModManager
{
    public partial class MainWindow
    {

        /// <summary>
        /// List of supported file extensions that can be dragged over the window
        /// </summary>
        private readonly string[] SupportedDroppableExtensions =
        {
            // File archives for mods. .7z is the only officially supported format.
            @".rar",
            @".zip",
            @".7z",
            // Extractable .exe format (ME3)
            @".exe", 
            // Legacy OT mod formats (for notify only)
            @".tpf",
            @".mod", 
            // Mass Effect Modder texture archive
            @".mem",
            // ME2 OT mod format
            @".me2mod",
            // Coalesced manifest
            @".xml",
            // TOC, Coalesced.bin formats
            @".bin", 
            // TLK file for compile/decompile
            @".tlk",
#if LEGACY
            // DRM file... thing?
            @".par",
#endif
            // Merge mod
            @".m3m",
            @".json",
            // LE1/LE2 config file manifest
            @".extractedbin",
            // Compressed Merge TLK Archive
            @".m3za", 
            // Global Shader Cache Override
            @".hlsl",
            // Binary Texture Package
            BinaryTexturePackage.EXTENSION_TEXTURE_OVERRIDE_BINARY
        };

        /// <summary>
        /// Invoked on drag over
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string ext = Path.GetExtension(files[0]).ToLower();
                if (!SupportedDroppableExtensions.Contains(ext))
                {
                    if (!Settings.DeveloperMode || ext != @".xaml") //dev mode supports .xaml file drop for localization
                    {
                        e.Effects = DragDropEffects.None;
                    }
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        /// <summary>
        /// Invoked on file drop
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                bool continueLoop = true;
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var file in files)
                {
                    if (!continueLoop)
                        break;
                    string ext = Path.GetExtension(file).ToLower();
                    M3Log.Information(@"File dropped onto interface: " + file);
                    switch (ext)
                    {
                        case @".rar":
                        case @".7z":
                        case @".zip":
                        case @".exe":
                            TelemetryInterposer.TrackEvent(@"User opened mod archive for import",
                                new Dictionary<string, string>
                                {
                                    { @"Method", @"Drag & drop" },
                                    { @"Filename", Path.GetFileName(file) }
                                });
                            openModImportUI(file);
                            break;
                        // Must come before .mem general case
                        case @".mem"
                            when ModFileFormats.GetGameMEMFileIsFor(file) is var memGame &&
                                 memGame.IsLEGame(): // For LE
                            App.SubmitAnalyticTelemetryEvent(@"User dropped LE mem file",
                                new Dictionary<string, string> { { @"Filename", Path.GetFileName(file) } });

                            continueLoop = false; // Do not parse other files in drop, we handle them here
                            var memFiles = new List<string>(files.Length);
                            memFiles.Add(file);
                            foreach (var f in files)
                            {
                                if (f == file)
                                    continue;
                                if (Path.GetExtension(f) == @".mem" && ModFileFormats.GetGameMEMFileIsFor(f) == memGame)
                                {
                                    memFiles.Add(f);
                                }
                            }


                            var impInstallCancel = M3L.ShowDialog(this,
                                M3L.GetString(M3L.string_importOrInstallTheseMemFilesQuestion)
                                + "\n\n" // do not localize
                                + @" - " + string.Join("\n - ", memFiles.Select(Path.GetFileName)), // do not localize
                                M3L.GetString(M3L.string_importOrInstall),
                                MessageBoxButton.YesNoCancel,
                                MessageBoxImage.Question,
                                MessageBoxResult.Yes,
                                yesContent: M3L.GetString(M3L.string_import),
                                noContent: M3L.GetString(M3L.string_install));

                            if (impInstallCancel == MessageBoxResult.Cancel)
                                return;
                            if (impInstallCancel == MessageBoxResult.Yes)
                            {
                                var task = BackgroundTaskEngine.SubmitBackgroundJob(@"TextureImport",
                                    M3L.GetString(M3L.string_importingTextureModsToLibrary),
                                    M3L.GetString(M3L.string_importedTextureMods));
                                Task.Run(() => { ModArchiveImport.ImportTextureFiles(memFiles, memGame); })
                                    .ContinueWithOnUIThread(x => { BackgroundTaskEngine.SubmitJobCompletion(task); });
                            }

                            if (impInstallCancel == MessageBoxResult.No)
                            {
                                // Install
                                var target = SelectedGameTarget;
                                if (target?.Game != memGame)
                                {
                                    target = GetCurrentTarget(memGame);
                                }

                                if (target == null)
                                {
                                    M3L.ShowDialog(this,
                                        M3L.GetString(M3L.string_interp_notTargetAvailableForX, memGame),
                                        M3L.GetString(M3L.string_gameNotAvailable),
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Error);
                                    return;
                                }

                                TextureInstallerPanel tip = new TextureInstallerPanel(target, memFiles);
                                tip.Close += (o, args) => ReleaseBusyControl();
                                ShowBusyControl(tip);
                            }

                            break;
                        //TPF, .mod, .mem
                        case @".tpf":
                        case @".mod":
                        case @".mem":
                            App.SubmitAnalyticTelemetryEvent(@"User redirected to MEM/ALOT Installer",
                                new Dictionary<string, string> { { @"Filename", Path.GetFileName(file) } });
                            M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_dialog_installingTextureMod, ext),
                                M3L.GetString(M3L.string_nonModManagerModFound), MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                            break;

                        case @".me2mod":
                            App.SubmitAnalyticTelemetryEvent(@"User opened me2mod file",
                                new Dictionary<string, string> { { @"Filename", Path.GetFileName(file) } });
                            openModImportUI(file);
                            break;
                        case @".xaml":
                            if (Settings.DeveloperMode)
                            {
                                LoadExternalLocalizationDictionary(file);
                            }

                            break;
                        case @".extractedbin":
                            {
                                using var fs = new FileStream(file, FileMode.Open, FileAccess.Read);
                                //var magic = fs.ReadInt32();
                                //fs.Dispose();
                                //if (magic is 0x666D726D or 0x1B) //fmrm (backwards) (ME3), 0x1B (LE1 (sigh))
                                //{

                                NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"Coalesced Compiler");
                                var task = BackgroundTaskEngine.SubmitBackgroundJob(@"CoalescedCompiler",
                                    M3L.GetString(M3L.string_compilingCoalescedFile),
                                    M3L.GetString(M3L.string_compiledCoalescedFile));
                                nbw.DoWork += (a, b) =>
                                {
                                    var dest = Path.Combine(Directory.GetParent(file).FullName, File.ReadAllLines(file)[0]);
                                    M3Log.Information($@"Compiling coalesced file: {file} -> {dest}");
                                    CoalescedConverter.Convert(CoalescedConverter.CoalescedType.ExtractedBin, file, dest);
                                    M3Log.Information(@"Compiled coalesced file");
                                };
                                nbw.RunWorkerCompleted += (a, b) => { BackgroundTaskEngine.SubmitJobCompletion(task); };
                                nbw.RunWorkerAsync();
                                // }
                            }
                            break;
                        case @".bin":
                            //Check for Coalesced
                            {
                                using var fs = new FileStream(file, FileMode.Open, FileAccess.Read);
                                var magic = fs.ReadInt32();
                                fs.Dispose();
                                if (magic is 0x666D726D or 0x1B or 0x1C
                                    or 0x1E) //fmrm (backwards) (ME3), 0x1B (LE1), 0x1C (LE2 count or something...) 0x1E (LE2) (sigh)
                                {

                                    NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"Coalesced Decompiler");
                                    var task = BackgroundTaskEngine.SubmitBackgroundJob(@"CoalescedDecompile",
                                        M3L.GetString(M3L.string_decompilingCoalescedFile),
                                        M3L.GetString(M3L.string_decompiledCoalescedFile));
                                    nbw.DoWork += (a, b) =>
                                    {
                                        var dest = Path.Combine(Directory.GetParent(file).FullName,
                                            Path.GetFileNameWithoutExtension(file));
                                        M3Log.Information($@"Decompiling coalesced file: {file} -> {dest}");
                                        CoalescedConverter.Convert(CoalescedConverter.CoalescedType.Binary, file, dest);
                                        M3Log.Information(@"Decompiled coalesced file");
                                    };
                                    nbw.RunWorkerCompleted += (a, b) => { BackgroundTaskEngine.SubmitJobCompletion(task); };
                                    nbw.RunWorkerAsync();
                                }
#if DEBUG && !AZURE
                                // TOC DUMP
                                else if (magic is 0x3AB70C13)
                                {
                                    TOCBinFile tbf = new TOCBinFile(file);
                                    tbf.DumpTOC();
                                }
#endif
                            }
                            break;
                        case @".xml":
                            //Check if it's ModMaker sideload, coalesced manifest, or TLK
                            {
                                try
                                {
                                    var xmldocument = XDocument.Load(file);
                                    var rootElement = xmldocument.Root;
                                    if (rootElement.Name == @"ModMaker")
                                    {
                                        //Modmaker Mod, sideload
                                        var modmakerPanel = new ModMakerPanel()
                                        {
                                            LocalFileOption = true,
                                            LocalFilePath = file
                                        };

                                        modmakerPanel.Close += (a, b) =>
                                        {
                                            ReleaseBusyControl();
                                            if (b.Data is Mod m)
                                            {
                                                M3LoadedMods.Instance.LoadMods(m);
                                            }
                                        };
                                        ShowBusyControl(modmakerPanel);
                                        break;
                                    }



                                    if (rootElement.Name == @"CoalesceFile")
                                    {
                                        bool failedToCompileCoalesced = false;

                                        void errorCompilingCoalesced(string message)
                                        {
                                            Application.Current.Dispatcher.Invoke(delegate
                                            {
                                                failedToCompileCoalesced = true;
                                                M3L.ShowDialog(this, message,
                                                    M3L.GetString(M3L.string_errorCompilingCoalesced), MessageBoxButton.OK,
                                                    MessageBoxImage.Error);
                                            });
                                        }

                                        //Coalesced manifest
                                        NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"Coalesced Compiler");
                                        var task = BackgroundTaskEngine.SubmitBackgroundJob(@"CoalescedCompile",
                                            M3L.GetString(M3L.string_compilingCoalescedFile),
                                            M3L.GetString(M3L.string_compiledCoalescedFile));
                                        nbw.DoWork += (a, b) =>
                                        {
                                            var dest = Path.Combine(Directory.GetParent(file).FullName,
                                                rootElement.Attribute(@"name").Value);
                                            M3Log.Information($@"Compiling coalesced file: {file} -> {dest}");
                                            try
                                            {
                                                CoalescedConverter.ConvertToBin(file, dest);
                                                M3Log.Information(@"Compiled coalesced file");
                                            }
                                            catch (Exception e)
                                            {
                                                M3Log.Error($@"Error compiling Coalesced file: {e.Message}:");
                                                M3Log.Error(App.FlattenException(e));
                                                errorCompilingCoalesced(M3L.GetString(
                                                    M3L.string_interp_exceptionOccuredWhileCompilingCoalsecedFileX,
                                                    e.Message));
                                            }
                                        };
                                        nbw.RunWorkerCompleted += (a, b) =>
                                        {
                                            if (failedToCompileCoalesced)
                                                task.FinishedUIText = M3L.GetString(M3L.string_errorCompilingCoalesced);
                                            BackgroundTaskEngine.SubmitJobCompletion(task);
                                        };
                                        nbw.RunWorkerAsync();
                                        break;
                                    }

                                    bool failedToCompileTLK = false;

                                    void errorCompilingTLK(string message)
                                    {
                                        Application.Current.Dispatcher.Invoke(delegate
                                        {
                                            failedToCompileTLK = true;
                                            M3L.ShowDialog(this, message, M3L.GetString(M3L.string_errorCompilingTLK),
                                                MessageBoxButton.OK, MessageBoxImage.Error);
                                        });
                                    }

                                    // Tankmaster's uses a capital T where ME3Explorer used lowercase t
                                    if (rootElement.Name == @"TlkFile")
                                    {
                                        //TLK file - ensure it's the manifest one
                                        var sourceName = rootElement.Attribute(@"source");
                                        if (sourceName != null)
                                        {
                                            //This is a manifest file
                                            /*
                                             * Manifest File
                                             * Folder with same name
                                             * |-> TLK.xml files
                                             */
                                            NamedBackgroundWorker nbw =
                                                new NamedBackgroundWorker(@"TLKTranspiler - CompileTankmaster");
                                            var task = BackgroundTaskEngine.SubmitBackgroundJob(@"TranspilerCompile",
                                                M3L.GetString(M3L.string_compilingTLKFile),
                                                M3L.GetString(M3L.string_compiledTLKFile));
                                            nbw.DoWork += (a, b) =>
                                            {
                                                TLKTranspiler.CompileTLKManifest(file, rootElement, errorCompilingTLK);
                                            };
                                            nbw.RunWorkerCompleted += (a, b) =>
                                            {
                                                if (failedToCompileTLK)
                                                    task.FinishedUIText = M3L.GetString(M3L.string_compilingFailed);
                                                BackgroundTaskEngine.SubmitJobCompletion(task);
                                            };
                                            nbw.RunWorkerAsync();
                                        }
                                        else
                                        {
                                            //Is this a straight up TLK?
                                            NamedBackgroundWorker nbw =
                                                new NamedBackgroundWorker(@"TLKTranspiler - CompileTankmaster");
                                            var task = BackgroundTaskEngine.SubmitBackgroundJob(@"TranspilerCompile",
                                                M3L.GetString(M3L.string_compilingTLKFile),
                                                M3L.GetString(M3L.string_compiledTLKFile));
                                            nbw.DoWork += (a, b) =>
                                            {
                                                TLKTranspiler.CompileTLKManifestStrings(file, rootElement,
                                                    errorCompilingTLK);
                                            };
                                            nbw.RunWorkerCompleted += (a, b) =>
                                            {
                                                if (failedToCompileTLK)
                                                    task.FinishedUIText = M3L.GetString(M3L.string_compilingFailed);
                                                BackgroundTaskEngine.SubmitJobCompletion(task);
                                            };
                                            nbw.RunWorkerAsync();
                                        }
                                    }
                                    else if (rootElement.Name == @"tlkFile") //ME3Explorer style
                                    {
                                        NamedBackgroundWorker nbw =
                                            new NamedBackgroundWorker(@"TLKTranspiler - CompileME3Exp");
                                        var task = BackgroundTaskEngine.SubmitBackgroundJob(@"TranspilerCompile",
                                            M3L.GetString(M3L.string_compilingTLKFile),
                                            M3L.GetString(M3L.string_compiledTLKFile));
                                        nbw.DoWork += (a, b) =>
                                        {
                                            TLKTranspiler.CompileTLKME3Explorer(file, rootElement, errorCompilingTLK);
                                        };
                                        nbw.RunWorkerCompleted += (a, b) =>
                                        {
                                            if (failedToCompileTLK)
                                                task.FinishedUIText = M3L.GetString(M3L.string_compilingFailed);
                                            BackgroundTaskEngine.SubmitJobCompletion(task);
                                        };
                                        nbw.RunWorkerAsync();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    M3Log.Error(@"Error loading XML file that was dropped onto UI: " + ex.Message);
                                    M3L.ShowDialog(this, M3L.GetString(M3L.string_interp_errorReadingXmlFileX, ex.Message),
                                        M3L.GetString(M3L.string_errorReadingXmlFile), MessageBoxButton.OK,
                                        MessageBoxImage.Error);
                                }
                            }
                            break;
                        case @".tlk":
                            {
                                //Break down into xml file
                                NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"TLK decompiler");
                                var task = BackgroundTaskEngine.SubmitBackgroundJob(@"TLKDecompile",
                                    M3L.GetString(M3L.string_decompilingTLKFile),
                                    M3L.GetString(M3L.string_decompiledTLKFile));
                                nbw.DoWork += (a, b) =>
                                {
                                    var dest = Path.Combine(Directory.GetParent(file).FullName,
                                        Path.GetFileNameWithoutExtension(file) + @".xml");
                                    M3Log.Information($@"Decompiling TLK file: {file} -> {dest}");
                                    var tf = new ME2ME3TalkFile(file);
                                    tf.SaveToXML(dest);
                                    M3Log.Information(@"Decompiled TLK file");
                                };
                                nbw.RunWorkerCompleted += (a, b) => { BackgroundTaskEngine.SubmitJobCompletion(task); };
                                nbw.RunWorkerAsync();

                            }
                            break;
#if LEGACY

                        case @".par":
                            {
                                var contents = PARTools.DecodePAR(File.ReadAllBytes(file));
                                Debug.WriteLine(contents);
                            }
                            break;
#endif

                        case @".json":
                            {
                                CompileMergeMod(file);
                            }
                            break;
                        case @".m3m":
                            try
                            {
                                MergeModLoader.DecompileM3M(file);
                            }
                            catch (Exception ex)
                            {
                                M3Log.Error($@"Error decompiling m3m mod file: {ex.Message}");
                                M3L.ShowDialog(this,
                                    M3L.GetString(M3L.string_interp_errorDecompilingM3MMessage, ex.Message),
                                    M3L.GetString(M3L.string_errorDecompilingM3m), MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                            }

                            break;
                        case @".m3za":
                            try
                            {
                                NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"M3ZA decompressor");
                                var fname = Path.GetFileName(file);
                                var task = BackgroundTaskEngine.SubmitBackgroundJob(@"M3ZADecompress",
                                    M3L.GetString(M3L.string_interp_decompressingFname, fname),
                                    M3L.GetString(M3L.string_interp_decompressedFname, fname));
                                nbw.DoWork += (a, b) =>
                                {
                                    void progress(int done, int total)
                                    {
                                        int percent = (int)Math.Round(done * 100.0f / total);
                                        BackgroundTaskEngine.SubmitBackgroundTaskUpdate(task,
                                            M3L.GetString(M3L.string_interp_decompressingFnamePercent, fname, percent));
                                    }

                                    using var f = File.OpenRead(file);
                                    var archive = CompressedTLKMergeData.ReadCompressedTlkMergeFile(f, true);
                                    var completed = archive.DecompressArchiveToDisk(Directory.GetParent(file).FullName,
                                        archive.LoadedCompressedData, progress);
                                    if (!completed)
                                    {
                                        task.FinishedUIText = M3L.GetString(M3L.string_interp_failedToDecompressFname,
                                            fname);
                                    }
                                };
                                nbw.RunWorkerCompleted += (a, b) =>
                                {
                                    if (b.Error != null)
                                    {
                                        M3Log.Exception(b.Error, $@"Error decompressing {file}:");
                                        task.FinishedUIText = M3L.GetString(M3L.string_interp_failedToDecompressFname, fname);
                                    }
                                    BackgroundTaskEngine.SubmitJobCompletion(task);
                                };
                                nbw.RunWorkerAsync();

                            }
                            catch (Exception ex2)
                            {
                                M3Log.Exception(ex2, $@"Error decompressing {file}:");
                            }

                            break;
                        case @".hlsl":
                            ConvertHLSLToM3GS(files).ContinueWithOnUIThread(_ => { });
                            // Do not loop; we handle it here.
                            continueLoop = false;
                            break;
                        case BinaryTexturePackage.EXTENSION_TEXTURE_OVERRIDE_BINARY:
                            HandleBTPFileDrop(file);
                            break;
                    }
                }
            }
        }

        private async Task ConvertHLSLToM3GS(string[] files)
        {
            try
            {
                var fxc = Environment.GetEnvironmentVariable(@"WindowsSdkVerBinPath") + @"x86\fxc.exe";
                if (!File.Exists(fxc))
                {
                    fxc = Environment.GetEnvironmentVariable(@"WindowsSdkVerBinPath") +
                          Environment.GetEnvironmentVariable(@"WindowsSDKVersion") + @"x86\fxc.exe";
                }

                if (!File.Exists(fxc))
                {
                    fxc = Environment.GetEnvironmentVariable(@"WindowsSdkDir") + @"bin\" +
                          Environment.GetEnvironmentVariable(@"WindowsSDKVersion") + @"x86\fxc.exe";
                }

                if (!File.Exists(fxc))
                {
                    fxc = Environment.GetEnvironmentVariable(@"WindowsSdkDir_80") + @"bin\x86\fxc.exe";
                }

                if (!File.Exists(fxc))
                {
                    fxc = Environment.GetEnvironmentVariable(@"DXSDK_DIR") + @"Utilities\bin\x86\fxc.exe";
                }

                if (!File.Exists(fxc))
                {
                    M3Log.Warning(@"Could not find fxc - install a Windows SDK that contains FXC");
                    M3L.ShowDialog(this,
                        M3L.GetString(M3L.string_dialog_couldNotfindFXC),
                        M3L.GetString(M3L.string_fXCNotFound), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var task = BackgroundTaskEngine.SubmitBackgroundJob(@"ShaderCompile",
                    M3L.GetString(M3L.string_compilingShaders),
                    M3L.GetString(M3L.string_compiledShaders));

                await Task.Run(async () =>
                {
                    void progress(int done, int total)
                    {
                        int percent = (int)Math.Round(done * 100.0f / total);
                        BackgroundTaskEngine.SubmitBackgroundTaskUpdate(task,
                            M3L.GetString(M3L.string_compilingShaders) + $@" {percent}%");
                    }

                    int total = files.Length;
                    int done = 0;
                    foreach (var f in files)
                    {
                        var isPixelShader = f.Contains(@"PixelShader",
                            StringComparison.OrdinalIgnoreCase);
                        var typeParam = isPixelShader ? @"ps_5_0" : @"vs_5_0";
                        var outPath = Path.Combine(Directory.GetParent(f).FullName, Path.GetFileNameWithoutExtension(f) + @".m3gs");
                        var cmd = Cli.Wrap(fxc)
                            .WithArguments($@"/T {typeParam} /Fo ""{outPath}"" ""{f}""")
                            .WithValidation(CommandResultValidation.None);

                        await foreach (var cmdEvent in cmd.ListenAsync())
                        {
                            switch (cmdEvent)
                            {
                                case StartedCommandEvent started:
                                    M3Log.Information($@"FXC: Process started with id {started.ProcessId}");
                                    break;
                                case StandardOutputCommandEvent stdOut:
                                    M3Log.Information($@"FXC: {stdOut.Text}");
                                    break;
                                case StandardErrorCommandEvent stdErr:
                                    M3Log.Error($@"FXC: {stdErr.Text}");
                                    break;
                                case ExitedCommandEvent exited:
                                    M3Log.Information($@"FXC: Process exited with code {exited.ExitCode}");
                                    break;
                            }
                        }

                        done++;
                        progress(done, total);
                    }
                    M3Log.Information(@"Shader compile complete");
                });

                BackgroundTaskEngine.SubmitJobCompletion(task);
            }
            catch (Exception ex)
            {
                M3Log.Exception(ex, $@"Error compiling shaders:");
            }
        }

        /// <summary>
        /// Handles extracting a BTP file
        /// </summary>
        /// <param name="file"></param>
        private void HandleBTPFileDrop(string file)
        {
            // Find metadata file
            var directory = Directory.GetParent(file).FullName;

            var testMetadataFile = Path.Combine(directory, @"BTPMetadata.btm");

            if (!File.Exists(testMetadataFile))
            {
                OpenFileDialog openFileDialog = new OpenFileDialog()
                {
                    Title = M3L.GetString(M3L.string_selectMetadataFile),
                    Filter = M3L.GetString(M3L.string_binaryTextureMetadataFile) + @"|*.btm",
                    InitialDirectory = directory,
                    // CustomPlaces = MEDirectories.CustomPlaces // Todo: Maybe make this from targets?
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    testMetadataFile = openFileDialog.FileName;
                }
                else
                {
                    return;
                }
            }

            var openFolderDialog = new OpenFolderDialog()
            {
                InitialDirectory = directory,
                Title = M3L.GetString(M3L.string_selectOutputFolder)
            };

            if (openFolderDialog.ShowDialog() != true)
                return;
            var outputFolder = openFolderDialog.FolderName;
            var task = BackgroundTaskEngine.SubmitBackgroundJob(@"BTPExtract",
                M3L.GetString(M3L.string_extractingTexturesFromBTP),
                M3L.GetString(M3L.string_finishedExtractingTextures));

            void OnUpdate(ProgressInfo _pi)
            {
                BackgroundTaskEngine.SubmitBackgroundTaskUpdate(task, M3L.GetString(M3L.string_extractingTexturesFromBTP) + $@" {_pi.Value:F2}%");
            }
            var pi = new ProgressInfo();
            pi.OnUpdate = OnUpdate;

            Task.Run(() =>
            {
                using var fs = File.OpenRead(file);
                var btp = new BinaryTexturePackage(fs); // Do not load mip data yet
                btp.ReconstituteSource(fs, testMetadataFile, outputFolder, pi);
            }).ContinueWithOnUIThread(x =>
            {
                if (x.Exception != null)
                {
                    // Error, set message then submit it so bottom left text doesn't wait for the dialog to close.
                    M3Log.Exception(x.Exception, @"Error extracting textures from BTP:");
                    task.FinishedUIText = M3L.GetString(M3L.string_errorExtractingTexturesFromBTP);
                    BackgroundTaskEngine.SubmitJobCompletion(task);
                    M3L.ShowDialog(this, M3L.GetString(M3L.string_dialog_errorExtractingTexturesFromBTP, x.Exception.Message), M3L.GetString(M3L.string_errorExtractingBTP), MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    // OK
                    BackgroundTaskEngine.SubmitJobCompletion(task);
                }
            });
        }
    }
}
