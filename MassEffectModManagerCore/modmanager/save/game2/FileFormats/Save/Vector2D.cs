using System;
using System.ComponentModel;

namespace ME3TweaksModManager.modmanager.save.game2.FileFormats.Save
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public partial class Vector2D : IUnrealSerializable
    {
        [UnrealFieldDisplayName("X")]
        public float X;

        [UnrealFieldDisplayName("Y")]
        public float Y;

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this.X);
            stream.Serialize(ref this.Y);
        }

        public override string ToString()
        {
            return String.Format("{0}, {1}",
                this.X,
                this.Y);
        }
    }
}
