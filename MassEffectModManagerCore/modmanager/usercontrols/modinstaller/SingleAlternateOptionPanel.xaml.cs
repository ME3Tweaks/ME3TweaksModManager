using ME3TweaksModManager.modmanager.objects;
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

namespace ME3TweaksModManager.modmanager.usercontrols.modinstaller
{
    /// <summary>
    /// Interaction logic for SingleAlternateOptionPanel.xaml
    /// </summary>
    public partial class SingleAlternateOptionPanel : UserControl
    {

        /// <summary>
        /// Option that is being displayed by this UI object
        /// </summary>
        public AlternateOption Option
        {
            get => (AlternateOption)GetValue(OptionProperty);
            set => SetValue(OptionProperty, value);
        }

        public static readonly DependencyProperty OptionProperty = DependencyProperty.Register(@"Option", typeof(AlternateOption), typeof(SingleAlternateOptionPanel));


        public SingleAlternateOptionPanel()
        {
            InitializeComponent();
        }

        private void SAOP_MouseLeave(object sender, MouseEventArgs e)
        {
            if (ToolTip is ToolTip tp)
                tp.IsOpen = false; // Close the image preview.
        }
    }
}
