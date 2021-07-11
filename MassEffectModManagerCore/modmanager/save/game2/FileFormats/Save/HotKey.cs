namespace MassEffectModManagerCore.modmanager.save.game2.FileFormats.Save
{
    // 00BB0C10
    public class HotKey : IUnrealSerializable
    {
        public string PawnName;
        public int PowerID;

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this.PawnName);
            stream.Serialize(ref this.PowerID);
        }
    }
}
