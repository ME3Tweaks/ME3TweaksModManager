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
    public class Loadout : IUnrealSerializable, INotifyPropertyChanged
    {
        #region Fields
        private string _AssaultRifle;
        private string _Shotgun;
        private string _SniperRifle;
        private string _SubmachineGun;
        private string _Pistol;
        private string _HeavyWeapon;
        #endregion

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this._AssaultRifle);
            stream.Serialize(ref this._Shotgun);
            stream.Serialize(ref this._SniperRifle);
            stream.Serialize(ref this._SubmachineGun);
            stream.Serialize(ref this._Pistol);
            stream.Serialize(ref this._HeavyWeapon);
        }

        #region Properties
        public string AssaultRifle
        {
            get { return this._AssaultRifle; }
            set
            {
                if (value != this._AssaultRifle)
                {
                    this._AssaultRifle = value;
                    this.NotifyPropertyChanged("AssaultRifle");
                }
            }
        }

        public string Shotgun
        {
            get { return this._Shotgun; }
            set
            {
                if (value != this._Shotgun)
                {
                    this._Shotgun = value;
                    this.NotifyPropertyChanged("Shotgun");
                }
            }
        }

        public string SniperRifle
        {
            get { return this._SniperRifle; }
            set
            {
                if (value != this._SniperRifle)
                {
                    this._SniperRifle = value;
                    this.NotifyPropertyChanged("SniperRifle");
                }
            }
        }

        public string SubmachineGun
        {
            get { return this._SubmachineGun; }
            set
            {
                if (value != this._SubmachineGun)
                {
                    this._SubmachineGun = value;
                    this.NotifyPropertyChanged("SubmachineGun");
                }
            }
        }

        public string Pistol
        {
            get { return this._Pistol; }
            set
            {
                if (value != this._Pistol)
                {
                    this._Pistol = value;
                    this.NotifyPropertyChanged("Pistol");
                }
            }
        }

        public string HeavyWeapon
        {
            get { return this._HeavyWeapon; }
            set
            {
                if (value != this._HeavyWeapon)
                {
                    this._HeavyWeapon = value;
                    this.NotifyPropertyChanged("HeavyWeapon");
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
