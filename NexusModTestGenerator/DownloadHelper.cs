using Pathoschild.FluentNexus.Models;
using System.ComponentModel;
using System.Net;

namespace NexusModTestGenerator;

/// <summary>
/// Download helper for NexusModTestGenerator
/// </summary>
public class DownloadHelper
{

    /// <summary>
    /// Determines if the file listing has any moddesc.ini files in it.
    /// </summary>
    /// <param name="fileListing"></param>
    /// <returns></returns>
    public static bool HasModdescIni(ContentPreview fileListing)
    {
        foreach (var e in fileListing.Children)
        {
            if (HasModdescIniRecursive(e))
                return true;
        }

        return false;
    }

    private static bool HasModdescIniRecursive(ContentPreviewEntry entry)
    {
        // Directory
        if (entry.Type == ContentPreviewEntryType.Directory)
        {
            foreach (var e in entry.Children)
            {
                if (HasModdescIniRecursive(e))
                    return true;
            }

            return false;
        }

        // File
        return entry.Name == @"moddesc.ini";
    }


    /// <summary>
    /// Asynchronously downloads a file to disk, but blocks the calling thread until the download completes. This will allow you to subscribe to the progress notification
    /// </summary>
    /// <param name="uri">Download link</param>
    /// <param name="destination">Where to download the file to</param>
    /// <param name="progressChanged">Handler for progress change</param>
    public static void DownloadFile(Uri uri, string destination, Action<long, long> progressChanged = null)
    {
        void HandleDownloadComplete(object sender, AsyncCompletedEventArgs args)
        {
            lock (args.UserState)
            {
                //releases blocked thread
                Monitor.Pulse(args.UserState);
            }
        }


        void HandleDownloadProgress(object sender, DownloadProgressChangedEventArgs args)
        {
            //Process progress updates here
            progressChanged?.Invoke(args.BytesReceived, args.TotalBytesToReceive);
        }

        using (var wc = new WebClient())
        {
            wc.DownloadProgressChanged += HandleDownloadProgress;
            wc.DownloadFileCompleted += HandleDownloadComplete;
            var syncObject = new Object();
            lock (syncObject)
            {
                wc.DownloadFileAsync(uri, destination, syncObject);
                //This would block the thread until download completes
                Monitor.Wait(syncObject);
            }
        }
    }

    internal static bool HasFilesMatching(ContentPreview fileListing, Func<string, bool> p)
    {
        foreach (var e in fileListing.Children)
        {
            if (HasFileRecursive(e, p))
                return true;
        }

        return false;
    }

    private static bool HasFileRecursive(ContentPreviewEntry entry, Func<string, bool> predicate)
    {
        // Directory
        if (entry.Type == ContentPreviewEntryType.Directory)
        {
            foreach (var e in entry.Children)
            {
                if (HasFileRecursive(e, predicate))
                    return true;
            }

            return false;
        }

        // File
        return predicate(entry.Path);
    }
}
