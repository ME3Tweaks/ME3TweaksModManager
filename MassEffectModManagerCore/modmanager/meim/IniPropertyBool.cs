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
using System.ComponentModel;
using LegendaryExplorerCore.Misc;

namespace ME3TweaksModManager.modmanager.meim
{
    [Localizable(false)]
    public class IniPropertyBool : IniPropertyMaster
    {
        private int _selectedIndex = -1;
        private bool _originalBoolValue = true;
        private string _originalValue = null;
        public int CurrentSelectedBoolIndex
        {
            get
            {
                if (_selectedIndex != -1)
                {
                    return _selectedIndex;
                }
                else
                {
                    return bool.Parse(OriginalValue) ? 0 : 1;
                }
            }
            set
            {
                if (_selectedIndex != value)
                {
                    _selectedIndex = value;
                    this.OnPropertyChanged("CurrentSelectedBoolIndex");
                    this.OnPropertyChanged("ResetEnabled");
                    this.OnPropertyChanged("DefaultText");
                }
            }
        }

        public override string OriginalValue
        {
            get { return _originalValue; }
            set
            {
                _originalBoolValue = bool.Parse(value);
                _originalValue = value;
            }
        }

        public override string ValueToWrite => CurrentSelectedBoolIndex == 0 ? @"TRUE" : @"FALSE";


        public override bool ResetEnabled
        {
            get
            {
                return CurrentSelectedBoolIndex != (_originalBoolValue ? 0 : 1);
            }
        }

        public override void LoadCurrentValue(DuplicatingIni configIni)
        {
            base.LoadCurrentValue(configIni);
            try
            {
                if (CurrentValue != "")
                {
                    CurrentSelectedBoolIndex = bool.Parse(CurrentValue) ? 0 : 1;
                }
                else
                {
                    CurrentSelectedBoolIndex = bool.Parse(OriginalValue) ? 0 : 1;
                }
            }
            catch (Exception)
            {
                //error parsing current value
                Notes = @"Error parsing current bool value: " + CurrentValue;
                CurrentSelectedBoolIndex = bool.Parse(OriginalValue) ? 0 : 1;
            }
        }

        public override void Reset()
        {
            if (CurrentSelectedBoolIndex != (_originalBoolValue ? 0 : 1))
            {
                CurrentSelectedBoolIndex = _originalBoolValue ? 0 : 1;
            }
        }

        internal override string Validate(string columnName)
        {
            return null; //no possible bad boolean
        }
    }
}
