using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MassEffectModManagerCore.modmanager.asi
{

    /// <summary>
    /// Object describing an installed ASI file. It is not a general ASI mod object, but it can be mapped to one
    /// </summary>
    public class InstalledASIMod
    {
        public InstalledASIMod(string asiFile, Mod.MEGame game)
        {
            Game = game;
            InstalledPath = asiFile;
            Filename = Path.GetFileNameWithoutExtension(asiFile);
            Hash = BitConverter.ToString(System.Security.Cryptography.MD5.Create()
                .ComputeHash(File.ReadAllBytes(asiFile))).Replace(@"-", "").ToLower();
        }

        public Mod.MEGame Game { get; }
        public string InstalledPath { get; set; }
        public string Hash { get; set; }
        public string Filename { get; set; }
    }
}
