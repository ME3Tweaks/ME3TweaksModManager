using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Gammtek.Conduit.Extensions.Collections.Generic;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.ui;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ModUpdateInformation.xaml
    /// </summary>
    public partial class ModUpdateInformation : UserControl, INotifyPropertyChanged
    {
        public event EventHandler<EventArgs> Close;
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnClosing(EventArgs e)
        {
            EventHandler<EventArgs> handler = Close;
            handler?.Invoke(this, e);
        }

        public ObservableCollection<OnlineContent.ModUpdateInfo> UpdatableMods { get; } = new ObservableCollection<OnlineContent.ModUpdateInfo>();

        public ModUpdateInformation(List<OnlineContent.ModUpdateInfo> modsWithUpdates)
        {
            DataContext = this;
            UpdatableMods.AddRange(modsWithUpdates);
            LoadCommands();
            InitializeComponent();
        }

        public ICommand CloseCommand { get; set; }
        private bool TaskRunning;
        private bool TaskNotRunning() => !TaskRunning;
        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(CloseDialog, TaskNotRunning);
        }

        private void CloseDialog()
        {
            OnClosing(EventArgs.Empty);
        }
    }
}
