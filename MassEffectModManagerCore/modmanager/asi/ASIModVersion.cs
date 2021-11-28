//using System;
//using System.Collections.Generic;
//using System.ComponentModel;
//using System.Text;
//using System.Windows.Media;
//using ME3TweaksModManager.modmanager.localizations;
//using LegendaryExplorerCore.Packages;
//using PropertyChanged;

//namespace ME3TweaksModManager.modmanager.asi
//{
//    /// <summary>
//    /// Object containing information about a single version of an ASI mod in the ASI mod manifest
//    /// </summary>
//    [AddINotifyPropertyChangedInterface]
//    public class ASIModVersion
//    {
//        /// <summary>
//        /// The direct download link to the ASI
//        /// </summary>
//        public string DownloadLink { get; internal set; }
//        /// <summary>
//        /// The link to the source code of the ASI
//        /// </summary>c3
//        public string SourceCodeLink { get; internal set; }

//        /// <summary>
//        /// MD5 of the ASI
//        /// </summary>
//        public string Hash { get; internal set; }
//        /// <summary>
//        /// Version of this ASI mod
//        /// </summary>
//        public string Version { get; internal set; }
//        /// <summary>
//        /// Developer of the ASI
//        /// </summary>
//        public string Author { get; internal set; }
//        /// <summary>
//        /// The preferred prefix for the ASI, used when installing
//        /// </summary>
//        public string InstalledPrefix { get; internal set; }
//        /// <summary>
//        /// The displayable name of the ASI
//        /// </summary>
//        public string Name { get; internal set; }
//        /// <summary>
//        /// The game this ASI is applicable to
//        /// </summary>
//        public MEGame Game { get; set; }
//        /// <summary>
//        /// The description of this ASI
//        /// </summary>
//        public string Description { get; internal set; }
//        /// <summary>
//        /// The owning mod of this version
//        /// </summary>
//        public ASIMod OwningMod { get; set; }

//        /// <summary>
//        /// List of other groups to delete on install. This is for ASIs that have multiple differing update groups (like autoload)
//        /// </summary>
//        public List<int> OtherGroupsToDeleteOnInstall = new();

//        /// <summary>
//        /// Internal hack to generate the OtherGroupsToDeleteOnInstall variable from text.
//        /// </summary>
//        public string _otherGroupsToDeleteOnInstallInternal
//        {
//            set
//            {
//                if (!string.IsNullOrWhiteSpace(value))
//                {
//                    OtherGroupsToDeleteOnInstall.Clear();
//                    var split = value.Split(',');
//                    foreach (var v in split)
//                    {
//                        if (int.TryParse(v, out var asigroupid))
//                        {
//                            OtherGroupsToDeleteOnInstall.Add(asigroupid);
//                        }
//                    }
//                }
//            }
//        }


//    }
//}
