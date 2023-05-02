using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3TweaksModManager.modmanager.objects.mod
{
    public partial class Mod
    {
        // Headmorph related things go in this file


        /// <summary>
        /// Headmorphs folder. Do not change
        /// </summary>
        public const string HEADMORPHS_FOLDER_NAME = @"Headmorphs"; // DO NOT CHANGE THIS VALUE

        /// <summary>
        /// Gets the full path to the headmorphs asset folder
        /// </summary>
        public string HeadmorphAssetsPath => FilesystemInterposer.PathCombine(Archive != null, ModPath, HEADMORPHS_FOLDER_NAME);

        /// <summary>
        /// Gets a list of available headmorph files in the Headmorphs folder. This method does NOT work if this mod is loaded from an archive
        /// </summary>
        /// <returns></returns>
        internal List<string> PopulateHeadmorphFileOptions()
        {
            if (!IsInArchive)
            {
                if (FilesystemInterposer.DirectoryExists(HeadmorphAssetsPath))
                {
                    return FilesystemInterposer.DirectoryGetFiles(HeadmorphAssetsPath).Where(x => IsAllowedHeadmorphFileType(Game, x))
                        .Select(x => x.Substring(HeadmorphAssetsPath.Length + 1)).Prepend(@"").ToList();
                }
            }

            return new List<string>();
        }

        private static bool IsAllowedHeadmorphFileType(MEGame game, string filename)
        {
            var extension = Path.GetExtension(filename);
            switch (extension)
            {
                case @".me2headmorph" when game.IsGame2():
                    return true;
                case @".me3headmorph" when game.IsGame3():
                    return true;
                case @".ron":
                    return true;
                default:
                    return false;
            }
        }
    }
}
