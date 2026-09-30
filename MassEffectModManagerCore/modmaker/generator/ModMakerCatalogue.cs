using System;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Targets;

namespace ME3TweaksModManager.modmaker.generator
{
    /// <summary>
    /// Constants and rules shared by every part of the ModMaker catalogue generator.
    /// </summary>
    public static class ModMakerCatalogue
    {
        /// <summary>
        /// The DLC folder name the ModMaker compiler always emits.
        ///
        /// Fixed on purpose: a ModMaker mod is a single DLC, and building a second one replaces the
        /// first rather than stacking with it. The UI has to say that, because the alternative is a
        /// user discovering it by losing work.
        /// </summary>
        public const string CompilerDLCFolderName = @"DLC_MOD_ModMaker";

        /// <summary>
        /// String ref carrying the generated DLC's display name.
        ///
        /// Fixed for the same reason the folder name is: there is only ever one ModMaker DLC, so
        /// there is nothing to allocate per mod. The value itself is arbitrary - any positive id
        /// works - but it is the kind of thing that wants registering with ME3Tweaks so that no other
        /// mod picks it.
        /// </summary>
        public const int CompilerTlkId = 1928400;

        /// <summary>
        /// True if this DLC folder is ModMaker's own output and must be excluded from every scan.
        ///
        /// This is the single most important rule in the generator, and it fails silently if it is
        /// missed. The generator reads the installed game to learn what the defaults ARE. If it also
        /// reads back the DLC ModMaker itself built, then after one build-and-install cycle the
        /// "shipped default" for every edited value is the user's own edit: the diff against shipped
        /// shows no change, reverting a value reverts to the modded value, and the catalogue has
        /// quietly stopped describing the game. Nothing throws.
        ///
        /// Every other installed DLC - official, Milan, someone else's mod - is deliberately IN
        /// scope. Those are the environment being edited against.
        /// </summary>
        /// <param name="dlcFolderNameOrPath">A DLC folder name or a full path to one.</param>
        public static bool IsCompilerOutput(string dlcFolderNameOrPath)
        {
            if (string.IsNullOrWhiteSpace(dlcFolderNameOrPath)) return false;
            var name = System.IO.Path.GetFileName(dlcFolderNameOrPath.TrimEnd('\\', '/'));
            return name.Equals(CompilerDLCFolderName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// True if this file lies inside ModMaker's own DLC folder. Used when filtering package scans,
        /// where what we hold is a file path rather than a DLC folder.
        /// </summary>
        public static bool IsInCompilerOutput(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;
            // Match the folder as a whole path segment so a mod named e.g. DLC_MOD_ModMakerExtras
            // is not caught by a naive Contains().
            var needle = System.IO.Path.DirectorySeparatorChar + CompilerDLCFolderName + System.IO.Path.DirectorySeparatorChar;
            var normalized = filePath.Replace('/', System.IO.Path.DirectorySeparatorChar);
            return normalized.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Whether ModMaker can build a catalogue for this game at all. Milan is a multiplayer game
        /// with its own <see cref="MEGame.LMP"/> identity; LE3 is supported for contrast during
        /// development.
        /// </summary>
        public static bool SupportsGame(MEGame game) => game is MEGame.LMP or MEGame.LE3;

        /// <summary>
        /// The DLC folders the generator will read, lowest mount priority first, with ModMaker's own
        /// output removed. Mount order matters: it is the order the game resolves config in, so it is
        /// the order the merge has to happen in.
        /// </summary>
        public static List<string> ScannableDLCFoldersInMountOrder(GameTarget target)
        {
            var names = MELoadedDLC.GetDLCNamesInMountOrder(target.Game, target.TargetPath);
            names.RemoveAll(IsCompilerOutput);
            return names;
        }
    }
}
