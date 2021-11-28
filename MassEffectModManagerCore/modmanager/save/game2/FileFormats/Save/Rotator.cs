using System;
using System.ComponentModel;

namespace ME3TweaksModManager.modmanager.save.game2.FileFormats.Save
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public partial class Rotator : IUnrealSerializable
    {
        [UnrealFieldDisplayName("Pitch")]
        public int Pitch;

        [UnrealFieldDisplayName("Yaw")]
        public int Yaw;

        [UnrealFieldDisplayName("Roll")]
        public int Roll;

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this.Pitch);
            stream.Serialize(ref this.Yaw);
            stream.Serialize(ref this.Roll);
        }

        public override string ToString()
        {
            return String.Format("{0}, {1}, {2}",
                this.Pitch,
                this.Yaw,
                this.Roll);
        }
    }
}
