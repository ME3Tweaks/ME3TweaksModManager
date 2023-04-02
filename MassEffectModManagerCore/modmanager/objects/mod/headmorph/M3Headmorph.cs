using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3TweaksCore.Helpers;
using ME3TweaksModManager.modmanager.objects.mod.interfaces;

namespace ME3TweaksModManager.modmanager.objects.mod.headmorph
{
    /// <summary>
    /// A ModDesc-mod headmorph reference - pinned to a .me3headmorph and .ron file
    /// </summary>
    public class M3Headmorph : M3ValidateOnLoadObject
    {
        /// <summary>
        /// Blank constructor
        /// </summary>
        public M3Headmorph() { }

        /// <summary>
        /// The filename of the headmorph
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// The title of the headmorph when being shown to the user
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// The description of the headmorph when being shown to the user
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The image asset to show in the the user interface when this headmorph is to be presented to the user, as part of a preview
        /// </summary>
        public string ImageAsset { get; set; }

        #region PARAMETERS
        private const string FILENAME_PARM = @"Filename";
        private const string TITLE_PARM = @"Title";
        private const string DESCRIPTION_PARM = @"Description";
        private const string IMAGE_PARM = @"ImageAsset";
        #endregion

        /// <summary>
        /// Initializes a headmorph object from a mod and the ini struct of the headmorph
        /// </summary>
        /// <param name="mod"></param>
        /// <param name="headmorphStruct"></param>
        public M3Headmorph(Mod mod, string headmorphStruct)
        {
            var parms = StringStructParser.GetCommaSplitValues(headmorphStruct, canBeCaseInsensitive: true);
            if (!ValidateParameters(nameof(M3Headmorph), parms, FILENAME_PARM, TITLE_PARM))
                return;

            if (ValidateFileParameter(mod, nameof(M3Headmorph), parms, FILENAME_PARM, Mod.HEADMORPHS_FOLDER_NAME, required: true))
            {
                FileName = parms[FILENAME_PARM];
            }
            else
            {
                return; // Validation failed, the parameter will be set and logged already.
            }

            // Title already validated
            Title = parms[TITLE_PARM];

            Description = parms.ContainsKey(DESCRIPTION_PARM) ? parms[DESCRIPTION_PARM] : null;
            if (parms.ContainsKey(IMAGE_PARM) && ValidateImageParameter(mod, nameof(M3Headmorph), parms, IMAGE_PARM, false, false)) // Should headmorphs be installable directly from archive...?
            {
                ImageAsset = parms[IMAGE_PARM];
            }
        }

        /// <summary>
        /// Gets the list of referenced files, relative to the root of the mod.
        /// </summary>
        /// <param name="mod">The mod we are gathering relative references for</param>
        /// <returns>A list of strings that represent relative files</returns>
        public IEnumerable<string> GetRelativeReferences(Mod mod)
        {
            List<string> references = new List<string>();
            var fullPath = FilesystemInterposer.PathCombine(mod.IsInArchive, mod.ModPath, Mod.HEADMORPHS_FOLDER_NAME, FileName);
            references.Add((mod.IsInArchive && mod.ModPath.Length == 0) ? fullPath : fullPath.Substring(mod.ModPath.Length + 1)); // Add the headmorph file

            if (ImageAsset != null)
            {
                var imageAssetPath = FilesystemInterposer.PathCombine(mod.IsInArchive, mod.ModImageAssetsPath, ImageAsset);
                references.Add((mod.IsInArchive && mod.ModPath.Length == 0) ? imageAssetPath : imageAssetPath.Substring(mod.ModPath.Length + 1)); // Add the headmorph file
            }

            return references;
        }
    }
}
