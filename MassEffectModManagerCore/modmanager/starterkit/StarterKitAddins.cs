using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Coalesced.Xml;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using LegendaryExplorerCore.UnrealScript;
using Microsoft.WindowsAPICodePack.NativeAPI.Consts;
using Newtonsoft.Json;

namespace ME3TweaksModManager.modmanager.starterkit
{
    /// <summary>
    /// Info about what to add to a coalesced file
    /// </summary>
    internal class CoalescedEntryInfo
    {
        public string File { get; set; }
        public string Section { get; set; }
        public CoalesceProperty Property { get; set; }
    }
    /// <summary>
    /// Adds addition features to a DLC mod
    /// </summary>
    internal class StarterKitAddins
    {
        #region STARTUP FILE
        /// <summary>
        /// Generates a startup file for the specified game
        /// </summary>
        /// <param name="game">Game to generate for. Cannot generate ME1 startup files</param>
        /// <param name="dlcFolderPath">The path to the root of the DLC folder</param>
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
            var startupPackageProp = new CoalesceProperty("DLCStartupPackage", new CoalesceValue(Path.GetFileNameWithoutExtension(startupFName), CoalesceParseAction.AddUnique));
            var configInfo = new CoalescedEntryInfo() { File = "BioEngine", Section = "Engine.StartupPackages", Property = startupPackageProp };
            if (game == MEGame.LE1)
            {
                AddStartupCoalescedRefLE1(dlcFolderPath, startupFName);
            }
            else if (game.IsGame2())
            {
                AddCoalescedEntryGame2(dlcName, cookedPath, configInfo);
            }
            else if (game.IsGame3())
            {
                AddCoalescedEntryGame3(dlcName, cookedPath, configInfo);
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
        public static void GeneratePlotData(MEGame game, string dlcFolderPath)
        {
            // Startup file
            var dlcName = Path.GetFileName(dlcFolderPath);
            var cookedPath = Path.Combine(dlcFolderPath, game.CookedDirName());
            var startupFName = $"Startup{dlcName.Substring(3)}.pcc"; // "DLC|_"
            var startupPackagePath = Path.Combine(cookedPath, startupFName);
            if (!File.Exists(startupPackagePath))
            {
                // Generate startup file (it's required)
                AddStartupFile(game, dlcFolderPath);
            }

            if (game.IsGame2())
            {
                // We need to add the conditionals
                var plotManagerPackageName = AddConditionalsClass(startupPackagePath, dlcName);
                AddCoalescedReference(game, dlcName, cookedPath, "BioEngine", "Engine.StartupPackages", "Package", plotManagerPackageName, CoalesceParseAction.AddUnique);

                var bio2daPackageName = AddBio2DAGame2(game, dlcName, startupPackagePath);
                AddCoalescedReference(game, dlcName, cookedPath, "BioEngine", "Engine.StartupPackages", "Package", bio2daPackageName, CoalesceParseAction.AddUnique);


                // Must also add to biogame
                AddCoalescedReference(game, dlcName, cookedPath, "BioGame", "SFXGame.BioWorldInfo", "ConditionalClasses", $"{plotManagerPackageName}.BioAutoConditionals", CoalesceParseAction.AddUnique);
            }

            // Generate the maps
            var plotAutoPackageName = AddPlotManagerAuto(startupPackagePath, dlcName);

            if (game.IsGame1())
            {
                // Add to the autoload?
            }
            else
            {
                // Add to Coalesced
                AddCoalescedReference(game, dlcName, cookedPath, "BioEngine", "Engine.StartupPackages", "Package", plotAutoPackageName, CoalesceParseAction.AddUnique);
            }

            if (game.IsGame3())
            {
                // Conditionals file
                CNDFile c = new CNDFile();
                c.ToFile(Path.Combine(dlcFolderPath, game.CookedDirName(), $@"Conditionals_{Path.GetFileName(dlcFolderPath)}.cnd"));
            }
        }

        private static string AddBio2DAGame2(MEGame game, string dlcName, string startupPackagePath)
        {
            using var startupFile = MEPackageHandler.OpenMEPackage(startupPackagePath);
            var plotPackageName = $@"BIOG_2DA_{dlcName.Substring(4)}_PlotManager_X"; // DLC_|<stuff>
            var plotPackageExport = startupFile.FindExport(plotPackageName);
            if (plotPackageExport == null)
            {
                // Create the export
                plotPackageExport = ExportCreator.CreatePackageExport(startupFile, plotPackageName);
            }

            var rand = new Random();
            var cols = new[] { @"Id", @"Credits", @"Eezo", @"Palladium", @"Platinum", @"Iridium", @"Description" };
            Create2DA(plotPackageExport, new NameReference(@"Plot_Treasure_Resources_part", rand.Next(100000) + 1000), cols, true);

            cols = new[] { @"nmLevel", @"nmTreasure", @"nmTech", @"nmResource", @"nPrice", @"nmRequiredTech", @"nRequiredTechLevel", @"nNoAnimation", @"nMultiLevel" };
            Create2DA(plotPackageExport, new NameReference(@"Plot_Treasure_Treasure_part", rand.Next(100000) + 1000), cols, false);

            if (startupFile.IsModified)
                startupFile.Save();
            return plotPackageName;
        }

        private static ExportEntry Create2DA(IEntry parent, NameReference objectName, string[] columns, bool isStandard2DA)
        {
            // Test if exists first - do not overwrite
            var fulltest = $@"{parent.InstancedFullPath}.{objectName}";
            var exp = parent.FileRef.FindExport(fulltest);
            if (exp != null)
                return exp; // Return existing

            // Generate it 
            var className = isStandard2DA ? @"Bio2DA" : @"Bio2DANumberedRows";
            var rop = new RelinkerOptionsPackage { ImportExportDependencies = true };
            exp = new ExportEntry(parent.FileRef, parent, objectName)
            { Class = EntryImporter.EnsureClassIsInFile(parent.FileRef, className, rop) };

            exp.ObjectFlags |= UnrealFlags.EObjectFlags.Public | UnrealFlags.EObjectFlags.LoadForClient | UnrealFlags.EObjectFlags.LoadForServer | UnrealFlags.EObjectFlags.Standalone;
            exp.ExportFlags |= UnrealFlags.EExportFlags.ForcedExport;

            // Since table is blank we don't need to care about the column names property
            Bio2DA bio2DA = new Bio2DA();
            bio2DA.Cells = new Bio2DACell[0, 0];
            foreach (var c in columns)
            {
                bio2DA.AddColumn(c);
            }
            bio2DA.Write2DAToExport(exp);

            parent.FileRef.AddExport(exp);
            return exp;
        }


        /// <summary>
        /// Adds BioAutoConditionals
        /// </summary>
        /// <param name="startupPackagePath"></param>
        /// <param name="dlcName"></param>
        /// <returns>Name of package export 'PlotManager[DLCNAME]' that is added to coalesced</returns>
        private static string AddConditionalsClass(string startupPackagePath, string dlcName)
        {
            using var startupFile = MEPackageHandler.OpenMEPackage(startupPackagePath);
            var sfPlotExportName = $@"PlotManager{dlcName}";
            var sfPlotExport = startupFile.FindExport(sfPlotExportName);
            if (sfPlotExport == null)
            {
                // Create the export
                sfPlotExport = ExportCreator.CreatePackageExport(startupFile, sfPlotExportName);
            }

            var lib = new FileLib(startupFile);
            lib.Initialize();
            var scriptText = @"Class BioAutoConditionals extends BioConditionals; public function bool FTemplateFunction(BioWorldInfo bioWorld, int Argument){ local BioGlobalVariableTable gv; gv = bioWorld.GetGlobalVariables(); return TRUE; } defaultproperties { }";
            UnrealScriptCompiler.CompileClass(startupFile, scriptText, lib, parent: sfPlotExport);

            if (startupFile.IsModified)
                startupFile.Save();

            return sfPlotExportName;
        }

        /// <summary>
        /// Adds plot manager maps (codex, consequence, journal, etc)
        /// </summary>
        /// <param name="startupPackagePath"></param>
        /// <param name="dlcName"></param>
        /// <returns>'PlotManagerAuto[DLCName]' for adding to startup packages</returns>
        private static string AddPlotManagerAuto(string startupPackagePath, string dlcName)
        {
            using var startupFile = MEPackageHandler.OpenMEPackage(startupPackagePath);
            var sfPlotExportName = $@"PlotManagerAuto{dlcName}";
            var sfPlotExport = startupFile.FindExport(sfPlotExportName);
            if (sfPlotExport == null)
            {
                // Create the export
                sfPlotExport = ExportCreator.CreatePackageExport(startupFile, sfPlotExportName);
            }

            // Generate the map exports
            AddToObjectReferencer(GeneratePlotManagerAutoExport(sfPlotExport, "DataCodexMap", "BioCodexMap", 2));
            AddToObjectReferencer(GeneratePlotManagerAutoExport(sfPlotExport, "ConsequenceMap", "BioConsequenceMap", 1));
            AddToObjectReferencer(GeneratePlotManagerAutoExport(sfPlotExport, "OutcomeMap", "BioOutcomeMap", 1));
            AddToObjectReferencer(GeneratePlotManagerAutoExport(sfPlotExport, "QuestMap", "BioQuestMap", 4)); // Journal
            AddToObjectReferencer(GeneratePlotManagerAutoExport(sfPlotExport, "StateTransitionMap", "BioStateEventMap", 1));
            startupFile.Save();

            return sfPlotExportName;
        }


        /// <summary>
        /// Generates one of the plot manager auto exports
        /// </summary>
        /// <param name="parent">The plot manager auto package export</param>
        /// <param name="objectName">The name of the export</param>
        /// <param name="className">Name of class of export to generate</param>
        /// <param name="numZerosBinary">Number of 4 byte 0s to add as binary data (for empty binary)</param>
        /// <returns>The generated export</returns>
        private static ExportEntry GeneratePlotManagerAutoExport(ExportEntry parent, string objectName, string className, int numZerosBinary)
        {
            // Test if exists first - do not overwrite
            var fulltest = $@"{parent.InstancedFullPath}.{objectName}";
            var exp = parent.FileRef.FindExport(fulltest);
            if (exp != null)
                return exp; // Return existing

            // Generate it 
            var rop = new RelinkerOptionsPackage { ImportExportDependencies = true };
            exp = new ExportEntry(parent.FileRef, parent, objectName)
            { Class = EntryImporter.EnsureClassIsInFile(parent.FileRef, className, rop) };
            exp.ObjectFlags |= UnrealFlags.EObjectFlags.Public | UnrealFlags.EObjectFlags.LoadForClient | UnrealFlags.EObjectFlags.LoadForServer | UnrealFlags.EObjectFlags.Standalone;
            exp.ExportFlags |= UnrealFlags.EExportFlags.ForcedExport;
            exp.WriteBinary(new byte[4 * numZerosBinary]); // Blank data
            parent.FileRef.AddExport(exp);
            return exp;
        }
        #endregion

        #region 2DA

        #endregion

        #region Utility 

        /// <summary>
        /// Adds an item to Coalesced
        /// </summary>
        /// <param name="game">Game to add to</param>
        /// <param name="dlcName">The name of the DLC</param>
        /// <param name="cookedPath">The path of the CookedPCConsole folder</param>
        /// <param name="configFilename">The name of the config file</param>
        /// <param name="sectionName">The section name in the config file</param>
        /// <param name="key">The key of the property (property name)</param>
        /// <param name="value">The value of the property</param>
        /// <param name="parseAction">How the property should be applied</param>
        private static void AddCoalescedReference(MEGame game, string dlcName, string cookedPath, string configFilename, string sectionName, string key, string value, CoalesceParseAction parseAction)
        {
            var prop = new CoalesceProperty(key, new CoalesceValue(value, parseAction));
            var info = new CoalescedEntryInfo() { File = configFilename, Section = sectionName, Property = prop };
            if (game.IsGame2())
            {
                AddCoalescedEntryGame2(dlcName, cookedPath, info);
            }
            else if (game.IsGame3())
            {
                AddCoalescedEntryGame3(dlcName, cookedPath, info);
            }
        }

        /// <summary>
        /// Adds game 2 coalesced entry
        /// </summary>
        /// <param name="dlcName">Name of DLC</param>
        /// <param name="cookedPath">Directory of CookedPCConsole</param>
        /// <param name="info">Info about what to add</param>
        private static void AddCoalescedEntryGame2(string dlcName, string cookedPath, CoalescedEntryInfo info)
        {
            // Add to the coalesced
            var actualFileName = $@"{Path.GetFileNameWithoutExtension(info.File)}.ini";
            var iniFile = Path.Combine(cookedPath, actualFileName);
            CoalesceAsset configIni;
            if (!File.Exists(iniFile))
            {
                // No contents.
                configIni = ConfigFileProxy.ParseIni(@"");
            }
            else
            {
                configIni = ConfigFileProxy.LoadIni(iniFile);
            }

            var sp = configIni.GetOrAddSection(info.Section);
            sp.AddEntryIfUnique(info.Property);
            File.WriteAllText(iniFile, configIni.GetGame2IniText());
        }

        /// <summary>
        /// Applies a change to a Game 3 coalesced file
        /// </summary>
        /// <param name="dlcName"></param>
        /// <param name="cookedPath"></param>
        /// <param name="startupInfo"></param>
        private static void AddCoalescedEntryGame3(string dlcName, string cookedPath, CoalescedEntryInfo startupInfo)
        {
            // Todo: Non-saving mode to improve performance

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
            var file = iniFiles[$@"{Path.GetFileNameWithoutExtension(startupInfo.File)}.xml"]; // Ensure we don't use extension in provided file.
            var section = file.GetOrAddSection(startupInfo.Section);
            section.AddEntryIfUnique(startupInfo.Property);

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

        /// <summary>
        /// Adds the specified entry to the object referencer in the package. If there is no object referencer already added then this does nothing.
        /// </summary>
        /// <param name="entry">The entry to add. It is not checked if it is already in the list</param>
        /// <returns>If object reference was added</returns>
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
    }
}
