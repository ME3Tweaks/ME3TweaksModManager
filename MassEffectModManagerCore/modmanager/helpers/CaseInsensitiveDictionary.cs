using System;
using System.Collections.Generic;

namespace MassEffectModManagerCore.modmanager.helpers
{
    class CaseInsensitiveDictionary<V> : Dictionary<string, V>
    {
        public CaseInsensitiveDictionary() : base(StringComparer.OrdinalIgnoreCase)
        {
        }
    }
}
