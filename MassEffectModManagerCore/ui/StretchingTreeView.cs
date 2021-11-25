using System.Windows;
using System.Windows.Controls;

namespace MassEffectModManagerCore.ui
{
    public class StretchingTreeView : TreeView
    {
        protected override DependencyObject GetContainerForItemOverride()
        {
            return new StretchingTreeViewItem();
        }

        protected override bool IsItemItsOwnContainerOverride(object item)
        {
            return item is StretchingTreeViewItem;
        }

        public StretchingTreeView()
        {
            this.SetResourceReference(StyleProperty, typeof(TreeView));
        }
    }

    public class StretchingTreeViewItem : TreeViewItem
    {
        public StretchingTreeViewItem()
        {
            this.Loaded += StretchingTreeViewItem_Loaded;
        }

        private void StretchingTreeViewItem_Loaded(object sender, RoutedEventArgs e)
        {
            // The purpose of this code is to stretch the Header Content all the way accross the TreeView. 
            if (VisualChildrenCount > 0)
            {
                if (GetVisualChild(0) is Grid grid && grid.ColumnDefinitions.Count == 3)
                {
                    // Remove the middle column which is set to Auto and let it get replaced with the 
                    // last column that is set to Star.
                    grid.ColumnDefinitions.RemoveAt(1);
                }
            }
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new StretchingTreeViewItem();
        }

        protected override bool IsItemItsOwnContainerOverride(object item)
        {
            return item is StretchingTreeViewItem;
        }
    }
}
