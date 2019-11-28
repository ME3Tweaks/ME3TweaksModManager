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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace MassEffectIniModder.classes
{
    public class IniPropertyInt : IniPropertyMaster
    {
        public IniPropertyInt()
        {

        }

        internal override string Validate(string columnName)
        {
            if (columnName == "CurrentValue")
            {
                // Validate property and return a string if there is an error
                if (string.IsNullOrEmpty(CurrentValue))
                    return "Value cannot be blank";
                int f;
                if (!int.TryParse(CurrentValue, out f))
                {
                    return "Value must be a integer";
                }
            }

            // If there's no error, null gets returned
            return null;
        }
    }
}
