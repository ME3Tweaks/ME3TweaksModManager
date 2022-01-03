using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Xml;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCore.Diagnostics;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Services;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using Microsoft.AppCenter.Crashes;

namespace ME3TweaksModManager.modmanager.me3tweaks
{
    [Localizable(false)]
    partial class M3OnlineContent
    {
        //private static readonly string LatestHelpFileLink = StaticFilesBaseURL_Github + "dynamichelp/latesthelp-localized.xml";
        //internal static readonly string HelpResourcesBaseURL = StaticFilesBaseURL_Github + "dynamichelp/resources";

        public static List<SortableHelpElement> FetchLatestHelp(string language, bool preferLocal, bool overrideThrottling = false)
        {
            var localHelpExists = File.Exists(M3Utilities.GetLocalHelpFile());
            string cached = null;
            if (localHelpExists)
            {
                try
                {
                    cached = File.ReadAllText(M3Utilities.GetLocalHelpFile());
                }
                catch (Exception e)
                {
                    var attachments = new List<ErrorAttachmentLog>();
                    string log = LogCollector.CollectLatestLog(MCoreFilesystem.GetLogDir(), true);
                    if (log.Length < FileSize.MebiByte * 7)
                    {
                        attachments.Add(ErrorAttachmentLog.AttachmentWithText(log, "applog.txt"));
                    }
                    Crashes.TrackError(e, new Dictionary<string, string>()
                    {
                        {"Error type", "Error reading cached online content" },
                        {"Service", "Dynamic Help" },
                        {"Message", e.Message }
                    }, attachments.ToArray());
                }
            }

            if (localHelpExists && preferLocal)
            {
                return ParseLocalHelp(cached, language);
            }


            if (!localHelpExists || overrideThrottling || MOnlineContent.CanFetchContentThrottleCheck())
            {
                foreach (var staticendpoint in M3OnlineContent.StaticFileBaseEndpoints.GetAllLinks())
                {
                    using var wc = new System.Net.WebClient();
                    try
                    {
                        string xml = wc.DownloadStringAwareOfEncoding(staticendpoint + @"dynamichelp/latesthelp-localized.xml");
                        File.WriteAllText(M3Utilities.GetLocalHelpFile(), xml);
                        return ParseLocalHelp(xml, language);
                    }
                    catch (Exception e)
                    {
                        M3Log.Error($"Error fetching online help from endpoint {staticendpoint}: {e.Message}");
                    }
                }
                if (cached != null)
                {
                    M3Log.Warning("Using cached help instead");
                }
                else
                {
                    M3Log.Error("Unable to display dynamic help: Could not fetch online asset and cached help asset does not exist.");
                    return null;
                }
            }

            try
            {
                return ParseLocalHelp(cached, language);
            }
            catch (Exception e)
            {
                M3Log.Error("Unable to parse local dynamic help file: " + e.Message);
                return null;
            }
        }

        private static List<SortableHelpElement> ParseLocalHelp(string xml, string language)
        {
            //We can't use LINQ here as we have to multiselect
            //And parse things in a certain order
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);
                var sortableHelpItems = new List<SortableHelpElement>();
                //Get top level items, in order
                string xpathExpression = $"/localizations/helpmenu[@lang='{language}']/helpitem|/localizations/helpmenu[@lang='{language}']/list";

                var nodes = doc.SelectNodes(xpathExpression);
                foreach (XmlNode node in nodes)
                {
                    var title = node.Attributes.GetNamedItem("title");
                    if (title != null)
                    {
                        var helpItem = new SortableHelpElement(node);
                        sortableHelpItems.Add(helpItem);
                    }
                }
                sortableHelpItems.Sort();
                return sortableHelpItems;
            }
            catch (Exception e)
            {
                M3Log.Error("ERROR IN LOCAL HELP FILE: " + e.Message);
                return new List<SortableHelpElement>();
            }
        }
    }

    [Localizable(false)]
    public class SortableHelpElement : IComparable<SortableHelpElement>
    {
        private string priority;
        internal string Title;
        internal string ToolTip;
        internal string URL;
        internal string ModalTitle;
        internal string ModalIcon;
        internal string ModalText;
        internal string ResourceName;
        internal string ResourceMD5;
        internal int ResourceSize;
        internal List<SortableHelpElement> Children = new List<SortableHelpElement>();
        public SortableHelpElement(XmlNode source)
        {
            this.priority = source.Attributes.GetNamedItem("sort")?.InnerText;
            this.Title = source.Attributes.GetNamedItem("title")?.InnerText;
            this.ToolTip = source.Attributes.GetNamedItem("tooltip")?.InnerText;
            this.URL = source.Attributes.GetNamedItem("url")?.InnerText;

            //Modal help items
            this.ModalTitle = source.Attributes.GetNamedItem("modaltitle")?.InnerText;
            this.ModalIcon = source.Attributes.GetNamedItem("modalicon")?.InnerText;
            this.ModalText = source.InnerText;
            this.ResourceName = source.Attributes.GetNamedItem("resource")?.InnerText;
            this.ResourceMD5 = source.Attributes.GetNamedItem("md5")?.InnerText;

            string resourceSizeStr = source.Attributes.GetNamedItem("size")?.InnerText;
            if (resourceSizeStr != null)
            {
                this.ResourceSize = int.Parse(resourceSizeStr);
            }

            //Download resource if any
            if (ResourceName != null)
            {
                //Validate locally
                var localFile = Path.Combine(M3Utilities.GetLocalHelpResourcesDirectory(), ResourceName);
                if (!File.Exists(localFile))
                {
                    //Download
                    foreach (var staticendpoint in M3OnlineContent.StaticFileBaseEndpoints.GetAllLinks())
                    {
                        var fullURL = staticendpoint + "dynamichelp/resources/" + ResourceName;

                        try
                        {
                            using var wc = new System.Net.WebClient();

                            M3Log.Information("Downloading dynamic help image asset: " + fullURL);
                            wc.DownloadFile(fullURL, localFile);

                            var md5OfDownloadedFile = M3Utilities.CalculateMD5(localFile);
                            if (md5OfDownloadedFile != ResourceMD5)
                            {
                                M3Log.Error($"Downloaded asset has wrong hash. Expected: {ResourceMD5}, got: {md5OfDownloadedFile}");
                                File.Delete(localFile);
                            }
                            else
                            {
                                M3Log.Information("Downloaded resource passed md5 check");
                            }
                        }
                        catch (Exception e)
                        {
                            M3Log.Error($"Error downloading dynamic help asset from endpoint {fullURL}: {e.Message}");
                        }
                    }
                }
            }

            foreach (XmlNode v in source.ChildNodes)
            {
                if (v.NodeType == XmlNodeType.Element)
                {
                    Children.Add(new SortableHelpElement(v));
                }
            }
        }
        public int CompareTo(SortableHelpElement other)
        {
            return getPriorityValue(priority).CompareTo(getPriorityValue(other.priority));
        }

        private int getPriorityValue(string priority)
        {
            if (string.IsNullOrEmpty(priority))
                return 0;
            switch (priority)
            {
                case "low":
                    return 1;
                case "medium":
                    return 0;
                case "high":
                    return -1;
                default:
                    return 0;
            }
        }
    }
}
