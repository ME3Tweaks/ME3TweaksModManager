using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using static MassEffectModManagerCore.modmanager.usercontrols.ASIManagerPanel;

namespace MassEffectModManagerCore.modmanager.asi
{
    public class ASIDisplayTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ASITemplate { get; set; }
        public DataTemplate NonManifestASITemplate { get; set; }

        public ASIDisplayTemplateSelector()
        {
            Debug.WriteLine("init");
        }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is ASIMod)
                return ASITemplate;
            if (item is InstalledASIMod)
                return NonManifestASITemplate;

            return null;
        }
    }
}
