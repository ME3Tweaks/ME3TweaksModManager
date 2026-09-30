using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Coalesced.Config;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.ME3Tweaks.StarterKit;
using ME3TweaksModManager.modmanager;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmaker.generator;

namespace ME3TweaksModManager.modmaker.compiler
{
    /// <summary>What an emit produced, or the reasons it produced nothing.</summary>
    public class EmitResult
    {
        /// <summary>The mod folder, holding moddesc.ini and the DLC folder. Null if the emit failed.</summary>
        public string ModDirectory { get; init; }

        public string CoalescedPath { get; init; }

        public List<CompileProblem> Problems { get; } = new();

        public bool Succeeded => ModDirectory != null;

        /// <summary>Config property writes committed into the DLC's coalesced.</summary>
        public int PropertiesWritten { get; internal set; }
    }

    /// <summary>
    /// Turns a build manifest into a mod on disk: the emission half of the compiler.
    ///
    /// It reads the manifest and nothing else - not the project, not the definition bundle, and not
    /// the installed game. Every decision that needed those was made by <see cref="ProjectCompiler"/>
    /// and is already written down as concrete text and numbers, which is what makes a build
    /// reproducible from the manifest alone.
    ///
    /// The DLC folder itself is built by ME3TweaksCore's StarterKit rather than by hand. A custom DLC
    /// is not just a folder with a coalesced in it - it needs a mount file with the right game byte
    /// (LMP writes 0x8, not LE3's), per-language TLKs carrying the DLC's name, and a coalesced whose
    /// BioEngine section declares the DLC's seek-free paths. StarterKit is the maintained
    /// implementation of all of that, and reimplementing it here would be a second copy to keep in
    /// step with the game.
    /// </summary>
    public static class ModEmitter
    {
        /// <summary>
        /// Writes the mod.
        /// </summary>
        /// <param name="manifest">A manifest from <see cref="ProjectCompiler"/>.</param>
        /// <param name="outputFolderOverride">
        /// Where to put the mod folder. Null puts it in the user's mod library under the mod's name,
        /// which is what a real build does; tests pass a temp folder.
        /// </param>
        /// <param name="progress">Optional UI text callback, passed through to StarterKit.</param>
        public static EmitResult Emit(BuildManifest manifest, string outputFolderOverride = null,
                                      Action<string> progress = null)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));

            var problems = new List<CompileProblem>();
            void Error(string where, string message) =>
                problems.Add(new CompileProblem(CompileProblemSeverity.Error, where, message));

            if (manifest.Format > BuildManifest.CurrentFormat)
                Error(@"format", $@"this manifest is format {manifest.Format}; this build understands {BuildManifest.CurrentFormat}");

            if (manifest.Kind != BuildManifest.KindString)
                Error(@"kind", @"this is not a ModMaker build manifest");

            if (!Enum.TryParse<MEGame>(manifest.Game, ignoreCase: true, out var game) || !ModMakerCatalogue.SupportsGame(game))
                Error(@"game", $@"'{manifest.Game}' is not a game ModMaker can build for");

            var dlcFolder = manifest.Mod?.DlcFolder;
            if (string.IsNullOrWhiteSpace(dlcFolder) || !dlcFolder.StartsWith(DlcPrefix, StringComparison.OrdinalIgnoreCase))
                Error(@"mod.dlcFolder", $@"'{dlcFolder}' is not a DLC folder name this can generate");

            if (string.IsNullOrWhiteSpace(manifest.Mod?.Name))
                Error(@"mod.name", @"a mod needs a name; it is the folder it is written into");

            if (problems.Count > 0)
            {
                var refused = new EmitResult();
                refused.Problems.AddRange(problems);
                return refused;
            }

            var options = new StarterKitOptions
            {
                ModGame = game,
                ModName = manifest.Mod.Name,
                ModInternalName = manifest.Mod.Name,
                ModDeveloper = manifest.Mod.Author ?? string.Empty,
                ModDescription = manifest.Mod.Description ?? string.Empty,
                ModURL = string.Empty,
                // StarterKit adds the DLC_MOD_ prefix itself, so the manifest's folder name is split
                // rather than a second literal being written next to it.
                ModDLCFolderNameSuffix = dlcFolder[DlcPrefix.Length..],
                ModMountPriority = manifest.Mod.Mount?.Priority ?? InstallMount.DefaultPriority,
                ModInternalTLKID = manifest.Mod.Mount?.TlkId ?? ModMakerCatalogue.CompilerTlkId,
                ModMountFlag = new MountFlag(manifest.Mod.Mount?.Flags ?? 0, isME2: false),
                OutputFolderOverride = outputFolderOverride,
                GenerateModdesc = false,
            };

            M3Log.Information($@"ModMaker: generating {dlcFolder} via StarterKit");

            // Only resolved when the caller did not name a folder: it reads the app's mod library
            // setting, which a caller supplying its own output folder has no reason to need.
            var library = outputFolderOverride == null ? ModLibraryFor(game) : null;
            var modDirectory = DLCModGenerator.CreateStarterKitMod(
                library, options, progress, out _, ModSourcing.GetGamePatchModFolder);

            var cookedDirectory = Path.Combine(modDirectory, dlcFolder, game.CookedDirName());
            var result = new EmitResult
            {
                ModDirectory = modDirectory,
                CoalescedPath = Path.Combine(cookedDirectory, $@"Default_{dlcFolder}.bin"),
            };

            WriteModdesc(manifest, modDirectory, game, dlcFolder);
            WriteConfig(manifest, game, cookedDirectory, dlcFolder, result);

            result.Problems.AddRange(problems);
            return result;
        }

        private const string DlcPrefix = @"DLC_MOD_";

        /// <summary>
        /// The moddesc, written here rather than taken from StarterKit's own.
        ///
        /// StarterKit writes a fixed `modver` of 1.0, which is wrong for something rebuilt: a mod that
        /// never changes version does not look newer than the copy already installed. The rest of the
        /// descriptors follow StarterKitGeneratorWindow exactly.
        /// </summary>
        private static void WriteModdesc(BuildManifest manifest, string modDirectory, MEGame game, string dlcFolder)
        {
            var ini = new DuplicatingIni();
            ini[Mod.MODDESC_HEADERKEY_MODMANAGER][Mod.MODDESC_DESCRIPTOR_MODMANAGER_CMMVER].Value =
                App.HighestSupportedModDesc.ToString(CultureInfo.InvariantCulture); // prevent commas
            ini[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_GAME].Value = game.ToString();
            ini[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_NAME].Value = manifest.Mod.Name;
            ini[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_DEVELOPER].Value = manifest.Mod.Author ?? string.Empty;
            ini[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_DESCRIPTION].Value =
                M3Utilities.ConvertNewlineToBr(manifest.Mod.Description ?? string.Empty);
            ini[Mod.MODDESC_HEADERKEY_MODINFO][Mod.MODDESC_DESCRIPTOR_MODINFO_VERSION].Value = manifest.Mod.Version ?? @"1";

            ini[Mod.MODDESC_HEADERKEY_CUSTOMDLC][Mod.MODDESC_DESCRIPTOR_CUSTOMDLC_SOURCEDIRS].Value = dlcFolder;
            ini[Mod.MODDESC_HEADERKEY_CUSTOMDLC][Mod.MODDESC_DESCRIPTOR_CUSTOMDLC_DESTDIRS].Value = dlcFolder;

            ini.WriteToFile(Path.Combine(modDirectory, @"moddesc.ini"), new UTF8Encoding(false));
        }

        /// <summary>
        /// Merges the manifest's config writes into the DLC's coalesced.
        ///
        /// Into, not over: StarterKit's coalesced already carries the BioEngine section that declares
        /// the DLC's seek-free paths, and a DLC without that does not load its own content. So the
        /// generated file is opened and added to.
        /// </summary>
        private static void WriteConfig(BuildManifest manifest, MEGame game, string cookedDirectory,
                                        string dlcFolder, EmitResult result)
        {
            var job = manifest.Jobs.OfType<CoalescedJob>().FirstOrDefault();
            if (job == null || job.Files.Count == 0) return;

            var bundle = ConfigAssetBundle.FromDLCFolder(game, cookedDirectory, dlcFolder);
            if (bundle == null)
            {
                result.Problems.Add(new CompileProblem(CompileProblemSeverity.Error, @"coalesced",
                    @"the generated DLC's coalesced could not be opened"));
                return;
            }

            foreach (var (assetName, file) in job.Files)
            {
                var asset = bundle.GetAsset(assetName); // created, with the Source the game 3 compiler needs
                foreach (var section in file.Sections)
                {
                    var target = asset.GetOrAddSection(section.Name);
                    foreach (var write in section.Properties)
                    {
                        Apply(target, write);
                        result.PropertiesWritten++;
                    }
                }
            }

            bundle.CommitDLCAssets(cookedDirectory, dlcFolder);
        }

        /// <summary>
        /// One property write, as coalesced values.
        ///
        /// A replacement is TWO values in one property, and the order is load-bearing: the Remove has
        /// to be applied before the Add or the game holds both entries. They are appended to the same
        /// property in that order, which is the order ConfigMerge walks them in.
        /// </summary>
        private static void Apply(CoalesceSection section, PropertyWrite write)
        {
            if (!section.TryGetValue(write.Name, out var property))
            {
                property = new CoalesceProperty(write.Name);
                section.Add(write.Name, property);
            }

            if (write.Replace != null)
            {
                property.Add(new CoalesceValue(write.Replace.Remove, CoalesceParseAction.Remove));
                property.Add(new CoalesceValue(write.Replace.Add, CoalesceParseAction.AddUnique));
                return;
            }

            property.Add(new CoalesceValue(write.Value, write.ValueType ?? 0));
        }

        /// <summary>Where a real build writes to. Overridden in tests, which have no mod library.</summary>
        private static string ModLibraryFor(MEGame game) =>
            modmanager.loaders.M3LoadedMods.GetModDirectoryForGame(game);
    }
}
