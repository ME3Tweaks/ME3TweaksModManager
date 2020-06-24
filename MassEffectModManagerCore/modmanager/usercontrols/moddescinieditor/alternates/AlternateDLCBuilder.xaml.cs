using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;

namespace MassEffectModManagerCore.modmanager.usercontrols.moddescinieditor.alternates
{
    /// <summary>
    /// Interaction logic for AlternateDLCBuilder.xaml
    /// </summary>
    public partial class AlternateDLCBuilder : UserControl, INotifyPropertyChanged
    {
        public Mod EditingMod { get; set; }
        public ModJob CustomDLCJob { get; set; }
        public AlternateDLCBuilder()
        {
            DataContext = this;
            InitializeComponent();
        }

        public void SetEditingMod(Mod m)
        {
            EditingMod = m;
            CustomDLCJob = m.GetJob(ModJob.JobHeader.CUSTOMDLC);
        }

        public ObservableCollectionExtended<AlternateDLC> AlternateDLCs { get; } = new ObservableCollectionExtended<AlternateDLC>();
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
