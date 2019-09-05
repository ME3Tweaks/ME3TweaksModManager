using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IniParser.Parser;

namespace MassEffectModManager.modmanager
{
    public class Mod : INotifyPropertyChanged
    {
        public const string DefaultWebsite = "http://example.com"; //this is required to prevent exceptions when binding the navigateuri
        public event PropertyChangedEventHandler PropertyChanged;

        // Constants

        //Mod variables
        public bool ValidMod;
        private string ModPath;

        //private List<ModJob> jobs;



        public string ModName { get; set; }
        public string ModDeveloper { get; set; }
        public string ModDescription { get; set; }

        public string DisplayedModDescription
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(ModDescription);
                sb.AppendLine("=============================");
                //Todo: Mod Deltas

                //Todo: Automatic configuration

                //Todo: Optional manuals

                sb.AppendLine($"Mod version: {ModVersionString}");
                sb.AppendLine($"Mod developer: {ModDeveloper}");

                return sb.ToString();
            }
        }

        public string ModAuthor { get; set; }
        public string ModVersionString { get; set; }
        public double ParsedModVersion { get; set; }
        public string ModWebsite { get; set; } = ""; //not null default I guess.
        public double ModDescTargetVersion { get; set; }
        
        public Mod(MemoryStream iniStream)
        {
            
        }

        public Mod(string filePath)
        {
            loadMod(File.ReadAllText(filePath));
        }

        private void loadMod(string iniText)
        {
            var parser = new IniDataParser();
            var iniData = parser.Parse(iniText);

            ModName = iniData["ModInfo"]["modname"];
            ModDescription = Utilities.ConvertBrToNewline(iniData["ModInfo"]["moddesc"]);
            ModDeveloper = iniData["ModInfo"]["moddev"];
            ModVersionString = iniData["ModInfo"]["modver"];
            double.TryParse(ModVersionString, out double parsedValue);
            ParsedModVersion = parsedValue;

            ModWebsite = iniData["ModInfo"]["modsite"] ?? DefaultWebsite;
            Thread.Sleep(500);
            ValidMod = true;
        }
    }
}
