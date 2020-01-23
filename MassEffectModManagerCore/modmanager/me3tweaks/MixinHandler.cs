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
using MassEffectModManagerCore.modmanager.memoryanalyzer;
using Microsoft.IO;
using ByteSizeLib;
using System.Runtime;

namespace MassEffectModManagerCore.modmanager.me3tweaks
{
    /// <summary>
    /// Handler class for the ME3Tweaks Mixin Package
    /// </summary>
    public static class MixinHandler
    {
        static MixinHandler()
        {
            AttemptResetMemoryManager();

        }
        public static RecyclableMemoryStreamManager MixinMemoryStreamManager { get; private set; }
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
        /// <summary>
        /// Gets a dictionary of module=> [filename, list of mixins] to apply.
        /// </summary>
        /// <param name="allmixins"></param>
        /// <returns></returns>
        public static Dictionary<ModJob.JobHeader, Dictionary<string, List<Mixin>>> GetMixinApplicationList(List<Mixin> allmixins)
        {
            var compilingListsPerModule = new Dictionary<ModJob.JobHeader, Dictionary<string, List<Mixin>>>();
            var modules = allmixins.Select(x => x.TargetModule).Distinct().ToList();
            foreach (var module in modules)
            {
                var moduleMixinMapping = new Dictionary<string, List<Mixin>>();
                var mixinsForModule = allmixins.Where(x => x.TargetModule == module).ToList();
                foreach (var mixin in mixinsForModule)
                {
                    List<Mixin> mixinListForFile;
                    if (!moduleMixinMapping.TryGetValue(mixin.TargetFile, out mixinListForFile))
                    {
                        mixinListForFile = new List<Mixin>();
                        moduleMixinMapping[mixin.TargetFile] = mixinListForFile;
                    }

                    //make sure finalizer is last
                    if (mixin.IsFinalizer)
                    {
                        CLog.Information(
                            $@"Adding finalizer mixin to mixin list for file {Path.GetFileName(mixin.TargetFile)}: {mixin.PatchName}",
                            Settings.LogModMakerCompiler);
                        mixinListForFile.Add(mixin);
                    }
                    else
                    {
                        CLog.Information(
                            $@"Adding mixin to mixin list for file {Path.GetFileName(mixin.TargetFile)}: {mixin.PatchName}",
                            Settings.LogModMakerCompiler);
                        mixinListForFile.Insert(0, mixin);
                    }
                }

                //verify only one finalizer
                foreach (var list in moduleMixinMapping)
                {
                    if (list.Value.Count(x => x.IsFinalizer) > 1)
                    {
                        Log.Error(@"ERROR: MORE THAN ONE FINALIZER IS PRESENT FOR FILE: " + list.Key);
                        //do something here to abort
                    }
                }

                compilingListsPerModule[module] = moduleMixinMapping;
            }
            return compilingListsPerModule;
        }
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

        /// <summary>
        /// Fetches a mixin by it's ID. This does not load the patch data.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        internal static Mixin GetMixinByME3TweaksID(int id)
        {
            return ME3TweaksPackageMixins.FirstOrDefault(x => x.ME3TweaksID == id);
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

        private static MemoryStream GetPatchDataForMixin(ZipArchive zip, Mixin mixin)
        {
            var patchfile = zip.GetEntry(mixin.PatchFilename);
            if (patchfile != null)
            {
                using var patchStream = patchfile.Open();
                MemoryStream patchData = MixinMemoryStreamManager.GetStream();
                patchStream.CopyTo(patchData);
                patchData.Position = 0;
                return patchData;
            }
            return null;
        }

        public static void LoadPatchDataForMixins(List<Mixin> mixins)
        {
            using (var file = File.OpenRead(MixinPackagePath))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
            {
                foreach (var mixin in mixins)
                {
                    mixin.PatchData = GetPatchDataForMixin(zip, mixin);
                }
            }
        }

        private static string StreamToString(Stream stream)
        {
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        internal static MemoryStream ApplyMixins(MemoryStream decompressedStream, List<Mixin> mixins, Action notifyApplicationDone = null, Action<string> failedApplicationCallback = null)
        {
            foreach (var mixin in mixins)
            {
                Log.Information(@"Applying mixin: " + mixin.PatchName);
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
                        Log.Information(@"Applied mixin: " + mixin.PatchName);
                        decompressedStream.Dispose();
                        decompressedStream = outStream; //pass through
                    }
                }
                else
                {
                    Log.Error($@"Mixin {mixin.PatchName} cannot be applied to this data, length of data is wrong. Expected size {mixin.TargetSize} but received source data size of {decompressedStream.Length}");
                    failedApplicationCallback?.Invoke($"{mixin.PatchName} cannot be applied to {mixin.TargetFile}. Expected size { mixin.TargetSize} but received source data size of { decompressedStream.Length}");
                }

                notifyApplicationDone?.Invoke();
            }

            return decompressedStream;
        }

        public static void FreeME3TweaksPatchData()
        {
            foreach (var mixin in ME3TweaksPackageMixins)
            {
                if (mixin.PatchData != null)
                {
                    mixin.PatchData.Dispose();
                    mixin.PatchData = null;
                }
            }
            //MixinMemoryStreamManager.AggressiveBufferReturn = true;
        }

        public static void AttemptResetMemoryManager()
        {
            bool isResetting = false;
            if (MixinMemoryStreamManager == null || (MixinMemoryStreamManager.LargePoolInUseSize == 0 && MixinMemoryStreamManager.SmallPoolInUseSize == 0))
            {
                if (MixinMemoryStreamManager != null) isResetting = true;
                var MB = 1024 * 1024;
                MixinMemoryStreamManager = new RecyclableMemoryStreamManager(MB * 4, MB * 128, MB * 256);
                MixinMemoryStreamManager.GenerateCallStacks = true;
                MixinMemoryStreamManager.AggressiveBufferReturn = true;
                MemoryAnalyzer.AddTrackedMemoryItem("Mixin Memory Stream Manager", new WeakReference(MixinMemoryStreamManager));
            }
            if (isResetting)
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect();
            }
        }
    }
}
