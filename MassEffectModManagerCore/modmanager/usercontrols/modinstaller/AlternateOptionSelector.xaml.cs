using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
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
using System.Xml.Linq;
using LegendaryExplorerCore.Helpers;
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
        /// If tooltips should not be shown. This is real hack because tooltip is a real PITA.
        /// </summary>
        public bool SuppressingTooltip { get; set; }

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
                group.TrySelectOption(newItem);
            }
        }

        /// <summary>
        /// Called only by the 'OtherOptions' items.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NotSelectedAlternateItem_MouseUp(object sender, MouseButtonEventArgs e)
        {
            SuppressTooltip();
            AlternateItem_MouseUp(sender, e);
        }


        // Fix for mouse wheel scrolling
        private void HandleMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            // This forces scrolling to bubble up
            // cause expander eats it
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
                eventArg.RoutedEvent = UIElement.MouseWheelEvent;
                eventArg.Source = sender;
                var parent = (((Control)sender).TemplatedParent ?? ((Control)sender).Parent) as UIElement;
                parent.RaiseEvent(eventArg);
            }
        }

        private void AlternateItemCheckbox_Click(object sender, RoutedEventArgs e)
        {
            var element = (FrameworkElement)sender;
            if (DataContext is AlternateGroup group)
            {
                group.TrySelectOption(group.AlternateOptions[0]);
                if (element?.ToolTip is ToolTip tp)
                    tp.IsOpen = false; // Close the tooltip
            }
        }

        /// <summary>
        /// Indicates that we should not open tooltips for the next few milliseconds to suppress some very odd behavior in ToolTip
        /// </summary>
        private void SuppressTooltip()
        {
            SuppressingTooltip = true;
            Task.Run(() =>
            {
                Thread.Sleep(30); // We really need just 1 rendered frame.
            }).ContinueWithOnUIThread(task =>
            {
                SuppressingTooltip = false;
            });
        }
    }
}
