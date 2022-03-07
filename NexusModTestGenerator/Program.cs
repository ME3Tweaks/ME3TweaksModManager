using Newtonsoft.Json;
using NexusModTestGenerator;
using Pathoschild.FluentNexus;
using Pathoschild.FluentNexus.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using TestArchiveGenerator;

public class Program
{
    /// <summary>
    /// Path where temporary data is stored
    /// </summary>
    private static string TempPath;

    /// <summary>
    /// API Key for NexusMods
    /// </summary>
    private static string NexusAPIKey;

    /// <summary>
    /// Where downloads are downloaded to
    /// </summary>
    private static string DownloadTempFolder;

    /// <summary>
    /// Where a generated archive is stored to
    /// </summary>
    private static string OutputPath;

    /// <summary>
    /// Where downloads are staged before compression
    /// </summary>
    private static string StagingTempFolder;

    /// <summary>
    /// The list of files that have already been processed (according to the indexer status)
    /// </summary>
    private static Dictionary<string, List<int>> ProcessedFiles = new()
    {
        { @"masseffect", new List<int>() },
        { @"masseffect2", new List<int>() },
        { @"masseffect3", new List<int>() },
        { @"masseffectlegendaryedition", new List<int>() },
    };

    /// <summary>
    /// The mapping of domains => mod ids => file ids of that mods main files 
    /// </summary>
    private static Dictionary<string, Dictionary<int, int[]>> Mainfiles = new();

    /// <summary>
    /// Domains to scan
    /// </summary>
    private static string[] domains = { @"masseffect", @"masseffect2", @"masseffect3", @"masseffectlegendaryedition" };

    /// <summary>
    /// Location where the Nexus API cache is stored from the NexusIndexer program
    /// </summary>
    private static string NexusAPICachePath;

    /// <summary>
    /// Client for Nexus API
    /// </summary>
    private static NexusClient PathosClient;

    /// <summary>
    /// The number of downloads per session
    /// </summary>
    private static int NumDownloadsRemaining = 30000;

    /// <summary>
    /// Location of the indexer file
    /// </summary>
    private static string IndexerPath;

    public static void Main(string[] args)
    {
        ParseArguments(args);
        CheckArguments();
        PathosClient = new NexusClient(NexusAPIKey, "ME3Tweaks - Test Case Archive Generator", "1.0.0");

        foreach (var domain in domains)
        {
            if (NumDownloadsRemaining <= 0)
                continue;

            var cachePath = Path.Combine(NexusAPICachePath, domain);
            var folders = Directory.GetDirectories(cachePath);

            foreach (var folder in folders)
            {
                if (NumDownloadsRemaining <= 0)
                    continue;

                var modId = int.Parse(Path.GetFileName(folder));

                var modInfoFile = Path.Combine(folder, @"modinfo.json");
                var modInfo = JsonConvert.DeserializeObject<Mod>(File.ReadAllText(modInfoFile));

                var fileIndex = Path.Combine(folder, @"files.json");
                var modFileList = JsonConvert.DeserializeObject<ModFileList>(File.ReadAllText(fileIndex));
                var mainFiles = modFileList.Files.Where(x => x.Category == FileCategory.Main).ToList();

                // Files that are not in the download list 
                var fileIdsToDownload = mainFiles.Select(x => x.FileID).Except(ProcessedFiles[domain]).Select(x => mainFiles.Find(y => y.FileID == x)).ToList();
                var storageBaseName = $"{domainPrefix(domain)}-{SanitizeName(modInfo.Name)}";

                foreach (var v in fileIdsToDownload)
                {
                    if (NumDownloadsRemaining <= 0)
                        continue;

                    // Determine if the file listing contains a moddesc.ini
                    var fileListing = Path.Combine(folder, $"{v.FileID}.json");
                    if (File.Exists(fileListing))
                    {
                        var contentPreview = JsonConvert.DeserializeObject<ContentPreview>(File.ReadAllText(fileListing));
                        if (contentPreview.Children != null && DownloadHelper.HasModdescIni(contentPreview)
                            && v.SizeInBytes != null && v.SizeInBytes < (1024 * 1024 * 400) // < 400MB
                            )
                        {
                            var storageName = storageBaseName;
                            if (mainFiles.Count > 1)
                            {
                                storageName += $@"-{SanitizeName(v.Name)}";
                            }
                            storageName += v.FileVersion;
                            //Console.WriteLine($"File needs downloaded: {v.Name}");
                            DownloadMod(storageName, Path.GetExtension(v.FileName), domain, modId, v.FileID);
                            NumDownloadsRemaining--;
                        }
                        else
                        {
                            Debug.WriteLine($"Skipping {v.Name}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"No content preview, skipping {fileListing}");
                    }
                }
            }
        }
    }

    private static string domainPrefix(string domain)
    {
        if (domain == @"masseffect") return "me1";
        if (domain == @"masseffect2") return "me2";
        if (domain == @"masseffect3") return "me3";
        if (domain == @"masseffectlegendaryedition") return "le";
        return null;
    }

    private static object SanitizeName(string name)
    {
        return name.ToLower()
            .Replace("(", "")
            .Replace(")", "")
            .Replace(" ", "")
            .Replace("'", "")
            .Replace("&#39;", "")
            ;
    }

    private static void CheckArguments()
    {

    }

    /// <summary>
    /// Argument parser
    /// </summary>
    /// <param name="args"></param>
    public static void ParseArguments(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            string argument = args[i];
            switch (argument)
            {
                case "--nexusapikeyfilepath":
                    if (args.Length < i + 1) NotEnoughArguments(argument);
                    NexusAPIKey = File.ReadAllText(args[i + 1]);
                    i++;
                    break;
                case "--indexerpath":
                    if (args.Length < i + 1) NotEnoughArguments(argument);
                    IndexerPath = args[i + 1];
                    // File must already exist. If you don't have one just make a blank file at this path.
                    if (File.Exists(IndexerPath))
                    {
                        ProcessedFiles = JsonConvert.DeserializeObject<Dictionary<string, List<int>>>(File.ReadAllText(IndexerPath));
                    }
                    i++;
                    break;
                case "--nexusapicachepath":
                    if (args.Length < i + 1) NotEnoughArguments(argument);
                    NexusAPICachePath = args[i + 1];
                    i++;
                    break;
                case "--testarchiveoutputpath":
                    if (args.Length < i + 1) NotEnoughArguments(argument);
                    OutputPath = args[i + 1];
                    i++;
                    break;
                case "--temppath":
                    if (args.Length < i + 1) NotEnoughArguments(argument);
                    TempPath = args[i + 1];
                    if (Directory.Exists(TempPath))
                    {
                        TestArchiveGeneratorProgram.DeleteFilesAndFoldersRecursively(TempPath);
                    }
                    Directory.CreateDirectory(TempPath);
                    DownloadTempFolder = Directory.CreateDirectory(Path.Combine(TempPath, @"ModDownload")).FullName;
                    StagingTempFolder = Directory.CreateDirectory(Path.Combine(TempPath, @"Staging")).FullName;
                    i++;
                    break;
            }
        }
    }

    private static void NotEnoughArguments(string argument)
    {
        Console.WriteLine("Missing argument for option: " + argument);
        Environment.Exit(1);
    }


    public static void DownloadMod(string storageBaseName, string extension, string domain, int modId, int fileId)
    {
        var downloadPath = Path.Combine(DownloadTempFolder, $"{storageBaseName}{extension}");
        Console.WriteLine($"Downloading {downloadPath}");
        //File.Create(downloadPath); // TEST
        //return; // Do not actually download DEBUG

        var dlLink = PathosClient.ModFiles.GetDownloadLinks(domain, modId, fileId).Result.FirstOrDefault().Uri;


        using (var progress = new ProgressBar())
        {
            DownloadHelper.DownloadFile(dlLink, downloadPath, (x, y) =>
            {
                if (y != 0)
                {
                    progress.Report((double)x / y);
                }
            });
        }
        TestArchiveGeneratorProgram.CreateBlankArchive(downloadPath, OutputPath, StagingTempFolder);
        File.Delete(downloadPath);

        // Update the indexer.
        ProcessedFiles[domain].Add(fileId);
        WriteIndexer();

        Console.WriteLine("Done.");
    }

    private static void WriteIndexer()
    {
        File.WriteAllText(IndexerPath, JsonConvert.SerializeObject(ProcessedFiles));
    }
}