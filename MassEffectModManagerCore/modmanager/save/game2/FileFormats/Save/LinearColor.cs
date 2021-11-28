using System;
using System.ComponentModel;

namespace ME3TweaksModManager.modmanager.save.game2.FileFormats.Save
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public partial class LinearColor : IUnrealSerializable
    {
        [UnrealFieldDisplayName("R")]
        public float R;

        [UnrealFieldDisplayName("G")]
        public float G;

        [UnrealFieldDisplayName("B")]
        public float B;

        [UnrealFieldDisplayName("A")]
        public float A;

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this.R);
            stream.Serialize(ref this.G);
            stream.Serialize(ref this.B);
            stream.Serialize(ref this.A);
        }

        public override string ToString()
        {
            return String.Format("{0}, {1}, {2}, {3}",
                this.R,
                this.G,
                this.B,
                this.A);
        }
    }
}
