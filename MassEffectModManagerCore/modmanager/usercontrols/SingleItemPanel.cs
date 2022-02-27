using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ME3TweaksModManager.modmanager.memoryanalyzer;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// A cheap workaround for the memory leak caused by adding new controls to MMBusyPanelBase. Remove the subcontrol of this to force it to dispose of the actual content we care about.
    /// </summary>
    internal class SingleItemPanel : ContentControl
    {
        public SingleItemPanel(FrameworkElement control)
        {
            MemoryAnalyzer.AddTrackedMemoryItem(@"SingleItemPanel", new WeakReference(this));
            //control.DataContext = control;
            //DataContext = control;
            Content = control;
        }

        public void DetatchControl()
        {
            Content = null;
        }
    }
}
