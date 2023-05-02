using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ME3TweaksModManager.modmanager.memoryanalyzer;
using ME3TweaksModManager.modmanager.usercontrols;
using PropertyChanged;

namespace ME3TweaksModManager.ui
{
    /// <summary>
    /// Interaction logic for SingleItemPanel2.xaml
    /// </summary>
    public partial class SingleItemPanel2 : ContentControl, INotifyPropertyChanged
    {
        public SingleItemPanel2(MMBusyPanelBase control)
        {
            M3MemoryAnalyzer.AddTrackedMemoryItem(@"SingleItemPanel", this);
            //control.DataContext = control;
            Content = control;
            control.Loaded += SingleItemPanel2_OnLoaded;
            DataContext = this;
        }



        public void DetatchControl()
        {
            Content = null;
        }

        /// <summary>
        /// This is so we can raise a binding change. It has no other purpose.
        /// </summary>
        public object DummyProperty => null;

        private void SingleItemPanel2_OnLoaded(object sender, RoutedEventArgs e)
        {
            InitializeComponent();
            if (Content is MMBusyPanelBase bp)
            {
                bp.Loaded -= SingleItemPanel2_OnLoaded;
                
                // This is a hack to make it know how much to size the content...
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
                timer.Start();
                timer.Tick += (sender, args) =>
                {
                    timer.Stop();
                    TriggerResize();
                };
            }
        }

        /// <summary>
        /// Triggers a resizing binding update on the panel
        /// </summary>
        public void TriggerResize()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DummyProperty)));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
