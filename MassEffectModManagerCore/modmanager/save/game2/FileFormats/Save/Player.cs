using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace MassEffectModManagerCore.modmanager.save.game2.FileFormats.Save
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public partial class Player : IUnrealSerializable, IPlayerRecord
    {
        [UnrealFieldOffset(0x000)]
        [UnrealFieldCategory("Player")]
        [UnrealFieldDisplayName("Is Female")]
        public bool IsFemale;
        public bool Proxy_IsFemale
        {
            get => IsFemale;
            set => IsFemale = value;
        }

        [UnrealFieldOffset(0x004)]
        [UnrealFieldCategory("Statistics")]
        [UnrealFieldDisplayName("Class Name")]
        public string PlayerClassName;

        [UnrealFieldOffset(0x018)]
        [UnrealFieldCategory("Statistics")]
        [UnrealFieldDisplayName("Level")]
        public int Level;

        [UnrealFieldOffset(0x01C)]
        [UnrealFieldCategory("Statistics")]
        [UnrealFieldDisplayName("Current XP")]
        public float CurrentXP;

        [UnrealFieldOffset(0x020)]
        [UnrealFieldCategory("Player")]
        [UnrealFieldDisplayName("First Name")]
        public string FirstName;
        public string Proxy_FirstName
        {
            get => FirstName;
            set => FirstName = value;
        }

        public void SetMorphHead(IMorphHead morphHead)
        {
            if (morphHead is MorphHead head)
            {
                Appearance.MorphHead = head;
                Appearance.HasMorphHead = true;
            }
            else
            {
                Appearance.MorphHead = null;
                Appearance.HasMorphHead = false;
            }
        }

        [UnrealFieldOffset(0x02C)]
        [UnrealFieldCategory("Player")]
        [UnrealFieldDisplayName("Last Name")]
        [UnrealFieldDescription("String ID of last name. Not actually used.")]
        public int LastName;

        [UnrealFieldOffset(0x030)]
        [UnrealFieldCategory("Statistics")]
        [UnrealFieldDisplayName("Origin")]
        public OriginType Origin;

        [UnrealFieldOffset(0x031)]
        [UnrealFieldCategory("Statistics")]
        [UnrealFieldDisplayName("Notoriety")]
        public NotorietyType Notoriety;

        [UnrealFieldOffset(0x034)]
        [UnrealFieldCategory("Statistics")]
        [UnrealFieldDisplayName("Talent Points")]
        public int TalentPoints;

        [UnrealFieldOffset(0x038)]
        [UnrealFieldCategory("Powers")]
        [UnrealFieldDisplayName("Mapped Power #1")]
        public string MappedPower1;

        [UnrealFieldOffset(0x044)]
        [UnrealFieldCategory("Powers")]
        [UnrealFieldDisplayName("Mapped Power #2")]
        public string MappedPower2;

        [UnrealFieldOffset(0x058)]
        [UnrealFieldCategory("Powers")]
        [UnrealFieldDisplayName("Mapped Power #3")]
        public string MappedPower3;

        [UnrealFieldOffset(0x05C)]
        [UnrealFieldCategory("Player")]
        [UnrealFieldDisplayName("Appearance")]
        public Appearance Appearance;

        [UnrealFieldOffset(0x11C)]
        [UnrealFieldCategory("Powers")]
        [UnrealFieldDisplayName("Powers")]
        public List<Power> Powers;

        [UnrealFieldOffset(0x128)]
        [UnrealFieldCategory("Inventory")]
        [UnrealFieldDisplayName("Weapons")]
        public List<Weapon> Weapons;

        [UnrealFieldOffset(0x134)]
        [UnrealFieldCategory("Inventory")]
        [UnrealFieldDisplayName("Loadout")]
        public Loadout LoadoutWeapons;

        [UnrealFieldOffset(0x17C)]
        [UnrealFieldCategory("Other")]
        [UnrealFieldDisplayName("HotKeys")]
        public List<HotKey> HotKeys;

        [UnrealFieldOffset(0x188)]
        [UnrealFieldCategory("Resources")]
        [UnrealFieldDisplayName("Credits")]
        public int Credits;

        [UnrealFieldOffset(0x18C)]
        [UnrealFieldCategory("Resources")]
        [UnrealFieldDisplayName("Medigel")]
        public int Medigel;

        [UnrealFieldOffset(0x190)]
        [UnrealFieldCategory("Resources")]
        [UnrealFieldDisplayName("Element Zero")]
        public int Eezo;

        [UnrealFieldOffset(0x194)]
        [UnrealFieldCategory("Resources")]
        [UnrealFieldDisplayName("Iridium")]
        public int Iridium;

        [UnrealFieldOffset(0x198)]
        [UnrealFieldCategory("Resources")]
        [UnrealFieldDisplayName("Palladium")]
        public int Palladium;

        [UnrealFieldOffset(0x19C)]
        [UnrealFieldCategory("Resources")]
        [UnrealFieldDisplayName("Platinum")]
        public int Platinum;

        [UnrealFieldOffset(0x1A0)]
        [UnrealFieldCategory("Resources")]
        [UnrealFieldDisplayName("Probes")]
        public int Probes;

        [UnrealFieldOffset(0x1A4)]
        [UnrealFieldCategory("Resources")]
        [UnrealFieldDisplayName("Current Fuel")]
        public float CurrentFuel;

        [UnrealFieldOffset(0x1A8)]
        [UnrealFieldCategory("Player")]
        [UnrealFieldDisplayName("Face Code")]
        public string FaceCode;

        [UnrealFieldOffset(0x014)]
        [UnrealFieldCategory("Statistics")]
        [UnrealFieldDisplayName("Class Friendly Name")]
        [UnrealFieldDescription("String ID of the player's class.")]
        public int ClassFriendlyName;

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this.IsFemale);
            stream.Serialize(ref this.PlayerClassName);
            stream.Serialize(ref this.Level);
            stream.Serialize(ref this.CurrentXP);
            stream.Serialize(ref this.FirstName);
            stream.Serialize(ref this.LastName);
            stream.SerializeEnum(ref this.Origin);
            stream.SerializeEnum(ref this.Notoriety);
            stream.Serialize(ref this.TalentPoints);
            stream.Serialize(ref this.MappedPower1);
            stream.Serialize(ref this.MappedPower2);
            stream.Serialize(ref this.MappedPower3);
            stream.Serialize(ref this.Appearance);
            stream.Serialize(ref this.Powers);
            stream.Serialize(ref this.Weapons);

            if (stream.Version >= 18)
            {
                stream.Serialize(ref this.LoadoutWeapons);
            }

            if (stream.Version >= 19)
            {
                stream.Serialize(ref this.HotKeys);
            }

            stream.Serialize(ref this.Credits);
            stream.Serialize(ref this.Medigel);
            stream.Serialize(ref this.Eezo);
            stream.Serialize(ref this.Iridium);
            stream.Serialize(ref this.Palladium);
            stream.Serialize(ref this.Platinum);
            stream.Serialize(ref this.Probes);
            stream.Serialize(ref this.CurrentFuel);

            if (stream.Version >= 25)
            {
                stream.Serialize(ref this.FaceCode);
            }
            else
            {
                throw new Exception();
            }

            if (stream.Version >= 26)
            {
                stream.Serialize(ref this.ClassFriendlyName);
            }
        }
    }
}
