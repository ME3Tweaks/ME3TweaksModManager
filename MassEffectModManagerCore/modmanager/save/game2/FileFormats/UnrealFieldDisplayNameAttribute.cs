using System;

namespace ME3TweaksModManager.modmanager.save.game2.FileFormats
{
    public class UnrealFieldDisplayNameAttribute : Attribute
    {
        public string Name;

        public UnrealFieldDisplayNameAttribute(string name)
        {
            this.Name = name;
        }
    }
}
