using System;

namespace MassEffectModManagerCore.modmanager.save.game2.FileFormats
{
    public class UnrealFieldDescriptionAttribute : Attribute
    {
        public string Description;

        public UnrealFieldDescriptionAttribute(string description)
        {
            this.Description = description;
        }
    }
}
