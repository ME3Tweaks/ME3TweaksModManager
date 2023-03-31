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

namespace ME3TweaksModManager.modmanager.usercontrols.moddescinieditor
{
    /// <summary>
    /// Interaction logic for TexturesEditorControl.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class TexturesEditorControl : ModdescEditorControlBase
    {
        public TexturesEditorControl()
        {
            InitializeComponent();
        }

        public override void OnLoaded(object sender, RoutedEventArgs e)
        {
            
        }

        public override void Serialize(IniData ini)
        {

        }
    }
}
