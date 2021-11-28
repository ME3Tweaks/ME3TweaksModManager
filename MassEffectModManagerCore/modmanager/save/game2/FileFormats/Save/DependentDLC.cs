using System;

namespace ME3TweaksModManager.modmanager.save.game2.FileFormats.Save
{
    // 00BAB3B0
    public class DependentDLC : IUnrealSerializable
    {
        public int ModuleID;
        public string Name;

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this.ModuleID);
            stream.Serialize(ref this.Name);
        }

        public override string ToString()
        {
            return String.Format("{1} ({0})",
                this.ModuleID,
                this.Name);
        }
    }
}
