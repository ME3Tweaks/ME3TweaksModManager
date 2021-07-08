using System.Collections.Generic;
using System.ComponentModel;

namespace MassEffectModManagerCore.modmanager.save.game2.FileFormats.Save
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public partial class Henchman : IUnrealSerializable
    {
        [UnrealFieldOffset(0x00)]
        [UnrealFieldDisplayName("Tag")]
        public string Tag;

        [UnrealFieldOffset(0x0C)]
        [UnrealFieldDisplayName("Powers")]
        public List<Power> Powers;

        [UnrealFieldOffset(0x18)]
        [UnrealFieldDisplayName("Level")]
        public int CharacterLevel;

        [UnrealFieldOffset(0x1C)]
        [UnrealFieldDisplayName("Talent Points")]
        public int TalentPoints;

        [UnrealFieldOffset(0x20)]
        [UnrealFieldDisplayName("Loadout")]
        public Loadout LoadoutWeapons;

        [UnrealFieldOffset(0x68)]
        [UnrealFieldDisplayName("Mapped Power")]
        public string MappedPower; // +68

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this.Tag);
            stream.Serialize(ref this.Powers);
            stream.Serialize(ref this.CharacterLevel);
            stream.Serialize(ref this.TalentPoints);

            if (stream.Version >= 23)
            {
                stream.Serialize(ref this.LoadoutWeapons);
            }

            if (stream.Version >= 29)
            {
                stream.Serialize(ref this.MappedPower);
            }
        }

        public override string ToString()
        {
            return this.Tag;
        }
    }
}
