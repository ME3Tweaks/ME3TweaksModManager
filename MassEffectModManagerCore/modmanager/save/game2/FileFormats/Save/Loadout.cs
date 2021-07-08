using System.ComponentModel;

namespace MassEffectModManagerCore.modmanager.save.game2.FileFormats.Save
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public partial class Loadout : IUnrealSerializable
    {
        [UnrealFieldDisplayName("Unknown #1")]
        public string Unknown0;

        [UnrealFieldDisplayName("Unknown #2")]
        public string Unknown1;

        [UnrealFieldDisplayName("Unknown #3")]
        public string Unknown2;

        [UnrealFieldDisplayName("Unknown #4")]
        public string Unknown3;

        [UnrealFieldDisplayName("Unknown #5")]
        public string Unknown4;

        [UnrealFieldDisplayName("Unknown #6")]
        public string Unknown5;

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this.Unknown0);
            stream.Serialize(ref this.Unknown1);
            stream.Serialize(ref this.Unknown2);
            stream.Serialize(ref this.Unknown3);
            stream.Serialize(ref this.Unknown4);
            stream.Serialize(ref this.Unknown5);
        }
    }
}
