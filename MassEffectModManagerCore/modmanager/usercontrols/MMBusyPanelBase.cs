using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCore.Diagnostics;
using ME3TweaksModManager.modmanager.memoryanalyzer;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.usercontrols.interfaces;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    public abstract class MMBusyPanelBase : UserControl, INotifyPropertyChanged, ISizeAdjustable
    {
        /// <summary>
        /// If app cleanup is in progress
        /// </summary>
        protected bool HandlingShutdownTasks { get; set; }

        //Fody uses this property on weaving
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore

        protected MMBusyPanelBase()
        {
#if DEBUG
            var methodInfo = new StackTrace().GetFrame(1).GetMethod();
            var className = methodInfo.ReflectedType.Name;
            M3MemoryAnalyzer.AddTrackedMemoryItem($@"MMBPB: {className}", this);

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
            
            // 01/16/2026: Observed deadlock behavior, so lock is removed
            // Lock removed - PopulateTargets() already locks this object internally
            // This prevents deadlock when PopulateTargets is running on background thread
            // and needs to marshal to UI thread while UI thread is blocked here
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
            M3Log.Information($@"Panel closing: {this.GetType().Name}");

            // This block is run on the UI thread as it might require UI interaction to release things
            Application.Current.Dispatcher.Invoke(() =>
            {
                Close?.Invoke(this, e);

                // This is required to run as its changes IsPanelOpen's result, which is checked for some commands.
                DataContext = null;
            });

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

        /// <summary>
        /// Triggers the resize event for the hosting SingleItemPanel2
        /// </summary>
        protected void TriggerResize()
        {
            if (Parent is SingleItemPanel2 sip2)
            {
                sip2.TriggerResize();
            }
        }

        /// <summary>
        /// Triggers a resize after a 1ms delay, which should make it resize on the next frame.
        /// </summary>
        protected void TriggerResizeNextFrame()
        {
            Task.Run(() =>
            {
                Thread.Sleep(1);
            }).ContinueWithOnUIThread(x =>
            {
                TriggerResize();
            });
        }

        /// <summary>
        /// When window is closing, does this panel support immediate closure? Some like installers should probably not
        /// </summary>
        /// <returns></returns>
        public virtual bool CanBeForceClosed()
        {
            return true;
        }

        /// <summary>
        /// Called when the application has a close request. Panels can override this to custom their shutdown behavior.
        /// </summary>
        public virtual void SignalAppClosing()
        {
            HandlingShutdownTasks = true;
        }

        public virtual double MaxWindowWidthPercent { get; set; }
        public virtual double MaxWindowHeightPercent { get; set; }

        /// <summary>
        /// Set to true to disable autosizing feature
        /// </summary>
        public virtual bool DisableM3AutoSizer { get; set; }
    }
}
