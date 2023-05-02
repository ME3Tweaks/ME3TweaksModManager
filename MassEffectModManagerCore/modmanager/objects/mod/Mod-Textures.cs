using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3TweaksModManager.modmanager.objects.mod
{
    public partial class Mod
    {
        /// <summary>
        /// Textures folder. Do not change
        /// </summary>
        public const string TEXTUREMOD_FOLDER_NAME = @"Textures";  // DO NOT CHANGE THIS VALUE

        /// <summary>
        /// Gets the full path to the headmorphs asset folder
        /// </summary>
        public string TextureAssetsPath => FilesystemInterposer.PathCombine(Archive != null, ModPath, TEXTUREMOD_FOLDER_NAME);

        /// <summary>
        /// Gets a list of available .mem Textures folder. This method does NOT work if this mod is loaded from an archive
        /// </summary>
        /// <returns></returns>
        internal List<string> PopulateTextureFileOptions()
        {
            if (!IsInArchive)
            {
                if (FilesystemInterposer.DirectoryExists(TextureAssetsPath))
                {
                    return FilesystemInterposer.DirectoryGetFiles(TextureAssetsPath).Where(IsAllowedTextureModFileType)
                        .Select(x => x.Substring(TextureAssetsPath.Length + 1)).Prepend(@"").ToList();
                }
            }

            return new List<string>();
        }

        private static bool IsAllowedTextureModFileType(string filename)
        {
            var extension = Path.GetExtension(filename);
            switch (extension)
            {
                case @".mem":
                    return true;
                default:
                    return false;
            }
        }
    }
}
