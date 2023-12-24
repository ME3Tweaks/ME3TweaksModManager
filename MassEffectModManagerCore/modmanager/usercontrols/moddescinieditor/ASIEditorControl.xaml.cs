using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using IniParser.Model;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.NativeMods;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.objects.mod.moddesc;

namespace ME3TweaksModManager.modmanager.usercontrols.moddescinieditor
{
    /// <summary>
    /// Interaction logic for ASIEditorControl.xaml
    /// </summary>
    public partial class ASIEditorControl : ModdescEditorControlBase
    {
        public ObservableCollectionExtended<ASIModVersionEditor> ASIMods { get; } = new();
        public ASIEditorControl()
        {
            InitializeComponent();
        }



        public override void OnLoaded(object sender, RoutedEventArgs e)
        {
            foreach (var m in EditingMod.ASIModsToInstall)
            {
                var info = ASIModVersionEditor.Create(EditingMod.Game, m);
                if (info != null)
                {
                    ASIMods.Add(info);
                }
            }
        }

        public override void Serialize(IniData ini)
        {
            List<string> structs = new List<string>();
            foreach (var asiMod in ASIMods)
            {
                structs.Add(asiMod.GenerateStruct());
            }

            ini[Mod.MODDESC_HEADERKEY_ASIMODS][Mod.MODDESC_DESCRIPTOR_ASI_ASIMODSTOINSTALL] = $@"({string.Join(',', structs)})"; 
        }


        public class ASIModVersionEditor : ASIModVersion
        {
            // public M3ASIVersion M3Base { get; set; }
            public ASIModVersion ManifestMod { get; set; }

            /// <summary>
            /// The version set by the user
            /// </summary>
            public int? Version { get; set; }

            public static ASIModVersionEditor Create(MEGame game, M3ASIVersion baseObj)
            {
                ASIModVersionEditor v = new ASIModVersionEditor();

                var asiModsForGame = ASIManager.GetASIModsByGame(game);
                var group = asiModsForGame.FirstOrDefault(x => x.UpdateGroupId == baseObj.ASIGroupID);
                if (group == null)
                {
                    M3Log.Error($@"Unable to find ASI group {baseObj.ASIGroupID}");
                    return null; // Not found!
                }

                if (baseObj.Version != null)
                {
                    v.ManifestMod = group.Versions.FirstOrDefault(x => x.Version == baseObj.Version);
                }
                else
                {
                    v.ManifestMod = group.LatestVersion;
                }

                if (v.ManifestMod == null)
                {
                    M3Log.Error($@"Unable to find version {baseObj.Version?.ToString() ?? @"(Latest)"} in ASI group {baseObj.ASIGroupID} {group.Versions.First().Name}");
                    return null; // Specific version was not found!!
                }

                return v;
            }

            public string GenerateStruct()
            {
                var data = new Dictionary<string, string>();
                data[M3ASIVersion.GROUP_KEY_NAME] = ManifestMod.OwningMod.UpdateGroupId.ToString();
                if (Version != null)
                {
                    data[M3ASIVersion.VERSION_KEY_NAME] = ManifestMod.Version.ToString();
                }
                return $@"({StringStructParser.BuildCommaSeparatedSplitValueList(data)})";
            }
        }
    }
}
