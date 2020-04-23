using MassEffectModManagerCore.modmanager.objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MassEffectModManagerCore.modmanager.me3tweaks
{
    public class BasegameFileIdentificationService
    {
        /// <summary>
        /// Looks up information about a basegame file using the Basegame File Identification Service
        /// </summary>
        /// <param name="target"></param>
        /// <param name="fullfilepath"></param>
        /// <returns></returns>
        public static BasegameCloudDBFile GetBasegameFileSource(GameTarget target, string fullfilepath)
        {
            if (App.BasegameFileIdentificationService == null) return null; //Not loaded
            if (App.BasegameFileIdentificationService.TryGetValue(target.Game.ToString(), out var infosForGame))
            {
                var relativeFilename = fullfilepath.Substring(target.TargetPath.Length + 1).ToUpper();

                if (infosForGame.TryGetValue(relativeFilename, out var items))
                {
                    var md5 = Utilities.CalculateMD5(fullfilepath);
                    return items.FirstOrDefault(x => x.hash == md5); //may need adjusted if multiple mods share files
                    //return info;
                }
            }

            return null;
        }

        public class BasegameCloudDBFile
        {
            public string file { get; set; }
            public string hash { get; set; }
            public string source { get; set; }
            public string game { get; set; }
            public BasegameCloudDBFile() { }
            public BasegameCloudDBFile(string file, GameTarget gameTarget, Mod modBeingInstalled)
            {
                this.file = file.Substring(gameTarget.TargetPath.Length + 1);
                this.hash = Utilities.CalculateMD5(file);
                this.game = gameTarget.Game.ToString().Substring(2);
                this.source = modBeingInstalled.ModName + @" " + modBeingInstalled.ModVersionString;
            }
        }
    }
}