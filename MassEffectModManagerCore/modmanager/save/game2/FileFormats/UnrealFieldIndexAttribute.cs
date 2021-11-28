using System;

namespace ME3TweaksModManager.modmanager.save.game2.FileFormats
{
    public class UnrealFieldIndexAttribute : Attribute
    {
        public uint Index;

        public UnrealFieldIndexAttribute(uint index)
        {
            this.Index = index;
        }
    }
}
