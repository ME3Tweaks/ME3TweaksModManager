using System;
using System.ComponentModel;

namespace ME3TweaksModManager.modmanager.save.game2.FileFormats.Save
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public partial class OffsetBone : IUnrealSerializable
    {
        [UnrealFieldDisplayName("Name")]
        public string Name;

        [UnrealFieldDisplayName("Offset")]
        public Vector Offset;

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this.Name);
            stream.Serialize<Vector>(ref this.Offset);
        }

        public override string ToString()
        {
            return String.Format("{0} = {1}",
                this.Name,
                this.Offset);
        }
    }
}
