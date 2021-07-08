using System;
using System.ComponentModel;

namespace MassEffectModManagerCore.modmanager.save.game2.FileFormats.Save
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public partial class VectorParameter : IUnrealSerializable
    {
        [UnrealFieldDisplayName("Name")]
        public string Name;

        [UnrealFieldDisplayName("Value")]
        public LinearColor Value;

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this.Name);
            stream.Serialize<LinearColor>(ref this.Value);
        }

        public override string ToString()
        {
            return String.Format("{0} = {1}",
                this.Name,
                this.Value);
        }
    }
}
