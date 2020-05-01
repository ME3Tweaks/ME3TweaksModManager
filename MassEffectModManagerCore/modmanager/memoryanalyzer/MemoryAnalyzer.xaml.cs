using ByteSizeLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.ui;
using ME3Explorer;
using PropertyChanged;

namespace MassEffectModManagerCore.modmanager.memoryanalyzer
{
    /// <summary>
    /// Interaction logic for MemoryAnalyzer.xaml
    /// </summary>
    [Localizable(false)]
    public partial class MemoryAnalyzer : Window, INotifyPropertyChanged
    {

        #region Static Reference Adding
        private static readonly List<MemoryAnalyzerObject> TrackedMemoryObjects = new List<MemoryAnalyzerObject>();

        //All calls to this method will be removed in release builds
        [Conditional("DEBUG")]
        public static void AddTrackedMemoryItem(string objectname, WeakReference reference)
        {
            //Force concurrency
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TrackedMemoryObjects.Add(new MemoryAnalyzerObject(objectname, reference));
                });
            }
        }

        #endregion

        public ObservableCollectionExtended<MemoryAnalyzerObject> InstancedTrackedMemoryObjects { get; set; } = new ObservableCollectionExtended<MemoryAnalyzerObject>();

        readonly DispatcherTimer dispatcherTimer;

        public event PropertyChangedEventHandler PropertyChanged;

        public MemoryAnalyzer()
        {
            AddTrackedMemoryItem(@"Memory Analyzer", new WeakReference(this));

            DataContext = this;
            Refresh();
            LoadCommands();
            InitializeComponent();

            //  DispatcherTimer setup
            dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += automatedRefresh_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 3);
            dispatcherTimer.Start();
        }

        public ICommand AttemptResetMemoryManagerCommand { get; set; }
        private void LoadCommands()
        {
            AttemptResetMemoryManagerCommand =
                new GenericCommand(
                    () =>
                    {
                        MixinHandler.AttemptResetMemoryManager();

                    },
                () => true
                    );

        }


        public string LastRefreshText { get; set; }
        public string CurrentMemoryUsageText { get; set; }

        private void automatedRefresh_Tick(object sender, EventArgs e)
        {
            Refresh();
        }

        private void ForceLargeGC_Click(object sender, RoutedEventArgs e)
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
        }

        private void ForceGC_Click(object sender, RoutedEventArgs e)
        {
            GC.Collect();
        }

        public string LargeInUseStr { get; set; }
        public string LargeFreeStr { get; set; }
        public string SmallInUseStr { get; set; }
        public string SmallFreeStr { get; set; }
        public string MemoryBlockSize { get; set; }
        public string MaxBufferSize { get; set; }
        private void Refresh()
        {
            TrackedMemoryObjects.Where(x => !x.IsAlive()).ToList().ForEach(x => x.RemainingLifetimeAfterGC--);
            TrackedMemoryObjects.RemoveAll(x => !x.IsAlive() && x.RemainingLifetimeAfterGC < 0);
            InstancedTrackedMemoryObjects.ReplaceAll(TrackedMemoryObjects);
            LastRefreshText = "Last refreshed: " + DateTime.Now;
            CurrentMemoryUsageText = "Current process allocation: " + ByteSize.FromBytes(System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64);

            LargeInUseStr = ByteSize.FromBytes(MixinHandler.MixinMemoryStreamManager.LargePoolInUseSize).MebiBytes.ToString();
            LargeFreeStr = ByteSize.FromBytes(MixinHandler.MixinMemoryStreamManager.LargePoolFreeSize).MebiBytes.ToString();
            SmallInUseStr = ByteSize.FromBytes(MixinHandler.MixinMemoryStreamManager.SmallPoolInUseSize).MebiBytes.ToString();
            SmallFreeStr = ByteSize.FromBytes(MixinHandler.MixinMemoryStreamManager.SmallPoolFreeSize).MebiBytes.ToString();
            MaxBufferSize = ByteSize.FromBytes(MixinHandler.MixinMemoryStreamManager.MaximumBufferSize).MebiBytes.ToString();
            MemoryBlockSize = ByteSize.FromBytes(MixinHandler.MixinMemoryStreamManager.BlockSize).MebiBytes.ToString();
            //foreach (var item in InstancedTrackedMemoryObjects)
            //{
            //    item.RefreshStatus();
            //}
        }

        private void CleanUpOldRefs_Click(object sender, RoutedEventArgs e)
        {
            TrackedMemoryObjects.RemoveAll(x => !x.IsAlive());
            InstancedTrackedMemoryObjects.ReplaceAll(TrackedMemoryObjects);
        }


        public class MemoryAnalyzerObject : INotifyPropertyChanged
        {
            private readonly WeakReference Reference;
            public string AllocationTime { get; }
            public System.Windows.Media.Brush DrawColor
            {
                get
                {
                    if (RemainingLifetimeAfterGC < 5)
                    {
                        //Fadeout
                        return new SolidColorBrush(Color.FromArgb((byte)(128 + (RemainingLifetimeAfterGC * 25)), 0, 0, 0));
                    }
                    else
                    {
                        return Brushes.Black;
                    }
                }
            }
            public int RemainingLifetimeAfterGC = 10;

            public event PropertyChangedEventHandler PropertyChanged;

            public string ReferenceName { get; set; }

            public string ReferenceStatus
            {
                get
                {
                    if (Reference.IsAlive)
                    {
                        if (Reference.Target is FrameworkElement w)
                        {
                            return w.IsLoaded ? "In Memory, Open" : "In Memory, Closed";
                        }
                        return "In Memory";
                    }
                    else
                    {
                        return "Garbage Collected";
                    }
                }
            }

            public MemoryAnalyzerObject(string ReferenceName, WeakReference Reference)
            {
                AllocationTime = DateTime.Now.ToString();
                this.Reference = Reference;
                this.ReferenceName = ReferenceName;
            }

            //public void RefreshStatus()
            //{
            //    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(ReferenceStatus));
            //}

            public bool IsAlive()
            {
                return Reference.IsAlive;
            }
        }

        private void MemoryAnalyzer_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            dispatcherTimer.Stop();
        }
    }
}
