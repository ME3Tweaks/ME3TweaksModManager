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

using System.Collections.Generic;
using System.ComponentModel;
using ME3TweaksModManager.modmanager.save.game2.FileFormats;

namespace ME3TweaksModManager.modmanager.save.game3
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [OriginalName("HenchmanSaveRecord")]
    public class Henchman : IUnrealSerializable, INotifyPropertyChanged
    {
        #region Fields
        [OriginalName("Tag")]
        private string _Tag;

        [OriginalName("Powers")]
        private List<Power> _Powers = new List<Power>();

        [OriginalName("CharacterLevel")]
        private int _CharacterLevel;

        [OriginalName("TalentPoints")]
        private int _TalentPoints;

        [OriginalName("LoadoutWeapons")]
        private Loadout _LoadoutWeapons = new Loadout();

        [OriginalName("MappedPower")]
        private string _MappedPower;

        [OriginalName("WeaponMods")]
        private List<WeaponMod> _WeaponMods = new List<WeaponMod>();

        [OriginalName("Grenades")]
        private int _Grenades;

        [OriginalName("Weapons")]
        private List<Weapon> _Weapons = new List<Weapon>();
        #endregion

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this._Tag);
            stream.Serialize(ref this._Powers);
            stream.Serialize(ref this._CharacterLevel);
            stream.Serialize(ref this._TalentPoints);
            stream.Serialize(ref this._LoadoutWeapons, s => s.Version < 23, () => new Loadout());
            stream.Serialize(ref this._MappedPower, s => s.Version < 29, () => null);
            stream.Serialize(ref this._WeaponMods, s => s.Version < 45, () => new List<WeaponMod>());
            stream.Serialize(ref this._Grenades, s => s.Version < 59, () => 0);
            stream.Serialize(ref this._Weapons, s => s.Version < 59, () => new List<Weapon>());
        }

        #region Properties
        public string Tag
        {
            get { return this._Tag; }
            set
            {
                if (value != this._Tag)
                {
                    this._Tag = value;
                    this.NotifyPropertyChanged("Tag");
                }
            }
        }

        public List<Power> Powers
        {
            get { return this._Powers; }
            set
            {
                if (value != this._Powers)
                {
                    this._Powers = value;
                    this.NotifyPropertyChanged("Powers");
                }
            }
        }

        public int CharacterLevel
        {
            get { return this._CharacterLevel; }
            set
            {
                if (value != this._CharacterLevel)
                {
                    this._CharacterLevel = value;
                    this.NotifyPropertyChanged("CharacterLevel");
                }
            }
        }

        public int TalentPoints
        {
            get { return this._TalentPoints; }
            set
            {
                if (value != this._TalentPoints)
                {
                    this._TalentPoints = value;
                    this.NotifyPropertyChanged("TalentPoints");
                }
            }
        }

        public Loadout LoadoutWeapons
        {
            get { return this._LoadoutWeapons; }
            set
            {
                if (value != this._LoadoutWeapons)
                {
                    this._LoadoutWeapons = value;
                    this.NotifyPropertyChanged("LoadoutWeapons");
                }
            }
        }

        public string MappedPower
        {
            get { return this._MappedPower; }
            set
            {
                if (value != this._MappedPower)
                {
                    this._MappedPower = value;
                    this.NotifyPropertyChanged("MappedPower");
                }
            }
        }

        public List<WeaponMod> WeaponMods
        {
            get { return this._WeaponMods; }
            set
            {
                if (value != this._WeaponMods)
                {
                    this._WeaponMods = value;
                    this.NotifyPropertyChanged("WeaponMods");
                }
            }
        }

        public int Grenades
        {
            get { return this._Grenades; }
            set
            {
                if (value != this._Grenades)
                {
                    this._Grenades = value;
                    this.NotifyPropertyChanged("Grenades");
                }
            }
        }

        public List<Weapon> Weapons
        {
            get { return this._Weapons; }
            set
            {
                if (value != this._Weapons)
                {
                    this._Weapons = value;
                    this.NotifyPropertyChanged("Weapons");
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
