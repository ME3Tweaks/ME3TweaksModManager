using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MassEffectModManagerCore.modmanager.usercontrols.moddescinieditor.alternates;
using MassEffectModManagerCore.ui;

namespace MassEffectModManagerCore.modmanager.usercontrols.moddescinieditor
{
    /// <summary>
    /// Editor control for a multilist
    /// </summary>
    public partial class SingleMultilistEditorControl : UserControl, INotifyPropertyChanged
    {

        /// <summary>
        /// The owner of this single editor
        /// </summary>
        //public MultilistEditorControl Owner
        //{
        //    get => (MultilistEditorControl)GetValue(OwnerProperty);
        //    set
        //    {
        //        SetValue(OwnerProperty, value);
        //        if (Owner != null)
        //        {
        //            //Owner.
        //        }
        //    }
        //}

        //public static readonly DependencyProperty OwnerProperty = DependencyProperty.Register("Owner", typeof(string), typeof(SingleMultilistEditorControl));
        /// <summary>
        /// The list index (as in moddesc.ini) of this multilist
        /// </summary>
        public int ListIndex { get; set; }
        public SingleMultilistEditorControl()
        {
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {
            AddFileCommand = new GenericCommand(AddFile);
        }

        private void AddFile()
        {
            if (!MultilistItems.Any() || !string.IsNullOrWhiteSpace(MultilistItems.Last().Value))
            {
                MultilistItems.Add(new SingleMultilistEditorItem()
                {
                    ItemIndex = MultilistItems.Count + 1 // we use 1 based UI indexing
                });
            }
        }

        public GenericCommand AddFileCommand { get; set; }

        public ObservableCollectionExtended<SingleMultilistEditorItem> MultilistItems { get; } = new ObservableCollectionExtended<SingleMultilistEditorItem>();

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class SingleMultilistEditorItem
    {
        // The index of the file in the list
        public int ItemIndex { get; set; }
        // The value of the multilist item
        public string Value { get; set; }
    }
}
