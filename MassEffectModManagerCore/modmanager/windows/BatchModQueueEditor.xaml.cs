using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MassEffectModManagerCore.modmanager.usercontrols;

namespace MassEffectModManagerCore.modmanager.windows
{
    /// <summary>
    /// Interaction logic for BatchModQueueEditor.xaml
    /// </summary>
    public partial class BatchModQueueEditor : Window
    {
        public BatchModQueueEditor(Window owner = null, BatchLibraryInstallQueue queueToEdit = null)
        {
            Owner = owner;
            InitializeComponent();
        }
    }
}
