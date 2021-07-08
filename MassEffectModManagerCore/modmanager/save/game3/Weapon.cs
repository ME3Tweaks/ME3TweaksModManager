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

using System.ComponentModel;
using MassEffectModManagerCore.modmanager.save.game2.FileFormats;

namespace MassEffectModManagerCore.modmanager.save.game3
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [OriginalName("WeaponSaveRecord")]
    public class Weapon : IUnrealSerializable, INotifyPropertyChanged
    {
        #region Fields
        [OriginalName("WeaponClassName")]
        private string _ClassName;

        [OriginalName("AmmoUsedCount")]
        private int _AmmoUsedCount;

        [OriginalName("TotalAmmo")]
        private int _AmmoTotal;

        [OriginalName("bCurrentWeapon")]
        private bool _CurrentWeapon;

        [OriginalName("bLastWeapon")]
        private bool _WasLastWeapon;

        [OriginalName("AmmoPowerName")]
        private string _AmmoPowerName;

        [OriginalName("AmmoPowerSourceTag")]
        private string _AmmoPowerSourceTag;
        #endregion

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this._ClassName);
            stream.Serialize(ref this._AmmoUsedCount);
            stream.Serialize(ref this._AmmoTotal);
            stream.Serialize(ref this._CurrentWeapon);
            stream.Serialize(ref this._WasLastWeapon);
            stream.Serialize(ref this._AmmoPowerName, s => s.Version < 17, () => null);
            stream.Serialize(ref this._AmmoPowerSourceTag, s => s.Version < 59, () => null);
        }

        public override string ToString()
        {
            return this._ClassName;
        }

        #region Properties
        public string ClassName
        {
            get { return this._ClassName; }
            set
            {
                if (value != this._ClassName)
                {
                    this._ClassName = value;
                    this.NotifyPropertyChanged("ClassName");
                }
            }
        }

        public int AmmoUsedCount
        {
            get { return this._AmmoUsedCount; }
            set
            {
                if (value != this._AmmoUsedCount)
                {
                    this._AmmoUsedCount = value;
                    this.NotifyPropertyChanged("AmmoUsedCount");
                }
            }
        }

        public int AmmoTotal
        {
            get { return this._AmmoTotal; }
            set
            {
                if (value != this._AmmoTotal)
                {
                    this._AmmoTotal = value;
                    this.NotifyPropertyChanged("AmmoTotal");
                }
            }
        }

        public bool IsCurrentWeapon
        {
            get { return this._CurrentWeapon; }
            set
            {
                if (value != this._CurrentWeapon)
                {
                    this._CurrentWeapon = value;
                    this.NotifyPropertyChanged("IsCurrentWeapon");
                }
            }
        }

        public bool WasLastWeapon
        {
            get { return this._WasLastWeapon; }
            set
            {
                if (value != this._WasLastWeapon)
                {
                    this._WasLastWeapon = value;
                    this.NotifyPropertyChanged("WasLastWeapon");
                }
            }
        }

        public string AmmoPowerName
        {
            get { return this._AmmoPowerName; }
            set
            {
                if (value != this._AmmoPowerName)
                {
                    this._AmmoPowerName = value;
                    this.NotifyPropertyChanged("AmmoPowerName");
                }
            }
        }

        public string AmmoPowerSourceTag
        {
            get { return this._AmmoPowerSourceTag; }
            set
            {
                if (value != this._AmmoPowerSourceTag)
                {
                    this._AmmoPowerSourceTag = value;
                    this.NotifyPropertyChanged("AmmoPowerSourceTag");
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
