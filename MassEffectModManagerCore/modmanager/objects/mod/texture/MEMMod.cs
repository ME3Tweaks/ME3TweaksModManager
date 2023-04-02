using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ME3TweaksCore.Helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.mod.interfaces;
using Newtonsoft.Json;

namespace ME3TweaksModManager.modmanager.objects.mod.texture
{
    /// <summary>
    /// Describes a MEMMod, which is a containing object for a .mem file
    /// </summary>
    public class MEMMod : M3ValidateOnLoadObject, INotifyPropertyChanged
    {
        private string _displayString;

        /// <summary>
        /// The header string
        /// </summary>
        [JsonIgnore]
        public string DisplayString
        {
            get
            {
                if (_displayString != null) return _displayString;
                return Path.GetFileName(FilePath);
            }
            set => SetField(ref _displayString, value);
        }

        /// <summary>
        /// The full path to the .mem file
        /// </summary>
        [JsonProperty("filepath")]
        public string FilePath { get; set; }

        /// <summary>
        /// The game this texture mod is for
        /// </summary>
        [JsonIgnore]
        public MEGame Game { get; set; }

        /// <summary>
        /// String for showing in the UI.
        /// </summary>
        [JsonIgnore]
        public string UIDescription => GetDescription();

        /// <summary>
        /// A list of modded textures as parsed out of the mem file. This can be null if it hasn't been parsed
        /// </summary>
        [JsonIgnore]
        public List<string> ModdedTextures { get; set; }

        /// <summary>
        /// If this file exists on disk (UI binding)
        /// </summary>
        [JsonIgnore]
        public bool FileExists { get; set; }

        /// <summary>
        /// The list of texture exports this MEM mod modifies. Cached the first time the list is used. 
        /// </summary>
        [JsonIgnore]
        public List<string> ModifiedExportNames { get; set; }



        /// <summary>
        /// Gets the full path to the MEM file. This method can be overridden.
        /// </summary>
        /// <returns></returns>
        public virtual string GetFilePathToMEM()
        {
            return FilePath;
        }

        /// <summary>
        /// Blank initialization constructor
        /// </summary>
        public MEMMod() { }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="other">Other MEM mod to clone</param>
        public MEMMod(MEMMod other)
        {
            FilePath = other.FilePath;
            Game = other.Game;
            ModdedTextures = other.ModdedTextures?.ToList(); // Clone the other's object with .ToList()
            FileExists = other.FileExists; // Should we do this...?
        }

        public MEMMod(string filePath)
        {
            FilePath = filePath;
        }

        public void ParseMEMData()
        {
            var filePath = GetFilePathToMEM();
            FileExists = File.Exists(filePath);
            if (FileExists)
            {
                Game = ModFileFormats.GetGameMEMFileIsFor(filePath);
                ModdedTextures = ModFileFormats.GetFileListForMEMFile(filePath);
            }
        }

        public virtual string GetDescription()
        {
            var modifiedExports = GetModifiedExportNames();
            return $"This texture mod modifies the following exports:\n{string.Join('\n', modifiedExports.Select(x => $@" - {(string.IsNullOrWhiteSpace(x) ? "<export not listed in .mem file>" : x)}"))}";
        }

        /// <summary>
        /// Returns the list of modified export names. 
        /// </summary>
        /// <returns></returns>
        public List<string> GetModifiedExportNames()
        {
            if (ModifiedExportNames != null) return ModifiedExportNames;
            var filePath = GetFilePathToMEM();
            if (File.Exists(filePath))
            {
                ModifiedExportNames = ModFileFormats.GetFileListForMEMFile(filePath).OrderBy(x => x).ToList(); // Alphabetize
            }
            else
            {
                ModifiedExportNames = new List<string>();
            }

            return ModifiedExportNames;
        }


        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #region NEWTONSOFT STUFF

        public bool ShouldSerializeFilePath()
        {
            if (this is M3MEMMod) return false; // M3MEMMod files should not serialize this variable
            return true;
        }
        #endregion
    }
}
