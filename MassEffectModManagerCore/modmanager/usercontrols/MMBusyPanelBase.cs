using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ME3TweaksModManager.modmanager.memoryanalyzer;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.usercontrols.interfaces;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    public abstract class MMBusyPanelBase : UserControl, INotifyPropertyChanged, ISizeAdjustable
    {
        //Fody uses this property on weaving
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore

        protected MMBusyPanelBase()
        {
#if DEBUG
            var methodInfo = new StackTrace().GetFrame(1).GetMethod();
            var className = methodInfo.ReflectedType.Name;
            MemoryAnalyzer.AddTrackedMemoryItem($@"MMBPB: {className}", new WeakReference(this));

#endif
            Loaded += UserControl_Loaded;
            Unloaded += UserControl_Unloaded;
        }

        /// <summary>
        /// Reference to the containing window
        /// </summary>
        public Window window { get; set; }
        protected MainWindow mainwindow;
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            window = Window.GetWindow(this);
            mainwindow = window as MainWindow;
            window.KeyDown += HandleKeyPress;
            OnPanelVisible();
        }

        /// <summary>
        /// Result of the panel on close
        /// </summary>
        public PanelResult Result = new();

        public abstract void HandleKeyPress(object sender, KeyEventArgs e);
        public abstract void OnPanelVisible();

        public event EventHandler<DataEventArgs> Close;
        protected virtual void OnClosing(DataEventArgs e)
        {
            Close?.Invoke(this, e);

            // This is required to run as its changes IsPanelOpen's result, which is checked for some commands.
            DataContext = null;
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            window.KeyDown -= HandleKeyPress;
            window = null; //lose reference
            mainwindow = null; // lose reference
        }

        public void TriggerPropertyChangedFor(string propertyname)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }

        /// <summary>
        /// If the panel is still open. This can be used to prevent command from firing on closure.
        /// </summary>
        public bool IsPanelOpen => DataContext != null;

        public virtual double MaxWindowWidthPercent { get; set; }
        public virtual double MaxWindowHeightPercent { get; set; }

        /// <summary>
        /// Set to false to disable autosizing feature
        /// </summary>
        public virtual bool DisableM3AutoSizer { get; set; }
    }
}
