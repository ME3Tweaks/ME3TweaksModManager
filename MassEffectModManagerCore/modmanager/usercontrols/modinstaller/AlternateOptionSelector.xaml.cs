using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public bool IsDropdownOpen { get; set; }
        public AlternateOptionSelector()
        {
            InitializeComponent();
        }

        private void AlternateItem_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var newItem = (AlternateOption)((FrameworkElement)sender).DataContext;
            if (DataContext is AlternateGroup group && group.SelectedOption != newItem)
            {
                group.SelectedOption = newItem;
                IsDropdownOpen = false;
            }
        }
    }
}
