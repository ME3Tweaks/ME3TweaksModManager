using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using MassEffectModManagerCore.modmanager.me3tweaks;
using ME3ExplorerCore.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;


namespace MassEffectModManagerCore.modmanager.objects.nexusfiledb
{
    public class GameDatabase
    {

        /// <summary>
        /// Checks the local DB file against the hash listed in the manifest. Downloads if it is outdated.
        /// Should be run on a background thread
        /// </summary>
        public static void EnsureDatabaseFile(bool downloadIfNeverDownloaded = false)
        {
            if (App.ServerManifest != null && App.ServerManifest.TryGetValue(@"latest_nexusdb_hash", out var nexusDbHash))
            {
                string md5 = null;
                var dbPath = Path.Combine(Utilities.GetNexusModsCache(), @"nexusmodsdb.zip");
                if (File.Exists(dbPath))
                {
                    md5 = Utilities.CalculateMD5(dbPath);
                }

                if (md5 == null && downloadIfNeverDownloaded)
                {
                    // Download
                    DownloadDB(dbPath);
                }
                else if (md5 != null && md5 != nexusDbHash)
                {
                    // Download
                    DownloadDB(dbPath);
                }
            }
        }

        private static void DownloadDB(string nexusDBPath)
        {
            var downloadResult = OnlineContent.DownloadME3TweaksStaticAsset(@"nexusfiledb.zip");
            if (downloadResult.errorMessage == null)
            {
                downloadResult.download.WriteToFile(nexusDBPath);
            }
        }

        public static GameDatabase LoadDatabase(string domain, Stream zipDataStream = null)
        {
            bool closeStreamAtEnd = zipDataStream == null;
            if (zipDataStream == null)
            {
                var fPath = Path.Combine(Utilities.GetNexusModsCache(), @"nexusmodsdb.zip");
                if (File.Exists(fPath))
                {
                    zipDataStream = File.OpenRead(fPath);
                }
            }

            if (zipDataStream == null) return null; // Cannot load the DB!

            var zip = new ZipArchive(zipDataStream, ZipArchiveMode.Read);
            var domainEntry = zip.Entries.FirstOrDefault(x => x.Name.Equals(domain + @".json", StringComparison.InvariantCultureIgnoreCase));

            if (domainEntry != null)
            {
                using var decompStream = domainEntry.Open();
                StreamReader sr = new StreamReader(decompStream);
                var json = sr.ReadToEnd();
                if (closeStreamAtEnd)
                {
                    zip.Dispose();
                    zipDataStream.Close();
                }
                return JsonConvert.DeserializeObject<GameDatabase>(json);
            }

            return null; // Database not found!
        }

        [JsonProperty(@"last_indexing_timestamp")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime IndexingTime { get; set; }

        [JsonProperty(@"filenames")]
        public Dictionary<int, string> NameTable { get; set; }

        [JsonProperty(@"paths")]
        public Dictionary<int, PathInstance> Paths { get; set; }

        [JsonProperty(@"fileinstances")]
        public Dictionary<int, List<FileInstance>> FileInstances { get; set; }

        /// <summary>
        /// Information about a single download file (file id)
        /// </summary>
        [JsonProperty(@"fileinfos")]
        public Dictionary<int, NMFileInfo> ModFileInfos { get; set; }

    }
}
