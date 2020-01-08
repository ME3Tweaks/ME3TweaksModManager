using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.IO;

namespace MassEffectModManagerCore.modmanager.me3tweaks
{
    /// <summary>
    /// Handler class for the ME3Tweaks Mixin Package
    /// </summary>
    public class MixinHandler
    {
        public static readonly RecyclableMemoryStreamManager MixinMemoryStreamManager = new RecyclableMemoryStreamManager();
        public static string ServerMixinHash;
        public static readonly string MixinPackageEndpoint = @"https://me3tweaks.com/mixins/mixinlibrary.zip";
        public static readonly string MixinPackagePath = Path.Combine(Directory.CreateDirectory(Path.Combine(Utilities.GetAppDataFolder(), "Mixins", "me3tweaks")).FullName, "mixinlibrary.zip");
        public static bool IsMixinPackageUpToDate()
        {
            if (ServerMixinHash == null) return true; //can't check. Just say it's up to date.
            if (File.Exists(MixinPackagePath))
            {
                var md5 = Utilities.CalculateMD5(MixinPackagePath);
                return md5.Equals(ServerMixinHash, StringComparison.InvariantCultureIgnoreCase);
            }

            return false;
        }

        public static List<Mixin> ME3TweaksPackageMixins = new List<Mixin>();
        public static List<Mixin> UserMixins = new List<Mixin>();

        internal static bool MixinPackageAvailable()
        {
            return File.Exists(MixinPackagePath);
        }

        internal static void LoadME3TweaksPackage()
        {
            if (MixinPackageAvailable())
            {
                try
                {
                    using (var file = File.OpenRead(MixinPackagePath))
                    using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
                    {
                        var manifest = zip.GetEntry("manifest.xml");
                        if (manifest != null)
                        {
                            //parse manifest.
                            using var mStream = manifest.Open();
                            var manifestText = StreamToString(mStream);
                            XDocument manifestDoc = XDocument.Parse(manifestText);
                            ME3TweaksPackageMixins = manifestDoc.Root.Elements().Select(elem => new Mixin()
                            {
                                PatchName = elem.Element("patchname").Value,
                                PatchDesc = elem.Element("patchdesc").Value,
                                PatchDeveloper = elem.Element("patchdev").Value,
                                PatchVersion = int.Parse(elem.Element("patchver").Value),
                                //targetversion = elem.Element("").Value,
                                TargetModule = Enum.Parse<ModJob.JobHeader>(elem.Element("targetmodule").Value),
                                TargetFile = elem.Element("targetfile").Value,
                                TargetSize = int.Parse(elem.Element("targetsize").Value),
                                IsFinalizer = elem.Element("finalizer").Value == "1" ? true : false,
                                PatchFilename = elem.Element("filename").Value,
                                //patchurl = elem.Element("").Value,
                                //folder = elem.Element("").Value,
                                ME3TweaksID = int.Parse(elem.Element("me3tweaksid").Value)
                            }).ToList();
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error("Error loading me3tweaks mixin package: " + e.Message);
                }
            }
            else
            {
                Log.Warning("Cannot load ME3Tweaks package: Local cached file does not exist");
            }
        }

        internal static Mixin GetMixinByME3TweaksID(int id)
        {
            var mixin = ME3TweaksPackageMixins.FirstOrDefault(x => x.ME3TweaksID == id);
            if (mixin.PatchData != null) return mixin;

            //Load patch data.
            var patchData = GetPatchDataForMixin(mixin);
            mixin.PatchData = patchData;
            return mixin;
        }

        /// <summary>
        /// Creates a mixin object from Dynamic Mixin data in a modmaker mod definition
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        internal static Mixin ReadDynamicMixin(XElement element)
        {
            Mixin dynamic = new Mixin()
            {
                TargetModule = Enum.Parse<ModJob.JobHeader>(element.Attribute("targetmodule").Value),
                TargetFile = element.Attribute("targetfile").Value,
                PatchName = element.Attribute("name").Value,
                TargetSize = int.Parse(element.Attribute("targetsize").Value)
            };
            var hexStr = element.Value;
            byte[] hexData = Utilities.HexStringToByteArray(hexStr);
            dynamic.PatchData = new MemoryStream(hexData);
            return dynamic;
        }

        private static MemoryStream GetPatchDataForMixin(Mixin mixin)
        {
            using (var file = File.OpenRead(MixinPackagePath))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
            {
                var patchfile = zip.GetEntry(mixin.PatchFilename);
                if (patchfile != null)
                {
                    using var patchStream = patchfile.Open();
                    MemoryStream patchData = new MemoryStream();
                    patchStream.CopyTo(patchData);
                    patchData.Position = 0;
                    return patchData;
                }
                return null;
            }
        }

        private static string StreamToString(Stream stream)
        {
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        internal static MemoryStream ApplyMixins(MemoryStream decompressedStream, List<Mixin> mixins, Action notifyApplicationDone = null)
        {
            foreach (var mixin in mixins)
            {
                CLog.Information(@"Applying mixin: " + mixin.PatchName, Settings.LogModMakerCompiler);
                if (decompressedStream.Length == mixin.TargetSize)
                {
                    var outStream = MixinMemoryStreamManager.GetStream();
                    //MemoryStream outStream = new MemoryStream();
                    JPatch.ApplyJPatch(decompressedStream, mixin.PatchData, outStream);
                    if (!mixin.IsFinalizer && outStream.Length != decompressedStream.Length)
                    {
                        Log.Error($@"Applied mixin {mixin.PatchName} is not a finalizer but the filesize has changed!! The output of this mixin patch will be discarded.");
                    }
                    else
                    {
                        CLog.Information(@"Applied mixin: " + mixin.PatchName, Settings.LogModMakerCompiler);
                        decompressedStream.Dispose();
                        decompressedStream = outStream; //pass through
                    }
                }
                else
                {
                    Log.Error($@"Mixin {mixin.PatchName} cannot be applied to this data, length of data is wrong. Expected size {mixin.TargetSize} but received source data size of {decompressedStream.Length}");
                }

                notifyApplicationDone?.Invoke();
            }

            return decompressedStream;
        }
    }
}
