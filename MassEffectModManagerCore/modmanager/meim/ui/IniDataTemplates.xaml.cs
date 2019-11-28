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
using MassEffectIniModder.classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MassEffectIniModder.ui
{
    public partial class IniDataTemplates : ResourceDictionary
    {
        public IniDataTemplates()
        {
            InitializeComponent();
        }

        public void Button_ResetToDefault_Click(object sender, EventArgs e)
        {
            ListViewItem lvi = GetParent((Button)sender);
            IniPropertyMaster property = (IniPropertyMaster)lvi.DataContext;
            if (property is IniPropertyBool)
            {
                var prop = property as IniPropertyBool;
                prop.CurrentSelectedBoolIndex = bool.Parse(prop.OriginalValue) ? 0 : 1;
            } else if (property is IniPropertyEnum)
            {
                var prop = property as IniPropertyEnum;
                prop.CurrentSelectedIndex = 0;
            } else if (property is IniPropertyInt || property is IniPropertyFloat)
            {
                property.CurrentValue = property.OriginalValue;
            }
        }

        private ListViewItem GetParent(Visual v)
        {
            while (v != null)
            {
                v = VisualTreeHelper.GetParent(v) as Visual;
                if (v is ListViewItem)
                    break;
            }
            return v as ListViewItem;
        }
    }
}
