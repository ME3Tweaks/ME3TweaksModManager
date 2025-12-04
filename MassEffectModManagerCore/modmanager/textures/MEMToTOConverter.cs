using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.ME3Tweaks.ModManager.Interfaces;
using ME3TweaksCore.Objects;
using ME3TweaksCore.TextureOverride;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.objects.mod.texture;
using ME3TweaksModManager.modmanager.usercontrols;
using ME3TweaksModManager.modmanager.windows;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ME3TweaksModManager.modmanager.textures
{
    /// <summary>
    /// Class that orchestrates the main window and converts a MEM to texture override.
    /// </summary>
    public class MEMToTOConverter
    {
        /// <summary>
        /// Window being orchestrated
        /// </summary>
        private MainWindow _window;

        /// <summary>
        /// Target to use for conversion
        /// </summary>
        private GameTarget target;

        /// <summary>
        /// The mod being converted formats
        /// </summary>
        private MEMMod modBeingConverted;

        /// <summary>
        /// Generate mod's CookedPCConsole folder
        /// </summary>
        private string targetCookedPath;

        /// <summary>
        /// DLC Folder name for target
        /// </summary>
        private string dlcName;

        public MEMToTOConverter(MainWindow window)
        {
            _window = window;
        }

        public bool SetupConversion()
        {
            var modsToShow = M3LoadedMods.GetAllM3ManagedMEMs(MEGame.LE1)
                .Concat(M3LoadedMods.GetAllM3ManagedMEMs(MEGame.LE2))
                .Concat(M3LoadedMods.GetAllM3ManagedMEMs(MEGame.LE3)).OfType<IDisplayableMod>().ToList();

            var msd = new ModSelectorDialog(_window, modsToShow, "Select .mem", "Select a .mem file to convert.", "Convert");

            if (msd.ShowDialog() is null or false)
                return false; // Cancelled

            modBeingConverted = msd.SelectedMods.FirstOrDefault() as MEMMod;
            if (modBeingConverted == null)
                return false; // Should not happen

            // Now pick target mod
            var modTarget = new ModSelectorDialog(_window, M3LoadedMods.GetModsForGame(modBeingConverted.Game).OfType<IDisplayableMod>().ToList(), "Select target mod", "Select target mod to convert MEM mod into.", "Select");
            if (modTarget.ShowDialog() is null or false)
                return false; // Cancelled

            var targetMod = modTarget.SelectedMods.FirstOrDefault() as Mod;
            if (targetMod == null)
                return false; // Should not happen

            // Find cooked folder...
            var dlcFolders = targetMod.GetAllPossibleCustomDLCFolders();
            if (dlcFolders.Count != 1)
            {
                M3L.ShowDialog(_window, $"The selected mod must install a single DLC mod folder. The selected target mod installs {dlcFolders.Count}.", "Incompatible mod selected",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            targetCookedPath = Path.Combine(targetMod.ModPath, dlcFolders[0], @"CookedPCConsole");
            dlcName = dlcFolders[0];
            // Verify it exists
            if (!Directory.Exists(targetCookedPath))
            {
                M3L.ShowDialog(_window, $"The selected mod does not have a CookedPCConsole folder at the expected location: {targetCookedPath}. Cannot continue.", "Incompatible mod selected",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // Okay, we are ready to begin conversion
            return true;
        }


        /// <summary>
        /// Begins conversion process
        /// </summary>
        public void BeginConversion()
        {
            target = _window.GetCurrentTarget(modBeingConverted.Game);

            // This is real complicated
            var shouldRestore = M3L.ShowDialog(_window, $"{modBeingConverted.Game.ToGameName()} will be restored to a clean slate to install the texture mod. A texture override version will then be built from the changes made to the installation.",
                     M3L.GetString(M3L.string_gameRestoreRequested),
                     MessageBoxButton.YesNoCancel,
                     MessageBoxImage.Warning,
                     MessageBoxResult.Cancel);

            if (shouldRestore == MessageBoxResult.Cancel)
                return; // Total cancellation.


            if (shouldRestore == MessageBoxResult.Yes)
            {
                AutoGameRestorePanel agrp = new AutoGameRestorePanel(target);
                agrp.Close += (sender, args) =>
                {
                    _window.ReleaseBusyControl(); // This is so the panel is closed

                    // Go to the next step.
                    BeginMEMConversion();
                };
                _window.ShowBusyControl(agrp);
            } else
            {
                // Convert without restore.
                // Go to the next step.
                BeginMEMConversion();
            }

        }

        private void BeginMEMConversion()
        {
            // Install MEM file
            var yn = M3L.ShowDialog(_window, "Perform texture install? If you don't have them already installed, you need to perform this or conversion will do nothing.", "Info", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (yn == MessageBoxResult.Yes)
            {
                TextureInstallerPanel tip = new TextureInstallerPanel(target, [modBeingConverted.GetFilePathToMEM()]) { ShowTextureWarning = false, SkipMarkers = true };
                tip.Close += (sender, args) =>
                {
                    _window.ReleaseBusyControl(); // This is so the panel is closed
                    if (tip.SessionResult?.ExitCode == 0)
                    {
                        // Next up we build the mod...
                        Task.Run(() =>
                        {
                            M3Log.Information(@"Waiting 4 seconds for panel results to be processed...");
                            Thread.Sleep(4000);
                        }).ContinueWithOnUIThread(x =>
                        {
                            ConvertInstallationToOverride();
                        });
                    }
                };
                _window.ShowBusyControl(tip);
            } else
            {
                ConvertInstallationToOverride();
            }
        }

        /// <summary>
        /// Gets TFC name to use for conversion
        /// </summary>
        /// <param name="tfcFile">TFC file name or path</param>
        /// <param name="dlcName">DLC name of TFC</param>
        /// <returns>Base TFC name, no extension.</returns>
        private static string GetNewTFCName(string tfcFile, string dlcName)
        {
            var name = $"Textures_{dlcName}";

            var tfcBaseName = Path.GetFileNameWithoutExtension(tfcFile);
            var indexStr = tfcBaseName[^4..];
            var index = int.Parse(indexStr);
            if (index > 0)
            {
                // ALOT0000 -> ALOT
                // ALOT0001 -> ALOT2
                index++;
                name += index;
            }

            return name;
        }

        private CaseInsensitiveConcurrentDictionary<Guid> GuidMap = new();

        /// <summary>
        /// Gets the guid for the given tfc name. Uses a hash of the tfc name to make them unique.
        /// </summary>
        /// <param name="tfcName">Name of the FINAL tfc</param>
        /// <returns>guid to assign.</returns>
        private Guid GetGuidFromTFCName(string tfcName)
        {
            if (GuidMap.TryGetValue(tfcName, out var g))
            {
                return g;
            }

            var md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(tfcName));
            Guid result = new Guid(hash);
            GuidMap[tfcName] = result;
            return result;
        }

        private void ConvertInstallationToOverride()
        {
            var task = BackgroundTaskEngine.SubmitBackgroundJob(@"BTO Build", "Pass 1: Inventory", "Completed");
            Task.Run(() =>
            {
                //var dlcDest = @"S:\SteamLibrary\steamapps\common\Mass Effect Legendary Edition\Game\ME1\BioGame\DLC\DLC_MOD_ISL";
                //if (Directory.Exists(dlcDest))
                //{
                //    MUtilities.DeleteFilesAndFoldersRecursively(dlcDest);
                //}

                var sync = new Object();
                var addObj = new Object();

                var files = target.GetFilesLoadedInGame(forceReload: true, includeTFCs: true);
                //var dlcName = "DLC_MOD_ISL";
                //var copyPath = @"Z:\ModLibrary\LE1\Improved Static Lighting for LE1\DLC_MOD_ISL\CookedPCConsole";
                //var stagePath = @"S:\convert";

                var manifest = new TextureOverrideManifest();
                manifest.Game = modBeingConverted.Game;
                manifest.Textures = new List<TextureOverrideTextureEntry>();

                // PASS 1 - Copy all texture packages and TFCs
                M3Log.Information($@"Beginning PASS 1 for .mem conversion");
                var texturesDict = new CaseInsensitiveConcurrentDictionary<TextureOverrideTextureEntry>();
                var done = 0;
                var total = files.Count;
                var copiedPackages = new List<string>();

                var stagingDir = Path.Combine(targetCookedPath, @"staging");
                if (Directory.Exists(stagingDir))
                {
                    M3Log.Information($@"Clearing existing staging directory {stagingDir}");
                    MUtilities.DeleteFilesAndFoldersRecursively(stagingDir, deleteDirectoryItself: false);
                }
                Directory.CreateDirectory(stagingDir);

                M3Log.Information($@"Identifying files with TexturesMEM TFCs and InternalFormatLODBias = -10 set on textures");
                Parallel.ForEach(files, pack =>
                {
                    done++;

                    if (!pack.Value.RepresentsPackageFilePath())
                    {
                        return; // Do not process
                    }

                    BackgroundTaskEngine.SubmitBackgroundTaskUpdate(task, $"PASS 1: Inventorying {done}/{total}");
                    var package = MEPackageHandler.UnsafePartialLoad(pack.Value, x => !x.IsDefaultObject && x.IsA("Texture2D"));
                    bool copied = false;
                    foreach (var tex in package.Exports.Where(x => x.IsDataLoaded()))
                    {
                        // Determine if texture is modified by MEM
                        bool isModifiedByMEM = false;

                        var internalFormatLodBias = tex.GetProperty<IntProperty>(@"InternalFormatLODBias");
                        if (internalFormatLodBias != null && internalFormatLodBias.Value == -10)
                        {
                            // MEM Always sets -10 for some reason.
                            // This covers package stored textures, I think.
                            isModifiedByMEM = true;
                        }

                        // Check TFC name, as above check will catch everything MEM, and we need to also know about our TFCs.
                        string tfc = null;
                        var tfcProp = tex.GetProperty<NameProperty>(@"TextureFileCacheName");
                        if (tfcProp != null)
                        {
                            tfc = tfcProp.Value.Name;
                            if (tfc.StartsWith(@"TexturesMEM"))
                            {
                                isModifiedByMEM = true; // Extra catch chance I guess
                            }
                        }

                        if (!isModifiedByMEM)
                            continue; // Nothing to do here.

                        // It's a texture modded texture
                        // Copy package if not copied already.
                        if (!copied)
                        {
                            // Copy file if not copied
                            var file = Path.Combine(stagingDir, pack.Key);
                            if (!File.Exists(file))
                            {
                                M3Log.Information($@"Copying texture modded package {pack.Key} to staging");
                                File.Copy(pack.Value, file);
                            }
                            lock (addObj)
                            {
                                copiedPackages.Add(file);
                                copied = true;
                            }

                            // Copy TFC if not found
                            if (tfc != null)
                            {
                                var tfcName = $@"{tfc}.tfc";
                                var tfcPath = files[tfcName];
                                var dest = Path.Combine(stagingDir, tfc + @".tfc");

                                lock (sync)
                                {
                                    if (!File.Exists(dest))
                                    {
                                        M3Log.Information($@"Copying MEM TFC {tfcPath} to staging");
                                        File.Copy(tfcPath, dest);
                                    }
                                }
                            }
                        }

                        // Inventory it...
                        var memoryPath = tex.MemoryFullPath;
                        var ifp = tex.InstancedFullPath;
                        if (!texturesDict.ContainsKey(memoryPath))
                        {
                            var overrideEntry = new TextureOverrideTextureEntry();
                            overrideEntry.TextureIFP = ifp;
                            if (!tex.IsForcedExport)
                            {
                                // it will become forced export so we have to set the IFP to what it will become
                                overrideEntry.TextureIFP = memoryPath;
                            }
                            else
                            {
                                overrideEntry.MemoryPath = memoryPath;
                            }

                            overrideEntry.CompilingSourcePackage = pack.Key;
                            texturesDict[memoryPath] = overrideEntry;
                        }
                    }
                }
                );


                // PASS 2 - Generate new packages with only the textures.
                M3Log.Information($@"PASS 2: Hallow out files, optimize override source, update tfc references");
                var largeDataSerializer = new LargePackageChunkedSerializer()
                {
                    game = modBeingConverted.Game,
                    basePackageName = $@"TO_{dlcName}",
                    baseSavePath = stagingDir
                };
   
                var cache = new PackageCache();
                done = 0;
                total = copiedPackages.Count;
                foreach (var cPackageP in copiedPackages)
                {
                    M3Log.Information($@"Extracting textures from {cPackageP}");
                    using var cPackage = MEPackageHandler.OpenMEPackage(cPackageP);

                    // Change intenral filename so we cannot use this as a global importable file,
                    // or it might try to port things as an import.
                    if (EntryImporter.IsSafeToImportFrom(cPackage.FilePath, cPackage.Game, null))
                    {
                        cPackage.SetInternalFilepath(@"_" + cPackage.FileNameNoExtension + @".pcc");
                    }
                    done++;

                    BackgroundTaskEngine.SubmitBackgroundTaskUpdate(task, $"PASS 2: Build TO {done}/{total} {cPackage.FileNameNoExtension}");
                    foreach (var tex in cPackage.Exports.Where(x => x.IsA(@"Texture2D")).ToList())
                    {
                        // Find package in buildPackage
                        var tfc = tex.GetProperty<NameProperty>(@"TextureFileCacheName");
                        if (tfc == null)
                            continue;
                        if (tfc.Value.Name.StartsWith(@"TexturesMEM"))
                        {
                            var portedEntry = largeDataSerializer.ExportInto(tex, cache);

                            // Repoint TFC
                            var texExport = portedEntry as ExportEntry;
                            if (texExport == null)
                            {
                                M3Log.Error($@"Ported entry ported as an import! This will break things.");
                            }
                            var newTfcName = GetNewTFCName(tfc.Value.Name, dlcName);
                            texExport.WriteProperty(new NameProperty(newTfcName, "TextureFileCacheName"));
                            var guid = GetGuidFromTFCName(newTfcName);
                            texExport.WriteProperty(CommonStructs.GuidProp(guid, @"TFCFileGuid"));
                         
                            // This will correct the one set by MEM
                            Texture2D.UpdateLODBiasForTexture(texExport);
                        }
                    }
                    
                    File.Delete(cPackageP); // Get rid of this as we don't need it anymore
                }

                // Ensure package is saved
                largeDataSerializer.Finish();

                copiedPackages.Sort();

                // Rename MEM TFCs
                M3Log.Information($@"PASS 3: Renaming MEM TFC files");
                var tfcFiles = Directory.GetFiles(stagingDir, @"TexturesMEM*.tfc");
                done = 0;
                total = tfcFiles.Length;
                foreach (var tfcFile in tfcFiles)
                {
                    done++;
                    BackgroundTaskEngine.SubmitBackgroundTaskUpdate(task, $"PASS 3: Move TFC files {done}/{total}");

                    // Update TFC header guid
                    var newBaseName = GetNewTFCName(tfcFile, dlcName);
                    var newGuid = GetGuidFromTFCName(newBaseName);
                    using (var fs = File.OpenWrite(tfcFile))
                    {
                        // Write guid at start of tfc
                        fs.WriteGuid(newGuid);
                    }


                    var newName = GetNewTFCName(tfcFile, dlcName) + @".tfc";
                    var dest = Path.Combine(stagingDir, newName);
                    if (File.Exists(dest))
                    {
                        // Don't move it
                        M3Log.Information($@"TFC file already at staging, not moving it: {Path.GetFileName(tfcFile)}");
                        File.Delete(tfcFile);
                    }
                    else
                    {
                        // Move it
                        M3Log.Information($@"Moving TFC file to staging: {tfcFile} -> {dest}");
                        File.Move(tfcFile, dest);
                    }
                }

                // Update overrides to TO_
                M3Log.Information($@"Updating override paths for new package names and IFPs");
                var sourcePackages = new List<IMEPackage>();
                foreach (var p in largeDataSerializer.PackagePaths)
                {
                    sourcePackages.Add(MEPackageHandler.UnsafePartialLoad(p, p => false)); // load tables only
                }


                foreach (var tex in texturesDict.Values)
                {
                    var foundPackageName = sourcePackages.FirstOrDefault(x => x.FindExport(tex.MemoryPath ?? tex.TextureIFP) != null);
                    M3Log.Error($@"We could not find a package containg the export: {tex.MemoryPath ?? tex.TextureIFP}! This will break things.");
                    tex.CompilingSourcePackage = foundPackageName.FileNameNoExtension + @".pcc";
                    if (tex.MemoryPath == tex.TextureIFP)
                    {
                        // Don't need to serialize this
                        tex.MemoryPath = null;
                    }
                }

                // Write out the final manifest.
                M3Log.Information($@"Serializing Texture Override Manifest");
                manifest.Textures = texturesDict.Values.ToList();
                var json = JsonConvert.SerializeObject(manifest);
                File.WriteAllText(Path.Combine(targetCookedPath, $@"{TextureOverrideManifest.PREFIX_TEXTURE_OVERRIDE_MANIFEST}{dlcName}.m3to"), json);

                // Move the staging files
                var stagedFiles = Directory.GetFiles(stagingDir);
                foreach (var sf in stagedFiles)
                {
                    var destPath = Path.Combine(targetCookedPath, Path.GetFileName(sf));
                    M3Log.Information($@"Moving staged file to final location: {Path.GetFileName(sf)}");
                    if (File.Exists(destPath))
                    {
                        File.Delete(destPath);
                    }
                    File.Move(sf, destPath);
                }

                M3Log.Information($@"Deleting staging directory {stagingDir}");
                Directory.Delete(stagingDir);
                M3Log.Information($@"Deleting staging directory {stagingDir}");
            }).ContinueWithOnUIThread(x =>
            {
                if (x.Exception != null)
                {
                    M3Log.Exception(x.Exception, @"Error converting MEM file: ");
                    task.FinishedUIText = "Error converting .mem";
                    BackgroundTaskEngine.SubmitJobCompletion(task);
                    Debugger.Break();
                    M3L.ShowDialog(MainWindow.Instance, $"An error occurred while converting the .mem to M3TO format:\n{x.Exception.Message}\n\nYou can find more detailed information in the application log.", "Error converting .mem", MessageBoxButton.OK, MessageBoxImage.Error);
                } else
                {
                    BackgroundTaskEngine.SubmitJobCompletion(task);
                }
            });
        }
    }
}
