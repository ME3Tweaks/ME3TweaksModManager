using System.Windows.Documents;
using Xceed.Wpf.Toolkit;

namespace ME3TweaksModManager.modmanager.converters
{
    internal class ModDescriptionFormatter : ITextFormatter
    {
        public string GetText(System.Windows.Documents.FlowDocument document)
        {
            return new TextRange(document.ContentStart, document.ContentEnd).Text;
        }

        public void SetText(System.Windows.Documents.FlowDocument document, string text)
        {
            new TextRange(document.ContentStart, document.ContentEnd).Text = text;
        }
    }
}
