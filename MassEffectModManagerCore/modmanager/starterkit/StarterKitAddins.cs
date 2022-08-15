using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Coalesced.Xml;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            package.AddExport(referencer);
            return referencer;
        }

        #endregion
    }
}
