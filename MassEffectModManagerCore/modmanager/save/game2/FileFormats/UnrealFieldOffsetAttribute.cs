using System;

namespace MassEffectModManagerCore.modmanager.save.game2.FileFormats
{
    public class UnrealFieldOffsetAttribute : Attribute
    {
        public uint Offset;

        public UnrealFieldOffsetAttribute(uint offset)
        {
            this.Offset = offset;
        }
    }
}
