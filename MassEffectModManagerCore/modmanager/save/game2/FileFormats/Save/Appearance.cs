using System.ComponentModel;

namespace ME3TweaksModManager.modmanager.save.game2.FileFormats.Save
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public partial class Appearance : IUnrealSerializable
    {
        [UnrealFieldOffset(0x00)]
        [UnrealFieldDisplayName("Combat Appearance")]
        public PlayerAppearanceType CombatAppearance;

        [UnrealFieldOffset(0x04)]
        [UnrealFieldDisplayName("Casual ID")]
        public int CasualID;

        [UnrealFieldOffset(0x08)]
        [UnrealFieldDisplayName("Full Body ID")]
        public int FullBodyID;

        [UnrealFieldOffset(0x0C)]
        [UnrealFieldDisplayName("Torso ID")]
        public int TorsoID;

        [UnrealFieldOffset(0x10)]
        [UnrealFieldDisplayName("Shoulder ID")]
        public int ShoulderID;

        [UnrealFieldOffset(0x14)]
        [UnrealFieldDisplayName("Arm ID")]
        public int ArmID;

        [UnrealFieldOffset(0x18)]
        [UnrealFieldDisplayName("Leg ID")]
        public int LegID;

        [UnrealFieldOffset(0x1C)]
        [UnrealFieldDisplayName("Spec ID")]
        public int SpecID;

        [UnrealFieldOffset(0x20)]
        [UnrealFieldDisplayName("Tint #1 ID")]
        public int Tint1ID;

        [UnrealFieldOffset(0x24)]
        [UnrealFieldDisplayName("Tint #2 ID")]
        public int Tint2ID;

        [UnrealFieldOffset(0x28)]
        [UnrealFieldDisplayName("Tint #3 ID")]
        public int Tint3ID;

        [UnrealFieldOffset(0x2C)]
        [UnrealFieldDisplayName("Pattern ID")]
        public int PatternID;

        [UnrealFieldOffset(0x30)]
        [UnrealFieldDisplayName("Pattern Color ID ID")]
        public int PatternColorID;

        [UnrealFieldOffset(0x34)]
        [UnrealFieldDisplayName("Helmet ID")]
        public int HelmetID;

        [UnrealFieldOffset(0x38)]
        [UnrealFieldDisplayName("Has Morph Head")]
        public bool HasMorphHead;

        [UnrealFieldOffset(0x3C)]
        [UnrealFieldDisplayName("Morph Head")]
        public MorphHead MorphHead;

        public void Serialize(IUnrealStream stream)
        {
            stream.SerializeEnum(ref this.CombatAppearance);
            stream.Serialize(ref this.CasualID);
            stream.Serialize(ref this.FullBodyID);
            stream.Serialize(ref this.TorsoID);
            stream.Serialize(ref this.ShoulderID);
            stream.Serialize(ref this.ArmID);
            stream.Serialize(ref this.LegID);
            stream.Serialize(ref this.SpecID);
            stream.Serialize(ref this.Tint1ID);
            stream.Serialize(ref this.Tint2ID);
            stream.Serialize(ref this.Tint3ID);
            stream.Serialize(ref this.PatternID);
            stream.Serialize(ref this.PatternColorID);
            stream.Serialize(ref this.HelmetID);
            stream.Serialize(ref this.HasMorphHead);
            
            if (this.HasMorphHead == true)
            {
                stream.Serialize(ref this.MorphHead);
            }
        }
    }
}
