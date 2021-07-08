/* Copyright (c) 2012 Rick (rick 'at' gibbed 'dot' us)
 * 
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 * 
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 * 
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.ComponentModel;
using MassEffectModManagerCore.modmanager.save.game2.FileFormats;

namespace MassEffectModManagerCore.modmanager.save.game3
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [OriginalName("DependentDLCRecord")]
    public class DependentDLC : IUnrealSerializable, INotifyPropertyChanged
    {
        #region Fields
        [OriginalName("ModuleID")]
        private int _ModuleId;

        [OriginalName("Name")]
        private string _Name;

        [OriginalName("CanonicalName")]
        private string _CanonicalName;
        #endregion

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this._ModuleId);
            stream.Serialize(ref this._Name);
            stream.Serialize(ref this._CanonicalName, s => s.Version < 50, () => null);
        }

        public override string ToString()
        {
            return String.Format("{1} ({0})",
                                 this._ModuleId,
                                 this._Name);
        }

        #region Properties

        public int ModuleId
        {
            get { return this._ModuleId; }
            set
            {
                if (value != this._ModuleId)
                {
                    this._ModuleId = value;
                    this.NotifyPropertyChanged("ModuleId");
                }
            }
        }

        public string Name
        {
            get { return this._Name; }
            set
            {
                if (value != this._Name)
                {
                    this._Name = value;
                    this.NotifyPropertyChanged("Name");
                }
            }
        }

        public string CanonicalName
        {
            get { return this._CanonicalName; }
            set
            {
                if (value != this._CanonicalName)
                {
                    this._CanonicalName = value;
                    this.NotifyPropertyChanged("CanonicalName");
                }
            }
        }
        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
