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
using ME3TweaksModManager.modmanager.objects;
using PropertyChanged;

namespace ME3TweaksModManager.modmanager.usercontrols.modinstaller
{
    /// <summary>
    /// Control that allows for selection of an alternate. Non-grouped alternates are displayed using a simple checkbox; grouped alternates will display a dropdown
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class AlternateOptionSelector : UserControl
    {
        /// <summary>
        /// The group containing the options that this selector can present. If the group has only one item, it is converted to a checkbox. The group will be the datacontext
        /// </summary>
        //private AlternateGroup Group => DataContext as AlternateGroup;

        public AlternateOptionSelector()
        {
            InitializeComponent();
        }

        private void AlternateOptionSelector_OnLoaded(object sender, RoutedEventArgs e)
        {
        }
    }
}
