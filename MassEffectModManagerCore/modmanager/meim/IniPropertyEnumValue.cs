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

namespace MassEffectModManagerCore.modmanager.meim
{
    public class IniPropertyEnumValue
    {
        private string _friendlyName;
        public string FriendlyName
        {
            get
            {
                return (_friendlyName != null && _friendlyName != "") ? _friendlyName :  IniValue;
            }
            set
            {
                _friendlyName = value;
            }
        }
        public string IniValue { get; set; }
        public string Notes { get; internal set; }
    }
}
