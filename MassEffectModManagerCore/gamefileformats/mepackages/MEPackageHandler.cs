using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using MassEffectModManager.modmanager;
using MassEffectModManager.modmanager.helpers;

namespace ME3Explorer.Packages
{
    public static class MEPackageHandler
    {
        static Func<string, Mod.MEGame, MEPackage> MEConstructorDelegate;
        private static bool initialized;

        public static IMEPackage OpenMEPackage(string pathToFile)
        {
            if (!initialized)
            {
                MEConstructorDelegate = MEPackage.Initialize();
                initialized = true;
            }
            pathToFile = Path.GetFullPath(pathToFile); //STANDARDIZE INPUT
            ushort version;
            ushort licenseVersion;

            using (FileStream fs = new FileStream(pathToFile, FileMode.Open, FileAccess.Read))
            {
                fs.Seek(4, SeekOrigin.Begin);
                version = fs.ReadUInt16();
                licenseVersion = fs.ReadUInt16();
            }


            if (version == 684 && licenseVersion == 194 ||
                version == 512 && licenseVersion == 130 ||
                version == 491 && licenseVersion == 1008)
            {
                return MEConstructorDelegate(pathToFile, Mod.MEGame.Unknown);
            }
            else
            {
                throw new FormatException("Not an ME1, ME2 or ME3 package file.");
            }
        }

        public static void CreateAndSaveMePackage(string path, Mod.MEGame game)
        {
            MEConstructorDelegate(path, game).save();
        }

    }
}