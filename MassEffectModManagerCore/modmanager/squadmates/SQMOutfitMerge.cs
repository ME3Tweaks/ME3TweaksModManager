using System.Threading.Tasks;
using System.Xml.Linq;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.UnrealScript;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using ME3TweaksCore.Config;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Helpers;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.gameini;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.merge.dlc;
using ME3TweaksModManager.modmanager.merge.dlc.LE2;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.merge.squadmate;
using Microsoft.WindowsAPICodePack.NativeAPI.Consts;
using Newtonsoft.Json;

namespace ME3TweaksModManager.modmanager.squadmates
{
    public class SQMOutfitMerge
    {
        public const string SQUADMATE_MERGE_MANIFEST_FILE = @"SquadmateMergeInfo.sqm";
        public const int STARTING_OUTFIT_CONDITIONAL = 10000;

        internal class SquadmateMergeInfo
        {
            [JsonProperty(@"game")]
            public MEGame Game { get; set; }

            [JsonProperty(@"outfits")]
            public List<SquadmateInfoSingle> Outfits { get; set; }

            public bool Validate(string dlcName, GameTargetWPF target, CaseInsensitiveDictionary<string> loadedFiles)
            {
                foreach (var outfit in Outfits)
                {
                    // Check packages
                    if (!loadedFiles.ContainsKey($@"{outfit.HenchPackage}.pcc"))
                    {
                        M3Log.Error($@"SquadmateMergeInfo failed validation: {outfit.HenchPackage}.pcc not found in game");
                        return false;
                    }

                    if (Game.IsGame3())
                    {
                        if (!loadedFiles.ContainsKey($@"{outfit.HenchPackage}_Explore.pcc"))
                        {
                            M3Log.Error($@"SquadmateMergeInfo failed validation: {outfit.HenchPackage}_Explore.pcc not found in game");
                            return false;
                        }
                    }

                    if (!loadedFiles.ContainsKey($@"SFXHenchImages_{dlcName}.pcc"))
                    {
                        M3Log.Error($@"SquadmateMergeInfo failed validation: SFXHenchImages_{dlcName}.pcc not found in game");
                        return false;
                    }
                }

                return true;
            }
        }

        private static StructProperty GeneratePlotStreamingElement(string packageName, int conditionalNum)
        {
            PropertyCollection pc = new PropertyCollection();
            pc.AddOrReplaceProp(new NameProperty(packageName, @"ChunkName"));
            pc.AddOrReplaceProp(new IntProperty(conditionalNum, @"Conditional"));
            pc.AddOrReplaceProp(new BoolProperty(false, @"bFallback"));
            pc.AddOrReplaceProp(new NoneProperty());

            return new StructProperty(@"PlotStreamingElement", pc);
        }

        private static int GetSquadmateOutfitInt(string squadmateName, MEGame game)
        {
            M3Log.Information($@"SQMMERGE: Generating outfit int for {game} {squadmateName}");
            if (game.IsGame2())
            {
                // This is if we ever implement it into Game 2
                switch (squadmateName)
                {
                    case @"Convict": return 314;
                    case @"Garrus": return 318;
                    case @"Geth": return 315;
                    case @"Grunt": return 322;
                    case @"Leading": return 313;
                    case @"Mystic": return 323;
                    case @"Professor": return 321;
                    case @"Tali": return 320;
                    case @"Thief": return 317;
                    case @"Veteran": return 324;
                    case @"Vixen": return 312;

                        // LOTSB doesn't use plot streaming for liara
                        // case @"Liara": return 312;
                }
            }
            else if (game.IsGame3())
            {
                switch (squadmateName)
                {
                    case @"Liara": return 10152;
                    case @"Kaidan": return 10153;
                    case @"Ashley": return 10154;
                    case @"Garrus": return 10155;
                    case @"EDI": return 10156;
                    case @"Prothean": return 10157;
                    case @"Marine": return 10158;
                    case @"Tali": return 10214;
                        // case @"Wrex": return ??; Wrex outfit can't be changed, its hardcoded to 13, which is TRUE
                }
            }

            throw new Exception(M3L.GetString(M3L.string_interp_invalidHenchNameSquadmateNameValueIsCaseSensitive, squadmateName));
        }

        /// <summary>
        /// Returns if the specified target has any squadmate outfit merge files.
        /// </summary>
        /// <param name="target"></param>
        public static bool NeedsMerged(GameTargetWPF target)
        {
            if (!target.Game.IsGame3() && target.Game != MEGame.LE2) return false;
            var sqmSupercedances = M3Directories.GetFileSupercedances(target, new[] { @".sqm" });
            return sqmSupercedances.TryGetValue(SQUADMATE_MERGE_MANIFEST_FILE, out var infoList) && infoList.Count > 0;
        }

        /// <summary>
        /// Generates squadmate outfit information for Game 3 and LE2. The merge DLC must be already generated.
        /// </summary>
        /// <param name="mergeDLC"></param>
        /// <exception cref="Exception"></exception>
        public static void RunSquadmateOutfitMerge(M3MergeDLC mergeDLC)
        {
            if (!mergeDLC.Generated)
                return; // Do not run on non-generated. It may be that a prior check determined this merge was not necessary 

            var loadedFiles = MELoadedFiles.GetFilesLoadedInGame(mergeDLC.Target.Game, gameRootOverride: mergeDLC.Target.TargetPath);
            //var mergeFiles = loadedFiles.Where(x =>
            //    x.Key.StartsWith(@"BioH_") && x.Key.Contains(@"_DLC_MOD_") && x.Key.EndsWith(@".pcc") && !x.Key.Contains(@"_LOC_") && !x.Key.Contains(@"_Explore."));

            M3Log.Information(@"SQMMERGE: Building BioP_Global");
            var appearanceInfo = new CaseInsensitiveDictionary<List<SquadmateInfoSingle>>();

            int appearanceId = mergeDLC.Target.Game.IsGame3() ? 255 : 3; // starting // LE2 is 0-8, LE3 does not care
            int currentConditional = STARTING_OUTFIT_CONDITIONAL;

            // Scan squadmate merge files
            var sqmSupercedances = M3Directories.GetFileSupercedances(mergeDLC.Target, new[] { @".sqm" });
            var squadmateImageInfosLE2 = new List<LE2SquadmateImageInfo>();

            if (sqmSupercedances.TryGetValue(SQUADMATE_MERGE_MANIFEST_FILE, out var infoList))
            {
                infoList.Reverse();
                foreach (var dlc in infoList)
                {
                    M3Log.Information($@"SQMMERGE: Processing {dlc}");

                    var jsonFile = Path.Combine(M3Directories.GetDLCPath(mergeDLC.Target), dlc, mergeDLC.Target.Game.CookedDirName(), SQUADMATE_MERGE_MANIFEST_FILE);
                    var infoPackage = JsonConvert.DeserializeObject<SquadmateMergeInfo>(File.ReadAllText(jsonFile));
                    if (!infoPackage.Validate(dlc, mergeDLC.Target, loadedFiles))
                    {
                        continue; // skip this
                    }

                    IMEPackage imagePackage = null; // Not used for LE3
                    if (mergeDLC.Target.Game == MEGame.LE2)
                    {
                        var henchImagesP = Path.Combine(mergeDLC.Target.GetDLCPath(), dlc, mergeDLC.Target.Game.CookedDirName(), $@"SFXHenchImages_{dlc}.pcc");
                        imagePackage = MEPackageHandler.OpenMEPackage(henchImagesP);
                    }

                    // Enumerate all outfits listed for a single squadmate
                    foreach (var outfit in infoPackage.Outfits)
                    {
                        List<SquadmateInfoSingle> list;

                        // See if we already have an outfit list for this squadmate, maybe from another mod...
                        if (!appearanceInfo.TryGetValue(outfit.HenchName, out list))
                        {
                            list = new List<SquadmateInfoSingle>();
                            appearanceInfo[outfit.HenchName] = list;
                        }

                        outfit.ConditionalIndex = currentConditional++; // This is always incremented, so it might appear out of order in game files depending on how mod order is processed, that should be okay though.

                        if (mergeDLC.Target.Game.IsGame2())
                        {
                            // This is the 'slot' of the outfit for this squadmate
                            outfit.AppearanceId = list.Any() ? (list.MaxBy(x => x.AppearanceId).AppearanceId + 1) : GetFirstAvailableSquadmateAppearanceIndexLE2(outfit.HenchName); // Get first unused slot

                            // Todo: If higher than 9 we have too many outfits!!!!
                            if (outfit.AppearanceId > 9)
                            {
                                M3Log.Error(@"Squadmate outfit merge for LE2 only supports 9 outfits per character currently!");
                            }

                            var availableImage = imagePackage.FindExport(outfit.AvailableImage);
                            if (availableImage == null)
                            {
                                M3Log.Error($@"Available image {outfit.AvailableImage} not found in package: {imagePackage.FilePath}");
                                continue;
                            }

                            var selectedImage = imagePackage.FindExport(outfit.HighlightImage);
                            if (selectedImage == null)
                            {
                                M3Log.Error($@"Selected image {outfit.HighlightImage} not found in package: {imagePackage.FilePath}");
                                continue;
                            }

                            // Add the source exports to the porting list
                            squadmateImageInfosLE2.Add(new LE2SquadmateImageInfo()
                            {
                                SourceExport = availableImage,
                                DestinationTextureName = GetTextureExportNameForSquadmateLE2(outfit.HenchName, outfit.AppearanceId, false)
                            });

                            squadmateImageInfosLE2.Add(new LE2SquadmateImageInfo()
                            {
                                SourceExport = selectedImage,
                                DestinationTextureName = GetTextureExportNameForSquadmateLE2(outfit.HenchName, outfit.AppearanceId, true)
                            });
                        }
                        else if (mergeDLC.Target.Game.IsGame3())
                        {
                            // Must be fully unique
                            outfit.AppearanceId = appearanceId++; // may need adjusted
                        }
                        outfit.DLCName = dlc;
                        list.Add(outfit);
                        M3Log.Information($@"SQMMERGE: ConditionalIndex for {outfit.HenchName} appearanceid {outfit.AppearanceId}: {outfit.ConditionalIndex}");
                    }

                    //Debug.WriteLine("hi");
                }
            }

            if (appearanceInfo.Any())
            {
                var biopGlobal = MEPackageHandler.OpenMEPackage(loadedFiles[@"BioP_Global.pcc"]);
                var lsk = biopGlobal.Exports.FirstOrDefault(x => x.ClassName == @"LevelStreamingKismet");
                var persistentLevel = biopGlobal.FindExport(@"TheWorld.PersistentLevel");

                // Clone LevelStreamingKismets
                foreach (var sqm in appearanceInfo.Values)
                {
                    foreach (var outfit in sqm)
                    {
                        var fName = outfit.HenchPackage;
                        var newLSK = EntryCloner.CloneEntry(lsk);
                        newLSK.WriteProperty(new NameProperty(fName, @"PackageName"));

                        if (mergeDLC.Target.Game.IsGame3())
                        {
                            // Game 3 has _Explore files too
                            fName += @"_Explore";
                            newLSK = EntryCloner.CloneEntry(lsk);
                            newLSK.WriteProperty(new NameProperty(fName, @"PackageName"));
                        }
                    }
                }

                // Update BioWorldInfo
                // Doesn't have consistent number so we can't find it by instanced full path
                var bioWorldInfo = biopGlobal.Exports.FirstOrDefault(x => x.ClassName == @"BioWorldInfo");

                var props = bioWorldInfo.GetProperties();

                // Update Plot Streaming
                var plotStreaming = props.GetProp<ArrayProperty<StructProperty>>(@"PlotStreaming");
                foreach (var sqm in appearanceInfo.Values)
                {
                    foreach (var outfit in sqm)
                    {
                        // find item to add to
                        buildPlotElementObject(plotStreaming, outfit, mergeDLC.Target.Game, false);
                        if (mergeDLC.Target.Game.IsGame3())
                        {
                            buildPlotElementObject(plotStreaming, outfit, mergeDLC.Target.Game, true);
                        }
                    }
                }


                // Update StreamingLevels
                var streamingLevels = props.GetProp<ArrayProperty<ObjectProperty>>(@"StreamingLevels");
                streamingLevels.ReplaceAll(biopGlobal.Exports.Where(x => x.ClassName == @"LevelStreamingKismet").Select(x => new ObjectProperty(x)));

                bioWorldInfo.WriteProperties(props);

                // Save BioP_Global into DLC
                var cookedDir = Path.Combine(M3Directories.GetDLCPath(mergeDLC.Target), M3MergeDLC.MERGE_DLC_FOLDERNAME, mergeDLC.Target.Game.CookedDirName());
                var outP = Path.Combine(cookedDir, @"BioP_Global.pcc");
                biopGlobal.Save(outP);

                // Generate conditionals file
                if (mergeDLC.Target.Game.IsGame3())
                {
                    // ME3/LE3
                    CNDFile cnd = new CNDFile();
                    cnd.ConditionalEntries = new List<CNDFile.ConditionalEntry>();

                    foreach (var sqm in appearanceInfo.Values)
                    {
                        foreach (var outfit in sqm)
                        {
                            var scText = $@"(plot.ints[{GetSquadmateOutfitInt(outfit.HenchName, mergeDLC.Target.Game)}] == i{outfit.MemberAppearanceValue})";
                            var compiled = ME3ConditionalsCompiler.Compile(scText);
                            cnd.ConditionalEntries.Add(new CNDFile.ConditionalEntry()
                            { Data = compiled, ID = outfit.ConditionalIndex });
                        }
                    }

                    cnd.ToFile(Path.Combine(cookedDir, $@"Conditionals{M3MergeDLC.MERGE_DLC_FOLDERNAME}.cnd"));
                }
                else if (mergeDLC.Target.Game.IsGame2())
                {
                    // LE2
                    var startupF = Path.Combine(cookedDir, $@"Startup_{M3MergeDLC.MERGE_DLC_FOLDERNAME}.pcc");
                    var startup = MEPackageHandler.OpenMEPackageFromStream(M3Utilities.GetResourceStream($@"ME3TweaksModManager.modmanager.merge.dlc.{mergeDLC.Target.Game}.Startup_{M3MergeDLC.MERGE_DLC_FOLDERNAME}.pcc"), $@"Startup_{M3MergeDLC.MERGE_DLC_FOLDERNAME}.pcc");
                    var conditionalClass = startup.FindExport($@"PlotManager{M3MergeDLC.MERGE_DLC_FOLDERNAME}.BioAutoConditionals");

                    // Add Conditional Functions
                    FileLib fl = new FileLib(startup);
                    bool initialized = fl.Initialize(new RelativePackageCache() { RootPath = mergeDLC.Target.GetBioGamePath() }, mergeDLC.Target.TargetPath);
                    if (!initialized)
                    {
                        throw new Exception(@"FileLib for script update could not initialize, cannot install conditionals");
                    }


                    var scTextOrig = new StreamReader(M3Utilities.GetResourceStream(
                            $@"ME3TweaksModManager.modmanager.squadmates.{mergeDLC.Target.Game}.HasOutfitOnConditional.txt"))
                        .ReadToEnd();
                    foreach (var sqm in appearanceInfo.Values)
                    {
                        foreach (var outfit in sqm)
                        {
                            var scText = scTextOrig.Replace(@"%CONDITIONALNUM%", outfit.ConditionalIndex.ToString());
                            scText = scText.Replace(@"%SQUADMATEOUTFITPLOTINT%", GetSquadmateOutfitInt(outfit.HenchName, MEGame.LE2).ToString());
                            scText = scText.Replace(@"%OUTFITINDEX%", outfit.MemberAppearanceValue.ToString());

                            MessageLog log = UnrealScriptCompiler.AddOrReplaceInClass(conditionalClass, scText, fl);
                            if (log.AllErrors.Any())
                            {
                                M3Log.Error($@"Error compiling function F{outfit.ConditionalIndex}:");
                                foreach (var l in log.AllErrors)
                                {
                                    M3Log.Error(l.Message);
                                }

                                throw new Exception(M3L.GetString(M3L.string_interp_errorCompilingConditionalFunction, $@"F{outfit.ConditionalIndex}", string.Join('\n', log.AllErrors.Select(x => x.Message))));
                            }
                        }
                    }


                    // Relink the conditionals chain
                    //UClass uc = ObjectBinary.From<UClass>(conditionalClass);
                    //uc.UpdateLocalFunctions();
                    //uc.UpdateChildrenChain();
                    //conditionalClass.WriteBinary(uc);

                    startup.Save(startupF);
                }


                // Add startup package, member appearances
                if (mergeDLC.Target.Game.IsGame2())
                {
                    var configBundle = ConfigAssetBundle.FromDLCFolder(mergeDLC.Target.Game, cookedDir, M3MergeDLC.MERGE_DLC_FOLDERNAME);

                    // add startup file
                    var bioEngine = configBundle.GetAsset(@"BIOEngine.ini");
                    var startupSection = bioEngine.GetOrAddSection(@"Engine.StartupPackages");
                    startupSection.AddEntry(new CoalesceProperty(@"DLCStartupPackage", new CoalesceValue($@"Startup_{M3MergeDLC.MERGE_DLC_FOLDERNAME}", CoalesceParseAction.AddUnique)));
                    startupSection.AddEntry(new CoalesceProperty(@"Package", new CoalesceValue($@"PlotManager{M3MergeDLC.MERGE_DLC_FOLDERNAME}", CoalesceParseAction.AddUnique)));

                    // Add conditionals 
                    var bioGame = configBundle.GetAsset(@"BIOGame.ini");
                    var bioWorldInfoConfig = bioGame.GetOrAddSection(@"SFXGame.BioWorldInfo");
                    bioWorldInfoConfig.AddEntry(new CoalesceProperty(@"ConditionalClasses", new CoalesceValue($@"PlotManager{M3MergeDLC.MERGE_DLC_FOLDERNAME}.BioAutoConditionals", CoalesceParseAction.AddUnique)));

                    // Add appearances to list
                    var bioUi = configBundle.GetAsset(@"BIOUI.ini");
                    var partySelectionSection = bioUi.GetOrAddSection(@"SFXGame.BioSFHandler_PartySelection");

                    foreach (var sqm in appearanceInfo.Values)
                    {
                        foreach (var outfit in sqm)
                        {
                            //lstAppearances = (Tag = hench_tali, AddAppearance = 3, PlotFlag = -1);
                            var properties = new Dictionary<string, string>();
                            properties[@"Tag"] = $@"hench_{outfit.HenchName.ToLower()}";
                            properties[@"AddAppearance"] = outfit.AppearanceId.ToString();
                            properties[@"PlotFlag"] = outfit.PlotFlag.ToString();
                            var appearanceStruct = StringStructParser.BuildCommaSeparatedSplitValueList(properties);
                            partySelectionSection.AddEntry(new CoalesceProperty(@"lstAppearances",
                                new CoalesceValue(appearanceStruct, CoalesceParseAction.AddUnique)));
                        }

                        configBundle.CommitDLCAssets();

                        // Update squadmate images
                        // Create and patch BioH_SelectGUI for more squadmate images

                        // Lvl2/3/4 are LOTSB
                        var packagesToInjectInto = new[]
                            { @"BioH_SelectGUI.pcc", @"BioP_Exp1Lvl2.pcc", @"BioP_Exp1Lvl3.pcc", @"BioP_Exp1Lvl4.pcc" };
                        using var swfStream = M3Utilities.ExtractInternalFileToStream(
                            $@"ME3TweaksModManager.modmanager.merge.dlc.{mergeDLC.Target.Game}.TeamSelect.swf");
                        var swfData = swfStream.ToArray();
                        Parallel.ForEach(packagesToInjectInto, package =>
                        {

                            //foreach (var package in packagesToInjectInto)
                            //{
                            var packageF = loadedFiles[package];
                            using var packageP = MEPackageHandler.OpenMEPackage(packageF);

                            // Inject extended SWF
                            var swf = packageP.FindExport(@"GUI_SF_TeamSelect.TeamSelect");
                            var rawData = swf.GetProperty<ImmutableByteArrayProperty>(@"RawData");
                            rawData.Bytes = swfData;
                            swf.WriteProperty(rawData);

                            // Inject images
                            foreach (var squadmateImage in squadmateImageInfosLE2)
                            {
                                squadmateImage.InjectSquadmateImageIntoPackage(packageP);
                            }

                            packageP.Save(Path.Combine(cookedDir, package)); // Save into merge DLC
                        });
                        //}
                    }
                }
                else if (mergeDLC.Target.Game.IsGame3())
                {
                    var mergeCoalFile = Path.Combine(M3Directories.GetDLCPath(mergeDLC.Target), M3MergeDLC.MERGE_DLC_FOLDERNAME, mergeDLC.Target.Game.CookedDirName(), $@"Default_{M3MergeDLC.MERGE_DLC_FOLDERNAME}.bin");
                    var mergeCoal = CoalescedConverter.DecompileGame3ToMemory(new MemoryStream(File.ReadAllBytes(mergeCoalFile)));

                    // Member appearances
                    var bioUiDoc = XDocument.Parse(mergeCoal[@"BIOUI.xml"]);

                    foreach (var sqm in appearanceInfo.Values)
                    {
                        foreach (var outfit in sqm)
                        {
                            var entry = new Game3CoalescedValueEntry()
                            {
                                Section = @"sfxgame.sfxguidata_teamselect",
                                Name = @"selectappearances",
                                Type = 3,
                                Value = StringStructParser.BuildCommaSeparatedSplitValueList(outfit.ToPropertyDictionary(), @"AvailableImage", @"HighlightImage", @"DeadImage", @"SilhouetteImage")
                            };
                            Game3CoalescedHelper.AddArrayEntry(bioUiDoc, entry);
                        }
                    }

                    mergeCoal[@"BIOUI.xml"] = bioUiDoc.ToString();

                    // Dynamic load mapping
                    var bioEngineDoc = XDocument.Parse(mergeCoal[@"BIOEngine.xml"]);

                    foreach (var sqm in appearanceInfo.Values)
                    {
                        foreach (var outfit in sqm)
                        {
                            //
                            // * <Section name="sfxgame.sfxengine">
                            // <Property name="dynamicloadmapping">
                            // <Value type="3">(ObjectName="BIOG_GesturesConfigDLC.RuntimeData",SeekFreePackageName="GesturesConfigDLC")</Value>

                            var entry = new Game3CoalescedValueEntry()
                            {
                                Section = @"sfxgame.sfxengine",
                                Name = @"dynamicloadmapping",
                                Type = 3
                            };

                            entry.Values.Add($"(ObjectName=\"{outfit.AvailableImage}\",SeekFreePackageName=\"SFXHenchImages_{outfit.DLCName}\")"); // do not localize
                            entry.Values.Add($"(ObjectName=\"{outfit.HighlightImage}\",SeekFreePackageName=\"SFXHenchImages_{outfit.DLCName}\")"); // do not localize
                            Game3CoalescedHelper.AddArrayEntry(bioEngineDoc, entry);
                        }
                    }

                    mergeCoal[@"BIOEngine.xml"] = bioEngineDoc.ToString();


                    CoalescedConverter.CompileFromMemory(mergeCoal).WriteToFile(mergeCoalFile);
                }
            }
        }

        private static void buildPlotElementObject(ArrayProperty<StructProperty> plotStreaming, SquadmateInfoSingle sqm, MEGame game, bool isExplore)
        {
            var fName = sqm.HenchPackage;
            var virtualChunk = $@"BioH_{sqm.HenchName}";
            if (game.IsGame3() && isExplore)
            {
                fName += @"_Explore";
                virtualChunk += @"_Explore";
            }

            var element = plotStreaming.FirstOrDefault(x => x.GetProp<NameProperty>(@"VirtualChunkName").Value == virtualChunk);
            if (element != null)
            {
                var elem = element.GetProp<ArrayProperty<StructProperty>>(@"Elements");
                sqm.MemberAppearanceValue = elem.Count;
                elem.Add(GeneratePlotStreamingElement(fName, sqm.ConditionalIndex));
            }
        }

        /// <summary>
        /// LE2 has hardcoded squadmate images that load from the package that holds the SWF
        /// naNuke built a swf with more slots that reference the below names based on the hex
        /// representation of the ID of the sprite in the swf
        /// </summary>
        /// <param name="henchname"></param>
        /// <returns></returns>
        private static string GetTextureExportNameForSquadmateLE2(string henchname, int appearanceNumber, bool isGlow)
        {
            // Appearance indexes are 1-9
            var baseSpriteId = GetBaseSpriteIdForSquadmateImage(henchname, appearanceNumber);
            if (isGlow) baseSpriteId += 3;
            return $@"TeamSelect_I{baseSpriteId:X}";
        }

        private static int GetBaseSpriteIdForSquadmateImage(string henchname, int appearanceIndex)
        {
            // New slots use a fixed base number (increments of 500) and all start at the (appearance index * 10 + 1)
            // glow is offset by 3 and then 7 to the next non-glow for a skip of 10.
            // We add one to the base index number when calculating to account for the offsets being indexed at 1 and not at 0 (see the texture sheet)
            switch (henchname.ToLowerInvariant())
            {
                case @"vixen":
                    {
                        if (appearanceIndex == 0) return 0x4C; // Default
                        if (appearanceIndex == 1) return 0xB4; // Loyalty
                        if (appearanceIndex == 2) return 0x109; // DLC
                        return 1500 + ((GetFirstAvailableSquadmateAppearanceIndexLE2(@"vixen") + 1) * 10 + 1);
                    }
                case @"garrus":
                    {
                        if (appearanceIndex == 0) return 0x61; // Default
                        if (appearanceIndex == 1) return 0xBC; // Loyalty
                        if (appearanceIndex == 2) return 0x111; // DLC
                        return 2000 + ((GetFirstAvailableSquadmateAppearanceIndexLE2(@"garrus") + 1) * 10 + 1);
                    }
                case @"mystic":
                    {
                        if (appearanceIndex == 0) return 0x68; // Default
                        if (appearanceIndex == 1) return 0xC3; // Loyalty
                        return 2500 + ((GetFirstAvailableSquadmateAppearanceIndexLE2(@"mystic") + 1) * 10 + 1);
                    }
                case @"grunt":
                    {
                        if (appearanceIndex == 0) return 0x6F; // Default
                        if (appearanceIndex == 1) return 0xCA; // Loyalty
                        if (appearanceIndex == 2) return 0x118; // DLC
                        return 3000 + ((GetFirstAvailableSquadmateAppearanceIndexLE2(@"grunt") + 1) * 10 + 1);
                    }
                case @"leading":
                    {
                        if (appearanceIndex == 0) return 0x78; // Default
                        if (appearanceIndex == 1) return 0xD1; // Loyalty
                        return 3500 + ((GetFirstAvailableSquadmateAppearanceIndexLE2(@"leading") + 1) * 10 + 1);
                    }
                case @"tali":
                    {
                        if (appearanceIndex == 0) return 0x7F; // Default
                        if (appearanceIndex == 1) return 0xD8; // Loyalty
                        if (appearanceIndex == 2) return 0x11F; // DLC
                        return 4000 + ((GetFirstAvailableSquadmateAppearanceIndexLE2(@"tali") + 1) * 10 + 1);
                    }
                case @"convict":
                    {
                        if (appearanceIndex == 0) return 0x866; // Default
                        if (appearanceIndex == 1) return 0xDF; // Loyalty
                        if (appearanceIndex == 2) return 0x126; // DLC
                        return 4500 + ((GetFirstAvailableSquadmateAppearanceIndexLE2(@"convict") + 1) * 10 + 1);
                    }
                case @"geth":
                    {
                        if (appearanceIndex == 0) return 0x8D; // Default
                        if (appearanceIndex == 1) return 0xE6; // Loyalty
                        return 5000 + ((GetFirstAvailableSquadmateAppearanceIndexLE2(@"geth") + 1) * 10 + 1);
                    }
                case @"thief":
                    {
                        if (appearanceIndex == 0) return 0x96; // Default
                        if (appearanceIndex == 1) return 0xED; // Loyalty
                        return 5500 + ((GetFirstAvailableSquadmateAppearanceIndexLE2(@"thief") + 1) * 10 + 1);
                    }
                case @"assassin":
                    {
                        if (appearanceIndex == 0) return 0x9D; // Default
                        if (appearanceIndex == 1) return 0xF4; // Loyalty
                        if (appearanceIndex == 2) return 0x12D; // DLC
                        return 6000 + ((GetFirstAvailableSquadmateAppearanceIndexLE2(@"assassin") + 1) * 10 + 1);
                    }
                case @"professor":
                    {
                        if (appearanceIndex == 0) return 0xA6; // Default
                        if (appearanceIndex == 1) return 0xFB; // Loyalty
                        return 6500 + ((GetFirstAvailableSquadmateAppearanceIndexLE2(@"professor") + 1) * 10 + 1);
                    }
                case @"veteran":
                    {
                        if (appearanceIndex == 1) return 0xAD; // Default
                        if (appearanceIndex == 2) return 0x102; // Loyalty
                        return 7000 + ((GetFirstAvailableSquadmateAppearanceIndexLE2(@"veteran") + 1) * 10 + 1);
                    }
            }

            // The custom slot, not sure how we will implement this. I just say it's 'custom' which won't return anything in the next func but the 1 value
            return 7500 + ((GetFirstAvailableSquadmateAppearanceIndexLE2(@"custom") + 1) * 10 + 1);
        }

        private static int GetFirstAvailableSquadmateAppearanceIndexLE2(string henchname)
        {
            henchname = henchname.ToLowerInvariant();
            if (henchname == @"vixen") return 3;
            if (henchname == @"garrus") return 3;
            if (henchname == @"mystic") return 2;
            if (henchname == @"grunt") return 3;
            if (henchname == @"leading") return 2;
            if (henchname == @"tali") return 4;
            if (henchname == @"convict") return 3;
            if (henchname == @"thief") return 2;
            if (henchname == @"assassin") return 3;
            if (henchname == @"professor") return 2;
            if (henchname == @"veteran") return 2;

            return 1; // 13th slot is custom and begins at 1. 0 is done via the member info struct
        }
    }
}
