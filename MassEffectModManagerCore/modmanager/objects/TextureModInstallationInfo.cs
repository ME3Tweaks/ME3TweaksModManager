using System;
using System.Collections.Generic;
using System.IO;
using MassEffectModManagerCore.modmanager.localizations;
using LegendaryExplorerCore.Helpers;

namespace MassEffectModManagerCore.modmanager.objects
{
    /// <summary>
    /// Describes version information for a texture mod and/or installation
    /// </summary>
    public class TextureModInstallationInfo
    {
        public const int LATEST_TEXTURE_MOD_MARKER_VERSION = 4;
        public const int FIRST_EXTENDED_MARKER_VERSION = 4; // Do not change
        public const uint TEXTURE_MOD_MARKER_VERSIONING_MAGIC = 0xDEADBEEF;

        /// <summary>
        /// Major version of ALOT, e.g. 11
        /// </summary>
        public short ALOTVER;
        /// <summary>
        /// Update version of ALOT, e.g. the .2 of 11.2
        /// </summary>
        public byte ALOTUPDATEVER;
        /// <summary>
        /// Hotfix version for ALOT. This attribute has not been used and may never be
        /// </summary>
        public byte ALOTHOTFIXVER;
        /// <summary>
        /// Version of MEUITM, such as 2
        /// </summary>
        public int MEUITMVER;
        /// <summary>
        /// What the build number of the installer that was used for this installation was. This is the third set of digits in the version number (e.g. 586)
        /// </summary>
        public short ALOT_INSTALLER_VERSION_USED;
        /// <summary>
        /// The version of MEM that was used to perform the installation of the textures
        /// </summary>
        public short MEM_VERSION_USED;

        public List<InstalledTextureMod> InstalledTextureMods { get; } = new List<InstalledTextureMod>();
        public string InstallerVersionFullName { get; set; }
        public DateTime InstallationTimestamp { get; set; }

        public static readonly TextureModInstallationInfo NoVersion = new TextureModInstallationInfo(0, 0, 0, 0); //not versioned

        /// <summary>
        /// Creates a installation information object, with the information about what was used to install it.
        /// </summary>
        /// <param name="ALOTVersion"></param>
        /// <param name="ALOTUpdaterVersion"></param>
        /// <param name="ALOTHotfixVersion"></param>
        /// <param name="MEUITMVersion"></param>
        /// <param name="memVersionUsed"></param>
        /// <param name="alotInstallerVersionUsed"></param>
        public TextureModInstallationInfo(short ALOTVersion, byte ALOTUpdaterVersion, byte ALOTHotfixVersion, int MEUITMVersion, short memVersionUsed, short alotInstallerVersionUsed)
        {
            ALOTVER = ALOTVersion;
            ALOTUPDATEVER = ALOTUpdaterVersion;
            ALOTHOTFIXVER = ALOTHotfixVersion;
            MEUITMVER = MEUITMVersion;
            MEM_VERSION_USED = memVersionUsed;
            ALOT_INSTALLER_VERSION_USED = alotInstallerVersionUsed;
        }

        private TextureModInstallationInfo()
        {

        }

        /// <summary>
        /// Calculates the maximum version numbers between two TextureModInstallationInfo objects and returns the result.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public TextureModInstallationInfo MergeWith(TextureModInstallationInfo other)
        {
            TextureModInstallationInfo tmii = new TextureModInstallationInfo();
            tmii.ALOTVER = Math.Max(ALOTVER, other.ALOTVER);
            tmii.ALOTUPDATEVER = Math.Max(ALOTUPDATEVER, other.ALOTUPDATEVER);
            tmii.ALOTHOTFIXVER = Math.Max(ALOTHOTFIXVER, other.ALOTHOTFIXVER);
            tmii.MEUITMVER = Math.Max(MEUITMVER, other.MEUITMVER);
            return tmii;
        }

        /// <summary>
        /// Creates a installation information object, without information about what was used to install it.
        /// </summary>
        /// <param name="ALOTVersion"></param>
        /// <param name="ALOTUpdateVersion"></param>
        /// <param name="ALOTHotfixVersion"></param>
        /// <param name="MEUITMVersion"></param>
        public TextureModInstallationInfo(short ALOTVersion, byte ALOTUpdateVersion, byte ALOTHotfixVersion, int MEUITMVersion)
        {
            ALOTVER = ALOTVersion;
            ALOTUPDATEVER = ALOTUpdateVersion;
            ALOTHOTFIXVER = ALOTHotfixVersion;
            MEUITMVER = MEUITMVersion;
        }

        public TextureModInstallationInfo(TextureModInstallationInfo textureModInstallationInfo)
        {
        }

        public override string ToString()
        {
            string str = "";
            if (IsNotVersioned)
            {
                return M3L.GetString(M3L.string_textureModded);
            }

            if (ALOTVER > 0)
            {
                str += $@"ALOT {ALOTVER}.{ALOTUPDATEVER}";
            }

            if (MEUITMVER > 0)
            {
                if (str != @"")
                {
                    str += @", ";
                }

                str += $@"MEUITM v{MEUITMVER}";
            }

            return str;
        }

        /// <summary>
        /// Returns if this object doesn't represent an actual ALOT/MEUITM installation (no values set)
        /// </summary>
        /// <returns></returns>
        public bool IsNotVersioned => ALOTVER == 0 && ALOTHOTFIXVER == 0 & ALOTUPDATEVER == 0 && MEUITMVER == 0;

        /// <summary>
        /// MEMI extended version. 0 if this is not v3 or higher
        /// </summary>
        public int MarkerExtendedVersion { get; set; }

        /// <summary>
        /// Position where data belonging to this marker begins.
        /// </summary>
        public int MarkerStartPosition { get; set; }

        ///// <summary>
        ///// Calculates an installation marker based on the existing and installed file sets
        ///// </summary>
        ///// <param name="existing"></param>
        ///// <param name="packageFilesToInstall"></param>
        ///// <returns></returns>
        //public static TextureModInstallationInfo CalculateMarker(TextureModInstallationInfo existing, List<InstallerFile> packageFilesToInstall)
        //{
        //    TextureModInstallationInfo final = existing ?? new TextureModInstallationInfo();
        //    foreach (var v in packageFilesToInstall)
        //    {
        //        final = final.MergeWith(v.AlotVersionInfo);
        //    }
        //    return final;
        //}

        public Version ToVersion()
        {
            return new Version(ALOTVER, ALOTUPDATEVER, ALOTHOTFIXVER, MEUITMVER);
        }

        public static bool operator <(TextureModInstallationInfo first, TextureModInstallationInfo second)
        {
            return first.ToVersion().CompareTo(second.ToVersion()) < 0;
        }

        public static bool operator >(TextureModInstallationInfo first, TextureModInstallationInfo second)
        {
            return first.ToVersion().CompareTo(second.ToVersion()) > 0;
        }

        public class InstalledTextureMod
        {
            public enum InstalledTextureModType
            {
                USERFILE,
                MANIFESTFILE
            }

            public InstalledTextureModType ModType { get; set; }
            public string ModName { get; set; }
            public string AuthorName { get; set; }

            public string UIName
            {
                get
                {
                    var ret = ModName;
                    if (ModType == InstalledTextureModType.MANIFESTFILE)
                    {
                        ret = M3L.GetString(M3L.string_interp_modNameByAuthorName, ModName, AuthorName);
                    }
                    return ret;
                }
            }

            public List<string> ChosenOptions { get; } = new List<string>();

            ///// <summary>
            ///// Generates an InstalledTextureMod object from the given installer file. PreinstallMod objects are not supported.
            ///// </summary>
            ///// <param name="ifx"></param>
            //public InstalledTextureMod(InstallerFile ifx)
            //{
            //    if (ifx is PreinstallMod) throw new Exception(@"PreinstallMod is not a type of texture mod!");
            //    ModType = ifx is UserFile ? ModType = InstalledTextureModType.USERFILE : InstalledTextureModType.MANIFESTFILE;
            //    ModName = ifx.FriendlyName;
            //    if (ifx is ManifestFile mf)
            //    {
            //        AuthorName = ifx.Author;
            //        if (mf.ChoiceFiles.Any())
            //        {
            //            Debug.WriteLine("hi");
            //        }

            //        foreach (var cf in mf.ChoiceFiles)
            //        {
            //            var chosenFile = cf.GetChosenFile();
            //            if (chosenFile != null)
            //            {
            //                ChosenOptions.Add($"{cf.ChoiceTitle}: {chosenFile.ChoiceTitle}");
            //            }
            //        }
            //    }
            //}


            /// <summary>
            /// Reads installed texture mod info from the stream, based on the marker version
            /// </summary>
            /// <param name="inStream"></param>
            /// <param name="extendedMarkerVersion"></param>
            public InstalledTextureMod(Stream inStream, int extendedMarkerVersion)
            {
                // V4 marker version - DEFAULT
                ModType = (InstalledTextureModType)inStream.ReadByte();
                ModName = extendedMarkerVersion == 0x02 ? inStream.ReadStringUnicodeNull() : inStream.ReadUnrealString(); inStream.ReadStringUnicodeNull();
                if (ModType == InstalledTextureModType.MANIFESTFILE)
                {
                    AuthorName = extendedMarkerVersion == 0x02 ? inStream.ReadStringUnicodeNull() : inStream.ReadUnrealString();
                    var numChoices = inStream.ReadInt32();
                    while (numChoices > 0)
                    {
                        ChosenOptions.Add(inStream.ReadStringUnicodeNull());
                        numChoices--;
                    }
                }
            }

            public InstalledTextureMod()
            {

            }

            /// <summary>
            /// Writes this object's information to the stream, using the latest texture mod marker format.
            /// </summary>
            /// <param name="fs"></param>
            public void WriteToMarker(Stream fs)
            {
                fs.WriteByte((byte)ModType); // user file = 0, manifest file = 1
                fs.WriteStringUnicodeNull(ModName);
                if (ModType == InstalledTextureModType.MANIFESTFILE)
                {
                    fs.WriteStringUnicodeNull(AuthorName);
                    fs.WriteInt32(ChosenOptions.Count);
                    foreach (var c in ChosenOptions)
                    {
                        fs.WriteStringUnicodeNull(c);
                    }
                }
            }
        }

        ///// <summary>
        ///// Sets the installed texture mods list for this marker based on the list of files that are passed in
        ///// </summary>
        ///// <param name="installedInstallerFiles"></param>
        //public void SetInstalledFiles(List<InstallerFile> installedInstallerFiles)
        //{
        //    InstalledTextureMods.ReplaceAll(installedInstallerFiles.Where(x => !(x is PreinstallMod)).Select(x => new InstalledTextureMod(x)));
        //}

        //public string ToExtendedString()
        //{
        //    StringBuilder sb = new StringBuilder();
        //    sb.AppendLine($"MEMI Marker Version {MarkerExtendedVersion}");
        //    sb.AppendLine($"Installation by {InstallerVersionFullName}");
        //    sb.AppendLine($"Installed on {InstallationTimestamp}");
        //    sb.AppendLine("Files installed:");
        //    foreach (var v in InstalledTextureMods)
        //    {
        //        var str = $@"  [{v.ModType}] {v.ModName}";
        //        if (!string.IsNullOrWhiteSpace(v.AuthorName)) str += $" by {v.AuthorName}";
        //        sb.AppendLine(str);
        //        if (v.ChosenOptions.Any())
        //        {
        //            sb.AppendLine(@"  Optional items:");
        //            foreach (var c in v.ChosenOptions)
        //            {
        //                sb.AppendLine($@"    {c}");
        //            }
        //        }
        //    }

        //    sb.AppendLine($"MEM version used: {MEM_VERSION_USED}");
        //    sb.AppendLine($"Installer core version: {ALOT_INSTALLER_VERSION_USED}");
        //    sb.AppendLine($"Versioned texture info: {ToString()}");
        //    return sb.ToString();
        //}
    }
}
