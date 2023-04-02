using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Helpers;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.objects.mod.editor;
using ME3TweaksModManager.modmanager.objects.mod.headmorph;
using ME3TweaksModManager.modmanager.objects.mod.interfaces;
using ME3TweaksModManager.modmanager.objects.mod.texture;
using Newtonsoft.Json;
using WinCopies.Util;
using static ME3TweaksModManager.modmanager.objects.alternates.AlternateDLC;
using static Xceed.Wpf.Toolkit.Calculator;

namespace ME3TweaksModManager.modmanager.objects
{
    /// <summary>
    /// Contains M3-specific data about a texture mod, along with the MEMMod object.
    /// </summary>
    public class M3MEMMod : MEMMod
    {

        /// <summary>
        /// The associated moddesc mod, if any. If this is null, then the filepath is relative to the root of the mod library.
        /// </summary>
        [JsonIgnore]
        public Mod ModdescMod { get; set; }


        #region PARAMETERS
        /// <summary>
        /// The !!filename!! of the texture package. This is NOT the filepath!
        /// </summary>
        public string RelativeFileName { get; set; }

        /// <summary>
        /// The title of the texture package when being shown to the user
        /// </summary>
        [JsonProperty(@"title")]
        public string Title { get; set; }

        [JsonIgnore]
        public string FileSource => ModdescMod?.ModName ?? "Texture library";

        /// <summary>
        /// The description of the texture package when being shown to the user
        /// </summary>
        [JsonIgnore]
        public string Description { get; set; }

        /// <summary>
        /// The image asset to show in the the user interface when this texture mod is presented to the user, perhaps as an option.
        /// </summary>
        [JsonProperty(@"imageasset")]
        public string ImageAsset { get; set; }

        private const string FILENAME_PARM = @"Filename";
        private const string TITLE_PARM = @"Title";
        private const string DESCRIPTION_PARM = @"Description";
        private const string IMAGE_PARM = @"ImageAsset";
        #endregion

        /// <summary>
        /// Gets the full path to the MEM file.
        /// </summary>
        /// <returns></returns>
        public override string GetFilePathToMEM()
        {
            if (ModdescMod != null) return FilesystemInterposer.PathCombine(ModdescMod.IsInArchive, ModdescMod.ModPath, RelativeFileName);
            return Path.Combine(M3LoadedMods.GetCurrentModLibraryDirectory(), FilePath); // Mod library
        }

        /// <summary>
        /// Gets the relative path to the .mem file
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public string GetRelativePathToMEM()
        {
            if (ModdescMod == null)
                throw new Exception("Cannot get relative path to a M3MEMMod object that is not part of a moddesc mod!");

            return Mod.TEXTUREMOD_FOLDER_NAME + Path.DirectorySeparatorChar + RelativeFileName;
        }


        public M3MEMMod(Mod mod, string memModIniStruct)
        {
            var parms = StringStructParser.GetCommaSplitValues(memModIniStruct, canBeCaseInsensitive: true);
            if (!ValidateParameters(nameof(M3MEMMod), parms, FILENAME_PARM, TITLE_PARM))
                return;

            if (ValidateFileParameter(mod, nameof(M3MEMMod), parms, FILENAME_PARM, Mod.TEXTUREMOD_FOLDER_NAME, required: true))
            {
                RelativeFileName = parms[FILENAME_PARM];
            }
            else
            {
                return; // Validation failed, the parameter will be set and logged already.
            }
            // Title already validated
            Title = parms[TITLE_PARM];

            Description = parms.ContainsKey(DESCRIPTION_PARM) ? parms[DESCRIPTION_PARM] : null;
            if (parms.ContainsKey(IMAGE_PARM) && ValidateImageParameter(mod, nameof(M3MEMMod), parms, IMAGE_PARM, false, false)) // Should headmorphs be installable directly from archive...?
            {
                ImageAsset = parms[IMAGE_PARM];
            }

            ModdescMod = mod;
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="other"></param>
        public M3MEMMod(M3MEMMod other)
        {
            RelativeFileName = other.RelativeFileName;
            Title = other.Title;
            Description = other.Description;
            ModdescMod = other.ModdescMod;
        }

        /// <summary>
        /// Blank constructor for moddesc editor and for deserialization from json
        /// </summary>
        public M3MEMMod()
        {
        }

        // Todo: Implement compareto()

        public override string GetDescription()
        {
            var baseDescription = base.GetDescription();

            if (ModdescMod != null)
            {
                return $"{baseDescription}\n\nPart of {ModdescMod.ModName}";
            }
            else
            {
                return $"{baseDescription}\n\nPart of the texture library";
            }
        }

        /// <summary>
        /// Parameter map, used for the moddesc.ini editor Contains a list of values in the alternate mapped to their string value
        /// </summary>
        [JsonIgnore]
        public ObservableCollectionExtended<MDParameter> ParameterMap { get; } = new ObservableCollectionExtended<MDParameter>();

        /// <summary>
        /// List of all keys in the M3MEMMod struct that are publicly available for editing
        /// </summary>
        /// <param name="mod"></param>
        public void BuildParameterMap(Mod mod)
        {
            var parameterDictionary = new Dictionary<string, object>()
            {
                // List of available parameters for this object
                {FILENAME_PARM, RelativeFileName},
                {TITLE_PARM, Title},
                {DESCRIPTION_PARM, Description},
                {IMAGE_PARM, ImageAsset},
            };

            ParameterMap.ReplaceAll(MDParameter.MapIntoParameterMap(parameterDictionary));
        }

        /// <summary>
        /// Creates an M3MEMMod object for use in the moddesc.ini editor. This method exists due to the same signature as the constructor.
        /// </summary>
        /// <param name="editingMod"></param>
        /// <param name="memFile"></param>
        /// <returns></returns>
        public static M3MEMMod CreateForEditor(Mod editingMod, string memFile)
        {
            var m = new M3MEMMod();
            m.RelativeFileName = Path.GetFileName(memFile);
            m.Title = Path.GetFileNameWithoutExtension(memFile);
            m.ModdescMod = editingMod;
            return m;
        }
    }
}
