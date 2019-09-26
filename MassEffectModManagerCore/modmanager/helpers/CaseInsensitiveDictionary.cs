using System;
using System.Collections.Generic;
using System.Text;

namespace MassEffectModManagerCore.modmanager.helpers
{
    class CaseInsensitiveDictionary<V> : Dictionary<string, V>
    {
        public CaseInsensitiveDictionary() : base(StringComparer.OrdinalIgnoreCase)
        {
        }
    }
}
