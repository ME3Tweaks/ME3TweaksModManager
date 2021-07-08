using System;
using System.ComponentModel;

namespace MassEffectModManagerCore.modmanager.save.game2.FileFormats.Save
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public partial class Power : IUnrealSerializable
    {
        [UnrealFieldOffset(0x00)]
        [UnrealFieldDisplayName("Name")]
        public string PowerName;

        [UnrealFieldOffset(0x0C)]
        [UnrealFieldDisplayName("Current Rank")]
        public float CurrentRank;

        [UnrealFieldOffset(0x10)]
        [UnrealFieldDisplayName("Class Name")]
        public string PowerClassName;

        [UnrealFieldOffset(0x1C)]
        [UnrealFieldDisplayName("Wheel Display Index")]
        public int WheelDisplayIndex;

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this.PowerName);
            stream.Serialize(ref this.CurrentRank);
            stream.Serialize(ref this.PowerClassName);
            stream.Serialize(ref this.WheelDisplayIndex);
        }

        public override string ToString()
        {
            return String.Format("{0} = {1} ({2})",
                this.PowerName,
                this.CurrentRank,
                this.WheelDisplayIndex);
        }
    }
}
