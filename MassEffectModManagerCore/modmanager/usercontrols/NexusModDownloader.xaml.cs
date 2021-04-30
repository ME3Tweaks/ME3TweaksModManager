using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using System;
using System.Collections.Generic;
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

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for NexusModDownloader.xaml
    /// </summary>
    public partial class NexusModDownloader : MMBusyPanelBase
    {
        public ObservableCollectionExtended<ModDownload> Downloads { get; } = new ObservableCollectionExtended<ModDownload>();
        public NexusModDownloader()
        {
            InitializeComponent();
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {

        }

        public override void OnPanelVisible()
        {

        }

        public void AddDownload(string nxmLink)
        {
            var dl = new ModDownload(nxmLink);
            Downloads.Add(dl);
            dl.Initialize();
        }
    }
}
