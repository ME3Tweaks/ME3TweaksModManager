using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocalizationHelper;
using PropertyChanged;

namespace LocalizationHelper
{
    [AddINotifyPropertyChangedInterface]
    public class LocalizationLanguage
    {
        public string ShortName { get; init; }
        public string FullName { get; init; }
        public bool Selected { get; set; }
        public bool AlwaysSelected { get; set; }

        public bool Contains(LocalizationTablesUI.LocalizedString ls, string searchTerm)
        {
            return false;
        }
    }
}
