/*=============================================
Copyright (c) 2018 ME3Tweaks
This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
=============================================*/

using System.Collections.Generic;

namespace MassEffectModManagerCore.modmanager.meim
{
    public class IniSection
    {
        public string SectionName { get; set; }
        public List<IniPropertyEnum> EnumProperties { get; set; }
        public List<IniPropertyBool> BoolProperties { get; set; }
        public List<IniPropertyInt> IntProperties { get; set; }
        public List<IniPropertyFloat> FloatProperties { get; set; }
        public List<IniPropertyName> NameProperties { get; set; }
        internal List<IniPropertyMaster> GetAllProperties()
        {
            List<IniPropertyMaster> all = new List<IniPropertyMaster>();
            all.AddRange(EnumProperties);
            all.AddRange(BoolProperties);
            all.AddRange(IntProperties);
            all.AddRange(FloatProperties);
            all.AddRange(NameProperties);
            return all;
        }

        internal void PropogateOwnership()
        {
            foreach (IniPropertyMaster prop in GetAllProperties())
            {
                prop.SectionName = SectionName; //do not cause circular reference
                prop.SectionFriendlyName = SectionName; //temp
            }
        }
    }
}
