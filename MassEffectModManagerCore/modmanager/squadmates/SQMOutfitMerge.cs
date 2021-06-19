using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.UnrealScript;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.modmanager.windows;
using Serilog;

namespace MassEffectModManagerCore.modmanager.squadmates
{
    public class SQMOutfitMerge
    {
        public const int STARTING_OUTFIT_CONDITIONAL = 10000;

        // todo: move to merge dlc class
        public const string MERGE_DLC_FOLDERNAME = @"DLC_MOD_M3_MERGE";

        internal class SquadmatePlot
        {
            public int ConditionalIndex { get; init; }
            public int SquadmateOutfitPlotInt { get; init; }
            public int OutfitIndex { get; init; }
        }

        private StructProperty GeneratePlotStreamingElement(string packageName, int conditionalNum)
        {
            PropertyCollection pc = new PropertyCollection();
            pc.AddOrReplaceProp(new NameProperty(packageName, @"ChunkName"));
            pc.AddOrReplaceProp(new IntProperty(conditionalNum, @"Conditional"));
            pc.AddOrReplaceProp(new BoolProperty(false, @"bFallback"));
            pc.AddOrReplaceProp(new NoneProperty());

            return new StructProperty(@"PlotStreamingElement", pc);
        }

        private int GetSquadmateOutfitInt(string squadmateName, MEGame game)
        {
            if (game.IsGame2())
            {
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

            return 0;
        }

        public void BuildBioPGlobal(GameTarget target)
        {
            var loadedFiles = MELoadedFiles.GetFilesLoadedInGame(target.Game, gameRootOverride: target.TargetPath);
            var mergeFiles = loadedFiles.Where(x =>
                x.Key.StartsWith(@"BioH_") && x.Key.Contains(@"_DLC_MOD_") && x.Key.EndsWith(@".pcc") && !x.Key.Contains(@"_LOC_") && !x.Key.Contains(@"_Explore."));

            int conditionalIndex = STARTING_OUTFIT_CONDITIONAL;
            if (mergeFiles.Any())
            {
                var biopGlobal = MEPackageHandler.OpenMEPackageFromStream(
                    Utilities.GetResourceStream($@"MassEffectModManagerCore.modmanager.squadmates.{target.Game}.BioP_Global.pcc"));
                var lsk = biopGlobal.Exports.FirstOrDefault(x => x.ClassName == @"LevelStreamingKismet");
                var persistentLevel = biopGlobal.FindExport(@"TheWorld.PersistentLevel");

                // Clone LevelStreamingKismets
                foreach (var m in mergeFiles)
                {
                    var fName = Path.GetFileNameWithoutExtension(m.Key);
                    var newLSK = EntryCloner.CloneEntry(lsk);
                    newLSK.WriteProperty(new NameProperty(fName, @"PackageName"));

                    if (target.Game.IsGame3())
                    {
                        // Game 3 has _Explore files too
                        fName += @"_Explore";
                        newLSK = EntryCloner.CloneEntry(lsk);
                        newLSK.WriteProperty(new NameProperty(fName, @"PackageName"));
                    }
                }

                // Update BioWorldInfo
                // Doesn't have consistent number so we can't find it by instanced full path
                var bioWorldInfo = biopGlobal.Exports.FirstOrDefault(x => x.ClassName == @"BioWorldInfo");

                var props = bioWorldInfo.GetProperties();

                // Update Plot Streaming
                List<SquadmatePlot> smp = new List<SquadmatePlot>();
                var plotStreaming = props.GetProp<ArrayProperty<StructProperty>>(@"PlotStreaming");
                foreach (var m in mergeFiles)
                {
                    var fName = Path.GetFileNameWithoutExtension(m.Key);

                    var henchName = fName.Substring(5);
                    henchName = henchName.Substring(0, henchName.IndexOf(@"_"));

                    // find item to add to
                    var condIndex = conditionalIndex++;
                    buildPlotElementObject(plotStreaming, smp, target.Game, fName, henchName, false, condIndex);
                    if (target.Game.IsGame3())
                    {
                        buildPlotElementObject(plotStreaming, smp, target.Game, fName, henchName, true, condIndex);
                    }
                }


                // Update StreamingLevels
                var streamingLevels = props.GetProp<ArrayProperty<ObjectProperty>>(@"StreamingLevels");
                streamingLevels.ReplaceAll(biopGlobal.Exports.Where(x => x.ClassName == @"LevelStreamingKismet").Select(x => new ObjectProperty(x)));

                bioWorldInfo.WriteProperties(props);


                // Generate M3 DLC Folder
                var sko = new StarterKitGeneratorWindow.StarterKitOptions()
                {
                    ModGame = target.Game,
                    GenerateModdesc = false,
                    OutputFolderOverride = M3Directories.GetDLCPath(target),
                    ModDescription = null,
                    ModInternalName = @"ME3Tweaks Mod Manager Merge DLC",
                    ModInternalTLKID = 1928304430,
                    ModMountFlag = target.Game.IsGame3() ? new MountFlag(EME3MountFileFlag.LoadsInSingleplayer) : new MountFlag(0, true),
                    ModDeveloper = @"ME3Tweaks Mod Manager",
                    ModMountPriority = 1900000000,
                    ModDLCFolderNameSuffix = MERGE_DLC_FOLDERNAME.Substring(@"DLC_MOD_".Length)
                };

                StarterKitGeneratorWindow.CreateStarterKitMod(sko, null);

                // Save BioP_Global into DLC
                var cookedDir = Path.Combine(M3Directories.GetDLCPath(target), MERGE_DLC_FOLDERNAME, target.Game.CookedDirName());
                var outP = Path.Combine(cookedDir, @"BioP_Global.pcc");
                biopGlobal.Save(outP);

                // Generate conditionals file
                if (target.Game.IsGame3())
                {
                    CNDFile cnd = new CNDFile();
                    cnd.ConditionalEntries = new List<CNDFile.ConditionalEntry>();
                    cnd.unk1 = 0x01; // ?

                    foreach (var c in smp)
                    {
                        var scText = $@"(plot.ints[{c.SquadmateOutfitPlotInt}] == i{c.OutfitIndex})";
                        var compiled = ME3ConditionalsCompiler.Compile(scText);
                        cnd.ConditionalEntries.Add(new CNDFile.ConditionalEntry() { Data = compiled, ID = c.ConditionalIndex });
                    }

                    cnd.ToFile(Path.Combine(cookedDir, $@"Conditionals_{MERGE_DLC_FOLDERNAME}.cnd"));
                }
                else if (target.Game.IsGame2())
                {
                    var startupF = Path.Combine(cookedDir, $@"Startup_{MERGE_DLC_FOLDERNAME}.pcc");
                    var startup = MEPackageHandler.OpenMEPackageFromStream(Utilities.GetResourceStream($@"MassEffectModManagerCore.modmanager.mergedlc.{target.Game}.Startup_{MERGE_DLC_FOLDERNAME}.pcc"));
                    var conditionalClass =
                        startup.FindExport($@"PlotManager{MERGE_DLC_FOLDERNAME}.BioAutoConditionals");

                    // Add Conditional Functions
                    FileLib fl = new FileLib(startup);
                    bool initialized = fl.Initialize(new PackageCache()).Result;
                    if (!initialized)
                    {
                        throw new Exception(
                            $@"FileLib for script update could not initialize, cannot install conditionals");
                    }


                    var funcToClone = startup.FindExport($@"PlotManager{MERGE_DLC_FOLDERNAME}.BioAutoConditionals.TemplateFunction");
                    foreach (var c in smp)
                    {
                        var func = EntryCloner.CloneEntry(funcToClone);
                        func.ObjectName = $@"F{c.ConditionalIndex}";
                        func.indexValue = 0;

                        var scText = new StreamReader(Utilities.GetResourceStream(
                            $@"MassEffectModManagerCore.modmanager.squadmates.{target.Game}.HasOutfitOnConditional.txt")).ReadToEnd();

                        scText = scText.Replace(@"%CONDITIONALNUM%", c.ConditionalIndex.ToString());
                        scText = scText.Replace(@"%SQUADMATEOUTFITPLOTINT%", c.SquadmateOutfitPlotInt.ToString());
                        scText = scText.Replace(@"%OUTFITINDEX%", c.OutfitIndex.ToString());

                        (_, MessageLog log) = UnrealScriptCompiler.CompileFunction(func, scText, fl);
                        if (log.AllErrors.Any())
                        {
                            Log.Error($@"Error compiling function {func.InstancedFullPath}:");
                            foreach (var l in log.AllErrors)
                            {
                                Log.Error(l.Message);
                            }

                            // Is this right? [0]?
                            throw new Exception($"Error compiling function {func}: {log.AllErrors[0].Message}");
                        }
                    }


                    // Relink the conditionals chain
                    UClass uc = ObjectBinary.From<UClass>(conditionalClass);
                    uc.UpdateLocalFunctions();
                    uc.UpdateChildrenChain();
                    conditionalClass.WriteBinary(uc);

                    startup.Save(startupF);

                    // Add startup package
                    if (target.Game.IsGame2())
                    {
                        var bioEngine = Path.Combine(cookedDir, @"BIOEngine.ini");
                        var ini = DuplicatingIni.LoadIni(bioEngine);

                        var startupSection = ini.GetOrAddSection(@"Engine.StartupPackages");

                        startupSection.Entries.Add(new DuplicatingIni.IniEntry(@"+DLCStartupPackage",
                            $@"Startup_{MERGE_DLC_FOLDERNAME}"));
                        startupSection.Entries.Add(new DuplicatingIni.IniEntry(@"+Package",
                            $@"PlotManager{MERGE_DLC_FOLDERNAME}"));

                        ini.WriteToFile(bioEngine);
                    }
                    else if (target.Game.IsGame3())
                    {
                        // We don't need startup package for game 3. But leaving this here for later
                        //var coal = Path.Combine(cookedDir, $@"Default_{MERGE_DLC_FOLDERNAME}.bin");
                        //using var fs = File.OpenRead(coal);
                        //var coalFiles = CoalescedConverter.DecompileGame3ToMemory(fs);
                        //fs.Close();

                        //var bioEngine = XDocument.Parse(coalFiles[@"BioEngine.xml"]);
                        ///*
                        // Section name="engine.startuppackages">
                        //  <Property name="dlcstartuppackage" type="3">Startup_HEN_PR</Property>
                        //  <Property name="dlcstartuppackagename" type="0">Startup_HEN_PR</Property>
                        //  <Property name="package" type="3">PlotManagerAutoDLC_HEN_PR</Property>
                        //</Section>
                        // */
                        //var targetSection = bioEngine.XPathSelectElement($@"/CoalesceAsset/Sections/Section[@name='engine.startuppackages']");
                        //if (targetSection == null)
                        //{
                        //    targetSection = new XElement(@"Section");
                        //    targetSection.SetAttributeValue(@"name", @"engine.startuppackages");
                        //}
                        //targetSection.Add(new XElement(@"Property", ));

                        //coalFiles[@"BioEngine.xml"] = bioEngine.ToString();
                        //CoalescedConverter.CompileFromMemory(coalFiles).WriteToFile(coal);
                    }
                }
            }
        }

        private void buildPlotElementObject(ArrayProperty<StructProperty> plotStreaming, List<SquadmatePlot> smp, MEGame game, string fName, string henchName, bool isExplore, int conditionalIndex)
        {
            var virtualChunk = $@"BioH_{henchName}";
            if (game.IsGame3() && isExplore)
            {
                fName += @"_Explore";
                virtualChunk += @"_Explore";
            }

            var element = plotStreaming.FirstOrDefault(x =>
                x.GetProp<NameProperty>(@"VirtualChunkName").Value == virtualChunk);
            if (element != null)
            {
                SquadmatePlot sqp = new SquadmatePlot()
                {
                    ConditionalIndex = conditionalIndex,
                    SquadmateOutfitPlotInt = GetSquadmateOutfitInt(henchName, game),
                    OutfitIndex = 5 // TODO: HOW DO FIGURE THIS OUT?
                };
                smp.Add(sqp);
                element.GetProp<ArrayProperty<StructProperty>>(@"Elements").Add(GeneratePlotStreamingElement(fName, sqp.ConditionalIndex));
            }
        }
    }
}
