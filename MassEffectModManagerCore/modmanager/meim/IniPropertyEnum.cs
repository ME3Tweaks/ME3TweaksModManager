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
using MassEffectModManagerCore.modmanager.gameini;

namespace MassEffectIniModder.classes
{
    public class IniPropertyEnum : IniPropertyMaster
    {
        public List<IniPropertyEnumValue> Choices { get; set; }
        public IniPropertyEnum()
        {

        }

        private int _selectedIndex = 0;
        public int CurrentSelectedIndex
        {
            get
            {
                return _selectedIndex;
            }
            set
            {
                if (_selectedIndex != value)
                {
                    _selectedIndex = value;
                    this.OnPropertyChanged("CurrentSelectedIndex");
                    this.OnPropertyChanged("ResetEnabled");
                    this.OnPropertyChanged("DefaultText");
                    this.OnPropertyChanged("Notes");
                }
            }
        }

        private string _originalNotes;
        public override string Notes
        {
            get
            {
                return Choices[_selectedIndex].Notes ?? _originalNotes;
            }
            set
            {
                _originalNotes = value;
            }
        }

        public override bool ResetEnabled
        {
            get
            {
                return CurrentSelectedIndex != 0;
            }
        }

        public override string ValueToWrite
        {
            get { return Choices[CurrentSelectedIndex].IniValue; }
        }

        public override void LoadCurrentValue(DuplicatingIni configIni)
        {
            var entry = configIni.GetValue(SectionName,PropertyName);
            int index = -1;
            bool indexFound = false;
            if (entry != null)
            {
                foreach (IniPropertyEnumValue enumval in Choices)
                {
                    index++;
                    if (enumval.IniValue.Equals(entry.Value, StringComparison.InvariantCultureIgnoreCase))
                    {
                        indexFound = true;
                        break;
                    }
                }

                if (!indexFound)
                {
                    //user has their own item
                    IniPropertyEnumValue useritem = new IniPropertyEnumValue();
                    useritem.FriendlyName = useritem.IniValue = entry.Value;
                    Choices.Add(useritem);
                    CurrentSelectedIndex = Choices.Count - 1;
                }
                else
                {
                    CurrentSelectedIndex = index;
                }
            }
        }

        public override void Reset()
        {
            if (CurrentSelectedIndex != 0)
            {
                CurrentSelectedIndex = 0;
            }
        }

        internal override string Validate(string columnName)
        {
            return null; //no possible bad enum as UI code prevents this
        }
    }
}
