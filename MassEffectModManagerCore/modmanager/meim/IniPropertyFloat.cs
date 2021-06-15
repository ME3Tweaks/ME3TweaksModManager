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

using System.ComponentModel;
using LegendaryExplorerCore.Misc;

namespace MassEffectModManagerCore.modmanager.meim
{
    [Localizable(false)]
    public class IniPropertyFloat : IniPropertyMaster
    {
        public override void LoadCurrentValue(DuplicatingIni configIni)
        {
            base.LoadCurrentValue(configIni);
            CurrentValue = CurrentValue.TrimEnd('f');
            CurrentValue = CurrentValue.Contains(".") ? CurrentValue.TrimEnd('0').TrimEnd('.') : CurrentValue;
        }

        internal override string Validate(string columnName)
        {
            if (columnName == "CurrentValue")
            {
                // Validate property and return a string if there is an error
                if (string.IsNullOrEmpty(CurrentValue))
                    return "Value cannot be blank";
                float f;
                if (!float.TryParse(CurrentValue, out f))
                {
                    return "Value must be a floating point number";
                }
            }

            // If there's no error, null gets returned
            return null;
        }

    }
}
