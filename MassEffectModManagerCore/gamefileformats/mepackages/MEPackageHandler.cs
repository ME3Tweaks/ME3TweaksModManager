using System;
using System.IO;
using MassEffectModManagerCore.modmanager;
using MassEffectModManagerCore.modmanager.helpers;

namespace ME3Explorer.Packages
{
    public static class MEPackageHandler
    {
        static Func<string, Mod.MEGame, MEPackage> MEConstructorDelegate;
        static Func<Stream, Mod.MEGame, MEPackage> MEConstructorStreamDelegate;
        private static bool initialized;

        public static void Initialize()
        {
            if (!initialized)
            {
                MEConstructorDelegate = MEPackage.Initialize();
                MEConstructorStreamDelegate = MEPackage.InitializeStream();
                initialized = true;
            }
        }

        /// <summary>
        /// Opens an MEPackage from a stream. The stream position should be at the start of the expected MEPackage data.
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static MEPackage OpenMEPackage(Stream stream, string debugSourcePath = null)
        {
            if (!initialized)
            {
                MEConstructorDelegate = MEPackage.Initialize();
                MEConstructorStreamDelegate = MEPackage.InitializeStream();
                initialized = true;
            }
            ushort version;
            ushort licenseVersion;

            long originPoint = stream.Position;
            stream.Seek(4, SeekOrigin.Current);
            version = stream.ReadUInt16();
            licenseVersion = stream.ReadUInt16();
            stream.Position = originPoint;

            if (version == 684 && licenseVersion == 194 ||
                version == 512 && licenseVersion == 130 ||
                version == 491 && licenseVersion == 1008)
            {
                var package = MEConstructorStreamDelegate(stream, Mod.MEGame.Unknown);
                package.FileSourceForDebugging = debugSourcePath;
                return package;
            }
            else
            {
                throw new FormatException("Not an ME1, ME2 or ME3 package file.");
            }
        }

        public static MEPackage OpenMEPackage(string pathToFile)
        {
            if (!initialized)
            {
                MEConstructorDelegate = MEPackage.Initialize();
                MEConstructorStreamDelegate = MEPackage.InitializeStream();
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