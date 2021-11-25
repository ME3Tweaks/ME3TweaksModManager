#if DEBUG
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Serialization;
using MassEffectModManagerCore.modmanager.helpers;
using LegendaryExplorerCore.Helpers;

namespace MassEffectModManagerCore.modmanager.me3tweaks
{
    [Localizable(false)]
    public class Experiments
    {
        class LocalizedFile
        {
            public string basename;
            public bool basenameVersionExists;
            public List<string> localizedVersions = new List<string>();
        }

        // Must be lowercase to match target xml file
        public class extractionredirect
        {
            public string archivepathroot { get; set; }
            public string relativedestinationdirectory { get; set; }
            public string optionalrequireddlc { get; set; } // ; list
            public string optionalrequiredfiles { get; set; } // ; list
            public string optionalrequiredfilessizes { get; set; } // ; list
            public string loggingname { get; set; }
        }

        private static string Serialize<T>(T dataToSerialize)
        {
            try
            {
                var stringwriter = new System.IO.StringWriter();
                var serializer = new XmlSerializer(typeof(T));
                serializer.Serialize(stringwriter, dataToSerialize);
                return stringwriter.ToString();
            }
            catch
            {
                throw;
            }
        }

        public static string ConvertAlternatesToALOTManifestMod(string inputText)
        {
            var conditionalStructs = StringStructParser.GetParenthesisSplitValues(inputText);
            var redirects = new List<extractionredirect>();
            XElement root = new XElement("root");
            foreach (var cstruct in conditionalStructs)
            {
                XElement element = new XElement("extractionredirect");
                root.Add(element);
                var parsed = StringStructParser.GetCommaSplitValues(cstruct);
                var redirect = new extractionredirect();
                if (parsed.TryGetValue("ModAltDLC", out var arp))
                {
                    element.SetAttributeValue("archiverootpath", arp.Replace('/', '\\'));
                }
                if (parsed.TryGetValue("ModDestDLC", out var rdd))
                {
                    element.SetAttributeValue("relativedestinationdirectory", @"BIOGame\DLC\"+rdd.Replace('/', '\\'));
                }
                if (parsed.TryGetValue("ConditionalDLC", out var ord))
                {
                    element.SetAttributeValue("optionalrequireddlc", string.Join(';', StringStructParser.GetSemicolonSplitList(ord)));
                }
                if (parsed.TryGetValue("RequiredFileRelativePaths", out var orf))
                {
                    element.SetAttributeValue("optionalrequiredfiles", string.Join(';', StringStructParser.GetSemicolonSplitList(orf).Select(x=>x.Replace('/', '\\'))));
                }
                if (parsed.TryGetValue("RequiredFileSizes", out var orfs))
                {
                    element.SetAttributeValue("optionalrequiredfilessizes", string.Join(';', StringStructParser.GetSemicolonSplitList(orfs).Select(x => long.Parse(x))));
                }
                if (parsed.TryGetValue("FriendlyName", out var ln))
                {
                    element.SetAttributeValue("loggingname", ln);
                }
                redirects.Add(redirect); //just for later convenience if i refactor this
            }

            return root.ToString();
        }

        public static void GetUniquePlatformAudio()
        {
            var targetDirPS3 = @"Z:\Mass Effect Builds\PS3";
            var targetDirXenon = @"Z:\Mass Effect Builds\Xenon";
            var targetDirPc = @"Z:\ME1-Backup\BIOGame";

            var targetDirPS3Files = Directory.GetFiles(targetDirPS3, "*.isb", SearchOption.AllDirectories)
                .Select(filenameSelector).Where(filenameFilter).ToList();
            var targetDirXenonFiles = Directory.GetFiles(targetDirXenon, "*.isb", SearchOption.AllDirectories)
                .Select(filenameSelector).Where(filenameFilter).ToList();
            var targetDirPcFiles = Directory.GetFiles(targetDirPc, "*.isb", SearchOption.AllDirectories)
                .Select(filenameSelector).Where(filenameFilter).ToList();

            //var uniqueXenonFromPS3Files = targetDirXenonFiles.Except(targetDirPS3Files).ToList();
            //var uniqueXenonFromPCFiles2 = targetDirXenonFiles.Except(targetDirPcFiles).ToList();

            //var uniquePS3FromXenon = targetDirPS3Files.Except(targetDirXenonFiles).ToList();
            //var uniquePS3FromPC = targetDirPS3Files.Except(targetDirPcFiles).ToList();

            //var uniquePCFromXenon = targetDirPcFiles.Except(targetDirXenonFiles).ToList();
            //var uniquePCFromPS3 = targetDirPcFiles.Except(targetDirPS3Files).ToList();

            var ps3Localizations = buildLocalizedAudioTable(targetDirPS3Files);
            var pcLocalizations = buildLocalizedAudioTable(targetDirPcFiles);

            //var baseMissingPs3 = ps3Localizations.Where(x => x.Value.basenameVersionExists == false).ToList();
            //var baseMissingPc = pcLocalizations.Where(x => x.Value.basenameVersionExists == false).ToList();

            foreach (var sourceRow in ps3Localizations)
            {
                if (!pcLocalizations.TryGetValue(sourceRow.Key, out _))
                {
                    Debug.WriteLine($"Not on PC: {sourceRow.Key}");
                }
            }

            //foreach (var v in baseMissingPs3)
            //{
            //    Debug.WriteLine(v.Key);
            //}
            Debug.WriteLine("Done");
        }

        private static Dictionary<string, LocalizedFile> buildLocalizedAudioTable(List<string> fileList)
        {
            var localizedFiles = new Dictionary<string, LocalizedFile>();

            foreach (var v in fileList)
            {
                string basename = Path.GetFileNameWithoutExtension(v);
                string localization = null;
                bool isALocalizedFile = isLocalizedAudio(basename);
                if (isALocalizedFile)
                {
                    localization = basename.Substring(basename.LastIndexOf("_") + 1);
                    basename = basename.Substring(0, basename.LastIndexOf("_"));
                }

                if (!localizedFiles.TryGetValue(basename, out var localizedFile))
                {
                    localizedFile = new LocalizedFile();
                    localizedFile.basename = basename;
                    localizedFiles[basename] = localizedFile;
                }

                if (!isALocalizedFile)
                {
                    localizedFile.basenameVersionExists = true;
                }
                else
                {
                    localizedFile.localizedVersions.Add(localization);
                }
            }

            return localizedFiles;
        }

        private static bool isLocalizedAudio(string s)
        {
            if (s.EndsWith("_fr"))
                return true;
            if (s.EndsWith("_it"))
                return true;
            if (s.EndsWith("_de"))
                return true;
            return false; //int or none
        }

        public static void GetUniquePlatformFiles()
        {
            Dictionary<string, LocalizedFile> localizedFiles = new Dictionary<string, LocalizedFile>();
            var targetDirPS3 = @"Z:\Mass Effect Builds\PS3";
            var targetDirXenon = @"Z:\Mass Effect Builds\Xenon";
            var targetDirPc = @"Z:\ME1-Backup\BIOGame";

            var targetDirPS3Files = Directory.GetFiles(targetDirPS3, "*.*", SearchOption.AllDirectories)
                .Select(filenameSelector).Where(filenameFilter).ToList();
            var targetDirXenonFiles = Directory.GetFiles(targetDirXenon, "*.*", SearchOption.AllDirectories)
                .Select(filenameSelector).Where(filenameFilter).ToList();
            var targetDirPcFiles = Directory.GetFiles(targetDirPc, "*.*", SearchOption.AllDirectories)
                .Select(filenameSelector).Where(filenameFilter).ToList();

            var uniqueXenonFromPS3Files = targetDirXenonFiles.Except(targetDirPS3Files).ToList();
            var uniqueXenonFromPCFiles2 = targetDirXenonFiles.Except(targetDirPcFiles).ToList();

            var uniquePS3FromXenon = targetDirPS3Files.Except(targetDirXenonFiles).ToList();
            var uniquePS3FromPC = targetDirPS3Files.Except(targetDirPcFiles).ToList();

            var uniquePCFromXenon = targetDirPcFiles.Except(targetDirXenonFiles).ToList();
            var uniquePCFromPS3 = targetDirPcFiles.Except(targetDirPS3Files).ToList();

            foreach (var v in targetDirPS3Files)
            {
                string basename = v;
                string localization = null;
                bool isALocalizedFile = v.Contains("_loc_");
                if (isALocalizedFile)
                {
                    basename = v.Substring(0, v.IndexOf("_loc_"));
                    localization = v.Substring(basename.Length + "_loc_".Length);
                }

                if (!localizedFiles.TryGetValue(basename, out var localizedFile))
                {
                    localizedFile = new LocalizedFile();
                    localizedFile.basename = basename;
                    localizedFiles[basename] = localizedFile;
                }

                if (!isALocalizedFile)
                {
                    localizedFile.basenameVersionExists = true;
                }
                else
                {
                    localizedFile.localizedVersions.Add(localization);
                }
            }

            var baseMissing = localizedFiles.Where(x => x.Value.basenameVersionExists == false).ToList();

            Debug.WriteLine("Done");
        }

        static bool filenameFilter(string str)
        {
            var ext = Path.GetExtension(str);
            if (ext == ".isb") return true;
            if (ext == ".jnk") return false;
            if (ext == ".xex") return false;
            if (ext == ".ini") return false;
            if (ext == ".uncompressed_size") return false;
            if (ext == ".us") return false;

            if (ext == ".xxx") return false;
            if (ext == ".upk") return false;
            if (ext == ".u") return false;
            if (ext == ".sfm") return false;
            return false;
        }

        static string filenameSelector(string str)
        {
            var x = Path.GetFileName(str).ToLower();
            if (x.RepresentsPackageFilePath()) x = Path.GetFileNameWithoutExtension(x);
            if (x.RepresentsPackageFilePath()) x = Path.GetFileNameWithoutExtension(x);

            return x;
        }
    }
}
#endif