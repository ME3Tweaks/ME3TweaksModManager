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
    /// Class for handling serializing large amounts of data to. Only tracks single export transfers at a time. Splits files once they reach 512MB.
    /// </summary>
    class LargePackageChunkedSerializer
    {
        /// <summary>
        /// Max size of package "data" when uncompressed - due to how serialization 
        /// works it may be larger, but we buffer small enough it should be fine.
        /// </summary>
        private const long maxDataSize = 1024 * 1024 * 1024; // 1 GiB

        /// <summary>
        /// Amount of data we've recorded to current package exports
        /// </summary>
        private long currentDataSize = 0;

        /// <summary>
        /// Current package that is being serialized to
        /// </summary>
        private IMEPackage currentPackage;

        /// <summary>
        /// The current index of the package we are serializing
        /// </summary>
        int currentPackageIndex = 0;

        /// <summary>
        /// Where serialized data gets written out to when packages are saved
        /// </summary>
        public string baseSavePath { private get;  init; }

        /// <summary>
        /// Game packages are for
        /// </summary>
        public MEGame game { private get; init; }

        /// <summary>
        /// Name of packages to roll through
        /// </summary>
        public string basePackageName { private get; init; }


        private List<string> packagePaths = new();

        /// <summary>
        /// Saved package paths list
        /// </summary>
        public IReadOnlyList<string> PackagePaths => packagePaths;


        public IEntry ExportInto(ExportEntry source, PackageCache cache)
        {
            if (currentPackage == null || (source.DataSize + currentDataSize) > maxDataSize)
            {
                Rollover();
            }

            // Export the texture to the new package
            currentDataSize += source.DataSize;
            EntryExporter.ExportExportToPackage(source, currentPackage, out var portedEntry, cache);
            return portedEntry;
        }

        /// <summary>
        /// Saves current package, resets, and starts next package
        /// </summary>
        private void Rollover()
        {
            if (currentPackage != null)
            {
                M3Log.Information($@"Large data serializer - saving package...");
                currentPackage.Save();
                packagePaths.Add(currentPackage.FilePath);
            }

            currentPackageIndex++;
            currentDataSize = 0;

            var name = currentPackageIndex == 1 ? basePackageName : basePackageName + currentPackageIndex;
            var path = Path.Combine(baseSavePath, $@"{name}.pcc");
            M3Log.Information($@"Large data serializer - rolling new package {path}");
            currentPackage = MEPackageHandler.CreateAndOpenPackage(path, game);
        }

        /// <summary>
        /// Saves package and resets serializer
        /// </summary>
        public void Finalize()
        {
            if (currentPackage != null)
            {
                currentPackage.Save();
                packagePaths.Add(currentPackage.FilePath);
            }
            currentPackageIndex = 0;
            currentDataSize = 0;
        }
    }

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
            var yn = M3L.ShowDialog(_window, "Perform texture install?", "Info", MessageBoxButton.YesNo);

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
            return g;
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
                    MUtilities.DeleteFilesAndFoldersRecursively(stagingDir, deleteDirectoryItself: false);
                }
                Directory.CreateDirectory(stagingDir);

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

                        var internalFormatLodBias = tex.GetProperty<IntProperty>("InternalFormatLODBias");
                        if (internalFormatLodBias != null && internalFormatLodBias.Value == -10)
                        {
                            // MEM Always sets -10 for some reason.
                            // This covers package stored textures, I think.
                            isModifiedByMEM = true;
                        }

                        // Check TFC name, as above check will catch everything MEM, and we need to also know about our TFCs.
                        string tfc = null;
                        var tfcProp = tex.GetProperty<NameProperty>("TextureFileCacheName");
                        if (tfcProp != null)
                        {
                            tfc = tfcProp.Value.Name;
                            if (tfc.StartsWith("TexturesMEM"))
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
                                M3Log.Information($@"Copying {pack.Key} to staging");
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
                                var dest = Path.Combine(stagingDir, tfc + ".tfc");

                                lock (sync)
                                {
                                    if (!File.Exists(dest))
                                    {
                                        M3Log.Information($@"Copying {tfcPath} to staging");
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
                    using var cPackage = MEPackageHandler.OpenMEPackage(cPackageP);
                    done++;
                    if (done % 10 == 0)
                    {
                        M3Log.Information($@"PASS 2: {done}/{total}");
                    }

                    BackgroundTaskEngine.SubmitBackgroundTaskUpdate(task, $"PASS 2: Build TO {done}/{total} {cPackage.FileNameNoExtension}");
                    foreach (var tex in cPackage.Exports.Where(x => x.IsA("Texture2D")).ToList())
                    {
                        // Find package in buildPackage
                        var tfc = tex.GetProperty<NameProperty>("TextureFileCacheName");
                        if (tfc == null)
                            continue;
                        if (tfc.Value.Name.StartsWith("TexturesMEM"))
                        {
                            var portedEntry = largeDataSerializer.ExportInto(tex, cache);

                            // Repoint TFC
                            var texExport = portedEntry as ExportEntry;
                            var newTfcName = GetNewTFCName(tfc.Value.Name, dlcName);
                            texExport.WriteProperty(new NameProperty(newTfcName, "TextureFileCacheName"));
                            var guid = GetGuidFromTFCName(newTfcName);
                            texExport.WriteProperty(CommonStructs.GuidProp(guid, "TFCFileGuid"));
                         
                            // This will correct the one set by MEM
                            Texture2D.UpdateLODBiasForTexture(texExport);
                        }
                    }
                    
                    File.Delete(cPackageP); // Get rid of this as we don't need it anymore
                }

                // Ensure package is saved
                largeDataSerializer.Finalize();

                copiedPackages.Sort();

                // Rename MEM TFCs
                M3Log.Information($@"Renaming MEM TFC files");
                var tfcFiles = Directory.GetFiles(stagingDir, "TexturesMEM*.tfc");
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
                var sourcePackages = new List<IMEPackage>();
                foreach(var p in largeDataSerializer.PackagePaths)
                {
                    sourcePackages.Add(MEPackageHandler.UnsafePartialLoad(p, p => false)); // load tables only
                }


                foreach (var tex in texturesDict.Values)
                {
                    var foundPackageName = sourcePackages.First(x => x.FindExport(tex.MemoryPath ?? tex.TextureIFP) != null);
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
                    M3Log.Information($@"Move staged file to final: {Path.GetFileName(sf)}");
                    File.Move(sf, destPath);
                }

                Directory.Delete(stagingDir);

            }).ContinueWithOnUIThread(x =>
            {
                if (x.Exception != null)
                {
                    Debugger.Break();
                }
                BackgroundTaskEngine.SubmitJobCompletion(task);
            });
        }
    }
}
