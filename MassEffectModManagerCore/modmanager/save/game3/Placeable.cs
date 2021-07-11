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
    [OriginalName("PlaceableSaveRecord")]
    public class Placeable : IUnrealSerializable, INotifyPropertyChanged
    {
        #region Fields
        [OriginalName("PlaceableGUID")]
        private Guid _Guid;

        [OriginalName("IsDestroyed")]
        private byte _IsDestroyed;

        [OriginalName("IsDeactivated")]
        private byte _IsDeactivated;
        #endregion

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this._Guid);
            stream.Serialize(ref this._IsDestroyed);
            stream.Serialize(ref this._IsDeactivated);
        }

        #region Properties
        public Guid Guid
        {
            get { return this._Guid; }
            set
            {
                if (value != this._Guid)
                {
                    this._Guid = value;
                    this.NotifyPropertyChanged("Guid");
                }
            }
        }

        public bool IsDestroyed
        {
            get { return this._IsDestroyed != 0; }
            set
            {
                if (value != (this._IsDestroyed != 0))
                {
                    this._IsDestroyed = value == true ? (byte)1 : (byte)0;
                    this.NotifyPropertyChanged("IsDestroyed");
                }
            }
        }

        public bool IsDeactivated
        {
            get { return this._IsDeactivated != 0; }
            set
            {
                if (value != (this._IsDeactivated != 0))
                {
                    this._IsDeactivated = value == true ? (byte)1 : (byte)0;
                    this.NotifyPropertyChanged("IsDeactivated");
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
