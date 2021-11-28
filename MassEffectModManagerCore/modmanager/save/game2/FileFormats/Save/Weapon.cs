using System.ComponentModel;

namespace ME3TweaksModManager.modmanager.save.game2.FileFormats.Save
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public partial class Weapon : IUnrealSerializable
    {
        [UnrealFieldOffset(0x00)]
        [UnrealFieldDisplayName("Class Name")]
        public string WeaponClassName;

        [UnrealFieldOffset(0x0C)]
        [UnrealFieldDisplayName("Ammo Used Count")]
        public int AmmoUsedCount;

        [UnrealFieldOffset(0x10)]
        [UnrealFieldDisplayName("Ammo Total")]
        public int TotalAmmo;

        [UnrealFieldOffset(0x14)]
        [UnrealFieldIndex(0)]
        [UnrealFieldDisplayName("Current Weapon")]
        public bool CurrentWeapon;

        [UnrealFieldOffset(0x14)]
        [UnrealFieldIndex(1)]
        [UnrealFieldDisplayName("Last Weapon")]
        public bool LastWeapon;

        [UnrealFieldOffset(0x18)]
        [UnrealFieldDisplayName("Ammo Power Name")]
        public string AmmoPowerName;

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this.WeaponClassName);
            stream.Serialize(ref this.AmmoUsedCount);
            stream.Serialize(ref this.TotalAmmo);
            stream.Serialize(ref this.CurrentWeapon);
            stream.Serialize(ref this.LastWeapon);

            if (stream.Version >= 17)
            {
                stream.Serialize(ref this.AmmoPowerName);
            }
        }

        public override string ToString()
        {
            return this.WeaponClassName;
        }
    }
}
