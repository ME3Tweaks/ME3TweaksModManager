using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Coalesced.Xml;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using ME3TweaksCore.Targets;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using Microsoft.WindowsAPICodePack.NativeAPI.Consts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ME3TweaksModManager.modmanager.starterkit
{
    /// <summary>
    /// Adds addition features to a DLC mod
    /// </summary>
    internal class StarterKitAddins
    {
        #region STARTUP FILE
        public static void AddStartupFile(MEGame game, string dlcFolderPath)
        {
            if (game == MEGame.ME1)
            {
                M3Log.Error(@"Cannot add startup file to ME1.");
                return;
            }
            M3Log.Information($@"Adding startup file to {dlcFolderPath}. Game: {game}");
            var dlcName = Path.GetFileName(dlcFolderPath);
            var cookedPath = Path.Combine(dlcFolderPath, game.CookedDirName());
            var startupFName = $"Startup{dlcName.Substring(3)}.pcc"; // "DLC|_"
            var startupPackagePath = Path.Combine(cookedPath, startupFName);
            if (File.Exists(startupPackagePath))
            {
                M3Log.Error($@"A startup file already exists: {startupPackagePath}. Not regenerating.");
                return;
            }

            using var package = MEPackageHandler.CreateAndOpenPackage(startupPackagePath, game, true);
            CreateObjectReferencer(package);
            package.Save();

            // Add it to coalesced so it gets used
            if (game == MEGame.LE1)
            {
                AddStartupCoalescedRefLE1(dlcFolderPath, startupFName);
            }
            else if (game.IsGame2())
            {
                AddStartupCoalescedRefGame2(dlcName, cookedPath, startupFName);
            }
            else if (game.IsGame3())
            {
                AddStartupCoalescedRefGame3(dlcName, cookedPath, startupFName);
            }
        }


        /// <summary>
        /// Adds a LE1 coalcesed entry for a startup file. Startup files require at least Autoload v6
        /// </summary>
        /// <param name="dlcRootPath">The path of the DLC dir</param>
        /// <param name="startupFName">The filename of the startup package</param>
        private static void AddStartupCoalescedRefLE1(string dlcRootPath, string startupFName)
        {
            // Load autoload
            var autoload = Path.Combine(dlcRootPath, "Autoload.ini");
            var ini = DuplicatingIni.LoadIni(autoload);

            // Add globalpackage - may need adjusted if we generalize this code
            var packageHeading = ini.GetSection("Packages");
            packageHeading.SetSingleEntry("GlobalPackage1", Path.GetFileNameWithoutExtension(startupFName));

            // Reserialize
            File.WriteAllText(autoload, ini.ToString());
        }

        /// <summary>
        /// Adds a game 3 coalcesed entry for a startup file
        /// </summary>
        /// <param name="dlcName">Name of the DLC folder</param>
        /// <param name="cookedPath">The path of the CookedPCConsole dir</param>
        /// <param name="startupFName">The filename of the startup package</param>
        private static void AddStartupCoalescedRefGame3(string dlcName, string cookedPath, string startupFName)
        {
            // Load coalesced
            var coalFile = $"Default_{dlcName}.bin";
            var coalPath = Path.Combine(cookedPath, coalFile);
            var decompiled = CoalescedConverter.DecompileGame3ToMemory(new MemoryStream(File.ReadAllBytes(coalPath)));
            var iniFiles = new SortedDictionary<string, CoalesceAsset>(); // For recomp
            foreach (var f in decompiled)
            {
                iniFiles[f.Key] = XmlCoalesceAsset.LoadFromMemory(f.Value);
            }

            // Add entry
            var engine = iniFiles["BioEngine.xml"];
            var sp = engine.GetOrAddSection("engine.startuppackages");
            sp.AddEntryIfUnique(new CoalesceProperty("dlcstartuppackage", new CoalesceValue(Path.GetFileNameWithoutExtension(startupFName), CoalesceParseAction.AddUnique)));

            // Reserialize
            var assetTexts = new Dictionary<string, string>();
            foreach (var asset in iniFiles)
            {
                assetTexts[asset.Key] = asset.Value.ToXmlString();
            }

            var outBin = CoalescedConverter.CompileFromMemory(assetTexts);
            outBin.WriteToFile(coalPath);
        }

        /// <summary>
        /// Adds game 2 coalesced entry for a startup file
        /// </summary>
        /// <param name="dlcName">Not used</param>
        /// <param name="cookedPath">The path of the CookedPCConsole dir</param>
        /// <param name="startupFName">The filename of the startup package</param>
        private static void AddStartupCoalescedRefGame2(string dlcName, string cookedPath, string startupFName)
        {
            // Add to the coalesced
            var bioEngineFile = Path.Combine(cookedPath, "BIOEngine.ini");
            var engine = ConfigFileProxy.LoadIni(bioEngineFile);
            var sp = engine.GetOrAddSection("Engine.StartupPackages");
            sp.AddEntryIfUnique(new CoalesceProperty("DLCStartupPackage", new CoalesceValue(Path.GetFileNameWithoutExtension(startupFName), CoalesceParseAction.AddUnique)));
            File.WriteAllText(bioEngineFile, engine.GetGame2IniText());
        }

        /// <summary>
        /// Creates an empty ObjectReferencer if none exists - if one exists, it returns that instead
        /// </summary>
        /// <param name="package">Package to operate on</param>
        /// <returns>Export of an export referencer</returns>
        public static ExportEntry CreateObjectReferencer(IMEPackage package)
        {
            var referencer = package.Exports.FirstOrDefault(x => x.ClassName == "ObjectReferencer");
            if (referencer != null) return referencer;

            var rop = new RelinkerOptionsPackage() { Cache = new PackageCache() };
            referencer = new ExportEntry(package, 0, package.GetNextIndexedName("ObjectReferencer"), properties: new PropertyCollection() { new ArrayProperty<ObjectProperty>("ReferencedObjects") })
            {
                Class = EntryImporter.EnsureClassIsInFile(package, "ObjectReferencer", rop)
            };
            referencer.WriteProperty(new ArrayProperty<ObjectProperty>(@"ReferencedObjects"));
            package.AddExport(referencer);
            return referencer;
        }

        public static bool AddToObjectReferencer(IEntry entry)
        {
            var referencer = entry.FileRef.Exports.FirstOrDefault(x => x.ClassName == "ObjectReferencer");
            if (referencer == null) return false;
            var refs = referencer.GetProperty<ArrayProperty<ObjectProperty>>(@"ReferencedObjects") ?? new ArrayProperty<ObjectProperty>(@"ReferencedObjects");
            refs.Add(new ObjectProperty(entry));
            referencer.WriteProperty(refs);
            return true;
        }

        #endregion

        #region Squadmate Merge

        /// <summary>
        /// Generates the SquadmateOutfitMerge.sqm file from the listed dictionary of outfits.
        /// </summary>
        /// <param name="game">The game the merge is for</param>
        /// <param name="outfits">The outfits</param>
        /// <returns>Text ready to write to the .sqm file</returns>
        public static string GenerateOutfitMergeText(MEGame game, List<Dictionary<string, object>> outfits)
        {
            var outfitMerge = new Dictionary<string, object>();
            outfitMerge[@"game"] = game.ToString();
            outfitMerge[@"outfits"] = outfits;
            return JsonConvert.SerializeObject(outfitMerge, Formatting.Indented);
        }

        /// <summary>
        /// Generates squadmate outfit merge files for the specified henchmen
        /// </summary>
        /// <param name="game">The game to generate for</param>
        /// <param name="henchName">The internal name of the henchman</param>
        /// <param name="dlcFolderPath">The path to the DLC folder root to modify</param>
        /// <param name="outfits">The list of outfits to append to</param>
        /// <returns>Error if failed, null if OK</returns>
        public static string GenerateSquadmateMergeFiles(MEGame game, string henchName, string dlcFolderPath, List<Dictionary<string, object>> outfits)
        {
            // Setup
            var dlcName = Path.GetFileName(dlcFolderPath);
            var henchHumanName = GetHumanName(henchName);
            var cookedPath = Path.Combine(dlcFolderPath, game.CookedDirName());
            var sourcefiles = new List<string>();
            var sourceBaseDir = BackupService.GetGameBackupPath(game);
            if (sourceBaseDir == null || !Directory.Exists(sourceBaseDir))
            {
                M3Log.Warning($@"No backup available for {game}");
                return $"No backup available for {game}, cannot generate squadmate merge files";
            }
            var sourceBaseFiles = MELoadedFiles.GetFilesLoadedInGame(game, true, gameRootOverride: sourceBaseDir);

            // File list
            // Main
            sourcefiles.Add($@"BioH_{henchName}_00.pcc");
            sourcefiles.Add($@"BioH_{henchName}_00_Explore.pcc");

            // Localizations
            sourcefiles.Add($@"BioH_{henchName}_00_LOC_DEU.pcc");
            sourcefiles.Add($@"BioH_{henchName}_00_LOC_FRA.pcc");
            sourcefiles.Add($@"BioH_{henchName}_00_LOC_INT.pcc");
            sourcefiles.Add($@"BioH_{henchName}_00_LOC_ITA.pcc");
            sourcefiles.Add($@"BioH_{henchName}_00_Explore_LOC_DEU.pcc");
            sourcefiles.Add($@"BioH_{henchName}_00_Explore_LOC_FRA.pcc");
            sourcefiles.Add($@"BioH_{henchName}_00_Explore_LOC_INT.pcc");
            sourcefiles.Add($@"BioH_{henchName}_00_Explore_LOC_ITA.pcc");

            // Step 1: Verify files

            foreach (var f in sourcefiles)
            {
                if (!sourceBaseFiles.TryGetValue(f, out var _))
                {
                    M3Log.Warning($@"Required file for squadmate merge not available in backup: {f}");
                    return $"Required file for squadmate merge not available in backup: {f}";
                }
            }

            var isourcefname = $"SFXHenchImages{henchHumanName}0.pcc";
            if (!sourceBaseFiles.TryGetValue(isourcefname, out var _))
            {
                M3Log.Warning($@"Required file for squadmate merge not available in backup: {isourcefname}");
                return $"Required file for squadmate merge not available in backup: {isourcefname}";
            }

            M3Log.Information(@"Squadmate merge generator: all required source files found in backup");

            // Step 2: Copy files
            foreach (var f in sourcefiles)
            {
                var path = sourceBaseFiles[f];
                var destFName = Path.GetFileName(f);
                destFName = destFName.Replace(henchName, $"{henchName}_{dlcName}");

                var destpath = Path.Combine(cookedPath, destFName);

                using var package = MEPackageHandler.OpenMEPackage(path);
                ReplaceNameIfExists(package, $@"BioH_{henchName}_00", $@"BioH_{henchName}_{dlcName}_00");
                ReplaceNameIfExists(package, $@"BioH_{henchName}_00_Explore", $@"BioH_{henchName}_{dlcName}_00_Explore");
                ReplaceNameIfExists(package, @"VariantA", $@"Variant{dlcName}");
                ReplaceNameIfExists(package, $@"{henchHumanName}A_Combat", $@"{henchHumanName}{dlcName}_Combat");
                ReplaceNameIfExists(package, $@"{henchHumanName}A_EX_Combat", $@"{henchHumanName}{dlcName}_EX_Combat");
                ReplaceNameIfExists(package, $@"{henchHumanName}A_Conversation", $@"{henchHumanName}{dlcName}_Conversation");
                package.Save(destpath);
            }

            // Step 3: Add hench images package

            var idestpath = Path.Combine(cookedPath, $@"SFXHenchImages_{dlcName}.pcc");
            if (File.Exists(idestpath))
            {
                // Edit existing package
                using var ipackage = MEPackageHandler.OpenMEPackage(idestpath);
                var texToClone = ipackage.Exports.FirstOrDefault(x => x.ClassName == @"Texture2D");

                // Available
                var exp = EntryCloner.CloneEntry(texToClone);
                AddToObjectReferencer(exp);
                exp.ObjectName = new NameReference($"{henchHumanName}0", 0);
                var t2d = new Texture2D(exp);
                var imageBytes = M3Utilities.ExtractInternalFileToStream(@"ME3TweaksModManager.modmanager.starterkit.henchimages.placeholder_available.png").GetBuffer();
                t2d.Replace(Image.LoadFromFileMemory(imageBytes, 2, PixelFormat.ARGB), exp.GetProperties(), isPackageStored: true);

                // Silouette
                exp = EntryCloner.CloneEntry(texToClone);
                AddToObjectReferencer(exp);
                exp.ObjectName = new NameReference($"{henchHumanName}0_locked", 0);
                t2d = new Texture2D(exp);
                imageBytes = M3Utilities.ExtractInternalFileToStream(@"ME3TweaksModManager.modmanager.starterkit.henchimages.placeholder_silo.png").GetBuffer();
                t2d.Replace(Image.LoadFromFileMemory(imageBytes, 2, PixelFormat.ARGB), exp.GetProperties(), isPackageStored: true);

                // Chosen
                exp = EntryCloner.CloneEntry(texToClone);
                AddToObjectReferencer(exp);
                exp.ObjectName = new NameReference($"{henchHumanName}0Glow", 0);
                t2d = new Texture2D(exp);
                imageBytes = M3Utilities.ExtractInternalFileToStream(@"ME3TweaksModManager.modmanager.starterkit.henchimages.placeholder_chosen.png").GetBuffer();
                t2d.Replace(Image.LoadFromFileMemory(imageBytes, 2, PixelFormat.ARGB), exp.GetProperties(), isPackageStored: true);

                ipackage.Save();
            }
            else
            {
                // Generate new package
                using var ipackage = MEPackageHandler.OpenMEPackage(sourceBaseFiles[isourcefname]);

                // Available
                var exp = ipackage.FindExport($@"GUI_Henchmen_Images.{henchHumanName}0");
                var t2d = new Texture2D(exp);
                var imageBytes = M3Utilities.ExtractInternalFileToStream(@"ME3TweaksModManager.modmanager.starterkit.henchimages.placeholder_available.png").GetBuffer();
                t2d.Replace(Image.LoadFromFileMemory(imageBytes, 2, PixelFormat.ARGB), exp.GetProperties(), isPackageStored: true);

                // Silouette
                exp = ipackage.FindExport($"GUI_Henchmen_Images.{henchHumanName}0_locked");
                t2d = new Texture2D(exp);
                imageBytes = M3Utilities.ExtractInternalFileToStream(@"ME3TweaksModManager.modmanager.starterkit.henchimages.placeholder_silo.png").GetBuffer();
                t2d.Replace(Image.LoadFromFileMemory(imageBytes, 2, PixelFormat.ARGB), exp.GetProperties(), isPackageStored: true);

                // Chosen
                exp = ipackage.FindExport($"GUI_Henchmen_Images.{henchHumanName}0Glow");
                t2d = new Texture2D(exp);
                imageBytes = M3Utilities.ExtractInternalFileToStream(@"ME3TweaksModManager.modmanager.starterkit.henchimages.placeholder_chosen.png").GetBuffer();
                t2d.Replace(Image.LoadFromFileMemory(imageBytes, 2, PixelFormat.ARGB), exp.GetProperties(), isPackageStored: true);

                ReplaceNameIfExists(ipackage, $@"GUI_Henchmen_Images", $@"GUI_Henchmen_Images_{dlcName}");
                ReplaceNameIfExists(ipackage, $@"SFXHenchImages{henchHumanName}0", $@"SFXHenchImages_{dlcName}");
                ipackage.Save(idestpath);
            }

            // Step 4: Add squadmate outfit merge to the list
            var outfit = new Dictionary<string, object>();
            outfit[@"henchname"] = henchName;
            outfit[@"henchpackage"] = $@"BioH_{henchName}_{dlcName}_00";
            outfit[@"highlightimage"] = $@"GUI_Henchmen_Images_{dlcName}.{henchHumanName}0Glow";
            outfit[@"availableimage"] = $@"GUI_Henchmen_Images_{dlcName}.{henchHumanName}0";
            outfit[@"silhouetteimage"] = $@"GUI_Henchmen_Images_{dlcName}.{henchHumanName}0_locked";
            outfit[@"deadimage"] = @"GUI_Henchmen_Images.PlaceHolder";
            outfit[@"descriptiontext0"] = 0;
            outfit[@"customtoken0"] = 0;
            outfits.Add(outfit);

            return null;
        }

        private static string GetHumanName(string henchName)
        {
            if (henchName == "Marine") return "James";
            return henchName;
        }

        private static void ReplaceNameIfExists(IMEPackage package, string originalName, string newName)
        {
            var idx = package.findName(originalName);
            if (idx >= 0)
            {
                package.replaceName(idx, newName);
            }
        }
        #endregion

        #region Plot Manager 

        #endregion

        #region 2DA

        #endregion
    }
}
