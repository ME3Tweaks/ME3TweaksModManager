using System.Windows;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using ME3TweaksCore.ME3Tweaks.ModManager;
using ME3TweaksCore.Objects;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.mod.merge.v1;

namespace ME3TweaksModManager.modmanager.objects.mod.merge
{
    public class MergeModLoader
    {
        private const string MERGEMOD_MAGIC = @"M3MM";
        public static IMergeMod LoadMergeMod(Stream mergeFileStream, string filename, bool loadAssets, bool validate = true)
        {
            if (mergeFileStream.ReadStringASCII(4) != MERGEMOD_MAGIC)
            {
                throw new Exception(M3L.GetString(M3L.string_mergeModFileDoesNotHaveCorrectMagicHeader));
            }

            var version = mergeFileStream.ReadByte();

            switch (version)
            {
                case 1:
                    return MergeMod1.ReadMergeMod(mergeFileStream, filename, loadAssets, validate: validate);
                case 2:
                    // Supports moddesc version and compressed strings
                    return MergeMod1.ReadMergeMod(mergeFileStream, filename, loadAssets, mergeModVersion: 2, validate: validate);
                default:
                    return null;
            }
        }

        /// <summary>
        /// Serializes manifest file into a m3m file
        /// </summary>
        /// <param name="inputfile"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static string SerializeManifest(string inputfile, int version)
        {
            var outfile = Path.Combine(Directory.GetParent(inputfile).FullName, Path.GetFileNameWithoutExtension(inputfile) + @".m3m");
            M3Log.Information($@"M3MCompiler: Serializing {inputfile} to {outfile}");
            using MemoryStream fs = new MemoryStream();
            fs.WriteStringASCII(MERGEMOD_MAGIC); // Do not change
            fs.WriteByte((byte)version);
            IList<string> messages = null;
            switch (version)
            {
                case 1:
                    messages = MergeMod1.Serialize(fs, inputfile); // This was always hardcoded to 1 initially.
                    break;
                case 2:
                    messages = MergeMod1.Serialize(fs, inputfile, version);
                    break;
                default:
                    throw new Exception(M3L.GetString(M3L.string_interp_unsupportedMergeModVersionVersionX, version));
            }

            if (messages != null)
            {
                // Will be caught higher up
                throw new Exception(M3L.GetString(M3L.string_interp_invalidMergeModManifestReason, string.Join('\n', messages)));
            }

            M3Log.Information($@"M3MCompiler: Writing final result to {outfile}");
            fs.WriteToFile(outfile);
            return outfile;
        }

        /// <summary>
        /// Decompiles an m3m file into its components.
        /// </summary>
        /// <param name="file">Filepath to decompile</param>
        public static void DecompileM3M(string file, string outDir = null)
        {
            using var fs = File.OpenRead(file);
            var mm = LoadMergeMod(fs, file, true, validate: false); // Decompile does not depend on version number
            if (mm != null)
            {
                mm.ExtractToFolder(outDir ?? Directory.GetParent(file).FullName);
            }
            else
            {
                M3Log.Error($@"Error decompiling {file}: loader returned null. The version is likely unsupported by this version of mod manager");
                M3L.ShowDialog(MainWindow.Instance, M3L.GetString(M3L.string_dialog_unsupportedMergemodVersionMayBeTooNew), M3L.GetString(M3L.string_error), MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }

        /// <summary>
        /// Gets the list of allowed files that a mergemod can target for a game.
        /// </summary>
        /// <returns>List of filenames (with extensions). Startup may have _INT on the end!</returns>
        public static List<string> GetAllowedMergeTargetFilenames(MEGame game, bool withAllLocalizationVariations = false)
        {
            var safeFiles = EntryImporter.FilesSafeToImportFrom(game).ToList();
            if (game == MEGame.ME1)
            {
                safeFiles.Add(@"EntryMenu.SFM"); // ME1
            }
            else
            {
                safeFiles.Add(@"EntryMenu.pcc"); // ME2+
            }

            // Game3 doesn't use localized startup files at all
            if (!game.IsGame3() && withAllLocalizationVariations)
            {
                var locList = new List<string>();
                foreach (var f in safeFiles)
                {
                    var baseFile = f.StripUnrealLocalization();
                    locList.Add(baseFile);
                    foreach (var lang in GameLanguage.GetLanguagesForGame(game))
                    {
                        locList.Add(baseFile.SetUnrealLocalization(game, lang.Localization));
                    }
                }

                return locList;
            }

            return safeFiles;
        }

        /// <summary>
        /// Attempts to get the merge mod version to compile with. If one cannot be ascertained, the user will be prompted
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static int? GetMergeModVersionForCompile(Window window, string file, double featureLevel = 0)
        {
            // Pre-provided value
            if (featureLevel >= ModDescConsts.MODDESC_VERSION_9_0)
                return 2;
            if (featureLevel > 0)
                return 1;

            // Detect

            // Containing folder
            var parentPath = Directory.GetParent(file);
            if (parentPath == null)
                return AskUserForVersion(window);

            // Parent folder (ModPath in most cases)
            parentPath = parentPath.Parent;
            if (parentPath == null)
                return AskUserForVersion(window);

            var mdPath = Path.Combine(parentPath.FullName, @"moddesc.ini");
            if (File.Exists(mdPath))
            {
                DuplicatingIni ini = DuplicatingIni.LoadIni(mdPath);
                var moddescVerEntry = ini[Mod.MODDESC_HEADERKEY_MODMANAGER][Mod.MODDESC_DESCRIPTOR_MODMANAGER_CMMVER];
                if (moddescVerEntry.HasValue && double.TryParse(moddescVerEntry.Value, out var mdVer))
                {
                    return mdVer >= ModDescConsts.MODDESC_VERSION_9_0 ? 2 : 1;
                }
            }

            return AskUserForVersion(window);
        }

        private static int? AskUserForVersion(Window window)
        {
            var res = M3L.ShowDialog(window, M3L.GetString(M3L.string_dialog_mergeModFeatureLevel1), M3L.GetString(M3L.string_useEnhancedFeaturesQuestion), MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.No);
            if (res == MessageBoxResult.Cancel)
                return null;
            return res == MessageBoxResult.No ? 2 : 1;
        }

        public static double GetMinimumCmmVerRequirement(int mmMergeModVersion)
        {
            if (mmMergeModVersion == 1)
                return ModDescConsts.MODDESC_VERSION_7_0;
            if (mmMergeModVersion == 2)
                return ModDescConsts.MODDESC_VERSION_9_0;

            return 100;
        }
    }
}
