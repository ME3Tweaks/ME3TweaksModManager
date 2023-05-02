using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Xml;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCore.Diagnostics;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Services;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using Microsoft.AppCenter.Crashes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ME3TweaksModManager.modmanager.me3tweaks.services
{
    /// <summary>
    /// Service that powers the dynamic help system in the Help menu of ME3Tweaks Mod Manager.
    /// </summary>
    [Localizable(false)]
    public class DynamicHelpService
    {
        /// <summary>
        /// The xml string blob used for parsing
        /// </summary>
        private static XmlDocument Database;

        /// <summary>
        /// If the database has been initialized by a data source (cached or online)
        /// </summary>
        public static bool ServiceLoaded { get; set; }

        /// <summary>
        /// The name of the service for logging (templated)
        /// </summary>
        private const string ServiceLoggingName = @"DynamicHelp Service";

        private static string GetServiceCacheFile() => M3Filesystem.GetDynamicHelpCachedFile();

        public static bool LoadService(JToken data)
        {
            return InternalLoadService(data);
        }

        private static bool InternalLoadService(JToken serviceData)
        {
            // Online first
            if (serviceData != null)
            {
                try
                {
                    // This is an xml service!
                    var xml = serviceData.ToObject<string>();
                    Database = new XmlDocument();
                    Database.LoadXml(xml);
                    ServiceLoaded = true;
                    File.WriteAllText(GetServiceCacheFile(), xml);
                    M3Log.Information($@"Loaded online {ServiceLoggingName}");
                    return true;
                }
                catch (Exception ex)
                {
                    if (ServiceLoaded)
                    {
                        M3Log.Error($@"Loaded online {ServiceLoggingName}, but failed to cache to disk: {ex.Message}");
                        return true;
                    }
                    else
                    {
                        M3Log.Error($@"Failed to load {ServiceLoggingName}: {ex.Message}");
                        return false;
                    }

                }
            }

            // Use cached if online is not available
            if (File.Exists(GetServiceCacheFile()))
            {
                try
                {
                    var cached = File.ReadAllText(GetServiceCacheFile());
                    Database = new XmlDocument();
                    Database.LoadXml(cached);
                    ServiceLoaded = true;
                    M3Log.Information($@"Loaded cached {ServiceLoggingName}");
                    return true;
                }
                catch (Exception e)
                {
                    M3Log.Error($@"Failed to load cached {ServiceLoggingName}: {e.Message}");
                    var relevantInfo = new Dictionary<string, string>()
                    {
                        {@"Error type", @"Error reading cached online content"},
                        {@"Service", ServiceLoggingName},
                        {@"Message", e.Message}
                    };
                    TelemetryInterposer.UploadErrorLog(e, relevantInfo);
                }
            }

            M3Log.Information($@"Unable to load {ServiceLoggingName}: No cached content or online content was available to load");
            return false;
        }

        public static IReadOnlyList<SortableHelpElement> GetHelpItems(string language)
        {
            if (!ServiceLoaded || Database == null) return new List<SortableHelpElement>(0); // Nothing.

            //We can't use LINQ here as we have to multiselect
            //And parse things in a certain order
            try
            {
                var nonLocalizedHelpItems = new List<NonLocalizedHelpMenu>();

                // Get non-localized help blocks first
                string nlxpathExpression = $"/localizations/nonlocalizedhelpmenudefinition";
                var nlnodes = Database.SelectNodes(nlxpathExpression);
                foreach (XmlNode nlnode in nlnodes)
                {
                    var nlhm = new NonLocalizedHelpMenu() { Name = nlnode.Attributes["name"].Value };

                    // PARSE CHILDREN
                    foreach (XmlNode node in nlnode.ChildNodes)
                    {
                        var title = node.Attributes.GetNamedItem("title") ?? node.Attributes.GetNamedItem("localizedtitle");
                        if (title != null)
                        {
                            var helpItem = new SortableHelpElement(node, null);
                            if (helpItem.InheritsNLLocalizedTitle)
                                nlhm.LocalizedTitleNode = helpItem;
                            nlhm.Children.Add(helpItem);
                        }
                    }

                    nonLocalizedHelpItems.Add(nlhm);
                }

                //Get top level items, in order
                var sortableHelpItems = new List<SortableHelpElement>();
                string xpathExpression = $"/localizations/helpmenu[@lang='{language}']/helpitem|/localizations/helpmenu[@lang='{language}']/list|/localizations/helpmenu[@lang='{language}']/nonlocalizedhelpmenu";

                var nodes = Database.SelectNodes(xpathExpression);
                foreach (XmlNode node in nodes)
                {
                    if (node.Name == "nonlocalizedhelpmenu")
                    {
                        addNonLocalizedHelpMenuItems(sortableHelpItems, node, nonLocalizedHelpItems);
                    }
                    else
                    {
                        var title = node.Attributes.GetNamedItem("title");
                        if (title != null)
                        {
                            var helpItem = new SortableHelpElement(node, nonLocalizedHelpItems);
                            sortableHelpItems.Add(helpItem);
                        }
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

        /// <summary>
        /// Helper method to add items to a list
        /// </summary>
        /// <param name="sortableHelpItems"></param>
        /// <param name="node"></param>
        /// <param name="nonLocalizedHelpItems"></param>
        internal static void addNonLocalizedHelpMenuItems(List<SortableHelpElement> sortableHelpItems, XmlNode node, List<NonLocalizedHelpMenu> nonLocalizedHelpItems)
        {
            var id = node.Attributes["name"].Value;
            var localizedtitle = node.Attributes["title"]?.Value;
            var nlhmToUse = nonLocalizedHelpItems.FirstOrDefault(x => x.Name == id);
            if (nlhmToUse != null && localizedtitle != null)
            {
                nlhmToUse.LocalizedTitleNode.Title = localizedtitle;
            }

            foreach (var v in nlhmToUse.Children)
            {
                sortableHelpItems.Add(v);
            }
        }
    }

    /// <summary>
    /// Class representing a non-localized help item
    /// </summary>
    public class NonLocalizedHelpMenu
    {
        /// <summary>
        /// The ID of this non-localized help menu
        /// </summary>
        public string Name;

        /// <summary>
        /// The children in this non-localized help block.
        /// </summary>
        internal List<SortableHelpElement> Children = new List<SortableHelpElement>();

        /// <summary>
        /// The node that should set the title as defined in the non-localized help menu definition xml
        /// </summary>
        public SortableHelpElement LocalizedTitleNode;
    }

    /// <summary>
    /// Class representing a sortable dynamic help item
    /// </summary>
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
        internal string FontAwesomeIconResource;
        internal int ResourceSize;
        internal bool InheritsNLLocalizedTitle;
        internal List<SortableHelpElement> Children = new List<SortableHelpElement>();
        public SortableHelpElement(XmlNode source, List<NonLocalizedHelpMenu> nonLocalizedHelpMenus)
        {
            this.priority = source.Attributes.GetNamedItem("sort")?.InnerText;
            this.Title = source.Attributes.GetNamedItem("title")?.InnerText;
            this.ToolTip = source.Attributes.GetNamedItem("tooltip")?.InnerText;
            this.URL = source.Attributes.GetNamedItem("url")?.InnerText;
            this.FontAwesomeIconResource = source.Attributes.GetNamedItem("icon")?.InnerText;
            if (bool.TryParse(source.Attributes.GetNamedItem("localizedtitle")?.InnerText, out var ilt))
            {
                this.InheritsNLLocalizedTitle = ilt;
            }

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
                var localFile = Path.Combine(M3Filesystem.GetLocalHelpResourcesDirectory(), ResourceName);
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

                            var md5OfDownloadedFile = MUtilities.CalculateHash(localFile);
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
                    if (v.Name == "nonlocalizedhelpmenu")
                    {
                        DynamicHelpService.addNonLocalizedHelpMenuItems(Children, v, nonLocalizedHelpMenus);
                    }
                    else
                    {
                        Children.Add(new SortableHelpElement(v, nonLocalizedHelpMenus));
                    }
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
