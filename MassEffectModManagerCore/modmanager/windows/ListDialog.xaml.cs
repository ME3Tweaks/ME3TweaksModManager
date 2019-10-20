using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;


namespace MassEffectModManagerCore.modmanager.windows
{
    /// <summary>
    /// Dialog that has copy button, designed for showing lists of short lines of text
    /// </summary>
    public partial class ListDialog : Window
    {
        List<string> items;
        public ListDialog(List<string> listItems, string title, string message, Window owner, int width = 0, int height = 0)
        {
            InitializeComponent();
            Title = title;
            ListDialog_Message.Text = message;
            items = listItems;
            if (width != 0)
            {
                Width = width;
            }
            if (height != 0)
            {
                Height = height;
            }
            foreach (string str in listItems)
            {
                ListDialog_List.Items.Add(str);
            }
            Owner = owner;
        }

        private void CopyItemsToClipBoard_Click(object sender, RoutedEventArgs e)
        {
            string toClipboard = string.Join("\n", items);
            try
            {
                Clipboard.SetText(toClipboard);
                ListDialog_Status.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                //yes, this actually happens sometimes...
                MessageBox.Show("Could not set data to clipboard:\n" + ex.Message);
            }
        }

        private void TextBlock_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 2)
            {
                string text = ((TextBlock)sender).Text;
                if (File.Exists(text))
                {
                    Utilities.HighlightInExplorer(text);
                }
            }
        }
    }
}
