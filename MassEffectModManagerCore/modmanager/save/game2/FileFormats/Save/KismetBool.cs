using System;

namespace MassEffectModManagerCore.modmanager.save.game2.FileFormats.Save
{
    // 00BB0C50
    public class KismetBool : IUnrealSerializable
    {
        public Guid BoolGUID;
        public bool Value;

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this.BoolGUID);
            stream.Serialize(ref this.Value);
        }

        public override string ToString()
        {
            return String.Format("{0} = {1}",
                this.BoolGUID,
                this.Value);
        }
    }
}
