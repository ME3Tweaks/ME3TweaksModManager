using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Helpers;
using ME3TweaksModManager.modmanager.objects.mod.editor;
using ME3TweaksModManager.modmanager.objects.mod.interfaces;
using ME3TweaksModManager.modmanager.usercontrols.interfaces;
using Newtonsoft.Json;

namespace ME3TweaksModManager.modmanager.objects.mod.headmorph
{
    /// <summary>
    /// A ModDesc-mod headmorph reference - pinned to a .me3headmorph and .ron file
    /// </summary>
    public class M3Headmorph : M3ValidateOnLoadObject, IM3ImageEnabled
    {
        /// <summary>
        /// Blank constructor
        /// </summary>
        public M3Headmorph()
        {
        }

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
        public string ImageAssetName { get; set; }

        /// <summary>
        /// The height of the image to display when shown in a tooltip
        /// </summary>
        [JsonIgnore]
        public int ImageHeight { get; set; }

        #region PARAMETERS

        private const string FILENAME_PARM = @"Filename";
        private const string TITLE_PARM = @"Title";
        private const string DESCRIPTION_PARM = @"Description";
        private const string IMAGE_PARM = @"ImageAsset";
        private const string IMAGE_HEIGHT_PARM = @"ImageHeight";

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

            if (ValidateFileParameter(mod, nameof(M3Headmorph), parms, FILENAME_PARM, Mod.HEADMORPHS_FOLDER_NAME,
                    required: true))
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
            if (parms.ContainsKey(IMAGE_PARM) && ValidateImageParameter(mod, nameof(M3Headmorph), parms, IMAGE_PARM, false,
                    false,
                    additionalRequiredParam:
                    IMAGE_HEIGHT_PARM)) // Should headmorphs be installable directly from archive...?
            {
                ImageAssetName = parms[IMAGE_PARM];
                if (int.TryParse(parms[IMAGE_HEIGHT_PARM], out var imageHeight))
                {
                    if (imageHeight < 1)
                    {
                        M3Log.Error($@"{IMAGE_HEIGHT_PARM} value must be an integer greater than 0.");
                        ValidationFailedReason = $"{IMAGE_HEIGHT_PARM} value must be an integer greater than 0.";
                        return;
                    }

                    ImageHeight = imageHeight;
                }
            }

            ModdescMod = mod;
        }

        /// <summary>
        /// Gets the list of referenced files, relative to the root of the mod.
        /// </summary>
        /// <param name="mod">The mod we are gathering relative references for</param>
        /// <returns>A list of strings that represent relative files</returns>
        public IEnumerable<string> GetRelativeReferences(Mod mod)
        {
            List<string> references = new List<string>();
            var fullPath =
                FilesystemInterposer.PathCombine(mod.IsInArchive, mod.ModPath, Mod.HEADMORPHS_FOLDER_NAME, FileName);
            references.Add((mod.IsInArchive && mod.ModPath.Length == 0)
                ? fullPath
                : fullPath.Substring(mod.ModPath.Length + 1)); // Add the headmorph file

            if (ImageAssetName != null)
            {
                var imageAssetPath =
                    FilesystemInterposer.PathCombine(mod.IsInArchive, mod.ModImageAssetsPath, ImageAssetName);
                references.Add((mod.IsInArchive && mod.ModPath.Length == 0)
                    ? imageAssetPath
                    : imageAssetPath.Substring(mod.ModPath.Length + 1)); // Add the headmorph file
            }

            return references;
        }

        /// <summary>
        /// Parameter map, used for the moddesc.ini editor Contains a list of values in the alternate mapped to their string value
        /// </summary>
        [JsonIgnore]
        public ObservableCollectionExtended<MDParameter> ParameterMap { get; } =
            new ObservableCollectionExtended<MDParameter>();

        /// <summary>
        /// List of all keys in the M3MEMMod struct that are publicly available for editing
        /// </summary>
        /// <param name="mod"></param>
        public void BuildParameterMap(Mod mod)
        {
            var parameterDictionary = new Dictionary<string, object>()
            {
                // List of available parameters for this object
                { FILENAME_PARM, FileName },
                { TITLE_PARM, Title },
                { DESCRIPTION_PARM, Description },
                {
                    IMAGE_PARM,
                    new MDParameter(@"string", IMAGE_PARM, ImageAssetName, new[] { @"" }, "")
                        { AllowedValuesPopulationFunc = mod.PopulateImageOptions }
                }, // Uses image population function
                { IMAGE_HEIGHT_PARM, ImageHeight },
            };

            ParameterMap.ReplaceAll(MDParameter.MapIntoParameterMap(parameterDictionary));
        }

        // IM3ImageEnabled interface
        public Mod ModdescMod { get; set; }
        public BitmapSource ImageBitmap { get; set; }
    }
}