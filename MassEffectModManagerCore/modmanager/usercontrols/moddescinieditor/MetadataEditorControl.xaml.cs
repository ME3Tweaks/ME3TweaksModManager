using System;
using System.Collections.Generic;
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
using Polly.NoOp;

namespace MassEffectModManagerCore.modmanager.usercontrols.moddescinieditor
{
    /// <summary>
    /// Interaction logic for MetadataEditorControl.xaml
    /// </summary>
    public partial class MetadataEditorControl : UserControl, INotifyPropertyChanged
    {
        public Mod EditingMod { get; set; }
        public MetadataEditorControl()
        {
            DataContext = this;
            InitializeComponent();
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
