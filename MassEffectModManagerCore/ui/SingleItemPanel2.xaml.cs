using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ME3TweaksModManager.modmanager.memoryanalyzer;
using ME3TweaksModManager.modmanager.usercontrols;

namespace ME3TweaksModManager.ui
{
    /// <summary>
    /// Interaction logic for SingleItemPanel2.xaml
    /// </summary>
    public partial class SingleItemPanel2 : ContentControl
    {
        public SingleItemPanel2(MMBusyPanelBase control)
        {
            MemoryAnalyzer.AddTrackedMemoryItem(@"SingleItemPanel", new WeakReference(this));
            //control.DataContext = control;
            Content = control;
            control.Loaded += SingleItemPanel2_OnLoaded;
            DataContext = this;
        }



        public void DetatchControl()
        {
            Content = null;
        }

        private void SingleItemPanel2_OnLoaded(object sender, RoutedEventArgs e)
        {
            InitializeComponent();
            if (Content is MMBusyPanelBase bp)
            {
                bp.Loaded -= SingleItemPanel2_OnLoaded;
            }
        }
    }
}
