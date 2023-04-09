using System.Windows;
using LegendaryExplorerCore.Misc;

namespace ME3TweaksModManager.modmanager.windows.input
{
    [AddINotifyPropertyChangedInterface]
    public partial class DropdownPromptDialog : Window
    {
        public ObservableCollectionExtended<object> Items { get; } = new();
        public object SelectedItem { get; set; }
        public string DisplayString { get; }
        public string Watermark { get; }

        public DropdownPromptDialog(string question, string title, string watermark, IEnumerable<object> items, Window owner)
        {
            Owner = owner;
            InitializeComponent();
            Title = title;
            DisplayString = question;
            Watermark = watermark;
            Items.AddRange(items);
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}