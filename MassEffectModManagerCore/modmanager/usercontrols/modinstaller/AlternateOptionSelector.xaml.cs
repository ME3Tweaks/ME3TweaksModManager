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
using ME3TweaksModManager.modmanager.objects.alternates;
using PropertyChanged;
using Xceed.Wpf.Toolkit;

namespace ME3TweaksModManager.modmanager.usercontrols.modinstaller
{
    /// <summary>
    /// Control that allows for selection of an alternate. Non-grouped alternates are displayed using a simple checkbox; grouped alternates will display a dropdown
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class AlternateOptionSelector : UserControl
    {
        /// <summary>
        /// If the dropdown is currently open. This is set to always true if there is only one option, to enable the mouseover effect
        /// </summary>
        public bool IsDropdownOpen { get; set; }

        /// <summary>
        /// If there should be a mouseover highlight event on the main control
        /// </summary>
        public bool ShowMouseoverHighlightOnMain
        {
            get
            {
                if (DataContext is AlternateGroup group && group.GroupName != null)
                {
                    // Only show if dropdown is open when showing in group mode
                    return IsDropdownOpen;
                }

                // Always show
                return true;
            }
        }

        /// <summary>
        /// The last time the dropdown was closed. This is so if you click on the dropdown while it is open, it won't open until a few ms later to prevent re-opening as it is mousedown vs mouseup.
        /// </summary>
        private DateTime LastCloseTime;

        public AlternateOptionSelector()
        {
            InitializeComponent();
        }

        private void AlternateItem_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var element = (FrameworkElement)sender;
            var newItem = (AlternateOption)element.DataContext;
            if (DataContext is AlternateGroup group)
            {
                if (group.SelectedOption != newItem)
                {
                    group.SelectedOption = newItem;
                    IsDropdownOpen = false; // Multi mode
                    if (element.ToolTip is ToolTip tp)
                        tp.IsOpen = false; // Close the tooltip
                }

                if (group.AlternateOptions.Count == 1 && !group.SelectedOption.IsAlways)
                {
                    // Single mode
                    group.SelectedOption.isUserSelecting = true;
                    group.SelectedOption.IsSelected = !group.SelectedOption.IsSelected;
                    group.SelectedOption.isUserSelecting = false;

                }
            }
        }

        public void OnIsDropdownOpenChanged()
        {
            Debug.WriteLine($"Now {IsDropdownOpen}");
        }

        private void DropdownButton_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsDropdownOpen)
                LastCloseTime = DateTime.Now;
            //if (IsDropdownOpen)
            // e.Handled = true;
        }

        private void DropdownButton_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            //if (!IsDropdownOpen)
            //{
            //    var timeDelta = DateTime.Now - LastCloseTime;
            //    if (timeDelta < new TimeSpan(0, 0, 0, 0, 200))
            //    {
            //        e.Handled = true;
            //        if (sender is DropDownButton obj)
            //        {
            //            // This prevents it from capturing the next mouse input event
            //            obj.ReleaseMouseCapture();
            //        }
            //        Debug.WriteLine("Suppressed re-click on dropdown");
            //    }

            //    Debug.WriteLine($"Time Delta: {timeDelta.Milliseconds}");
            //    //if (IsDropdownOpen)
            //    // e.Handled = true;
            //}
        }
    }
}
